using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Dienstbetrieb;

/// <summary>
/// Angaben einer Veranstaltung der Jugend- oder Kinderfeuerwehr (V48) zu einem Dienst der Art Fahrt, Zeltlager, Ausflug, Elternabend oder
/// Kreisveranstaltung: Anmeldung über das Elternkonto oder auf Papier, Einwilligung der Eltern je Teilnehmer, Teilnehmerbeitrag, Betreuer
/// und Zuschuss. Nur angesetzte Veranstaltungen sind über die Unfallkasse versichert, deshalb hängt jede an einem Dienst.
/// </summary>
public class Jugendveranstaltung : Entitaet
{
    /// <summary>Schlüssel des Dienstes.</summary>
    public Guid DienstId { get; set; }
    /// <summary>Der Dienst mit Zeit, Ort und Zielgruppe.</summary>
    public Dienst? Dienst { get; set; }

    /// <summary>Mit Übernachtung; dann muss jeder Betreuer ein gültiges Führungszeugnis haben (§ 72a SGB VIII).</summary>
    public bool Uebernachtung { get; set; }
    /// <summary>Eltern melden ihr Kind an; ohne Anmeldung genügt die Zu- oder Absage.</summary>
    public bool AnmeldungNoetig { get; set; } = true;
    /// <summary>Anmeldeschluss; danach melden nur noch Jugendwart oder Kommandant an.</summary>
    public DateOnly? AnmeldungBis { get; set; }
    /// <summary>Die Eltern willigen bei der Anmeldung ausdrücklich in die Teilnahme ein (Fahrt, Zeltlager).</summary>
    public bool EinwilligungNoetig { get; set; } = true;
    /// <summary>Teilnehmerbeitrag in Euro; leer, wenn die Teilnahme nichts kostet.</summary>
    public decimal? Teilnehmerbeitrag { get; set; }

    /// <summary>Zuschussgeber, bei dem der Zuschuss beantragt wird.</summary>
    public Guid? ZuschussgeberId { get; set; }
    /// <summary>Der Zuschussgeber.</summary>
    public Zuschussgeber? Zuschussgeber { get; set; }
    /// <summary>Tag, an dem der Zuschuss beantragt wurde.</summary>
    public DateOnly? ZuschussBeantragtAm { get; set; }
    /// <summary>Bewilligter oder ausgezahlter Zuschuss in Euro.</summary>
    public decimal? ZuschussBetrag { get; set; }
    /// <summary>Freie Bemerkung, z. B. Treffpunkt oder Packliste.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Angemeldete Kinder und Jugendliche.</summary>
    public List<Veranstaltungsanmeldung> Anmeldungen { get; set; } = [];
    /// <summary>Eingeteilte Betreuer aus dem Betreuer-Register.</summary>
    public List<Veranstaltungsbetreuer> Betreuer { get; set; } = [];

    /// <summary>Eltern können am Stichtag selbst anmelden.</summary>
    /// <param name="heute">Stichtag in Ortszeit.</param>
    /// <param name="ersterTag">Erster Tag der Veranstaltung in Ortszeit.</param>
    /// <returns><c>true</c>, solange angemeldet werden kann.</returns>
    public bool AnmeldungOffen(DateOnly heute, DateOnly ersterTag) =>
        AnmeldungNoetig && Dienst?.Status == DienstStatus.Geplant && heute <= (AnmeldungBis ?? ersterTag.AddDays(-1));
}

/// <summary>Wie eine Anmeldung zu einer Jugendveranstaltung eingegangen ist.</summary>
public enum Anmeldeweg
{
    /// <summary>Eltern haben im Elternkonto angemeldet.</summary>
    [Display(Name = "Elternkonto")] Elternkonto,
    /// <summary>Anmeldezettel auf Papier, vom Jugendwart eingetragen.</summary>
    [Display(Name = "Papier")] Papier,
}

/// <summary>Anmeldung eines Kindes oder Jugendlichen zu einer Jugendveranstaltung (V48).</summary>
public class Veranstaltungsanmeldung : Entitaet
{
    /// <summary>Schlüssel der Veranstaltung.</summary>
    public Guid JugendveranstaltungId { get; set; }
    /// <summary>Die Veranstaltung.</summary>
    public Jugendveranstaltung? Jugendveranstaltung { get; set; }
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Elternkonto oder Papier.</summary>
    public Anmeldeweg Weg { get; set; }
    /// <summary>Tag der Anmeldung.</summary>
    public DateOnly AngemeldetAm { get; set; }
    /// <summary>Wer angemeldet hat, z. B. Name des Elternteils.</summary>
    public string? AngemeldetVon { get; set; }
    /// <summary>Die Eltern haben in die Teilnahme eingewilligt.</summary>
    public bool Einwilligung { get; set; }
    /// <summary>Teilnahmetage, wenn nicht die ganze Veranstaltung; zählt für den Zuschuss.</summary>
    public int? Tage { get; set; }
    /// <summary>Tag, an dem der Teilnehmerbeitrag eingegangen ist.</summary>
    public DateOnly? BezahltAm { get; set; }
}

/// <summary>Betreuer einer Jugendveranstaltung, aus dem Betreuer-Register (V47, V48).</summary>
public class Veranstaltungsbetreuer : Entitaet
{
    /// <summary>Schlüssel der Veranstaltung.</summary>
    public Guid JugendveranstaltungId { get; set; }
    /// <summary>Die Veranstaltung.</summary>
    public Jugendveranstaltung? Jugendveranstaltung { get; set; }
    /// <summary>Schlüssel des Betreuers.</summary>
    public Guid JugendbetreuerId { get; set; }
    /// <summary>Der Betreuer.</summary>
    public Jugendbetreuer? Jugendbetreuer { get; set; }
    /// <summary>Betreuungstage, wenn nicht die ganze Veranstaltung.</summary>
    public int? Tage { get; set; }
}

/// <summary>
/// Zuschussgeber für Veranstaltungen der Jugend (V48), z. B. Jugendpflege des Landkreises oder Kreisjugendring. Sätze, Altersgrenzen und
/// Fristen sind Stammdaten und bleiben leer, bis sie bekannt sind; der Fristrechner rechnet nur mit dem, was eingetragen ist.
/// </summary>
public class Zuschussgeber : Entitaet
{
    /// <summary>Name, z. B. „Kreisjugendring Göppingen“.</summary>
    public required string Name { get; set; }
    /// <summary>Zuschuss je Teilnehmer und Tag in Euro.</summary>
    public decimal? SatzJeTeilnehmertag { get; set; }
    /// <summary>Teilnehmer zählen ab diesem Alter (am ersten Tag).</summary>
    public int? MindestalterJahre { get; set; }
    /// <summary>Teilnehmer zählen bis einschließlich dieses Alters (am ersten Tag).</summary>
    public int? HoechstalterJahre { get; set; }
    /// <summary>Die Veranstaltung muss mindestens so viele Tage dauern.</summary>
    public int? MindestTage { get; set; }
    /// <summary>Es müssen mindestens so viele Teilnehmer zählen.</summary>
    public int? MindestTeilnehmer { get; set; }
    /// <summary>Betreuer zählen als Teilnehmer.</summary>
    public bool BetreuerZaehlen { get; set; }
    /// <summary>Antrag spätestens so viele Tage nach dem letzten Tag.</summary>
    public int? AntragsfristTage { get; set; }
    /// <summary>So viele Monate nach der Veranstaltung sind Teilnehmerlisten als Nachweis aufzubewahren (L55).</summary>
    public int? AufbewahrungMonate { get; set; }
    /// <summary>Freie Bemerkung, z. B. Ansprechpartner oder Fundstelle der Richtlinie.</summary>
    public string? Bemerkung { get; set; }
    /// <summary>Wählbar.</summary>
    public bool Aktiv { get; set; } = true;
}

/// <summary>Berechnung des Zuschusses einer Jugendveranstaltung nach den Angaben des Zuschussgebers (V48).</summary>
public static class Zuschussrechner
{
    /// <summary>Ergebnis des Fristrechners.</summary>
    /// <param name="Tage">Dauer der Veranstaltung in Kalendertagen.</param>
    /// <param name="Teilnehmer">Teilnehmer, die zählen (mit Betreuern, falls der Zuschussgeber sie zählt).</param>
    /// <param name="Teilnehmertage">Summe der Teilnahmetage, die zählen.</param>
    /// <param name="Zuschuss">Erwarteter Zuschuss; leer ohne Satz oder wenn eine Voraussetzung fehlt.</param>
    /// <param name="AntragBis">Letzter Tag für den Antrag; leer ohne Frist.</param>
    /// <param name="Hinweise">Was fehlt oder nicht passt.</param>
    public sealed record Ergebnis(int Tage, int Teilnehmer, int Teilnehmertage, decimal? Zuschuss, DateOnly? AntragBis, IReadOnlyList<string> Hinweise);

    /// <summary>Ein Teilnehmer für die Berechnung.</summary>
    /// <param name="Geburtsdatum">Geburtsdatum; leer bei Betreuern.</param>
    /// <param name="Tage">Teilnahmetage oder leer für die ganze Veranstaltung.</param>
    /// <param name="IstBetreuer">Betreuer statt Teilnehmer.</param>
    public sealed record Person(DateOnly? Geburtsdatum, int? Tage, bool IstBetreuer);

    /// <summary>Rechnet Teilnehmertage, Zuschuss und Antragsfrist.</summary>
    /// <param name="geber">Zuschussgeber mit Sätzen und Fristen.</param>
    /// <param name="ersterTag">Erster Tag.</param>
    /// <param name="letzterTag">Letzter Tag.</param>
    /// <param name="personen">Angemeldete Teilnehmer und Betreuer.</param>
    /// <returns>Das Ergebnis.</returns>
    public static Ergebnis Rechnen(Zuschussgeber geber, DateOnly ersterTag, DateOnly letzterTag, IEnumerable<Person> personen)
    {
        var dauer = Math.Max(1, letzterTag.DayNumber - ersterTag.DayNumber + 1);
        var hinweise = new List<string>();
        var zaehlen = personen.Where(p => p.IstBetreuer ? geber.BetreuerZaehlen : AlterPasst(geber, p.Geburtsdatum, ersterTag)).ToList();
        var teilnehmertage = zaehlen.Sum(p => Math.Clamp(p.Tage ?? dauer, 0, dauer));
        var moeglich = true;
        if (geber.MindestTage is { } mindestTage && dauer < mindestTage)
        {
            hinweise.Add($"Die Veranstaltung dauert {dauer} Tage, gefördert wird erst ab {mindestTage}.");
            moeglich = false;
        }

        if (geber.MindestTeilnehmer is { } mindestTeilnehmer && zaehlen.Count < mindestTeilnehmer)
        {
            hinweise.Add($"Es zählen {zaehlen.Count} Teilnehmer, nötig sind {mindestTeilnehmer}.");
            moeglich = false;
        }

        if (geber.SatzJeTeilnehmertag is null)
        {
            hinweise.Add("Beim Zuschussgeber ist noch kein Satz je Teilnehmer und Tag eingetragen.");
        }

        if (geber.AntragsfristTage is null)
        {
            hinweise.Add("Beim Zuschussgeber ist noch keine Antragsfrist eingetragen.");
        }

        return new Ergebnis(dauer, zaehlen.Count, teilnehmertage,
            moeglich && geber.SatzJeTeilnehmertag is { } satz ? Math.Round(satz * teilnehmertage, 2) : null,
            geber.AntragsfristTage is { } frist ? letzterTag.AddDays(frist) : null,
            hinweise);
    }

    /// <summary>Alter am ersten Tag liegt in den Grenzen des Zuschussgebers; ohne Geburtsdatum zählt nur, wer keine Grenze kennt.</summary>
    private static bool AlterPasst(Zuschussgeber geber, DateOnly? geburtsdatum, DateOnly stichtag)
    {
        if (geburtsdatum is not { } geboren)
        {
            return geber.MindestalterJahre is null && geber.HoechstalterJahre is null;
        }

        var alter = Mitglied.Alter(geboren, stichtag);
        return (geber.MindestalterJahre is not { } min || alter >= min) && (geber.HoechstalterJahre is not { } max || alter <= max);
    }
}
