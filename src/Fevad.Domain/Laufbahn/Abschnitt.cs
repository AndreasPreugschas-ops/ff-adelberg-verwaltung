using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

/// <summary>Mitgliedschaft mit aufgelöstem Abteilungscode; Eingabe für die Laufbahnberechnungen.</summary>
public sealed record Abschnitt(
    Guid Id,
    string AbteilungCode,
    string Abteilung,
    MitgliedStatus Status,
    DateOnly Von,
    DateOnly? Bis,
    bool Vordienstzeit = false,
    Vordienstart? Vordienstart = null,
    Befreiungsgrund? Befreiungsgrund = null,
    bool Bestaetigt = false)
{
    public bool IstOffenAm(DateOnly stichtag) => Von <= stichtag && (Bis is null || stichtag <= Bis);

    public static Abschnitt Aus(Mitgliedschaft m) => new(
        m.Id,
        m.Abteilung?.Code ?? throw new InvalidOperationException("Abteilung nicht geladen."),
        m.Abteilung.Bezeichnung,
        m.Status,
        m.GueltigAb,
        m.GueltigBis,
        m.Vordienstzeit,
        m.Vordienstart,
        m.Befreiungsgrund,
        m.EinsatzdienstBestaetigt);
}
