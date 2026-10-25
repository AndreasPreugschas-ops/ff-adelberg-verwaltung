using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Ausbildung;

/// <summary>Stand einer Lehrgangsteilnahme, abgeleitet aus Daten und Ergebnis.</summary>
public enum LehrgangStand
{
    /// <summary>Angemeldet, noch ohne Platz.</summary>
    [Display(Name = "angemeldet")] Angemeldet,
    /// <summary>Platz zugewiesen, Lehrgang noch nicht begonnen.</summary>
    [Display(Name = "Platz zugewiesen")] Zugewiesen,
    /// <summary>Lehrgang läuft gerade.</summary>
    [Display(Name = "läuft gerade")] Laeuft,
    /// <summary>Lehrgang vorbei, Ergebnis noch nicht eingetragen.</summary>
    [Display(Name = "vorbei, Ergebnis fehlt")] ErgebnisFehlt,
    /// <summary>Abgeschlossen und bestanden.</summary>
    [Display(Name = "bestanden")] Bestanden,
    /// <summary>Abgeschlossen, aber nicht bestanden.</summary>
    [Display(Name = "nicht bestanden")] NichtBestanden,
}

/// <summary>
/// Weg zu einem Lehrgang: angemeldet, Platz zugewiesen, Zeitraum, Ergebnis. Erst mit „abgeschlossen“ steht das Ergebnis fest;
/// abgeschlossen ohne „bestanden“ heißt nicht bestanden. Bestanden und abgeschlossen ergibt den <see cref="QualifikationNachweis"/>
/// zum letzten Lehrgangstag. Abzeichen laufen nicht hierüber, sie werden am Abnahmetag direkt eingetragen.
/// </summary>
public class Lehrgangsteilnahme : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das teilnehmende Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Schlüssel des Lehrgangs im Qualifikationskatalog.</summary>
    public Guid QualifikationId { get; set; }
    /// <summary>Der Lehrgang.</summary>
    public Qualifikation? Qualifikation { get; set; }

    /// <summary>Tag der Anmeldung zum Lehrgang.</summary>
    public DateOnly? AngemeldetAm { get; set; }

    /// <summary>Tag, an dem der Lehrgangsplatz zugewiesen wurde (Zusage von Kreis oder Landesfeuerwehrschule).</summary>
    public DateOnly? ZugewiesenAm { get; set; }

    /// <summary>Erster Lehrgangstag.</summary>
    public DateOnly? Von { get; set; }
    /// <summary>Letzter Lehrgangstag; wird Erwerbsdatum des Nachweises.</summary>
    public DateOnly? Bis { get; set; }

    /// <summary>Der Lehrgang wurde bestanden; zählt erst mit <see cref="Abgeschlossen"/>.</summary>
    public bool Bestanden { get; set; }
    /// <summary>Das Ergebnis steht fest.</summary>
    public bool Abgeschlossen { get; set; }

    /// <summary>Ort des Lehrgangs, z. B. Landesfeuerwehrschule Bruchsal.</summary>
    public string? Lehrgangsort { get; set; }
    /// <summary>Freitext.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Der daraus entstandene Nachweis; fällt weg, wenn das Ergebnis zurückgenommen wird.</summary>
    public Guid? NachweisId { get; set; }
    /// <summary>Der entstandene Qualifikationsnachweis.</summary>
    public QualifikationNachweis? Nachweis { get; set; }

    /// <summary>Leitet den Stand aus Ergebnis und Daten ab.</summary>
    /// <param name="heute">Heutiger Tag.</param>
    /// <returns>Den Stand der Teilnahme.</returns>
    public LehrgangStand Stand(DateOnly heute) =>
        Abgeschlossen ? (Bestanden ? LehrgangStand.Bestanden : LehrgangStand.NichtBestanden)
        : Bis is { } bis && bis < heute ? LehrgangStand.ErgebnisFehlt
        : Von is { } von && von <= heute ? LehrgangStand.Laeuft
        : ZugewiesenAm is not null ? LehrgangStand.Zugewiesen
        : LehrgangStand.Angemeldet;

    /// <summary>Plausibel: zugewiesen nicht vor angemeldet, bis nicht vor von, abgeschlossen erst mit Ende des Lehrgangs.</summary>
    /// <param name="angemeldet">Tag der Anmeldung.</param>
    /// <param name="zugewiesen">Tag der Zuweisung.</param>
    /// <param name="von">Erster Lehrgangstag.</param>
    /// <param name="bis">Letzter Lehrgangstag.</param>
    /// <param name="abgeschlossen">Das Ergebnis soll festgeschrieben werden.</param>
    /// <returns>Fehlermeldungen; leer, wenn die Angaben stimmig sind.</returns>
    public static IReadOnlyList<string> Fehler(DateOnly? angemeldet, DateOnly? zugewiesen, DateOnly? von, DateOnly? bis, bool abgeschlossen)
    {
        var fehler = new List<string>();
        if (angemeldet is null && zugewiesen is null && von is null)
        {
            fehler.Add("Mindestens Anmeldung, Zuweisung oder Beginn angeben.");
        }

        if (angemeldet is not null && zugewiesen < angemeldet)
        {
            fehler.Add("Zugewiesen kann nicht vor der Anmeldung liegen.");
        }

        if (von is not null && bis < von)
        {
            fehler.Add("Das Ende liegt vor dem Beginn.");
        }

        if (abgeschlossen && (bis ?? von) is null)
        {
            fehler.Add("Zum Abschließen den Zeitraum angeben, mindestens den letzten Tag.");
        }

        return fehler;
    }
}
