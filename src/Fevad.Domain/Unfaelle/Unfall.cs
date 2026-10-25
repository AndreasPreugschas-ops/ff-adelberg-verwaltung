using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Unfaelle;

/// <summary>Bei welcher Gelegenheit sich ein Unfall ereignet hat.</summary>
public enum UnfallAnlass
{
    /// <summary>Bei einem Einsatz.</summary>
    [Display(Name = "Einsatz")] Einsatz,
    /// <summary>Bei einer Übung oder einem sonstigen Dienst.</summary>
    [Display(Name = "Übung oder Dienst")] Dienst,
    /// <summary>Bei einem Lehrgang.</summary>
    [Display(Name = "Lehrgang")] Lehrgang,
    /// <summary>Auf dem Weg zum oder vom Dienst (Wegeunfall).</summary>
    [Display(Name = "Weg zum oder vom Dienst")] Weg,
    /// <summary>Sonstige Gelegenheit.</summary>
    [Display(Name = "Sonstiges")] Sonstiges,
}

/// <summary>Was gemeldet wird: Dienstunfall, Kleinverletzung fürs Verbandbuch oder Beinaheunfall (V40).</summary>
public enum UnfallArt
{
    /// <summary>Dienstunfall; bei Tod oder mehr als drei Tagen Arbeitsunfähigkeit anzeigepflichtig.</summary>
    [Display(Name = "Dienstunfall")] Unfall,
    /// <summary>Kleinverletzung ohne Arztbesuch, Eintrag ins Verbandbuch (DGUV Vorschrift 1 § 24 Abs. 6).</summary>
    [Display(Name = "Kleinverletzung (Verbandbuch)")] Verbandbuch,
    /// <summary>Beinaheunfall ohne Verletzung, für die Gefährdungsbeurteilung.</summary>
    [Display(Name = "Beinaheunfall")] Beinaheunfall,
}

/// <summary>Katalog der Verletzungsarten, z. B. „Schnittwunde“; frei getippte Angaben gibt es nicht (Andi, 25.10.2026).</summary>
public class Verletzungsart : Katalogeintrag
{
}

/// <summary>Bearbeitungsstand eines Unfalls.</summary>
public enum UnfallStatus
{
    /// <summary>Erfasst, noch nicht gemeldet.</summary>
    [Display(Name = "erfasst")] Erfasst,
    /// <summary>Die Unfallanzeige ist an die Unfallkasse gegangen.</summary>
    [Display(Name = "an die Unfallkasse gemeldet")] Gemeldet,
    /// <summary>Abgeschlossen; ab dann läuft die Löschfrist.</summary>
    [Display(Name = "abgeschlossen")] Abgeschlossen,
}

/// <summary>
/// Dienstunfall eines Mitglieds mit den Angaben der Unfallanzeige nach § 193 SGB VII für die Unfallkasse Baden-Württemberg.
/// Verletzung und Erstbehandlung sind Gesundheitsdaten (Art. 9 DSGVO): nur so knapp, wie die Unfallanzeige sie verlangt,
/// keine Befunde oder Arztberichte. Gelöscht wird nach Löschkonzept L36.
/// </summary>
public class Unfall : Entitaet
{
    /// <summary>Verletzte Person.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das verletzte Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Dienstunfall, Kleinverletzung (Verbandbuch) oder Beinaheunfall.</summary>
    public UnfallArt Art { get; set; }

    /// <summary>Vom Mitglied selbst gemeldet statt von Führungskraft oder Kommandant erfasst.</summary>
    public bool Selbstmeldung { get; set; }

    /// <summary>Gelegenheit des Unfalls.</summary>
    public UnfallAnlass Anlass { get; set; }
    /// <summary>Schlüssel des Einsatzes, wenn der Unfall bei einem Einsatz geschah.</summary>
    public Guid? EinsatzId { get; set; }
    /// <summary>Schlüssel des Dienstes, wenn der Unfall bei einem Dienst geschah.</summary>
    public Guid? DienstId { get; set; }

    /// <summary>Zeitpunkt des Unfalls in UTC.</summary>
    public DateTimeOffset Zeitpunkt { get; set; }
    /// <summary>Unfallort.</summary>
    public required string Ort { get; set; }

    /// <summary>Tätigkeit zum Unfallzeitpunkt, z. B. „Angriffstrupp, Brandbekämpfung im Innenangriff“.</summary>
    public string? Taetigkeit { get; set; }

    /// <summary>Ausführliche Schilderung des Hergangs, nur der Ablauf, keine Diagnosen oder Befunde. Nicht im Protokoll, weil oft Verletzungen anklingen.</summary>
    [NichtProtokollieren]
    public required string Hergang { get; set; }

    /// <summary>Verletzte Körperteile, z. B. „linke Hand“ (Gesundheitsdatum).</summary>
    [NichtProtokollieren]
    public string? VerletzteKoerperteile { get; set; }

    /// <summary>Art der Verletzung aus dem Katalog, z. B. „Schnittwunde“ (Gesundheitsdatum, kein Befund).</summary>
    [NichtProtokollieren]
    public Guid? VerletzungsartId { get; set; }
    /// <summary>Die Verletzungsart.</summary>
    public Verletzungsart? Verletzungsart { get; set; }

    /// <summary>Der Unfall endete tödlich.</summary>
    public bool Toedlich { get; set; }

    /// <summary>Erstbehandelnder Arzt oder Krankenhaus mit Ort.</summary>
    [NichtProtokollieren]
    public string? Erstbehandlung { get; set; }

    /// <summary>Krankenkasse der verletzten Person, für die Weiterleitung an die Unfallkasse.</summary>
    public string? Krankenkasse { get; set; }

    /// <summary>Dienst bzw. Arbeit eingestellt: leer heißt nein.</summary>
    public DateTimeOffset? DienstEingestelltAm { get; set; }

    /// <summary>Voraussichtlich mehr als drei Kalendertage arbeitsunfähig; dann ist die Unfallanzeige Pflicht.</summary>
    public bool MehrAlsDreiTageArbeitsunfaehig { get; set; }

    /// <summary>Wer zuerst vom Unfall erfahren hat.</summary>
    public string? ZuerstKenntnis { get; set; }

    /// <summary>Bearbeitungsstand.</summary>
    public UnfallStatus Status { get; set; }
    /// <summary>Tag der Meldung an die Unfallkasse.</summary>
    public DateOnly? GemeldetAm { get; set; }

    /// <summary>Aktenzeichen der Unfallkasse.</summary>
    public string? Aktenzeichen { get; set; }
    /// <summary>Tag des Abschlusses.</summary>
    public DateOnly? AbgeschlossenAm { get; set; }
    /// <summary>Freitext.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Zeugen und Ersthelfer.</summary>
    public List<UnfallZeuge> Zeugen { get; set; } = [];

    /// <summary>Fotos vom Unfallort oder Schaden.</summary>
    public List<Meldungsfoto> Fotos { get; set; } = [];

    /// <summary>Anzeigepflichtig nach § 193 Abs. 1 SGB VII: ein Dienstunfall, tödlich oder mit mehr als drei Tagen Arbeitsunfähigkeit.</summary>
    public bool Anzeigepflichtig => Art == UnfallArt.Unfall && (Toedlich || MehrAlsDreiTageArbeitsunfaehig);

    /// <summary>Letzter Tag für die Anzeige: Kenntnis vom Unfall (hier der Unfalltag) plus Frist; null, wenn nicht anzeigepflichtig.</summary>
    /// <param name="fristTage">Anzeigefrist in Tagen (Parameter <see cref="ParameterSchluessel.UnfallAnzeigefristTage"/>).</param>
    /// <returns>Der letzte Tag der Anzeigefrist oder null.</returns>
    public DateOnly? AnzeigeBis(int fristTage) => Anzeigepflichtig ? Ortszeit.Datum(Zeitpunkt).AddDays(fristTage) : null;

    /// <summary>Ampel der Anzeigefrist: rot, wenn überfällig und noch nicht gemeldet.</summary>
    /// <param name="heute">Heutiger Tag.</param>
    /// <param name="fristTage">Anzeigefrist in Tagen.</param>
    /// <returns>Die Ampel; grün, wenn nicht anzeigepflichtig oder schon gemeldet.</returns>
    public Fristampel AnzeigeAmpel(DateOnly heute, int fristTage) =>
        AnzeigeBis(fristTage) is not { } bis || Status != UnfallStatus.Erfasst ? Fristampel.Gruen : Frist.Bewerten(bis, heute, fristTage);

    /// <summary>Status setzen; „gemeldet“ und „abgeschlossen“ bekommen ihr Datum, ein Zurücksetzen nimmt es wieder weg.</summary>
    /// <param name="neu">Neuer Status.</param>
    /// <param name="heute">Heutiger Tag für die Datumsfelder.</param>
    public void StatusSetzen(UnfallStatus neu, DateOnly heute)
    {
        Status = neu;
        GemeldetAm = neu == UnfallStatus.Erfasst ? null : GemeldetAm ?? heute;
        AbgeschlossenAm = neu == UnfallStatus.Abgeschlossen ? AbgeschlossenAm ?? heute : null;
    }
}

/// <summary>Zeuge oder Ersthelfer; ein Mitglied oder eine andere Person mit Kontakt.</summary>
public class UnfallZeuge : Entitaet
{
    /// <summary>Schlüssel des Unfalls.</summary>
    public Guid UnfallId { get; set; }
    /// <summary>Der Unfall.</summary>
    public Unfall? Unfall { get; set; }

    /// <summary>Name des Zeugen.</summary>
    public required string Name { get; set; }
    /// <summary>Telefon, E-Mail oder Anschrift.</summary>
    public string? Kontakt { get; set; }

    /// <summary>Hat den Unfall selbst gesehen (Augenzeuge), sonst nur hinzugekommen.</summary>
    public bool Augenzeuge { get; set; }
}
