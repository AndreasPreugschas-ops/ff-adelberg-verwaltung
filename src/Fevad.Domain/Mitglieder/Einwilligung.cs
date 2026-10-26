using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>
/// Art einer Einwilligung im Einwilligungsregister (V39, V45), z. B. Fotos auf der Homepage, Kommunikation über WhatsApp oder
/// Mitnahme im privaten Pkw. Fotos haben je Kanal eine eigene Art (Entscheidung Andi, 25.10.2026: alle Kanäle werden genutzt).
/// </summary>
public class Einwilligungsart : Katalogeintrag
{
    /// <summary>Code der Art „Fotos auf der Homepage“.</summary>
    public const string CodeFotoHomepage = "FOTO-HP";
    /// <summary>Code der Art „Fotos in sozialen Medien“; im Formular mit den Kanälen aus dem Parameter.</summary>
    public const string CodeFotoSozialeMedien = "FOTO-SM";
    /// <summary>Code der Art „Kommunikation über WhatsApp“.</summary>
    public const string CodeWhatsApp = "WA";
    /// <summary>Code der Art „Kontaktliste für Mitglieder“ (V42): Mobilnummer und Mail für die anderen Mitglieder sichtbar.</summary>
    public const string CodeKontaktliste = "KONTAKT";

    /// <summary>Wortlaut, in den eingewilligt wird; erscheint im Register und im Konto.</summary>
    public string? Beschreibung { get; set; }
    /// <summary>Nur für die Jugend- und Kinderfeuerwehr abgefragt (Fahrt, Medikamentengabe, Baden …).</summary>
    public bool NurJugend { get; set; }
    /// <summary>Nur Volljährige können einwilligen; bei Minderjährigen wird die Art nicht angeboten (z. B. Kontaktliste).</summary>
    public bool NurVolljaehrige { get; set; }
    /// <summary>Einwilligung in die Veröffentlichung von Fotos (§ 22 KUG).</summary>
    public bool Foto { get; set; }
    /// <summary>Wird im Aufnahmeantrag abgefragt (Baustein C); die Medikamentengabe hat ein eigenes Formular.</summary>
    public bool ImAntrag { get; set; } = true;
    /// <summary>Folgeaufgabe nach einem Widerruf, z. B. „aus der WhatsApp-Gruppe entfernen“; leer, wenn nichts zu tun ist.</summary>
    public string? HinweisBeiWiderruf { get; set; }
}

/// <summary>Wer die Einwilligung erklärt hat.</summary>
public enum EinwilligungDurch
{
    /// <summary>Das Mitglied selbst.</summary>
    [Display(Name = "Mitglied selbst")] Mitglied,
    /// <summary>Ein Erziehungsberechtigter für das minderjährige Mitglied.</summary>
    [Display(Name = "Erziehungsberechtigte")] Erziehungsberechtigter,
}

/// <summary>Auf welchem Weg die Erklärung kam.</summary>
public enum EinwilligungWeg
{
    /// <summary>Unterschriebenes Papier, eingetragen von der Verwaltung.</summary>
    [Display(Name = "Papier")] Papier,
    /// <summary>Aus dem angenommenen Aufnahmeantrag.</summary>
    [Display(Name = "Antrag")] Antrag,
    /// <summary>Selbst im eigenen Konto oder im Elternkonto erklärt.</summary>
    [Display(Name = "Konto")] Konto,
}

/// <summary>
/// Eine Erklärung im Einwilligungsregister: erteilt oder abgelehnt, mit Datum, Person und Weg als Nachweis nach Art. 7 Abs. 1 DSGVO.
/// Jede neue Erklärung ist ein neuer Eintrag, so bleibt der Verlauf nachvollziehbar; ein Widerruf setzt <see cref="WiderrufenAm"/>.
/// Löschung nach L51.
/// </summary>
public class Einwilligung : Entitaet
{
    /// <summary>Mitglied, für das die Erklärung gilt.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }
    /// <summary>Art der Einwilligung.</summary>
    public Guid EinwilligungsartId { get; set; }
    /// <summary>Die Art.</summary>
    public Einwilligungsart? Einwilligungsart { get; set; }

    /// <summary>Eingewilligt (<c>true</c>) oder ausdrücklich abgelehnt (<c>false</c>).</summary>
    public bool Erteilt { get; set; }
    /// <summary>Tag der Erklärung.</summary>
    public DateOnly Am { get; set; }
    /// <summary>Wer erklärt hat.</summary>
    public EinwilligungDurch Durch { get; set; }
    /// <summary>Name der erklärenden Person, z. B. des Elternteils.</summary>
    public string? DurchName { get; set; }
    /// <summary>Weg der Erklärung.</summary>
    public EinwilligungWeg Weg { get; set; }
    /// <summary>Konto, über das die Erklärung im Programm abgegeben wurde.</summary>
    public Guid? KontoId { get; set; }
    /// <summary>Ablageort des Papiers oder Hinweis zum Nachweis, z. B. „Ordner Jugend 2026“.</summary>
    public string? Nachweis { get; set; }
    /// <summary>
    /// Der Jugendliche hat selbst zugestimmt; ab dem Alter aus dem Parameter Jugend.Einwilligung.MitentscheidungAbAlter nötig,
    /// damit die Erklärung der Eltern wirkt. Leer, wenn nicht gefragt.
    /// </summary>
    public bool? JugendlicherZugestimmt { get; set; }

    /// <summary>Tag des Widerrufs; leer, solange nicht widerrufen.</summary>
    public DateOnly? WiderrufenAm { get; set; }
    /// <summary>Wer widerrufen hat.</summary>
    public string? WiderrufenDurch { get; set; }
    /// <summary>Tag, an dem die Folgeaufgabe des Widerrufs (<see cref="Einwilligungsart.HinweisBeiWiderruf"/>) erledigt wurde.</summary>
    public DateOnly? FolgeaufgabeErledigtAm { get; set; }
    /// <summary>Wer die Folgeaufgabe erledigt hat.</summary>
    public string? FolgeaufgabeErledigtVon { get; set; }
}

/// <summary>Stand einer Einwilligungsart für ein Mitglied.</summary>
public enum Einwilligungsstatus
{
    /// <summary>Keine Erklärung im Register.</summary>
    [Display(Name = "fehlt")] Fehlt,
    /// <summary>Eingewilligt und wirksam.</summary>
    [Display(Name = "erteilt")] Erteilt,
    /// <summary>Eltern haben eingewilligt, der Jugendliche muss noch zustimmen.</summary>
    [Display(Name = "Zustimmung des Jugendlichen fehlt")] ZustimmungFehlt,
    /// <summary>Ausdrücklich abgelehnt.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
    /// <summary>Widerrufen.</summary>
    [Display(Name = "widerrufen")] Widerrufen,
}

/// <summary>
/// Regeln des Einwilligungsregisters (Rückfrage B, Empfehlung): bei Minderjährigen genügt die Erklärung eines Elternteils, der
/// Widerruf eines Elternteils wirkt sofort, ab dem Mitentscheidungsalter muss der Jugendliche zusätzlich zustimmen.
/// </summary>
public static class Einwilligungsregeln
{
    /// <summary>Die maßgebliche Erklärung: die jüngste nach Tag und Anlage.</summary>
    /// <param name="eintraege">Erklärungen einer Art für ein Mitglied.</param>
    /// <returns>Die jüngste Erklärung oder null.</returns>
    public static Einwilligung? Aktuell(IEnumerable<Einwilligung> eintraege) =>
        eintraege.OrderByDescending(e => e.Am).ThenByDescending(e => e.Id).FirstOrDefault();

    /// <summary>Stand einer Art am Stichtag.</summary>
    /// <param name="eintraege">Erklärungen einer Art für ein Mitglied.</param>
    /// <param name="alter">Alter des Mitglieds am Stichtag.</param>
    /// <param name="volljaehrigAb">Alter der Volljährigkeit.</param>
    /// <param name="mitentscheidungAb">Ab diesem Alter muss der Jugendliche zustimmen.</param>
    /// <returns>Der Stand.</returns>
    public static Einwilligungsstatus Status(IEnumerable<Einwilligung> eintraege, int alter, int volljaehrigAb, int mitentscheidungAb)
    {
        var e = Aktuell(eintraege);
        if (e is null)
        {
            return Einwilligungsstatus.Fehlt;
        }

        if (e.WiderrufenAm is not null)
        {
            return Einwilligungsstatus.Widerrufen;
        }

        if (!e.Erteilt)
        {
            return Einwilligungsstatus.Abgelehnt;
        }

        return alter < volljaehrigAb && alter >= mitentscheidungAb && e.Durch == EinwilligungDurch.Erziehungsberechtigter && e.JugendlicherZugestimmt != true
            ? Einwilligungsstatus.ZustimmungFehlt
            : Einwilligungsstatus.Erteilt;
    }

    /// <summary>Arten, die das Formular eines Antrags abfragt: aktiv, im Antrag, die nur für die Jugend gelten nur bei der Jugendaufnahme.</summary>
    /// <param name="arten">Alle Arten.</param>
    /// <param name="art">Art des Antrags.</param>
    /// <returns>Die Arten in ihrer Reihenfolge.</returns>
    public static IReadOnlyList<Einwilligungsart> FuerAntrag(IEnumerable<Einwilligungsart> arten, AntragArt art) =>
        !Antragsregeln.MitEinwilligungen(art)
            ? []
            : arten.Where(a => a.Aktiv && a.ImAntrag && (!a.NurJugend || Antragsregeln.IstJugendaufnahme(art)))
                .OrderBy(a => a.Sortierung).ThenBy(a => a.Bezeichnung).ToList();

    /// <summary>Wer selbst im Konto erteilen darf: Volljährige für sich, Eltern für ihr Kind; Minderjährige nur widerrufen oder zustimmen.</summary>
    /// <param name="selbst">Die Erklärung betrifft das eigene Mitglied.</param>
    /// <param name="alter">Alter des Mitglieds.</param>
    /// <param name="volljaehrigAb">Alter der Volljährigkeit.</param>
    /// <returns><c>true</c>, wenn erteilt werden darf.</returns>
    public static bool DarfErteilen(bool selbst, int alter, int volljaehrigAb) => !selbst || alter >= volljaehrigAb;
}

/// <summary>Antwort im Antrag zu einer Einwilligungsart; nur angekreuzte Arten haben einen Eintrag.</summary>
public class AntragEinwilligung : Entitaet
{
    /// <summary>Schlüssel des Antrags.</summary>
    public Guid AntragId { get; set; }
    /// <summary>Der Antrag.</summary>
    public Antrag? Antrag { get; set; }
    /// <summary>Art der Einwilligung.</summary>
    public Guid EinwilligungsartId { get; set; }
    /// <summary>Die Art.</summary>
    public Einwilligungsart? Einwilligungsart { get; set; }
    /// <summary>Ja (<c>true</c>) oder nein (<c>false</c>).</summary>
    public bool Erteilt { get; set; }
}
