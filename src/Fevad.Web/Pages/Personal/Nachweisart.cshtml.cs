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
public class NachweisartModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public bool IstNeu => Formular.Id is null;
    public IReadOnlyList<SelectListItem> Qualifikationen { get; private set; } = [];

    public static IReadOnlyList<SelectListItem> Pflichten { get; } =
        Enum.GetValues<NachweisPflicht>().Select(p => new SelectListItem(Anzeige.Text(p), p.ToString())).ToList();

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }
        public uint Version { get; set; }

        [Required(ErrorMessage = "Code fehlt."), StringLength(20), Display(Name = "Code")] public string Code { get; set; } = "";
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")] public string Bezeichnung { get; set; } = "";
        [Range(1, 120, ErrorMessage = "1 bis 120 Monate."), Display(Name = "Intervall (Monate)")] public int IntervallMonate { get; set; } = 12;
        [Display(Name = "Pflicht für")] public NachweisPflicht Pflicht { get; set; }
        [Display(Name = "Qualifikation")] public Guid? QualifikationId { get; set; }
        [StringLength(500), Display(Name = "Beschreibung")] public string? Beschreibung { get; set; }
        [Display(Name = "Sortierung")] public int Sortierung { get; set; } = 100;
        [Display(Name = "Aktiv")] public bool Aktiv { get; set; } = true;
        [Display(Name = "Gesundheitsbezug")] public bool Gesundheitsbezogen { get; set; }
        [Display(Name = "Einmalig")] public bool Einmalig { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            var art = await db.Nachweisarten.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (art is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = art.Id,
                Version = db.Entry(art).Property<uint>("Version").CurrentValue,
                Code = art.Code,
                Bezeichnung = art.Bezeichnung,
                IntervallMonate = art.IntervallMonate,
                Pflicht = art.Pflicht,
                QualifikationId = art.QualifikationId,
                Beschreibung = art.Beschreibung,
                Sortierung = art.Sortierung,
                Aktiv = art.Aktiv,
                Gesundheitsbezogen = art.Gesundheitsbezogen,
                Einmalig = art.Einmalig,
            };
        }

        await AuswahlLadenAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var code = Formular.Code.Trim().ToUpperInvariant();
        if (await db.Nachweisarten.IgnoreQueryFilters().AnyAsync(a => a.Code == code && a.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Code", "Dieser Code ist schon vergeben.");
        }

        if (Formular.Pflicht == NachweisPflicht.MitQualifikation && Formular.QualifikationId is null)
        {
            ModelState.AddModelError("Formular.QualifikationId", "Bitte die Qualifikation wählen.");
        }

        if (!ModelState.IsValid)
        {
            await AuswahlLadenAsync(ct);
            return Page();
        }

        Nachweisart art;
        if (IstNeu)
        {
            art = new Nachweisart { Code = code, Bezeichnung = "" };
            db.Nachweisarten.Add(art);
        }
        else
        {
            var vorhanden = await db.Nachweisarten.FirstOrDefaultAsync(a => a.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            art = vorhanden;
            db.Entry(art).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        art.Code = code;
        art.Bezeichnung = Formular.Bezeichnung.Trim();
        art.IntervallMonate = Formular.IntervallMonate;
        art.Pflicht = Formular.Pflicht;
        art.QualifikationId = Formular.Pflicht == NachweisPflicht.MitQualifikation ? Formular.QualifikationId : null;
        art.Beschreibung = string.IsNullOrWhiteSpace(Formular.Beschreibung) ? null : Formular.Beschreibung.Trim();
        art.Sortierung = Formular.Sortierung;
        art.Aktiv = Formular.Aktiv;
        art.Gesundheitsbezogen = Formular.Gesundheitsbezogen;
        art.Einmalig = Formular.Einmalig;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Der Eintrag wurde inzwischen geändert. Bitte neu laden.");
            await AuswahlLadenAsync(ct);
            return Page();
        }

        TempData["Meldung"] = $"{art.Bezeichnung} gespeichert. Bestehende Nachweise behalten ihr Ablaufdatum.";
        return RedirectToPage("Kataloge");
    }

    private async Task AuswahlLadenAsync(CancellationToken ct) =>
        Qualifikationen = await db.Qualifikationen.AsNoTracking()
            .OrderBy(q => q.Art).ThenBy(q => q.Sortierung)
            .Select(q => new SelectListItem(q.Bezeichnung, q.Id.ToString()))
            .ToListAsync(ct);
}
