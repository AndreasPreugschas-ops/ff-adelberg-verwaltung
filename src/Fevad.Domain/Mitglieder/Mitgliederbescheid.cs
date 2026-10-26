using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Art eines Bescheids des Bürgermeisters an ein Mitglied oder einen Bewerber (V27).</summary>
public enum MitgliederbescheidArt
{
    /// <summary>Ablehnung der Aufnahme (Satzung § 3 Abs. 5).</summary>
    [Display(Name = "Ablehnung der Aufnahme")] AblehnungAufnahme,
    /// <summary>Feststellung der Beendigung des Feuerwehrdienstes (Satzung § 4 Abs. 5 Satz 3).</summary>
    [Display(Name = "Feststellung der Beendigung des Dienstes")] FeststellungBeendigung,
    /// <summary>Geldbuße bei groben Verstößen auf Antrag des Kommandanten (Satzung § 5 Abs. 9).</summary>
    [Display(Name = "Geldbuße")] Geldbusse,
    /// <summary>Entlassung aus dem Dienst in der Einsatzabteilung (Satzung § 4 Abs. 2).</summary>
    [Display(Name = "Entlassung aus der Einsatzabteilung")] Entlassung,
}

/// <summary>Stand eines Bescheids an ein Mitglied.</summary>
public enum MitgliederbescheidStand
{
    /// <summary>In Vorbereitung.</summary>
    [Display(Name = "Entwurf")] Entwurf,
    /// <summary>Anhörung versandt, Frist läuft oder ist abgelaufen.</summary>
    [Display(Name = "Anhörung läuft")] Anhoerung,
    /// <summary>Bescheid erlassen.</summary>
    [Display(Name = "erlassen")] Erlassen,
    /// <summary>Bescheid bekanntgegeben.</summary>
    [Display(Name = "bekanntgegeben")] Bekanntgegeben,
    /// <summary>Verfahren eingestellt.</summary>
    [Display(Name = "eingestellt")] Eingestellt,
}

/// <summary>
/// Bescheid des Bürgermeisters an ein Mitglied oder einen Bewerber (V27): Anhörung mit Frist, Erlass mit Begründung und Rechtsbehelfsbelehrung,
/// Bekanntgabe. Unterschrieben wird auf Papier; FEVAD führt Daten und Texte und erzeugt die Schreiben.
/// </summary>
public class Mitgliederbescheid : Entitaet
{
    /// <summary>Art des Bescheids.</summary>
    public MitgliederbescheidArt Art { get; set; }

    /// <summary>Betroffenes Mitglied; null bei einem Bewerber ohne Mitgliedsakte.</summary>
    public Guid? MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Name des Empfängers.</summary>
    public required string EmpfaengerName { get; set; }
    /// <summary>Zustellanschrift.</summary>
    public string? EmpfaengerAnschrift { get; set; }

    /// <summary>Sachverhalt.</summary>
    public string? Sachverhalt { get; set; }
    /// <summary>Entscheidungssatz (Tenor).</summary>
    public string? Tenor { get; set; }
    /// <summary>Begründung.</summary>
    public string? Begruendung { get; set; }

    /// <summary>Betrag der Geldbuße in Euro; nur bei <see cref="MitgliederbescheidArt.Geldbusse"/>.</summary>
    public decimal? Betrag { get; set; }

    /// <summary>Tag, an dem die Anhörung versandt wurde.</summary>
    public DateOnly? AnhoerungAm { get; set; }
    /// <summary>Frist zur Äußerung.</summary>
    public DateOnly? AnhoerungBis { get; set; }
    /// <summary>Stellungnahme des Betroffenen, kurz.</summary>
    public string? Stellungnahme { get; set; }

    /// <summary>Tag des Erlasses.</summary>
    public DateOnly? ErlassenAm { get; set; }
    /// <summary>Tag der Bekanntgabe.</summary>
    public DateOnly? BekanntgegebenAm { get; set; }

    /// <summary>Das Verfahren wurde eingestellt.</summary>
    public bool Eingestellt { get; set; }

    /// <summary>Stand.</summary>
    public MitgliederbescheidStand Stand => Eingestellt ? MitgliederbescheidStand.Eingestellt
        : BekanntgegebenAm is not null ? MitgliederbescheidStand.Bekanntgegeben
        : ErlassenAm is not null ? MitgliederbescheidStand.Erlassen
        : AnhoerungAm is not null ? MitgliederbescheidStand.Anhoerung
        : MitgliederbescheidStand.Entwurf;

    /// <summary>Prüft, ob der Bescheid erlassen werden kann.</summary>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="hoechstbetrag">Höchstbetrag der Geldbuße (Parameter).</param>
    /// <returns>Fehlermeldung oder null.</returns>
    public string? ErlassPruefen(DateOnly heute, decimal hoechstbetrag)
    {
        if (string.IsNullOrWhiteSpace(Tenor) || string.IsNullOrWhiteSpace(Begruendung))
        {
            return "Entscheidungssatz und Begründung fehlen.";
        }

        // Die Ablehnung der Aufnahme braucht keine Anhörung, weil sie nicht in Rechte eines Mitglieds eingreift; alle anderen schon (§ 28 LVwVfG).
        if (Art != MitgliederbescheidArt.AblehnungAufnahme && (AnhoerungAm is null || AnhoerungBis is null))
        {
            return "Vor dem Erlass ist der Betroffene anzuhören.";
        }

        if (AnhoerungBis is { } bis && bis >= heute && string.IsNullOrWhiteSpace(Stellungnahme))
        {
            return $"Die Anhörungsfrist läuft noch bis {bis:dd.MM.yyyy}.";
        }

        if (Art == MitgliederbescheidArt.Geldbusse && (Betrag is not > 0 || Betrag > hoechstbetrag))
        {
            return $"Die Geldbuße muss zwischen 0,01 € und {hoechstbetrag.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))} € liegen.";
        }

        return null;
    }

    /// <summary>Mustertexte je Art für Entscheidungssatz und Begründung.</summary>
    /// <param name="art">Art des Bescheids.</param>
    /// <returns>Tenor und Begründung mit Platzhaltern in eckigen Klammern.</returns>
    public static (string Tenor, string Begruendung) Muster(MitgliederbescheidArt art) => art switch
    {
        MitgliederbescheidArt.AblehnungAufnahme => ("Ihr Gesuch um Aufnahme in die Freiwillige Feuerwehr vom [Datum] wird abgelehnt.",
            "Über die Aufnahme entscheidet der Feuerwehrausschuss (Feuerwehrsatzung § 3 Abs. 4). Er hat in seiner Sitzung vom [Datum] die Aufnahme abgelehnt, weil [Gründe]. Ein Rechtsanspruch auf Aufnahme besteht nicht (§ 3 Abs. 5)."),
        MitgliederbescheidArt.FeststellungBeendigung => ("Es wird festgestellt, dass Ihr ehrenamtlicher Feuerwehrdienst mit Ablauf des [Datum] beendet ist.",
            "Der Gemeinderat hat in seiner Sitzung vom [Datum] nach Anhörung des Feuerwehrausschusses und Ihrer Anhörung den Feuerwehrdienst aus wichtigem Grund beendet (Feuerwehrsatzung § 4 Abs. 5), weil [Gründe]. Bitte geben Sie Dienstausweis und Ausrüstung bis [Datum] zurück."),
        MitgliederbescheidArt.Geldbusse => ("Gegen Sie wird wegen eines groben Verstoßes gegen die Dienstpflichten eine Geldbuße von [Betrag] festgesetzt.",
            "Auf Antrag des Feuerwehrkommandanten vom [Datum] wird der Verstoß nach Feuerwehrsatzung § 5 Abs. 9 geahndet. Sie haben [Sachverhalt]. Ihre Stellungnahme vom [Datum] wurde berücksichtigt."),
        MitgliederbescheidArt.Entlassung => ("Sie werden mit Ablauf des [Datum] aus dem Feuerwehrdienst in der Einsatzabteilung entlassen.",
            "Die Entlassung erfolgt nach Feuerwehrsatzung § 4 Abs. 2 [auf Ihren Antrag vom (Datum) / nach Anhörung des Feuerwehrausschusses], weil [Gründe]."),
        _ => ("", ""),
    };
}

/// <summary>Mustertexte für Anhörung und Rechtsbehelfsbelehrung der Bescheide an Mitglieder; Platzhalter wie bei den Kostenersatzbescheiden.</summary>
public static class Mitgliederbescheidtexte
{
    /// <summary>Anschreiben zur Anhörung; Platzhalter <c>{Gemeinde}</c>, <c>{Vorhaben}</c>, <c>{AnhoerungBis}</c>.</summary>
    public const string Anhoerung =
        "Bevor über die folgende Maßnahme entschieden wird, erhalten Sie nach § 28 des Landesverwaltungsverfahrensgesetzes Gelegenheit, sich zu äußern:\n\n"
        + "{Vorhaben}\n\n"
        + "Bitte teilen Sie Ihre Stellungnahme bis {AnhoerungBis} schriftlich oder mündlich beim Bürgermeisteramt {Gemeinde} mit. "
        + "Äußern Sie sich nicht, wird nach Aktenlage entschieden.";

    /// <summary>Rechtsbehelfsbelehrung; Platzhalter <c>{Gemeinde}</c>.</summary>
    public const string Rechtsbehelf =
        "Gegen diesen Bescheid kann innerhalb eines Monats nach Bekanntgabe Widerspruch erhoben werden. Der Widerspruch ist schriftlich, "
        + "in elektronischer Form oder zur Niederschrift beim Bürgermeisteramt {Gemeinde} einzulegen.";
}
