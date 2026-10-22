using System.Reflection;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Version nach SemVer; im Image setzt die CI sie aus dem GitHub-Release, ergänzt um den Commit (<c>SourceRevisionId</c>).
/// </summary>
public static class Programmversion
{
    public const string Name = "FEVAD";

    private static readonly string Informationell =
        typeof(Programmversion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    /// <summary>Version ohne Build-Metadaten, z. B. <c>0.1.0</c> oder <c>0.2.0-beta.1</c>.</summary>
    public static string Version { get; } = Informationell.Split('+')[0];

    /// <summary>Kurzer Commit-Hash, falls beim Build bekannt.</summary>
    public static string? Commit { get; } =
        Informationell.Split('+') is [_, var metadaten, ..] ? metadaten[..Math.Min(7, metadaten.Length)] : null;

    /// <summary>Anzeige für die Oberfläche, z. B. <c>FEVAD 0.1.0 (1a2b3c4)</c>.</summary>
    public static string Anzeige { get; } = Commit is null ? $"{Name} {Version}" : $"{Name} {Version} ({Commit})";
}
