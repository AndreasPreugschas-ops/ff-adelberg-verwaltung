using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Artikel anlegen und ändern; Lagerzugang, Mindestmengen und neue Einzelstücke.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class ArtikelBearbeitenModel(AppDbContext db, Kleiderkammerbuchungen buchungen, Kleiderkammerstand stand, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public bool IstNeu => Formular.Id is null;
    public bool DarfBearbeiten { get; private set; }
    public bool HatBewegungen { get; private set; }
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];
    public IReadOnlyList<BestandZeile> Bestand { get; private set; } = [];
    public IReadOnlyList<PsaStand> Stuecke { get; private set; } = [];

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }
        public uint Version { get; set; }

        [Required(ErrorMessage = "Code fehlt."), StringLength(20), Display(Name = "Code")]
        public string Code { get; set; } = "";

        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")]
        public string Bezeichnung { get; set; } = "";

        [Display(Name = "Kategorie")] public ArtikelKategorie Kategorie { get; set; }
        [Display(Name = "Größe nach")] public Groessenart Groessenart { get; set; }

        [Display(Name = "Einzelstück als Geräteart")]
        public Guid? GeraeteartId { get; set; }

        [Display(Name = "Grundausstattung für")]
        public AbteilungArt? GrundausstattungArt { get; set; }

        [StringLength(100), Display(Name = "Norm")] public string? Norm { get; set; }
        [Display(Name = "Sortierung")] public int Sortierung { get; set; }
        [Display(Name = "aktiv")] public bool Aktiv { get; set; } = true;
        [Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            var artikel = await db.Artikel.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (artikel is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = artikel.Id,
                Version = db.Entry(artikel).Property<uint>("Version").CurrentValue,
                Code = artikel.Code,
                Bezeichnung = artikel.Bezeichnung,
                Kategorie = artikel.Kategorie,
                Groessenart = artikel.Groessenart,
                GeraeteartId = artikel.GeraeteartId,
                GrundausstattungArt = artikel.GrundausstattungArt,
                Norm = artikel.Norm,
                Sortierung = artikel.Sortierung,
                Aktiv = artikel.Aktiv,
                Bemerkung = artikel.Bemerkung,
            };
        }

        await LadenAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var code = Formular.Code.Trim().ToUpperInvariant();
        if (await db.Artikel.IgnoreQueryFilters().AnyAsync(a => a.Code == code && a.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Code", "Dieser Code ist schon vergeben.");
        }

        if (Formular.GeraeteartId is not null && await db.Artikel.AnyAsync(a => a.GeraeteartId == Formular.GeraeteartId && a.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.GeraeteartId", "Diese Geräteart gehört schon zu einem anderen Artikel.");
        }

        Artikel? artikel = null;
        if (!IstNeu)
        {
            artikel = await db.Artikel.FirstOrDefaultAsync(a => a.Id == Formular.Id, ct);
            if (artikel is null)
            {
                return NotFound();
            }

            // Wechsel zwischen Menge und Einzelstück würde Bestand und Ausstattung verfälschen.
            if (artikel.GeraeteartId != Formular.GeraeteartId && await BewegungenAsync(artikel.Id, ct))
            {
                ModelState.AddModelError("Formular.GeraeteartId", "Die Führung kann nicht mehr geändert werden, weil schon Bestand oder Ausgaben gebucht sind.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        if (artikel is null)
        {
            artikel = new Artikel { Code = code, Bezeichnung = Formular.Bezeichnung.Trim() };
            db.Artikel.Add(artikel);
        }
        else
        {
            db.Entry(artikel).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        artikel.Code = code;
        artikel.Bezeichnung = Formular.Bezeichnung.Trim();
        artikel.Kategorie = Formular.Kategorie;
        artikel.Groessenart = Formular.Groessenart;
        artikel.GeraeteartId = Formular.GeraeteartId;
        artikel.GrundausstattungArt = Formular.GrundausstattungArt;
        artikel.Norm = string.IsNullOrWhiteSpace(Formular.Norm) ? null : Formular.Norm.Trim();
        artikel.Sortierung = Formular.Sortierung;
        artikel.Aktiv = Formular.Aktiv;
        artikel.Bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Fehler"] = "Der Artikel wurde inzwischen von jemand anderem geändert. Bitte erneut bearbeiten.";
            return RedirectToPage(new { id = artikel.Id });
        }

        TempData["Meldung"] = "Artikel gespeichert.";
        return RedirectToPage(new { id = artikel.Id });
    }

    public Task<IActionResult> OnPostZugangAsync(Guid id, string? groesse, int menge, CancellationToken ct) =>
        BuchenAsync(id, () => buchungen.ZugangBuchenAsync(id, groesse ?? "", menge, ct),
            menge >= 0 ? $"Zugang von {menge} Stück gebucht." : $"Abgang von {-menge} Stück gebucht.");

    public Task<IActionResult> OnPostMindestmengeAsync(Guid id, string? groesse, int mindestmenge, CancellationToken ct) =>
        BuchenAsync(id, () => buchungen.MindestmengeSetzenAsync(id, groesse ?? "", mindestmenge, ct), "Mindestmenge gespeichert.");

    public Task<IActionResult> OnPostStueckAsync(Guid id, string? inventarnummer, string? groesse, string? hersteller, string? seriennummer, DateOnly? herstelldatum, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(inventarnummer)
            ? BuchenAsync(id, () => throw new KleiderkammerFehler("Inventarnummer fehlt."), "")
            : BuchenAsync(id, () => buchungen.EinzelstueckAnlegenAsync(id, inventarnummer, groesse ?? "", Leer(hersteller), Leer(seriennummer), herstelldatum, ct),
                $"Stück {inventarnummer.Trim()} angelegt.");

    private async Task<IActionResult> BuchenAsync(Guid id, Func<Task> buchung, string meldung)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        try
        {
            await buchung();
            TempData["Meldung"] = meldung;
        }
        catch (KleiderkammerFehler fehler)
        {
            TempData["Fehler"] = fehler.Message;
        }

        return RedirectToPage(new { id });
    }

    private async Task<bool> DarfAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded;

    private async Task<bool> BewegungenAsync(Guid artikelId, CancellationToken ct) =>
        await db.Ausstattungen.IgnoreQueryFilters().AnyAsync(a => a.ArtikelId == artikelId, ct)
        || await db.Lagerbestaende.AnyAsync(l => l.ArtikelId == artikelId && l.Menge > 0, ct);

    private async Task LadenAsync(CancellationToken ct)
    {
        DarfBearbeiten = await DarfAsync();
        Geraetearten = await db.Geraetearten.AsNoTracking()
            .Where(g => !g.FuerFahrzeuge && g.Aktiv)
            .OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString()))
            .ToListAsync(ct);

        if (Formular.Id is { } id)
        {
            HatBewegungen = await BewegungenAsync(id, ct);
            Bestand = (await stand.BestandAsync(ct)).Where(b => b.ArtikelId == id).ToList();
            if (Formular.GeraeteartId is not null)
            {
                Stuecke = (await stand.PsaAsync(Anzeige.Heute(zeit), ct))
                    .Where(p => p.ArtikelId == id)
                    .OrderBy(p => Kleiderkammerstand.Groessenfolge(p.Groesse ?? "")).ThenBy(p => p.Stand.Nummer)
                    .ToList();
            }
        }
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
