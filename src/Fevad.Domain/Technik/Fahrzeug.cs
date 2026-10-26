using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>Fahrzeug der Feuerwehr mit Funkrufname, Prüfvorlage und Anlagendaten.</summary>
public class Fahrzeug : Entitaet, IAnlagegut
{
    /// <summary>Muss exakt der Einsatzmittelkennung in alamos FE2 entsprechen, z. B. "Florian Adelberg 43".</summary>
    public required string Funkrufname { get; set; }
    /// <summary>Fahrzeugtyp, z. B. „HLF 10“.</summary>
    public required string Typ { get; set; }
    /// <summary>Amtliches Kennzeichen.</summary>
    public string? Kennzeichen { get; set; }
    /// <summary>Hersteller des Fahrgestells.</summary>
    public string? Hersteller { get; set; }
    /// <summary>Aufbauhersteller.</summary>
    public string? Aufbau { get; set; }
    /// <summary>Baujahr.</summary>
    public int? Baujahr { get; set; }
    /// <summary>Tag der Indienststellung.</summary>
    public DateOnly? IndienstAm { get; set; }
    /// <summary>Tag der Außerdienststellung.</summary>
    public DateOnly? AusserDienstAm { get; set; }
    /// <summary>ISSI des Fahrzeugfunkgeräts (Digitalfunk).</summary>
    public string? Issi { get; set; }
    /// <summary>FMS-Kennung.</summary>
    public string? FmsKennung { get; set; }
    /// <summary>Zahl der Sitzplätze.</summary>
    public int? Sitzplaetze { get; set; }

    /// <summary>Fahrzeug-Identifizierungsnummer (FIN) aus der Zulassungsbescheinigung.</summary>
    public string? Fin { get; set; }

    /// <summary>
    /// Zulässige Gesamtmasse in kg; bestimmt das HU-Intervall und welche Fahrerlaubnis reicht
    /// (z. B. Klasse B bis 3,5 t, Feuerwehr-Fahrberechtigung bis 4,75 t oder 7,5 t, V59).
    /// </summary>
    public int? ZulaessigeGesamtmasseKg { get; set; }

    /// <summary>Besatzung in der Schreibweise der Feuerwehr, z. B. „1/8“.</summary>
    public string? Besatzung { get; set; }

    /// <summary>Löschwassertank in Litern.</summary>
    public int? LoeschwasserLiter { get; set; }

    /// <summary>Schaummitteltank in Litern.</summary>
    public int? SchaummittelLiter { get; set; }

    /// <summary>Feuerlöschkreiselpumpe, z. B. „FPN 10-2000“.</summary>
    public string? Pumpe { get; set; }

    /// <summary>Zuletzt bekannter Kilometerstand.</summary>
    public int? Kilometerstand { get; set; }

    /// <summary>Betriebsstunden von Fahrzeug bzw. Pumpe, zuletzt bekannter Stand.</summary>
    public decimal? Betriebsstunden { get; set; }

    /// <summary>Tag, an dem Kilometerstand und Betriebsstunden zuletzt abgelesen wurden.</summary>
    public DateOnly? StandAm { get; set; }

    /// <summary>Zuwendungsbescheid (Z-Feu): Datum.</summary>
    public DateOnly? ZuwendungsbescheidAm { get; set; }

    /// <summary>Zuwendungsbescheid (Z-Feu): Aktenzeichen des Landratsamts bzw. Regierungspräsidiums.</summary>
    public string? ZuwendungsbescheidAktenzeichen { get; set; }

    /// <summary>
    /// Fahrzeugklasse der VOKeFw für den Kostenersatz, z. B. „HLF 10“ oder bei vergleichbaren Fahrzeugen „TLF 2000“ (§ 1 Abs. 2 VOKeFw);
    /// verweist auf einen Kostensatz der Art Fahrzeugklasse. Ein eigener Satz am Fahrzeug geht vor.
    /// </summary>
    public string? Kostenklasse { get; set; }

    /// <summary>Geräteart mit dem Prüfplan des Fahrzeugs (HU, UVV).</summary>
    public Guid? PruefvorlageId { get; set; }
    /// <summary>Die Prüfvorlage.</summary>
    public Geraeteart? Pruefvorlage { get; set; }

    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Anschaffungswert brutto in Euro (V60).</summary>
    public decimal? Anschaffungswert { get; set; }

    /// <inheritdoc />
    public string? Anlagennummer { get; set; }

    /// <inheritdoc />
    public int? NutzungsdauerJahre { get; set; }

    /// <inheritdoc />
    public Eigentuemer Eigentuemer { get; set; }

    /// <inheritdoc />
    public decimal? Foerdermittel { get; set; }

    /// <inheritdoc />
    public string? Foerdergeber { get; set; }

    /// <inheritdoc />
    public DateOnly? ZweckbindungBis { get; set; }

    /// <summary>Grund der Außerdienststellung bzw. Aussonderung.</summary>
    public string? Aussonderungsgrund { get; set; }

    /// <inheritdoc />
    public Verwertung? Verwertung { get; set; }

    /// <inheritdoc />
    public decimal? Erloes { get; set; }

    /// <inheritdoc />
    public string? Aussonderungsbeschluss { get; set; }

    /// <inheritdoc />
    public DateOnly? FoerdergeberMitgeteiltAm { get; set; }

    /// <summary>Verlastete Geräte.</summary>
    public List<Geraet> Geraete { get; set; } = [];

    /// <summary>Soll-Beladung je Fach (V59).</summary>
    public List<Beladeposition> Beladeplan { get; set; } = [];

    /// <summary>
    /// Läuft die Zweckbindung der Förderung bei einer Außerdienststellung zum Stichtag noch, ohne dass der Fördergeber informiert ist?
    /// Dann muss vor der Verwertung die Mitteilung an den Fördergeber erfolgen (VwV Z-Feu Nr. 6.5).
    /// </summary>
    /// <param name="ausserDienstAm">Geplante oder erfolgte Außerdienststellung.</param>
    /// <returns>true, wenn eine Warnung angezeigt werden muss.</returns>
    public bool ZweckbindungWarnung(DateOnly? ausserDienstAm) =>
        ausserDienstAm is { } tag && FoerdergeberMitgeteiltAm is null && Aussonderungsregeln.FoerdergeberMitteilen(this, tag);
}

/// <summary>
/// Ob die Fahrerlaubnis eines Mitglieds für ein Fahrzeug reicht (V59). Der Gerätewart sieht nur „ja/nein bis Datum“,
/// nicht die Klassen und nicht den Führerschein (Andi, 25.10.2026).
/// </summary>
public static class Fahrberechtigung
{
    /// <summary>Eine Fahrerlaubnis des Mitglieds.</summary>
    /// <param name="BisKg">Zulässige Gesamtmasse, bis zu der die Fahrerlaubnis reicht; leer = ohne Grenze.</param>
    /// <param name="ErworbenAm">Erteilt am.</param>
    /// <param name="GueltigBis">Befristet bis; leer = unbefristet.</param>
    public sealed record Erlaubnis(int? BisKg, DateOnly ErworbenAm, DateOnly? GueltigBis);

    /// <summary>Ergebnis für ein Mitglied und ein Fahrzeug.</summary>
    /// <param name="Ausreichend">Darf das Fahrzeug am Stichtag fahren.</param>
    /// <param name="Bis">Bis wann das gilt; leer = ohne Ende.</param>
    public sealed record Ergebnis(bool Ausreichend, DateOnly? Bis);

    /// <summary>Prüft, ob eine am Stichtag gültige Fahrerlaubnis die zulässige Gesamtmasse abdeckt und die Führerscheinkontrolle gilt.</summary>
    /// <param name="gesamtmasseKg">Zulässige Gesamtmasse des Fahrzeugs.</param>
    /// <param name="erlaubnisse">Fahrerlaubnisse des Mitglieds.</param>
    /// <param name="kontrolleGueltigBis">
    /// Gültigkeit der letzten Führerscheinkontrolle; null, wenn keine Kontrolle verlangt wird (keine Nachweisart dafür angelegt).
    /// <see cref="DateOnly.MinValue"/> heißt: verlangt, aber nie erfasst.
    /// </param>
    /// <param name="stichtag">Prüftag.</param>
    /// <returns>Ja/nein und bis wann.</returns>
    public static Ergebnis Pruefen(int gesamtmasseKg, IEnumerable<Erlaubnis> erlaubnisse, DateOnly? kontrolleGueltigBis, DateOnly stichtag)
    {
        var passende = erlaubnisse
            .Where(e => e.ErworbenAm <= stichtag && (e.GueltigBis is null || e.GueltigBis >= stichtag) && (e.BisKg is null || e.BisKg >= gesamtmasseKg))
            .ToList();
        if (passende.Count == 0 || kontrolleGueltigBis < stichtag)
        {
            return new(false, null);
        }

        DateOnly? bis = passende.Any(e => e.GueltigBis is null) ? null : passende.Max(e => e.GueltigBis);
        if (kontrolleGueltigBis is { } kontrolle && (bis is null || kontrolle < bis))
        {
            bis = kontrolle;
        }

        return new(true, bis);
    }
}
