using Fevad.Domain.Dokumente;
using Fevad.Domain.Unfaelle;

namespace Fevad.Web.Infrastruktur;

/// <summary>Grenzen für Datei-Uploads.</summary>
public static class Hochladen
{
    /// <summary>
    /// Technische Obergrenze einer Upload-Anfrage. Die fachliche Grenze steht im Parameter
    /// Dokument.Datei.MaxMegabyte und muss darunter bleiben.
    /// </summary>
    public const long MaxAnfrageBytes = 100L * 1024 * 1024;

    /// <summary>Prüft eine Datei, die nur JPG, PNG oder PDF sein darf.</summary>
    /// <param name="datei">Die hochgeladene Datei.</param>
    /// <param name="maxMegabyte">Fachliche Obergrenze aus dem Parameter.</param>
    /// <returns>Fehlertext oder <c>null</c>.</returns>
    public static string? BildOderPdfPruefen(IFormFile? datei, int maxMegabyte)
    {
        if (datei is null || datei.Length == 0)
        {
            return "Bitte eine Datei wählen.";
        }

        var name = Dateitypen.Bereinigen(datei.FileName);
        if (Dateitypen.Inhaltstyp(name) is not ("image/jpeg" or "image/png" or "application/pdf"))
        {
            return $"„{name}“: nur JPG, PNG oder PDF.";
        }

        return datei.Length > maxMegabyte * 1024L * 1024L ? $"„{name}“ ist größer als {maxMegabyte} MB." : null;
    }

    /// <summary>Liest eine geprüfte Datei; Fotos kommen ohne Standort- und Kameradaten zurück.</summary>
    /// <param name="datei">Die mit <see cref="BildOderPdfPruefen"/> geprüfte Datei.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Name, Inhaltstyp und Inhalt; der Inhalt ist <c>null</c>, wenn ein Foto kein gültiges Bild ist.</returns>
    public static async Task<(string Name, string Typ, byte[]? Inhalt)> BildOderPdfLesenAsync(IFormFile datei, CancellationToken ct)
    {
        var name = Dateitypen.Bereinigen(datei.FileName);
        var typ = Dateitypen.Inhaltstyp(name)!;
        using var speicher = new MemoryStream();
        await datei.CopyToAsync(speicher, ct);
        var inhalt = speicher.ToArray();
        return (name, typ, typ == "application/pdf" ? inhalt : Fotobereinigung.Bereinigen(inhalt, typ));
    }
}
