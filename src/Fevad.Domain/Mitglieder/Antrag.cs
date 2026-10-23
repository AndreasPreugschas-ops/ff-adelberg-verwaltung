using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;

namespace Fevad.Domain.Mitglieder;

public enum AntragArt
{
    [Display(Name = "Aufnahme in die Jugendfeuerwehr")] JugendEintritt,
    [Display(Name = "Übertritt in die Einsatzabteilung mit 17")] UebertrittEinsatz,
    [Display(Name = "Aufnahme in die Einsatzabteilung")] EinsatzEintritt,
    [Display(Name = "Erklärung bei Volljährigkeit")] Volljaehrigkeit,
}

public enum AntragStatus
{
    [Display(Name = "Entwurf")] Entwurf,
    [Display(Name = "unterschrieben eingegangen")] Eingegangen,
    [Display(Name = "angenommen")] Angenommen,
    [Display(Name = "abgelehnt")] Abgelehnt,
    [Display(Name = "zurückgezogen")] Zurueckgezogen,
}

/// <summary>Warum jemand eine frühere Feuerwehr verlassen hat. Bewusst ohne Gesundheit und Ausschluss (Datenminimierung).</summary>
public enum Ausscheidensgrund
{
    [Display(Name = "Umzug")] Umzug,
    [Display(Name = "Wechsel in eine andere Feuerwehr")] Wechsel,
    [Display(Name = "Ende der Jugendfeuerwehr ohne Übertritt")] JugendfeuerwehrEnde,
    [Display(Name = "berufliche Gründe")] Beruf,
    [Display(Name = "persönliche Gründe")] Persoenlich,
    [Display(Name = "sonstiger Grund")] Sonstiges,
}

/// <summary>
/// Antrag auf Aufnahme, Übertritt oder die Erklärung bei Volljährigkeit. Wird in FEVAD ausgefüllt und als PDF zum
/// Unterschreiben gedruckt; rechtsverbindlich ist das unterschriebene Papier. Bei Übertritt und Volljährigkeit gehört
/// der Antrag zu einem Mitglied, bei Aufnahmen entsteht das Mitglied erst mit der Annahme.
/// </summary>
public class Antrag : Entitaet
{
    public AntragArt Art { get; set; }
    public AntragStatus Status { get; set; }

    /// <summary>Abteilung, in die aufgenommen oder übergetreten wird.</summary>
    public Guid AbteilungId { get; set; }
    public Abteilung? Abteilung { get; set; }

    public Guid? MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public required string Vorname { get; set; }
    public required string Nachname { get; set; }
    public DateOnly Geburtsdatum { get; set; }
    public string? Geburtsort { get; set; }
    public Geschlecht? Geschlecht { get; set; }
    public string? Strasse { get; set; }
    public string? Plz { get; set; }
    public string? Ort { get; set; }
    public string? Telefon { get; set; }
    public string? Mobil { get; set; }
    public string? Email { get; set; }

    /// <summary>Gewünschter Beginn der Mitgliedschaft bzw. des Übertritts.</summary>
    public DateOnly? GewuenschtAb { get; set; }

    // Vorgeschichte (Aufnahme in die Einsatzabteilung ohne Übertritt aus der eigenen Jugendfeuerwehr).
    public bool FrueherFeuerwehr { get; set; }
    public string? FrueherFeuerwehrName { get; set; }
    public DateOnly? FrueherVon { get; set; }
    public DateOnly? FrueherBis { get; set; }
    public Ausscheidensgrund? FrueherAustrittsgrund { get; set; }
    public string? FrueherLehrgaenge { get; set; }
    public string? FrueherDienstgrad { get; set; }
    public bool AndereFeuerwehrAktiv { get; set; }
    public string? AndereFeuerwehrName { get; set; }

    /// <summary>Nur ja oder nein, keine Gründe.</summary>
    public bool FrueherAusgeschlossen { get; set; }

    /// <summary>
    /// Einwilligung in Fotos für die Öffentlichkeitsarbeit (Art. 7 DSGVO): freiwillig, nicht vorbelegt, jederzeit widerrufbar.
    /// Leer heißt keine Angabe. Wird bei der Annahme mit Datum ins Mitglied übernommen.
    /// </summary>
    public bool? FotoEinwilligung { get; set; }

    public DateOnly? EingegangenAm { get; set; }
    public DateOnly? EntschiedenAm { get; set; }
    public string? Bemerkung { get; set; }

    public List<AntragElternteil> Erziehungsberechtigte { get; set; } = [];

    public string Anzeigename => $"{Nachname}, {Vorname}";

    public bool Abgeschlossen => Status is AntragStatus.Angenommen or AntragStatus.Abgelehnt or AntragStatus.Zurueckgezogen;
}

/// <summary>Erziehungsberechtigte(r) auf einem Antrag; wird bei der Annahme als <see cref="Erziehungsberechtigter"/> übernommen.</summary>
public class AntragElternteil : Entitaet
{
    public Guid AntragId { get; set; }
    public Antrag? Antrag { get; set; }

    public int Nummer { get; set; }
    public Sorgeverhaeltnis? Verhaeltnis { get; set; }
    public required string Vorname { get; set; }
    public required string Nachname { get; set; }
    public string? Strasse { get; set; }
    public string? Plz { get; set; }
    public string? Ort { get; set; }
    public string? Telefon { get; set; }
    public string? Mobil { get; set; }
    public string? Email { get; set; }
}

/// <summary>Was ein Antragsformular braucht und welche Hinweise beim Ausfüllen gelten.</summary>
public static class Antragsregeln
{
    /// <summary>Art der Zielabteilung.</summary>
    public static AbteilungArt Zielabteilung(AntragArt art) => art == AntragArt.JugendEintritt ? AbteilungArt.Jugend : AbteilungArt.Einsatz;

    /// <summary>Bei Übertritt und Volljährigkeit gibt es das Mitglied schon.</summary>
    public static bool MitMitglied(AntragArt art) => art is AntragArt.UebertrittEinsatz or AntragArt.Volljaehrigkeit;

    /// <summary>Unterschreibt die Person selbst? Bei der Aufnahme in die Jugendfeuerwehr nur die Erziehungsberechtigten.</summary>
    public static bool UnterschriftPerson(AntragArt art) => art != AntragArt.JugendEintritt;

    /// <summary>Unterschreiben die Erziehungsberechtigten? Immer bei Minderjährigen, nie bei der Volljährigkeitserklärung.</summary>
    public static bool UnterschriftEltern(AntragArt art, int? alterBeiAntrag, int volljaehrigAb) =>
        art switch
        {
            AntragArt.Volljaehrigkeit => false,
            AntragArt.JugendEintritt or AntragArt.UebertrittEinsatz => true,
            _ => alterBeiAntrag is null || alterBeiAntrag < volljaehrigAb,
        };

    /// <summary>Hinweise zu Altersgrenzen der Satzung; der Antrag wird trotzdem gespeichert.</summary>
    /// <summary>
    /// Fälligkeit der Erklärung bei Volljährigkeit für Mitglieder, die minderjährig in die Einsatzabteilung kamen: ab
    /// <paramref name="vorlaufMonate"/> vor dem Geburtstag bis <paramref name="erinnernMonate"/> danach; sonst null.
    /// </summary>
    public static DateOnly? VolljaehrigkeitFaellig(
        DateOnly geburtsdatum, DateOnly einsatzSeit, DateOnly stichtag, int volljaehrigAb, int vorlaufMonate, int erinnernMonate)
    {
        var tag = Laufbahnhinweise.Geburtstag(geburtsdatum, volljaehrigAb);
        return einsatzSeit < tag && tag <= stichtag.AddMonths(vorlaufMonate) && stichtag <= tag.AddMonths(erinnernMonate) ? tag : null;
    }

    public static IReadOnlyList<string> Hinweise(AntragArt art, DateOnly geburtsdatum, DateOnly stichtag, Laufbahnregeln regeln, int volljaehrigAb)
    {
        var alter = Mitglied.Alter(geburtsdatum, stichtag);
        var hinweise = new List<string>();
        switch (art)
        {
            case AntragArt.JugendEintritt:
                if (regeln.JugendAufnahmeAbAlter > 0 && alter < regeln.JugendAufnahmeAbAlter)
                {
                    hinweise.Add($"Aufnahme in die Jugendfeuerwehr erst ab {regeln.JugendAufnahmeAbAlter} Jahren.");
                }

                if (alter >= regeln.JugendAufnahmeBisAlter)
                {
                    hinweise.Add($"Aufnahme in die Jugendfeuerwehr nur bis zum vollendeten {regeln.JugendAufnahmeBisAlter}. Lebensjahr; ab {regeln.AufnahmeEinsatzAbAlter} ist die Einsatzabteilung möglich.");
                }

                break;
            case AntragArt.UebertrittEinsatz:
            case AntragArt.EinsatzEintritt:
                if (alter < regeln.AufnahmeEinsatzAbAlter)
                {
                    hinweise.Add($"Aufnahme in die Einsatzabteilung erst ab {regeln.AufnahmeEinsatzAbAlter} Jahren.");
                }
                else if (alter < regeln.EinsatzdienstAbAlter)
                {
                    hinweise.Add($"Einsatzdienst erst ab {regeln.EinsatzdienstAbAlter}; bis dahin mit schriftlicher Zustimmung der Erziehungsberechtigten.");
                }

                if (alter >= regeln.AltersgrenzeEinsatzdienst)
                {
                    hinweise.Add($"Der Dienst in der Einsatzabteilung endet mit {regeln.AltersgrenzeEinsatzdienst}.");
                }

                break;
            case AntragArt.Volljaehrigkeit:
                if (alter < volljaehrigAb)
                {
                    hinweise.Add($"Volljährig erst am {Laufbahnhinweise.Geburtstag(geburtsdatum, volljaehrigAb):dd.MM.yyyy}.");
                }

                break;
        }

        return hinweise;
    }
}
