namespace Fevad.Domain.Gemeinsam;

/// <summary>Basis für pflegbare Kataloge (Abteilung, Dienstgrad, Funktion, ...).</summary>
public abstract class Katalogeintrag : Entitaet
{
    /// <summary>Stabiler Kurzschlüssel des Eintrags, auf den sich Code und Importe beziehen, z. B. „TM“.</summary>
    public required string Code { get; set; }
    /// <summary>Volle Bezeichnung für die Oberfläche, z. B. „Truppmann“.</summary>
    public required string Bezeichnung { get; set; }
    /// <summary>Kurzform für Listen und Ausdrucke, z. B. „TM“.</summary>
    public string? Kurzbezeichnung { get; set; }
    /// <summary>Reihenfolge in Auswahllisten; kleinere Werte zuerst.</summary>
    public int Sortierung { get; set; }
    /// <summary>Nur aktive Einträge werden zur Auswahl angeboten; bestehende Zuordnungen bleiben erhalten.</summary>
    public bool Aktiv { get; set; } = true;
}
