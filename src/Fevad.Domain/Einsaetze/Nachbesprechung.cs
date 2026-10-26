using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Einsaetze;

/// <summary>Art einer Maßnahme aus der Nachbesprechung (V17).</summary>
public enum MassnahmeArt
{
    /// <summary>Sonstige Aufgabe, z. B. Absprache mit der Nachbarwehr.</summary>
    [Display(Name = "Aufgabe")] Aufgabe,
    /// <summary>Übungsbedarf; erscheint nach der Freigabe im Ausbildungsplan als Vorschlag für die Übungsplanung.</summary>
    [Display(Name = "Übungsbedarf")] Uebungsbedarf,
    /// <summary>Mangel an Fahrzeug oder Gerät; wird mit der Freigabe zur Mängelmeldung an die Gerätewarte.</summary>
    [Display(Name = "Mangel an Ausrüstung")] Mangel,
    /// <summary>Beschaffung oder Ersatz.</summary>
    [Display(Name = "Beschaffung")] Beschaffung,
}

/// <summary>Bearbeitungsstand einer Maßnahme.</summary>
public enum MassnahmeStatus
{
    /// <summary>Aus der Nachbesprechung vorgeschlagen; Kommandant oder Stellvertreter prüfen und geben frei (Andi zu V17).</summary>
    [Display(Name = "vorgeschlagen")] Vorgeschlagen,
    /// <summary>Freigegeben; der Verantwortliche setzt um.</summary>
    [Display(Name = "freigegeben")] Freigegeben,
    /// <summary>Umgesetzt.</summary>
    [Display(Name = "erledigt")] Erledigt,
    /// <summary>Vom Kommando nicht übernommen.</summary>
    [Display(Name = "verworfen")] Verworfen,
}

/// <summary>
/// Einsatznachbesprechung (V17, FwDV 100 Kontrolle und Nachbereitung): was lief gut, was nicht, und die Maßnahmen daraus.
/// Gehört zum Einsatzbericht und wird mit ihm aufbewahrt und gelöscht.
/// </summary>
public class Nachbesprechung : Entitaet
{
    /// <summary>Kennung des Einsatzes.</summary>
    public Guid EinsatzId { get; set; }
    /// <summary>Der Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Tag der Nachbesprechung.</summary>
    public DateOnly Datum { get; set; }
    /// <summary>Teilnehmer als Funktionen oder Einheiten, z. B. „Besatzung HLF 10, Kommandant“.</summary>
    public string? Teilnehmer { get; set; }
    /// <summary>Was gut lief.</summary>
    public string? Gut { get; set; }
    /// <summary>Was besser werden muss.</summary>
    public string? Verbesserung { get; set; }

    /// <summary>Maßnahmen aus der Nachbesprechung.</summary>
    public List<Massnahme> Massnahmen { get; set; } = [];
}

/// <summary>
/// Maßnahme mit Verantwortlichem und Termin (V17). Vorschläge prüft und gibt das Kommando frei; ein freigegebener Mangel geht als Mängelmeldung
/// an die Gerätewarte, Übungsbedarf als Vorschlag in den Ausbildungsplan.
/// </summary>
public class Massnahme : Entitaet
{
    /// <summary>Kennung der Nachbesprechung, aus der die Maßnahme stammt.</summary>
    public Guid NachbesprechungId { get; set; }
    /// <summary>Die Nachbesprechung.</summary>
    public Nachbesprechung? Nachbesprechung { get; set; }

    /// <summary>Was zu tun ist.</summary>
    public required string Titel { get; set; }
    /// <summary>Art der Maßnahme.</summary>
    public MassnahmeArt Art { get; set; }
    /// <summary>Bearbeitungsstand.</summary>
    public MassnahmeStatus Status { get; set; }

    /// <summary>Verantwortliches Mitglied.</summary>
    public Guid? VerantwortlichId { get; set; }
    /// <summary>Das verantwortliche Mitglied.</summary>
    public Mitglied? Verantwortlich { get; set; }
    /// <summary>Termin.</summary>
    public DateOnly? ErledigenBis { get; set; }

    /// <summary>Betroffenes Fahrzeug bei einem Mangel.</summary>
    public Guid? FahrzeugId { get; set; }
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }
    /// <summary>Betroffenes Gerät bei einem Mangel.</summary>
    public Guid? GeraetId { get; set; }
    /// <summary>Das Gerät.</summary>
    public Geraet? Geraet { get; set; }
    /// <summary>Die aus der Freigabe entstandene Mängelmeldung.</summary>
    public Guid? MangelId { get; set; }

    /// <summary>Zeitpunkt der Freigabe (UTC).</summary>
    public DateTimeOffset? FreigegebenAm { get; set; }
    /// <summary>Konto, das freigegeben oder verworfen hat.</summary>
    public Guid? FreigegebenVon { get; set; }
    /// <summary>Tag der Erledigung.</summary>
    public DateOnly? ErledigtAm { get; set; }
    /// <summary>Anmerkung, z. B. Grund des Verwerfens.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Die Maßnahme ist noch umzusetzen oder zu entscheiden.</summary>
    public bool Offen => Status is MassnahmeStatus.Vorgeschlagen or MassnahmeStatus.Freigegeben;

    /// <summary>Ampel einer freigegebenen Maßnahme: rot nach dem Termin, gelb in der Warnzeit davor.</summary>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="warnTage">Tage vor dem Termin, ab denen gelb gilt (Parameter).</param>
    /// <returns>Die Ampel; <c>null</c> ohne Termin oder wenn die Maßnahme nicht freigegeben ist.</returns>
    public Fristampel? Ampel(DateOnly heute, int warnTage) =>
        Status != MassnahmeStatus.Freigegeben || ErledigenBis is not { } bis ? null
        : heute > bis ? Fristampel.Rot
        : heute.AddDays(warnTage) >= bis ? Fristampel.Gelb
        : Fristampel.Gruen;

    /// <summary>Gibt die Maßnahme frei (Kommandant oder Stellvertreter).</summary>
    /// <param name="benutzerId">Konto, das freigibt.</param>
    /// <param name="zeitpunkt">Zeitpunkt.</param>
    /// <exception cref="InvalidOperationException">Die Maßnahme ist nicht vorgeschlagen.</exception>
    public void Freigeben(Guid benutzerId, DateTimeOffset zeitpunkt)
    {
        if (Status != MassnahmeStatus.Vorgeschlagen)
        {
            throw new InvalidOperationException("Nur vorgeschlagene Maßnahmen lassen sich freigeben.");
        }

        Status = MassnahmeStatus.Freigegeben;
        FreigegebenAm = zeitpunkt;
        FreigegebenVon = benutzerId;
    }

    /// <summary>Verwirft einen Vorschlag.</summary>
    /// <param name="benutzerId">Konto, das verwirft.</param>
    /// <param name="grund">Grund, optional.</param>
    /// <exception cref="InvalidOperationException">Die Maßnahme ist nicht vorgeschlagen.</exception>
    public void Verwerfen(Guid benutzerId, string? grund)
    {
        if (Status != MassnahmeStatus.Vorgeschlagen)
        {
            throw new InvalidOperationException("Nur vorgeschlagene Maßnahmen lassen sich verwerfen.");
        }

        Status = MassnahmeStatus.Verworfen;
        FreigegebenVon = benutzerId;
        Bemerkung = string.IsNullOrWhiteSpace(grund) ? Bemerkung : grund.Trim();
    }

    /// <summary>Markiert eine freigegebene Maßnahme als erledigt.</summary>
    /// <param name="tag">Tag der Erledigung.</param>
    /// <exception cref="InvalidOperationException">Die Maßnahme ist nicht freigegeben.</exception>
    public void Erledigen(DateOnly tag)
    {
        if (Status != MassnahmeStatus.Freigegeben)
        {
            throw new InvalidOperationException("Nur freigegebene Maßnahmen lassen sich erledigen.");
        }

        Status = MassnahmeStatus.Erledigt;
        ErledigtAm = tag;
    }
}
