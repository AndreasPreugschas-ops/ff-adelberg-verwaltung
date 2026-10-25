namespace Fevad.Domain.Entschaedigung;

/// <summary>
/// Zeitanteilige Jahrespauschale für eine Funktion (Kommandant, Gerätewart, ...). Jeder Tag zählt
/// mit Jahressatz / Tage des Jahres. Bereits abgerechnete Tage werden übersprungen, so dass
/// Nachtragsabrechnungen eine Funktion nie doppelt bezahlen.
/// </summary>
public static class Funktionspauschale
{
    /// <summary>Ein zusammenhängender Zeitraum mit gleichem Jahressatz.</summary>
    /// <param name="Von">Erster Tag.</param>
    /// <param name="Bis">Letzter Tag.</param>
    /// <param name="Jahressatz">Jahressatz in Euro.</param>
    /// <param name="Tage">Abgerechnete Tage.</param>
    /// <param name="Betrag">Betrag in Euro, auf Cent gerundet.</param>
    public sealed record Anteil(DateOnly Von, DateOnly Bis, decimal Jahressatz, int Tage, decimal Betrag);

    /// <summary>Berechnet die Anteile einer Funktionspauschale im Abrechnungszeitraum.</summary>
    /// <param name="von">Beginn der Abrechnung.</param>
    /// <param name="bis">Ende der Abrechnung (einschließlich).</param>
    /// <param name="funktionAb">Beginn der Funktion.</param>
    /// <param name="funktionBis">Ende der Funktion (einschließlich) oder leer.</param>
    /// <param name="bereitsAbgerechnet">Tage, die schon in einer abgeschlossenen Abrechnung stehen.</param>
    /// <param name="jahressatz">Jahressatz am Tag oder null, wenn keiner gilt.</param>
    /// <returns>Die Anteile, je Satz- und Jahreswechsel oder Lücke ein eigener.</returns>
    public static IReadOnlyList<Anteil> Berechnen(
        DateOnly von,
        DateOnly bis,
        DateOnly funktionAb,
        DateOnly? funktionBis,
        Func<DateOnly, bool> bereitsAbgerechnet,
        Func<DateOnly, decimal?> jahressatz)
    {
        var start = funktionAb > von ? funktionAb : von;
        var ende = funktionBis is { } fb && fb < bis ? fb : bis;

        var anteile = new List<Anteil>();
        DateOnly? segmentVon = null;
        var segmentBis = start;
        decimal segmentSatz = 0;
        decimal segmentSumme = 0;
        var segmentTage = 0;

        void Abschliessen()
        {
            if (segmentVon is { } sv)
            {
                anteile.Add(new Anteil(sv, segmentBis, segmentSatz, segmentTage, Math.Round(segmentSumme, 2, MidpointRounding.AwayFromZero)));
            }

            segmentVon = null;
            segmentSumme = 0;
            segmentTage = 0;
        }

        for (var tag = start; tag <= ende; tag = tag.AddDays(1))
        {
            var satz = bereitsAbgerechnet(tag) ? null : jahressatz(tag);
            if (satz is not > 0)
            {
                Abschliessen();
                continue;
            }

            // Neues Segment bei Satzwechsel, Jahreswechsel (andere Tageszahl) oder Lücke.
            if (segmentVon is not null && (satz != segmentSatz || tag.Year != segmentBis.Year))
            {
                Abschliessen();
            }

            segmentVon ??= tag;
            segmentBis = tag;
            segmentSatz = satz.Value;
            segmentSumme += satz.Value / (DateTime.IsLeapYear(tag.Year) ? 366 : 365);
            segmentTage++;
        }

        Abschliessen();
        return anteile;
    }

    /// <summary>
    /// Berechnet die Anteile einer Funktionspauschale in Zwölfteln: jeder Kalendermonat, in dem die Funktion an mindestens einem Tag
    /// im Abrechnungszeitraum bestand, zählt mit Jahressatz / 12. Ein Monat, von dem schon ein Tag abgerechnet ist, entfällt ganz.
    /// Die Anteile umfassen ganze Monate, damit spätere Abrechnungen denselben Monat erkennen.
    /// </summary>
    /// <param name="von">Beginn der Abrechnung.</param>
    /// <param name="bis">Ende der Abrechnung (einschließlich).</param>
    /// <param name="funktionAb">Beginn der Funktion.</param>
    /// <param name="funktionBis">Ende der Funktion (einschließlich) oder leer.</param>
    /// <param name="bereitsAbgerechnet">Tage, die schon in einer abgeschlossenen Abrechnung stehen.</param>
    /// <param name="jahressatz">Jahressatz am Tag oder null, wenn keiner gilt.</param>
    /// <returns>Die Anteile; <see cref="Anteil.Tage"/> enthält hier die Zahl der Monate.</returns>
    public static IReadOnlyList<Anteil> BerechnenZwoelftel(
        DateOnly von,
        DateOnly bis,
        DateOnly funktionAb,
        DateOnly? funktionBis,
        Func<DateOnly, bool> bereitsAbgerechnet,
        Func<DateOnly, decimal?> jahressatz)
    {
        var start = funktionAb > von ? funktionAb : von;
        var ende = funktionBis is { } fb && fb < bis ? fb : bis;
        var anteile = new List<Anteil>();
        if (ende < start)
        {
            return anteile;
        }

        DateOnly? segmentVon = null;
        var segmentBis = start;
        decimal segmentSatz = 0;
        var monate = 0;

        void Abschliessen()
        {
            if (segmentVon is { } sv)
            {
                anteile.Add(new Anteil(sv, segmentBis, segmentSatz, monate, Math.Round(segmentSatz * monate / 12m, 2, MidpointRounding.AwayFromZero)));
            }

            segmentVon = null;
            monate = 0;
        }

        for (var monat = new DateOnly(start.Year, start.Month, 1); monat <= ende; monat = monat.AddMonths(1))
        {
            var monatsende = monat.AddMonths(1).AddDays(-1);
            var ersterTag = monat > start ? monat : start;
            decimal? satz = null;
            var schonAbgerechnet = false;
            for (var tag = monat; tag <= monatsende; tag = tag.AddDays(1))
            {
                schonAbgerechnet |= bereitsAbgerechnet(tag);
            }

            if (!schonAbgerechnet)
            {
                satz = jahressatz(ersterTag);
            }

            if (satz is not > 0)
            {
                Abschliessen();
                continue;
            }

            if (segmentVon is not null && (satz != segmentSatz || monat.Year != segmentBis.Year))
            {
                Abschliessen();
            }

            segmentVon ??= monat;
            segmentBis = monatsende;
            segmentSatz = satz.Value;
            monate++;
        }

        Abschliessen();
        return anteile;
    }
}
