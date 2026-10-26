using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Haushalt;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Technik;

/// <summary>Bestand, Zu- und Abgänge einer Geräteart im Berichtsjahr.</summary>
/// <param name="Geraeteart">Bezeichnung der Geräteart.</param>
/// <param name="Bestand">Bestand am Jahresende.</param>
/// <param name="Zugaenge">Im Jahr beschafft.</param>
/// <param name="Abgaenge">Im Jahr ausgesondert.</param>
public sealed record Bestandszeile(string Geraeteart, int Bestand, int Zugaenge, int Abgaenge);

/// <summary>Laufende Kosten und Nutzung eines Fahrzeugs im Berichtsjahr.</summary>
/// <param name="Funkrufname">Funkrufname.</param>
/// <param name="Typ">Fahrzeugtyp.</param>
/// <param name="Baujahr">Baujahr.</param>
/// <param name="Fahrten">Fahrten laut Fahrtenbuch.</param>
/// <param name="Kilometer">Gefahrene Kilometer.</param>
/// <param name="Liter">Getankte Liter.</param>
/// <param name="Treibstoff">Treibstoffkosten.</param>
/// <param name="Reparaturen">Kosten behobener Mängel ohne Garantiefälle.</param>
/// <param name="Maengel">Im Jahr gemeldete Mängel.</param>
public sealed record Fahrzeugkosten(string Funkrufname, string Typ, int? Baujahr, int Fahrten, int Kilometer, decimal Liter, decimal Treibstoff, decimal Reparaturen, int Maengel)
{
    /// <summary>Treibstoff und Reparaturen.</summary>
    public decimal Summe => Treibstoff + Reparaturen;
}

/// <summary>Eine Zeile des Ausblicks auf Ersatzbeschaffungen.</summary>
/// <param name="Jahr">Jahr, in dem der Ersatz ansteht.</param>
/// <param name="Gegenstand">Fahrzeug, Geräteart oder Beschaffung.</param>
/// <param name="Grund">Warum, z. B. Ende der Nutzungsdauer.</param>
/// <param name="Kosten">Geschätzte Kosten, soweit bekannt.</param>
public sealed record Ausblickzeile(int Jahr, string Gegenstand, string Grund, decimal? Kosten);

/// <summary>Position der Haushaltsanmeldung, die die Gemeinde gekürzt, gestrichen oder verschoben hat.</summary>
/// <param name="Bezeichnung">Bezeichnung der Position.</param>
/// <param name="Ergebnis">Rückmeldung der Gemeinde.</param>
/// <param name="Angemeldet">Angemeldeter Betrag im Planjahr.</param>
/// <param name="Beschlossen">Beschlossener Betrag.</param>
public sealed record Kuerzung(string Bezeichnung, Haushaltsergebnis Ergebnis, decimal Angemeldet, decimal Beschlossen);

/// <summary>Jahresbericht des Gerätewarts (V64).</summary>
/// <param name="Jahr">Berichtsjahr.</param>
/// <param name="Stichtag">Stand der Prüffristen: Jahresende oder heute.</param>
/// <param name="Pruefgegenstaende">Geräte und Fahrzeuge mit Prüfplan.</param>
/// <param name="Ueberfaellig">Davon mit überfälliger Prüfung am Stichtag.</param>
/// <param name="Pruefungen">Prüfungen im Jahr (ohne stornierte).</param>
/// <param name="PruefungenExtern">Davon durch Fremdfirmen bzw. FW Göppingen.</param>
/// <param name="NichtBestanden">Davon nicht bestanden.</param>
/// <param name="MaengelGemeldet">Im Jahr gemeldete Mängel.</param>
/// <param name="MaengelBehoben">Im Jahr behobene Mängel.</param>
/// <param name="MaengelOffen">Am Jahresende offene Mängel.</param>
/// <param name="AusserBetrieb">Davon „außer Betrieb“.</param>
/// <param name="Bestand">Bestand je Geräteart.</param>
/// <param name="FahrzeugeImDienst">Fahrzeuge im Dienst am Jahresende.</param>
/// <param name="FahrzeugZugaenge">Im Jahr in Dienst gestellte Fahrzeuge.</param>
/// <param name="FahrzeugAbgaenge">Im Jahr außer Dienst gestellte Fahrzeuge.</param>
/// <param name="Kosten">Kosten je Fahrzeug.</param>
/// <param name="Ausblick">Anstehende Ersatzbeschaffungen.</param>
/// <param name="Kuerzungen">Kürzungen der Gemeinde in der Haushaltsanmeldung des Jahres.</param>
/// <param name="AnmeldungFolgejahr">Stand der Anmeldung für das Folgejahr, z. B. „Entwurf, 42.300,00 €“; null ohne Anmeldung.</param>
public sealed record Geraetewartbericht(int Jahr, DateOnly Stichtag, int Pruefgegenstaende, int Ueberfaellig, int Pruefungen, int PruefungenExtern, int NichtBestanden,
    int MaengelGemeldet, int MaengelBehoben, int MaengelOffen, int AusserBetrieb, IReadOnlyList<Bestandszeile> Bestand, int FahrzeugeImDienst,
    IReadOnlyList<string> FahrzeugZugaenge, IReadOnlyList<string> FahrzeugAbgaenge, IReadOnlyList<Fahrzeugkosten> Kosten, IReadOnlyList<Ausblickzeile> Ausblick,
    IReadOnlyList<Kuerzung> Kuerzungen, string? AnmeldungFolgejahr)
{
    /// <summary>Anteil der Prüfgegenstände ohne überfällige Prüfung in Prozent; null ohne Prüfgegenstände.</summary>
    public decimal? Pruefquote => Pruefgegenstaende == 0 ? null : Math.Round((Pruefgegenstaende - Ueberfaellig) * 100m / Pruefgegenstaende, 1);
}

/// <summary>
/// Jahresbericht Gerätewart und Kennzahlen (V64) für Hauptversammlung (Bericht des Kommandanten, § 8 FwG, Satzung § 14) und Haushaltsanmeldung:
/// Prüfquote, Bestand mit Zu- und Abgängen, Mängel, Kosten je Fahrzeug, Ausblick auf Ersatzbeschaffungen und Kürzungen im Haushalt. Ohne Personendaten.
/// </summary>
public sealed class Geraetewartberichte(AppDbContext db, Pruefstaende pruefstaende, Wartungsvorschau vorschau)
{
    /// <summary>Erstellt den Bericht.</summary>
    /// <param name="jahr">Berichtsjahr.</param>
    /// <param name="heute">Heutiger Tag; im laufenden Jahr Stichtag der Prüffristen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Bericht.</returns>
    public async Task<Geraetewartbericht> ErstellenAsync(int jahr, DateOnly heute, CancellationToken ct)
    {
        var anfang = new DateOnly(jahr, 1, 1);
        var ende = new DateOnly(jahr, 12, 31);
        var stichtag = ende < heute ? ende : heute;
        var (vonUtc, bisUtc) = (Ortszeit.TagesbeginnUtc(anfang), Ortszeit.TagesbeginnUtc(ende.AddDays(1)));

        // Prüfquote am Stichtag: Geräte und Fahrzeuge mit Prüfplan, davon mindestens eine Prüfart überfällig.
        var staende = (await pruefstaende.GeraeteAsync(stichtag, ct: ct)).Concat(await pruefstaende.FahrzeugeAsync(stichtag, ct: ct))
            .Where(s => s.Faelligkeiten.Count > 0).ToList();
        var ueberfaellig = staende.Count(s => s.Faelligkeiten.Any(f => f.Ampel == Fristampel.Rot));

        var pruefungen = await db.Pruefungen.AsNoTracking().Where(p => p.Datum >= anfang && p.Datum <= ende && p.StorniertAm == null)
            .Select(p => new { Extern = p.PrueferExtern != null, p.Ergebnis }).ToListAsync(ct);

        var maengel = await db.Maengel.AsNoTracking().Where(m => m.GemeldetAm < bisUtc && (m.BehobenAm == null || m.BehobenAm >= vonUtc))
            .Select(m => new { m.FahrzeugId, m.GeraetId, m.GemeldetAm, m.BehobenAm, m.Schwere, m.Kosten, m.Garantiefall }).ToListAsync(ct);
        var offen = maengel.Where(m => m.BehobenAm is null || m.BehobenAm >= bisUtc).ToList();

        // Bestand je Geräteart am Jahresende mit Zu- und Abgängen.
        var geraete = await db.Geraete.AsNoTracking().Where(g => (g.BeschafftAm == null || g.BeschafftAm <= ende) && (g.AusgesondertAm == null || g.AusgesondertAm >= anfang))
            .Select(g => new { Art = g.Geraeteart!.Bezeichnung, g.BeschafftAm, g.AusgesondertAm }).ToListAsync(ct);
        var bestand = geraete.GroupBy(g => g.Art).Select(g => new Bestandszeile(g.Key,
                g.Count(x => x.AusgesondertAm is null || x.AusgesondertAm > ende),
                g.Count(x => x.BeschafftAm >= anfang && x.BeschafftAm <= ende),
                g.Count(x => x.AusgesondertAm >= anfang && x.AusgesondertAm <= ende)))
            .OrderByDescending(b => b.Bestand).ThenBy(b => b.Geraeteart).ToList();

        // Fahrzeuge: im Jahr im Dienst, mit Kosten aus Fahrtenbuch, Tankbelegen und Mängeln.
        var fahrzeuge = await db.Fahrzeuge.AsNoTracking().Where(f => (f.IndienstAm == null || f.IndienstAm <= ende) && (f.AusserDienstAm == null || f.AusserDienstAm >= anfang))
            .OrderBy(f => f.Funkrufname).ToListAsync(ct);
        var fahrten = await db.Fahrten.AsNoTracking().Where(f => f.Beginn >= vonUtc && f.Beginn < bisUtc)
            .GroupBy(f => f.FahrzeugId).Select(g => new { Id = g.Key, Anzahl = g.Count(), Km = g.Sum(f => f.Kilometer ?? 0) }).ToDictionaryAsync(g => g.Id, ct);
        var tanken = await db.Betankungen.AsNoTracking().Where(b => b.Datum >= anfang && b.Datum <= ende)
            .GroupBy(b => b.FahrzeugId).Select(g => new { Id = g.Key, Liter = g.Sum(b => b.Liter), Betrag = g.Sum(b => b.Betrag ?? 0) }).ToDictionaryAsync(g => g.Id, ct);
        var kosten = fahrzeuge.Select(f => new Fahrzeugkosten(f.Funkrufname, f.Typ, f.Baujahr,
            fahrten.GetValueOrDefault(f.Id)?.Anzahl ?? 0, fahrten.GetValueOrDefault(f.Id)?.Km ?? 0,
            tanken.GetValueOrDefault(f.Id)?.Liter ?? 0, tanken.GetValueOrDefault(f.Id)?.Betrag ?? 0,
            maengel.Where(m => m.FahrzeugId == f.Id && m.GeraetId == null && !m.Garantiefall && m.BehobenAm >= vonUtc && m.BehobenAm < bisUtc).Sum(m => m.Kosten ?? 0),
            maengel.Count(m => m.FahrzeugId == f.Id && m.GeraetId == null && m.GemeldetAm >= vonUtc))).ToList();

        // Ausblick: Fahrzeuge am Ende der Nutzungsdauer, Geräte am Ende der Lebensdauer, offene Beschaffungen.
        var ausblick = new List<Ausblickzeile>();
        foreach (var f in fahrzeuge.Where(f => f.AusserDienstAm is null && f.NutzungsdauerJahre is not null))
        {
            var beginn = f.IndienstAm?.Year ?? f.Baujahr;
            if (beginn is not null && beginn.Value + f.NutzungsdauerJahre!.Value is var faellig && faellig <= jahr + 5)
            {
                ausblick.Add(new(Math.Max(faellig, jahr + 1), $"{f.Funkrufname} ({f.Typ}{(f.Baujahr is { } bj ? $", Baujahr {bj}" : "")})",
                    faellig <= jahr ? $"Nutzungsdauer von {f.NutzungsdauerJahre} Jahren seit {faellig} überschritten" : $"Nutzungsdauer von {f.NutzungsdauerJahre} Jahren endet",
                    null));
            }
        }

        foreach (var z in (await vorschau.BerechnenAsync(heute, null, ct)).Where(z => z.Art == Vorschauart.Ersatz && z.Jahr > jahr))
        {
            ausblick.Add(new(z.Jahr, $"{z.Anzahl} × {z.Geraeteart}", "Lebensdauer endet", null));
        }

        var beschaffungen = await db.Beschaffungen.AsNoTracking()
            .Where(b => b.Status != BeschaffungStatus.Geliefert && b.Status != BeschaffungStatus.Abgelehnt && b.Haushaltsjahr > jahr)
            .OrderBy(b => b.Haushaltsjahr).ToListAsync(ct);
        ausblick.AddRange(beschaffungen.Select(b => new Ausblickzeile(b.Haushaltsjahr!.Value, b.Bezeichnung, $"Beschaffung {Anzeige(b.Status)}", b.Kostenschaetzung)));

        // Haushalt: Kürzungen der Gemeinde im Berichtsjahr und Stand der Anmeldung für das Folgejahr.
        var anmeldungen = await db.Haushaltsanmeldungen.AsNoTracking().Include(a => a.Positionen).ThenInclude(p => p.Haushaltsstelle)
            .Where(a => a.Jahr == jahr || a.Jahr == jahr + 1).ToListAsync(ct);
        var kuerzungen = anmeldungen.FirstOrDefault(a => a.Jahr == jahr)?.Angemeldet
            .Where(p => p.Ergebnis is Haushaltsergebnis.Gekuerzt or Haushaltsergebnis.Gestrichen or Haushaltsergebnis.Verschoben)
            .Select(p => new Kuerzung(p.Bezeichnung, p.Ergebnis!.Value, p.BetragPlanjahr, Haushaltsueberwachung.Ansatz(p)))
            .ToList() ?? [];
        var folgejahr = anmeldungen.FirstOrDefault(a => a.Jahr == jahr + 1) is { } f1
            ? $"{Anzeige(f1.Status)}, {f1.Summe(Haushaltsteil.Ergebnishaushalt) + f1.Summe(Haushaltsteil.Finanzhaushalt):N2} € angemeldet"
            : null;

        return new Geraetewartbericht(jahr, stichtag, staende.Count, ueberfaellig, pruefungen.Count, pruefungen.Count(p => p.Extern),
            pruefungen.Count(p => p.Ergebnis == PruefErgebnis.NichtBestanden),
            maengel.Count(m => m.GemeldetAm >= vonUtc), maengel.Count(m => m.BehobenAm >= vonUtc && m.BehobenAm < bisUtc), offen.Count,
            offen.Count(m => m.Schwere == MangelSchwere.AusserBetrieb), bestand,
            fahrzeuge.Count(f => f.AusserDienstAm is null || f.AusserDienstAm > ende),
            fahrzeuge.Where(f => f.IndienstAm >= anfang && f.IndienstAm <= ende).Select(f => $"{f.Funkrufname} ({f.Typ})").ToList(),
            fahrzeuge.Where(f => f.AusserDienstAm >= anfang && f.AusserDienstAm <= ende).Select(f => $"{f.Funkrufname} ({f.Typ})").ToList(),
            kosten, ausblick.OrderBy(a => a.Jahr).ThenBy(a => a.Gegenstand).ToList(), kuerzungen, folgejahr);
    }

    private static string Anzeige(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
            .OfType<System.ComponentModel.DataAnnotations.DisplayAttribute>().FirstOrDefault()?.Name ?? wert.ToString();
}
