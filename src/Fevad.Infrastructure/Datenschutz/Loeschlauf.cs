using Fevad.Domain.Atemschutz;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>
/// Wendet die Fristen des Löschkonzepts an: löscht oder leert fällige Daten endgültig, schwärzt die zugehörigen
/// Einträge im Änderungsprotokoll und schreibt je Regel einen <see cref="Loeschnachweis"/>.
/// Alle Fristen kommen aus <see cref="Parameter"/>; der Lauf ist idempotent und darf jederzeit wiederholt werden.
/// </summary>
public sealed class Loeschlauf(AppDbContext db, ParameterLeser parameter, ILogger<Loeschlauf> log)
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public const string RegelLauf = "Löschlauf";
    public const string RegelAlarmdaten = "L1 Alarm-Rohdaten";
    public const string RegelEinsatzbeteiligte = "L3 Beteiligte im Einsatzbericht";
    public const string RegelKontaktdaten = "L6 Kontaktdaten nach Austritt";
    public const string RegelRestdatensatz = "L7 Restdatensatz anonymisiert";
    public const string RegelAtemschutznachweise = "L8 Atemschutznachweise";
    public const string RegelAnwesenheiten = "L5 Anwesenheiten verdichtet";
    public const string RegelGroessen = "L14 Größenprofil nach Austritt";
    public const string RegelAusstattungen = "L14 Ausstattung nach Rückgabe";
    public const string RegelErziehungsberechtigte = "L15 Erziehungsberechtigte";
    public const string RegelPruefhinweise = "L31 Prüfhinweise";
    public const string RegelAntraege = "L32 Anträge abgeschlossen";
    public const string RegelAntragsentwuerfe = "L33 Antragsentwürfe";
    public const string RegelFruehereAnschriften = "L34 Frühere Anschriften";
    public const string RegelElternzuordnung = "L35 Elternzuordnung";
    public const string RegelUnfaelle = "L36 Dienstunfälle";
    public const string RegelBankverbindungen = "L37 Bankverbindungen";
    public const string RegelErinnerungslaeufe = "L10a Erinnerungsläufe";
    public const string RegelKonten = "L18 Benutzerkonten";
    public const string RegelAenderungsprotokoll = "L19 Änderungsprotokoll";
    public const string RegelAenderungsprotokollVerdichtet = "L19 Änderungsprotokoll verdichtet";
    public const string RegelBegruendungen = "L24 Absage-Begründungen";
    public const string RegelRueckmeldungen = "L25 Rückmeldungen";
    public const string RegelDokument = "L29 Dokument endgültig gelöscht";
    public const string RegelDokumentVersionen = "L29 alte Dokumentversionen";

    public sealed record Ergebnis(string Regel, int Anzahl);

    /// <summary>
    /// Nächtlicher Aufruf: führt den Lauf aus, wenn die Startstunde erreicht ist und heute noch keiner lief.
    /// Gibt null zurück, wenn nichts zu tun war.
    /// </summary>
    public async Task<IReadOnlyList<Ergebnis>?> FaelligAusfuehrenAsync(DateTimeOffset jetzt, CancellationToken ct)
    {
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(jetzt, Berlin).DateTime);
        var abStunde = await parameter.ZahlAsync(ParameterSchluessel.LoeschlaufAbStunde, heute, 3, ct);
        var letzter = await db.Loeschnachweise.Where(l => l.Regel == RegelLauf).MaxAsync(l => (DateTimeOffset?)l.Zeitpunkt, ct);
        return IstFaellig(jetzt, abStunde, letzter) ? await AusfuehrenAsync(jetzt, null, ct) : null;
    }

    /// <summary>Einmal je Kalendertag (Ortszeit) ab der Startstunde; ein Lauf von Hand am selben Tag zählt mit.</summary>
    public static bool IstFaellig(DateTimeOffset jetzt, int abStunde, DateTimeOffset? letzterLauf)
    {
        var lokal = TimeZoneInfo.ConvertTime(jetzt, Berlin);
        return lokal.Hour >= abStunde
            && (letzterLauf is null || TimeZoneInfo.ConvertTime(letzterLauf.Value, Berlin).Date < lokal.Date);
    }

    /// <summary>Wendet alle Regeln sofort an, z. B. auf Knopfdruck des Administrators.</summary>
    public async Task<IReadOnlyList<Ergebnis>> AusfuehrenAsync(DateTimeOffset jetzt, Guid? ausgeloestVon, CancellationToken ct)
    {
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(jetzt, Berlin).DateTime);
        async Task<int> Monate(string schluessel, int ersatz) => await parameter.ZahlAsync(schluessel, heute, ersatz, ct);

        var begruendungMonate = await Monate(ParameterSchluessel.RueckmeldungBegruendungLoeschenMonate, 3);
        var rueckmeldungMonate = await Monate(ParameterSchluessel.RueckmeldungLoeschenMonate, 12);
        var kontaktMonate = await Monate(ParameterSchluessel.KontaktdatenLoeschenNachAustrittMonate, 3);
        var elternBisAlter = await parameter.ZahlAsync(ParameterSchluessel.ErziehungsberechtigteLoeschenAbAlter, heute, 18, ct);
        var pruefhinweisMonate = await Monate(ParameterSchluessel.PruefhinweiseLoeschenNachMonate, 12);
        var kontoMonate = await Monate(ParameterSchluessel.KontoLoeschenNachDeaktivierungMonate, 6);
        var protokollMonate = await Monate(ParameterSchluessel.AenderungsprotokollAufbewahrungMonate, 36);
        var erinnerungMonate = await Monate(ParameterSchluessel.ErinnerungslaufAufbewahrungMonate, 12);
        var antragMonate = await Monate(ParameterSchluessel.AntraegeLoeschenNachMonate, 12);
        var entwurfMonate = await Monate(ParameterSchluessel.AntragsentwuerfeLoeschenNachMonate, 6);
        var anschriftMonate = await Monate(ParameterSchluessel.AnschriftenVerlaufLoeschenNachMonate, 24);
        var volljaehrig = await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, heute, 18, ct);
        var beteiligteMonate = await Monate(ParameterSchluessel.EinsatzbeteiligteLoeschenNachMonate, 12);
        var atemschutzJahre = await parameter.ZahlAsync(ParameterSchluessel.AtemschutznachweiseAufbewahrungJahre, heute, 40, ct);
        var anwesenheitJahre = await parameter.ZahlAsync(ParameterSchluessel.AnwesenheitenVerdichtenNachJahren, heute, 6, ct);
        var ausstattungMonate = await Monate(ParameterSchluessel.AusstattungLoeschenNachRueckgabeMonate, 12);
        var verdichtenMonate = await Monate(ParameterSchluessel.AenderungsprotokollVerdichtenNachMonate, 12);
        var restJahre = await parameter.ZahlAsync(ParameterSchluessel.RestdatensatzAufbewahrungJahre, heute, 10, ct);
        var unfallJahre = await parameter.ZahlAsync(ParameterSchluessel.UnfaelleLoeschenNachJahren, heute, 5, ct);
        var bankMonate = await parameter.ZahlAsync(ParameterSchluessel.BankverbindungLoeschenNachMonate, heute, 12, ct);

        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        var ergebnisse = new List<Ergebnis>
        {
            new(RegelAlarmdaten, await AlarmdatenAsync(heute, ct)),
            new(RegelBegruendungen, await BegruendungenAsync(jetzt.AddMonths(-begruendungMonate), ct)),
            new(RegelRueckmeldungen, await RueckmeldungenAsync(jetzt.AddMonths(-rueckmeldungMonate), ct)),
            new(RegelEinsatzbeteiligte, await EinsatzbeteiligteAsync(heute.AddMonths(-beteiligteMonate), ct)),
        };
        ergebnisse.Add(new(RegelAnwesenheiten, await AnwesenheitenVerdichtenAsync(heute, anwesenheitJahre, ct)));
        var (kontakt, groessen, eltern, hinweise, anschriften) = await AusgetreteneAsync(
            heute, heute.AddMonths(-kontaktMonate), heute.AddYears(-elternBisAlter), jetzt.AddMonths(-pruefhinweisMonate), heute.AddMonths(-anschriftMonate), ct);
        ergebnisse.Add(new(RegelKontaktdaten, kontakt));
        ergebnisse.Add(new(RegelFruehereAnschriften, anschriften));
        ergebnisse.Add(new(RegelElternzuordnung, await ElternzuordnungenAsync(heute, volljaehrig, jetzt, ct)));
        ergebnisse.Add(new(RegelErziehungsberechtigte, eltern));
        ergebnisse.Add(new(RegelPruefhinweise, hinweise));
        ergebnisse.Add(new(RegelGroessen, groessen));
        ergebnisse.Add(new(RegelAusstattungen, await AusstattungenAsync(heute.AddMonths(-ausstattungMonate), ct)));
        ergebnisse.Add(new(RegelAtemschutznachweise, await AtemschutznachweiseAsync(heute.AddYears(-atemschutzJahre), ct)));
        ergebnisse.Add(new(RegelUnfaelle, await UnfaelleAsync(heute.AddYears(-unfallJahre), ct)));
        ergebnisse.Add(new(RegelBankverbindungen, await BankverbindungenAsync(heute.AddMonths(-bankMonate), ct)));
        ergebnisse.Add(new(RegelRestdatensatz, await RestdatensaetzeAsync(heute.AddYears(-restJahre), jetzt, ct)));
        ergebnisse.Add(new(RegelKonten, await KontenAsync(jetzt.AddMonths(-kontoMonate), jetzt, ct)));
        var (antraege, entwuerfe) = await AntraegeAsync(heute.AddMonths(-antragMonate), jetzt.AddMonths(-entwurfMonate), ct);
        ergebnisse.Add(new(RegelAntraege, antraege));
        ergebnisse.Add(new(RegelAntragsentwuerfe, entwuerfe));
        var erinnerungGrenze = jetzt.AddMonths(-erinnerungMonate);
        ergebnisse.Add(new(RegelErinnerungslaeufe, await db.Erinnerungslaeufe.IgnoreQueryFilters()
            .Where(e => e.Zeitpunkt < erinnerungGrenze).ExecuteDeleteAsync(ct)));

        var protokollGrenze = jetzt.AddMonths(-protokollMonate);
        ergebnisse.Add(new(RegelAenderungsprotokoll, await db.AuditEintraege
            .Where(a => a.Zeitpunkt < protokollGrenze).ExecuteDeleteAsync(ct)));

        // L19 Verdichtung: wer wann was geändert hat, bleibt bis zur Aufbewahrungsfrist; die Feldwerte selbst nur kürzer.
        var verdichtenGrenze = jetzt.AddMonths(-verdichtenMonate);
        ergebnisse.Add(new(RegelAenderungsprotokollVerdichtet, await db.AuditEintraege
            .Where(a => a.Zeitpunkt < verdichtenGrenze && a.Aenderungen != null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Aenderungen, (string?)null), ct)));

        foreach (var e in ergebnisse.Where(e => e.Anzahl > 0))
        {
            db.Loeschnachweise.Add(new Loeschnachweis { Zeitpunkt = jetzt, Regel = e.Regel, Anzahl = e.Anzahl, AusgeloestVon = ausgeloestVon });
        }

        db.Loeschnachweise.Add(new Loeschnachweis { Zeitpunkt = jetzt, Regel = RegelLauf, Anzahl = ergebnisse.Sum(e => e.Anzahl), AusgeloestVon = ausgeloestVon });
        await db.SaveChangesAsync(ct);
        await transaktion.CommitAsync(ct);

        log.LogInformation("Löschlauf: {Ergebnisse}", string.Join(", ", ergebnisse.Select(e => $"{e.Regel} {e.Anzahl}")));
        return ergebnisse;
    }

    /// <summary>
    /// L3: Beteiligte und Ansprechpartner anderer Organisationen, wenn das Einsatzjahr vor der Grenze endete
    /// und kein Kostenersatz offen ist bzw. sein Abschluss vor der Grenze liegt. Der Einsatzbericht selbst bleibt (L2).
    /// </summary>
    private async Task<int> EinsatzbeteiligteAsync(DateOnly grenze, CancellationToken ct)
    {
        // Einsatzjahr endet am 31.12.; vor der Grenze heißt: Einsatz vor dem 01.01. des Grenzjahres (Ortszeit).
        var jahresbeginn = Ortszeit.TagesbeginnUtc(new DateOnly(grenze.Year, 1, 1));
        var einsaetze = db.Einsaetze.IgnoreQueryFilters()
            .Where(e => e.Alarmiert < jahresbeginn && !e.KostenersatzOffen
                && (e.KostenersatzAbgeschlossenAm == null || e.KostenersatzAbgeschlossenAm < grenze))
            .Select(e => e.Id);

        var beteiligte = await db.EinsatzBeteiligte.IgnoreQueryFilters().Where(b => einsaetze.Contains(b.EinsatzId)).Select(b => b.Id).ToListAsync(ct);
        var organisationen = await db.EinsatzOrganisationen.IgnoreQueryFilters().Where(o => einsaetze.Contains(o.EinsatzId)).Select(o => o.Id).ToListAsync(ct);
        if (beteiligte.Count > 0)
        {
            await db.EinsatzBeteiligte.IgnoreQueryFilters().Where(b => beteiligte.Contains(b.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<EinsatzBeteiligter>(beteiligte, ct);
        }

        if (organisationen.Count > 0)
        {
            await db.EinsatzOrganisationen.IgnoreQueryFilters().Where(o => organisationen.Contains(o.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<EinsatzOrganisation>(organisationen, ct);
        }

        return beteiligte.Count + organisationen.Count;
    }

    /// <summary>
    /// L5: Anwesenheiten eines Kalenderjahres werden nach der Frist ab Jahresende zu einer Jahressumme je Mitglied verdichtet
    /// (besuchte, entschuldigte, unentschuldigte Dienste, Minuten, davon Fortbildung) und dann gelöscht. Abrechnungsposten
    /// verlieren nur den Verweis auf die Einzelanwesenheit, die Posten selbst bleiben. Zählt die gelöschten Anwesenheiten.
    /// </summary>
    private async Task<int> AnwesenheitenVerdichtenAsync(DateOnly heute, int jahre, CancellationToken ct)
    {
        // Das Jahr J ist fällig, wenn sein 31.12. plus Frist vor heute liegt: alle Dienste vor dem 1.1. des Jahres (heute − Frist).
        var grenze = Ortszeit.TagesbeginnUtc(new DateOnly(heute.AddYears(-jahre).Year, 1, 1));
        var faellig = await db.Anwesenheiten.IgnoreQueryFilters()
            .Where(a => a.Dienst!.Beginn < grenze)
            .Select(a => new { a.Id, a.MitgliedId, a.Status, a.Von, a.Bis, a.GeloeschtAm, a.Dienst!.Beginn, a.Dienst.Ende, a.Dienst.ZaehltFuerFortbildung })
            .ToListAsync(ct);
        if (faellig.Count == 0)
        {
            return 0;
        }

        // Nur Anwesenheiten zählen, die in der Oberfläche noch sichtbar waren; ausgeblendete werden nur entfernt.
        var werte = faellig
            .Where(a => a.GeloeschtAm == null)
            .Select(a => new Anwesenheitsjahressumme.Einzelwert(
                a.MitgliedId, Ortszeit.Datum(a.Beginn).Year, a.Status,
                a.Status == AnwesenheitStatus.Anwesend ? (a.Bis ?? a.Ende) - (a.Von ?? a.Beginn) : TimeSpan.Zero, a.ZaehltFuerFortbildung))
            .ToList();
        var mitglieder = werte.Select(w => w.MitgliedId).Distinct().ToList();
        var vorhandene = await db.Anwesenheitsjahressummen.Where(s => mitglieder.Contains(s.MitgliedId)).ToListAsync(ct);
        foreach (var summe in Anwesenheitsjahressumme.Verdichten(werte, vorhandene))
        {
            if (!vorhandene.Contains(summe))
            {
                db.Anwesenheitsjahressummen.Add(summe);
            }
        }

        await db.SaveChangesAsync(ct);

        var ids = faellig.Select(a => a.Id).ToList();
        await db.AbrechnungsPosten.IgnoreQueryFilters().Where(p => p.AnwesenheitId != null && ids.Contains(p.AnwesenheitId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.AnwesenheitId, (Guid?)null), ct);
        await db.Anwesenheiten.IgnoreQueryFilters().Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<Anwesenheit>(ids, ct);
        return ids.Count;
    }

    /// <summary>
    /// L14: Zurückgegebene Ausstattung nach der Frist ab Rückgabe löschen, dazu Kleiderkammer-Vorgänge, auf die danach
    /// keine Ausstattung mehr verweist. Das Einzelstück selbst (Gerät) bleibt mit seiner Prüfhistorie.
    /// </summary>
    private async Task<int> AusstattungenAsync(DateOnly grenze, CancellationToken ct)
    {
        var ausstattungen = await db.Ausstattungen.IgnoreQueryFilters()
            .Where(a => a.ZurueckAm != null && a.ZurueckAm < grenze)
            .Select(a => a.Id).ToListAsync(ct);
        if (ausstattungen.Count > 0)
        {
            await db.Ausstattungen.IgnoreQueryFilters().Where(a => ausstattungen.Contains(a.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Ausstattung>(ausstattungen, ct);
        }

        var grenzeUtc = Ortszeit.TagesbeginnUtc(grenze);
        var vorgaenge = await db.Kleiderkammervorgaenge.IgnoreQueryFilters()
            .Where(v => v.Zeitpunkt < grenzeUtc
                && !db.Ausstattungen.IgnoreQueryFilters().Any(a => a.AusgabeVorgangId == v.Id || a.RuecknahmeVorgangId == v.Id))
            .Select(v => v.Id).ToListAsync(ct);
        if (vorgaenge.Count > 0)
        {
            await db.Kleiderkammervorgaenge.IgnoreQueryFilters().Where(v => vorgaenge.Contains(v.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Kleiderkammervorgang>(vorgaenge, ct);
        }

        return ausstattungen.Count + vorgaenge.Count;
    }

    /// <summary>
    /// L7: Der Restdatensatz eines Ausgetretenen wird nach der Frist anonymisiert, sobald auch keine Atemschutznachweise (L8)
    /// mehr vorhanden sind. Name, Personalnummer und Kennungen werden ersetzt, das Geburtsdatum auf den 1. Januar des Jahrzehnts gerundet (Altersgruppen bleiben, das Geburtsjahr nicht),
    /// Dienstgrade, Lehrgänge, Nachweise, Ehrungen, Gruppen und Vorsorgen gelöscht und ein Konto entkoppelt. Mitgliedschaftszeiträume,
    /// Funktionen, Einsatzteilnahmen, Anwesenheitssummen und Abrechnungsposten bleiben ohne Personenbezug für Statistik und Kasse.
    /// </summary>
    private async Task<int> RestdatensaetzeAsync(DateOnly grenze, DateTimeOffset jetzt, CancellationToken ct)
    {
        var ids = await db.Mitglieder.IgnoreQueryFilters()
            .Where(m => m.AnonymisiertAm == null
                && m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null)
                && !m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null && (s.GueltigBis == null || s.GueltigBis >= grenze))
                && !db.AtemschutzNachweise.IgnoreQueryFilters().Any(n => n.MitgliedId == m.Id))
            .Select(m => new { m.Id, m.Geburtsdatum, m.Personalnummer }).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        var alle = ids.Select(m => m.Id).ToList();
        foreach (var m in ids)
        {
            // Fiktive Testdaten behalten ihr Präfix, damit der Testdaten-Aufräumer sie weiter findet.
            var kennung = (m.Personalnummer.StartsWith("T-", StringComparison.Ordinal) ? "T-" : "") + "X" + m.Id.ToString("N")[^12..];
            await db.Mitglieder.IgnoreQueryFilters().Where(x => x.Id == m.Id).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Personalnummer, kennung)
                .SetProperty(x => x.Vorname, "gelöscht")
                .SetProperty(x => x.Nachname, kennung)
                .SetProperty(x => x.Geburtsdatum, new DateOnly(m.Geburtsdatum.Year / 10 * 10, 1, 1))
                .SetProperty(x => x.Anrede, (string?)null)
                .SetProperty(x => x.Titel, (string?)null)
                .SetProperty(x => x.LegacyMpFeuerId, (string?)null)
                .SetProperty(x => x.Barcode, (string?)null)
                .SetProperty(x => x.FotoEinwilligungAm, (DateOnly?)null)
                .SetProperty(x => x.FotoWiderrufAm, (DateOnly?)null)
                .SetProperty(x => x.Fe2SyncAktiv, false)
                .SetProperty(x => x.AnonymisiertAm, jetzt), ct);
        }

        async Task Loeschen<T>(IQueryable<T> menge, Func<IQueryable<T>, IQueryable<Guid>> id) where T : Entitaet
        {
            var weg = await id(menge).ToListAsync(ct);
            if (weg.Count > 0)
            {
                await menge.Where(e => weg.Contains(e.Id)).ExecuteDeleteAsync(ct);
                await db.ProtokollSchwaerzenAsync<T>(weg, ct);
            }
        }

        await Loeschen(db.Lehrgangsteilnahmen.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.QualifikationNachweise.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.Nachweise.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.Ehrungen.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.DienstgradZuordnungen.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.GruppenMitgliedschaften.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.AtemschutzVorsorgen.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await db.Users.Where(u => u.MitgliedId != null && alle.Contains(u.MitgliedId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.MitgliedId, (Guid?)null), ct);
        await db.ProtokollSchwaerzenAsync<Mitglied>(alle, ct);
        return alle.Count;
    }

    /// <summary>L37: Bankverbindungen von Mitgliedern, die seit der Frist keine Mitgliedschaft mehr haben, dazu gelöschte.</summary>
    private async Task<int> BankverbindungenAsync(DateOnly grenze, CancellationToken ct)
    {
        var ids = await db.Bankverbindungen.IgnoreQueryFilters()
            .Where(b => b.GeloeschtAm != null
                || !db.Mitgliedschaften.IgnoreQueryFilters().Any(s => s.MitgliedId == b.MitgliedId && !s.Vordienstzeit && s.GeloeschtAm == null
                    && (s.GueltigBis == null || s.GueltigBis >= grenze)))
            .Select(b => b.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Bankverbindungen.IgnoreQueryFilters().Where(b => ids.Contains(b.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Fevad.Domain.Entschaedigung.Bankverbindung>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>L36: abgeschlossene Dienstunfälle samt Zeugen nach der Frist ab Abschluss löschen; offene bleiben.</summary>
    private async Task<int> UnfaelleAsync(DateOnly grenze, CancellationToken ct)
    {
        var ids = await db.Unfaelle.IgnoreQueryFilters()
            .Where(u => (u.AbgeschlossenAm != null && u.AbgeschlossenAm < grenze) || u.GeloeschtAm != null)
            .Select(u => u.Id).ToListAsync(ct);
        var zeugen = await db.UnfallZeugen.IgnoreQueryFilters().Where(z => ids.Contains(z.UnfallId) || z.GeloeschtAm != null)
            .Select(z => z.Id).ToListAsync(ct);
        if (zeugen.Count > 0)
        {
            await db.UnfallZeugen.IgnoreQueryFilters().Where(z => zeugen.Contains(z.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Fevad.Domain.Unfaelle.UnfallZeuge>(zeugen, ct);
        }

        if (ids.Count > 0)
        {
            await db.Unfaelle.IgnoreQueryFilters().Where(u => ids.Contains(u.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Fevad.Domain.Unfaelle.Unfall>(ids, ct);
        }

        return ids.Count;
    }

    private async Task<int> AlarmdatenAsync(DateOnly heute, CancellationToken ct)
    {
        var ids = await db.AlarmImporte.IgnoreQueryFilters().Where(a => a.LoeschenAb <= heute).Select(a => a.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.AlarmImporte.IgnoreQueryFilters().Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<AlarmImport>(ids, ct);
        return ids.Count;
    }

    private async Task<int> BegruendungenAsync(DateTimeOffset grenze, CancellationToken ct)
    {
        // Auch Rückmeldungen ohne aktuelle Begründung: ältere Protokolleinträge können eine frühere enthalten.
        var ids = await db.Rueckmeldungen.IgnoreQueryFilters()
            .Where(r => db.Dienste.IgnoreQueryFilters().Any(d => d.Id == r.DienstId && d.Ende < grenze))
            .Select(r => r.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        var geleert = await db.Rueckmeldungen.IgnoreQueryFilters().Where(r => ids.Contains(r.Id) && r.Begruendung != null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Begruendung, (string?)null), ct);
        await db.ProtokollSchwaerzenAsync<Rueckmeldung>(ids, ct);
        return geleert;
    }

    private async Task<int> RueckmeldungenAsync(DateTimeOffset grenze, CancellationToken ct)
    {
        var ids = await db.Rueckmeldungen.IgnoreQueryFilters()
            .Where(r => db.Dienste.IgnoreQueryFilters().Any(d => d.Id == r.DienstId && d.Ende < grenze))
            .Select(r => r.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.Rueckmeldungen.IgnoreQueryFilters().Where(r => ids.Contains(r.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<Rueckmeldung>(ids, ct);
        return ids.Count;
    }

    /// <summary>
    /// L8: Die Atemschutznachweise eines Mitglieds (Einsätze und Übungen unter Atemschutz, FwDV 7) sind sein Expositionsverzeichnis
    /// nach § 10a GefStoffV (KoAtEx der DGUV). Sie bleiben, bis der letzte Nachweis des Mitglieds vor dem Stichtag liegt, und werden
    /// dann vollständig gelöscht; L6 und L7 lassen sie unberührt. Der Lauf schreibt nur Anzahlen, keine Inhalte.
    /// </summary>
    private async Task<int> AtemschutznachweiseAsync(DateOnly grenze, CancellationToken ct)
    {
        var faellig = db.AtemschutzNachweise.IgnoreQueryFilters()
            .GroupBy(n => n.MitgliedId)
            .Where(g => g.Max(n => n.Datum) < grenze)
            .Select(g => g.Key);
        var ids = await db.AtemschutzNachweise.IgnoreQueryFilters().Where(n => faellig.Contains(n.MitgliedId)).Select(n => n.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.AtemschutzNachweise.IgnoreQueryFilters().Where(n => ids.Contains(n.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<AtemschutzNachweis>(ids, ct);
        return ids.Count;
    }

    /// <summary>
    /// Ausgetreten ist, wer eine eigene Mitgliedschaft hatte und seit dem Stichtag in keiner Abteilung mehr ist.
    /// Nach einem Tod gilt das ohne Wartefrist ab dem Sterbetag. Der Restdatensatz (Name, Geburtsdatum, Laufbahn)
    /// bleibt für Bescheinigungen und Ehrungen (L7); die Atemschutznachweise bleiben als Expositionsverzeichnis bis zur Frist aus L8.
    /// </summary>
    private async Task<(int Kontakt, int Groessen, int Erziehungsberechtigte, int Pruefhinweise, int Anschriften)> AusgetreteneAsync(
        DateOnly heute, DateOnly grenze, DateOnly volljaehrigBis, DateTimeOffset hinweiseAbgeschlossenVor, DateOnly anschriftenBisVor, CancellationToken ct)
    {
        var ausgetreten = db.Mitglieder.IgnoreQueryFilters()
            .Where(m => m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null)
                && (!m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null && s.Status != MitgliedStatus.Ausgeschieden
                        && (s.GueltigBis == null || s.GueltigBis >= grenze))
                    || (m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null && s.Austrittsgrund == Austrittsgrund.Tod && s.GueltigBis < heute)
                        && !m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null && s.Status != MitgliedStatus.Ausgeschieden
                            && (s.GueltigBis == null || s.GueltigBis >= heute)))));

        var ids = await ausgetreten
            .Where(m => m.Strasse != null || m.Plz != null || m.Ort != null || m.TelefonMobil != null || m.TelefonFestnetz != null
                || m.Email != null || m.Beruf != null || m.Arbeitgeber != null || m.ArbeitgeberStrasse != null || m.ArbeitgeberPlz != null
                || m.Arbeitsort != null || m.ArbeitgeberTelefon != null || m.ArbeitgeberMobil != null || m.ArbeitgeberEmail != null
                || m.Notizen != null || m.Barcode != null || m.Fe2SyncAktiv)
            .Select(m => m.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Mitglieder.IgnoreQueryFilters().Where(m => ids.Contains(m.Id)).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Strasse, (string?)null)
                .SetProperty(m => m.Plz, (string?)null)
                .SetProperty(m => m.Ort, (string?)null)
                .SetProperty(m => m.TelefonMobil, (string?)null)
                .SetProperty(m => m.TelefonFestnetz, (string?)null)
                .SetProperty(m => m.Email, (string?)null)
                .SetProperty(m => m.Beruf, (string?)null)
                .SetProperty(m => m.Arbeitgeber, (string?)null)
                .SetProperty(m => m.ArbeitgeberStrasse, (string?)null)
                .SetProperty(m => m.ArbeitgeberPlz, (string?)null)
                .SetProperty(m => m.Arbeitsort, (string?)null)
                .SetProperty(m => m.ArbeitgeberTelefon, (string?)null)
                .SetProperty(m => m.ArbeitgeberMobil, (string?)null)
                .SetProperty(m => m.ArbeitgeberEmail, (string?)null)
                .SetProperty(m => m.Notizen, (string?)null)
                .SetProperty(m => m.Barcode, (string?)null)
                .SetProperty(m => m.Fe2SyncAktiv, false), ct);
        }

        // Das Protokoll aller Ausgetretenen schwärzen, auch wenn die Felder schon von Hand geleert wurden.
        var ausgetreteneIds = ausgetreten.Select(m => m.Id);
        await db.ProtokollSchwaerzenAsync<Mitglied>(await ausgetreteneIds.ToListAsync(ct), ct);
        var groessenIds = await db.Groessenangaben.IgnoreQueryFilters().Where(g => ausgetreteneIds.Contains(g.MitgliedId))
            .Select(g => g.Id).ToListAsync(ct);
        if (groessenIds.Count > 0)
        {
            await db.Groessenangaben.IgnoreQueryFilters().Where(g => groessenIds.Contains(g.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Groessenangabe>(groessenIds, ct);
        }

        // Erziehungsberechtigte braucht es nur, solange das Mitglied minderjährig und dabei ist (Elterninformation: Löschung mit Volljährigkeit).
        var eltern = await db.Erziehungsberechtigte.IgnoreQueryFilters()
            .Where(e => ausgetreteneIds.Contains(e.MitgliedId)
                || db.Mitglieder.IgnoreQueryFilters().Any(m => m.Id == e.MitgliedId && m.Geburtsdatum <= volljaehrigBis))
            .Select(e => e.Id).ToListAsync(ct);
        if (eltern.Count > 0)
        {
            await db.Erziehungsberechtigte.IgnoreQueryFilters().Where(e => eltern.Contains(e.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Erziehungsberechtigter>(eltern, ct);
        }

        // Prüfhinweise sind nur Vorschläge: abgeschlossene nach Frist, die von Ausgetretenen mit L6.
        var hinweise = await db.Pruefhinweise.IgnoreQueryFilters()
            .Where(p => ausgetreteneIds.Contains(p.MitgliedId)
                || (p.Status != PruefhinweisStatus.Offen && p.AbgeschlossenAm != null && p.AbgeschlossenAm < hinweiseAbgeschlossenVor))
            .Select(p => p.Id).ToListAsync(ct);
        if (hinweise.Count > 0)
        {
            await db.Pruefhinweise.IgnoreQueryFilters().Where(p => hinweise.Contains(p.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Pruefhinweis>(hinweise, ct);
        }

        // Frühere Anschriften nach Frist ab ihrer Ablösung, die von Ausgetretenen mit L6.
        var anschriften = await db.FruehereAnschriften.IgnoreQueryFilters()
            .Where(a => ausgetreteneIds.Contains(a.MitgliedId) || a.BisDatum < anschriftenBisVor || a.GeloeschtAm != null)
            .Select(a => a.Id).ToListAsync(ct);
        if (anschriften.Count > 0)
        {
            await db.FruehereAnschriften.IgnoreQueryFilters().Where(a => anschriften.Contains(a.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<FruehereAnschrift>(anschriften, ct);
        }

        return (ids.Count, groessenIds.Count, eltern.Count, hinweise.Count, anschriften.Count);
    }

    /// <summary>
    /// Elternkonten: Zuordnung aufheben, wenn das Kind volljährig ist, keine Jugend-/Kinderabteilung mehr hat oder eine Sperre
    /// gesetzt wurde; Elternkonten ohne Kind deaktivieren (danach L18). Zählt die aufgehobenen Zuordnungen.
    /// </summary>
    private async Task<int> ElternzuordnungenAsync(DateOnly heute, int volljaehrig, DateTimeOffset jetzt, CancellationToken ct)
    {
        var ids = await db.Erziehungsberechtigte.IgnoreQueryFilters().Where(Elternkonten.Faellig(heute, volljaehrig))
            .Select(e => e.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Erziehungsberechtigte.IgnoreQueryFilters().Where(e => ids.Contains(e.Id)).ExecuteUpdateAsync(s => s
                .SetProperty(e => e.KontoId, (Guid?)null)
                .SetProperty(e => e.KontoZugeordnetAm, (DateTimeOffset?)null), ct);
        }

        await Elternkonten.VerwaisteDeaktivierenAsync(db, jetzt, ct);
        return ids.Count;
    }

    /// <summary>
    /// Anträge: entschiedene oder zurückgezogene nach Frist ab der Entscheidung (L32), Entwürfe nach Frist ab der letzten
    /// Änderung und verworfene sofort (L33). Angenommene Anträge sind dann schon ins Mitglied übernommen.
    /// </summary>
    private async Task<(int Abgeschlossen, int Entwuerfe)> AntraegeAsync(DateOnly entschiedenVor, DateTimeOffset geaendertVor, CancellationToken ct)
    {
        var abgeschlossen = await db.Antraege.IgnoreQueryFilters()
            .Where(a => a.GeloeschtAm == null && a.Status != AntragStatus.Entwurf && a.Status != AntragStatus.Eingegangen
                && a.EntschiedenAm != null && a.EntschiedenAm < entschiedenVor)
            .Select(a => a.Id).ToListAsync(ct);
        var entwuerfe = await db.Antraege.IgnoreQueryFilters()
            .Where(a => a.GeloeschtAm != null || (a.Status == AntragStatus.Entwurf && (a.GeaendertAm ?? a.ErstelltAm) < geaendertVor))
            .Select(a => a.Id).ToListAsync(ct);
        var alle = abgeschlossen.Concat(entwuerfe).ToList();

        // Auch einzeln entfernte Erziehungsberechtigte noch laufender Anträge.
        var eltern = await db.AntragElternteile.IgnoreQueryFilters()
            .Where(e => alle.Contains(e.AntragId) || e.GeloeschtAm != null)
            .Select(e => e.Id).ToListAsync(ct);
        if (eltern.Count > 0)
        {
            await db.AntragElternteile.IgnoreQueryFilters().Where(e => eltern.Contains(e.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<AntragElternteil>(eltern, ct);
        }

        if (alle.Count > 0)
        {
            await db.Antraege.IgnoreQueryFilters().Where(a => alle.Contains(a.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Antrag>(alle, ct);
        }

        return (abgeschlossen.Count, entwuerfe.Count);
    }

    /// <summary>
    /// Leert Konten, die länger als die Frist deaktiviert sind. Gelöscht wird nicht, weil Lesebestätigungen und
    /// Protokolleinträge auf die ID verweisen; übrig bleibt ein Konto ohne Namen, Adresse, Passwort und Rollen.
    /// </summary>
    private async Task<int> KontenAsync(DateTimeOffset grenze, DateTimeOffset jetzt, CancellationToken ct)
    {
        var ids = await db.Users.Where(u => u.DeaktiviertAm != null && u.DeaktiviertAm < grenze && u.AnonymisiertAm == null)
            .Select(u => u.Id).ToListAsync(ct);
        var anonymisiert = new List<Guid>();
        foreach (var id in ids)
        {
            var name = $"geloescht-{id:N}";
            // Bedingung erneut prüfen: ein inzwischen wieder aktiviertes Konto bleibt unberührt. Der neue
            // ConcurrencyStamp lässt eine Speicherung aus einem vorher geladenen Formular scheitern.
            var geaendert = await db.Users
                .Where(u => u.Id == id && u.DeaktiviertAm != null && u.DeaktiviertAm < grenze && u.AnonymisiertAm == null)
                .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.UserName, name)
                .SetProperty(u => u.NormalizedUserName, name.ToUpperInvariant())
                .SetProperty(u => u.Email, (string?)null)
                .SetProperty(u => u.NormalizedEmail, (string?)null)
                .SetProperty(u => u.EmailConfirmed, false)
                .SetProperty(u => u.PasswordHash, (string?)null)
                .SetProperty(u => u.PhoneNumber, (string?)null)
                .SetProperty(u => u.PhoneNumberConfirmed, false)
                .SetProperty(u => u.TwoFactorEnabled, false)
                .SetProperty(u => u.SecurityStamp, Guid.NewGuid().ToString())
                .SetProperty(u => u.KalenderToken, (string?)null)
                .SetProperty(u => u.MitgliedId, (Guid?)null)
                .SetProperty(u => u.LetzteAnmeldungAm, (DateTimeOffset?)null)
                .SetProperty(u => u.ConcurrencyStamp, Guid.NewGuid().ToString())
                .SetProperty(u => u.AnonymisiertAm, jetzt), ct);
            if (geaendert > 0)
            {
                anonymisiert.Add(id);
            }
        }

        if (anonymisiert.Count > 0)
        {
            await db.UserRoles.Where(r => anonymisiert.Contains(r.UserId)).ExecuteDeleteAsync(ct);
            await db.UserTokens.Where(t => anonymisiert.Contains(t.UserId)).ExecuteDeleteAsync(ct);
            await db.UserClaims.Where(c => anonymisiert.Contains(c.UserId)).ExecuteDeleteAsync(ct);
            await db.UserLogins.Where(l => anonymisiert.Contains(l.UserId)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Benutzer>(anonymisiert, ct);
            await db.ProtokollSchwaerzenAsync<IdentityUserRole<Guid>>(anonymisiert, ct);
        }

        return anonymisiert.Count;
    }
}
