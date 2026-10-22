namespace Fevad.Web.Infrastruktur;

public static class Hochladen
{
    /// <summary>
    /// Technische Obergrenze einer Upload-Anfrage. Die fachliche Grenze steht im Parameter
    /// Dokument.Datei.MaxMegabyte und muss darunter bleiben.
    /// </summary>
    public const long MaxAnfrageBytes = 100L * 1024 * 1024;
}
