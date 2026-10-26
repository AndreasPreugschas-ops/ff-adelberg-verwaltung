using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Verwaltung;

/// <summary>
/// Betriebsseite (V71): Version, Laufzeit, Antwortzeiten und Fehlerrate seit dem Start, letzte Sicherung, letzter Restore-Test und letzter
/// Löschlauf. Zeigt Warnungen, wenn ein Lauf ausbleibt oder fehlschlägt. Keine personenbezogenen Daten.
/// </summary>
[Authorize(Policy = Berechtigungen.Betrieb)]
public class BetriebModel(AppDbContext db, Betriebsmessung messung, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Antwortzeiten und Fehler seit dem Start.</summary>
    public Betriebsmessung.Stand Messung { get; private set; } = null!;

    /// <summary>Start der Anwendung in UTC.</summary>
    public DateTimeOffset Start { get; private set; }

    /// <summary>Jetzt in UTC.</summary>
    public DateTimeOffset Jetzt { get; private set; }

    /// <summary>Letzte erfolgreiche Sicherung.</summary>
    public Betriebsmeldung? Sicherung { get; private set; }

    /// <summary>Letzter erfolgreicher Restore-Test.</summary>
    public Betriebsmeldung? Restoretest { get; private set; }

    /// <summary>Letzter nächtlicher oder manueller Löschlauf.</summary>
    public DateTimeOffset? Loeschlauf { get; private set; }

    /// <summary>Die letzten Meldungen der Betriebsskripte, neueste zuerst.</summary>
    public IReadOnlyList<Betriebsmeldung> Meldungen { get; private set; } = [];

    /// <summary>Größe der Datenbank in Byte.</summary>
    public long DatenbankGroesse { get; private set; }

    /// <summary>Warnungen für die Kopfzeile der Seite.</summary>
    public IReadOnlyList<string> Warnungen { get; private set; } = [];

    /// <summary>Lädt die Kennzahlen und bewertet sie gegen die Parameter.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Messung = messung.Auswerten();
        Start = messung.Start;
        Jetzt = zeit.GetUtcNow();
        var heute = Anzeige.Heute(zeit);

        Meldungen = await db.Betriebsmeldungen.AsNoTracking().OrderByDescending(m => m.Zeitpunkt).Take(20).ToListAsync(ct);
        Sicherung = await LetzteErfolgreicheAsync(BetriebsmeldungArt.Sicherung, ct);
        Restoretest = await LetzteErfolgreicheAsync(BetriebsmeldungArt.Restoretest, ct);
        Loeschlauf = await db.Loeschnachweise.Where(l => l.Regel == Fevad.Infrastructure.Datenschutz.Loeschlauf.RegelLauf)
            .MaxAsync(l => (DateTimeOffset?)l.Zeitpunkt, ct);
        DatenbankGroesse = await db.Database.SqlQueryRaw<long>("select pg_database_size(current_database()) as \"Value\"").SingleAsync(ct);

        var warnungen = new List<string>();
        var sicherungStunden = await parameter.ZahlAsync(ParameterSchluessel.BetriebSicherungWarnungStunden, heute, 36, ct);
        if (Sicherung is null || Sicherung.Zeitpunkt < Jetzt.AddHours(-sicherungStunden))
        {
            warnungen.Add($"Seit mehr als {sicherungStunden} Stunden keine erfolgreiche Sicherung gemeldet.");
        }

        var restoreTage = await parameter.ZahlAsync(ParameterSchluessel.BetriebRestoretestWarnungTage, heute, 8, ct);
        if (Restoretest is null || Restoretest.Zeitpunkt < Jetzt.AddDays(-restoreTage))
        {
            warnungen.Add($"Seit mehr als {restoreTage} Tagen kein erfolgreicher Restore-Test gemeldet.");
        }

        if (Meldungen.FirstOrDefault(m => m.Art == BetriebsmeldungArt.Sicherung) is { Erfolgreich: false } fehlSicherung)
        {
            warnungen.Add($"Die letzte Sicherung ist fehlgeschlagen ({Anzeige.DatumZeit(fehlSicherung.Zeitpunkt)} Uhr).");
        }

        if (Meldungen.FirstOrDefault(m => m.Art == BetriebsmeldungArt.Restoretest) is { Erfolgreich: false } fehlTest)
        {
            warnungen.Add($"Der letzte Restore-Test ist fehlgeschlagen ({Anzeige.DatumZeit(fehlTest.Zeitpunkt)} Uhr).");
        }

        // Der Löschlauf ist wie die Sicherung nächtlich; dieselbe Schwelle.
        if (Loeschlauf is null || Loeschlauf < Jetzt.AddHours(-sicherungStunden))
        {
            warnungen.Add($"Der nächtliche Löschlauf ist seit mehr als {sicherungStunden} Stunden nicht gelaufen.");
        }

        var promille = await parameter.ZahlAsync(ParameterSchluessel.BetriebFehlerrateWarnungPromille, heute, 10, ct);
        if (Messung.Fehlerrate is { } rate && rate * 10 >= promille && Messung.Fehler > 0)
        {
            warnungen.Add($"Fehlerrate {Anzeige.Zahl((decimal)rate, 1)} % seit dem Start (Warnschwelle {Anzeige.Zahl(promille / 10m, 1)} %).");
        }

        Warnungen = warnungen;
    }

    private Task<Betriebsmeldung?> LetzteErfolgreicheAsync(BetriebsmeldungArt art, CancellationToken ct) =>
        db.Betriebsmeldungen.AsNoTracking().Where(m => m.Art == art && m.Erfolgreich).OrderByDescending(m => m.Zeitpunkt).FirstOrDefaultAsync(ct);

    /// <summary>Zeitspanne als „3 Tage 4 Std.“ bzw. „5 Std. 12 Min.“.</summary>
    /// <param name="dauer">Die Dauer.</param>
    /// <returns>Der Text.</returns>
    public static string Dauer(TimeSpan dauer) => dauer.TotalDays >= 1
        ? $"{(int)dauer.TotalDays} Tage {dauer.Hours} Std."
        : $"{dauer.Hours} Std. {dauer.Minutes} Min.";

    /// <summary>Byte als MB oder GB.</summary>
    /// <param name="bytes">Größe.</param>
    /// <returns>Der Text.</returns>
    public static string Groesse(long bytes) => bytes >= 1L << 30
        ? $"{Anzeige.Zahl(bytes / (decimal)(1L << 30), 1)} GB"
        : $"{Anzeige.Zahl(bytes / (decimal)(1L << 20), 1)} MB";
}
