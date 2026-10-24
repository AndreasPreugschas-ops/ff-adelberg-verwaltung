using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Ausbildung;

public enum LehrgangStand
{
    [Display(Name = "angemeldet")] Angemeldet,
    [Display(Name = "Platz zugewiesen")] Zugewiesen,
    [Display(Name = "läuft gerade")] Laeuft,
    [Display(Name = "vorbei, Ergebnis fehlt")] ErgebnisFehlt,
    [Display(Name = "bestanden")] Bestanden,
    [Display(Name = "nicht bestanden")] NichtBestanden,
}

/// <summary>
/// Weg zu einem Lehrgang: angemeldet, Platz zugewiesen, Zeitraum, Ergebnis. Erst mit „abgeschlossen“ steht das Ergebnis fest;
/// abgeschlossen ohne „bestanden“ heißt nicht bestanden. Bestanden und abgeschlossen ergibt den <see cref="QualifikationNachweis"/>
/// zum letzten Lehrgangstag. Abzeichen laufen nicht hierüber, sie werden am Abnahmetag direkt eingetragen.
/// </summary>
public class Lehrgangsteilnahme : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid QualifikationId { get; set; }
    public Qualifikation? Qualifikation { get; set; }

    public DateOnly? AngemeldetAm { get; set; }

    /// <summary>Tag, an dem der Lehrgangsplatz zugewiesen wurde (Zusage von Kreis oder Landesfeuerwehrschule).</summary>
    public DateOnly? ZugewiesenAm { get; set; }

    public DateOnly? Von { get; set; }
    public DateOnly? Bis { get; set; }

    public bool Bestanden { get; set; }
    public bool Abgeschlossen { get; set; }

    public string? Lehrgangsort { get; set; }
    public string? Bemerkung { get; set; }

    /// <summary>Der daraus entstandene Nachweis; fällt weg, wenn das Ergebnis zurückgenommen wird.</summary>
    public Guid? NachweisId { get; set; }
    public QualifikationNachweis? Nachweis { get; set; }

    public LehrgangStand Stand(DateOnly heute) =>
        Abgeschlossen ? (Bestanden ? LehrgangStand.Bestanden : LehrgangStand.NichtBestanden)
        : Bis is { } bis && bis < heute ? LehrgangStand.ErgebnisFehlt
        : Von is { } von && von <= heute ? LehrgangStand.Laeuft
        : ZugewiesenAm is not null ? LehrgangStand.Zugewiesen
        : LehrgangStand.Angemeldet;

    /// <summary>Plausibel: zugewiesen nicht vor angemeldet, bis nicht vor von, abgeschlossen erst mit Ende des Lehrgangs.</summary>
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
