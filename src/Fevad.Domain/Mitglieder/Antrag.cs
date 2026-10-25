using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;

namespace Fevad.Domain.Mitglieder;

/// <summary>Art eines Antrags; bestimmt Formular, Unterschriften und was die Annahme bewirkt.</summary>
public enum AntragArt
{
    /// <summary>Aufnahme in die Jugendfeuerwehr; unterschreiben nur die Erziehungsberechtigten.</summary>
    [Display(Name = "Aufnahme in die Jugendfeuerwehr")] JugendEintritt,
    /// <summary>Übertritt aus der Jugendfeuerwehr in die Einsatzabteilung ab 17 Jahren, mit Zustimmung der Erziehungsberechtigten.</summary>
    [Display(Name = "Übertritt in die Einsatzabteilung mit 17")] UebertrittEinsatz,
    /// <summary>Aufnahme in die Einsatzabteilung von außen; setzt die Volljährigkeit voraus.</summary>
    [Display(Name = "Aufnahme in die Einsatzabteilung")] EinsatzEintritt,
    /// <summary>Erklärung bei Volljährigkeit für Mitglieder, die minderjährig in die Einsatzabteilung kamen.</summary>
    [Display(Name = "Erklärung bei Volljährigkeit")] Volljaehrigkeit,
    /// <summary>Wechsel aus der Einsatzabteilung in die Altersabteilung auf Antrag.</summary>
    [Display(Name = "Wechsel in die Altersabteilung")] Altersabteilung,
    /// <summary>Ausscheiden aus der Feuerwehr auf Antrag.</summary>
    [Display(Name = "Ausscheiden aus der Feuerwehr")] Ausscheiden,
}

/// <summary>Bearbeitungsstand eines Antrags.</summary>
public enum AntragStatus
{
    /// <summary>In FEVAD ausgefüllt, noch nicht unterschrieben zurück.</summary>
    [Display(Name = "Entwurf")] Entwurf,
    /// <summary>Unterschriebenes Formular ist eingegangen und wartet auf die Entscheidung.</summary>
    [Display(Name = "unterschrieben eingegangen")] Eingegangen,
    /// <summary>Angenommen; die Mitgliedschaft wurde angelegt oder geändert.</summary>
    [Display(Name = "angenommen")] Angenommen,
    /// <summary>Abgelehnt.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
    /// <summary>Vom Antragsteller zurückgezogen.</summary>
    [Display(Name = "zurückgezogen")] Zurueckgezogen,
}

/// <summary>Warum jemand eine frühere Feuerwehr verlassen hat. Bewusst ohne Gesundheit und Ausschluss (Datenminimierung).</summary>
public enum Ausscheidensgrund
{
    /// <summary>Umzug.</summary>
    [Display(Name = "Umzug")] Umzug,
    /// <summary>Wechsel in eine andere Feuerwehr.</summary>
    [Display(Name = "Wechsel in eine andere Feuerwehr")] Wechsel,
    /// <summary>Ende der Jugendfeuerwehr ohne Übertritt in die Einsatzabteilung.</summary>
    [Display(Name = "Ende der Jugendfeuerwehr ohne Übertritt")] JugendfeuerwehrEnde,
    /// <summary>Berufliche Gründe.</summary>
    [Display(Name = "berufliche Gründe")] Beruf,
    /// <summary>Persönliche Gründe, auch gesundheitliche.</summary>
    [Display(Name = "persönliche Gründe")] Persoenlich,
    /// <summary>Sonstiger Grund.</summary>
    [Display(Name = "sonstiger Grund")] Sonstiges,
}

/// <summary>
/// Gründe für den Antrag auf Ausscheiden nach der Feuerwehrsatzung Adelberg. Was ohne Antrag endet (Altersgrenze, Probezeit nicht
/// bestanden, Dienstverpflichtung erfüllt, Richterspruch, Beendigung durch den Gemeinderat), ist kein Antrag. Gesundheitliche Gründe
/// fallen bewusst unter „persönliche Gründe“, damit keine Gesundheitsangaben gespeichert werden.
/// </summary>
public enum Entlassungsgrund
{
    /// <summary>Austritt während oder mit Ablauf der Probezeit (§ 4 Abs. 1 Nr. 2).</summary>
    [Display(Name = "Austritt während oder mit Ablauf der Probezeit (§ 4 Abs. 1 Nr. 2)")] AustrittProbezeit,
    /// <summary>Dienst aus persönlichen oder beruflichen Gründen nicht mehr möglich (§ 4 Abs. 2 Nr. 2).</summary>
    [Display(Name = "Dienst aus persönlichen oder beruflichen Gründen nicht mehr möglich (§ 4 Abs. 2 Nr. 2)")] PersoenlichBeruflich,
    /// <summary>Wohnung in eine andere Gemeinde verlegt (§ 4 Abs. 2 Nr. 3).</summary>
    [Display(Name = "Wohnung in eine andere Gemeinde verlegt (§ 4 Abs. 2 Nr. 3)")] Wohnung,
    /// <summary>Arbeitsstätte in eine andere Gemeinde verlegt, Wohnung auswärts (§ 4 Abs. 2 Nr. 4).</summary>
    [Display(Name = "Arbeitsstätte in eine andere Gemeinde verlegt, Wohnung auswärts (§ 4 Abs. 2 Nr. 4)")] Arbeitsstaette,
    /// <summary>Austritt aus der Jugendfeuerwehr (§ 7 Abs. 3 Nr. 2).</summary>
    [Display(Name = "Austritt aus der Jugendfeuerwehr (§ 7 Abs. 3 Nr. 2)")] JugendAustritt,
    /// <summary>Erziehungsberechtigte nehmen ihre Zustimmung zurück (§ 7 Abs. 3 Nr. 3).</summary>
    [Display(Name = "Erziehungsberechtigte nehmen ihre Zustimmung zurück (§ 7 Abs. 3 Nr. 3)")] ZustimmungZurueck,
}

/// <summary>
/// Antrag auf Aufnahme, Übertritt, Wechsel in die Altersabteilung, Ausscheiden oder die Erklärung bei Volljährigkeit. Wird in FEVAD ausgefüllt und als PDF zum
/// Unterschreiben gedruckt; rechtsverbindlich ist das unterschriebene Papier. Bei Übertritt und Volljährigkeit gehört
/// der Antrag zu einem Mitglied, bei Aufnahmen entsteht das Mitglied erst mit der Annahme.
/// </summary>
public class Antrag : Entitaet
{
    /// <summary>Art des Antrags.</summary>
    public AntragArt Art { get; set; }
    /// <summary>Bearbeitungsstand.</summary>
    public AntragStatus Status { get; set; }

    /// <summary>Abteilung, in die aufgenommen oder übergetreten wird.</summary>
    public Guid AbteilungId { get; set; }
    /// <summary>Die Abteilung des Antrags.</summary>
    public Abteilung? Abteilung { get; set; }

    /// <summary>Schlüssel des bestehenden Mitglieds bei Übertritt, Volljährigkeit, Altersabteilung und Ausscheiden; leer bei Aufnahmen.</summary>
    public Guid? MitgliedId { get; set; }
    /// <summary>Das bestehende Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>
    /// Wiedereintritt: Aufnahme eines früheren Mitglieds. Die Annahme nimmt dann dieses Mitglied wieder auf (neue Mitgliedschaft,
    /// gleiche Personalnummer, Laufbahn und Lehrgänge bleiben), statt ein zweites anzulegen.
    /// </summary>
    public Guid? WiedereintrittVonId { get; set; }
    /// <summary>Das frühere Mitglied, das wieder aufgenommen wird.</summary>
    public Mitglied? WiedereintrittVon { get; set; }

    /// <summary>Vorname der antragstellenden Person.</summary>
    public required string Vorname { get; set; }
    /// <summary>Nachname der antragstellenden Person.</summary>
    public required string Nachname { get; set; }
    /// <summary>Geburtsdatum; daran prüft FEVAD die Altersgrenzen.</summary>
    public DateOnly Geburtsdatum { get; set; }
    /// <summary>Geburtsort, wie im Aufnahmeformular erfragt.</summary>
    public string? Geburtsort { get; set; }
    /// <summary>Geschlecht; darf offen bleiben.</summary>
    public Geschlecht? Geschlecht { get; set; }
    /// <summary>Straße und Hausnummer.</summary>
    public string? Strasse { get; set; }
    /// <summary>Postleitzahl.</summary>
    public string? Plz { get; set; }
    /// <summary>Ort.</summary>
    public string? Ort { get; set; }
    /// <summary>Festnetznummer.</summary>
    public string? Telefon { get; set; }
    /// <summary>Mobilnummer.</summary>
    public string? Mobil { get; set; }
    /// <summary>E-Mail-Adresse.</summary>
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
    /// <summary>Die Person war schon in einer anderen Feuerwehr.</summary>
    public bool FrueherFeuerwehr { get; set; }
    /// <summary>Name der früheren Feuerwehr.</summary>
    public string? FrueherFeuerwehrName { get; set; }
    /// <summary>Beginn der Zeit in der früheren Feuerwehr.</summary>
    public DateOnly? FrueherVon { get; set; }
    /// <summary>Ende der Zeit in der früheren Feuerwehr.</summary>
    public DateOnly? FrueherBis { get; set; }
    /// <summary>Grund des Ausscheidens aus der früheren Feuerwehr.</summary>
    public Ausscheidensgrund? FrueherAustrittsgrund { get; set; }
    /// <summary>Früher besuchte Lehrgänge als Freitext; werden nach der Annahme mit Nachweis übernommen.</summary>
    public string? FrueherLehrgaenge { get; set; }
    /// <summary>Zuletzt erreichter Dienstgrad in der früheren Feuerwehr.</summary>
    public string? FrueherDienstgrad { get; set; }
    /// <summary>Die Person ist zugleich in einer anderen Feuerwehr aktiv (Doppelmitgliedschaft).</summary>
    public bool AndereFeuerwehrAktiv { get; set; }
    /// <summary>Name der anderen Feuerwehr bei Doppelmitgliedschaft.</summary>
    public string? AndereFeuerwehrName { get; set; }

    /// <summary>Nur ja oder nein, keine Gründe.</summary>
    public bool FrueherAusgeschlossen { get; set; }

    /// <summary>
    /// Einwilligung in Fotos für die Öffentlichkeitsarbeit (Art. 7 DSGVO): freiwillig, nicht vorbelegt, jederzeit widerrufbar.
    /// Leer heißt keine Angabe. Wird bei der Annahme mit Datum ins Mitglied übernommen.
    /// </summary>
    public bool? FotoEinwilligung { get; set; }

    /// <summary>Tag, an dem das unterschriebene Formular eingegangen ist.</summary>
    public DateOnly? EingegangenAm { get; set; }
    /// <summary>Tag der Annahme oder Ablehnung.</summary>
    public DateOnly? EntschiedenAm { get; set; }
    /// <summary>Freitext, z. B. Ausschussbeschluss oder Grund der Ablehnung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Erziehungsberechtigte, die auf dem Antrag stehen.</summary>
    public List<AntragElternteil> Erziehungsberechtigte { get; set; } = [];

    /// <summary>„Nachname, Vorname“ für Listen.</summary>
    public string Anzeigename => $"{Nachname}, {Vorname}";

    /// <summary>Angenommen, abgelehnt oder zurückgezogen; dann ist der Antrag nicht mehr änderbar.</summary>
    public bool Abgeschlossen => Status is AntragStatus.Angenommen or AntragStatus.Abgelehnt or AntragStatus.Zurueckgezogen;
}

/// <summary>Erziehungsberechtigte(r) auf einem Antrag; wird bei der Annahme als <see cref="Erziehungsberechtigter"/> übernommen.</summary>
public class AntragElternteil : Entitaet
{
    /// <summary>Schlüssel des Antrags.</summary>
    public Guid AntragId { get; set; }
    /// <summary>Der Antrag.</summary>
    public Antrag? Antrag { get; set; }

    /// <summary>1 oder 2.</summary>
    public int Nummer { get; set; }
    /// <summary>Verhältnis zum Kind.</summary>
    public Sorgeverhaeltnis? Verhaeltnis { get; set; }
    /// <summary>Vorname.</summary>
    public required string Vorname { get; set; }
    /// <summary>Nachname.</summary>
    public required string Nachname { get; set; }
    /// <summary>Straße und Hausnummer.</summary>
    public string? Strasse { get; set; }
    /// <summary>Postleitzahl.</summary>
    public string? Plz { get; set; }
    /// <summary>Ort.</summary>
    public string? Ort { get; set; }
    /// <summary>Festnetznummer.</summary>
    public string? Telefon { get; set; }
    /// <summary>Mobilnummer.</summary>
    public string? Mobil { get; set; }
    /// <summary>E-Mail-Adresse.</summary>
    public string? Email { get; set; }
}

/// <summary>Was ein Antragsformular braucht und welche Prüfungen beim Ausfüllen gelten.</summary>
public static class Antragsregeln
{
    /// <summary>
    /// Art der Abteilung des Antrags: Ziel bei Aufnahme und Übertritt, die bisherige Einsatzabteilung beim Wechsel in die
    /// Altersabteilung; beim Ausscheiden jede Abteilung, in der das Mitglied ist (null).
    /// </summary>
    /// <param name="art">Art des Antrags.</param>
    /// <returns>Die Abteilungsart oder null beim Ausscheiden.</returns>
    public static AbteilungArt? Zielabteilung(AntragArt art) => art switch
    {
        AntragArt.JugendEintritt => AbteilungArt.Jugend,
        AntragArt.Ausscheiden => null,
        _ => AbteilungArt.Einsatz,
    };

    /// <summary>Außer bei den Aufnahmen gibt es das Mitglied schon.</summary>
    /// <param name="art">Art des Antrags.</param>
    /// <returns>true, wenn der Antrag zu einem bestehenden Mitglied gehört.</returns>
    public static bool MitMitglied(AntragArt art) => art is not (AntragArt.JugendEintritt or AntragArt.EinsatzEintritt);

    /// <summary>Mit Probezeit nach der Annahme: Übertritt und Aufnahme in die Einsatzabteilung.</summary>
    /// <param name="art">Art des Antrags.</param>
    /// <returns>true bei Übertritt und Aufnahme in die Einsatzabteilung.</returns>
    public static bool MitProbezeit(AntragArt art) => art is AntragArt.UebertrittEinsatz or AntragArt.EinsatzEintritt;

    /// <summary>Foto-Einwilligung gehört zu Aufnahme, Übertritt und Volljährigkeit, nicht zu Wechsel und Ausscheiden.</summary>
    /// <param name="art">Art des Antrags.</param>
    /// <returns>true, wenn das Formular die Foto-Einwilligung abfragt.</returns>
    public static bool MitFotos(AntragArt art) => art is not (AntragArt.Altersabteilung or AntragArt.Ausscheiden);

    /// <summary>Unterschreibt die Person selbst? Bei der Aufnahme in die Jugendfeuerwehr nur die Erziehungsberechtigten.</summary>
    /// <param name="art">Art des Antrags.</param>
    /// <returns>true, wenn die antragstellende Person selbst unterschreibt.</returns>
    public static bool UnterschriftPerson(AntragArt art) => art != AntragArt.JugendEintritt;

    /// <summary>
    /// Stehen die Erziehungsberechtigten mit ihren Angaben auf dem Antrag? Nur bei Jugendfeuerwehr und Übertritt: Minderjährige kommen
    /// nur über den Übertritt in die Einsatzabteilung, die Aufnahme von außen setzt die Volljährigkeit voraus.
    /// </summary>
    /// <param name="art">Art des Antrags.</param>
    /// <returns>true bei Aufnahme in die Jugendfeuerwehr und Übertritt.</returns>
    public static bool UnterschriftEltern(AntragArt art) => art is AntragArt.JugendEintritt or AntragArt.UebertrittEinsatz;

    /// <summary>Unterschreiben Erziehungsberechtigte? Wie <see cref="UnterschriftEltern"/>, dazu beim Ausscheiden Minderjähriger.</summary>
    /// <param name="art">Art des Antrags.</param>
    /// <param name="minderjaehrig">Die Person ist am gewünschten Tag minderjährig.</param>
    /// <returns>true, wenn das Formular Unterschriftsfelder für Erziehungsberechtigte braucht.</returns>
    public static bool ElternUnterschreiben(AntragArt art, bool minderjaehrig) =>
        UnterschriftEltern(art) || (art == AntragArt.Ausscheiden && minderjaehrig);

    /// <summary>Welche Gründe beim Ausscheiden aus einer Abteilung dieser Art zulässig sind.</summary>
    /// <param name="bisher">Art der Abteilung, aus der ausgeschieden wird.</param>
    /// <param name="probezeit">Das Mitglied ist noch in der Probezeit.</param>
    /// <returns>Die zulässigen Gründe für die Auswahl.</returns>
    public static IReadOnlyList<Entlassungsgrund> Gruende(AbteilungArt bisher, bool probezeit) => bisher switch
    {
        AbteilungArt.Jugend or AbteilungArt.Kinder => [Entlassungsgrund.JugendAustritt, Entlassungsgrund.ZustimmungZurueck],
        AbteilungArt.Einsatz when probezeit => [Entlassungsgrund.AustrittProbezeit, Entlassungsgrund.PersoenlichBeruflich, Entlassungsgrund.Wohnung, Entlassungsgrund.Arbeitsstaette],
        AbteilungArt.Einsatz => [Entlassungsgrund.PersoenlichBeruflich, Entlassungsgrund.Wohnung, Entlassungsgrund.Arbeitsstaette],
        _ => [Entlassungsgrund.PersoenlichBeruflich, Entlassungsgrund.Wohnung],
    };

    /// <summary>Satzung § 6 Abs. 1: wer aus diesen Gründen die Einsatzabteilung verlässt, kommt in die Altersabteilung, wenn er nicht widerspricht.</summary>
    /// <param name="bisher">Art der bisherigen Abteilung.</param>
    /// <param name="grund">Gewählter Grund des Ausscheidens.</param>
    /// <returns>true, wenn die Übernahme in die Altersabteilung angeboten wird.</returns>
    public static bool AltersabteilungMoeglich(AbteilungArt bisher, Entlassungsgrund? grund) =>
        bisher == AbteilungArt.Einsatz && grund is Entlassungsgrund.PersoenlichBeruflich or Entlassungsgrund.Wohnung or Entlassungsgrund.Arbeitsstaette;

    /// <summary>Aufnahme in die Einsatzabteilung ohne Übertritt erst mit der Volljährigkeit.</summary>
    /// <param name="art">Art des Antrags.</param>
    /// <param name="geburtsdatum">Geburtsdatum der Person.</param>
    /// <param name="stichtag">Gewünschter Tag der Aufnahme.</param>
    /// <param name="volljaehrigAb">Alter der Volljährigkeit (Parameter).</param>
    /// <returns>true, wenn eine Aufnahme in die Einsatzabteilung am Stichtag zu früh wäre.</returns>
    public static bool ZuJungFuerAufnahme(AntragArt art, DateOnly geburtsdatum, DateOnly stichtag, int volljaehrigAb) =>
        art == AntragArt.EinsatzEintritt && Mitglied.Alter(geburtsdatum, stichtag) < volljaehrigAb;

    /// <summary>
    /// Fälligkeit der Erklärung bei Volljährigkeit für Mitglieder, die minderjährig in die Einsatzabteilung kamen: ab
    /// <paramref name="vorlaufMonate"/> vor dem Geburtstag bis <paramref name="erinnernMonate"/> danach; sonst null.
    /// </summary>
    /// <param name="geburtsdatum">Geburtsdatum des Mitglieds.</param>
    /// <param name="einsatzSeit">Beginn der Mitgliedschaft in der Einsatzabteilung.</param>
    /// <param name="stichtag">Heutiger Tag.</param>
    /// <param name="volljaehrigAb">Alter der Volljährigkeit (Parameter).</param>
    /// <param name="vorlaufMonate">So viele Monate vor dem Geburtstag wird die Erklärung fällig.</param>
    /// <param name="erinnernMonate">So viele Monate nach dem Geburtstag wird noch erinnert.</param>
    /// <returns>Den Tag der Volljährigkeit, wenn die Erklärung jetzt fällig ist, sonst null.</returns>
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
    /// <param name="art">Art des Antrags.</param>
    /// <param name="geburtsdatum">Geburtsdatum der Person.</param>
    /// <param name="gewuenschtAb">Gewünschter Tag; leer heißt heute.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <param name="regeln">Altersgrenzen aus Satzung und Parametern.</param>
    /// <param name="volljaehrigAb">Alter der Volljährigkeit.</param>
    /// <param name="vorlaufMonate">Höchster Vorlauf in Monaten.</param>
    /// <returns>Fehlermeldungen für die Oberfläche; leer, wenn der Antrag zulässig ist.</returns>
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

    /// <summary>Arten, deren Datum ein Beginn ist (Aufnahme, Übertritt, Altersabteilung); ein Beginn vor heute ist Nacherfassung.</summary>
    /// <param name="art">Art des Antrags.</param>
    /// <returns>true, wenn das gewünschte Datum der Beginn einer Mitgliedschaft ist.</returns>
    public static bool MitBeginn(AntragArt art) =>
        art is AntragArt.JugendEintritt or AntragArt.UebertrittEinsatz or AntragArt.EinsatzEintritt or AntragArt.Altersabteilung;

    /// <summary>Hinweise, die den Antrag nicht hindern.</summary>
    /// <param name="art">Art des Antrags.</param>
    /// <param name="geburtsdatum">Geburtsdatum der Person.</param>
    /// <param name="stichtag">Gewünschter Tag.</param>
    /// <param name="regeln">Altersgrenzen aus Satzung und Parametern.</param>
    /// <returns>Hinweise, z. B. dass Einsatzdienst erst ab einem höheren Alter möglich ist; leer, wenn es keine gibt.</returns>
    public static IReadOnlyList<string> Hinweise(AntragArt art, DateOnly geburtsdatum, DateOnly stichtag, Laufbahnregeln regeln)
    {
        var alter = Mitglied.Alter(geburtsdatum, stichtag);
        return art is AntragArt.UebertrittEinsatz or AntragArt.EinsatzEintritt && alter >= regeln.AufnahmeEinsatzAbAlter && alter < regeln.EinsatzdienstAbAlter
            ? [$"Einsatzdienst erst ab {regeln.EinsatzdienstAbAlter}; bis dahin Übungen und Ausbildung, mit schriftlicher Zustimmung der Erziehungsberechtigten."]
            : [];
    }
}
