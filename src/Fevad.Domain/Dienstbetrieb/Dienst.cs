using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Dienstbetrieb;

/// <summary>Art eines Dienstes oder Termins.</summary>
public enum DienstArt
{
    /// <summary>Praktische Übung.</summary>
    [Display(Name = "Übung")] Uebung,
    /// <summary>Theoretischer Unterricht.</summary>
    [Display(Name = "Unterricht")] Unterricht,
    /// <summary>Sonderdienst, z. B. Brandsicherheitswache oder Absicherung.</summary>
    [Display(Name = "Sonderdienst")] Sonderdienst,
    /// <summary>Arbeitsdienst, z. B. Gerätehauspflege.</summary>
    [Display(Name = "Arbeitsdienst")] Arbeitsdienst,
    /// <summary>Sitzung, z. B. des Feuerwehrausschusses.</summary>
    [Display(Name = "Sitzung")] Sitzung,
    /// <summary>Hauptversammlung.</summary>
    [Display(Name = "Hauptversammlung")] Hauptversammlung,
    /// <summary>Veranstaltung, z. B. Fest oder Tag der offenen Tür.</summary>
    [Display(Name = "Veranstaltung")] Veranstaltung,
    /// <summary>Externer Lehrgang.</summary>
    [Display(Name = "Lehrgang (extern)")] Lehrgang,
    /// <summary>Fahrt der Jugend- oder Kinderfeuerwehr, z. B. Freizeit (V48).</summary>
    [Display(Name = "Fahrt")] Fahrt,
    /// <summary>Zeltlager der Jugend, mit Übernachtung (V48).</summary>
    [Display(Name = "Zeltlager")] Zeltlager,
    /// <summary>Ausflug der Jugend ohne Übernachtung (V48).</summary>
    [Display(Name = "Ausflug")] Ausflug,
    /// <summary>Elternabend der Jugend- oder Kinderfeuerwehr (V48).</summary>
    [Display(Name = "Elternabend")] Elternabend,
    /// <summary>Veranstaltung der Kreisjugendfeuerwehr, z. B. Kreiszeltlager oder Leistungsspange (V48).</summary>
    [Display(Name = "Kreisveranstaltung")] Kreisveranstaltung,
    /// <summary>Brandsicherheitswache nach § 2 Abs. 2 FwG, z. B. Sonnwendfeuer oder Veranstaltung im Kloster (V25, F24); mit Auftraggeber und Leistungsnachweis.</summary>
    [Display(Name = "Brandsicherheitswache")] Brandsicherheitswache,
    /// <summary>Unterweisung nach DGUV Vorschrift 1 § 4 mit Pflichtthemen (V53, F22); der Abschluss trägt die Nachweise ein.</summary>
    [Display(Name = "Unterweisung")] Unterweisung,
    /// <summary>Dienstsport (V25).</summary>
    [Display(Name = "Dienstsport")] Dienstsport,
    /// <summary>Brandschutzerziehung, z. B. in Kindergarten oder Schule (V25).</summary>
    [Display(Name = "Brandschutzerziehung")] Brandschutzerziehung,
}

/// <summary>Dienstarten der Jugend mit Anmeldung, Betreuern und Zuschuss (V48).</summary>
public static class Jugenddienstarten
{
    /// <summary>Die Arten, zu denen eine Jugendveranstaltung gehört.</summary>
    public static readonly IReadOnlyList<DienstArt> Alle = [DienstArt.Fahrt, DienstArt.Zeltlager, DienstArt.Ausflug, DienstArt.Elternabend, DienstArt.Kreisveranstaltung];

    /// <summary>Gehört die Art zur Jugend.</summary>
    /// <param name="art">Die Dienstart.</param>
    /// <returns><c>true</c> für Fahrt, Zeltlager, Ausflug, Elternabend und Kreisveranstaltung.</returns>
    public static bool Ist(DienstArt art) => Alle.Contains(art);
}

/// <summary>Durchführungsstand eines Dienstes.</summary>
public enum DienstStatus
{
    /// <summary>Geplant.</summary>
    [Display(Name = "geplant")] Geplant,
    /// <summary>Durchgeführt; wird nie automatisch gesetzt, auch nicht bei rückdatierten Diensten.</summary>
    [Display(Name = "durchgeführt")] Durchgefuehrt,
    /// <summary>Abgesagt; keine Rückmeldungen mehr möglich.</summary>
    [Display(Name = "abgesagt")] Abgesagt,
}

/// <summary>Übung, Unterricht, Sitzung oder sonstiger Dienst mit Anwesenheitsliste.</summary>
public class Dienst : Entitaet
{
    /// <summary>Art des Dienstes.</summary>
    public DienstArt Art { get; set; }
    /// <summary>Titel für Kalender und Listen.</summary>
    public required string Titel { get; set; }
    /// <summary>Thema oder Inhalt.</summary>
    public string? Thema { get; set; }
    /// <summary>Beginn in UTC.</summary>
    public DateTimeOffset Beginn { get; set; }
    /// <summary>Ende in UTC.</summary>
    public DateTimeOffset Ende { get; set; }
    /// <summary>Ort, z. B. Gerätehaus.</summary>
    public string? Ort { get; set; }

    /// <summary>Zielgruppe; leer bedeutet alle Abteilungen. Mehrere, wenn Abteilungen zusammen üben.</summary>
    public List<Abteilung> Abteilungen { get; set; } = [];

    /// <summary>Zielgruppe zusätzlich auf eine Gruppe eingeschränkt (z. B. Gruppe 1).</summary>
    public Guid? GruppeId { get; set; }
    /// <summary>Die Gruppe der Zielgruppe.</summary>
    public Gruppe? Gruppe { get; set; }

    /// <summary>Zielgruppe zusätzlich auf Träger einer gültigen Qualifikation eingeschränkt (z. B. AGT, Maschinist).</summary>
    public Guid? QualifikationId { get; set; }
    /// <summary>Die Qualifikation der Zielgruppe.</summary>
    public Qualifikation? Qualifikation { get; set; }

    /// <summary>Gehört zu einer Serie wiederkehrender Termine.</summary>
    public Guid? SerieId { get; set; }
    /// <summary>Die Serie, aus der der Termin stammt.</summary>
    public DienstSerie? Serie { get; set; }

    /// <summary>Mitglieder der Zielgruppe sollen zu- oder absagen.</summary>
    public bool RueckmeldungErbeten { get; set; } = true;

    /// <summary>Übungsleiter: darf Rückmeldungen und Anwesenheit seines Dienstes immer ändern, auch nach dem Rückmeldeschluss.</summary>
    public Guid? VerantwortlicherId { get; set; }
    /// <summary>Der Übungsleiter.</summary>
    public Mitglied? Verantwortlicher { get; set; }

    /// <summary>Als Dienst geplant: mit Anwesenheitsliste, eine Absage steht dort gleich als entschuldigt. Sonst nur ein Termin im Kalender.</summary>
    public bool AlsDienst { get; set; } = true;

    /// <summary>Nur ein Kennzeichen, dass der Dienst fertig bearbeitet ist; er bleibt für alle sichtbar und änderbar.</summary>
    public bool Abgeschlossen { get; set; }

    /// <summary>Durchführungsstand.</summary>
    public DienstStatus Status { get; set; }

    /// <summary>Zählt für die jährliche Standortfortbildung (40 Stunden).</summary>
    public bool ZaehltFuerFortbildung { get; set; } = true;
    /// <summary>Bericht über den Dienst.</summary>
    public string? Bericht { get; set; }

    /// <summary>Themen einer Unterweisung (V53); nur bei <see cref="DienstArt.Unterweisung"/> belegt.</summary>
    public List<Unterweisungsthema> Unterweisungsthemen { get; set; } = [];

    /// <summary>Auftraggeber einer Brandsicherheitswache (Veranstalter), Name und Anschrift; Grundlage für die Rechnung der Gemeindekasse (V25, E16).</summary>
    public string? Auftraggeber { get; set; }

    /// <summary>Ansprechpartner des Auftraggebers mit Telefon oder E-Mail.</summary>
    public string? AuftraggeberKontakt { get; set; }

    /// <summary>Thema der Standortausbildung für den Jahresausbildungsplan (V53).</summary>
    public Guid? AusbildungsthemaId { get; set; }
    /// <summary>Navigationsverweis auf das Ausbildungsthema.</summary>
    public Ausbildungsthema? Ausbildungsthema { get; set; }

    /// <summary>Lernziele der Übung oder des Unterrichts (V53).</summary>
    public string? Lernziele { get; set; }

    /// <summary>Benötigte Fahrzeuge und Material (V53).</summary>
    public string? FahrzeugeUndMaterial { get; set; }

    /// <summary>Kostensatz (Personal je Stunde) für den Leistungsnachweis der Brandsicherheitswache.</summary>
    public Guid? KostensatzId { get; set; }

    /// <summary>Anwesenheitsliste.</summary>
    public List<Anwesenheit> Anwesenheiten { get; set; } = [];
    /// <summary>Zu- und Absagen der Mitglieder.</summary>
    public List<Rueckmeldung> Rueckmeldungen { get; set; } = [];

    /// <summary>Geplante Dauer von Beginn bis Ende.</summary>
    public TimeSpan Dauer => Ende - Beginn;

    /// <summary>Schlüssel der Zielabteilungen; erwartet geladene <see cref="Abteilungen"/>.</summary>
    public IReadOnlyList<Guid> AbteilungIds => Abteilungen.Select(a => a.Id).ToList();

    /// <summary>Zu- und Absagen sind bis <paramref name="schlussStunden"/> Stunden vor Beginn möglich, nicht bei abgesagten Diensten.</summary>
    /// <param name="jetzt">Aktueller Zeitpunkt.</param>
    /// <param name="schlussStunden">Rückmeldeschluss in Stunden vor Beginn (Parameter).</param>
    /// <returns>true, wenn Mitglieder noch zu- oder absagen können.</returns>
    public bool RueckmeldungMoeglich(DateTimeOffset jetzt, int schlussStunden) =>
        RueckmeldungErbeten && Status == DienstStatus.Geplant && jetzt < Beginn.AddHours(-schlussStunden);

    /// <summary>
    /// Nach dem Rückmeldeschluss kann sich ein Mitglied für einen Dienst mit Anwesenheitsliste noch entschuldigen, bis
    /// <paramref name="nachfristTage"/> Tage nach dem Diensttag (Satzung § 5 Abs. 6: bis zum Folgetag). Nicht bei abgesagten Diensten.
    /// </summary>
    /// <param name="jetzt">Aktueller Zeitpunkt.</param>
    /// <param name="schlussStunden">Rückmeldeschluss in Stunden vor Beginn (Parameter).</param>
    /// <param name="heute">Heutiger Tag in Ortszeit.</param>
    /// <param name="diensttag">Tag des Dienstes in Ortszeit.</param>
    /// <param name="nachfristTage">Nachfrist in Tagen nach dem Diensttag (Parameter).</param>
    /// <returns>true, wenn eine nachträgliche Absage noch angenommen wird.</returns>
    public bool EntschuldigungMoeglich(DateTimeOffset jetzt, int schlussStunden, DateOnly heute, DateOnly diensttag, int nachfristTage) =>
        AlsDienst && Status != DienstStatus.Abgesagt && !RueckmeldungMoeglich(jetzt, schlussStunden) && heute <= diensttag.AddDays(nachfristTage);
}

/// <summary>Anwesenheit eines Mitglieds bei einem Dienst.</summary>
public enum AnwesenheitStatus
{
    /// <summary>Anwesend.</summary>
    [Display(Name = "anwesend")] Anwesend,
    /// <summary>Entschuldigt gefehlt.</summary>
    [Display(Name = "entschuldigt")] Entschuldigt,
    /// <summary>Unentschuldigt gefehlt.</summary>
    [Display(Name = "unentschuldigt")] Unentschuldigt,
}

/// <summary>Wie eine Anwesenheit erfasst wurde.</summary>
public enum ErfassungsWeg
{
    /// <summary>Von Hand in der Weboberfläche.</summary>
    Web,
    /// <summary>Selbst per QR-Code eingecheckt.</summary>
    Qr,
    /// <summary>Im Notbetrieb ohne Verbindung zum Server erfasst.</summary>
    Notbetrieb,

    /// <summary>Aus der Zu- oder Absage des Mitglieds übernommen.</summary>
    Rueckmeldung,
}

/// <summary>Eintrag der Anwesenheitsliste eines Dienstes.</summary>
public class Anwesenheit : Entitaet
{
    /// <summary>Schlüssel des Dienstes.</summary>
    public Guid DienstId { get; set; }
    /// <summary>Der Dienst.</summary>
    public Dienst? Dienst { get; set; }

    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Anwesend, entschuldigt oder unentschuldigt.</summary>
    public AnwesenheitStatus Status { get; set; }

    /// <summary>Leer bedeutet ganze Dienstdauer.</summary>
    public DateTimeOffset? Von { get; set; }
    /// <summary>Ende der Anwesenheit; leer bedeutet bis Dienstende.</summary>
    public DateTimeOffset? Bis { get; set; }
    /// <summary>Funktion beim Dienst, z. B. Ausbilder oder Maschinist.</summary>
    public string? Funktion { get; set; }
    /// <summary>Wie die Anwesenheit erfasst wurde.</summary>
    public ErfassungsWeg ErfasstUeber { get; set; }
    /// <summary>Freitext.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Anwesende Zeit; Null bei Abwesenheit.</summary>
    /// <param name="dienst">Der Dienst, für Beginn und Ende, wenn keine eigenen Zeiten eingetragen sind.</param>
    /// <returns>Die anwesende Zeit.</returns>
    public TimeSpan Stunden(Dienst dienst) => Status == AnwesenheitStatus.Anwesend
        ? (Bis ?? dienst.Ende) - (Von ?? dienst.Beginn)
        : TimeSpan.Zero;
}
