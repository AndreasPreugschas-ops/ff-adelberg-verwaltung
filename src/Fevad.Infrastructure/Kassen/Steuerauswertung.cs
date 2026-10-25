using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Kassen;

/// <summary>
/// Steuergrundlagen der Kassen für ein Jahr (V03): Nettoumsatz der wirtschaftlichen Tätigkeit gegen die Umsatzgrenze, Umsatzsteuer je
/// Satz ab dem Tag, ab dem § 2b UStG gilt, und die Buchungen für den Export an die Kämmerei. Die Ampel zählt alle Kassen zusammen,
/// weil die Kämmerei die Umsätze unter der Steuernummer der Gemeinde meldet.
/// </summary>
public sealed class Steuerauswertung(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Eine Buchung mit Steuerangaben; Beträge mit Vorzeichen (Storno negativ).</summary>
    /// <param name="Buchung">Die Buchung samt Kasse, Position und Veranstaltung.</param>
    /// <param name="SatzProzent">Angewandter Steuersatz in Prozent; 0 vor § 2b oder ohne Satz.</param>
    /// <param name="Brutto">Betrag mit Vorzeichen.</param>
    /// <param name="Netto">Nettobetrag mit Vorzeichen.</param>
    public sealed record Zeile(Buchung Buchung, decimal SatzProzent, decimal Brutto, decimal Netto)
    {
        /// <summary>Enthaltene Umsatzsteuer.</summary>
        public decimal Umsatzsteuer => Brutto - Netto;
    }

    /// <summary>Summen einer Kasse.</summary>
    /// <param name="Kasse">Die Kasse.</param>
    /// <param name="Umsatz">Nettoeinnahmen der wirtschaftlichen Tätigkeit.</param>
    /// <param name="AusgabenWirtschaftlich">Ausgaben der wirtschaftlichen Tätigkeit (brutto).</param>
    /// <param name="Vermoegensverwaltung">Einnahmen der Vermögensverwaltung.</param>
    /// <param name="Umsatzsteuer">Enthaltene Umsatzsteuer der wirtschaftlichen Einnahmen.</param>
    public sealed record Kassensumme(Kasse Kasse, decimal Umsatz, decimal AusgabenWirtschaftlich, decimal Vermoegensverwaltung, decimal Umsatzsteuer);

    /// <summary>Ergebnis eines Jahres.</summary>
    /// <param name="Jahr">Das Jahr.</param>
    /// <param name="Kassen">Summen je Kasse.</param>
    /// <param name="Zeilen">Alle Buchungen außerhalb des internen Bereichs.</param>
    /// <param name="Grenze">Umsatzgrenze in Euro.</param>
    /// <param name="WarnungProzent">Ab diesem Anteil gelb.</param>
    /// <param name="Paragraph2bAb">Tag, ab dem § 2b UStG gilt.</param>
    public sealed record Stand(int Jahr, IReadOnlyList<Kassensumme> Kassen, IReadOnlyList<Zeile> Zeilen, int Grenze, int WarnungProzent, DateOnly? Paragraph2bAb)
    {
        /// <summary>Nettoumsatz der wirtschaftlichen Tätigkeit aller Kassen.</summary>
        public decimal Umsatz => Kassen.Sum(k => k.Umsatz);

        /// <summary>Ampel gegen die Umsatzgrenze.</summary>
        public Fristampel Ampel => Steuerregeln.Ampel(Umsatz, Grenze, WarnungProzent);

        /// <summary>Gilt § 2b UStG schon im Jahr (mindestens ab einem Tag darin)?</summary>
        public bool Paragraph2bImJahr => Paragraph2bAb is { } ab && ab.Year <= Jahr;
    }

    /// <summary>Wertet ein Jahr über alle Kassen aus.</summary>
    /// <param name="jahr">Das Jahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Stand.</returns>
    public async Task<Stand> AuswertenAsync(int jahr, CancellationToken ct = default)
    {
        var stichtag = new DateOnly(jahr, 12, 31);
        var grenze = await parameter.ZahlAsync(ParameterSchluessel.KameradschaftskasseUmsatzgrenzeEuro, stichtag, 45000, ct);
        var warnung = await parameter.ZahlAsync(ParameterSchluessel.KasseSteuerWarnungProzent, stichtag, 80, ct);
        var ab = await db.Organisationen.AsNoTracking().Select(o => o.UmsatzsteuerParagraph2bAb).FirstOrDefaultAsync(ct);
        var saetze = await db.Parameter.AsNoTracking()
            .Where(p => p.Schluessel == ParameterSchluessel.UmsatzsteuerRegelsatzProzent || p.Schluessel == ParameterSchluessel.UmsatzsteuerErmaessigterSatzProzent)
            .ToListAsync(ct);

        var kassen = await db.Kassen.AsNoTracking().OrderBy(k => k.Sortierung).ThenBy(k => k.Name).ToListAsync(ct);
        var buchungen = await db.Buchungen.AsNoTracking().Include(b => b.Planposition).Include(b => b.Veranstaltung).Include(b => b.StornoVon)
            .Where(b => b.Jahr == jahr && b.Art != BuchungArt.Umbuchung && b.Steuerbereich != Steuerbereich.Intern)
            .OrderBy(b => b.KasseId).ThenBy(b => b.Belegnummer)
            .ToListAsync(ct);

        var zeilen = buchungen.Select(b =>
        {
            var satz = b.Steuerbereich == Steuerbereich.Wirtschaftlich && Steuerregeln.Paragraph2bGilt(b.Datum, ab) ? Satz(saetze, b.Steuersatz, b.Datum) : 0m;
            var brutto = Kassenregeln.Ist(b);
            return new Zeile(b, satz, brutto, Steuerregeln.Netto(brutto, satz));
        }).ToList();

        var summen = kassen.Select(k =>
        {
            var eigene = zeilen.Where(z => z.Buchung.KasseId == k.Id).ToList();
            var einnahmen = eigene.Where(z => z.Buchung.Art == BuchungArt.Einnahme && z.Buchung.Steuerbereich == Steuerbereich.Wirtschaftlich).ToList();
            return new Kassensumme(
                k,
                einnahmen.Sum(z => z.Netto),
                eigene.Where(z => z.Buchung.Art == BuchungArt.Ausgabe && z.Buchung.Steuerbereich == Steuerbereich.Wirtschaftlich).Sum(z => z.Brutto),
                eigene.Where(z => z.Buchung.Art == BuchungArt.Einnahme && z.Buchung.Steuerbereich == Steuerbereich.Vermoegensverwaltung).Sum(z => z.Brutto),
                einnahmen.Sum(z => z.Umsatzsteuer));
        }).ToList();

        foreach (var z in zeilen)
        {
            z.Buchung.Kasse = kassen.First(k => k.Id == z.Buchung.KasseId);
        }

        return new Stand(jahr, summen, zeilen, grenze, warnung, ab);
    }

    /// <summary>Steuersatz in Prozent am Tag aus den geladenen Parametern.</summary>
    private static decimal Satz(IReadOnlyList<Parameter> saetze, Umsatzsteuersatz satz, DateOnly tag)
    {
        var schluessel = satz switch
        {
            Umsatzsteuersatz.Regel => ParameterSchluessel.UmsatzsteuerRegelsatzProzent,
            Umsatzsteuersatz.Ermaessigt => ParameterSchluessel.UmsatzsteuerErmaessigterSatzProzent,
            _ => null,
        };
        var wert = saetze.Where(p => p.Schluessel == schluessel && p.GueltigAb <= tag && (p.GueltigBis == null || p.GueltigBis >= tag))
            .OrderByDescending(p => p.GueltigAb).Select(p => p.Wert).FirstOrDefault();
        return decimal.TryParse(wert, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var prozent) ? prozent : 0m;
    }
}
