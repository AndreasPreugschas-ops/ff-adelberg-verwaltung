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
    public const string RegelKontaktdaten = "L6 Kontaktdaten nach Austritt";
    public const string RegelGroessen = "L14 Größenprofil nach Austritt";
    public const string RegelErziehungsberechtigte = "L15 Erziehungsberechtigte";
    public const string RegelPruefhinweise = "L31 Prüfhinweise";
    public const string RegelAntraege = "L32 Anträge abgeschlossen";
    public const string RegelAntragsentwuerfe = "L33 Antragsentwürfe";
    public const string RegelFruehereAnschriften = "L34 Frühere Anschriften";
    public const string RegelErinnerungslaeufe = "L10a Erinnerungsläufe";
    public const string RegelKonten = "L18 Benutzerkonten";
    public const string RegelAenderungsprotokoll = "L19 Änderungsprotokoll";
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

        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        var ergebnisse = new List<Ergebnis>
        {
            new(RegelAlarmdaten, await AlarmdatenAsync(heute, ct)),
            new(RegelBegruendungen, await BegruendungenAsync(jetzt.AddMonths(-begruendungMonate), ct)),
            new(RegelRueckmeldungen, await RueckmeldungenAsync(jetzt.AddMonths(-rueckmeldungMonate), ct)),
        };
        var (kontakt, groessen, eltern, hinweise, anschriften) = await AusgetreteneAsync(
            heute.AddMonths(-kontaktMonate), heute.AddYears(-elternBisAlter), jetzt.AddMonths(-pruefhinweisMonate), heute.AddMonths(-anschriftMonate), ct);
        ergebnisse.Add(new(RegelKontaktdaten, kontakt));
        ergebnisse.Add(new(RegelFruehereAnschriften, anschriften));
        ergebnisse.Add(new(RegelErziehungsberechtigte, eltern));
        ergebnisse.Add(new(RegelPruefhinweise, hinweise));
        ergebnisse.Add(new(RegelGroessen, groessen));
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
    /// Ausgetreten ist, wer eine eigene Mitgliedschaft hatte und seit dem Stichtag in keiner Abteilung mehr ist.
    /// Der Restdatensatz (Name, Geburtsdatum, Laufbahn) bleibt für Bescheinigungen und Ehrungen (L7).
    /// </summary>
    private async Task<(int Kontakt, int Groessen, int Erziehungsberechtigte, int Pruefhinweise, int Anschriften)> AusgetreteneAsync(
        DateOnly grenze, DateOnly volljaehrigBis, DateTimeOffset hinweiseAbgeschlossenVor, DateOnly anschriftenBisVor, CancellationToken ct)
    {
        var ausgetreten = db.Mitglieder.IgnoreQueryFilters()
            .Where(m => m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null)
                && !m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null && s.Status != MitgliedStatus.Ausgeschieden
                    && (s.GueltigBis == null || s.GueltigBis >= grenze)));

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
