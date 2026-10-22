using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Ausbildung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

[Authorize(Policy = Berechtigungen.AusbildungKataloge)]
public class QualifikationModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public bool IstNeu => Formular.Id is null;

    public IReadOnlyList<SelectListItem> Vorstufen { get; private set; } = [];

    public static IReadOnlyList<SelectListItem> Arten { get; } =
        Enum.GetValues<QualifikationArt>().Select(a => new SelectListItem(Anzeige.Text(a), a.ToString())).ToList();

    public static IReadOnlyList<SelectListItem> Ebenen { get; } =
        Enum.GetValues<AusbildungsEbene>().Select(e => new SelectListItem(Anzeige.Text(e), e.ToString())).ToList();

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }
        public uint Version { get; set; }

        [Required(ErrorMessage = "Code fehlt."), StringLength(20), Display(Name = "Code")] public string Code { get; set; } = "";
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")] public string Bezeichnung { get; set; } = "";
        [StringLength(20), Display(Name = "Kurzbezeichnung")] public string? Kurzbezeichnung { get; set; }
        [Display(Name = "Art")] public QualifikationArt Art { get; set; }
        [Display(Name = "Ebene")] public AusbildungsEbene? Ebene { get; set; }
        [Range(1, 240, ErrorMessage = "1 bis 240 Monate."), Display(Name = "Gültigkeit (Monate, leer = unbefristet)")] public int? GueltigkeitMonate { get; set; }
        [StringLength(20), Display(Name = "alamos-Funktion")] public string? Fe2Funktion { get; set; }
        [Display(Name = "Vorstufe")] public Guid? VorstufeId { get; set; }
        [Range(0, 10, ErrorMessage = "0 bis 10 Jahre."), Display(Name = "Abstand zur Vorstufe (Kalenderjahre)")] public int? VorstufeAbstandJahre { get; set; }
        [StringLength(100), Display(Name = "Nötige Qualifikationen (Codes)")] public string? Voraussetzungen { get; set; }
        [Range(0, 99, ErrorMessage = "0 bis 99."), Display(Name = "Mindestalter")] public int? MindestalterJahre { get; set; }
        [Range(0, 99, ErrorMessage = "0 bis 99."), Display(Name = "Höchstalter")] public int? HoechstalterJahre { get; set; }
        [Display(Name = "Alter nach Jahrgang")] public bool AlterNachJahrgang { get; set; }
        [Range(1, 240, ErrorMessage = "1 bis 240 Monate."), Display(Name = "Mindestens Monate in der Jugendfeuerwehr")] public int? MindestJugendMonate { get; set; }
        [Display(Name = "Sortierung")] public int Sortierung { get; set; } = 100;
        [Display(Name = "Aktiv")] public bool Aktiv { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        await VorstufenLadenAsync(id, ct);
        if (id is null)
        {
            return Page();
        }

        var q = await db.Qualifikationen.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (q is null)
        {
            return NotFound();
        }

        Formular = new Eingabe
        {
            Id = q.Id,
            Version = db.Entry(q).Property<uint>("Version").CurrentValue,
            Code = q.Code,
            Bezeichnung = q.Bezeichnung,
            Kurzbezeichnung = q.Kurzbezeichnung,
            Art = q.Art,
            Ebene = q.Ebene,
            GueltigkeitMonate = q.GueltigkeitMonate,
            Fe2Funktion = q.Fe2Funktion,
            VorstufeId = q.VorstufeId,
            VorstufeAbstandJahre = q.VorstufeAbstandJahre,
            Voraussetzungen = q.Voraussetzungen,
            MindestalterJahre = q.MindestalterJahre,
            HoechstalterJahre = q.HoechstalterJahre,
            AlterNachJahrgang = q.AlterNachJahrgang,
            MindestJugendMonate = q.MindestJugendMonate,
            Sortierung = q.Sortierung,
            Aktiv = q.Aktiv,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var code = Formular.Code.Trim().ToUpperInvariant();
        if (await db.Qualifikationen.IgnoreQueryFilters().AnyAsync(q => q.Code == code && q.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Code", "Dieser Code ist schon vergeben.");
        }

        var katalog = await db.Qualifikationen.AsNoTracking().ToDictionaryAsync(q => q.Id, ct);
        var codes = katalog.Values.Select(q => q.Code).Append(code).ToHashSet();
        var unbekannt = Abzeichenpruefung.Gruppen(Formular.Voraussetzungen).SelectMany(g => g).Where(c => !codes.Contains(c)).ToList();
        if (unbekannt.Count > 0)
        {
            ModelState.AddModelError("Formular.Voraussetzungen", "Unbekannte Codes: " + string.Join(", ", unbekannt));
        }

        // Eine Vorstufe darf nicht wieder auf diese Qualifikation zurückführen.
        for (var stufe = Formular.VorstufeId; stufe is { } s; stufe = katalog.GetValueOrDefault(s)?.VorstufeId)
        {
            if (s == Formular.Id)
            {
                ModelState.AddModelError("Formular.VorstufeId", "Die Stufenfolge darf keinen Kreis bilden.");
                break;
            }
        }

        if (!ModelState.IsValid)
        {
            await VorstufenLadenAsync(Formular.Id, ct);
            return Page();
        }

        Qualifikation q;
        if (IstNeu)
        {
            q = new Qualifikation { Code = code, Bezeichnung = "" };
            db.Qualifikationen.Add(q);
        }
        else
        {
            var vorhanden = await db.Qualifikationen.FirstOrDefaultAsync(x => x.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            q = vorhanden;
            db.Entry(q).Property<uint>("Version").OriginalValue = Formular.Version;

            // Der Code AGT steuert die Atemschutzfristen; umbenennen würde sie stillschweigend abschalten.
            if (q.Code == Qualifikation.CodeAtemschutz && code != q.Code)
            {
                ModelState.AddModelError("Formular.Code", $"Der Code {Qualifikation.CodeAtemschutz} wird für die Atemschutzfristen gebraucht und kann nicht geändert werden.");
                await VorstufenLadenAsync(Formular.Id, ct);
                return Page();
            }
        }

        q.Code = code;
        q.Bezeichnung = Formular.Bezeichnung.Trim();
        q.Kurzbezeichnung = string.IsNullOrWhiteSpace(Formular.Kurzbezeichnung) ? null : Formular.Kurzbezeichnung.Trim();
        q.Art = Formular.Art;
        q.Ebene = Formular.Ebene;
        q.GueltigkeitMonate = Formular.GueltigkeitMonate;
        q.Fe2Funktion = string.IsNullOrWhiteSpace(Formular.Fe2Funktion) ? null : Formular.Fe2Funktion.Trim();
        q.VorstufeId = Formular.VorstufeId;
        q.VorstufeAbstandJahre = Formular.VorstufeId is null ? null : Formular.VorstufeAbstandJahre;
        q.Voraussetzungen = Abzeichenpruefung.Gruppen(Formular.Voraussetzungen) is { Count: > 0 } gruppen
            ? string.Join("; ", gruppen.Select(g => string.Join("|", g)))
            : null;
        q.MindestalterJahre = Formular.MindestalterJahre;
        q.HoechstalterJahre = Formular.HoechstalterJahre;
        q.AlterNachJahrgang = Formular.AlterNachJahrgang;
        q.MindestJugendMonate = Formular.MindestJugendMonate;
        q.Sortierung = Formular.Sortierung;
        q.Aktiv = Formular.Aktiv;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Der Eintrag wurde inzwischen geändert. Bitte neu laden.");
            await VorstufenLadenAsync(Formular.Id, ct);
            return Page();
        }

        TempData["Meldung"] = $"{q.Bezeichnung} gespeichert.";
        return RedirectToPage("Kataloge");
    }

    private async Task VorstufenLadenAsync(Guid? id, CancellationToken ct) =>
        Vorstufen = await db.Qualifikationen.AsNoTracking()
            .Where(q => q.Id != id)
            .OrderBy(q => q.Art).ThenBy(q => q.Sortierung)
            .Select(q => new SelectListItem(q.Bezeichnung, q.Id.ToString()))
            .ToListAsync(ct);
}
