using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Beschaffung.Foerderung;

/// <summary>Z-Feu-Antrag zu einer Beschaffung (V30): Statuskette, Fristen, Checkliste Verwendungsnachweis, Zweckbindung ans Fahrzeug.</summary>
public class AntragModel(AppDbContext db, Foerderverfahren verfahren, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Die Beschaffung.</summary>
    public Domain.Technik.Beschaffung Beschaffung { get; private set; } = null!;

    /// <summary>Der Antrag; null, solange keiner angelegt ist.</summary>
    public Foerderantrag? Antrag { get; private set; }

    /// <summary>Fristen des Antrags.</summary>
    public Foerderfristen? Fristen { get; private set; }

    /// <summary>Checkliste des Verwendungsnachweises.</summary>
    public IReadOnlyList<(string Punkt, bool Erfuellt)> Checkliste { get; private set; } = [];

    /// <summary>Nächste Schritte mit dem, was dafür noch fehlt.</summary>
    public IReadOnlyList<(Foerderstatus Ziel, IReadOnlyList<string> Fehlt)> Schritte { get; private set; } = [];

    /// <summary>Fördergegenstände zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Gegenstaende { get; private set; } = [];

    /// <summary>Fahrzeuge zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];

    /// <summary>Ob der Nutzer bearbeiten darf.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Heute in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Formular.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Eingaben zum Antrag.</summary>
    public sealed class Eingabe
    {
        /// <summary>Fördergegenstand.</summary>
        [Display(Name = "Fördergegenstand")] public Guid? FoerdergegenstandId { get; set; }
        /// <summary>Rahmenvertrag.</summary>
        [Display(Name = "Beschaffung über Rahmenvertrag")] public bool Rahmenvertrag { get; set; }
        /// <summary>Antragsjahr.</summary>
        [Range(2025, 2100, ErrorMessage = "Antragsjahr ungültig."), Display(Name = "Antrag zur Frist im Jahr")] public int Antragsjahr { get; set; }
        /// <summary>KBM-Gespräch.</summary>
        [Display(Name = "Vorgespräch KBM am")] public DateOnly? KbmAbgestimmtAm { get; set; }
        /// <summary>Gemeinderatsbeschluss.</summary>
        [Display(Name = "Gemeinderatsbeschluss am")] public DateOnly? GemeinderatsbeschlussAm { get; set; }
        /// <summary>Beschlussnummer.</summary>
        [StringLength(100), Display(Name = "Beschluss-Nr.")] public string? Gemeinderatsbeschluss { get; set; }
        /// <summary>Antragsdatum.</summary>
        [Display(Name = "Beantragt am")] public DateOnly? BeantragtAm { get; set; }
        /// <summary>Bescheid.</summary>
        [Display(Name = "Zuwendungsbescheid vom")] public DateOnly? BescheidAm { get; set; }
        /// <summary>Aktenzeichen.</summary>
        [StringLength(100), Display(Name = "Aktenzeichen")] public string? BescheidAktenzeichen { get; set; }
        /// <summary>Bewilligte Zuwendung als Text mit Komma.</summary>
        [Display(Name = "Bewilligte Zuwendung (€)")] public string? Zuwendung { get; set; }
        /// <summary>Altfahrzeug.</summary>
        [Display(Name = "Ersetzt Fahrzeug")] public Guid? AltfahrzeugId { get; set; }
        /// <summary>Neues Fahrzeug.</summary>
        [Display(Name = "Beschafftes Fahrzeug")] public Guid? FahrzeugId { get; set; }
        /// <summary>Checkliste.</summary>
        public bool RechnungLiegtVor { get; set; }
        /// <summary>Checkliste.</summary>
        public bool RichtigkeitsbescheinigungLiegtVor { get; set; }
        /// <summary>Checkliste.</summary>
        public bool AbnahmeberichtLiegtVor { get; set; }
        /// <summary>Checkliste.</summary>
        public bool BeladungserklaerungLiegtVor { get; set; }
        /// <summary>Nachweis eingereicht.</summary>
        [Display(Name = "Verwendungsnachweis eingereicht am")] public DateOnly? NachweisEingereichtAm { get; set; }
        /// <summary>Auszahlung.</summary>
        [Display(Name = "Ausgezahlt am")] public DateOnly? AusgezahltAm { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(2000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt Antrag oder Angebot zum Anlegen.</summary>
    /// <param name="beschaffungId">Beschaffung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Seite oder 403/404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid beschaffungId, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.BeschaffungLesen))
        {
            return Forbid();
        }

        if (!await LadenAsync(beschaffungId, ct))
        {
            return NotFound();
        }

        if (Antrag is not null)
        {
            Formular = new Eingabe
            {
                FoerdergegenstandId = Antrag.FoerdergegenstandId, Rahmenvertrag = Antrag.Rahmenvertrag, Antragsjahr = Antrag.Antragsjahr,
                KbmAbgestimmtAm = Antrag.KbmAbgestimmtAm, GemeinderatsbeschlussAm = Antrag.GemeinderatsbeschlussAm, Gemeinderatsbeschluss = Antrag.Gemeinderatsbeschluss,
                BeantragtAm = Antrag.BeantragtAm, BescheidAm = Antrag.BescheidAm, BescheidAktenzeichen = Antrag.BescheidAktenzeichen,
                Zuwendung = Antrag.Zuwendung?.ToString("0.00"), AltfahrzeugId = Antrag.AltfahrzeugId, FahrzeugId = Antrag.FahrzeugId,
                RechnungLiegtVor = Antrag.RechnungLiegtVor, RichtigkeitsbescheinigungLiegtVor = Antrag.RichtigkeitsbescheinigungLiegtVor,
                AbnahmeberichtLiegtVor = Antrag.AbnahmeberichtLiegtVor, BeladungserklaerungLiegtVor = Antrag.BeladungserklaerungLiegtVor,
                NachweisEingereichtAm = Antrag.NachweisEingereichtAm, AusgezahltAm = Antrag.AusgezahltAm, Bemerkung = Antrag.Bemerkung,
            };
        }

        return Page();
    }

    /// <summary>Legt den Antrag an; Antragsjahr ist das nächste, dessen Frist noch nicht verstrichen ist.</summary>
    /// <param name="beschaffungId">Beschaffung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 403/404.</returns>
    public async Task<IActionResult> OnPostAnlegenAsync(Guid beschaffungId, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.BeschaffungBearbeiten))
        {
            return Forbid();
        }

        if (!await LadenAsync(beschaffungId, ct))
        {
            return NotFound();
        }

        if (Antrag is null)
        {
            var antrag = new Foerderantrag { BeschaffungId = beschaffungId, Antragsjahr = Heute.Year, AltfahrzeugId = Beschaffung.FahrzeugId };
            var fristen = await verfahren.FristenAsync(antrag, null, Heute, ct);
            if (fristen.Antragsfrist < Heute)
            {
                antrag.Antragsjahr++;
            }

            Beschaffung.Foerderfaehig = true;
            db.Foerderantraege.Add(antrag);
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage(new { beschaffungId });
    }

    /// <summary>Speichert die Angaben und geht auf Wunsch einen Schritt weiter.</summary>
    /// <param name="beschaffungId">Beschaffung.</param>
    /// <param name="ziel">Nächster Stand oder null.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, Seite mit Fehlern oder 403/404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid beschaffungId, Foerderstatus? ziel, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.BeschaffungBearbeiten))
        {
            return Forbid();
        }

        if (!await LadenAsync(beschaffungId, ct, verfolgen: true) || Antrag is null)
        {
            return NotFound();
        }

        var zuwendung = Entschaedigungssaetze.Lesen(Formular.Zuwendung);
        if (!string.IsNullOrWhiteSpace(Formular.Zuwendung) && zuwendung is null or < 0)
        {
            ModelState.AddModelError("Formular.Zuwendung", "Betrag ungültig, z. B. 150000 oder 25000,00.");
        }

        if (Formular.FoerdergegenstandId is { } gid && !await db.Foerdergegenstaende.AnyAsync(g => g.Id == gid, ct))
        {
            ModelState.AddModelError("Formular.FoerdergegenstandId", "Unbekannter Fördergegenstand.");
        }

        foreach (var (feld, id) in new[] { ("Formular.AltfahrzeugId", Formular.AltfahrzeugId), ("Formular.FahrzeugId", Formular.FahrzeugId) })
        {
            if (id is { } fid && !await db.Fahrzeuge.AnyAsync(f => f.Id == fid, ct))
            {
                ModelState.AddModelError(feld, "Unbekanntes Fahrzeug.");
            }
        }

        if (!ModelState.IsValid)
        {
            await AuswahlAsync(ct);
            return Page();
        }

        var a = Antrag;
        a.FoerdergegenstandId = Formular.FoerdergegenstandId;
        a.Rahmenvertrag = Formular.Rahmenvertrag;
        a.Antragsjahr = Formular.Antragsjahr;
        a.KbmAbgestimmtAm = Formular.KbmAbgestimmtAm;
        a.GemeinderatsbeschlussAm = Formular.GemeinderatsbeschlussAm;
        a.Gemeinderatsbeschluss = Kurz(Formular.Gemeinderatsbeschluss);
        a.BeantragtAm = Formular.BeantragtAm;
        a.BescheidAm = Formular.BescheidAm;
        a.BescheidAktenzeichen = Kurz(Formular.BescheidAktenzeichen);
        a.Zuwendung = zuwendung;
        a.AltfahrzeugId = Formular.AltfahrzeugId;
        a.FahrzeugId = Formular.FahrzeugId;
        a.RechnungLiegtVor = Formular.RechnungLiegtVor;
        a.RichtigkeitsbescheinigungLiegtVor = Formular.RichtigkeitsbescheinigungLiegtVor;
        a.AbnahmeberichtLiegtVor = Formular.AbnahmeberichtLiegtVor;
        a.BeladungserklaerungLiegtVor = Formular.BeladungserklaerungLiegtVor;
        a.NachweisEingereichtAm = Formular.NachweisEingereichtAm;
        a.AusgezahltAm = Formular.AusgezahltAm;
        a.Bemerkung = Kurz(Formular.Bemerkung);

        if (ziel is { } z)
        {
            await db.Entry(a).Reference(x => x.Foerdergegenstand).LoadAsync(ct);
            var altAusserDienst = await AltfahrzeugAusserDienstAsync(a.AltfahrzeugId, ct);
            if (!Foerderantrag.Folgestatus(a.Status).Contains(z))
            {
                ModelState.AddModelError(string.Empty, $"Von „{Anzeige.Text(a.Status)}“ geht es nicht nach „{Anzeige.Text(z)}“.");
            }
            else if (a.Fehlt(z, altAusserDienst) is { Count: > 0 } fehlt)
            {
                ModelState.AddModelError(string.Empty, $"Für „{Anzeige.Text(z)}“ fehlt noch: {string.Join(", ", fehlt)}.");
            }
            else if (z == Foerderstatus.NachweisEingereicht && a.FahrzeugId is { } fahrzeugId)
            {
                // Mit dem Verwendungsnachweis gehören Zuwendung und Zweckbindung in die Fahrzeugakte (Aussonderung warnt dann).
                var fahrzeug = await db.Fahrzeuge.FirstAsync(f => f.Id == fahrzeugId, ct);
                if (Foerderverfahren.ZweckbindungUebernehmen(a, fahrzeug, Beschaffung.GeliefertAm) is { } fehler)
                {
                    ModelState.AddModelError(string.Empty, fehler);
                }
            }

            if (!ModelState.IsValid)
            {
                await AuswahlAsync(ct);
                await StandAsync(ct);
                return Page();
            }

            a.Status = z;
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = ziel is null ? "Z-Feu-Antrag gespeichert." : $"Z-Feu-Antrag: {Anzeige.Text(a.Status)}.";
        return RedirectToPage(new { beschaffungId });
    }

    private async Task<bool?> AltfahrzeugAusserDienstAsync(Guid? altfahrzeugId, CancellationToken ct) =>
        altfahrzeugId is { } id ? await db.Fahrzeuge.Where(f => f.Id == id).Select(f => f.AusserDienstAm != null).FirstOrDefaultAsync(ct) : null;

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private static string? Kurz(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<bool> LadenAsync(Guid beschaffungId, CancellationToken ct, bool verfolgen = false)
    {
        Heute = Anzeige.Heute(zeit);
        var beschaffung = await db.Beschaffungen.FirstOrDefaultAsync(b => b.Id == beschaffungId, ct);
        if (beschaffung is null)
        {
            return false;
        }

        Beschaffung = beschaffung;
        var abfrage = db.Foerderantraege.Include(a => a.Foerdergegenstand).Include(a => a.Fahrzeug).Include(a => a.Altfahrzeug);
        Antrag = await (verfolgen ? abfrage : abfrage.AsNoTracking()).FirstOrDefaultAsync(a => a.BeschaffungId == beschaffungId, ct);
        DarfBearbeiten = await DarfAsync(Berechtigungen.BeschaffungBearbeiten);
        await AuswahlAsync(ct);
        await StandAsync(ct);
        return true;
    }

    private async Task AuswahlAsync(CancellationToken ct)
    {
        var gewaehlt = Formular.FoerdergegenstandId ?? Antrag?.FoerdergegenstandId;
        Gegenstaende = await db.Foerdergegenstaende.Where(g => g.Aktiv || g.Id == gewaehlt)
            .OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem($"{g.Bezeichnung} ({g.Festbetrag:N0} €)", g.Id.ToString())).ToListAsync(ct);
        Fahrzeuge = await db.Fahrzeuge.OrderBy(f => f.Funkrufname)
            .Select(f => new SelectListItem(f.Typ == null ? f.Funkrufname : $"{f.Funkrufname} ({f.Typ}{(f.Baujahr == null ? "" : ", " + f.Baujahr)})", f.Id.ToString())).ToListAsync(ct);
    }

    private async Task StandAsync(CancellationToken ct)
    {
        if (Antrag is null)
        {
            return;
        }

        var altAusserDienst = await AltfahrzeugAusserDienstAsync(Antrag.AltfahrzeugId, ct);
        Fristen = await verfahren.FristenAsync(Antrag, Beschaffung.GeliefertAm, Heute, ct);
        Checkliste = Antrag.Checkliste(altAusserDienst);
        Schritte = Foerderantrag.Folgestatus(Antrag.Status).Select(z => (z, Antrag.Fehlt(z, altAusserDienst))).ToList();
    }
}
