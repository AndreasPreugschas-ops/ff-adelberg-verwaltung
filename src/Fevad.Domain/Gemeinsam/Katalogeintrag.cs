namespace Fevad.Domain.Gemeinsam;

/// <summary>Basis für pflegbare Kataloge (Abteilung, Dienstgrad, Funktion, ...).</summary>
public abstract class Katalogeintrag : Entitaet
{
    public required string Code { get; set; }
    public required string Bezeichnung { get; set; }
    public string? Kurzbezeichnung { get; set; }
    public int Sortierung { get; set; }
    public bool Aktiv { get; set; } = true;
}
