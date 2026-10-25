using System.ComponentModel.DataAnnotations;

namespace Fevad.Domain.Entschaedigung;

/// <summary>
/// Satz und Rundung für eine Vorgangsart (Einsatz, Sonderdienst, Lehrgang, ...) nach der
/// Feuerwehr-Entschädigungssatzung. Die Werte kommen aus versionierten Parametern.
/// </summary>
/// <param name="SatzStunde">Euro je abgerechnete Stunde.</param>
/// <param name="RundungMinuten">Angefangene Einheiten dieser Länge werden aufgerundet (60 = volle Stunde, 30 = halbe Stunde, 1 = minutengenau).</param>
/// <param name="SatzTag">Euro je Kalendertag; ist er gesetzt, wird nach Tagen statt nach Stunden abgerechnet.</param>
/// <param name="SatzHalberTag">Euro für einen Tag unter <paramref name="HalberTagUnterStunden"/> Stunden.</param>
/// <param name="HalberTagUnterStunden">Grenze für den halben Tagessatz.</param>
public sealed record Entschaedigungsregel(
    decimal SatzStunde,
    int RundungMinuten,
    decimal? SatzTag = null,
    decimal? SatzHalberTag = null,
    decimal? HalberTagUnterStunden = null)
{
    /// <summary>Es wird nach Kalendertagen statt nach Stunden abgerechnet.</summary>
    public bool NachTagen => SatzTag is > 0;

    /// <summary>Für diesen Vorgang gibt es überhaupt eine Entschädigung.</summary>
    public bool Verguetet => SatzStunde > 0 || NachTagen;

    /// <summary>Berechnet die Entschädigung für eine Teilnahme.</summary>
    /// <param name="dauer">Dauer der Teilnahme.</param>
    /// <param name="kalendertage">Anzahl der berührten Kalendertage (Ortszeit), mindestens 1.</param>
    /// <returns>Menge, Einheit, Satz und Betrag.</returns>
    public Entschaedigungsbetrag Berechnen(TimeSpan dauer, int kalendertage)
    {
        if (dauer <= TimeSpan.Zero)
        {
            return new(0, Entschaedigungseinheit.Stunde, 0, 0);
        }

        if (NachTagen)
        {
            var tage = Math.Max(1, kalendertage);
            if (tage == 1 && SatzHalberTag is { } halb && HalberTagUnterStunden is { } grenze && (decimal)dauer.TotalHours < grenze)
            {
                return new(1, Entschaedigungseinheit.HalberTag, halb, halb);
            }

            return new(tage, Entschaedigungseinheit.Tag, SatzTag!.Value, tage * SatzTag.Value);
        }

        var einheit = Math.Max(1, RundungMinuten);
        var minuten = (decimal)Math.Ceiling(dauer.TotalMinutes / einheit) * einheit;
        var stunden = Math.Round(minuten / 60m, 2, MidpointRounding.AwayFromZero);
        return new(stunden, Entschaedigungseinheit.Stunde, SatzStunde, Math.Round(stunden * SatzStunde, 2, MidpointRounding.AwayFromZero));
    }
}

/// <summary>Einheit, in der eine Entschädigung abgerechnet wird.</summary>
public enum Entschaedigungseinheit
{
    /// <summary>Stunden, nach Rundung.</summary>
    [Display(Name = "Std.")] Stunde,
    /// <summary>Ganze Kalendertage.</summary>
    [Display(Name = "Tag")] Tag,
    /// <summary>Halber Tagessatz für kurze Tage.</summary>
    [Display(Name = "halber Tag")] HalberTag,
    /// <summary>Tage eines Jahres bei zeitanteiligen Funktionspauschalen.</summary>
    [Display(Name = "Tage anteilig")] Jahresanteil,
}

/// <summary>Ergebnis der Berechnung einer Entschädigung.</summary>
/// <param name="Einheit">Einheit der Menge.</param>
/// <param name="Betrag">Betrag in Euro, auf Cent gerundet.</param>
/// <param name="Menge">Abgerechnete Stunden, Tage oder Tage des Jahresanteils.</param>
/// <param name="Satz">Satz je Einheit (bei Pauschalen der Jahresbetrag).</param>
public sealed record Entschaedigungsbetrag(decimal Menge, Entschaedigungseinheit Einheit, decimal Satz, decimal Betrag);
