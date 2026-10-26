using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Ausbildung;

/// <summary>Stand eines Lehrgangswunsches (V54).</summary>
public enum WunschStatus
{
    /// <summary>Vom Mitglied eingetragen, noch nicht entschieden.</summary>
    [Display(Name = "gewünscht")] Gewuenscht,
    /// <summary>Vorgemerkt, aber in diesem Jahr ohne Platz.</summary>
    [Display(Name = "Warteliste")] Warteliste,
    /// <summary>Beim Kreisbrandmeister oder im Bildungsportal angemeldet.</summary>
    [Display(Name = "angemeldet")] Angemeldet,
    /// <summary>Platz bestätigt.</summary>
    [Display(Name = "bestätigt")] Bestaetigt,
    /// <summary>Vom Kommandanten nicht befürwortet.</summary>
    [Display(Name = "nicht befürwortet")] Abgelehnt,
    /// <summary>Vom Mitglied zurückgezogen.</summary>
    [Display(Name = "zurückgezogen")] Zurueckgezogen,
}

/// <summary>
/// Wunsch eines Mitglieds, einen Lehrgang zu besuchen (V54). Der Kommandant setzt Priorität und Stand und meldet den Bedarf im Herbst an den
/// Kreisbrandmeister (VwV Feuerwehrausbildung). Mit der Anmeldung entsteht die <see cref="Lehrgangsteilnahme"/>, die den weiteren Weg trägt.
/// </summary>
public class Lehrgangswunsch : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Schlüssel des Lehrgangs im Qualifikationskatalog.</summary>
    public Guid QualifikationId { get; set; }
    /// <summary>Der gewünschte Lehrgang.</summary>
    public Qualifikation? Qualifikation { get; set; }

    /// <summary>Jahr, in dem der Lehrgang besucht werden soll.</summary>
    public int Jahr { get; set; }

    /// <summary>Tag, an dem der Wunsch eingetragen wurde.</summary>
    public DateOnly EingetragenAm { get; set; }

    /// <summary>Begründung oder Hinweis des Mitglieds, z. B. „ab Herbst wieder im Ort“.</summary>
    public string? Begruendung { get; set; }

    /// <summary>Stand.</summary>
    public WunschStatus Status { get; set; }

    /// <summary>Priorität für die Meldung an den Kreisbrandmeister; 1 ist die höchste.</summary>
    public int? Prioritaet { get; set; }

    /// <summary>Tag, an dem der Wunsch mit der Herbstmeldung an den Kreisbrandmeister ging.</summary>
    public DateOnly? GemeldetAm { get; set; }

    /// <summary>Bemerkung des Kommandanten.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Die Lehrgangsteilnahme, die mit der Anmeldung entstanden ist.</summary>
    public Guid? TeilnahmeId { get; set; }
    /// <summary>Navigationsverweis auf die Teilnahme.</summary>
    public Lehrgangsteilnahme? Teilnahme { get; set; }

    /// <summary>Der Wunsch ist noch nicht erledigt: gewünscht, auf der Warteliste oder angemeldet.</summary>
    public bool Offen => Status is WunschStatus.Gewuenscht or WunschStatus.Warteliste or WunschStatus.Angemeldet;

    /// <summary>Das Mitglied darf den Wunsch selbst zurückziehen, solange er noch nicht angemeldet ist.</summary>
    public bool Zurueckziehbar => Status is WunschStatus.Gewuenscht or WunschStatus.Warteliste;

    /// <summary>
    /// Prüft die Voraussetzungen des Lehrgangs (Mindestalter, nötige Lehrgänge wie Truppführer vor Gruppenführer) zum Stichtag.
    /// Eine fehlende Voraussetzung sperrt den Wunsch nicht, denn ein Vorlehrgang kann bis zum Lehrgangsjahr noch abgeschlossen werden.
    /// </summary>
    /// <param name="lehrgang">Der Lehrgang mit seinen Voraussetzungen.</param>
    /// <param name="stichtag">Tag der Prüfung, in der Regel heute.</param>
    /// <param name="geburtsdatum">Geburtsdatum des Mitglieds.</param>
    /// <param name="vorhandene">Qualifikationsnachweise mit geladener Qualifikation.</param>
    /// <param name="namen">Bezeichnungen je Code.</param>
    /// <returns>Die geprüften Bedingungen.</returns>
    public static IReadOnlyList<Voraussetzung> Pruefen(Qualifikation lehrgang, DateOnly stichtag, DateOnly geburtsdatum,
        IEnumerable<QualifikationNachweis> vorhandene, IReadOnlyDictionary<string, string>? namen = null) =>
        Abzeichenpruefung.Pruefen(lehrgang, stichtag, geburtsdatum, vorhandene, null, namen);

    /// <summary>Der Lehrgang ist schon mit gültigem Nachweis vorhanden.</summary>
    /// <param name="lehrgang">Der Lehrgang.</param>
    /// <param name="stichtag">Tag der Prüfung.</param>
    /// <param name="vorhandene">Qualifikationsnachweise mit geladener Qualifikation.</param>
    /// <returns><c>true</c>, wenn ein gültiger Nachweis vorliegt.</returns>
    public static bool Vorhanden(Qualifikation lehrgang, DateOnly stichtag, IEnumerable<QualifikationNachweis> vorhandene) =>
        Abzeichenpruefung.Gueltig(vorhandene, lehrgang.Code.ToUpperInvariant(), stichtag);
}
