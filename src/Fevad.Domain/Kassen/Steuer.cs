using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Kassen;

/// <summary>
/// Steuerlicher Bereich einer Buchung (V03). Steuerpflichtig ist die Gemeinde; die Kämmerei braucht für ihre Erklärungen die Umsätze
/// der Kassen getrennt nach Bereich. Körperschaftsteuer wird nicht überwacht (Andi, 25.10.2026), nur die Umsatzgrenze und § 2b UStG.
/// </summary>
public enum Steuerbereich
{
    /// <summary>Interner Bereich ohne Leistungsaustausch, z. B. Zuwendung der Gemeinde, Kameradschaftspflege, Ehrungen.</summary>
    [Display(Name = "intern (nicht steuerbar)")] Intern,

    /// <summary>Vermögensverwaltung, z. B. Zinsen.</summary>
    [Display(Name = "Vermögensverwaltung (z. B. Zinsen)")] Vermoegensverwaltung,

    /// <summary>Wirtschaftliche Tätigkeit, z. B. Feste, Verkauf, Sponsoring; zählt zur Umsatzgrenze.</summary>
    [Display(Name = "wirtschaftlich (Feste, Verkauf, Sponsoring)")] Wirtschaftlich,
}

/// <summary>Umsatzsteuersatz einer Buchung; der Prozentsatz kommt aus den Parametern am Buchungstag.</summary>
public enum Umsatzsteuersatz
{
    /// <summary>Ohne Umsatzsteuer, z. B. im internen Bereich oder vor Anwendung von § 2b UStG.</summary>
    [Display(Name = "ohne")] Ohne,

    /// <summary>Ermäßigter Satz (§ 12 Abs. 2 UStG), z. B. Speisen.</summary>
    [Display(Name = "ermäßigt")] Ermaessigt,

    /// <summary>Regelsatz (§ 12 Abs. 1 UStG), z. B. Getränke.</summary>
    [Display(Name = "Regelsatz")] Regel,
}

/// <summary>Regeln für Steuerbereiche, Nettoumsatz und Steuerampel (V03).</summary>
public static class Steuerregeln
{
    /// <summary>
    /// Nettobetrag einer Buchung. Vor dem Tag, ab dem § 2b UStG gilt, weist die Kasse keine Umsatzsteuer aus; dann ist der Betrag selbst
    /// der Umsatz.
    /// </summary>
    /// <param name="betrag">Bruttobetrag in Euro.</param>
    /// <param name="satzProzent">Steuersatz in Prozent, 0 ohne Steuer.</param>
    /// <returns>Der auf Cent gerundete Nettobetrag.</returns>
    public static decimal Netto(decimal betrag, decimal satzProzent) =>
        satzProzent <= 0 ? betrag : Math.Round(betrag * 100m / (100m + satzProzent), 2, MidpointRounding.AwayFromZero);

    /// <summary>Ob die Umsatzsteuer nach § 2b UStG am Tag gilt.</summary>
    /// <param name="tag">Tag der Buchung.</param>
    /// <param name="ab">Erster Tag nach § 2b laut Stammdaten; leer = noch nicht festgelegt.</param>
    /// <returns><c>true</c> ab dem festgelegten Tag.</returns>
    public static bool Paragraph2bGilt(DateOnly tag, DateOnly? ab) => ab is { } beginn && tag >= beginn;

    /// <summary>
    /// Ampel des Nettoumsatzes der wirtschaftlichen Tätigkeit gegen die Umsatzgrenze: gelb ab dem Warnanteil, rot über der Grenze.
    /// </summary>
    /// <param name="umsatz">Nettoumsatz des Jahres in Euro.</param>
    /// <param name="grenze">Umsatzgrenze in Euro; 0 schaltet die Ampel ab.</param>
    /// <param name="warnungProzent">Ab diesem Anteil der Grenze gelb.</param>
    /// <returns>Die Ampel.</returns>
    public static Fristampel Ampel(decimal umsatz, int grenze, int warnungProzent)
    {
        if (grenze <= 0)
        {
            return Fristampel.Gruen;
        }

        if (umsatz > grenze)
        {
            return Fristampel.Rot;
        }

        return umsatz >= grenze * warnungProzent / 100m ? Fristampel.Gelb : Fristampel.Gruen;
    }

    /// <summary>Steuerbereich, den eine Buchung ohne eigene Angabe von ihrer Position übernimmt; Umbuchungen sind immer intern.</summary>
    /// <param name="art">Art der Buchung.</param>
    /// <param name="position">Position des Wirtschaftsplans.</param>
    /// <returns>Der Steuerbereich.</returns>
    public static Steuerbereich Vorschlag(BuchungArt art, Planposition? position) =>
        art == BuchungArt.Umbuchung ? Steuerbereich.Intern : position?.Steuerbereich ?? Steuerbereich.Intern;
}
