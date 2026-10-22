namespace Fevad.Domain.Laufbahn;

/// <summary>Codes der Abteilungen aus dem Katalog, an denen Regeln hängen (Satzung: Einsatz-, Jugend- und Altersabteilung).</summary>
public static class AbteilungCode
{
    public const string Einsatz = "EA";
    public const string Jugend = "JF";
    public const string Alters = "AA";

    /// <summary>Gibt es in Adelberg nicht; wer eine Musikabteilung anlegt, muss diesen Code verwenden.</summary>
    public const string Musik = "MU";
}

/// <summary>Qualifikations-Codes der Jugendabzeichen (Katalog der Personalverwaltung).</summary>
public static class JugendabzeichenCode
{
    public const string Leistungsspange = "LSP";
    public const string Jugendflamme1 = "JFL1";
    public const string Jugendflamme2 = "JFL2";
    public const string Jugendflamme3 = "JFL3";
}
