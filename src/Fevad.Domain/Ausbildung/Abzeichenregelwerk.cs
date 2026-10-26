using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Ausbildung;

/// <summary>
/// Regelwerk für die Abnahme von Jugendabzeichen, z. B. die Jugendflamme nach den Kreisrichtlinien Heilbronn 2017 oder nach
/// Neckar-Odenwald 2026 (V50a). Abnahmen finden auch außerhalb des Landkreises statt, deshalb gelten mehrere Regelwerke
/// nebeneinander; der Jugendwart wählt je Abnahme das Regelwerk des Abnahmeorts. Altersgrenzen und Fristen stehen an den
/// <see cref="AbzeichenregelwerkStufe"/>n, nicht im Code.
/// </summary>
public class Abzeichenregelwerk : Katalogeintrag
{
    /// <summary>Wo das Regelwerk angewendet wird, z. B. „Landkreis Heilbronn, Baden-Baden“.</summary>
    public string? Geltungsbereich { get; set; }

    /// <summary>Fundstelle der Richtlinie, z. B. Internetadresse der Kreisjugendfeuerwehr.</summary>
    public string? Quelle { get; set; }

    /// <summary>Vorbelegung bei neuen Abnahmen; höchstens ein Regelwerk ist Standard.</summary>
    public bool Standard { get; set; }

    /// <summary>Abweichende Regeln je Abzeichenstufe; eine Stufe ohne Eintrag folgt dem Katalog.</summary>
    public List<AbzeichenregelwerkStufe> Stufen { get; set; } = [];
}

/// <summary>
/// Regeln einer Abzeichenstufe in einem <see cref="Abzeichenregelwerk"/>. Leere Felder übernehmen den Wert aus dem
/// Katalogeintrag der <see cref="Qualifikation"/>.
/// </summary>
public class AbzeichenregelwerkStufe : Entitaet
{
    /// <summary>Schlüssel des Regelwerks.</summary>
    public Guid RegelwerkId { get; set; }
    /// <summary>Das Regelwerk.</summary>
    public Abzeichenregelwerk? Regelwerk { get; set; }

    /// <summary>Schlüssel der Abzeichenstufe im Qualifikationskatalog.</summary>
    public Guid QualifikationId { get; set; }
    /// <summary>Die Abzeichenstufe.</summary>
    public Qualifikation? Qualifikation { get; set; }

    /// <summary>Mindestalter nach diesem Regelwerk; leer heißt wie im Katalog.</summary>
    public int? MindestalterJahre { get; set; }

    /// <summary>Nötige Nachweise als Codes wie im Katalog („EH“, „TM1; AGT|SPRF“); leer heißt wie im Katalog.</summary>
    public string? Voraussetzungen { get; set; }

    /// <summary>Die nötigen Nachweise dürfen am Abnahmetag höchstens so viele Monate alt sein, z. B. Erste Hilfe 24; leer heißt ohne Grenze.</summary>
    public int? NachweisHoechstensMonate { get; set; }

    /// <summary>Hinweis für den Jugendwart ohne Prüfung, z. B. „Leistungsspange soll vorher abgelegt sein“.</summary>
    public string? Hinweis { get; set; }
}
