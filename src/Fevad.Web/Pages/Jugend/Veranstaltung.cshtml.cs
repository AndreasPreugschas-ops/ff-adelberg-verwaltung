using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Jugend;

/// <summary>
/// Eine Veranstaltung der Jugend (V48): Anmeldung und Einwilligung je Teilnehmer, Teilnehmerbeitrag mit Zahlungseingang, Betreuer mit
/// Prüfung des Führungszeugnisses (bei Übernachtung rot ohne gültiges Zeugnis), Zuschuss mit Fristrechner und Teilnehmerliste als PDF.
/// </summary>
[Authorize(Policy = Berechtigungen.JugendVeranstaltungenSehen)]
public class VeranstaltungModel(AppDbContext db, Datenbereich datenbereich, Jugendveranstaltungen veranstaltungen, Dienstplanung planung,
    Kassenberichte berichte, TimeProvider zeit) : PageModel
{
    /// <summary>Einstellungen der Veranstaltung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Mit Übernachtung.</summary>
        [Display(Name = "mit Übernachtung")] public bool Uebernachtung { get; set; }
        /// <summary>Anmeldung nötig.</summary>
        [Display(Name = "Eltern melden ihr Kind an")] public bool AnmeldungNoetig { get; set; }
        /// <summary>Anmeldeschluss.</summary>
        [Display(Name = "Anmeldung bis")] public DateOnly? AnmeldungBis { get; set; }
        /// <summary>Einwilligung nötig.</summary>
        [Display(Name = "Einwilligung der Eltern zur Teilnahme verlangen")] public bool EinwilligungNoetig { get; set; }
        /// <summary>Teilnehmerbeitrag.</summary>
        [Range(0, 10000, ErrorMessage = "Bitte einen Betrag zwischen 0 und 10.000 € angeben."), Display(Name = "Teilnehmerbeitrag (€)")] public decimal? Teilnehmerbeitrag { get; set; }
        /// <summary>Zuschussgeber.</summary>
        [Display(Name = "Zuschussgeber")] public Guid? ZuschussgeberId { get; set; }
        /// <summary>Zuschuss beantragt am.</summary>
        [Display(Name = "Zuschuss beantragt am")] public DateOnly? ZuschussBeantragtAm { get; set; }
        /// <summary>Zuschussbetrag.</summary>
        [Range(0, 100000, ErrorMessage = "Bitte einen Betrag zwischen 0 und 100.000 € angeben."), Display(Name = "Zuschuss bewilligt (€)")] public decimal? ZuschussBetrag { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung (Treffpunkt, Packliste)")] public string? Bemerkung { get; set; }
    }

    /// <summary>Die Einstellungen.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Die Veranstaltung.</summary>
    public Jugendveranstaltung Veranstaltung { get; private set; } = null!;

    /// <summary>Die Betreuer mit Stand; leer ohne Recht auf das Betreuer-Register.</summary>
    public IReadOnlyList<Jugendveranstaltungen.Betreuerstand> Betreuer { get; private set; } = [];

    /// <summary>Der Benutzer darf Betreuer einteilen und ihren Stand sehen (Recht Jugend.Betreuer).</summary>
    public bool DarfBetreuer { get; private set; }

    /// <summary>Betreuer aus dem Register, die noch nicht eingeteilt sind.</summary>
    public IReadOnlyList<SelectListItem> Betreuerauswahl { get; private set; } = [];

    /// <summary>Kinder der Zielgruppe, die noch nicht angemeldet sind.</summary>
    public IReadOnlyList<SelectListItem> Kinderauswahl { get; private set; } = [];

    /// <summary>Wählbare Zuschussgeber.</summary>
    public IReadOnlyList<SelectListItem> Zuschussgeber { get; private set; } = [];

    /// <summary>Ergebnis des Fristrechners; null ohne Zuschussgeber.</summary>
    public Zuschussrechner.Ergebnis? Zuschuss { get; private set; }

    /// <summary>Stichtag in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Erster Tag.</summary>
    public DateOnly ErsterTag { get; private set; }

    /// <summary>Letzter Tag.</summary>
    public DateOnly LetzterTag { get; private set; }

    /// <summary>Zeigt die Veranstaltung.</summary>
    /// <param name="id">Schlüssel des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        var v = Veranstaltung;
        Formular = new Eingabe
        {
            Uebernachtung = v.Uebernachtung, AnmeldungNoetig = v.AnmeldungNoetig, AnmeldungBis = v.AnmeldungBis, EinwilligungNoetig = v.EinwilligungNoetig,
            Teilnehmerbeitrag = v.Teilnehmerbeitrag, ZuschussgeberId = v.ZuschussgeberId, ZuschussBeantragtAm = v.ZuschussBeantragtAm,
            ZuschussBetrag = v.ZuschussBetrag, Bemerkung = v.Bemerkung,
        };
        return Page();
    }

    /// <summary>Speichert die Einstellungen.</summary>
    /// <param name="id">Schlüssel des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var v = Veranstaltung;
        var f = Formular;
        v.Uebernachtung = f.Uebernachtung;
        v.AnmeldungNoetig = f.AnmeldungNoetig;
        v.AnmeldungBis = f.AnmeldungBis;
        v.EinwilligungNoetig = f.EinwilligungNoetig;
        v.Teilnehmerbeitrag = f.Teilnehmerbeitrag is > 0 ? f.Teilnehmerbeitrag : null;
        v.ZuschussgeberId = f.ZuschussgeberId;
        v.ZuschussBeantragtAm = f.ZuschussBeantragtAm;
        v.ZuschussBetrag = f.ZuschussBetrag;
        v.Bemerkung = string.IsNullOrWhiteSpace(f.Bemerkung) ? null : f.Bemerkung.Trim();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Meldet ein Kind nach einem Anmeldezettel auf Papier an, auch nach dem Anmeldeschluss.</summary>
    /// <param name="id">Schlüssel des Dienstes.</param>
    /// <param name="mitgliedId">Das Kind.</param>
    /// <param name="einwilligung">Die Eltern haben auf dem Zettel eingewilligt.</param>
    /// <param name="tage">Teilnahmetage, falls nicht die ganze Veranstaltung.</param>
    /// <param name="von">Wer den Zettel unterschrieben hat.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAnmeldenAsync(Guid id, Guid mitgliedId, bool einwilligung, int? tage, string? von, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        var ergebnis = await veranstaltungen.AnmeldenAsync(Veranstaltung, mitgliedId, Anmeldeweg.Papier, Kuerzen(von), einwilligung, tage, Anzeige.Heute(zeit), ct);
        if (ergebnis == Jugendveranstaltungen.Anmeldeergebnis.NichtZielgruppe)
        {
            return Forbid();
        }

        TempData[ergebnis == Jugendveranstaltungen.Anmeldeergebnis.Geschlossen ? "Fehler" : "Meldung"] = ergebnis switch
        {
            Jugendveranstaltungen.Anmeldeergebnis.Geschlossen => "Die Veranstaltung ist abgesagt.",
            _ when Veranstaltung.EinwilligungNoetig && !einwilligung => "Angemeldet; die Einwilligung der Eltern fehlt noch.",
            _ => "Angemeldet.",
        };
        return RedirectToPage(new { id });
    }

    /// <summary>Vermerkt den Zahlungseingang des Teilnehmerbeitrags oder nimmt ihn zurück.</summary>
    /// <param name="id">Schlüssel des Dienstes.</param>
    /// <param name="anmeldungId">Die Anmeldung.</param>
    /// <param name="bezahlt">Bezahlt oder nicht.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostBezahltAsync(Guid id, Guid anmeldungId, bool bezahlt, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (Veranstaltung.Anmeldungen.FirstOrDefault(a => a.Id == anmeldungId) is not { } anmeldung)
        {
            return NotFound();
        }

        anmeldung.BezahltAm = bezahlt ? Anzeige.Heute(zeit) : null;
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { id });
    }

    /// <summary>Vermerkt die Einwilligung der Eltern nachträglich, z. B. wenn der Zettel nachgereicht wird.</summary>
    /// <param name="id">Schlüssel des Dienstes.</param>
    /// <param name="anmeldungId">Die Anmeldung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostEinwilligungAsync(Guid id, Guid anmeldungId, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (Veranstaltung.Anmeldungen.FirstOrDefault(a => a.Id == anmeldungId) is not { } anmeldung)
        {
            return NotFound();
        }

        anmeldung.Einwilligung = true;
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { id });
    }

    /// <summary>Nimmt eine Anmeldung zurück.</summary>
    /// <param name="id">Schlüssel des Dienstes.</param>
    /// <param name="mitgliedId">Das Kind.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAbmeldenAsync(Guid id, Guid mitgliedId, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        await veranstaltungen.AbmeldenAsync(Veranstaltung, mitgliedId, ct);
        TempData["Meldung"] = "Abgemeldet.";
        return RedirectToPage(new { id });
    }

    /// <summary>Teilt einen Betreuer aus dem Register ein.</summary>
    /// <param name="id">Schlüssel des Dienstes.</param>
    /// <param name="betreuerId">Der Betreuer.</param>
    /// <param name="tage">Betreuungstage, falls nicht die ganze Veranstaltung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostBetreuerAsync(Guid id, Guid betreuerId, int? tage, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (!DarfBetreuer || !Betreuerauswahl.Any(b => b.Value == betreuerId.ToString()))
        {
            return Forbid();
        }

        db.Veranstaltungsbetreuer.Add(new Veranstaltungsbetreuer { JugendveranstaltungId = Veranstaltung.Id, JugendbetreuerId = betreuerId, Tage = tage is > 0 ? tage : null });
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { id });
    }

    /// <summary>Nimmt einen Betreuer aus der Veranstaltung.</summary>
    /// <param name="id">Schlüssel des Dienstes.</param>
    /// <param name="eintragId">Die Einteilung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostBetreuerEntfernenAsync(Guid id, Guid eintragId, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        if (!DarfBetreuer)
        {
            return Forbid();
        }

        if (Veranstaltung.Betreuer.FirstOrDefault(b => b.Id == eintragId) is { } eintrag)
        {
            db.Veranstaltungsbetreuer.Remove(eintrag);
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Teilnehmerliste als Nachweis für den Zuschuss, nur auf Abruf erzeugt.</summary>
    /// <param name="id">Schlüssel des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetTeilnehmerlisteAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        var v = Veranstaltung;
        var dauer = LetzterTag.DayNumber - ErsterTag.DayNumber + 1;
        var teilnehmer = v.Anmeldungen.Where(a => a.Mitglied is not null)
            .OrderBy(a => a.Mitglied!.Nachname).ThenBy(a => a.Mitglied!.Vorname)
            .Select(a => new TeilnehmerlistePdf.Zeile($"{a.Mitglied!.Nachname}, {a.Mitglied.Vorname}", Anzeige.Datum(a.Mitglied.Geburtsdatum),
                Anschrift.Zeile(a.Mitglied.Strasse, a.Mitglied.Plz, a.Mitglied.Ort), a.Tage ?? dauer))
            .ToList();
        var betreuer = v.Betreuer.Where(b => b.Jugendbetreuer is not null)
            .OrderBy(b => b.Jugendbetreuer!.Nachname).ThenBy(b => b.Jugendbetreuer!.Vorname)
            .Select(b => new TeilnehmerlistePdf.Zeile(b.Jugendbetreuer!.Anzeigename, null, null, b.Tage ?? dauer))
            .ToList();
        var organisation = await db.Organisationen.AsNoTracking().FirstAsync(ct);
        var traeger = v.Dienst!.Abteilungen.FirstOrDefault()?.Bezeichnung is { } abteilung ? $"{abteilung}, {organisation.FeuerwehrName}" : organisation.FeuerwehrName;
        var pdf = TeilnehmerlistePdf.Erzeugen(new(await berichte.KopfAsync(ct), v.Dienst.Titel, Anzeige.Zeitraum(ErsterTag, LetzterTag), v.Dienst.Ort,
            traeger, v.Zuschussgeber?.Name, teilnehmer, betreuer));
        return File(pdf, "application/pdf", $"teilnehmerliste-{ErsterTag:yyyy-MM-dd}.pdf");
    }

    /// <summary>Lädt Veranstaltung und Auswahllisten und prüft das Recht; gibt bei Fehlern das Ergebnis zurück.</summary>
    private async Task<IActionResult?> LadenAsync(Guid id, CancellationToken ct)
    {
        if (await veranstaltungen.LadenAsync(id, ct) is not { } v)
        {
            return NotFound();
        }

        if (!await datenbereich.DarfDienstBearbeitenAsync(User, v.Dienst!.AbteilungIds, ct))
        {
            return Forbid();
        }

        Veranstaltung = v;
        ErsterTag = Jugendveranstaltungen.ErsterTag(v.Dienst);
        LetzterTag = Jugendveranstaltungen.LetzterTag(v.Dienst);
        var heute = Heute = Anzeige.Heute(zeit);
        Zuschuss = Jugendveranstaltungen.Zuschuss(v);

        var betreuerFreigabe = await datenbereich.FreigabeAsync(User, Berechtigungen.JugendBetreuer);
        var abteilungen = v.Dienst.AbteilungIds;
        DarfBetreuer = abteilungen.Count > 0 && abteilungen.Any(betreuerFreigabe.Umfasst);
        if (DarfBetreuer)
        {
            Betreuer = await veranstaltungen.BetreuerAsync(v, heute, ct);
            var eingeteilt = v.Betreuer.Select(b => b.JugendbetreuerId).ToList();
            var erlaubt = abteilungen.Where(betreuerFreigabe.Umfasst).ToList();
            var tag = ErsterTag;
            Betreuerauswahl = await db.Jugendbetreuer.AsNoTracking()
                .Where(b => erlaubt.Contains(b.AbteilungId) && !eingeteilt.Contains(b.Id) && (b.Bis == null || b.Bis >= tag))
                .OrderBy(b => b.Nachname).ThenBy(b => b.Vorname)
                .Select(b => new SelectListItem(b.Nachname + ", " + b.Vorname, b.Id.ToString()))
                .ToListAsync(ct);
        }

        var angemeldet = v.Anmeldungen.Select(a => a.MitgliedId).ToHashSet();
        Kinderauswahl = (await planung.TeilnehmerkreisAsync(v.Dienst, ct))
            .Where(t => !angemeldet.Contains(t.MitgliedId))
            .Select(t => new SelectListItem($"{t.Nachname}, {t.Vorname}", t.MitgliedId.ToString()))
            .ToList();
        Zuschussgeber = await db.Zuschussgeber.AsNoTracking()
            .Where(z => z.Aktiv || z.Id == v.ZuschussgeberId)
            .OrderBy(z => z.Name)
            .Select(z => new SelectListItem(z.Name, z.Id.ToString()))
            .ToListAsync(ct);
        return null;
    }

    private static string? Kuerzen(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(text.Trim().Length, 200)];
}
