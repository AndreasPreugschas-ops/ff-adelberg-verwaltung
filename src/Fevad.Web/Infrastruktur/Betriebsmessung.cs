using System.Diagnostics;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Antwortzeiten und Fehlerrate der Seitenaufrufe seit dem Start (V71), nur im Speicher: die letzten <see cref="Fenster"/> Anfragen ohne
/// Pfad und ohne Benutzer. Statische Dateien und der Gesundheitscheck zählen nicht.
/// </summary>
/// <param name="zeit">Uhr.</param>
public sealed class Betriebsmessung(TimeProvider zeit)
{
    /// <summary>Anzahl der Anfragen, über die gemittelt wird.</summary>
    public const int Fenster = 5000;

    private readonly (DateTimeOffset Zeitpunkt, double Millisekunden, bool Fehler)[] _puffer = new (DateTimeOffset, double, bool)[Fenster];
    private readonly Lock _sperre = new();
    private long _anzahl;

    /// <summary>Zeitpunkt des Starts.</summary>
    public DateTimeOffset Start { get; } = zeit.GetUtcNow();

    /// <summary>Kennzahlen über das Fenster.</summary>
    /// <param name="Anfragen">Anfragen seit dem Start.</param>
    /// <param name="Gemessen">Anfragen im Fenster.</param>
    /// <param name="Median">Median der Antwortzeit in Millisekunden.</param>
    /// <param name="P95">95. Perzentil in Millisekunden.</param>
    /// <param name="Fehler">Antworten mit Status 500 und höher im Fenster.</param>
    /// <param name="FehlerLetzteStunde">Davon in der letzten Stunde.</param>
    public sealed record Stand(long Anfragen, int Gemessen, double? Median, double? P95, int Fehler, int FehlerLetzteStunde)
    {
        /// <summary>Fehlerrate im Fenster in Prozent.</summary>
        public double? Fehlerrate => Gemessen == 0 ? null : 100.0 * Fehler / Gemessen;
    }

    /// <summary>Hält eine Anfrage fest.</summary>
    /// <param name="millisekunden">Dauer.</param>
    /// <param name="status">HTTP-Status.</param>
    public void Erfassen(double millisekunden, int status)
    {
        lock (_sperre)
        {
            _puffer[_anzahl % Fenster] = (zeit.GetUtcNow(), millisekunden, status >= 500);
            _anzahl++;
        }
    }

    /// <summary>Rechnet die Kennzahlen.</summary>
    /// <returns>Der Stand.</returns>
    public Stand Auswerten()
    {
        (DateTimeOffset Zeitpunkt, double Millisekunden, bool Fehler)[] werte;
        long anzahl;
        lock (_sperre)
        {
            anzahl = _anzahl;
            werte = _puffer.Take((int)Math.Min(anzahl, Fenster)).ToArray();
        }

        if (werte.Length == 0)
        {
            return new Stand(anzahl, 0, null, null, 0, 0);
        }

        var zeiten = werte.Select(w => w.Millisekunden).Order().ToArray();
        double Perzentil(double p) => zeiten[Math.Min(zeiten.Length - 1, (int)Math.Ceiling(p * zeiten.Length) - 1)];
        var stunde = zeit.GetUtcNow().AddHours(-1);
        return new Stand(anzahl, werte.Length, Perzentil(0.5), Perzentil(0.95), werte.Count(w => w.Fehler), werte.Count(w => w.Fehler && w.Zeitpunkt >= stunde));
    }

    /// <summary>Middleware: misst Seitenaufrufe, auch solche, die mit einer Ausnahme enden.</summary>
    /// <param name="kontext">Die Anfrage.</param>
    /// <param name="weiter">Der Rest der Pipeline.</param>
    /// <returns>Die Aufgabe.</returns>
    public async Task MessenAsync(HttpContext kontext, Func<Task> weiter)
    {
        var pfad = kontext.Request.Path;
        if (Path.HasExtension(pfad.Value) || pfad.StartsWithSegments("/health"))
        {
            await weiter();
            return;
        }

        var uhr = Stopwatch.StartNew();
        try
        {
            await weiter();
            Erfassen(uhr.Elapsed.TotalMilliseconds, kontext.Response.StatusCode);
        }
        catch
        {
            Erfassen(uhr.Elapsed.TotalMilliseconds, StatusCodes.Status500InternalServerError);
            throw;
        }
    }
}
