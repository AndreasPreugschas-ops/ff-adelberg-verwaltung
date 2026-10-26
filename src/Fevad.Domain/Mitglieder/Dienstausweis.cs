using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>
/// Vom Bürgermeister ausgestellter Dienstausweis (Satzung § 3 Abs. 6, V27). Das Lichtbild wird nur zum Drucken hochgeladen und nicht gespeichert;
/// FEVAD führt Nummer, Gültigkeit und Rückgabe.
/// </summary>
public class Dienstausweis : Entitaet
{
    /// <summary>Kennung des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Ausweisnummer, z. B. Personalnummer und laufende Nummer.</summary>
    public required string Nummer { get; set; }

    /// <summary>Tag der Ausstellung.</summary>
    public DateOnly AusgestelltAm { get; set; }
    /// <summary>Gültig bis.</summary>
    public DateOnly GueltigBis { get; set; }

    /// <summary>Tag der Rückgabe; null, solange der Ausweis ausgegeben ist.</summary>
    public DateOnly? ZurueckAm { get; set; }
    /// <summary>Vermerk zur Rückgabe, z. B. „verloren gemeldet“.</summary>
    public string? Vermerk { get; set; }

    /// <summary>Der Ausweis ist ausgegeben und nicht zurückgegeben.</summary>
    public bool Ausgegeben => ZurueckAm is null;

    /// <summary>Nächste Ausweisnummer eines Mitglieds.</summary>
    /// <param name="personalnummer">Personalnummer.</param>
    /// <param name="bisher">Zahl der bisherigen Ausweise des Mitglieds.</param>
    /// <returns>Nummer der Form „Personalnummer-laufende Nummer“.</returns>
    public static string NaechsteNummer(string personalnummer, int bisher) => $"{personalnummer}-{bisher + 1}";
}
