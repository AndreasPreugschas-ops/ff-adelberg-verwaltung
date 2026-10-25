using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

/// <summary>Art des Anlasses eines Prüfhinweises.</summary>
public enum PruefhinweisArt
{
    /// <summary>Eine Beförderung ist möglich.</summary>
    [Display(Name = "Beförderung")] Befoerderung,
    /// <summary>Eine Ehrung ist fällig.</summary>
    [Display(Name = "Ehrung")] Ehrung,
    /// <summary>Probezeitende, Übertritt oder Altersgrenze steht an.</summary>
    [Display(Name = "Mitgliedschaft")] Mitgliedschaft,
    /// <summary>Bei einer Funktion fehlt eine Voraussetzung oder die Amtszeit läuft aus.</summary>
    [Display(Name = "Funktion")] Funktion,
}

/// <summary>Bearbeitungsstand eines Prüfhinweises.</summary>
public enum PruefhinweisStatus
{
    /// <summary>Offen; wartet auf eine Entscheidung.</summary>
    [Display(Name = "offen")] Offen,

    /// <summary>Die Prüfung findet den Anlass nicht mehr, z. B. weil die Beförderung eingetragen ist.</summary>
    [Display(Name = "entfallen")] Entfallen,

    /// <summary>Von Hand zurückgestellt; die Prüfung meldet denselben Anlass nicht erneut.</summary>
    [Display(Name = "zurückgestellt")] Zurueckgestellt,
}

/// <summary>
/// Ergebnis der Personalprüfung: ein Anlass, über den jemand entscheiden sollte (Beförderung möglich, Ehrung fällig,
/// Übertritt, Funktion ohne Voraussetzung). Wird bei Änderungen am Mitglied und im Gesamtlauf neu bewertet;
/// <see cref="Schluessel"/> erkennt denselben Anlass wieder.
/// </summary>
public class Pruefhinweis : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Art des Anlasses.</summary>
    public PruefhinweisArt Art { get; set; }
    /// <summary>Fachlicher Schlüssel des Anlasses, z. B. „Funktion:{Id}:Ende“; erkennt den Anlass im nächsten Lauf wieder.</summary>
    public required string Schluessel { get; set; }
    /// <summary>Hinweistext für die Oberfläche.</summary>
    public required string Text { get; set; }
    /// <summary>Tag, bis zu dem entschieden werden sollte.</summary>
    public DateOnly? FaelligAm { get; set; }
    /// <summary>Die Satzung oder Vorschrift verlangt eine Entscheidung; sonst nur ein Hinweis.</summary>
    public bool Pflicht { get; set; }

    /// <summary>Bearbeitungsstand.</summary>
    public PruefhinweisStatus Status { get; set; }
    /// <summary>Zeitpunkt, zu dem die Prüfung den Anlass zuerst gefunden hat.</summary>
    public DateTimeOffset ErkanntAm { get; set; }
    /// <summary>Zeitpunkt, zu dem der Hinweis entfallen ist oder zurückgestellt wurde.</summary>
    public DateTimeOffset? AbgeschlossenAm { get; set; }
}

/// <summary>Anlass, den eine Prüfung gefunden hat; noch nicht gespeichert.</summary>
/// <param name="Art">Art des Anlasses.</param>
/// <param name="Schluessel">Fachlicher Schlüssel zum Wiedererkennen.</param>
/// <param name="Text">Hinweistext.</param>
/// <param name="FaelligAm">Tag, bis zu dem entschieden werden sollte.</param>
/// <param name="Pflicht">Eine Entscheidung ist vorgeschrieben.</param>
public sealed record Pruefanlass(PruefhinweisArt Art, string Schluessel, string Text, DateOnly? FaelligAm, bool Pflicht);

/// <summary>Laufende Funktion eines Mitglieds mit den Angaben, die die Prüfung braucht.</summary>
/// <param name="ZuordnungId">Schlüssel der Funktionszuordnung.</param>
/// <param name="Bezeichnung">Bezeichnung der Funktion.</param>
/// <param name="Voraussetzungen">Nötige Qualifikationen als Codes (siehe <see cref="Mitglieder.Funktion.Voraussetzungen"/>).</param>
/// <param name="Art">Wahl oder Bestellung.</param>
/// <param name="GueltigAb">Beginn der Funktion.</param>
/// <param name="GueltigBis">Eingetragenes Ende.</param>
/// <param name="AmtszeitJahre">Amtszeit bei Wahl.</param>
public sealed record LaufendeFunktion(
    Guid ZuordnungId, string Bezeichnung, string? Voraussetzungen, FunktionArt Art, DateOnly GueltigAb, DateOnly? GueltigBis, int? AmtszeitJahre);

/// <summary>Prüfung der Funktionen eines Mitglieds für die Personalprüfung.</summary>
public static class Funktionsanlaesse
{
    /// <summary>
    /// Fehlende Voraussetzungen und auslaufende Amtszeiten. Gewählte Funktionen ohne eingetragenes Ende laufen nach
    /// <see cref="LaufendeFunktion.AmtszeitJahre"/> aus; Hinweis ab <paramref name="vorlaufMonate"/> vorher.
    /// </summary>
    /// <param name="funktionen">Funktionen des Mitglieds; nur die am Stichtag laufenden werden geprüft.</param>
    /// <param name="qualifikationen">Codes der gültigen Qualifikationen des Mitglieds.</param>
    /// <param name="stichtag">Tag der Prüfung.</param>
    /// <param name="vorlaufMonate">Vorlauf für den Hinweis auf das Ende.</param>
    /// <param name="namen">Bezeichnungen je Code für den Hinweistext.</param>
    /// <returns>Die gefundenen Anlässe.</returns>
    public static IReadOnlyList<Pruefanlass> Pruefen(
        IEnumerable<LaufendeFunktion> funktionen, IReadOnlySet<string> qualifikationen, DateOnly stichtag, int vorlaufMonate,
        IReadOnlyDictionary<string, string>? namen = null)
    {
        var anlaesse = new List<Pruefanlass>();
        foreach (var f in funktionen.Where(f => f.GueltigAb <= stichtag && (f.GueltigBis is null || f.GueltigBis >= stichtag)))
        {
            var fehlend = Abzeichenpruefung.Gruppen(f.Voraussetzungen)
                .Where(g => !g.Any(qualifikationen.Contains))
                .Select(g => string.Join(" oder ", g.Select(c => namen?.GetValueOrDefault(c) ?? c)))
                .ToList();
            if (fehlend.Count > 0)
            {
                anlaesse.Add(new(PruefhinweisArt.Funktion, $"Funktion:{f.ZuordnungId}:Voraussetzung",
                    $"{f.Bezeichnung}: Voraussetzung fehlt ({string.Join(", ", fehlend)}).", null, true));
            }

            var ende = f.GueltigBis ?? (f.Art == FunktionArt.Wahl && f.AmtszeitJahre is { } jahre ? f.GueltigAb.AddYears(jahre).AddDays(-1) : null);
            if (ende is { } e && e <= stichtag.AddMonths(vorlaufMonate))
            {
                anlaesse.Add(new(PruefhinweisArt.Funktion, $"Funktion:{f.ZuordnungId}:Ende",
                    f.Art == FunktionArt.Wahl
                        ? $"{f.Bezeichnung}: Amtszeit endet am {e:dd.MM.yyyy}, Wahl vorbereiten."
                        : $"{f.Bezeichnung}: Bestellung endet am {e:dd.MM.yyyy}, Nachfolge oder Verlängerung klären.",
                    e, true));
            }
        }

        return anlaesse;
    }
}
