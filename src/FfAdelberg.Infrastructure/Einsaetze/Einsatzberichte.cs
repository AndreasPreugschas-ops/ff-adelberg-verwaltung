using FfAdelberg.Domain.Einsaetze;
using FfAdelberg.Domain.Gemeinsam;
using FfAdelberg.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FfAdelberg.Infrastructure.Einsaetze;

/// <summary>Legt Einsatzberichte an: leer von Hand oder als Entwurf aus einem alamos-FE2-Alarm.</summary>
public sealed class Einsatzberichte(AppDbContext db, ParameterLeser parameter, ILogger<Einsatzberichte> log)
{
    private const int Versuche = 5;
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    /// <summary>Leerer Bericht mit der nächsten freien Einsatznummer.</summary>
    public async Task<Einsatz> NeuAnlegenAsync(DateTimeOffset alarmiert, CancellationToken ct = default)
    {
        var einsatz = new Einsatz { Einsatznummer = "", Alarmiert = alarmiert, Status = EinsatzStatus.Entwurf };
        db.Einsaetze.Add(einsatz);
        await MitNummerSpeichernAsync(einsatz, ct);
        return einsatz;
    }

    /// <summary>
    /// Erzeugt aus einem gespeicherten Alarm einen Berichtsentwurf und markiert den Alarm als verarbeitet.
    /// Gibt es schon einen Bericht mit derselben Leitstellennummer, wird der Alarm nur damit verknüpft.
    /// </summary>
    public async Task<Einsatz?> EntwurfAusAlarmAsync(AlarmImport import, CancellationToken ct = default)
    {
        var alarm = Fe2Alarm.Lesen(import.Rohdaten);
        if (!alarm.IstAlarm)
        {
            import.Verarbeitet = true;
            await db.SaveChangesAsync(ct);
            return null;
        }

        // Verknüpft wird nur über die vollständige ID; eine gekürzte könnte zu einem fremden Einsatz passen.
        var externeId = alarm.ExterneId ?? import.ExterneId;
        var leitstellenNummer = Kuerzen(externeId, 50);
        var vorhanden = externeId.Length > 50
            ? null
            : await db.Einsaetze.FirstOrDefaultAsync(e => e.LeitstellenNummer == externeId, ct);
        if (vorhanden is not null)
        {
            import.EinsatzId = vorhanden.Id;
            import.Verarbeitet = true;
            await db.SaveChangesAsync(ct);
            return vorhanden;
        }

        var alarmiert = alarm.Zeitpunkt ?? import.EmpfangenAm;
        var einsatz = new Einsatz
        {
            Einsatznummer = "",
            LeitstellenNummer = leitstellenNummer,
            Stichwort = Kuerzen(alarm.Stichwort, 50),
            StichwortBeschreibung = alarm.StichwortBeschreibung,
            StichwortKategorie = alarm.StichwortKategorie,
            Meldung = Meldung(alarm),
            Strasse = alarm.Strasse,
            Hausnummer = alarm.Hausnummer,
            Plz = alarm.Plz,
            Ort = alarm.Ort,
            Ortsteil = alarm.Ortsteil,
            Objekt = alarm.Objekt,
            Latitude = alarm.Latitude,
            Longitude = alarm.Longitude,
            Alarmiert = alarmiert,
            Status = EinsatzStatus.Entwurf,
        };

        await FahrzeugeZuordnenAsync(einsatz, alarm.Fahrzeuge, ct);

        db.Einsaetze.Add(einsatz);
        import.EinsatzId = einsatz.Id;
        import.Verarbeitet = true;
        await MitNummerSpeichernAsync(einsatz, ct);

        log.LogInformation("Einsatzbericht-Entwurf {Einsatznummer} aus FE2-Alarm {ExterneId} angelegt.", einsatz.Einsatznummer, import.ExterneId);
        return einsatz;
    }

    /// <summary>Holt Alarme nach, deren Entwurf beim Empfang nicht angelegt werden konnte.</summary>
    public async Task<int> OffeneAlarmeVerarbeitenAsync(CancellationToken ct = default)
    {
        var offen = await db.AlarmImporte
            .Where(a => !a.Verarbeitet)
            .OrderBy(a => a.EmpfangenAm)
            .ToListAsync(ct);

        foreach (var import in offen)
        {
            await EntwurfAusAlarmAsync(import, ct);
        }

        return offen.Count;
    }

    public static int Jahr(DateTimeOffset zeitpunkt) => TimeZoneInfo.ConvertTime(zeitpunkt, Berlin).Year;

    private async Task MitNummerSpeichernAsync(Einsatz einsatz, CancellationToken ct)
    {
        var jahr = Jahr(einsatz.Alarmiert);
        var format = await parameter.WertAsync(ParameterSchluessel.EinsatznummerFormat, new DateOnly(jahr, 12, 31), ct)
            ?? Einsatznummer.StandardFormat;

        for (var versuch = 1; ; versuch++)
        {
            einsatz.Einsatznummer = await NaechsteNummerAsync(format, jahr, ct);
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (versuch < Versuche
                && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
                && pg.ConstraintName?.Contains("einsatznummer", StringComparison.Ordinal) == true)
            {
                // Zwei Berichte gleichzeitig angelegt: nächste Nummer probieren. Die Protokolleinträge
                // des gescheiterten Versuchs verwerfen, sonst landen sie doppelt in der Tabelle.
                foreach (var eintrag in db.ChangeTracker.Entries<AuditEintrag>().Where(e => e.State == EntityState.Added).ToList())
                {
                    eintrag.State = EntityState.Detached;
                }
            }
        }
    }

    /// <summary>Höchste laufende Nummer des Jahres + 1. Gelöschte Berichte zählen mit, ihre Nummer wird nicht neu vergeben.</summary>
    private async Task<string> NaechsteNummerAsync(string format, int jahr, CancellationToken ct)
    {
        var (praefix, suffix) = Einsatznummer.Rahmen(format, jahr);
        var kandidaten = await db.Einsaetze.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.Einsatznummer.StartsWith(praefix) && e.Einsatznummer.EndsWith(suffix))
            .Select(e => e.Einsatznummer)
            .ToListAsync(ct);

        var hoechste = kandidaten.Select(n => Einsatznummer.LaufendeNummer(format, jahr, n) ?? 0).DefaultIfEmpty(0).Max();
        return Einsatznummer.Bilden(format, jahr, hoechste + 1);
    }

    /// <summary>Ordnet FE2-Fahrzeuge über den Funkrufnamen zu; Groß-/Kleinschreibung und Leerzeichen zählen nicht.</summary>
    private async Task FahrzeugeZuordnenAsync(Einsatz einsatz, IReadOnlyList<string> namen, CancellationToken ct)
    {
        if (namen.Count == 0)
        {
            return;
        }

        var fahrzeuge = await db.Fahrzeuge.AsNoTracking().Select(f => new { f.Id, f.Funkrufname }).ToListAsync(ct);
        foreach (var name in namen)
        {
            var treffer = fahrzeuge.FirstOrDefault(f => Normalisieren(f.Funkrufname) == Normalisieren(name));
            if (treffer is null)
            {
                log.LogInformation("FE2-Fahrzeug {Name} ist keinem Fahrzeug zugeordnet (Funkrufname prüfen).", name);
                continue;
            }

            if (einsatz.Fahrzeuge.All(f => f.FahrzeugId != treffer.Id))
            {
                einsatz.Fahrzeuge.Add(new EinsatzFahrzeug { FahrzeugId = treffer.Id });
            }
        }
    }

    private static string Normalisieren(string name) =>
        string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant();

    private static string? Meldung(Fe2Alarm alarm)
    {
        IEnumerable<string> zeilen = alarm.StichwortZusatz is null ? alarm.Meldung : [alarm.StichwortZusatz, .. alarm.Meldung];
        var text = string.Join('\n', zeilen);
        return text.Length == 0 ? null : text;
    }

    private static string? Kuerzen(string? text, int laenge) =>
        text is null || text.Length <= laenge ? text : text[..laenge];
}
