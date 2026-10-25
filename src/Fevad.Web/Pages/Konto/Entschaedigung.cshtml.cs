using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Kasse;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Meine Entschädigung (V32, V34): Lehrgänge und Einsätze über mehr als zwei Tage beantragt das Mitglied hier selbst, mit Tagen, Erwerbslage,
/// Fahrkosten und Belegen. Es sieht den Stand seiner Anträge, alle abgerechneten Posten mit steuerlicher Art und lädt je Jahr eine
/// Jahresbescheinigung herunter. Einsätze bis zwei Tage und Dienste rechnet die Feuerwehr ohne Antrag ab.
/// </summary>
[RequestSizeLimit(Hochladen.MaxAnfrageBytes), RequestFormLimits(MultipartBodyLengthLimit = Hochladen.MaxAnfrageBytes)]
public class EntschaedigungModel(
    AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, Abrechnungen abrechnungen, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>So viele Tage lassen sich in einem Antrag eintragen.</summary>
    public const int MaxTage = 14;

    /// <summary>Ein eigener Antrag mit voraussichtlichem Betrag.</summary>
    /// <param name="Antrag">Der Antrag mit Tagen und Belegen.</param>
    /// <param name="Voraussichtlich">Berechneter Betrag nach den heutigen Sätzen.</param>
    public sealed record Eintrag(Entschaedigungsantrag Antrag, decimal Voraussichtlich);

    /// <summary>Ein abgerechneter Posten.</summary>
    /// <param name="Datum">Tag.</param>
    /// <param name="Bezeichnung">Was.</param>
    /// <param name="Steuerart">Steuerliche Einordnung.</param>
    /// <param name="Betrag">Betrag in Euro.</param>
    /// <param name="Abrechnung">Bezeichnung der Abrechnung.</param>
    /// <param name="Abgeschlossen">Die Abrechnung ist abgeschlossen.</param>
    public sealed record Posten(DateOnly Datum, string Bezeichnung, Steuerart Steuerart, decimal Betrag, string Abrechnung, bool Abgeschlossen);

    /// <summary>Ein Beleg ohne Inhalt.</summary>
    /// <param name="Id">Kennung.</param>
    /// <param name="AntragId">Kennung des Antrags.</param>
    /// <param name="Dateiname">Dateiname.</param>
    public sealed record BelegInfo(Guid Id, Guid AntragId, string Dateiname);

    /// <summary>Belege der eigenen Anträge ohne Inhalt.</summary>
    public IReadOnlyList<BelegInfo> Belege { get; private set; } = [];

    /// <summary>Eigene Anträge, neueste zuerst.</summary>
    public IReadOnlyList<Eintrag> Antraege { get; private set; } = [];

    /// <summary>Eigene Posten der letzten Abrechnungen.</summary>
    public IReadOnlyList<Posten> Abgerechnet { get; private set; } = [];

    /// <summary>Jahre mit abgeschlossenen Abrechnungen für die Jahresbescheinigung.</summary>
    public IReadOnlyList<int> Jahre { get; private set; } = [];

    /// <summary>Eigene Lehrgangsteilnahmen zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Lehrgaenge { get; private set; } = [];

    /// <summary>Formular des Antrags.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Hochgeladene Belege.</summary>
    [BindProperty] public List<IFormFile> Dateien { get; set; } = [];

    /// <summary>Eingaben eines Antrags.</summary>
    public sealed class Eingabe
    {
        /// <summary>Lehrgang oder mehrtägiger Einsatz.</summary>
        [Display(Name = "Wofür")] public AntragAnlass Anlass { get; set; }

        /// <summary>Lehrgangsteilnahme aus der Ausbildung.</summary>
        [Display(Name = "Lehrgang aus meiner Ausbildung")] public Guid? LehrgangsteilnahmeId { get; set; }

        /// <summary>Bezeichnung, z. B. „Truppführer“.</summary>
        [Required(ErrorMessage = "Bitte angeben, welcher Lehrgang oder Einsatz."), StringLength(200), Display(Name = "Bezeichnung")]
        public string Bezeichnung { get; set; } = "";

        /// <summary>Ort.</summary>
        [StringLength(200), Display(Name = "Ort")] public string? Ort { get; set; }

        /// <summary>Die Tage.</summary>
        public List<TagEingabe> Tage { get; set; } = [];

        /// <summary>Erwerbslage.</summary>
        [Display(Name = "Ich bin")] public Erwerbslage Erwerbslage { get; set; }

        /// <summary>Tatsächlicher Verdienstausfall ist entstanden.</summary>
        [Display(Name = "Mir ist tatsächlich Verdienst entgangen")] public bool Verdienstausfall { get; set; }

        /// <summary>Entgangener Verdienst in Euro bei mehr als zwei Tagen.</summary>
        [Range(0, 100_000), Display(Name = "Entgangener Verdienst laut Nachweis (Euro, nur bei mehr als zwei Tagen)")]
        public decimal? VerdienstausfallBetrag { get; set; }

        /// <summary>Weitergezahlte Sozialversicherungsbeiträge.</summary>
        [Range(0, 100_000), Display(Name = "Sozialversicherungsbeiträge laut Nachweis (Euro)")]
        public decimal? SozialversicherungBetrag { get; set; }

        /// <summary>Sonstige notwendige Auslagen.</summary>
        [Range(0, 100_000), Display(Name = "Sonstige notwendige Auslagen (Euro, nur bei mehr als zwei Tagen)")]
        public decimal? AuslagenBetrag { get; set; }

        /// <summary>Anreise.</summary>
        [Display(Name = "Anreise")] public Fahrtart Fahrtart { get; set; }

        /// <summary>Kilometer hin und zurück.</summary>
        [Range(0, 10_000), Display(Name = "Kilometer hin und zurück, alle Tage")] public decimal? Kilometer { get; set; }

        /// <summary>Fahrkosten laut Fahrkarte.</summary>
        [Range(0, 10_000), Display(Name = "Fahrkosten laut Fahrkarte (Euro)")] public decimal? Fahrkosten { get; set; }

        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Ein Tag mit Beginn und Ende.</summary>
    public sealed class TagEingabe
    {
        /// <summary>Datum.</summary>
        public DateOnly? Datum { get; set; }
        /// <summary>Beginn „HH:MM“.</summary>
        public string? Beginn { get; set; }
        /// <summary>Ende „HH:MM“.</summary>
        public string? Ende { get; set; }
    }

    /// <summary>Zeigt Formular, Anträge und Posten.</summary>
    /// <param name="lehrgang">Vorgewählte Lehrgangsteilnahme.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? lehrgang, CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        await LadenAsync(mitgliedId, ct);
        if (lehrgang is { } id && await db.Lehrgangsteilnahmen.AsNoTracking().Include(l => l.Qualifikation)
                .FirstOrDefaultAsync(l => l.Id == id && l.MitgliedId == mitgliedId, ct) is { } teilnahme)
        {
            Formular.LehrgangsteilnahmeId = teilnahme.Id;
            Formular.Bezeichnung = teilnahme.Qualifikation?.Bezeichnung ?? "";
            Formular.Ort = teilnahme.Lehrgangsort;
            if (teilnahme.Von is { } von)
            {
                var bis = teilnahme.Bis ?? von;
                for (var tag = von; tag <= bis && Formular.Tage.Count < MaxTage; tag = tag.AddDays(1))
                {
                    if (tag.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) || von == bis)
                    {
                        Formular.Tage.Add(new TagEingabe { Datum = tag, Beginn = "08:00", Ende = "16:30" });
                    }
                }
            }
        }

        Auffuellen();
        return Page();
    }

    /// <summary>Reicht einen Antrag mit Belegen ein.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        await LadenAsync(mitgliedId, ct);
        if (Formular.LehrgangsteilnahmeId is { } l && !Lehrgaenge.Any(x => x.Value == l.ToString()))
        {
            ModelState.AddModelError("Formular.LehrgangsteilnahmeId", "Dieser Lehrgang steht nicht zur Wahl.");
        }

        var heute = Anzeige.Heute(zeit);
        var antrag = new Entschaedigungsantrag
        {
            MitgliedId = mitgliedId,
            Anlass = Formular.Anlass,
            Bezeichnung = Formular.Bezeichnung.Trim(),
            Ort = Leer(Formular.Ort),
            LehrgangsteilnahmeId = Formular.Anlass == AntragAnlass.Lehrgang ? Formular.LehrgangsteilnahmeId : null,
            Erwerbslage = Formular.Erwerbslage,
            Verdienstausfall = Formular.Verdienstausfall,
            VerdienstausfallBetrag = Formular.VerdienstausfallBetrag,
            SozialversicherungBetrag = Formular.SozialversicherungBetrag,
            AuslagenBetrag = Formular.AuslagenBetrag,
            Fahrtart = Formular.Fahrtart,
            Kilometer = Formular.Fahrtart == Fahrtart.Pkw ? Formular.Kilometer : null,
            Fahrkosten = Formular.Fahrtart == Fahrtart.OeffentlicheVerkehrsmittel ? Formular.Fahrkosten : null,
            Bemerkung = Leer(Formular.Bemerkung),
            Stand = AntragStand.Eingereicht,
            EingereichtAm = heute,
        };

        for (var i = 0; i < Formular.Tage.Count && i < MaxTage; i++)
        {
            var t = Formular.Tage[i];
            if (t.Datum is null && string.IsNullOrWhiteSpace(t.Beginn) && string.IsNullOrWhiteSpace(t.Ende))
            {
                continue;
            }

            if (t.Datum is not { } datum || !ZeitEingabe.TryUhrzeit(t.Beginn, out var beginn) || !ZeitEingabe.TryUhrzeit(t.Ende, out var ende))
            {
                ModelState.AddModelError($"Formular.Tage[{i}].Datum", $"Tag {i + 1}: bitte Datum, Beginn und Ende als HH:MM (24 Stunden) angeben.");
                continue;
            }

            if (datum > heute)
            {
                ModelState.AddModelError($"Formular.Tage[{i}].Datum", $"Tag {i + 1}: beantragt wird erst nach dem Lehrgang.");
                continue;
            }

            antrag.Tage.Add(new AntragTag { Von = Anzeige.AlsUtc(datum, beginn), Bis = Anzeige.AlsUtc(datum, ende) });
        }

        var saetze = await abrechnungen.SaetzeAsync(ct);
        var grenze = (int)(saetze.Zahl(Entschaedigungssaetze.TatsaechlicheHoeheUeberTage, heute) ?? 0);
        foreach (var fehler in antrag.Pruefen(grenze))
        {
            ModelState.AddModelError("", fehler);
        }

        var belege = await Kassenauswahl.BelegeLesenAsync(Dateien, parameter, heute, ModelState, nameof(Dateien), ct);
        if (!ModelState.IsValid)
        {
            Auffuellen();
            return Page();
        }

        antrag.Belege = belege.Select(b => new AntragBeleg { Dateiname = b.Dateiname, Inhaltstyp = b.Inhaltstyp, Inhalt = b.Inhalt, Pruefsumme = b.Pruefsumme }).ToList();
        db.Entschaedigungsantraege.Add(antrag);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = antrag.InTatsaechlicherHoehe(grenze)
            ? "Eingereicht. Über zwei Tage wird in tatsächlicher Höhe erstattet; bitte die Nachweise (Arbeitgeber, Auslagen) hochladen, falls noch nicht geschehen."
            : "Eingereicht. Nach der Prüfung geht der Antrag in die nächste Abrechnung ein; den Stand siehst du hier.";
        return RedirectToPage();
    }

    /// <summary>Zieht einen noch nicht geprüften Antrag zurück.</summary>
    /// <param name="id">Kennung des Antrags.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostZurueckziehenAsync(Guid id, CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        var antrag = await db.Entschaedigungsantraege.FirstOrDefaultAsync(a => a.Id == id && a.MitgliedId == mitgliedId && a.Stand == AntragStand.Eingereicht, ct);
        if (antrag is null)
        {
            return NotFound();
        }

        antrag.Stand = AntragStand.Zurueckgezogen;
        antrag.EntschiedenAm = Anzeige.Heute(zeit);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Antrag zurückgezogen.";
        return RedirectToPage();
    }

    /// <summary>Liefert einen Beleg eines eigenen Antrags aus.</summary>
    /// <param name="belegId">Kennung des Belegs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 404.</returns>
    public async Task<IActionResult> OnGetBelegAsync(Guid belegId, CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        var beleg = await db.AntragBelege.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == belegId && db.Entschaedigungsantraege.Any(a => a.Id == b.EntschaedigungsantragId && a.MitgliedId == mitgliedId), ct);
        return beleg is null ? NotFound() : File(beleg.Inhalt, beleg.Inhaltstyp, beleg.Dateiname);
    }

    /// <summary>Jahresbescheinigung als PDF.</summary>
    /// <param name="jahr">Kalenderjahr des Abschlusses der Abrechnungen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetBescheinigungAsync(int jahr, CancellationToken ct)
    {
        if (await planung.MitgliedIdAsync(benutzer.Id, ct) is not { } mitgliedId || jahr is < 2000 or > 2100)
        {
            return NotFound();
        }

        var zeilen = await Jahresbescheinigung.ZeilenAsync(db, mitgliedId, jahr, ct);
        if (zeilen.Count == 0)
        {
            return NotFound();
        }

        var mitglied = await db.Mitglieder.AsNoTracking().FirstAsync(m => m.Id == mitgliedId, ct);
        var o = await db.OrganisationAsync(ct);
        var stichtag = new DateOnly(jahr, 12, 31);
        var freibetraege = new Jahresbescheinigung.Freibetraege(
            await parameter.DezimalAsync(ParameterSchluessel.SteuerAufwandsentschaedigungMonatEuro, stichtag, ct),
            await parameter.DezimalAsync(ParameterSchluessel.SteuerUebungsleiterEuro, stichtag, ct),
            await parameter.DezimalAsync(ParameterSchluessel.SteuerSonstigeLeistungenFreigrenzeEuro, stichtag, ct));
        var daten = new Jahresbescheinigung.Daten(new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp),
            mitglied.Anzeigename, mitglied.Personalnummer, jahr, zeilen, freibetraege);
        return File(Jahresbescheinigung.Pdf(daten), "application/pdf", Jahresbescheinigung.Dateiname(jahr));
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task LadenAsync(Guid mitgliedId, CancellationToken ct)
    {
        var antraege = await db.Entschaedigungsantraege.AsNoTracking().Include(a => a.Tage)
            .Where(a => a.MitgliedId == mitgliedId)
            .OrderByDescending(a => a.EingereichtAm).Take(50)
            .ToListAsync(ct);
        var saetze = await abrechnungen.SaetzeAsync(ct);
        Antraege = antraege.Select(a => new Eintrag(a, Antragsberechnung.Berechnen(a, saetze).Summe)).ToList();
        var ids = antraege.Select(a => a.Id).ToList();
        Belege = await db.AntragBelege.AsNoTracking().Where(b => ids.Contains(b.EntschaedigungsantragId))
            .Select(b => new BelegInfo(b.Id, b.EntschaedigungsantragId, b.Dateiname)).ToListAsync(ct);

        Abgerechnet = await db.AbrechnungsPosten.AsNoTracking()
            .Where(p => p.MitgliedId == mitgliedId)
            .OrderByDescending(p => p.Datum).Take(200)
            .Select(p => new Posten(p.Datum, p.Bezeichnung, p.Steuerart, p.Betrag, p.Abrechnung!.Bezeichnung, p.Abrechnung.Status == AbrechnungStatus.Abgeschlossen))
            .ToListAsync(ct);
        Jahre = await Jahresbescheinigung.JahreAsync(db, mitgliedId, ct);

        var lehrgaenge = await db.Lehrgangsteilnahmen.AsNoTracking().Include(l => l.Qualifikation)
            .Where(l => l.MitgliedId == mitgliedId && l.Von != null)
            .OrderByDescending(l => l.Von).Take(30)
            .ToListAsync(ct);
        Lehrgaenge = lehrgaenge.Select(l => new SelectListItem($"{l.Qualifikation?.Bezeichnung} ({Anzeige.Zeitraum(l.Von, l.Bis)})", l.Id.ToString())).ToList();
    }

    private void Auffuellen()
    {
        while (Formular.Tage.Count < MaxTage)
        {
            Formular.Tage.Add(new TagEingabe());
        }
    }
}
