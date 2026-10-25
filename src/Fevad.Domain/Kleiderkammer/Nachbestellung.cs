namespace Fevad.Domain.Kleiderkammer;

/// <summary>Nachbestellbedarf eines Artikels in einer Größe. Wird nie gespeichert.</summary>
/// <param name="Mindestmenge">Mindestbestand im Lager.</param>
/// <param name="ImLager">Stücke im Lager.</param>
/// <param name="FehlendeGrundausstattung">Mitglieder, denen dieses Stück aus der Grundausstattung fehlt.</param>
/// <param name="ErsatzFuerAusgegebene">Ausgegebene Stücke, die bald ihre Lebensdauer erreichen.</param>
/// <param name="LagerBaldAuszusondern">Lagerstücke, die bald ihre Lebensdauer erreichen.</param>
/// <param name="BereitsBeschafft">Stücke in offenen Beschaffungen.</param>
public sealed record Nachbestellbedarf(
    int Mindestmenge,
    int ImLager,
    int FehlendeGrundausstattung,
    int ErsatzFuerAusgegebene,
    int LagerBaldAuszusondern,
    int BereitsBeschafft)
{
    /// <summary>So viele Stücke sollen verfügbar sein: Mindestmenge, Erstausstattung und Ersatz für ablaufende ausgegebene Stücke.</summary>
    public int Zielbestand => Mindestmenge + FehlendeGrundausstattung + ErsatzFuerAusgegebene;

    /// <summary>Lagerstücke, die nicht bald selbst ausgesondert werden.</summary>
    public int Verfuegbar => Math.Max(0, ImLager - LagerBaldAuszusondern);

    /// <summary>Fehlende Stücke ohne Rücksicht auf offene Beschaffungen.</summary>
    public int Bedarf => Math.Max(0, Zielbestand - Verfuegbar);

    /// <summary>Was nach offenen Beschaffungen noch zu bestellen ist.</summary>
    public int Offen => Math.Max(0, Bedarf - BereitsBeschafft);

    /// <summary>Gründe des Bedarfs als Text für die Bedarfsliste.</summary>
    /// <returns>Die Gründe; leer, wenn kein Bedarf besteht.</returns>
    public IReadOnlyList<string> Gruende()
    {
        var gruende = new List<string>();
        if (Mindestmenge > 0 && Verfuegbar < Mindestmenge)
        {
            gruende.Add($"Mindestbestand {Mindestmenge}, im Lager {Verfuegbar}");
        }

        if (FehlendeGrundausstattung > 0)
        {
            gruende.Add($"{FehlendeGrundausstattung} Mitglied(er) ohne Grundausstattung");
        }

        if (ErsatzFuerAusgegebene > 0)
        {
            gruende.Add($"{ErsatzFuerAusgegebene} ausgegebene(s) Stück(e) erreichen bald die Lebensdauer");
        }

        if (LagerBaldAuszusondern > 0)
        {
            gruende.Add($"{LagerBaldAuszusondern} Lagerstück(e) erreichen bald die Lebensdauer");
        }

        if (BereitsBeschafft > 0)
        {
            gruende.Add($"{BereitsBeschafft} bereits in Beschaffung");
        }

        return gruende;
    }
}
