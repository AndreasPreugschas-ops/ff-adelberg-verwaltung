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
    [Display(Name = "Wechsel in die Altersabteilung")] Altersabteilung,
    [Display(Name = "Ausscheiden aus der Feuerwehr")] Ausscheiden,
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
/// Gründe für den Antrag auf Ausscheiden nach der Feuerwehrsatzung Adelberg. Was ohne Antrag endet (Altersgrenze, Probezeit nicht
/// bestanden, Dienstverpflichtung erfüllt, Richterspruch, Beendigung durch den Gemeinderat), ist kein Antrag. Gesundheitliche Gründe
/// fallen bewusst unter „persönliche Gründe“, damit keine Gesundheitsangaben gespeichert werden.
/// </summary>
public enum Entlassungsgrund
{
    [Display(Name = "Austritt während oder mit Ablauf der Probezeit (§ 4 Abs. 1 Nr. 2)")] AustrittProbezeit,
    [Display(Name = "Dienst aus persönlichen oder beruflichen Gründen nicht mehr möglich (§ 4 Abs. 2 Nr. 2)")] PersoenlichBeruflich,
    [Display(Name = "Wohnung in eine andere Gemeinde verlegt (§ 4 Abs. 2 Nr. 3)")] Wohnung,
    [Display(Name = "Arbeitsstätte in eine andere Gemeinde verlegt, Wohnung auswärts (§ 4 Abs. 2 Nr. 4)")] Arbeitsstaette,
    [Display(Name = "Austritt aus der Jugendfeuerwehr (§ 7 Abs. 3 Nr. 2)")] JugendAustritt,
    [Display(Name = "Erziehungsberechtigte nehmen ihre Zustimmung zurück (§ 7 Abs. 3 Nr. 3)")] ZustimmungZurueck,
}

/// <summary>
/// Antrag auf Aufnahme, Übertritt, Wechsel in die Altersabteilung, Ausscheiden oder die Erklärung bei Volljährigkeit. Wird in FEVAD ausgefüllt und als PDF zum
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

    /// <summary>Gewünschter Beginn der Mitgliedschaft bzw. des Übertritts, beim Ausscheiden der letzte Diensttag.</summary>
    public DateOnly? GewuenschtAb { get; set; }

    /// <summary>Nur beim Ausscheiden.</summary>
    public Entlassungsgrund? Entlassungsgrund { get; set; }

    /// <summary>
    /// Beim Ausscheiden aus der Einsatzabteilung: in die Altersabteilung übernehmen (Satzung § 6 Abs. 1, gilt ohne gegenteilige Erklärung).
    /// </summary>
    public bool InAltersabteilung { get; set; }

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

/// <summary>Was ein Antragsformular braucht und welche Prüfungen beim Ausfüllen gelten.</summary>
public static class Antragsregeln
{
    /// <summary>
    /// Art der Abteilung des Antrags: Ziel bei Aufnahme und Übertritt, die bisherige Einsatzabteilung beim Wechsel in die
    /// Altersabteilung; beim Ausscheiden jede Abteilung, in der das Mitglied ist (null).
    /// </summary>
    public static AbteilungArt? Zielabteilung(AntragArt art) => art switch
    {
        AntragArt.JugendEintritt => AbteilungArt.Jugend,
        AntragArt.Ausscheiden => null,
        _ => AbteilungArt.Einsatz,
    };

    /// <summary>Außer bei den Aufnahmen gibt es das Mitglied schon.</summary>
    public static bool MitMitglied(AntragArt art) => art is not (AntragArt.JugendEintritt or AntragArt.EinsatzEintritt);

    /// <summary>Mit Probezeit nach der Annahme: Übertritt und Aufnahme in die Einsatzabteilung.</summary>
    public static bool MitProbezeit(AntragArt art) => art is AntragArt.UebertrittEinsatz or AntragArt.EinsatzEintritt;

    /// <summary>Foto-Einwilligung gehört zu Aufnahme, Übertritt und Volljährigkeit, nicht zu Wechsel und Ausscheiden.</summary>
    public static bool MitFotos(AntragArt art) => art is not (AntragArt.Altersabteilung or AntragArt.Ausscheiden);

    /// <summary>Unterschreibt die Person selbst? Bei der Aufnahme in die Jugendfeuerwehr nur die Erziehungsberechtigten.</summary>
    public static bool UnterschriftPerson(AntragArt art) => art != AntragArt.JugendEintritt;

    /// <summary>
    /// Stehen die Erziehungsberechtigten mit ihren Angaben auf dem Antrag? Nur bei Jugendfeuerwehr und Übertritt: Minderjährige kommen
    /// nur über den Übertritt in die Einsatzabteilung, die Aufnahme von außen setzt die Volljährigkeit voraus.
    /// </summary>
    public static bool UnterschriftEltern(AntragArt art) => art is AntragArt.JugendEintritt or AntragArt.UebertrittEinsatz;

    /// <summary>Unterschreiben Erziehungsberechtigte? Wie <see cref="UnterschriftEltern"/>, dazu beim Ausscheiden Minderjähriger.</summary>
    public static bool ElternUnterschreiben(AntragArt art, bool minderjaehrig) =>
        UnterschriftEltern(art) || (art == AntragArt.Ausscheiden && minderjaehrig);

    /// <summary>Welche Gründe beim Ausscheiden aus einer Abteilung dieser Art zulässig sind.</summary>
    public static IReadOnlyList<Entlassungsgrund> Gruende(AbteilungArt bisher, bool probezeit) => bisher switch
    {
        AbteilungArt.Jugend or AbteilungArt.Kinder => [Entlassungsgrund.JugendAustritt, Entlassungsgrund.ZustimmungZurueck],
        AbteilungArt.Einsatz when probezeit => [Entlassungsgrund.AustrittProbezeit, Entlassungsgrund.PersoenlichBeruflich, Entlassungsgrund.Wohnung, Entlassungsgrund.Arbeitsstaette],
        AbteilungArt.Einsatz => [Entlassungsgrund.PersoenlichBeruflich, Entlassungsgrund.Wohnung, Entlassungsgrund.Arbeitsstaette],
        _ => [Entlassungsgrund.PersoenlichBeruflich, Entlassungsgrund.Wohnung],
    };

    /// <summary>Satzung § 6 Abs. 1: wer aus diesen Gründen die Einsatzabteilung verlässt, kommt in die Altersabteilung, wenn er nicht widerspricht.</summary>
    public static bool AltersabteilungMoeglich(AbteilungArt bisher, Entlassungsgrund? grund) =>
        bisher == AbteilungArt.Einsatz && grund is Entlassungsgrund.PersoenlichBeruflich or Entlassungsgrund.Wohnung or Entlassungsgrund.Arbeitsstaette;

    /// <summary>Aufnahme in die Einsatzabteilung ohne Übertritt erst mit der Volljährigkeit.</summary>
    public static bool ZuJungFuerAufnahme(AntragArt art, DateOnly geburtsdatum, DateOnly stichtag, int volljaehrigAb) =>
        art == AntragArt.EinsatzEintritt && Mitglied.Alter(geburtsdatum, stichtag) < volljaehrigAb;

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

    /// <summary>
    /// Was einen Antrag unmöglich macht: zu viel Vorlauf (höchstens <paramref name="vorlaufMonate"/> vor dem gewünschten Tag) und
    /// Altersgrenzen der Satzung am gewünschten Tag, etwa die Aufnahme in die Einsatzabteilung mit 15. Ohne Datum gilt heute.
    /// </summary>
    public static IReadOnlyList<string> Fehler(
        AntragArt art, DateOnly geburtsdatum, DateOnly? gewuenschtAb, DateOnly heute, Laufbahnregeln regeln, int volljaehrigAb, int vorlaufMonate)
    {
        var fehler = new List<string>();
        var spaetestens = heute.AddMonths(vorlaufMonate);
        if (gewuenschtAb > spaetestens)
        {
            fehler.Add($"Anträge gehen höchstens {vorlaufMonate} Monate im Voraus; für den {gewuenschtAb:dd.MM.yyyy} frühestens ab dem {gewuenschtAb.Value.AddMonths(-vorlaufMonate):dd.MM.yyyy}.");
        }

        var stichtag = gewuenschtAb ?? heute;
        var alter = Mitglied.Alter(geburtsdatum, stichtag);

        // Grenze noch nicht erreicht: Meldung mit dem Tag, ab dem es geht, und wann der Antrag frühestens gestellt werden kann.
        void ErstAb(int jahre, string was)
        {
            var tag = Laufbahnhinweise.Geburtstag(geburtsdatum, jahre);
            var text = $"{was} erst ab {jahre} Jahren, also ab dem {tag:dd.MM.yyyy} (am {stichtag:dd.MM.yyyy}: {alter}).";
            fehler.Add(tag > spaetestens ? $"{text} Der Antrag geht frühestens ab dem {tag.AddMonths(-vorlaufMonate):dd.MM.yyyy}." : $"{text} „Gewünscht ab“ auf diesen Tag setzen.");
        }

        switch (art)
        {
            case AntragArt.JugendEintritt when regeln.JugendAufnahmeAbAlter > 0 && alter < regeln.JugendAufnahmeAbAlter:
                ErstAb(regeln.JugendAufnahmeAbAlter, "Aufnahme in die Jugendfeuerwehr");
                break;
            case AntragArt.JugendEintritt when alter >= regeln.JugendAufnahmeBisAlter:
                fehler.Add($"In die Jugendfeuerwehr nur bis zum vollendeten {regeln.JugendAufnahmeBisAlter}. Lebensjahr (am {stichtag:dd.MM.yyyy}: {alter}).");
                break;
            case AntragArt.UebertrittEinsatz when alter < regeln.AufnahmeEinsatzAbAlter:
                ErstAb(regeln.AufnahmeEinsatzAbAlter, "Übertritt in die Einsatzabteilung");
                break;
            case AntragArt.EinsatzEintritt when alter < volljaehrigAb:
                fehler.Add($"Ohne Übertritt aus der Jugendfeuerwehr nimmt die Einsatzabteilung erst ab {volljaehrigAb} Jahren auf (am {stichtag:dd.MM.yyyy}: {alter}). " +
                    $"Jugendliche kommen ab {regeln.AufnahmeEinsatzAbAlter} über die Jugendfeuerwehr und den Übertritt.");
                break;
            case AntragArt.Altersabteilung when alter < regeln.AltersabteilungAbAlter:
                ErstAb(regeln.AltersabteilungAbAlter, "Wechsel in die Altersabteilung auf Antrag");
                break;
            case AntragArt.Volljaehrigkeit when alter < volljaehrigAb:
                ErstAb(volljaehrigAb, "Die Erklärung bei Volljährigkeit");
                break;
        }

        if (art is AntragArt.UebertrittEinsatz or AntragArt.EinsatzEintritt && alter >= regeln.AltersgrenzeEinsatzdienst)
        {
            fehler.Add($"Mit {regeln.AltersgrenzeEinsatzdienst} Jahren endet der Dienst in der Einsatzabteilung.");
        }

        return fehler;
    }

    /// <summary>Hinweise, die den Antrag nicht hindern.</summary>
    public static IReadOnlyList<string> Hinweise(AntragArt art, DateOnly geburtsdatum, DateOnly stichtag, Laufbahnregeln regeln)
    {
        var alter = Mitglied.Alter(geburtsdatum, stichtag);
        return art is AntragArt.UebertrittEinsatz or AntragArt.EinsatzEintritt && alter >= regeln.AufnahmeEinsatzAbAlter && alter < regeln.EinsatzdienstAbAlter
            ? [$"Einsatzdienst erst ab {regeln.EinsatzdienstAbAlter}; bis dahin Übungen und Ausbildung, mit schriftlicher Zustimmung der Erziehungsberechtigten."]
            : [];
    }
}
