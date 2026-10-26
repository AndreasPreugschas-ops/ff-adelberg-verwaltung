using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
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
    /// <summary>Formulardaten des Artikels.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Die Seite legt einen neuen Artikel an.</summary>
    public bool IstNeu => Formular.Id is null;
    /// <summary>Der Benutzer darf Artikel bearbeiten und buchen.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Zum Artikel gibt es schon Lagerbewegungen oder Ausgaben.</summary>
    public bool HatBewegungen { get; private set; }
    /// <summary>Gerätearten zur Auswahl für Einzelstücke.</summary>
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];
    /// <summary>Lagerbestand des Artikels je Größe.</summary>
    public IReadOnlyList<BestandZeile> Bestand { get; private set; } = [];
    /// <summary>Einzelstücke des Artikels mit Prüf- und Lebensdauerstand.</summary>
    public IReadOnlyList<PsaStand> Stuecke { get; private set; } = [];
    /// <summary>Preise des Artikels je Lieferant.</summary>
    public IReadOnlyList<ArtikelPreis> Preise { get; private set; } = [];
    /// <summary>Preis, mit dem der Haushalt plant (bevorzugt, sonst günstigster).</summary>
    public ArtikelPreis? Massgeblich { get; private set; }
    /// <summary>Aktive Lieferanten zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Lieferanten { get; private set; } = [];

    /// <summary>Eingabefelder eines Artikels.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung des Artikels; <c>null</c> beim Anlegen.</summary>
        public Guid? Id { get; set; }
        /// <summary>Zeilenversion zur Erkennung gleichzeitiger Änderungen.</summary>
        public uint Version { get; set; }

        /// <summary>Kurzcode des Artikels.</summary>
        [Required(ErrorMessage = "Code fehlt."), StringLength(20), Display(Name = "Code")]
        public string Code { get; set; } = "";

        /// <summary>Bezeichnung des Artikels.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")]
        public string Bezeichnung { get; set; } = "";

        /// <summary>Kategorie des Artikels.</summary>
        [Display(Name = "Kategorie")] public ArtikelKategorie Kategorie { get; set; }
        /// <summary>Nach welcher Größe der Artikel ausgegeben wird.</summary>
        [Display(Name = "Größe nach")] public Groessenart Groessenart { get; set; }

        /// <summary>Geräteart, wenn jedes Stück einzeln mit Inventarnummer geführt wird.</summary>
        [Display(Name = "Einzelstück als Geräteart")]
        public Guid? GeraeteartId { get; set; }

        /// <summary>Abteilungsart, zu deren Grundausstattung der Artikel gehört.</summary>
        [Display(Name = "Grundausstattung für")]
        public AbteilungArt? GrundausstattungArt { get; set; }

        /// <summary>Norm des Artikels.</summary>
        [StringLength(100), Display(Name = "Norm")] public string? Norm { get; set; }
        /// <summary>Sortierschlüssel.</summary>
        [Display(Name = "Sortierung")] public int Sortierung { get; set; }
        /// <summary>Der Artikel wird noch ausgegeben.</summary>
        [Display(Name = "aktiv")] public bool Aktiv { get; set; } = true;
        /// <summary>Bemerkung.</summary>
        [Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt einen Artikel oder ein leeres Formular.</summary>
    /// <param name="id">Kennung des Artikels; <c>null</c> legt neu an.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
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

    /// <summary>Speichert den Artikel oder legt ihn an.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Artikel, die Seite mit Fehlern oder 403.</returns>
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

    /// <summary>Bucht einen Zugang (positive Menge) oder Abgang (negative Menge) im Lager.</summary>
    /// <param name="id">Kennung des Artikels.</param>
    /// <param name="groesse">Größe; leer bei Artikeln ohne Größe.</param>
    /// <param name="menge">Zu- oder abgehende Stückzahl.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder Fehler; 403 ohne Recht.</returns>
    public Task<IActionResult> OnPostZugangAsync(Guid id, string? groesse, int menge, CancellationToken ct) =>
        BuchenAsync(id, () => buchungen.ZugangBuchenAsync(id, groesse ?? "", menge, ct),
            menge >= 0 ? $"Zugang von {menge} Stück gebucht." : $"Abgang von {-menge} Stück gebucht.");

    /// <summary>Setzt die Mindestmenge einer Größe für die Nachbestellung.</summary>
    /// <param name="id">Kennung des Artikels.</param>
    /// <param name="groesse">Größe; leer bei Artikeln ohne Größe.</param>
    /// <param name="mindestmenge">Mindestbestand.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder Fehler; 403 ohne Recht.</returns>
    public Task<IActionResult> OnPostMindestmengeAsync(Guid id, string? groesse, int mindestmenge, CancellationToken ct) =>
        BuchenAsync(id, () => buchungen.MindestmengeSetzenAsync(id, groesse ?? "", mindestmenge, ct), "Mindestmenge gespeichert.");

    /// <summary>Legt ein neues Einzelstück mit Inventarnummer als Gerät an.</summary>
    /// <param name="id">Kennung des Artikels.</param>
    /// <param name="inventarnummer">Inventarnummer; Pflicht.</param>
    /// <param name="groesse">Größe des Stücks.</param>
    /// <param name="hersteller">Hersteller.</param>
    /// <param name="seriennummer">Seriennummer.</param>
    /// <param name="herstelldatum">Herstelldatum, Grundlage der Lebensdauer.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder Fehler; 403 ohne Recht.</returns>
    public Task<IActionResult> OnPostStueckAsync(Guid id, string? inventarnummer, string? groesse, string? hersteller, string? seriennummer, DateOnly? herstelldatum, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(inventarnummer)
            ? BuchenAsync(id, () => throw new KleiderkammerFehler("Inventarnummer fehlt."), "")
            : BuchenAsync(id, () => buchungen.EinzelstueckAnlegenAsync(id, inventarnummer, groesse ?? "", Leer(hersteller), Leer(seriennummer), herstelldatum, ct),
                $"Stück {inventarnummer.Trim()} angelegt.");

    /// <summary>Erfasst oder ändert den Preis des Artikels bei einem Lieferanten.</summary>
    /// <param name="id">Kennung des Artikels.</param>
    /// <param name="lieferantId">Kennung des Lieferanten.</param>
    /// <param name="preis">Stückpreis brutto, z. B. „189,90“.</param>
    /// <param name="bestellnummer">Artikelnummer beim Lieferanten.</param>
    /// <param name="standAm">Tag des Angebots.</param>
    /// <param name="bevorzugt">Bei diesem Lieferanten wird bestellt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder Fehler; 403 ohne Recht.</returns>
    public Task<IActionResult> OnPostPreisAsync(Guid id, Guid lieferantId, string? preis, string? bestellnummer, DateOnly? standAm, bool bevorzugt, CancellationToken ct) =>
        BuchenAsync(id, async () =>
        {
            if (Entschaedigungssaetze.Lesen(preis) is not { } betrag || betrag < 0)
            {
                throw new KleiderkammerFehler("Preis ungültig, z. B. 189,90.");
            }

            if (!await db.Lieferanten.AnyAsync(l => l.Id == lieferantId && l.Aktiv, ct) || !await db.Artikel.AnyAsync(a => a.Id == id, ct))
            {
                throw new KleiderkammerFehler("Unbekannter Lieferant.");
            }

            var preise = await db.ArtikelPreise.Where(p => p.ArtikelId == id).ToListAsync(ct);
            var eintrag = preise.FirstOrDefault(p => p.LieferantId == lieferantId);
            if (eintrag is null)
            {
                eintrag = new ArtikelPreis { ArtikelId = id, LieferantId = lieferantId };
                db.ArtikelPreise.Add(eintrag);
            }

            eintrag.Preis = Math.Round(betrag, 2);
            eintrag.Bestellnummer = Leer(bestellnummer);
            eintrag.StandAm = standAm;
            eintrag.Bevorzugt = bevorzugt;
            // Nur ein Lieferant ist bevorzugt.
            foreach (var anderer in preise.Where(p => bevorzugt && p != eintrag))
            {
                anderer.Bevorzugt = false;
            }

            await db.SaveChangesAsync(ct);
        }, "Preis gespeichert.");

    /// <summary>Entfernt den Preis eines Lieferanten.</summary>
    /// <param name="id">Kennung des Artikels.</param>
    /// <param name="preisId">Kennung des Preises.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung; 403 ohne Recht.</returns>
    public Task<IActionResult> OnPostPreisEntfernenAsync(Guid id, Guid preisId, CancellationToken ct) =>
        BuchenAsync(id, async () =>
        {
            if (await db.ArtikelPreise.FirstOrDefaultAsync(p => p.Id == preisId && p.ArtikelId == id, ct) is { } preis)
            {
                db.ArtikelPreise.Remove(preis);
                await db.SaveChangesAsync(ct);
            }
        }, "Preis entfernt.");

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
            Preise = await db.ArtikelPreise.AsNoTracking().Include(p => p.Lieferant)
                .Where(p => p.ArtikelId == id).OrderBy(p => p.Preis).ToListAsync(ct);
            Massgeblich = Preiswahl.Massgeblich(Preise);
            Lieferanten = await db.Lieferanten.AsNoTracking().Where(l => l.Aktiv).OrderBy(l => l.Name)
                .Select(l => new SelectListItem(l.Name, l.Id.ToString())).ToListAsync(ct);
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
