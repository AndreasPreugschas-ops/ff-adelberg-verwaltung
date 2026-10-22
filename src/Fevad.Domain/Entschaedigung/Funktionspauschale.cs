namespace Fevad.Domain.Entschaedigung;

/// <summary>
/// Zeitanteilige Jahrespauschale für eine Funktion (Kommandant, Gerätewart, ...). Jeder Tag zählt
/// mit Jahressatz / Tage des Jahres. Bereits abgerechnete Tage werden übersprungen, so dass
/// Nachtragsabrechnungen eine Funktion nie doppelt bezahlen.
/// </summary>
public static class Funktionspauschale
{
    /// <summary>Ein zusammenhängender Zeitraum mit gleichem Jahressatz.</summary>
    public sealed record Anteil(DateOnly Von, DateOnly Bis, decimal Jahressatz, int Tage, decimal Betrag);

    /// <param name="von">Beginn der Abrechnung.</param>
    /// <param name="bis">Ende der Abrechnung (einschließlich).</param>
    /// <param name="funktionAb">Beginn der Funktion.</param>
    /// <param name="funktionBis">Ende der Funktion (einschließlich) oder leer.</param>
    /// <param name="bereitsAbgerechnet">Tage, die schon in einer abgeschlossenen Abrechnung stehen.</param>
    /// <param name="jahressatz">Jahressatz am Tag oder null, wenn keiner gilt.</param>
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
}
