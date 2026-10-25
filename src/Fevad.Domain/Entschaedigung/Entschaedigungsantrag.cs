using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Entschaedigung;

/// <summary>Wofür ein Mitglied Entschädigung beantragt.</summary>
public enum AntragAnlass
{
    /// <summary>Aus- oder Fortbildungslehrgang (FwES Adelberg § 2).</summary>
    [Display(Name = "Lehrgang")] Lehrgang,
    /// <summary>Einsatz über mehr als zwei Tage; kürzere Einsätze rechnet der Einsatzbericht ab (§ 1 Abs. 4).</summary>
    [Display(Name = "Einsatz über mehrere Tage")] Einsatz,
}

/// <summary>Erwerbslage des Mitglieds; bestimmt, wie Verdienstausfall berechnet wird.</summary>
public enum Erwerbslage
{
    /// <summary>Arbeitnehmer; Verdienstausfall mit Nachweis des Arbeitgebers.</summary>
    [Display(Name = "Arbeitnehmer")] Arbeitnehmer,
    /// <summary>Selbstständig; Verdienstausfall mit eigenem Nachweis.</summary>
    [Display(Name = "selbstständig")] Selbststaendig,
    /// <summary>Haushaltsführend ohne Verdienst; Zeitversäumnis gilt als Verdienstausfall (§ 15 Abs. 1 Satz 3 FwG, FwES § 4).</summary>
    [Display(Name = "haushaltsführend")] Haushaltsfuehrend,
    /// <summary>Ohne Verdienst und ohne Haushaltsführung, z. B. Schüler oder Rentner.</summary>
    [Display(Name = "ohne Verdienst")] OhneVerdienst,
}

/// <summary>Wie das Mitglied zum Lehrgang gefahren ist.</summary>
public enum Fahrtart
{
    /// <summary>Keine Fahrkosten.</summary>
    [Display(Name = "keine Fahrkosten")] Keine,
    /// <summary>Bahn oder Bus, zweite Klasse, nach Beleg.</summary>
    [Display(Name = "Bahn oder Bus (2. Klasse)")] OeffentlicheVerkehrsmittel,
    /// <summary>Eigenes Kraftfahrzeug, Wegstreckenentschädigung je Kilometer nach LRKG.</summary>
    [Display(Name = "eigenes Auto")] Pkw,
}

/// <summary>Bearbeitungsstand eines Entschädigungsantrags.</summary>
public enum AntragStand
{
    /// <summary>Eingereicht, wartet auf die Prüfung.</summary>
    [Display(Name = "eingereicht")] Eingereicht,
    /// <summary>Genehmigt; geht in die nächste Abrechnung ein.</summary>
    [Display(Name = "genehmigt")] Genehmigt,
    /// <summary>Abgelehnt mit Begründung.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
    /// <summary>Vom Mitglied zurückgezogen.</summary>
    [Display(Name = "zurückgezogen")] Zurueckgezogen,
}

/// <summary>Steuerliche Einordnung eines Entschädigungspostens (V34; OFD Karlsruhe, Leitfaden Ehrenamt 04/2025).</summary>
public enum Steuerart
{
    /// <summary>Aufwandsentschädigung aus öffentlicher Kasse, steuerfrei bis zum Freibetrag (§ 3 Nr. 12 Satz 2 EStG, R 3.12 LStR).</summary>
    [Display(Name = "Aufwandsentschädigung (§ 3 Nr. 12 EStG)")] Aufwandsentschaedigung,
    /// <summary>Ersatz für entgangenen Verdienst (§ 24 Nr. 1 Buchst. a EStG), grundsätzlich steuerpflichtig.</summary>
    [Display(Name = "Verdienstausfall (§ 24 Nr. 1a EStG)")] Verdienstausfall,
    /// <summary>Übungsleiter- und Ausbildertätigkeit, steuerfrei bis zum Freibetrag (§ 3 Nr. 26 EStG).</summary>
    [Display(Name = "Ausbilder, Übungsleiter (§ 3 Nr. 26 EStG)")] Uebungsleiter,
    /// <summary>Ersatz von Fahrkosten und Auslagen gegen Nachweis (§ 3 Nr. 13 EStG).</summary>
    [Display(Name = "Auslagen, Reisekosten (§ 3 Nr. 13 EStG)")] Auslagenersatz,
    /// <summary>Sonstige Leistung (§ 22 Nr. 3 EStG, Freigrenze).</summary>
    [Display(Name = "Sonstiges (§ 22 Nr. 3 EStG)")] Sonstiges,
}

/// <summary>
/// Antrag eines Mitglieds auf Entschädigung nach § 16 FwG (V32). Lehrgänge beantragt das Mitglied selbst; Einsätze gelten über den
/// Einsatzbericht als beantragt, nur Einsätze über mehr als zwei Tage werden hier in tatsächlicher Höhe beantragt. Der Kassier oder
/// Kommandant prüft; genehmigte Anträge gehen in die nächste Abrechnung ein.
/// </summary>
public class Entschaedigungsantrag : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied, das die Entschädigung erhält.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Lehrgang oder mehrtägiger Einsatz.</summary>
    public AntragAnlass Anlass { get; set; }

    /// <summary>Bezeichnung, z. B. „Truppführer“ oder „Unwetter Juli 2027“.</summary>
    public required string Bezeichnung { get; set; }
    /// <summary>Ort, z. B. „Landesfeuerwehrschule Bruchsal“.</summary>
    public string? Ort { get; set; }

    /// <summary>Lehrgangsteilnahme aus der Ausbildung, falls zugeordnet.</summary>
    public Guid? LehrgangsteilnahmeId { get; set; }
    /// <summary>Die zugeordnete Lehrgangsteilnahme.</summary>
    public Lehrgangsteilnahme? Lehrgangsteilnahme { get; set; }

    /// <summary>Einsatz bei einem mehrtägigen Einsatz.</summary>
    public Guid? EinsatzId { get; set; }
    /// <summary>Der Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Die Tage mit Beginn und Ende.</summary>
    public List<AntragTag> Tage { get; set; } = [];

    /// <summary>Erwerbslage des Mitglieds.</summary>
    public Erwerbslage Erwerbslage { get; set; }

    /// <summary>Es ist tatsächlich Verdienstausfall entstanden (Aufschlag je Stunde nach FwES § 2 Abs. 1 Satz 2).</summary>
    public bool Verdienstausfall { get; set; }

    /// <summary>Bei Abrechnung in tatsächlicher Höhe: entgangener Verdienst laut Nachweis des Arbeitgebers oder eigener Aufstellung.</summary>
    public decimal? VerdienstausfallBetrag { get; set; }

    /// <summary>Weitergezahlte Sozialversicherungsbeiträge, die gegen Nachweis erstattet werden.</summary>
    public decimal? SozialversicherungBetrag { get; set; }

    /// <summary>Sonstige notwendige Auslagen in tatsächlicher Höhe, z. B. Verpflegung bei mehrtägigen Einsätzen.</summary>
    public decimal? AuslagenBetrag { get; set; }

    /// <summary>Art der Anreise.</summary>
    public Fahrtart Fahrtart { get; set; }
    /// <summary>Gefahrene Kilometer hin und zurück bei eigenem Auto.</summary>
    public decimal? Kilometer { get; set; }
    /// <summary>Fahrkosten laut Beleg bei Bahn oder Bus.</summary>
    public decimal? Fahrkosten { get; set; }

    /// <summary>Hinweise des Mitglieds; keine Gesundheitsangaben.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Bearbeitungsstand.</summary>
    public AntragStand Stand { get; set; }
    /// <summary>Tag der Einreichung (Ortszeit).</summary>
    public DateOnly EingereichtAm { get; set; }
    /// <summary>Tag der Entscheidung (Ortszeit).</summary>
    public DateOnly? EntschiedenAm { get; set; }
    /// <summary>Konto, das entschieden hat.</summary>
    public Guid? EntschiedenVon { get; set; }
    /// <summary>Begründung einer Ablehnung oder Hinweis zur Genehmigung.</summary>
    public string? Entscheidungsvermerk { get; set; }

    /// <summary>Belege: Teilnahmebescheinigung, Nachweis des Arbeitgebers, Fahrkarten.</summary>
    public List<AntragBeleg> Belege { get; set; } = [];

    /// <summary>Erster Tag des Antrags; null ohne Tage.</summary>
    public DateOnly? ErsterTag => Tage.Count == 0 ? null : Tage.Min(t => Ortszeit.Datum(t.Von));

    /// <summary>Längste Folge aufeinanderfolgender Kalendertage (Ortszeit) über alle Tage des Antrags.</summary>
    public int AufeinanderfolgendeTage
    {
        get
        {
            var tage = Tage.SelectMany(t => Tageliste(t.Von, t.Bis)).Distinct().Order().ToList();
            var laengste = 0;
            var lauf = 0;
            for (var i = 0; i < tage.Count; i++)
            {
                lauf = i > 0 && tage[i].DayNumber == tage[i - 1].DayNumber + 1 ? lauf + 1 : 1;
                laengste = Math.Max(laengste, lauf);
            }

            return laengste;
        }
    }

    /// <summary>Wird in tatsächlicher Höhe statt nach Durchschnittssatz abgerechnet (mehr als zwei aufeinanderfolgende Tage, § 16 Abs. 4 FwG).</summary>
    /// <param name="grenzeTage">Über so vielen aufeinanderfolgenden Kalendertagen gilt die tatsächliche Höhe; 0 = nie.</param>
    /// <returns><c>true</c> bei Einzelabrechnung in tatsächlicher Höhe.</returns>
    public bool InTatsaechlicherHoehe(int grenzeTage) => grenzeTage > 0 && AufeinanderfolgendeTage > grenzeTage;

    /// <summary>Gründe, warum der Antrag so nicht eingereicht werden kann.</summary>
    /// <param name="grenzeTage">Grenze für die tatsächliche Höhe in Kalendertagen.</param>
    /// <returns>Fehlermeldungen; leer, wenn alles passt.</returns>
    public IReadOnlyList<string> Pruefen(int grenzeTage)
    {
        var fehler = new List<string>();
        if (Tage.Count == 0)
        {
            fehler.Add("Bitte mindestens einen Tag mit Beginn und Ende angeben.");
        }

        if (Tage.Any(t => t.Bis <= t.Von))
        {
            fehler.Add("Das Ende muss nach dem Beginn liegen.");
        }

        if (Anlass == AntragAnlass.Einsatz && Tage.Count > 0 && !InTatsaechlicherHoehe(grenzeTage))
        {
            fehler.Add("Einsätze bis zu dieser Dauer rechnet der Einsatzbericht ab; ein eigener Antrag ist nicht nötig.");
        }

        if (Fahrtart == Fahrtart.Pkw && Kilometer is not > 0)
        {
            fehler.Add("Bitte die gefahrenen Kilometer angeben.");
        }

        if (Fahrtart == Fahrtart.OeffentlicheVerkehrsmittel && Fahrkosten is not > 0)
        {
            fehler.Add("Bitte die Fahrkosten laut Fahrkarte angeben.");
        }

        return fehler;
    }

    private static IEnumerable<DateOnly> Tageliste(DateTimeOffset von, DateTimeOffset bis)
    {
        var erster = Ortszeit.Datum(von);
        var letzter = Ortszeit.Datum(bis > von ? bis.AddTicks(-1) : von);
        for (var tag = erster; tag <= letzter; tag = tag.AddDays(1))
        {
            yield return tag;
        }
    }
}

/// <summary>Ein Tag eines Antrags mit Beginn und Ende (UTC), z. B. Unterrichtsbeginn und -ende.</summary>
public class AntragTag : Entitaet
{
    /// <summary>Schlüssel des Antrags.</summary>
    public Guid EntschaedigungsantragId { get; set; }
    /// <summary>Beginn in UTC.</summary>
    public DateTimeOffset Von { get; set; }
    /// <summary>Ende in UTC.</summary>
    public DateTimeOffset Bis { get; set; }
}

/// <summary>Beleg zu einem Entschädigungsantrag, z. B. Teilnahmebescheinigung oder Nachweis des Arbeitgebers.</summary>
public class AntragBeleg : Entitaet
{
    /// <summary>Schlüssel des Antrags.</summary>
    public Guid EntschaedigungsantragId { get; set; }
    /// <summary>Bereinigter Dateiname.</summary>
    public required string Dateiname { get; set; }
    /// <summary>MIME-Typ.</summary>
    public required string Inhaltstyp { get; set; }
    /// <summary>Inhalt.</summary>
    [NichtProtokollieren]
    public required byte[] Inhalt { get; set; }
    /// <summary>SHA-256 des Inhalts als Hex-Text.</summary>
    public required string Pruefsumme { get; set; }
}
