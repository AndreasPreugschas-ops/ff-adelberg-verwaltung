using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <summary>
/// Kalkulation des Stundensatzes für eine Einsatzkraft nach § 34 Abs. 5 FwG (V24): Jahreskosten der Einsatzabteilung, gemittelt über vier
/// Jahre, geteilt durch Angehörige mal Stunden je Angehörigem, plus Entschädigungssatz. Ergebnis ist eine Anlage zur Gemeinderatsvorlage;
/// der Satz selbst gilt erst, wenn er in der Kostenersatzsatzung steht.
/// </summary>
public class Satzkalkulation : Entitaet
{
    /// <summary>Tag der Kalkulation; von hier läuft die Wiedervorlage.</summary>
    public DateOnly KalkuliertAm { get; set; }

    /// <summary>Erstes der vier Haushaltsjahre.</summary>
    public int ErstesJahr { get; set; }

    /// <summary>Jahreskosten der Einsatzabteilung im ersten Jahr in Euro.</summary>
    public decimal Kosten1 { get; set; }
    /// <summary>Jahreskosten im zweiten Jahr.</summary>
    public decimal Kosten2 { get; set; }
    /// <summary>Jahreskosten im dritten Jahr.</summary>
    public decimal Kosten3 { get; set; }
    /// <summary>Jahreskosten im vierten Jahr.</summary>
    public decimal Kosten4 { get; set; }

    /// <summary>Zahl der Angehörigen der Einsatzabteilung.</summary>
    public int Angehoerige { get; set; }

    /// <summary>Stunden je Angehörigem und Jahr (§ 34 Abs. 5 FwG: 80).</summary>
    public decimal StundenJeAngehoerigem { get; set; }

    /// <summary>Entschädigungssatz je Stunde in Euro, der auf den Satz aufgeschlagen wird.</summary>
    public decimal Entschaedigungssatz { get; set; }

    /// <summary>Erläuterung, z. B. welche Haushaltsstellen in die Jahreskosten eingeflossen sind.</summary>
    public string? Erlaeuterung { get; set; }

    /// <summary>Wer kalkuliert hat.</summary>
    public string? Bearbeiter { get; set; }

    /// <summary>Mittelwert der vier Jahreskosten.</summary>
    public decimal Mittelwert => (Kosten1 + Kosten2 + Kosten3 + Kosten4) / 4m;

    /// <summary>Kostenanteil je Stunde ohne Entschädigung, auf Cent gerundet; 0, wenn Angehörige oder Stunden fehlen.</summary>
    public decimal KostenJeStunde => Angehoerige <= 0 || StundenJeAngehoerigem <= 0
        ? 0
        : Math.Round(Mittelwert / (Angehoerige * StundenJeAngehoerigem), 2, MidpointRounding.AwayFromZero);

    /// <summary>Stundensatz je Einsatzkraft: Kostenanteil plus Entschädigungssatz.</summary>
    public decimal Stundensatz => KostenJeStunde + Entschaedigungssatz;

    /// <summary>Tag der Wiedervorlage.</summary>
    /// <param name="jahre">Abstand der Wiedervorlage in Jahren (Parameter).</param>
    /// <returns>Kalkulationstag plus die Jahre.</returns>
    public DateOnly WiedervorlageAm(int jahre) => KalkuliertAm.AddYears(jahre);
}
