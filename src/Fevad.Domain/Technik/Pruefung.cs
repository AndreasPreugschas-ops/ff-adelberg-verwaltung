using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Technik;

/// <summary>Ergebnis einer Prüfung.</summary>
public enum PruefErgebnis
{
    /// <summary>Bestanden.</summary>
    [Display(Name = "bestanden")] Bestanden,
    /// <summary>Bestanden, aber mit Mängeln.</summary>
    [Display(Name = "bestanden mit Mängeln")] BestandenMitMaengeln,
    /// <summary>Nicht bestanden.</summary>
    [Display(Name = "nicht bestanden")] NichtBestanden,
    /// <summary>Ausgesondert.</summary>
    [Display(Name = "ausgesondert")] Ausgesondert,
}

/// <summary>
/// Prüfprotokoll für ein Gerät oder ein Fahrzeug (genau eines von beiden).
/// Nach Abschluss unveränderlich; Korrekturen durch Storno und neue Prüfung (V58, Signaturersatz bei EDV-Dokumentation).
/// </summary>
public class Pruefung : Entitaet
{
    /// <summary>Geprüftes Gerät.</summary>
    public Guid? GeraetId { get; set; }
    /// <summary>Das Gerät.</summary>
    public Geraet? Geraet { get; set; }

    /// <summary>Geprüftes Fahrzeug.</summary>
    public Guid? FahrzeugId { get; set; }
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Position im Prüfplan, nach der geprüft wurde.</summary>
    public Guid? PruefplanPositionId { get; set; }
    /// <summary>Die Prüfplanposition.</summary>
    public PruefplanPosition? PruefplanPosition { get; set; }

    /// <summary>Art der Prüfung.</summary>
    public Pruefart Pruefart { get; set; }
    /// <summary>Prüfdatum.</summary>
    public DateOnly Datum { get; set; }
    /// <summary>Ergebnis.</summary>
    public PruefErgebnis Ergebnis { get; set; }

    /// <summary>Ausgefüllte Checkliste mit Messwerten als JSON.</summary>
    public string Ergebnisse { get; set; } = "[]";
    /// <summary>Nächste Fälligkeit dieser Prüfart.</summary>
    public DateOnly? NaechsteFaellig { get; set; }

    /// <summary>Befähigte Person nach DGUV 305-002.</summary>
    public Guid? PrueferId { get; set; }
    /// <summary>Das prüfende Mitglied.</summary>
    public Mitglied? Pruefer { get; set; }
    /// <summary>Externer Prüfer, z. B. Firma oder Werkstatt.</summary>
    public string? PrueferExtern { get; set; }

    /// <summary>Prüfung nach Gebrauch im Einsatz.</summary>
    public Guid? EinsatzId { get; set; }

    /// <summary>Das Protokoll ist abgeschlossen und unveränderlich.</summary>
    public bool Abgeschlossen { get; set; }
    /// <summary>Im Notbetrieb erfasst.</summary>
    public bool ErfasstImNotbetrieb { get; set; }
    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Stornierte Prüfungen bleiben im Prüfbuch sichtbar, zählen aber für keine Frist.</summary>
    public DateTimeOffset? StorniertAm { get; set; }

    /// <summary>Benutzerkonto, das storniert hat.</summary>
    public Guid? StorniertVon { get; set; }
    /// <summary>Pflichtangabe beim Storno, z. B. „falsches Gerät gescannt“.</summary>
    public string? Stornogrund { get; set; }

    /// <summary>Die Prüfung ist storniert und zählt für keine Frist.</summary>
    public bool Storniert => StorniertAm is not null;

    /// <summary>Gründe gegen ein Storno; leer heißt, das Storno ist möglich.</summary>
    public string? StornoHindernis() => this switch
    {
        { Storniert: true } => "Die Prüfung ist bereits storniert.",
        { Ergebnis: PruefErgebnis.Ausgesondert } => "Eine Aussonderung wird am Gerät zurückgenommen, nicht durch Storno.",
        _ => null,
    };

    /// <summary>Storniert die Prüfung; wirft, wenn ein Hindernis besteht oder der Grund fehlt.</summary>
    public void Stornieren(Guid benutzerId, string grund, DateTimeOffset jetzt)
    {
        if (StornoHindernis() is { } hindernis)
        {
            throw new InvalidOperationException(hindernis);
        }

        if (string.IsNullOrWhiteSpace(grund))
        {
            throw new ArgumentException("Ein Storno braucht einen Grund.", nameof(grund));
        }

        StorniertAm = jetzt;
        StorniertVon = benutzerId;
        Stornogrund = grund.Trim();
    }

    /// <summary>Eine bestandene Prüfung (auch mit Mängeln) oder eine Aussonderung hebt Sperren bis zur Prüfung auf (nach dem Einsatz, Eingangsprüfung).</summary>
    public bool HebtSperreAuf => Ergebnis is PruefErgebnis.Bestanden or PruefErgebnis.BestandenMitMaengeln or PruefErgebnis.Ausgesondert;

    /// <summary>Belegt die nächste Fälligkeit aus dem Intervall der Position vor, wenn sie noch leer ist.</summary>
    /// <param name="position">Die Prüfplanposition.</param>
    public void NaechsteFaelligkeitVorbelegen(PruefplanPosition position)
    {
        NaechsteFaellig ??= Datum.AddMonths(position.IntervallMonate);
    }
}

/// <summary>Schwere eines Mangels.</summary>
public enum MangelSchwere
{
    /// <summary>Hinweis ohne Einschränkung.</summary>
    [Display(Name = "Hinweis")] Info,
    /// <summary>Gerät nur eingeschränkt nutzbar.</summary>
    [Display(Name = "Einschränkung")] Einschraenkung,
    /// <summary>Gerät außer Betrieb.</summary>
    [Display(Name = "außer Betrieb")] AusserBetrieb,
}

/// <summary>Bearbeitungsstand eines Mangels.</summary>
public enum MangelStatus
{
    /// <summary>Offen.</summary>
    [Display(Name = "offen")] Offen,
    /// <summary>In Arbeit.</summary>
    [Display(Name = "in Arbeit")] InArbeit,
    /// <summary>Behoben.</summary>
    [Display(Name = "behoben")] Behoben,
}

/// <summary>Mangel an einem Gerät oder Fahrzeug.</summary>
public class Mangel : Entitaet
{
    /// <summary>Betroffenes Gerät.</summary>
    public Guid? GeraetId { get; set; }
    /// <summary>Das Gerät.</summary>
    public Geraet? Geraet { get; set; }

    /// <summary>Betroffenes Fahrzeug.</summary>
    public Guid? FahrzeugId { get; set; }
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Meldendes Mitglied.</summary>
    public Guid? GemeldetVonId { get; set; }
    /// <summary>Das meldende Mitglied.</summary>
    public Mitglied? GemeldetVon { get; set; }
    /// <summary>Zeitpunkt der Meldung (UTC).</summary>
    public DateTimeOffset GemeldetAm { get; set; }

    /// <summary>Beschreibung des Mangels.</summary>
    public required string Beschreibung { get; set; }
    /// <summary>Schwere.</summary>
    public MangelSchwere Schwere { get; set; }
    /// <summary>Bearbeitungsstand.</summary>
    public MangelStatus Status { get; set; }
    /// <summary>Zeitpunkt der Behebung (UTC).</summary>
    public DateTimeOffset? BehobenAm { get; set; }

    /// <summary>Was der Gerätewart getan hat bzw. tun will.</summary>
    public string? Massnahme { get; set; }
    /// <summary>Prüfung, bei der der Mangel festgestellt wurde.</summary>
    public Guid? PruefungId { get; set; }

    /// <summary>Sperre nach Benutzung im Einsatz (V13); erledigt sich mit der nächsten bestandenen Prüfung.</summary>
    public Guid? EinsatzId { get; set; }

    /// <summary>Sperre, die erst die nächste bestandene Prüfung aufhebt: nach Benutzung im Einsatz (V13) oder bis zur Eingangsprüfung (V60).</summary>
    public bool SperreBisPruefung { get; set; }

    /// <summary>Prüfung, die diese Sperre aufgehoben hat; ein Storno der Prüfung öffnet sie wieder.</summary>
    public Guid? ErledigtDurchPruefungId { get; set; }

    /// <summary>Text der Sperre bis zur Eingangsprüfung.</summary>
    public const string EingangspruefungText = "Neu geliefert: Eingangsprüfung vor der ersten Nutzung (DGUV Grundsatz 305-002).";

    /// <summary>Sperrt ein neu geliefertes Gerät bis zur ersten bestandenen Prüfung (V60).</summary>
    public static Mangel Eingangssperre(Guid geraetId, DateTimeOffset jetzt, Guid? gemeldetVonId) => new()
    {
        GeraetId = geraetId,
        Beschreibung = EingangspruefungText,
        Schwere = MangelSchwere.AusserBetrieb,
        Status = MangelStatus.Offen,
        GemeldetAm = jetzt,
        GemeldetVonId = gemeldetVonId,
        SperreBisPruefung = true,
    };

    /// <summary>Wer den Mangel behebt (V63).</summary>
    public MangelWerkstatt Werkstatt { get; set; }

    /// <summary>Name der fremden Werkstatt oder Fachfirma.</summary>
    public string? Werkstattname { get; set; }

    /// <summary>Kosten der Behebung brutto in Euro (für Jahresbericht und Haushalt).</summary>
    public decimal? Kosten { get; set; }

    /// <summary>Die Behebung läuft über Garantie oder Gewährleistung.</summary>
    public bool Garantiefall { get; set; }

    /// <summary>Ende der Garantie bzw. Gewährleistung des Gegenstands.</summary>
    public DateOnly? GarantieBis { get; set; }

    /// <summary>Ersatzgerät, das für die Dauer der Behebung im Fach des defekten Geräts liegt.</summary>
    public Guid? ErsatzGeraetId { get; set; }
    /// <summary>Das Ersatzgerät.</summary>
    public Geraet? ErsatzGeraet { get; set; }

    /// <summary>Fahrzeug, auf dem das Ersatzgerät vorher lag (leer = Lager).</summary>
    public Guid? ErsatzVorherFahrzeugId { get; set; }
    /// <summary>Fach bzw. Lagerort des Ersatzgeräts vorher.</summary>
    public string? ErsatzVorherLagerort { get; set; }

    /// <summary>Fahrzeug, von dem das defekte Gerät genommen wurde; Ziel beim Wiederverladen.</summary>
    public Guid? UrsprungFahrzeugId { get; set; }
    /// <summary>Fach des defekten Geräts; Ziel beim Wiederverladen.</summary>
    public string? UrsprungLagerort { get; set; }

    /// <summary>Tag, an dem das reparierte Gerät wieder verladen und das Ersatzgerät zurückgelegt wurde.</summary>
    public DateOnly? WiederVerladenAm { get; set; }

    /// <summary>Zeitpunkt (UTC), zu dem der Kommandant über den einsatzrelevanten Fahrzeugmangel informiert wurde.</summary>
    public DateTimeOffset? KommandantInformiertAm { get; set; }

    /// <summary>Fotos zum Mangel.</summary>
    public List<Mangelfoto> Fotos { get; set; } = [];

    /// <summary>Ein Mangel mit Status Offen oder InArbeit schränkt das Gerät ein.</summary>
    public bool IstOffen => Status != MangelStatus.Behoben;

    /// <summary>Ersatzgerät steckt im Fach, das reparierte Gerät ist noch nicht zurück.</summary>
    public bool ErsatzEingesetzt => ErsatzGeraetId is not null && WiederVerladenAm is null;

    /// <summary>
    /// Einsatzrelevanter Fahrzeugmangel: das Fahrzeug selbst ist außer Betrieb. Dann bekommt der Kommandant einen Hinweis (V63),
    /// weil die Ausrückeordnung betroffen ist.
    /// </summary>
    public bool Einsatzrelevant => FahrzeugId is not null && Schwere == MangelSchwere.AusserBetrieb && IstOffen;

    /// <summary>
    /// Legt ein Ersatzgerät in das Fach des defekten Geräts und nimmt das defekte vom Fahrzeug (V63); so bleibt der Beladeplan stimmig.
    /// Die bisherigen Plätze werden am Mangel gemerkt.
    /// </summary>
    /// <param name="defekt">Das Gerät dieses Mangels.</param>
    /// <param name="ersatz">Das Ersatzgerät.</param>
    /// <param name="ablage">Lagerort des defekten Geräts bis zur Rückkehr, z. B. „Werkstatt“.</param>
    /// <exception cref="InvalidOperationException">Wenn der Mangel nicht zu diesem Gerät gehört oder schon ein Ersatz steckt.</exception>
    public void ErsatzEinsetzen(Geraet defekt, Geraet ersatz, string ablage)
    {
        if (defekt.Id != GeraetId || ersatz.Id == defekt.Id)
        {
            throw new InvalidOperationException("Ersatz nur für das Gerät dieses Mangels und nicht durch sich selbst.");
        }

        if (ErsatzEingesetzt)
        {
            throw new InvalidOperationException("Es steckt schon ein Ersatzgerät im Fach.");
        }

        ErsatzGeraetId = ersatz.Id;
        ErsatzVorherFahrzeugId = ersatz.FahrzeugId;
        ErsatzVorherLagerort = ersatz.Lagerort;
        UrsprungFahrzeugId = defekt.FahrzeugId;
        UrsprungLagerort = defekt.Lagerort;
        WiederVerladenAm = null;

        ersatz.FahrzeugId = defekt.FahrzeugId;
        ersatz.Lagerort = defekt.Lagerort;
        defekt.FahrzeugId = null;
        defekt.Lagerort = ablage;
    }

    /// <summary>Schritt „wieder verladen“: das reparierte Gerät zurück in sein Fach, das Ersatzgerät an seinen alten Platz.</summary>
    /// <param name="defekt">Das Gerät dieses Mangels.</param>
    /// <param name="ersatz">Das Ersatzgerät; null, wenn es inzwischen ausgesondert oder gelöscht wurde.</param>
    /// <param name="heute">Tag des Verladens.</param>
    /// <exception cref="InvalidOperationException">Ohne eingesetzten Ersatz.</exception>
    public void WiederVerladen(Geraet defekt, Geraet? ersatz, DateOnly heute)
    {
        if (!ErsatzEingesetzt || defekt.Id != GeraetId)
        {
            throw new InvalidOperationException("Kein Ersatzgerät eingesetzt.");
        }

        defekt.FahrzeugId = UrsprungFahrzeugId;
        defekt.Lagerort = UrsprungLagerort;
        if (ersatz is not null && ersatz.Id == ErsatzGeraetId)
        {
            ersatz.FahrzeugId = ErsatzVorherFahrzeugId;
            ersatz.Lagerort = ErsatzVorherLagerort;
        }

        WiederVerladenAm = heute;
    }
}

/// <summary>Wer einen Mangel behebt.</summary>
public enum MangelWerkstatt
{
    /// <summary>Noch nicht festgelegt.</summary>
    [Display(Name = "offen")] Offen,
    /// <summary>Gerätewart bzw. eigene Werkstatt.</summary>
    [Display(Name = "intern")] Intern,
    /// <summary>Fremde Werkstatt oder Fachfirma.</summary>
    [Display(Name = "extern")] Extern,
}

/// <summary>
/// Foto zu einem Mangel (V63). Fotos können Personen zeigen; sie werden deshalb nach der Behebung gelöscht (Löschregel L62),
/// der Mangel selbst bleibt.
/// </summary>
public class Mangelfoto : Entitaet
{
    /// <summary>Kennung des Mangels.</summary>
    public Guid MangelId { get; set; }

    /// <summary>Bereinigter Dateiname.</summary>
    public required string Dateiname { get; set; }
    /// <summary>MIME-Typ (PNG oder JPEG).</summary>
    public required string Inhaltstyp { get; set; }
    /// <summary>Inhalt, ohne Metadaten gespeichert.</summary>
    [NichtProtokollieren]
    public required byte[] Inhalt { get; set; }
}
