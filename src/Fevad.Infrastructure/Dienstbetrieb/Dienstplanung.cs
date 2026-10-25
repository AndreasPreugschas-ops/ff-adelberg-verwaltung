using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Dienstbetrieb;

/// <summary>Übungs- und Dienstplan: Serien anlegen, Zielgruppen auflösen, Zu- und Absagen, Übernahme in die Anwesenheit.</summary>
public sealed partial class Dienstplanung(AppDbContext db, ParameterLeser parameter, TimeProvider zeit)
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    /// <summary>Mitglied der Zielgruppe eines Dienstes.</summary>
    /// <param name="MitgliedId">Schlüssel des Mitglieds.</param>
    /// <param name="Nachname">Nachname.</param>
    /// <param name="Vorname">Vorname.</param>
    /// <param name="Abteilung">Bezeichnung der Abteilung am Diensttag.</param>
    public sealed record Teilnehmer(Guid MitgliedId, string Nachname, string Vorname, string? Abteilung);

    /// <summary>Ergebnis einer Zu- oder Absage.</summary>
    public enum RueckmeldungErgebnis
    {
        /// <summary>Die Rückmeldung ist gespeichert.</summary>
        Gespeichert,
        /// <summary>Den Dienst gibt es nicht.</summary>
        NichtGefunden,
        /// <summary>Das Mitglied gehört nicht zur Zielgruppe des Dienstes.</summary>
        NichtZielgruppe,
        /// <summary>Die Rückmeldung ist geschlossen (Rückmeldeschluss, Dienst abgesagt oder keine Rückmeldung erbeten).</summary>
        Geschlossen,
    }

    /// <summary>Was die Sammelzuordnung für die markierten Mitglieder einträgt: eine Rückmeldung oder gleich die Anwesenheit.</summary>
    public enum Sammelmeldung
    {
        /// <summary>Als Zusage eintragen.</summary>
        Zusage,
        /// <summary>Als Absage eintragen.</summary>
        Absage,
        /// <summary>Als anwesend eintragen.</summary>
        Anwesend,
        /// <summary>Als entschuldigt eintragen.</summary>
        Entschuldigt,
        /// <summary>Als unentschuldigt eintragen.</summary>
        Unentschuldigt,
    }

    /// <summary>Kalendertag eines Zeitpunkts in Ortszeit.</summary>
    /// <param name="zeitpunkt">Zeitpunkt.</param>
    /// <returns>Das Datum in Europe/Berlin.</returns>
    public static DateOnly LokalesDatum(DateTimeOffset zeitpunkt) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeitpunkt, Berlin).DateTime);

    /// <summary>Wandelt Datum und Uhrzeit in Ortszeit in einen UTC-Zeitpunkt um.</summary>
    /// <param name="datum">Datum in Ortszeit.</param>
    /// <param name="uhrzeit">Uhrzeit in Ortszeit.</param>
    /// <returns>Der Zeitpunkt in UTC.</returns>
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

    /// <summary>Jemand, für den ein Konto Termine sieht und zu- oder absagt: das eigene Mitglied oder ein Kind (Elternkonto).</summary>
    public sealed record Person(Guid MitgliedId, string Vorname, bool Selbst);

    /// <summary>Eigenes Mitglied zuerst, dann die Kinder aus den Zuordnungen des Elternkontos (<see cref="Elternkonten.Berechtigt"/>).</summary>
    public async Task<IReadOnlyList<Person>> PersonenAsync(Guid? benutzerId, CancellationToken ct = default)
    {
        if (benutzerId is null)
        {
            return [];
        }

        var personen = new List<Person>();
        var eigenes = await MitgliedIdAsync(benutzerId, ct);
        if (eigenes is { } id)
        {
            personen.Add(new(id, await db.Mitglieder.Where(m => m.Id == id).Select(m => m.Vorname).FirstAsync(ct), true));
        }

        var heute = LokalesDatum(zeit.GetUtcNow());
        var volljaehrig = await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, heute, 18, ct);
        var kinder = await db.Erziehungsberechtigte.AsNoTracking()
            .Where(e => e.KontoId == benutzerId).Where(Elternkonten.Berechtigt(heute, volljaehrig))
            .Select(e => new { e.MitgliedId, e.Mitglied!.Vorname })
            .Distinct().OrderBy(k => k.Vorname).ToListAsync(ct);
        personen.AddRange(kinder.Where(k => k.MitgliedId != eigenes).Select(k => new Person(k.MitgliedId, k.Vorname, false)));
        return personen;
    }

    /// <summary>Lädt Mitgliedschaften, Gruppen und Qualifikationen eines Mitglieds, um Zielgruppen zu prüfen.</summary>
    /// <param name="mitgliedId">Schlüssel des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das Teilnehmerprofil.</returns>
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
        var dienste = await db.Dienste.AsNoTracking().MitZielgruppe()
            .Where(d => d.Beginn >= von && d.Beginn < bis)
            .OrderBy(d => d.Beginn)
            .ToListAsync(ct);
        return profil is null ? dienste : dienste.Where(d => profil.Betrifft(d, LokalesDatum(d.Beginn))).ToList();
    }

    /// <summary>Alle Mitglieder, die am Tag des Dienstes zur Zielgruppe gehören.</summary>
    public async Task<List<Teilnehmer>> TeilnehmerkreisAsync(Dienst dienst, CancellationToken ct = default)
    {
        var tag = LokalesDatum(dienst.Beginn);
        var abteilungen = dienst.AbteilungIds.ToList();
        var alle = abteilungen.Count == 0;
        var abfrage = db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s => s.GueltigAb <= tag && (s.GueltigBis == null || s.GueltigBis >= tag)
                && s.Status != MitgliedStatus.Ausgeschieden
                && (alle || abteilungen.Contains(s.AbteilungId))));
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
                .Where(s => s.GueltigAb <= tag && (s.GueltigBis == null || s.GueltigBis >= tag) && (alle || abteilungen.Contains(s.AbteilungId)))
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
        foreach (var dienst in serie.Dienste)
        {
            await AbwesenheitenAbgleichenAsync(dienst, ct);
        }

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
        var folgende = await db.Dienste.Include(d => d.Abteilungen)
            .Where(d => d.SerieId == dienst.SerieId && d.Id != dienst.Id && d.Beginn > bisherigerBeginn && d.Status == DienstStatus.Geplant)
            .ToListAsync(ct);
        foreach (var d in folgende)
        {
            d.Art = dienst.Art;
            d.Titel = dienst.Titel;
            d.Thema = dienst.Thema;
            d.Ort = dienst.Ort;
            d.Abteilungen.Clear();
            d.Abteilungen.AddRange(dienst.Abteilungen);
            d.GruppeId = dienst.GruppeId;
            d.QualifikationId = dienst.QualifikationId;
            d.VerantwortlicherId = dienst.VerantwortlicherId;
            d.AlsDienst = dienst.AlsDienst;
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
        foreach (var d in folgende)
        {
            await AbwesenheitenAbgleichenAsync(d, ct);
            await AbsagenAbgleichenAsync(d, ct);
        }

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

    /// <summary>Rückmeldeschluss in Stunden vor Dienstbeginn aus dem Parameter.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Stunden; 0 heißt bis Dienstbeginn.</returns>
    public async Task<int> RueckmeldungSchlussStundenAsync(CancellationToken ct = default) =>
        await parameter.ZahlAsync(ParameterSchluessel.DienstRueckmeldungSchlussStunden, LokalesDatum(zeit.GetUtcNow()), 0, ct);

    /// <summary>
    /// Speichert die Zu- oder Absage eines Mitglieds; eine spätere Rückmeldung ersetzt die frühere. Bei einem Dienst steht eine
    /// Absage gleich als entschuldigt in der Anwesenheit, eine spätere Zusage nimmt das wieder heraus.
    /// </summary>
    public async Task<RueckmeldungErgebnis> RueckmeldenAsync(Guid dienstId, Guid mitgliedId, RueckmeldungAntwort antwort, string? begruendung, CancellationToken ct = default)
    {
        var dienst = await db.Dienste.AsNoTracking().MitZielgruppe().FirstOrDefaultAsync(d => d.Id == dienstId, ct);
        if (dienst is null)
        {
            return RueckmeldungErgebnis.NichtGefunden;
        }

        if (!(await ProfilAsync(mitgliedId, ct)).Betrifft(dienst, LokalesDatum(dienst.Beginn)))
        {
            return RueckmeldungErgebnis.NichtZielgruppe;
        }

        var jetzt = zeit.GetUtcNow();
        var schluss = await RueckmeldungSchlussStundenAsync(ct);
        var nachtraeglich = false;
        if (!dienst.RueckmeldungMoeglich(jetzt, schluss))
        {
            // Nach dem Schluss geht nur noch die Entschuldigung bis zur Nachfrist (Satzung § 5 Abs. 6).
            if (antwort != RueckmeldungAntwort.Absage || !await EntschuldigungMoeglichAsync(dienst, jetzt, schluss, ct))
            {
                return RueckmeldungErgebnis.Geschlossen;
            }

            nachtraeglich = true;
        }

        await MeldungSpeichernAsync(dienst, mitgliedId, antwort, begruendung, jetzt, ct, unentschuldigtAufheben: nachtraeglich);
        await db.SaveChangesAsync(ct);
        return RueckmeldungErgebnis.Gespeichert;
    }

    /// <summary>
    /// Sammelzuordnung durch Bearbeiter oder Übungsleiter, ohne Rückmeldeschluss: Zu- oder Absage für die Mitglieder eintragen
    /// (eine vorhandene Begründung bleibt bei einer Absage stehen) oder gleich ihre Anwesenheit. Gibt die Zahl der Mitglieder zurück.
    /// </summary>
    public async Task<int> SammelnAsync(Dienst dienst, IReadOnlyCollection<Guid> mitglieder, Sammelmeldung meldung, CancellationToken ct = default)
    {
        var jetzt = zeit.GetUtcNow();
        if (meldung is Sammelmeldung.Zusage or Sammelmeldung.Absage)
        {
            var antwort = meldung == Sammelmeldung.Zusage ? RueckmeldungAntwort.Zusage : RueckmeldungAntwort.Absage;
            var begruendungen = await db.Rueckmeldungen.Where(r => r.DienstId == dienst.Id && mitglieder.Contains(r.MitgliedId))
                .ToDictionaryAsync(r => r.MitgliedId, r => r.Begruendung, ct);
            foreach (var mitgliedId in mitglieder)
            {
                await MeldungSpeichernAsync(dienst, mitgliedId, antwort, begruendungen.GetValueOrDefault(mitgliedId), jetzt, ct);
            }
        }
        else
        {
            var status = meldung switch
            {
                Sammelmeldung.Anwesend => AnwesenheitStatus.Anwesend,
                Sammelmeldung.Entschuldigt => AnwesenheitStatus.Entschuldigt,
                _ => AnwesenheitStatus.Unentschuldigt,
            };
            var vorhanden = await db.Anwesenheiten.Where(a => a.DienstId == dienst.Id && mitglieder.Contains(a.MitgliedId))
                .ToDictionaryAsync(a => a.MitgliedId, ct);
            foreach (var mitgliedId in mitglieder)
            {
                if (!vorhanden.TryGetValue(mitgliedId, out var anwesenheit))
                {
                    anwesenheit = new Anwesenheit { DienstId = dienst.Id, MitgliedId = mitgliedId };
                    db.Anwesenheiten.Add(anwesenheit);
                }

                anwesenheit.Status = status;
                anwesenheit.ErfasstUeber = ErfassungsWeg.Web;
            }

            var gespeichert = await db.Dienste.FirstAsync(d => d.Id == dienst.Id, ct);
            if (gespeichert.Status == DienstStatus.Geplant && gespeichert.Beginn <= jetzt)
            {
                gespeichert.Status = DienstStatus.Durchgefuehrt;
            }
        }

        await db.SaveChangesAsync(ct);
        return mitglieder.Count;
    }

    /// <summary>
    /// Nach dem Ändern eines Dienstes: Ist er als Dienst geplant, stehen alle Absagen ohne Eintrag als entschuldigt in der Anwesenheit;
    /// sonst fallen die nur aus Absagen übernommenen Einträge weg.
    /// </summary>
    public async Task AbsagenAbgleichenAsync(Dienst dienst, CancellationToken ct = default)
    {
        if (dienst.AlsDienst)
        {
            var vorhanden = await db.Anwesenheiten.Where(a => a.DienstId == dienst.Id).Select(a => a.MitgliedId).ToListAsync(ct);
            var absagen = await db.Rueckmeldungen
                .Where(r => r.DienstId == dienst.Id && r.Antwort == RueckmeldungAntwort.Absage && !vorhanden.Contains(r.MitgliedId))
                .Select(r => r.MitgliedId).ToListAsync(ct);
            db.Anwesenheiten.AddRange(absagen.Select(m => Entschuldigt(dienst.Id, m)));
        }
        else
        {
            db.Anwesenheiten.RemoveRange(await db.Anwesenheiten
                .Where(a => a.DienstId == dienst.Id && a.ErfasstUeber == ErfassungsWeg.Rueckmeldung && a.Status == AnwesenheitStatus.Entschuldigt)
                .ToListAsync(ct));
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Speichert eine Zu- oder Absage und gleicht die Anwesenheit eines Dienstes an; gespeichert wird beim Aufrufer.</summary>
    /// <param name="dienst">Der Dienst.</param>
    /// <param name="mitgliedId">Schlüssel des Mitglieds.</param>
    /// <param name="antwort">Zusage oder Absage.</param>
    /// <param name="begruendung">Begründung einer Absage, höchstens 500 Zeichen.</param>
    /// <param name="jetzt">Zeitpunkt der Meldung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <param name="unentschuldigtAufheben">Eine fristgerechte Entschuldigung macht aus „unentschuldigt“ wieder „entschuldigt“.</param>
    /// <param name="abwesenheitId">Die Absage stammt aus dieser gemeldeten Abwesenheit.</param>
    /// <returns>Die gespeicherte Rückmeldung.</returns>
    private async Task<Rueckmeldung> MeldungSpeichernAsync(Dienst dienst, Guid mitgliedId, RueckmeldungAntwort antwort, string? begruendung, DateTimeOffset jetzt,
        CancellationToken ct, bool unentschuldigtAufheben = false, Guid? abwesenheitId = null)
    {
        var rueckmeldung = await db.Rueckmeldungen.FirstOrDefaultAsync(r => r.DienstId == dienst.Id && r.MitgliedId == mitgliedId, ct);
        if (rueckmeldung is null)
        {
            rueckmeldung = new Rueckmeldung { DienstId = dienst.Id, MitgliedId = mitgliedId };
            db.Rueckmeldungen.Add(rueckmeldung);
        }

        rueckmeldung.Antwort = antwort;
        rueckmeldung.Begruendung = antwort == RueckmeldungAntwort.Absage && !string.IsNullOrWhiteSpace(begruendung)
            ? begruendung.Trim()[..Math.Min(begruendung.Trim().Length, 500)]
            : null;
        rueckmeldung.GemeldetAm = jetzt;
        rueckmeldung.AbwesenheitId = antwort == RueckmeldungAntwort.Absage ? abwesenheitId ?? rueckmeldung.AbwesenheitId : null;

        if (!dienst.AlsDienst)
        {
            return rueckmeldung;
        }

        // Von Hand erfasste Anwesenheit bleibt; nur der aus der Absage übernommene Eintrag folgt der Rückmeldung.
        var anwesenheit = await db.Anwesenheiten.FirstOrDefaultAsync(a => a.DienstId == dienst.Id && a.MitgliedId == mitgliedId, ct);
        if (antwort == RueckmeldungAntwort.Absage && anwesenheit is null)
        {
            db.Anwesenheiten.Add(Entschuldigt(dienst.Id, mitgliedId));
        }
        else if (antwort == RueckmeldungAntwort.Zusage && anwesenheit is { ErfasstUeber: ErfassungsWeg.Rueckmeldung, Status: AnwesenheitStatus.Entschuldigt })
        {
            db.Anwesenheiten.Remove(anwesenheit);
        }
        else if (antwort == RueckmeldungAntwort.Absage && unentschuldigtAufheben && anwesenheit is { Status: AnwesenheitStatus.Unentschuldigt })
        {
            anwesenheit.Status = AnwesenheitStatus.Entschuldigt;
            anwesenheit.ErfasstUeber = ErfassungsWeg.Rueckmeldung;
        }

        return rueckmeldung;
    }

    private static Anwesenheit Entschuldigt(Guid dienstId, Guid mitgliedId) => new()
    {
        DienstId = dienstId, MitgliedId = mitgliedId, Status = AnwesenheitStatus.Entschuldigt, ErfasstUeber = ErfassungsWeg.Rueckmeldung,
    };

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
        Abteilungen = [.. vorlage.Abteilungen],
        GruppeId = vorlage.GruppeId,
        QualifikationId = vorlage.QualifikationId,
        VerantwortlicherId = vorlage.VerantwortlicherId,
        AlsDienst = vorlage.AlsDienst,
        RueckmeldungErbeten = vorlage.RueckmeldungErbeten,
        ZaehltFuerFortbildung = vorlage.ZaehltFuerFortbildung,
        Status = DienstStatus.Geplant,
    };
}

/// <summary>Wiederverwendbare Abfragen auf Dienste.</summary>
public static class DienstAbfragen
{
    /// <summary>Lädt Abteilungen, Gruppe und Qualifikation mit; ohne die Abteilungen gälte ein Dienst als „für alle“.</summary>
    public static IQueryable<Dienst> MitZielgruppe(this IQueryable<Dienst> abfrage) =>
        abfrage.Include(d => d.Abteilungen).Include(d => d.Gruppe).Include(d => d.Qualifikation);
}
