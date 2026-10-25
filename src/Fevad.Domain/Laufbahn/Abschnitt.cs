using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

/// <summary>Mitgliedschaft mit aufgelöster Abteilungsart; Eingabe für die Laufbahnberechnungen.</summary>
/// <param name="Id">Schlüssel der Mitgliedschaft.</param>
/// <param name="Art">Art der Abteilung.</param>
/// <param name="Abteilung">Bezeichnung der Abteilung für Hinweistexte.</param>
/// <param name="Status">Status in diesem Zeitraum.</param>
/// <param name="Von">Erster Tag.</param>
/// <param name="Bis">Letzter Tag; leer, solange der Abschnitt läuft.</param>
/// <param name="Vordienstzeit">Zeit in einer anderen Wehr.</param>
/// <param name="Vordienstart">Art der Vordienstzeit.</param>
/// <param name="Befreiungsgrund">Grund bei Status befreit.</param>
/// <param name="Bestaetigt">Einsatzdienst in Alters- oder Musikabteilung vom Kommandanten bestätigt.</param>
/// <param name="ProbezeitBis">Eingetragenes Ende der Probezeit.</param>
/// <param name="AbteilungId">Schlüssel der Abteilung.</param>
public sealed record Abschnitt(
    Guid Id,
    AbteilungArt Art,
    string Abteilung,
    MitgliedStatus Status,
    DateOnly Von,
    DateOnly? Bis,
    bool Vordienstzeit = false,
    Vordienstart? Vordienstart = null,
    Befreiungsgrund? Befreiungsgrund = null,
    bool Bestaetigt = false,
    DateOnly? ProbezeitBis = null,
    Guid AbteilungId = default)
{
    /// <summary>Prüft, ob der Abschnitt an einem Tag besteht.</summary>
    /// <param name="stichtag">Zu prüfender Tag.</param>
    /// <returns>true, wenn der Stichtag zwischen Beginn und Ende liegt (jeweils einschließlich).</returns>
    public bool IstOffenAm(DateOnly stichtag) => Von <= stichtag && (Bis is null || stichtag <= Bis);

    /// <summary>Erzeugt den Abschnitt aus einer Mitgliedschaft.</summary>
    /// <param name="m">Mitgliedschaft mit geladener Abteilung.</param>
    /// <returns>Der Abschnitt.</returns>
    /// <exception cref="InvalidOperationException">Die Abteilung ist nicht geladen.</exception>
    public static Abschnitt Aus(Mitgliedschaft m) => new(
        m.Id,
        m.Abteilung?.Art ?? throw new InvalidOperationException("Abteilung nicht geladen."),
        m.Abteilung.Bezeichnung,
        m.Status,
        m.GueltigAb,
        m.GueltigBis,
        m.Vordienstzeit,
        m.Vordienstart,
        m.Befreiungsgrund,
        m.EinsatzdienstBestaetigt,
        m.ProbezeitBis,
        m.AbteilungId);
}
