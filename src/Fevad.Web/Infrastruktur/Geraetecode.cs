using Microsoft.Extensions.Options;
using QRCoder;

namespace Fevad.Web.Infrastruktur;

/// <summary>QR-Code am Gerät: führt auf /g/{Inventarnummer}, von dort zum Gerät oder zur Mängelmeldung.</summary>
public sealed class Geraetecode(IOptions<AnmeldungOptionen> optionen)
{
    public string Adresse(HttpRequest anfrage, string inventarnummer)
    {
        // Etiketten hängen jahrelang am Gerät; deshalb die feste öffentliche Adresse, wenn sie gesetzt ist.
        var basis = optionen.Value.OeffentlicheAdresse is { Length: > 0 } adresse
            ? adresse.TrimEnd('/')
            : $"{anfrage.Scheme}://{anfrage.Host}{anfrage.PathBase}";
        return $"{basis}/g/{Uri.EscapeDataString(inventarnummer)}";
    }

    public static string Svg(string inhalt)
    {
        using var generator = new QRCodeGenerator();
        using var daten = generator.CreateQrCode(inhalt, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(daten).GetGraphic(4);
    }
}
