using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Einsaetze;

/// <summary>Wie ein Gerät im Einsatz belastet wurde; bestimmt den Sperrtext.</summary>
public enum Belastung
{
    /// <summary>Normal benutzt; Prüfung nach Gebrauch.</summary>
    [Display(Name = "benutzt")] Benutzt,
    /// <summary>Stoßartig belastet (z. B. Leine, Gurt im Absturz); sofort prüfen.</summary>
    [Display(Name = "dynamisch belastet")] DynamischBelastet,
    /// <summary>Mit Gefahrstoffen beaufschlagt; erst reinigen, dann prüfen.</summary>
    [Display(Name = "kontaminiert")] Kontaminiert,
}

/// <summary>
/// Im Einsatz benutztes Gerät (V13). Ein Einzelgerät wird bis zur nächsten bestandenen Prüfung gesperrt;
/// Mengenartikel wie Schläuche kommen nur als Anzahl auf die Werkstattliste und werden nicht gesperrt.
/// </summary>
public class EinsatzGeraet : Entitaet
{
    /// <summary>Einsatz, in dem das Gerät benutzt wurde.</summary>
    public Guid EinsatzId { get; set; }
    /// <summary>Navigation zum Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Einzelgerät mit Inventarnummer; sonst Mengenartikel über <see cref="MaterialId"/>.</summary>
    public Guid? GeraetId { get; set; }
    /// <summary>Navigation zum Einzelgerät.</summary>
    public Geraet? Geraet { get; set; }

    /// <summary>Materialstamm eines Mengenartikels; leer bei Einzelgeräten.</summary>
    public Guid? MaterialId { get; set; }
    /// <summary>Navigation zum Materialstamm.</summary>
    public Material? Material { get; set; }
    /// <summary>Stückzahl bei Mengenartikeln; bei Einzelgeräten immer 1.</summary>
    public int Anzahl { get; set; } = 1;

    /// <summary>Fahrzeug, von dem das Gerät kam.</summary>
    public Guid? FahrzeugId { get; set; }
    /// <summary>Navigation zum Herkunftsfahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Belastung im Einsatz.</summary>
    public Belastung Belastung { get; set; }
    /// <summary>Hinweis des Einsatzleiters an den Gerätewart.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Sperre des Einzelgeräts bis zur Prüfung.</summary>
    public Guid? MangelId { get; set; }

    /// <summary>Mengenartikel zurück und geprüft; bei Einzelgeräten erledigt die Prüfung die Sperre.</summary>
    public DateTimeOffset? ErledigtAm { get; set; }
    /// <summary>Benutzerkonto, das den Mengenartikel als zurück vermerkt hat.</summary>
    public Guid? ErledigtVon { get; set; }

    /// <summary>Mengenartikel ohne Inventarnummer und ohne Sperre.</summary>
    public bool IstMengenartikel => GeraetId is null;

    /// <summary>Text der Sperre am Gerät; die Belastung entscheidet, wie dringend geprüft wird.</summary>
    public static string Sperrtext(string einsatznummer, Belastung belastung) => belastung switch
    {
        Belastung.DynamischBelastet => $"Dynamisch belastet im Einsatz {einsatznummer}: sofort prüfen (DGUV Grundsatz 305-002).",
        Belastung.Kontaminiert => $"Kontaminiert im Einsatz {einsatznummer}: reinigen, dann prüfen.",
        _ => $"Benutzt im Einsatz {einsatznummer}: Prüfung nach Gebrauch.",
    };
}
