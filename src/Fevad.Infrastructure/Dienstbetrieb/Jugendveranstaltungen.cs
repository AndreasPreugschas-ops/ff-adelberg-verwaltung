using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Dienstbetrieb;

/// <summary>
/// Veranstaltungen der Jugend (V48): Angaben zum Dienst laden oder anlegen, Kinder anmelden und abmelden, Betreuer einteilen und ihren
/// Stand beim Führungszeugnis prüfen, den Zuschuss rechnen.
/// </summary>
public sealed class Jugendveranstaltungen(AppDbContext db, Dienstplanung planung, Betreuerregister register)
{
    /// <summary>Ausgang einer Anmeldung.</summary>
    public enum Anmeldeergebnis
    {
        /// <summary>Angemeldet.</summary>
        Angemeldet,
        /// <summary>War schon angemeldet; Angaben aktualisiert.</summary>
        Aktualisiert,
        /// <summary>Anmeldeschluss vorbei oder Veranstaltung abgesagt.</summary>
        Geschlossen,
        /// <summary>Das Kind gehört nicht zur Zielgruppe.</summary>
        NichtZielgruppe,
        /// <summary>Die Einwilligung der Eltern fehlt.</summary>
        EinwilligungFehlt,
    }

    /// <summary>Ein eingeteilter Betreuer mit dem Stand seines Führungszeugnisses am letzten Tag der Veranstaltung.</summary>
    /// <param name="Eintrag">Die Einteilung mit geladenem Betreuer.</param>
    /// <param name="Status">Stand des Zeugnisses.</param>
    /// <param name="Einsetzbar">Darf eingesetzt werden; bei Übernachtung nur mit gültigem Zeugnis.</param>
    public sealed record Betreuerstand(Veranstaltungsbetreuer Eintrag, Zeugnisstatus Status, bool Einsetzbar);

    /// <summary>Erster Tag der Veranstaltung in Ortszeit.</summary>
    /// <param name="dienst">Der Dienst.</param>
    /// <returns>Der Tag.</returns>
    public static DateOnly ErsterTag(Dienst dienst) => Ortszeit.Datum(dienst.Beginn);

    /// <summary>Letzter Tag der Veranstaltung in Ortszeit; ein Ende um 00:00 Uhr zählt zum Vortag.</summary>
    /// <param name="dienst">Der Dienst.</param>
    /// <returns>Der Tag.</returns>
    public static DateOnly LetzterTag(Dienst dienst) => ErsterTag(dienst).AddDays(Ortszeit.Kalendertage(dienst.Beginn, dienst.Ende) - 1);

    /// <summary>
    /// Lädt die Angaben zur Veranstaltung mit Dienst, Anmeldungen und Betreuern. Fehlen sie bei einem Dienst der Jugend, werden sie mit
    /// Vorbelegung nach der Art angelegt.
    /// </summary>
    /// <param name="dienstId">Schlüssel des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Veranstaltung oder null, wenn der Dienst fehlt oder keine Jugendart hat.</returns>
    public async Task<Jugendveranstaltung?> LadenAsync(Guid dienstId, CancellationToken ct = default)
    {
        var vorhanden = await Abfrage().FirstOrDefaultAsync(v => v.DienstId == dienstId, ct);
        if (vorhanden is not null)
        {
            return vorhanden;
        }

        var dienst = await db.Dienste.Include(d => d.Abteilungen).FirstOrDefaultAsync(d => d.Id == dienstId, ct);
        if (dienst is null || !Jugenddienstarten.Ist(dienst.Art))
        {
            return null;
        }

        var mitAnmeldung = dienst.Art != DienstArt.Elternabend;
        var neu = new Jugendveranstaltung
        {
            DienstId = dienst.Id,
            Dienst = dienst,
            Uebernachtung = dienst.Art == DienstArt.Zeltlager || Ortszeit.Kalendertage(dienst.Beginn, dienst.Ende) > 1,
            AnmeldungNoetig = mitAnmeldung,
            EinwilligungNoetig = mitAnmeldung,
        };
        db.Jugendveranstaltungen.Add(neu);
        await db.SaveChangesAsync(ct);
        return neu;
    }

    /// <summary>
    /// Meldet ein Kind an oder ändert seine Anmeldung. Über das Elternkonto nur bis zum Anmeldeschluss und mit Einwilligung, wenn sie
    /// verlangt ist; die Anmeldung trägt zugleich die Zusage ein, solange Rückmeldungen möglich sind.
    /// </summary>
    /// <param name="v">Die Veranstaltung mit geladenem Dienst und Anmeldungen.</param>
    /// <param name="mitgliedId">Das Kind.</param>
    /// <param name="weg">Elternkonto oder Papier.</param>
    /// <param name="von">Wer anmeldet.</param>
    /// <param name="einwilligung">Die Eltern willigen ein.</param>
    /// <param name="tage">Teilnahmetage, falls nicht die ganze Veranstaltung.</param>
    /// <param name="heute">Stichtag in Ortszeit.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<Anmeldeergebnis> AnmeldenAsync(Jugendveranstaltung v, Guid mitgliedId, Anmeldeweg weg, string? von, bool einwilligung, int? tage,
        DateOnly heute, CancellationToken ct = default)
    {
        var dienst = v.Dienst!;
        if (dienst.Status == DienstStatus.Abgesagt || (weg == Anmeldeweg.Elternkonto && !v.AnmeldungOffen(heute, ErsterTag(dienst))))
        {
            return Anmeldeergebnis.Geschlossen;
        }

        if (!(await planung.ProfilAsync(mitgliedId, ct)).Betrifft(dienst, ErsterTag(dienst)))
        {
            return Anmeldeergebnis.NichtZielgruppe;
        }

        if (v.EinwilligungNoetig && !einwilligung && weg == Anmeldeweg.Elternkonto)
        {
            return Anmeldeergebnis.EinwilligungFehlt;
        }

        var anmeldung = v.Anmeldungen.FirstOrDefault(a => a.MitgliedId == mitgliedId);
        var ergebnis = anmeldung is null ? Anmeldeergebnis.Angemeldet : Anmeldeergebnis.Aktualisiert;
        if (anmeldung is null)
        {
            anmeldung = new Veranstaltungsanmeldung { JugendveranstaltungId = v.Id, MitgliedId = mitgliedId, AngemeldetAm = heute, Weg = weg, AngemeldetVon = von };
            db.Veranstaltungsanmeldungen.Add(anmeldung);
            v.Anmeldungen.Add(anmeldung);
        }

        anmeldung.Einwilligung = einwilligung;
        anmeldung.Tage = tage is > 0 ? tage : null;
        await db.SaveChangesAsync(ct);
        await planung.RueckmeldenAsync(dienst.Id, mitgliedId, RueckmeldungAntwort.Zusage, null, ct);
        return ergebnis;
    }

    /// <summary>Nimmt eine Anmeldung zurück; solange Rückmeldungen möglich sind, steht das Kind dann als abgesagt.</summary>
    /// <param name="v">Die Veranstaltung mit geladenen Anmeldungen.</param>
    /// <param name="mitgliedId">Das Kind.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns><c>true</c>, wenn eine Anmeldung bestand.</returns>
    public async Task<bool> AbmeldenAsync(Jugendveranstaltung v, Guid mitgliedId, CancellationToken ct = default)
    {
        var anmeldung = v.Anmeldungen.FirstOrDefault(a => a.MitgliedId == mitgliedId);
        if (anmeldung is null)
        {
            return false;
        }

        db.Veranstaltungsanmeldungen.Remove(anmeldung);
        v.Anmeldungen.Remove(anmeldung);
        await db.SaveChangesAsync(ct);
        await planung.RueckmeldenAsync(v.DienstId, mitgliedId, RueckmeldungAntwort.Absage, null, ct);
        return true;
    }

    /// <summary>Stand der eingeteilten Betreuer am letzten Tag der Veranstaltung; bei Übernachtung zählt nur ein gültiges Zeugnis.</summary>
    /// <param name="v">Die Veranstaltung mit geladenen Betreuern.</param>
    /// <param name="heute">Stichtag der Parameter.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Betreuer, sortiert nach Name.</returns>
    public async Task<IReadOnlyList<Betreuerstand>> BetreuerAsync(Jugendveranstaltung v, DateOnly heute, CancellationToken ct = default)
    {
        var fristen = await register.FristenAsync(heute, ct);
        var tag = LetzterTag(v.Dienst!);
        return v.Betreuer
            .Where(b => b.Jugendbetreuer is not null)
            .OrderBy(b => b.Jugendbetreuer!.Nachname).ThenBy(b => b.Jugendbetreuer!.Vorname)
            .Select(b =>
            {
                var status = Fuehrungszeugnisregeln.Status(b.Jugendbetreuer!, tag, fristen.Jahre(b.Jugendbetreuer!), fristen.VorlaufTage);
                var einsetzbar = (v.Uebernachtung ? Fuehrungszeugnisregeln.EinsetzbarBeiUebernachtung(status) : Fuehrungszeugnisregeln.Einsetzbar(status))
                    && b.Jugendbetreuer!.TaetigAm(tag);
                return new Betreuerstand(b, status, einsetzbar);
            })
            .ToList();
    }

    /// <summary>Rechnet den Zuschuss mit den angemeldeten Teilnehmern und Betreuern.</summary>
    /// <param name="v">Die Veranstaltung mit Dienst, Zuschussgeber, Anmeldungen samt Mitglied und Betreuern.</param>
    /// <returns>Das Ergebnis oder null ohne Zuschussgeber.</returns>
    public static Zuschussrechner.Ergebnis? Zuschuss(Jugendveranstaltung v) =>
        v.Zuschussgeber is null
            ? null
            : Zuschussrechner.Rechnen(v.Zuschussgeber, ErsterTag(v.Dienst!), LetzterTag(v.Dienst!),
                v.Anmeldungen.Select(a => new Zuschussrechner.Person(a.Mitglied?.Geburtsdatum, a.Tage, false))
                    .Concat(v.Betreuer.Select(b => new Zuschussrechner.Person(null, b.Tage, true))));

    private IQueryable<Jugendveranstaltung> Abfrage() => db.Jugendveranstaltungen
        .Include(v => v.Dienst).ThenInclude(d => d!.Abteilungen)
        .Include(v => v.Zuschussgeber)
        .Include(v => v.Anmeldungen).ThenInclude(a => a.Mitglied)
        .Include(v => v.Betreuer).ThenInclude(b => b.Jugendbetreuer).ThenInclude(b => b!.Abteilung)
        .AsSplitQuery();
}
