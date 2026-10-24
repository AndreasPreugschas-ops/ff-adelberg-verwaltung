namespace Fevad.Domain.Gemeinsam;

/// <summary>
/// Anschriften der Feuerwehr und ihrer Trägerin, der Gemeinde, dazu das Logo für die Formulare. Es gibt genau einen
/// Datensatz; der Start legt ihn an. Die Gemeinde ist Verantwortliche im Sinne der DSGVO.
/// </summary>
public class Organisation : Entitaet
{
    public string FeuerwehrName { get; set; } = "Freiwillige Feuerwehr";
    public string? FeuerwehrStrasse { get; set; }
    public string? FeuerwehrPlz { get; set; }
    public string? FeuerwehrOrt { get; set; }
    public string? FeuerwehrTelefon { get; set; }
    public string? FeuerwehrEmail { get; set; }

    public string GemeindeName { get; set; } = "Gemeinde";

    /// <summary>Vertretung der Gemeinde, z. B. „vertreten durch den Bürgermeister“.</summary>
    public string? GemeindeVertretung { get; set; }
    public string? GemeindeStrasse { get; set; }
    public string? GemeindePlz { get; set; }
    public string? GemeindeOrt { get; set; }
    public string? GemeindeTelefon { get; set; }
    public string? GemeindeEmail { get; set; }

    /// <summary>Logo als SVG, PNG oder JPEG; erscheint im Kopf der Formulare.</summary>
    [NichtProtokollieren]
    public byte[]? Logo { get; set; }
    public string? LogoInhaltstyp { get; set; }

    public string? FeuerwehrAnschrift => Anschrift.Zeile(FeuerwehrStrasse, FeuerwehrPlz, FeuerwehrOrt);
    public string? GemeindeAnschrift => Anschrift.Zeile(GemeindeStrasse, GemeindePlz, GemeindeOrt);
}

public static class Anschrift
{
    /// <summary>„Straße, PLZ Ort“; null, solange Straße oder Ort fehlen.</summary>
    public static string? Zeile(string? strasse, string? plz, string? ort) =>
        string.IsNullOrWhiteSpace(strasse) || string.IsNullOrWhiteSpace(ort) ? null : $"{strasse.Trim()}, {$"{plz} {ort}".Trim()}";
}
