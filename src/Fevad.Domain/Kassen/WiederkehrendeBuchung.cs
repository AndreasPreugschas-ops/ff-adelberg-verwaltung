using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Kassen;

/// <summary>Abstand zwischen zwei Fälligkeiten einer wiederkehrenden Buchung.</summary>
public enum Buchungsrhythmus
{
    /// <summary>Jeden Monat.</summary>
    [Display(Name = "monatlich")] Monatlich,

    /// <summary>Alle drei Monate.</summary>
    [Display(Name = "vierteljährlich")] Vierteljaehrlich,

    /// <summary>Alle sechs Monate.</summary>
    [Display(Name = "halbjährlich")] Halbjaehrlich,

    /// <summary>Einmal im Jahr.</summary>
    [Display(Name = "jährlich")] Jaehrlich,
}

/// <summary>
/// Wiederkehrende Buchung (V07), z. B. die Zuwendung der Gemeinde nach § 5 Entschädigungssatzung als feste Jahreseinnahme. FEVAD
/// zeigt sie ab der Fälligkeit auf der Kassenseite und füllt die Buchung vor; gebucht wird erst, wenn der Kassenwart speichert.
/// Der Betrag kommt aus einem Parameter, wenn einer genannt ist, sonst aus dem festen Betrag.
/// </summary>
public class WiederkehrendeBuchung : Entitaet
{
    /// <summary>Schlüssel der Kasse.</summary>
    public Guid KasseId { get; set; }

    /// <summary>Die Kasse.</summary>
    public Kasse? Kasse { get; set; }

    /// <summary>Kurze Bezeichnung, z. B. „Zuwendung der Gemeinde“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Einnahme oder Ausgabe; Umbuchungen wiederholen sich nicht.</summary>
    public BuchungArt Art { get; set; }

    /// <summary>Fester Betrag in Euro; gilt, wenn kein Parameter genannt ist oder der Parameter fehlt.</summary>
    public decimal? Betrag { get; set; }

    /// <summary>Schlüssel eines Parameters mit dem Betrag in Euro, z. B. der Zuschuss nach der Entschädigungssatzung.</summary>
    public string? BetragParameter { get; set; }

    /// <summary>Vorgeschlagenes Konto.</summary>
    public Guid? KontoId { get; set; }

    /// <summary>Das Konto.</summary>
    public Zahlungsmittelkonto? Konto { get; set; }

    /// <summary>Vorgeschlagene Position des Wirtschaftsplans.</summary>
    public Guid? PlanpositionId { get; set; }

    /// <summary>Die Position.</summary>
    public Planposition? Planposition { get; set; }

    /// <summary>Vorgeschlagener Zahlungspartner.</summary>
    public string? Partner { get; set; }

    /// <summary>Vorgeschlagener Verwendungszweck; „{Jahr}“ wird durch das Jahr der Fälligkeit ersetzt.</summary>
    public required string Text { get; set; }

    /// <summary>Abstand der Fälligkeiten.</summary>
    public Buchungsrhythmus Rhythmus { get; set; } = Buchungsrhythmus.Jaehrlich;

    /// <summary>Tag im Monat, an dem die Buchung fällig wird (1 bis 31; in kürzeren Monaten der letzte Tag).</summary>
    public int Tag { get; set; } = 1;

    /// <summary>Nächste Fälligkeit.</summary>
    public DateOnly NaechsteFaelligkeit { get; set; }

    /// <summary>Wird noch vorgeschlagen.</summary>
    public bool Aktiv { get; set; } = true;
}

/// <summary>Regeln für wiederkehrende Buchungen.</summary>
public static class Wiederkehrend
{
    /// <summary>Monate zwischen zwei Fälligkeiten.</summary>
    /// <param name="rhythmus">Der Rhythmus.</param>
    /// <returns>1, 3, 6 oder 12.</returns>
    public static int Monate(Buchungsrhythmus rhythmus) => rhythmus switch
    {
        Buchungsrhythmus.Monatlich => 1,
        Buchungsrhythmus.Vierteljaehrlich => 3,
        Buchungsrhythmus.Halbjaehrlich => 6,
        _ => 12,
    };

    /// <summary>Tag im Monat, in kurzen Monaten auf den letzten Tag begrenzt.</summary>
    /// <param name="jahr">Jahr.</param>
    /// <param name="monat">Monat.</param>
    /// <param name="tag">Gewünschter Tag (1 bis 31).</param>
    /// <returns>Das Datum.</returns>
    public static DateOnly Datum(int jahr, int monat, int tag) => new(jahr, monat, Math.Clamp(tag, 1, DateTime.DaysInMonth(jahr, monat)));

    /// <summary>Fälligkeit nach der gegebenen; der Tag im Monat bleibt erhalten, auch nach einem kurzen Monat.</summary>
    /// <param name="faellig">Bisherige Fälligkeit.</param>
    /// <param name="rhythmus">Der Rhythmus.</param>
    /// <param name="tag">Tag im Monat.</param>
    /// <returns>Die nächste Fälligkeit.</returns>
    public static DateOnly Naechste(DateOnly faellig, Buchungsrhythmus rhythmus, int tag)
    {
        var monat = new DateOnly(faellig.Year, faellig.Month, 1).AddMonths(Monate(rhythmus));
        return Datum(monat.Year, monat.Month, tag);
    }

    /// <summary>Die Buchung ist heute oder früher fällig und aktiv.</summary>
    /// <param name="w">Die wiederkehrende Buchung.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <returns>true, wenn sie zu buchen ist.</returns>
    public static bool Faellig(WiederkehrendeBuchung w, DateOnly heute) => w.Aktiv && w.NaechsteFaelligkeit <= heute;

    /// <summary>Verwendungszweck mit dem Jahr der Fälligkeit.</summary>
    /// <param name="w">Die wiederkehrende Buchung.</param>
    /// <returns>Der Text.</returns>
    public static string Text(WiederkehrendeBuchung w) => w.Text.Replace("{Jahr}", w.NaechsteFaelligkeit.Year.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
