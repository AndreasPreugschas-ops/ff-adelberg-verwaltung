using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Kassen;

/// <summary>
/// Gegenstand im Bestandsverzeichnis einer Kasse (V04): was aus dem Sondervermögen angeschafft wurde und über der Wertgrenze liegt
/// (Parameter, Satzung: 500 €), mit Beleg, Standort und Abgang. Geräte der Geräteverwaltung lassen sich verknüpfen, damit das
/// Gemeindeinventar getrennt bleibt.
/// </summary>
public class Vermoegensgegenstand : Entitaet
{
    /// <summary>Schlüssel der Kasse.</summary>
    public Guid KasseId { get; set; }

    /// <summary>Die Kasse.</summary>
    public Kasse? Kasse { get; set; }

    /// <summary>Laufende Nummer im Verzeichnis, z. B. „K-2026-003“; frei wählbar.</summary>
    public string? Inventarnummer { get; set; }

    /// <summary>Bezeichnung, z. B. „Festzeltgarnitur (10 Stück)“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Tag der Anschaffung.</summary>
    public DateOnly Anschaffung { get; set; }

    /// <summary>Anschaffungswert in Euro.</summary>
    public decimal Wert { get; set; }

    /// <summary>Buchung, mit der die Anschaffung bezahlt wurde (Beleg).</summary>
    public Guid? BuchungId { get; set; }

    /// <summary>Die Buchung.</summary>
    public Buchung? Buchung { get; set; }

    /// <summary>Standort, z. B. „Gerätehaus, Lager OG“.</summary>
    public string? Standort { get; set; }

    /// <summary>Verknüpftes Gerät der Geräteverwaltung (Eigentümer Kameradschaftskasse).</summary>
    public Guid? GeraetId { get; set; }

    /// <summary>Das Gerät.</summary>
    public Geraet? Geraet { get; set; }

    /// <summary>Tag des Abgangs; leer, solange der Gegenstand im Bestand ist.</summary>
    public DateOnly? AbgangAm { get; set; }

    /// <summary>Grund des Abgangs, z. B. „verkauft“, „defekt entsorgt“.</summary>
    public string? AbgangGrund { get; set; }

    /// <summary>Erlös bei einem Verkauf.</summary>
    public decimal? Erloes { get; set; }

    /// <summary>Freitext.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Der Gegenstand war am Tag im Bestand.</summary>
    /// <param name="tag">Stichtag.</param>
    /// <returns>true, wenn angeschafft und noch nicht abgegangen.</returns>
    public bool ImBestand(DateOnly tag) => Anschaffung <= tag && (AbgangAm is null || AbgangAm > tag);
}

/// <summary>
/// Jahresabschluss einer Kasse (V05): sperrt das Jahr, hält die Endbestände der Konten fest und dokumentiert die Vorlage an den
/// Bürgermeister und die Entlastung durch die Hauptversammlung. Buchungen in einem abgeschlossenen Jahr gehen nur noch als
/// Ausnahmebuchung mit Begründung.
/// </summary>
public class Jahresabschluss : Entitaet
{
    /// <summary>Schlüssel der Kasse.</summary>
    public Guid KasseId { get; set; }

    /// <summary>Die Kasse.</summary>
    public Kasse? Kasse { get; set; }

    /// <summary>Haushaltsjahr.</summary>
    public int Jahr { get; set; }

    /// <summary>Zeitpunkt des Abschlusses in UTC.</summary>
    public DateTimeOffset AbgeschlossenAm { get; set; }

    /// <summary>Konto, das abgeschlossen hat.</summary>
    public Guid? AbgeschlossenVon { get; set; }

    /// <summary>Einnahmen des Jahres beim Abschluss (ohne Umbuchungen, Stornos abgezogen).</summary>
    public decimal Einnahmen { get; set; }

    /// <summary>Ausgaben des Jahres beim Abschluss.</summary>
    public decimal Ausgaben { get; set; }

    /// <summary>Endbestände der Konten am 31.12. beim Abschluss; werden ins Folgejahr vorgetragen.</summary>
    public List<Kontoendbestand> Endbestaende { get; set; } = [];

    /// <summary>Tag, an dem der Abschluss dem Bürgermeister vorgelegt wurde.</summary>
    public DateOnly? VorgelegtAm { get; set; }

    /// <summary>Entlastungsbeschluss der Hauptversammlung aus dem Gremienmodul.</summary>
    public Guid? EntlastungBeschlussId { get; set; }

    /// <summary>Der Entlastungsbeschluss.</summary>
    public Beschluss? EntlastungBeschluss { get; set; }

    /// <summary>Freitext.</summary>
    public string? Bemerkung { get; set; }
}

/// <summary>Endbestand eines Kontos im Jahresabschluss.</summary>
public class Kontoendbestand : Entitaet
{
    /// <summary>Schlüssel des Abschlusses.</summary>
    public Guid JahresabschlussId { get; set; }

    /// <summary>Schlüssel des Kontos.</summary>
    public Guid KontoId { get; set; }

    /// <summary>Das Konto.</summary>
    public Zahlungsmittelkonto? Konto { get; set; }

    /// <summary>Bestand am 31.12. in Euro.</summary>
    public decimal Bestand { get; set; }
}

/// <summary>Regeln für Bestandsverzeichnis und Jahresabschluss.</summary>
public static class Abschlussregeln
{
    /// <summary>
    /// Ausgaben ab der Wertgrenze, die noch nicht im Bestandsverzeichnis stehen: nicht storniert, kein Storno, nicht verknüpft, nicht als
    /// „kein Gegenstand“ markiert. Nur
    /// Vorschläge; Verbrauchsausgaben wie ein Fest bleiben draußen, wenn der Kassenwart sie nicht aufnimmt.
    /// </summary>
    /// <param name="buchungen">Buchungen der Kasse.</param>
    /// <param name="grenze">Wertgrenze in Euro; 0 schaltet die Vorschläge ab.</param>
    /// <param name="verknuepft">Buchungen, die schon einem Gegenstand zugeordnet sind.</param>
    /// <returns>Die vorgeschlagenen Buchungen, neueste zuerst; als „kein Gegenstand“ markierte fehlen.</returns>
    public static IReadOnlyList<Buchung> Vorschlaege(IEnumerable<Buchung> buchungen, int grenze, IReadOnlySet<Guid> verknuepft)
    {
        if (grenze <= 0)
        {
            return [];
        }

        var liste = buchungen.ToList();
        var storniert = liste.Where(b => b.StornoVonId is not null).Select(b => b.StornoVonId!.Value).ToHashSet();
        return liste.Where(b => b.Art == BuchungArt.Ausgabe && b.StornoVonId is null && !storniert.Contains(b.Id) && b.Betrag >= grenze
                && !verknuepft.Contains(b.Id) && !b.KeinGegenstand)
            .OrderByDescending(b => b.Datum).ToList();
    }

    /// <summary>Gilt das Jahr als abgeschlossen?</summary>
    /// <param name="abgeschlossen">Abgeschlossene Jahre der Kasse.</param>
    /// <param name="tag">Buchungstag.</param>
    /// <returns>true, wenn eine Buchung an diesem Tag eine Ausnahmebuchung ist.</returns>
    public static bool Gesperrt(IReadOnlySet<int> abgeschlossen, DateOnly tag) => abgeschlossen.Contains(tag.Year);

    /// <summary>Gründe, warum ein Jahr noch nicht abgeschlossen werden kann.</summary>
    /// <param name="jahr">Das Jahr.</param>
    /// <param name="offen">Buchungen des Jahres, die noch nicht festgeschrieben sind.</param>
    /// <param name="vorjahrOffen">Das Vorjahr hat Buchungen, ist aber nicht abgeschlossen.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <returns>Die Hinderungsgründe; leer, wenn abgeschlossen werden kann.</returns>
    public static IReadOnlyList<string> Hindernisse(int jahr, int offen, bool vorjahrOffen, DateOnly heute)
    {
        var gruende = new List<string>();
        if (jahr >= heute.Year)
        {
            gruende.Add($"Das Jahr {jahr} ist noch nicht vorbei.");
        }

        if (offen > 0)
        {
            gruende.Add($"{offen} Buchungen sind noch nicht festgeschrieben (Beleg fehlt oder Festschreiben steht aus).");
        }

        if (vorjahrOffen)
        {
            gruende.Add($"Zuerst das Jahr {jahr - 1} abschließen.");
        }

        return gruende;
    }
}
