using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Dienstbetrieb;

/// <summary>
/// Thema einer Unterweisung (V53). Vorbelegt mit UVV allgemein, Sonderrechte, Atemschutz, Gefahrstoffe und Hygiene (Andi, F22);
/// die Liste ist änderbar. Ein Pflichtthema muss jede Person der Zielgruppe einmal im Kalenderjahr erhalten.
/// </summary>
public class Unterweisungsthema : Katalogeintrag
{
    /// <summary>Feste Kennung des Themas UVV allgemein.</summary>
    public const string CodeUvv = "UVV";
    /// <summary>Feste Kennung des Themas Sonderrechte.</summary>
    public const string CodeSonderrechte = "SOR";
    /// <summary>Feste Kennung des Themas Atemschutz.</summary>
    public const string CodeAtemschutz = "AS";
    /// <summary>Feste Kennung des Themas Gefahrstoffe.</summary>
    public const string CodeGefahrstoffe = "GEF";
    /// <summary>Feste Kennung des Themas Hygiene.</summary>
    public const string CodeHygiene = "HYG";

    /// <summary>Pflichtthema: erscheint in der Deckungsprüfung des Jahres.</summary>
    public bool Pflicht { get; set; } = true;

    /// <summary>Zielgruppe: nur Inhaber dieser Qualifikation (z. B. Maschinisten für Sonderrechte); leer = alle Einsatzkräfte.</summary>
    public Guid? QualifikationId { get; set; }
    /// <summary>Navigationsverweis auf die Qualifikation der Zielgruppe.</summary>
    public Qualifikation? Qualifikation { get; set; }

    /// <summary>Wiederkehrender Nachweis, den der Abschluss der Unterweisung für jeden Anwesenden der Zielgruppe einträgt.</summary>
    public Guid? NachweisartId { get; set; }
    /// <summary>Navigationsverweis auf die Nachweisart.</summary>
    public Nachweisart? Nachweisart { get; set; }

    /// <summary>
    /// Zählt als Atemschutz-Unterweisung nach FwDV 7: der Abschluss legt für anwesende Geräteträger einen Atemschutznachweis an,
    /// den Kommandant und Atemschutzgerätewart wie jeden anderen bestätigen.
    /// </summary>
    public bool Atemschutzunterweisung { get; set; }

    /// <summary>Kurze Beschreibung, z. B. Rechtsgrundlage.</summary>
    public string? Beschreibung { get; set; }
}

/// <summary>Deckungsprüfung der Pflichtthemen eines Jahres (V53): wer hat welches Pflichtthema noch nicht erhalten.</summary>
public static class Unterweisungsdeckung
{
    /// <summary>Ergebnis je Thema.</summary>
    /// <param name="ThemaId">Kennung des Themas.</param>
    /// <param name="Zielgruppe">Zahl der Personen, die das Thema erhalten müssen.</param>
    /// <param name="Unterwiesen">Zahl der Personen der Zielgruppe, die im Jahr an einer Unterweisung mit diesem Thema teilgenommen haben.</param>
    /// <param name="Offen">Kennungen der Personen der Zielgruppe ohne Unterweisung.</param>
    public sealed record Ergebnis(Guid ThemaId, int Zielgruppe, int Unterwiesen, IReadOnlyList<Guid> Offen)
    {
        /// <summary>Alle Personen der Zielgruppe sind unterwiesen.</summary>
        public bool Vollstaendig => Offen.Count == 0;

        /// <summary>Ampel wie bei Fristen: grün vollständig, rot nach Ablauf des Jahres offen, gelb ab dem Warnmonat offen.</summary>
        /// <param name="heute">Heutiges Datum.</param>
        /// <param name="jahr">Geprüftes Jahr.</param>
        /// <param name="warnenAbMonat">Monat, ab dem offene Pflichtthemen gelb werden (Parameter).</param>
        /// <returns>Die Ampel; <c>null</c>, wenn noch Personen offen sind, das Jahr aber noch Zeit lässt.</returns>
        public Fristampel? Ampel(DateOnly heute, int jahr, int warnenAbMonat) =>
            Vollstaendig ? Fristampel.Gruen
            : heute.Year > jahr ? Fristampel.Rot
            : heute.Year == jahr && heute.Month >= warnenAbMonat ? Fristampel.Gelb
            : null;
    }

    /// <summary>Berechnet die Deckung eines Themas.</summary>
    /// <param name="themaId">Kennung des Themas.</param>
    /// <param name="zielgruppe">Kennungen der Personen, die das Thema erhalten müssen.</param>
    /// <param name="teilgenommen">Kennungen der Personen, die im Jahr an einer Unterweisung mit dem Thema anwesend waren.</param>
    /// <returns>Das Ergebnis; Teilnehmer außerhalb der Zielgruppe zählen nicht.</returns>
    public static Ergebnis Berechnen(Guid themaId, IEnumerable<Guid> zielgruppe, IEnumerable<Guid> teilgenommen)
    {
        var ziel = zielgruppe.Distinct().ToList();
        var dabei = teilgenommen.ToHashSet();
        var offen = ziel.Where(id => !dabei.Contains(id)).ToList();
        return new(themaId, ziel.Count, ziel.Count - offen.Count, offen);
    }
}

/// <summary>Leistungsnachweis einer Brandsicherheitswache (V25): Personenstunden und Betrag für die Rechnung der Gemeindekasse.</summary>
public static class Wachleistung
{
    /// <summary>Ergebnis der Berechnung.</summary>
    /// <param name="Personen">Zahl der eingesetzten Wachen.</param>
    /// <param name="Personenstunden">Abgerechnete Stunden aller Wachen, je Person auf die Rundung aufgerundet.</param>
    /// <param name="Betrag">Personenstunden mal Stundensatz; <c>null</c> ohne Kostensatz.</param>
    /// <param name="AnteilKasse">Vorschlag für den Anteil der Kameradschaftskasse; <c>null</c> ohne Betrag oder bei 0 %.</param>
    public sealed record Ergebnis(int Personen, decimal Personenstunden, decimal? Betrag, decimal? AnteilKasse);

    /// <summary>Berechnet den Leistungsnachweis.</summary>
    /// <param name="dauern">Anwesenheitsdauer je Wache.</param>
    /// <param name="rundungMinuten">Abrechnungseinheit in Minuten, z. B. 60 für „je angefangene Stunde“.</param>
    /// <param name="stundensatz">Stundensatz je Person; <c>null</c>, wenn kein Kostensatz gewählt ist.</param>
    /// <param name="anteilKasseProzent">Anteil der Kameradschaftskasse in Prozent (Parameter, Standard 0).</param>
    /// <returns>Das Ergebnis.</returns>
    public static Ergebnis Berechnen(IEnumerable<TimeSpan> dauern, int rundungMinuten, decimal? stundensatz, decimal anteilKasseProzent)
    {
        var einheit = Math.Max(1, rundungMinuten);
        var liste = dauern.Where(d => d > TimeSpan.Zero).ToList();
        var minuten = liste.Sum(d => (decimal)Math.Ceiling(d.TotalMinutes / einheit) * einheit);
        var stunden = Math.Round(minuten / 60m, 2);
        decimal? betrag = stundensatz is { } satz ? Math.Round(stunden * satz, 2, MidpointRounding.AwayFromZero) : null;
        decimal? anteil = betrag is { } b && anteilKasseProzent > 0 ? Math.Round(b * anteilKasseProzent / 100m, 2, MidpointRounding.AwayFromZero) : null;
        return new(liste.Count, stunden, betrag, anteil);
    }
}
