using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Technik;

/// <summary>Was die Werkstatt an einem Gerät gemacht hat (mehrere möglich).</summary>
[Flags]
public enum Werkstattleistung
{
    /// <summary>Nichts vermerkt.</summary>
    [Display(Name = "keine")] Keine = 0,
    /// <summary>Gereinigt und desinfiziert.</summary>
    [Display(Name = "gereinigt")] Gereinigt = 1,
    /// <summary>Geprüft; dazu entsteht eine Prüfung im Prüfbuch.</summary>
    [Display(Name = "geprüft")] Geprueft = 2,
    /// <summary>Gefüllt (Atemluftflaschen).</summary>
    [Display(Name = "gefüllt")] Gefuellt = 4,
    /// <summary>Instand gesetzt oder Bauteile getauscht.</summary>
    [Display(Name = "instand gesetzt")] Instandgesetzt = 8,
}

/// <summary>
/// Abgabe von Geräten an eine fremde Werkstatt und ihre Rückgabe (V61, V63), z. B. Atemschutz an die Feuerwehr Göppingen.
/// Solange der Auftrag offen ist, gelten die Geräte als „in der Werkstatt“. Bei der Rückgabe übernimmt FEVAD das Werkstattprotokoll
/// und legt für geprüfte Geräte die Prüfung im Prüfbuch an.
/// </summary>
public class Werkstattauftrag : Entitaet
{
    /// <summary>Laufende Nummer „W-JJJJ-NNN“, steht auf dem Lieferschein.</summary>
    public required string Nummer { get; set; }

    /// <summary>Rücklaufweg, für den die Werkstatt zuständig ist.</summary>
    public Ruecklauf Werkstatt { get; set; }

    /// <summary>Name der Werkstatt, z. B. „Feuerwehr Göppingen, Atemschutzwerkstatt“; wird externer Prüfer im Prüfbuch.</summary>
    public required string Werkstattname { get; set; }

    /// <summary>Tag der Abgabe.</summary>
    public DateOnly AbgegebenAm { get; set; }

    /// <summary>Mitglied, das abgegeben hat (wie der Prüfer im Prüfbuch, Löschregel L11).</summary>
    public Guid? AbgegebenVonId { get; set; }
    /// <summary>Das abgebende Mitglied.</summary>
    public Mitglied? AbgegebenVon { get; set; }

    /// <summary>Tag der Rückgabe; leer, solange die Geräte in der Werkstatt sind.</summary>
    public DateOnly? ZurueckAm { get; set; }

    /// <summary>Nummer des Rückgabe-Lieferscheins oder Protokolls der Werkstatt.</summary>
    public string? BelegWerkstatt { get; set; }

    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Abgegebene Geräte.</summary>
    public List<Werkstattposition> Positionen { get; set; } = [];

    /// <summary>Angehängte Werkstattprotokolle (PDF oder Foto).</summary>
    public List<Werkstattprotokoll> Protokolle { get; set; } = [];

    /// <summary>Die Geräte sind noch in der Werkstatt.</summary>
    public bool Offen => ZurueckAm is null;

    /// <summary>Nächste Auftragsnummer eines Jahres aus der höchsten vergebenen.</summary>
    /// <param name="jahr">Jahr der Abgabe.</param>
    /// <param name="vorhandene">Bereits vergebene Nummern.</param>
    /// <returns>Die neue Nummer.</returns>
    public static string NaechsteNummer(int jahr, IEnumerable<string> vorhandene)
    {
        var praefix = $"W-{jahr}-";
        var hoechste = vorhandene
            .Where(n => n.StartsWith(praefix, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[praefix.Length..], out var zahl) ? zahl : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{praefix}{hoechste + 1:000}";
    }
}

/// <summary>Ein Gerät in einem Werkstattauftrag.</summary>
public class Werkstattposition : Entitaet
{
    /// <summary>Kennung des Auftrags.</summary>
    public Guid WerkstattauftragId { get; set; }
    /// <summary>Der Auftrag.</summary>
    public Werkstattauftrag? Werkstattauftrag { get; set; }

    /// <summary>Kennung des Geräts.</summary>
    public Guid GeraetId { get; set; }
    /// <summary>Das Gerät.</summary>
    public Geraet? Geraet { get; set; }

    /// <summary>Was die Werkstatt laut Protokoll gemacht hat; bei der Rückgabe erfasst.</summary>
    public Werkstattleistung Leistungen { get; set; }

    /// <summary>Prüfung, die bei der Rückgabe aus dem Werkstattprotokoll entstanden ist.</summary>
    public Guid? PruefungId { get; set; }

    /// <summary>Bemerkung der Werkstatt zu diesem Gerät.</summary>
    public string? Bemerkung { get; set; }
}

/// <summary>Werkstattprotokoll oder Lieferschein der Werkstatt als Anhang zum Auftrag.</summary>
public class Werkstattprotokoll : Entitaet
{
    /// <summary>Kennung des Auftrags.</summary>
    public Guid WerkstattauftragId { get; set; }

    /// <summary>Bereinigter Dateiname.</summary>
    public required string Dateiname { get; set; }
    /// <summary>MIME-Typ (PDF, PNG oder JPEG).</summary>
    public required string Inhaltstyp { get; set; }
    /// <summary>Inhalt.</summary>
    [NichtProtokollieren]
    public required byte[] Inhalt { get; set; }
}

/// <summary>
/// Bauteil mit eigener Lebensdauer in einem Gerät, z. B. Membran im Lungenautomaten (V61). Abschaltbar über den Parameter
/// <see cref="ParameterSchluessel.BauteildatenAktiv"/>, weil Adelberg die Instandhaltung in Göppingen machen lässt.
/// </summary>
public class Bauteilart : Katalogeintrag
{
    /// <summary>Geräteart, in die das Bauteil eingebaut wird.</summary>
    public Guid GeraeteartId { get; set; }
    /// <summary>Die Geräteart.</summary>
    public Geraeteart? Geraeteart { get; set; }

    /// <summary>Höchstdauer im eingebauten Zustand in Monaten.</summary>
    public int? EingebautMonate { get; set; }

    /// <summary>Höchstalter ab Herstellung in Monaten (Lagerung plus Einbau).</summary>
    public int? GesamtMonate { get; set; }

    /// <summary>Herkunft der Fristen, z. B. „Herstellerangabe (Vorschlag)“.</summary>
    public string? Grundlage { get; set; }
}

/// <summary>Eingebautes Bauteil eines Geräts.</summary>
public class Bauteil : Entitaet
{
    /// <summary>Kennung des Geräts.</summary>
    public Guid GeraetId { get; set; }
    /// <summary>Das Gerät.</summary>
    public Geraet? Geraet { get; set; }

    /// <summary>Kennung der Bauteilart.</summary>
    public Guid BauteilartId { get; set; }
    /// <summary>Die Bauteilart.</summary>
    public Bauteilart? Bauteilart { get; set; }

    /// <summary>Herstelldatum (Prägung oder Packungsangabe).</summary>
    public DateOnly? HergestelltAm { get; set; }

    /// <summary>Tag des Einbaus.</summary>
    public DateOnly EingebautAm { get; set; }

    /// <summary>Tag des Ausbaus; ausgebaute Bauteile bleiben als Lebenslauf sichtbar.</summary>
    public DateOnly? AusgebautAm { get; set; }

    /// <summary>Bemerkung, z. B. Charge.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Spätester Tausch: früheres Ende aus Einbaudauer und Gesamtalter.</summary>
    /// <param name="eingebautMonate">Höchstdauer eingebaut.</param>
    /// <param name="gesamtMonate">Höchstalter ab Herstellung.</param>
    /// <returns>Der Termin oder null ohne Frist.</returns>
    public DateOnly? TauschFaellig(int? eingebautMonate, int? gesamtMonate)
    {
        DateOnly? eingebaut = eingebautMonate is { } e ? EingebautAm.AddMonths(e) : null;
        DateOnly? gesamt = gesamtMonate is { } g && HergestelltAm is { } h ? h.AddMonths(g) : null;
        return (eingebaut, gesamt) switch
        {
            ({ } a, { } b) => a < b ? a : b,
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => null,
        };
    }
}

/// <summary>
/// Eintrag im Füllprotokoll einer Atemluftflasche (V61, Kann). Abschaltbar über <see cref="ParameterSchluessel.FuellprotokollAktiv"/>,
/// weil in Adelberg die Feuerwehr Göppingen füllt. Der Füller wird wie der Prüfer im Prüfbuch gespeichert (Löschregel L11).
/// </summary>
public class Flaschenfuellung : Entitaet
{
    /// <summary>Kennung der Flasche.</summary>
    public Guid GeraetId { get; set; }
    /// <summary>Die Flasche.</summary>
    public Geraet? Geraet { get; set; }

    /// <summary>Zeitpunkt der Füllung (UTC).</summary>
    public DateTimeOffset GefuelltAm { get; set; }

    /// <summary>Fülldruck in bar.</summary>
    public int DruckBar { get; set; }

    /// <summary>Mitglied, das gefüllt hat.</summary>
    public Guid? GefuelltVonId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? GefuelltVon { get; set; }

    /// <summary>Füllanlage (Atemluftkompressor als Gerät).</summary>
    public Guid? AnlageId { get; set; }
    /// <summary>Die Füllanlage.</summary>
    public Geraet? Anlage { get; set; }

    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }
}
