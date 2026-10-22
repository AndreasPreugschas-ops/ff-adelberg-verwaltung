using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Dienstbetrieb;

/// <summary>Übungs- und Dienstplan: Serien anlegen, Zielgruppen auflösen, Zu- und Absagen, Übernahme in die Anwesenheit.</summary>
public sealed class Dienstplanung(AppDbContext db, ParameterLeser parameter, TimeProvider zeit)
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public sealed record Teilnehmer(Guid MitgliedId, string Nachname, string Vorname, string? Abteilung);

    public enum RueckmeldungErgebnis
    {
        Gespeichert,
        NichtGefunden,
        NichtZielgruppe,
        Geschlossen,
    }

    public static DateOnly LokalesDatum(DateTimeOffset zeitpunkt) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeitpunkt, Berlin).DateTime);

    public static DateTimeOffset AlsUtc(DateOnly datum, TimeOnly uhrzeit)
    {
        var lokal = datum.ToDateTime(uhrzeit);
        return new DateTimeOffset(lokal, Berlin.GetUtcOffset(lokal)).ToUniversalTime();
    }

    /// <summary>Mitglied, das mit dem Benutzerkonto verknüpft ist.</summary>
    public async Task<Guid?> MitgliedIdAsync(Guid? benutzerId, CancellationToken ct = default) =>
        benutzerId is null
            ? null
            : await db.Users.Where(b => b.Id == benutzerId).Select(b => b.MitgliedId).FirstOrDefaultAsync(ct);

    public async Task<Teilnehmerprofil> ProfilAsync(Guid mitgliedId, CancellationToken ct = default)
    {
        var mitgliedschaften = await db.Mitgliedschaften.AsNoTracking().Where(m => m.MitgliedId == mitgliedId).ToListAsync(ct);
        var gruppen = await db.GruppenMitgliedschaften.Where(g => g.MitgliedId == mitgliedId).Select(g => g.GruppeId).ToListAsync(ct);
        var qualifikationen = await db.QualifikationNachweise.Where(q => q.MitgliedId == mitgliedId)
            .Select(q => new { q.QualifikationId, q.ErworbenAm, q.GueltigBis })
            .ToListAsync(ct);
        return new Teilnehmerprofil(mitgliedschaften, gruppen.ToHashSet(),
            qualifikationen.Select(q => (q.QualifikationId, q.ErworbenAm, q.GueltigBis)).ToList());
    }

    /// <summary>
    /// Dienste im halboffenen Zeitraum [von, bis), mit Zielgruppenangaben. Mit Profil nur die, die das Mitglied betreffen.
    /// </summary>
    public async Task<List<Dienst>> TermineAsync(DateTimeOffset von, DateTimeOffset bis, Teilnehmerprofil? profil, CancellationToken ct = default)
    {
        var dienste = await db.Dienste.AsNoTracking()
            .Include(d => d.Abteilung)
            .Include(d => d.Gruppe)
            .Include(d => d.Qualifikation)
            .Where(d => d.Beginn >= von && d.Beginn < bis)
            .OrderBy(d => d.Beginn)
            .ToListAsync(ct);
        return profil is null ? dienste : dienste.Where(d => profil.Betrifft(d, LokalesDatum(d.Beginn))).ToList();
    }

    /// <summary>Alle Mitglieder, die am Tag des Dienstes zur Zielgruppe gehören.</summary>
    public async Task<List<Teilnehmer>> TeilnehmerkreisAsync(Dienst dienst, CancellationToken ct = default)
    {
        var tag = LokalesDatum(dienst.Beginn);
        var abfrage = db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s => s.GueltigAb <= tag && (s.GueltigBis == null || s.GueltigBis >= tag)
                && s.Status != MitgliedStatus.Ausgeschieden
                && (dienst.AbteilungId == null || s.AbteilungId == dienst.AbteilungId)));
        if (dienst.GruppeId is { } gruppe)
        {
            abfrage = abfrage.Where(m => db.GruppenMitgliedschaften.Any(g => g.GruppeId == gruppe && g.MitgliedId == m.Id));
        }

        if (dienst.QualifikationId is { } qualifikation)
        {
            abfrage = abfrage.Where(m => db.QualifikationNachweise.Any(q => q.MitgliedId == m.Id && q.QualifikationId == qualifikation
                && q.ErworbenAm <= tag && (q.GueltigBis == null || q.GueltigBis >= tag)));
        }

        return await abfrage
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new Teilnehmer(m.Id, m.Nachname, m.Vorname, m.Mitgliedschaften
                .Where(s => s.GueltigAb <= tag && (s.GueltigBis == null || s.GueltigBis >= tag))
                .Select(s => s.Abteilung!.Kurzbezeichnung ?? s.Abteilung.Code)
                .FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>Höchstens so viele Monate im Voraus legt eine Serie Termine an (Parameter <see cref="ParameterSchluessel.DienstSerieMaxMonate"/>).</summary>
    public async Task<DateOnly> SerieSpaetestensAsync(DateOnly erster, CancellationToken ct = default) =>
        erster.AddMonths(await parameter.ZahlAsync(ParameterSchluessel.DienstSerieMaxMonate, erster, 12, ct));

    /// <summary>
    /// Legt für jeden Tag der Serie eine Kopie der Vorlage an (gleiche Uhrzeit in Ortszeit, auch über die Zeitumstellung).
    /// Gibt die angelegten Dienste zurück; gespeichert wird sofort.
    /// </summary>
    public async Task<List<Dienst>> SerieAnlegenAsync(Dienst vorlage, Wiederholung rhythmus, DateOnly letzter, CancellationToken ct = default)
    {
        var erster = LokalesDatum(vorlage.Beginn);
        var spaetestens = await SerieSpaetestensAsync(erster, ct);
        if (letzter > spaetestens)
        {
            letzter = spaetestens;
        }

        var beginn = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(vorlage.Beginn, Berlin).DateTime);
        var dauer = vorlage.Ende - vorlage.Beginn;
        var serie = new DienstSerie { Rhythmus = rhythmus, ErsterTag = erster, LetzterTag = letzter };
        foreach (var tag in DienstSerie.Tage(rhythmus, erster, letzter))
        {
            var start = AlsUtc(tag, beginn);
            serie.Dienste.Add(Kopie(vorlage, start, start + dauer));
        }

        db.DienstSerien.Add(serie);
        await db.SaveChangesAsync(ct);
        return serie.Dienste;
    }

    /// <summary>
    /// Übernimmt Art, Titel, Thema, Ort, Zielgruppe und Uhrzeit auf die folgenden, noch geplanten Termine der Serie;
    /// ist der Dienst abgesagt, werden auch sie abgesagt. „Folgend“ bemisst sich am Beginn vor der Änderung.
    /// </summary>
    public async Task<int> FolgendeAnpassenAsync(Dienst dienst, DateTimeOffset bisherigerBeginn, CancellationToken ct = default)
    {
        if (dienst.SerieId is null)
        {
            return 0;
        }

        var lokalBeginn = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(dienst.Beginn, Berlin).DateTime);
        var dauer = dienst.Ende - dienst.Beginn;
        var folgende = await db.Dienste
            .Where(d => d.SerieId == dienst.SerieId && d.Id != dienst.Id && d.Beginn > bisherigerBeginn && d.Status == DienstStatus.Geplant)
            .ToListAsync(ct);
        foreach (var d in folgende)
        {
            d.Art = dienst.Art;
            d.Titel = dienst.Titel;
            d.Thema = dienst.Thema;
            d.Ort = dienst.Ort;
            d.AbteilungId = dienst.AbteilungId;
            d.GruppeId = dienst.GruppeId;
            d.QualifikationId = dienst.QualifikationId;
            d.RueckmeldungErbeten = dienst.RueckmeldungErbeten;
            d.ZaehltFuerFortbildung = dienst.ZaehltFuerFortbildung;
            d.Beginn = AlsUtc(LokalesDatum(d.Beginn), lokalBeginn);
            d.Ende = d.Beginn + dauer;
            if (dienst.Status == DienstStatus.Abgesagt)
            {
                d.Status = DienstStatus.Abgesagt;
            }
        }

        await db.SaveChangesAsync(ct);
        return folgende.Count;
    }

    /// <summary>Löscht die folgenden Termine der Serie, für die noch keine Anwesenheit erfasst ist.</summary>
    public async Task<int> FolgendeLoeschenAsync(Dienst dienst, CancellationToken ct = default)
    {
        if (dienst.SerieId is null)
        {
            return 0;
        }

        var folgende = await db.Dienste
            .Where(d => d.SerieId == dienst.SerieId && d.Beginn > dienst.Beginn
                && !d.Anwesenheiten.Any(a => a.ErfasstUeber != ErfassungsWeg.Rueckmeldung))
            .ToListAsync(ct);
        await LoeschenAsync(folgende, ct);
        return folgende.Count;
    }

    /// <summary>Löscht Dienste samt Rückmeldungen und der daraus übernommenen Anwesenheit.</summary>
    public async Task LoeschenAsync(IReadOnlyCollection<Dienst> dienste, CancellationToken ct = default)
    {
        var ids = dienste.Select(d => d.Id).ToList();
        db.Rueckmeldungen.RemoveRange(await db.Rueckmeldungen.Where(r => ids.Contains(r.DienstId)).ToListAsync(ct));
        db.Anwesenheiten.RemoveRange(await db.Anwesenheiten
            .Where(a => ids.Contains(a.DienstId) && a.ErfasstUeber == ErfassungsWeg.Rueckmeldung).ToListAsync(ct));
        db.Dienste.RemoveRange(dienste);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> RueckmeldungSchlussStundenAsync(CancellationToken ct = default) =>
        await parameter.ZahlAsync(ParameterSchluessel.DienstRueckmeldungSchlussStunden, LokalesDatum(zeit.GetUtcNow()), 0, ct);

    /// <summary>Speichert die Zu- oder Absage eines Mitglieds; eine spätere Rückmeldung ersetzt die frühere.</summary>
    public async Task<RueckmeldungErgebnis> RueckmeldenAsync(Guid dienstId, Guid mitgliedId, RueckmeldungAntwort antwort, string? begruendung, CancellationToken ct = default)
    {
        var dienst = await db.Dienste.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dienstId, ct);
        if (dienst is null)
        {
            return RueckmeldungErgebnis.NichtGefunden;
        }

        if (!(await ProfilAsync(mitgliedId, ct)).Betrifft(dienst, LokalesDatum(dienst.Beginn)))
        {
            return RueckmeldungErgebnis.NichtZielgruppe;
        }

        var jetzt = zeit.GetUtcNow();
        if (!dienst.RueckmeldungMoeglich(jetzt, await RueckmeldungSchlussStundenAsync(ct)))
        {
            return RueckmeldungErgebnis.Geschlossen;
        }

        var rueckmeldung = await db.Rueckmeldungen.FirstOrDefaultAsync(r => r.DienstId == dienstId && r.MitgliedId == mitgliedId, ct);
        if (rueckmeldung is null)
        {
            rueckmeldung = new Rueckmeldung { DienstId = dienstId, MitgliedId = mitgliedId };
            db.Rueckmeldungen.Add(rueckmeldung);
        }

        rueckmeldung.Antwort = antwort;
        rueckmeldung.Begruendung = antwort == RueckmeldungAntwort.Absage && !string.IsNullOrWhiteSpace(begruendung)
            ? begruendung.Trim()[..Math.Min(begruendung.Trim().Length, 500)]
            : null;
        rueckmeldung.GemeldetAm = jetzt;
        await db.SaveChangesAsync(ct);
        return RueckmeldungErgebnis.Gespeichert;
    }

    /// <summary>
    /// Übernimmt Zusagen als „anwesend“ und Absagen als „entschuldigt“ für alle, die noch keinen Anwesenheitseintrag haben.
    /// Bereits erfasste Einträge bleiben unverändert.
    /// </summary>
    public async Task<int> RueckmeldungenUebernehmenAsync(Guid dienstId, CancellationToken ct = default)
    {
        var vorhanden = await db.Anwesenheiten.Where(a => a.DienstId == dienstId).Select(a => a.MitgliedId).ToListAsync(ct);
        var neu = await db.Rueckmeldungen
            .Where(r => r.DienstId == dienstId && !vorhanden.Contains(r.MitgliedId))
            .Select(r => new { r.MitgliedId, r.Antwort })
            .ToListAsync(ct);
        foreach (var r in neu)
        {
            db.Anwesenheiten.Add(new Anwesenheit
            {
                DienstId = dienstId,
                MitgliedId = r.MitgliedId,
                Status = r.Antwort == RueckmeldungAntwort.Zusage ? AnwesenheitStatus.Anwesend : AnwesenheitStatus.Entschuldigt,
                ErfasstUeber = ErfassungsWeg.Rueckmeldung,
            });
        }

        await db.SaveChangesAsync(ct);
        return neu.Count;
    }

    private static Dienst Kopie(Dienst vorlage, DateTimeOffset beginn, DateTimeOffset ende) => new()
    {
        Art = vorlage.Art,
        Titel = vorlage.Titel,
        Thema = vorlage.Thema,
        Ort = vorlage.Ort,
        Beginn = beginn,
        Ende = ende,
        AbteilungId = vorlage.AbteilungId,
        GruppeId = vorlage.GruppeId,
        QualifikationId = vorlage.QualifikationId,
        VerantwortlicherId = vorlage.VerantwortlicherId,
        RueckmeldungErbeten = vorlage.RueckmeldungErbeten,
        ZaehltFuerFortbildung = vorlage.ZaehltFuerFortbildung,
        Status = DienstStatus.Geplant,
    };
}
