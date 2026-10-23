using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

public enum PruefhinweisArt
{
    [Display(Name = "Beförderung")] Befoerderung,
    [Display(Name = "Ehrung")] Ehrung,
    [Display(Name = "Mitgliedschaft")] Mitgliedschaft,
    [Display(Name = "Funktion")] Funktion,
}

public enum PruefhinweisStatus
{
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
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public PruefhinweisArt Art { get; set; }
    public required string Schluessel { get; set; }
    public required string Text { get; set; }
    public DateOnly? FaelligAm { get; set; }
    public bool Pflicht { get; set; }

    public PruefhinweisStatus Status { get; set; }
    public DateTimeOffset ErkanntAm { get; set; }
    public DateTimeOffset? AbgeschlossenAm { get; set; }
}

/// <summary>Anlass, den eine Prüfung gefunden hat; noch nicht gespeichert.</summary>
public sealed record Pruefanlass(PruefhinweisArt Art, string Schluessel, string Text, DateOnly? FaelligAm, bool Pflicht);

/// <summary>Laufende Funktion eines Mitglieds mit den Angaben, die die Prüfung braucht.</summary>
public sealed record LaufendeFunktion(
    Guid ZuordnungId, string Bezeichnung, string? Voraussetzungen, FunktionArt Art, DateOnly GueltigAb, DateOnly? GueltigBis, int? AmtszeitJahre);

public static class Funktionsanlaesse
{
    /// <summary>
    /// Fehlende Voraussetzungen und auslaufende Amtszeiten. Gewählte Funktionen ohne eingetragenes Ende laufen nach
    /// <see cref="LaufendeFunktion.AmtszeitJahre"/> aus; Hinweis ab <paramref name="vorlaufMonate"/> vorher.
    /// </summary>
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
