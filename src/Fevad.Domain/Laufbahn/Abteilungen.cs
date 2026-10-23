namespace Fevad.Domain.Laufbahn;

/// <summary>
/// Codes der vorbelegten Abteilungen. Die Regeln hängen an der <see cref="Fevad.Domain.Mitglieder.AbteilungArt"/>,
/// nicht am Code; weitere Einsatzabteilungen (etwa je Ortsteil) bekommen eigene Codes.
/// </summary>
public static class AbteilungCode
{
    public const string Einsatz = "EA";
    public const string Jugend = "JF";
    public const string Kinder = "KF";
    public const string Alters = "AA";
}

