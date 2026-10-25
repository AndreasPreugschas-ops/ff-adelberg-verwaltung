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
/// <param name="Staffel">Gestaffelte Pauschale nach Stunden (Lehrgang nach FwES Adelberg § 2 Abs. 1); geht dem Stundensatz vor.</param>
public sealed record Entschaedigungsregel(
    decimal SatzStunde,
    int RundungMinuten,
    decimal? SatzTag = null,
    decimal? SatzHalberTag = null,
    decimal? HalberTagUnterStunden = null,
    Entschaedigungsstaffel? Staffel = null)
{
    /// <summary>Es wird nach Kalendertagen statt nach Stunden abgerechnet.</summary>
    public bool NachTagen => SatzTag is > 0;

    /// <summary>Es wird nach der Staffel abgerechnet.</summary>
    public bool NachStaffel => !NachTagen && Staffel is { ErsterBetrag: > 0 };

    /// <summary>Für diesen Vorgang gibt es überhaupt eine Entschädigung.</summary>
    public bool Verguetet => SatzStunde > 0 || NachTagen || NachStaffel;

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
        if (NachStaffel)
        {
            return new(stunden, Entschaedigungseinheit.Staffel, Staffel!.ErsterBetrag, Staffel.Betrag(stunden));
        }

        return new(stunden, Entschaedigungseinheit.Stunde, SatzStunde, Math.Round(stunden * SatzStunde, 2, MidpointRounding.AwayFromZero));
    }
}

/// <summary>
/// Gestaffelte Pauschale: ein Betrag für die ersten Stunden und ein Betrag je angefangenen weiteren Block,
/// z. B. 10 € für die ersten drei Stunden und 5 € je weitere drei Stunden (FwES Adelberg 2018 § 2 Abs. 1).
/// </summary>
/// <param name="ErsteStunden">Stunden, die der erste Betrag abdeckt.</param>
/// <param name="ErsterBetrag">Betrag für die ersten Stunden in Euro.</param>
/// <param name="WeitereStunden">Länge eines weiteren Blocks in Stunden; 0 = keine weiteren Blöcke.</param>
/// <param name="WeitererBetrag">Betrag je angefangenen weiteren Block in Euro.</param>
public sealed record Entschaedigungsstaffel(decimal ErsteStunden, decimal ErsterBetrag, decimal WeitereStunden, decimal WeitererBetrag)
{
    /// <summary>Betrag für die abgerechneten Stunden.</summary>
    /// <param name="stunden">Stunden nach Rundung.</param>
    /// <returns>Der Betrag in Euro; 0 ohne Stunden.</returns>
    public decimal Betrag(decimal stunden)
    {
        if (stunden <= 0)
        {
            return 0;
        }

        var rest = stunden - ErsteStunden;
        var bloecke = rest > 0 && WeitereStunden > 0 ? Math.Ceiling(rest / WeitereStunden) : 0;
        return ErsterBetrag + bloecke * WeitererBetrag;
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
    /// <summary>Stunden nach einer gestaffelten Pauschale; der Satz ist der Betrag der ersten Stufe.</summary>
    [Display(Name = "Std. (Staffel)")] Staffel,
    /// <summary>Zwölftel einer Jahrespauschale je angefangenen Monat.</summary>
    [Display(Name = "Monate (Zwölftel)")] Monat,
    /// <summary>Pauschale je Vorgang, z. B. je geleitete Übung.</summary>
    [Display(Name = "pauschal")] Pauschal,
}

/// <summary>Ergebnis der Berechnung einer Entschädigung.</summary>
/// <param name="Einheit">Einheit der Menge.</param>
/// <param name="Betrag">Betrag in Euro, auf Cent gerundet.</param>
/// <param name="Menge">Abgerechnete Stunden, Tage oder Tage des Jahresanteils.</param>
/// <param name="Satz">Satz je Einheit (bei Pauschalen der Jahresbetrag).</param>
public sealed record Entschaedigungsbetrag(decimal Menge, Entschaedigungseinheit Einheit, decimal Satz, decimal Betrag);
