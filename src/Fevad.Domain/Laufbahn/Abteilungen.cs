namespace Fevad.Domain.Laufbahn;

/// <summary>
/// Codes der vorbelegten Abteilungen. Die Regeln hängen an der <see cref="Fevad.Domain.Mitglieder.AbteilungArt"/>,
/// nicht am Code; weitere Einsatzabteilungen (etwa je Ortsteil) bekommen eigene Codes.
/// </summary>
public static class AbteilungCode
{
    /// <summary>Code der Einsatzabteilung.</summary>
    public const string Einsatz = "EA";
    /// <summary>Code der Jugendfeuerwehr.</summary>
    public const string Jugend = "JF";
    /// <summary>Code der Kinderfeuerwehr.</summary>
    public const string Kinder = "KF";
    /// <summary>Code der Altersabteilung.</summary>
    public const string Alters = "AA";
}

