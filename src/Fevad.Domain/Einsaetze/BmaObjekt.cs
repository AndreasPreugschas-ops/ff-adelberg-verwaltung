using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <summary>
/// Objektakte (V20): Objekt mit Brandmeldeanlage, die bei der Leitstelle aufgeschaltet ist (V24), oder Sonderobjekt ohne BMA
/// (z. B. Pflegeheim, Schule, Gewerbe mit Gefahrstoffen). Bei einer BMA ist der Betreiber bei einem Fehlalarm Kostenschuldner nach
/// § 34 Abs. 1 Nr. 6 FwG; FEVAD zählt die Fehlalarme je Jahr und legt daraus die Kostenersatz-Vorgänge in Serie an.
/// Hydranten und Karten bleiben außen vor.
/// </summary>
public class BmaObjekt : Entitaet
{
    /// <summary>Bezeichnung des Objekts, z. B. „Seniorenzentrum Adelberg“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Anschrift des Objekts.</summary>
    public string? Anschrift { get; set; }

    /// <summary>Nummer der Aufschaltung bei der Leitstelle bzw. beim Konzessionär.</summary>
    public string? Aufschaltnummer { get; set; }

    /// <summary>Konzessionär der Übertragungseinrichtung.</summary>
    public string? Konzessionaer { get; set; }

    /// <summary>Name des Betreibers (Person oder Firma), Kostenschuldner bei einem Fehlalarm.</summary>
    public required string BetreiberName { get; set; }

    /// <summary>Zustellanschrift des Betreibers.</summary>
    public string? BetreiberAnschrift { get; set; }

    /// <summary>Ansprechpartner beim Betreiber mit Telefon, nur für Rückfragen und den Bescheid.</summary>
    public string? Ansprechpartner { get; set; }

    /// <summary>Die Anlage ist noch aufgeschaltet; abgeschaltete Objekte bleiben für alte Einsätze erhalten.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>Tag, an dem die Anlage als abgeschaltet erfasst wurde; von hier läuft die Löschfrist L47.</summary>
    public DateOnly? AbgeschaltetAm { get; set; }

    /// <summary>Bemerkung, z. B. Laufkarten oder Schlüsseldepot.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Das Objekt hat eine aufgeschaltete Brandmeldeanlage; ohne BMA ist es ein Sonderobjekt ohne Fehlalarm-Kostenersatz.</summary>
    public bool HatBma { get; set; } = true;

    /// <summary>Art der Nutzung, z. B. „Pflegeheim“, „Schule“, „Landwirtschaft mit Biogasanlage“.</summary>
    public string? Nutzung { get; set; }

    /// <summary>Feuerwehrplan nach DIN 14095 als Dokument in der Dokumentenverwaltung.</summary>
    public Guid? FeuerwehrplanDokumentId { get; set; }

    /// <summary>Stand des Feuerwehrplans laut Plankopf.</summary>
    public DateOnly? FeuerwehrplanStand { get; set; }

    /// <summary>Begehungen des Objekts.</summary>
    public List<ObjektBegehung> Begehungen { get; set; } = [];

    /// <summary>
    /// Legt für einen Fehlalarm dieses Objekts den Kostenersatz-Vorgang an: Tatbestand § 34 Abs. 1 Nr. 6 FwG, Betreiber als Schuldner,
    /// Sachverhalt mit Objekt und Aufschaltnummer. Empfehlung und Entscheidung bleiben wie bei jedem Vorgang Sache von Feuerwehr und Gemeinde.
    /// </summary>
    /// <param name="einsatzId">Kennung des Einsatzes.</param>
    /// <param name="tag">Tag des Einsatzes in Ortszeit.</param>
    /// <param name="nummerImJahr">Der wievielte Fehlalarm des Objekts im Jahr es ist.</param>
    /// <returns>Der neue Vorgang mit einem Schuldner.</returns>
    public Kostenersatzvorgang SerienVorgang(Guid einsatzId, DateOnly tag, int nummerImJahr) => new()
    {
        EinsatzId = einsatzId,
        Tatbestand = Kostentatbestand.Abs1Nr6Brandmeldeanlage,
        Sachverhalt = $"Fehlalarm der Brandmeldeanlage {Bezeichnung}"
            + (string.IsNullOrWhiteSpace(Aufschaltnummer) ? "" : $" (Aufschaltnummer {Aufschaltnummer})")
            + $" am {tag:dd.MM.yyyy}, {nummerImJahr}. Fehlalarm des Objekts im Jahr {tag.Year}.",
        Schuldner =
        [
            new Kostenschuldner { Art = SchuldnerArt.Betreiber, Name = BetreiberName, Anschrift = BetreiberAnschrift, Zustellweg = Zustellweg.Brief },
        ],
    };
}

/// <summary>Eine Begehung eines Objekts mit Brandmeldeanlage oder eines Sonderobjekts (V20).</summary>
public class ObjektBegehung : Entitaet
{
    /// <summary>Kennung des Objekts.</summary>
    public Guid BmaObjektId { get; set; }

    /// <summary>Das Objekt.</summary>
    public BmaObjekt? BmaObjekt { get; set; }

    /// <summary>Tag der Begehung.</summary>
    public DateOnly Am { get; set; }

    /// <summary>Teilnehmer, z. B. „Kommandant, Betreiber, Gruppenführer Abt. Hundsholz“.</summary>
    public string? Teilnehmer { get; set; }

    /// <summary>Ergebnis und Maßnahmen, z. B. „Laufkarten veraltet, Betreiber erneuert bis 31.03.“.</summary>
    public string? Ergebnis { get; set; }
}
