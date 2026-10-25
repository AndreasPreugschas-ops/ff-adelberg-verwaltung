using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

/// <summary>
/// Ein Weg zu einem Dienstgrad nach VwV Feuerwehrbekleidung (z. B. OFM: Weg A 10 Jahre FM ohne Lehrgang,
/// Weg B 3 Jahre FM mit Truppmann Teil 2 und Maschinist, Atemschutz oder Sprechfunk). Ein Dienstgrad kann
/// mehrere Wege haben; einer genügt.
/// </summary>
public class Befoerderungsweg : Entitaet
{
    /// <summary>Schlüssel des Dienstgrads, zu dem der Weg führt.</summary>
    public Guid DienstgradId { get; set; }
    /// <summary>Der Dienstgrad, zu dem der Weg führt.</summary>
    public Dienstgrad? Dienstgrad { get; set; }

    /// <summary>Kurzname des Wegs, z. B. "Weg B".</summary>
    public string? Bezeichnung { get; set; }

    /// <summary>Mindestjahre im bisherigen Dienstgrad.</summary>
    public int MindestjahreImDienstgrad { get; set; }

    /// <summary>Codes der Qualifikationen, die alle vorliegen müssen.</summary>
    public List<string> AlleQualifikationen { get; set; } = [];

    /// <summary>Codes, von denen mindestens eine vorliegen muss; leer heißt keine Bedingung.</summary>
    public List<string> EineDerQualifikationen { get; set; } = [];

    /// <summary>Der Weg setzt eine bestandene Probezeit voraus.</summary>
    public bool ProbezeitBeendet { get; set; }

    /// <summary>Darf aus jedem niedrigeren Dienstgrad erreicht werden (LM mit Gruppenführer, BM mit Zugführer).</summary>
    public bool UeberspringenErlaubt { get; set; }

    /// <summary>Kein automatischer Vorschlag, nur von Hand (z. B. LHBM).</summary>
    public bool NurVonHand { get; set; }

    /// <summary>Weitere Bedingung zum Lesen, die das Programm nicht prüft.</summary>
    public string? Hinweis { get; set; }
}

/// <summary>Dienstgrad aus dem Katalog mit den Angaben, die die Beförderungsprüfung braucht.</summary>
/// <param name="Id">Schlüssel des Dienstgrads.</param>
/// <param name="Code">Code, z. B. „OFM“.</param>
/// <param name="Bezeichnung">Bezeichnung, z. B. „Oberfeuerwehrmann“.</param>
/// <param name="Sortierung">Rangfolge; höhere Werte sind höhere Dienstgrade.</param>
/// <param name="MindestwartezeitMonate">Mindestzeit bis zur nächsten Beförderung.</param>
public sealed record DienstgradStufe(Guid Id, string Code, string Bezeichnung, int Sortierung, int MindestwartezeitMonate);

/// <summary>Was die Prüfung über ein Mitglied wissen muss.</summary>
/// <param name="AktuellerDienstgrad">Leer, wenn noch kein Dienstgrad erfasst ist.</param>
/// <param name="DienstgradSeit">Datum der letzten Beförderung, sonst Eintritt in die Einsatzabteilung.</param>
/// <param name="ProbezeitBeendet">Die Probezeit ist bestanden.</param>
/// <param name="Qualifikationen">Codes der gültigen Qualifikationen.</param>
public sealed record Befoerderungsstand(
    DienstgradStufe? AktuellerDienstgrad,
    DateOnly DienstgradSeit,
    bool ProbezeitBeendet,
    IReadOnlySet<string> Qualifikationen);

/// <summary>Ergebnis der Beförderungsprüfung: welcher Dienstgrad über welchen Weg ab wann möglich ist.</summary>
/// <param name="Dienstgrad">Der erreichbare Dienstgrad.</param>
/// <param name="Weg">Der erfüllte Beförderungsweg.</param>
/// <param name="MoeglichAb">Frühester Tag der Beförderung.</param>
public sealed record Befoerderungsvorschlag(DienstgradStufe Dienstgrad, Befoerderungsweg Weg, DateOnly MoeglichAb);

/// <summary>Prüfung möglicher Beförderungen; Beförderungen werden nur vorgeschlagen, durchgeführt von der Personalverwaltung.</summary>
public static class Befoerderungspruefung
{
    /// <summary>
    /// Höchster Dienstgrad, der bis <paramref name="stichtag"/> erreichbar ist. Regel: kein Dienstgrad wird übersprungen
    /// (außer bei Wegen mit <see cref="Befoerderungsweg.UeberspringenErlaubt"/>), und zwischen zwei Beförderungen liegt die
    /// Mindestwartezeit des bisherigen Dienstgrads.
    /// </summary>
    /// <param name="stand">Dienstgrad, Probezeit und Qualifikationen des Mitglieds.</param>
    /// <param name="dienstgrade">Alle Dienstgrade mit Rangfolge.</param>
    /// <param name="wege">Alle Beförderungswege.</param>
    /// <param name="stichtag">Tag, bis zu dem die Beförderung möglich sein muss.</param>
    /// <returns>Der Vorschlag oder null, wenn keine Beförderung möglich ist.</returns>
    public static Befoerderungsvorschlag? Pruefen(
        Befoerderungsstand stand, IReadOnlyList<DienstgradStufe> dienstgrade, IReadOnlyList<Befoerderungsweg> wege, DateOnly stichtag)
    {
        var aktuell = stand.AktuellerDienstgrad?.Sortierung ?? int.MinValue;
        var hoehere = dienstgrade.Where(d => d.Sortierung > aktuell).OrderBy(d => d.Sortierung).ToList();
        if (hoehere.Count == 0)
        {
            return null;
        }

        var wartezeitEnde = stand.AktuellerDienstgrad is null
            ? stand.DienstgradSeit
            : stand.DienstgradSeit.AddMonths(stand.AktuellerDienstgrad.MindestwartezeitMonate);

        Befoerderungsvorschlag? bester = null;
        foreach (var ziel in hoehere)
        {
            var istNaechster = ziel == hoehere[0];
            foreach (var weg in wege.Where(w => w.DienstgradId == ziel.Id && !w.NurVonHand))
            {
                if (!istNaechster && !weg.UeberspringenErlaubt)
                {
                    continue;
                }

                if (MoeglichAb(stand, weg, wartezeitEnde) is { } ab && ab <= stichtag
                    && (bester is null || ziel.Sortierung > bester.Dienstgrad.Sortierung || (ziel == bester.Dienstgrad && ab < bester.MoeglichAb)))
                {
                    bester = new Befoerderungsvorschlag(ziel, weg, ab);
                }
            }
        }

        return bester;
    }

    /// <summary>Frühester Tag, an dem der Weg erfüllt ist, oder null, wenn eine Qualifikation oder die Probezeit fehlt.</summary>
    /// <param name="stand">Stand des Mitglieds.</param>
    /// <param name="weg">Zu prüfender Weg.</param>
    /// <param name="wartezeitEnde">Ende der Mindestwartezeit im bisherigen Dienstgrad.</param>
    /// <returns>Der früheste Tag oder null.</returns>
    public static DateOnly? MoeglichAb(Befoerderungsstand stand, Befoerderungsweg weg, DateOnly wartezeitEnde)
    {
        if (weg.ProbezeitBeendet && !stand.ProbezeitBeendet)
        {
            return null;
        }

        if (!weg.AlleQualifikationen.All(stand.Qualifikationen.Contains))
        {
            return null;
        }

        if (weg.EineDerQualifikationen.Count > 0 && !weg.EineDerQualifikationen.Any(stand.Qualifikationen.Contains))
        {
            return null;
        }

        var mindestzeit = stand.DienstgradSeit.AddYears(weg.MindestjahreImDienstgrad);
        return mindestzeit > wartezeitEnde ? mindestzeit : wartezeitEnde;
    }
}
