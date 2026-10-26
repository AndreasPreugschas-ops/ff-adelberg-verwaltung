using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Einsaetze;

/// <summary>
/// Eintrag im Verzeichnis externer Stellen (V20): Dienststellen, mit denen die Feuerwehr im Einsatz zu tun hat, z. B. Polizeirevier,
/// Rettungswache, Kreisbrandmeister, THW, Energieversorger, Wasserbehörde, Bauhof, PSNV. Nur dienstliche Erreichbarkeiten, keine Privatnummern.
/// </summary>
public class ExterneStelle : Entitaet
{
    /// <summary>Art der Stelle.</summary>
    public OrganisationArt Art { get; set; }

    /// <summary>Bezeichnung, z. B. „Polizeirevier Göppingen“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Dienstliche Telefonnummer.</summary>
    public string? Telefon { get; set; }

    /// <summary>Dienstliche Mailadresse.</summary>
    public string? Email { get; set; }

    /// <summary>Anschrift der Dienststelle.</summary>
    public string? Anschrift { get; set; }

    /// <summary>Bemerkung, z. B. Erreichbarkeit außerhalb der Dienstzeit.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Die Stelle wird noch angeboten; abgeschaltete bleiben für alte Einsätze erhalten.</summary>
    public bool Aktiv { get; set; } = true;
}
