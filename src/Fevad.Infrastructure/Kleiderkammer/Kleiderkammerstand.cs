using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Kleiderkammer;

/// <summary>Lagerbestand und Ausgabe eines Artikels in einer Größe.</summary>
/// <param name="ArtikelId">Schlüssel des Artikels.</param>
/// <param name="Artikel">Bezeichnung des Artikels.</param>
/// <param name="Kategorie">Kategorie.</param>
/// <param name="Einzelstueck">Stücke werden einzeln als Gerät geführt.</param>
/// <param name="Groesse">Größe.</param>
/// <param name="ImLager">Stücke im Lager.</param>
/// <param name="Ausgegeben">An Mitglieder ausgegebene Stücke.</param>
/// <param name="Mindestmenge">Mindestbestand.</param>
public sealed record BestandZeile(
    Guid ArtikelId,
    string Artikel,
    ArtikelKategorie Kategorie,
    bool Einzelstueck,
    string Groesse,
    int ImLager,
    int Ausgegeben,
    int Mindestmenge)
{
    /// <summary>Der Lagerbestand liegt unter der Mindestmenge.</summary>
    public bool UnterMindestmenge => ImLager < Mindestmenge;
}

/// <summary>Nachbestellbedarf eines Artikels in einer Größe.</summary>
/// <param name="ArtikelId">Schlüssel des Artikels.</param>
/// <param name="Artikel">Bezeichnung des Artikels.</param>
/// <param name="GeraeteartId">Geräteart bei Einzelstücken.</param>
/// <param name="Groessenart">Größenart des Artikels.</param>
/// <param name="Groesse">Größe; leer für Mitglieder ohne erfasste Größe.</param>
/// <param name="Bedarf">Berechneter Bedarf.</param>
/// <param name="OhneGrundausstattung">Mitglieder, denen der Artikel aus der Grundausstattung fehlt.</param>
public sealed record BedarfZeile(Guid ArtikelId, string Artikel, Guid? GeraeteartId, Groessenart Groessenart, string Groesse, Nachbestellbedarf Bedarf, IReadOnlyList<string> OhneGrundausstattung)
{
    /// <summary>Mitglieder ohne erfasste Größe landen in der Zeile ohne Größe; erst messen, dann bestellen.</summary>
    public bool GroesseFehlt => Groesse.Length == 0 && Groessenart != Groessenart.Einheitsgroesse;
}

/// <summary>Prüf- und Lebensdauerstand eines PSA-Einzelstücks mit dem Mitglied, das es gerade hat.</summary>
public sealed record PsaStand(Pruefstand Stand, Guid ArtikelId, string Artikel, string? Groesse, Guid? MitgliedId, string? Traeger);

/// <summary>Bestand, Nachbestellbedarf und Fristen der Kleiderkammer. Berechnet, nie gespeichert.</summary>
public sealed class Kleiderkammerstand(AppDbContext db, ParameterLeser parameter, Pruefstaende pruefstaende) : IFristQuelle
{
    /// <summary>Lagerbestand und ausgegebene Stücke je Artikel und Größe.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Bestandszeilen.</returns>
    public async Task<IReadOnlyList<BestandZeile>> BestandAsync(CancellationToken ct = default)
    {
        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => a.Aktiv)
            .Select(a => new { a.Id, a.Bezeichnung, a.Kategorie, a.GeraeteartId, a.Sortierung })
            .ToListAsync(ct);
        var artikelIds = artikel.Select(a => a.Id).ToList();
        var geraeteartIds = artikel.Where(a => a.GeraeteartId != null).Select(a => a.GeraeteartId!.Value).ToList();

        var lager = await db.Lagerbestaende.AsNoTracking()
            .Where(l => artikelIds.Contains(l.ArtikelId))
            .Select(l => new { l.ArtikelId, l.Groesse, l.Menge, l.Mindestmenge })
            .ToListAsync(ct);
        var ausgegeben = await db.Ausstattungen.AsNoTracking()
            .Where(a => a.ZurueckAm == null && artikelIds.Contains(a.ArtikelId))
            .GroupBy(a => new { a.ArtikelId, a.Groesse })
            .Select(g => new { g.Key.ArtikelId, g.Key.Groesse, Menge = g.Sum(a => a.Menge) })
            .ToListAsync(ct);
        var stuecke = await db.Geraete.AsNoTracking()
            .Where(g => g.AusgesondertAm == null && geraeteartIds.Contains(g.GeraeteartId))
            .Select(g => new { g.GeraeteartId, Groesse = g.Groesse ?? "", Ausgegeben = db.Ausstattungen.Any(a => a.GeraetId == g.Id && a.ZurueckAm == null) })
            .ToListAsync(ct);

        var zeilen = new List<(int Sortierung, BestandZeile Zeile)>();
        foreach (var a in artikel)
        {
            var groessen = lager.Where(l => l.ArtikelId == a.Id).Select(l => l.Groesse)
                .Concat(ausgegeben.Where(x => x.ArtikelId == a.Id).Select(x => x.Groesse))
                .Concat(stuecke.Where(s => s.GeraeteartId == a.GeraeteartId).Select(s => s.Groesse))
                .Distinct();
            foreach (var groesse in groessen)
            {
                var l = lager.FirstOrDefault(x => x.ArtikelId == a.Id && x.Groesse == groesse);
                var imLager = a.GeraeteartId is null
                    ? l?.Menge ?? 0
                    : stuecke.Count(s => s.GeraeteartId == a.GeraeteartId && s.Groesse == groesse && !s.Ausgegeben);
                var aus = ausgegeben.Where(x => x.ArtikelId == a.Id && x.Groesse == groesse).Sum(x => x.Menge);
                zeilen.Add((a.Sortierung, new BestandZeile(a.Id, a.Bezeichnung, a.Kategorie, a.GeraeteartId is not null, groesse, imLager, aus, l?.Mindestmenge ?? 0)));
            }
        }

        return zeilen
            .OrderBy(z => z.Sortierung).ThenBy(z => z.Zeile.Artikel).ThenBy(z => Groessenfolge(z.Zeile.Groesse))
            .Select(z => z.Zeile)
            .ToList();
    }

    /// <summary>Nachbestellbedarf aus Mindestmenge, fehlender Grundausstattung und bald auszusondernden Stücken.</summary>
    /// <param name="stichtag">Heutiger Tag; mit dem Vorlauf aus den Parametern bestimmt er, was bald ausgesondert wird.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Bedarfszeilen.</returns>
    public async Task<IReadOnlyList<BedarfZeile>> BedarfAsync(DateOnly stichtag, CancellationToken ct = default)
    {
        var vorlaufMonate = await parameter.ZahlAsync(ParameterSchluessel.KleiderkammerAussonderungVorlaufMonate, stichtag, 6, ct);
        var grenze = stichtag.AddMonths(vorlaufMonate);

        var bestand = await BestandAsync(ct);
        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => a.Aktiv)
            .Select(a => new { a.Id, a.Bezeichnung, a.GeraeteartId, a.Groessenart })
            .ToListAsync(ct);

        // Einzelstücke, die bis zur Grenze ihre Lebensdauer erreichen, getrennt nach Lager und ausgegeben.
        var ablaufend = (await PsaAsync(stichtag, ct))
            .Where(p => p.Stand.AussonderungFaellig is { } faellig && faellig <= grenze)
            .ToList();

        var beschafft = await db.Beschaffungen.AsNoTracking()
            .Where(b => b.ArtikelId != null
                && b.Status != BeschaffungStatus.Geliefert && b.Status != BeschaffungStatus.Abgelehnt)
            .GroupBy(b => new { ArtikelId = b.ArtikelId!.Value, Groesse = b.Groesse ?? "" })
            .Select(g => new { g.Key.ArtikelId, g.Key.Groesse, Menge = g.Sum(b => b.Menge) })
            .ToListAsync(ct);

        var fehlend = await FehlendeGrundausstattungAsync(stichtag, ct);

        var zeilen = new List<BedarfZeile>();
        foreach (var a in artikel)
        {
            var groessen = bestand.Where(b => b.ArtikelId == a.Id).Select(b => b.Groesse)
                .Concat(fehlend.Where(f => f.ArtikelId == a.Id).Select(f => f.Groesse))
                .Distinct();
            foreach (var groesse in groessen)
            {
                var b = bestand.FirstOrDefault(x => x.ArtikelId == a.Id && x.Groesse == groesse);
                var ohne = fehlend.Where(f => f.ArtikelId == a.Id && f.Groesse == groesse).Select(f => f.Name).Order().ToList();
                var bedarf = new Nachbestellbedarf(
                    b?.Mindestmenge ?? 0,
                    b?.ImLager ?? 0,
                    ohne.Count,
                    ablaufend.Count(p => p.ArtikelId == a.Id && (p.Groesse ?? "") == groesse && p.MitgliedId is not null),
                    ablaufend.Count(p => p.ArtikelId == a.Id && (p.Groesse ?? "") == groesse && p.MitgliedId is null),
                    beschafft.Where(x => x.ArtikelId == a.Id && x.Groesse == groesse).Sum(x => x.Menge));
                if (bedarf.Bedarf > 0 || ohne.Count > 0)
                {
                    zeilen.Add(new BedarfZeile(a.Id, a.Bezeichnung, a.GeraeteartId, a.Groessenart, groesse, bedarf, ohne));
                }
            }
        }

        return zeilen.OrderBy(z => z.Artikel).ThenBy(z => Groessenfolge(z.Groesse)).ToList();
    }

    /// <summary>Maßgeblicher Lieferantenpreis je Artikel: bevorzugter Lieferant, sonst der günstigste.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Preis mit geladenem Lieferanten je Artikel; Artikel ohne Preis fehlen.</returns>
    public async Task<IReadOnlyDictionary<Guid, ArtikelPreis>> PreiseAsync(CancellationToken ct = default) =>
        (await db.ArtikelPreise.AsNoTracking().Include(p => p.Lieferant).ToListAsync(ct))
            .GroupBy(p => p.ArtikelId)
            .Select(g => Preiswahl.Massgeblich(g))
            .OfType<ArtikelPreis>()
            .ToDictionary(p => p.ArtikelId);

    /// <summary>Legt für den offenen Bedarf eine Beschaffung mit Status Bedarf an. Gibt die Zahl der angelegten Beschaffungen zurück.</summary>
    public async Task<int> NachbestellungenAnlegenAsync(DateOnly stichtag, IReadOnlyCollection<(Guid ArtikelId, string Groesse)> auswahl, CancellationToken ct = default)
    {
        var angelegt = 0;
        var preise = await PreiseAsync(ct);
        foreach (var zeile in (await BedarfAsync(stichtag, ct)).Where(z => z.Bedarf.Offen > 0 && !z.GroesseFehlt && auswahl.Contains((z.ArtikelId, z.Groesse))))
        {
            db.Beschaffungen.Add(new Beschaffung
            {
                Bezeichnung = zeile.Groesse.Length == 0 ? zeile.Artikel : $"{zeile.Artikel}, Größe {zeile.Groesse}",
                Menge = zeile.Bedarf.Offen,
                Begruendung = "Kleiderkammer: " + string.Join("; ", zeile.Bedarf.Gruende()),
                Status = BeschaffungStatus.Bedarf,
                GeraeteartId = zeile.GeraeteartId,
                ArtikelId = zeile.ArtikelId,
                Groesse = zeile.Groesse,
                Haushaltsjahr = stichtag.Year,
                Kostenschaetzung = preise.GetValueOrDefault(zeile.ArtikelId)?.Preis * zeile.Bedarf.Offen,
                Lieferant = preise.GetValueOrDefault(zeile.ArtikelId)?.Lieferant?.Name,
                Bestellnummer = preise.GetValueOrDefault(zeile.ArtikelId)?.Bestellnummer,
            });
            angelegt++;
        }

        await db.SaveChangesAsync(ct);
        return angelegt;
    }

    /// <summary>Alle PSA-Einzelstücke (nicht ausgesondert) mit Prüffristen, Lebensdauer und aktuellem Träger.</summary>
    public async Task<IReadOnlyList<PsaStand>> PsaAsync(DateOnly stichtag, CancellationToken ct = default)
    {
        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => a.GeraeteartId != null)
            .Select(a => new { a.Id, a.Bezeichnung, GeraeteartId = a.GeraeteartId!.Value })
            .ToListAsync(ct);
        var artIds = artikel.Select(a => a.GeraeteartId).ToList();

        var geraete = await db.Geraete.AsNoTracking()
            .Where(g => g.AusgesondertAm == null && artIds.Contains(g.GeraeteartId))
            .Select(g => new { g.Id, g.GeraeteartId, g.Groesse })
            .ToDictionaryAsync(g => g.Id, ct);
        var traeger = await db.Ausstattungen.AsNoTracking()
            .Where(a => a.GeraetId != null && a.ZurueckAm == null)
            .Select(a => new { GeraetId = a.GeraetId!.Value, a.MitgliedId, a.Mitglied!.Nachname, a.Mitglied.Vorname })
            .ToDictionaryAsync(a => a.GeraetId, ct);

        return (await pruefstaende.GeraeteAsync(stichtag, ct: ct))
            .Where(s => geraete.ContainsKey(s.Id))
            .Select(s =>
            {
                var g = geraete[s.Id];
                var a = artikel.First(x => x.GeraeteartId == g.GeraeteartId);
                var t = traeger.GetValueOrDefault(s.Id);
                return new PsaStand(s, a.Id, a.Bezeichnung, g.Groesse, t?.MitgliedId, t is null ? null : $"{t.Nachname}, {t.Vorname}");
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Fristhinweis>> FaelligeAsync(DateOnly stichtag, CancellationToken ct)
    {
        var warnungTage = await parameter.ZahlAsync(ParameterSchluessel.FristWarnungTage, stichtag, 90, ct);
        var hinweise = new List<Fristhinweis>();
        foreach (var psa in await PsaAsync(stichtag, ct))
        {
            var gegenstand = $"{psa.Stand.Nummer} {psa.Artikel}" + (psa.Traeger is null ? " (Lager)" : $" ({psa.Traeger})");
            hinweise.AddRange(psa.Stand.Faelligkeiten
                .Where(f => f.Ampel != Fristampel.Gruen)
                .Select(f => new Fristhinweis("Kleiderkammer", gegenstand, f.NieGeprueft ? $"{Text(f.Pruefart)} (noch nie geprüft)" : Text(f.Pruefart),
                    f.FaelligAm, f.Ampel, Berechtigungen.KleiderkammerBearbeiten)));

            var ampel = Frist.Bewerten(psa.Stand.AussonderungFaellig, stichtag, warnungTage);
            if (ampel != Fristampel.Gruen)
            {
                hinweise.Add(new Fristhinweis("Kleiderkammer", gegenstand, "Aussonderung (Lebensdauer)", psa.Stand.AussonderungFaellig, ampel,
                    Berechtigungen.KleiderkammerBearbeiten));
            }
        }

        return hinweise;
    }

    /// <summary>
    /// Aktive Mitglieder ohne einen Grundausstattungsartikel ihrer Abteilungsart; bei mehreren Einsatzabteilungen zählt jede.
    /// Die Größe kommt aus dem Größenprofil; leer heißt „unbekannt“.
    /// </summary>
    private async Task<IReadOnlyList<(Guid ArtikelId, string Groesse, string Name)>> FehlendeGrundausstattungAsync(DateOnly stichtag, CancellationToken ct)
    {
        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => a.Aktiv && a.GrundausstattungArt != null)
            .Select(a => new { a.Id, Art = a.GrundausstattungArt!.Value, a.Groessenart })
            .ToListAsync(ct);
        if (artikel.Count == 0)
        {
            return [];
        }

        var arten = artikel.Select(a => a.Art).Distinct().ToList();
        var mitglieder = await db.Mitgliedschaften.AsNoTracking()
            .Where(s => arten.Contains(s.Abteilung!.Art)
                && s.Status != MitgliedStatus.Ausgeschieden
                && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag))
            .Select(s => new { s.Abteilung!.Art, s.MitgliedId, s.Mitglied!.Nachname, s.Mitglied.Vorname })
            .ToListAsync(ct);
        var mitgliedIds = mitglieder.Select(m => m.MitgliedId).Distinct().ToList();

        var hat = (await db.Ausstattungen.AsNoTracking()
                .Where(a => a.ZurueckAm == null && mitgliedIds.Contains(a.MitgliedId))
                .Select(a => new { a.MitgliedId, a.ArtikelId })
                .ToListAsync(ct))
            .Select(a => (a.MitgliedId, a.ArtikelId))
            .ToHashSet();
        var groessen = (await db.Groessenangaben.AsNoTracking()
                .Where(g => mitgliedIds.Contains(g.MitgliedId))
                .Select(g => new { g.MitgliedId, g.Groessenart, g.Wert })
                .ToListAsync(ct))
            .ToDictionary(g => (g.MitgliedId, g.Groessenart), g => g.Wert);

        return artikel
            .SelectMany(a => mitglieder
                .Where(m => m.Art == a.Art && !hat.Contains((m.MitgliedId, a.Id)))
                .DistinctBy(m => m.MitgliedId)
                .Select(m => (a.Id,
                    a.Groessenart == Groessenart.Einheitsgroesse ? "" : groessen.GetValueOrDefault((m.MitgliedId, a.Groessenart)) ?? "",
                    $"{m.Nachname}, {m.Vorname}")))
            .ToList();
    }

    /// <summary>Sortiert Größen natürlich: Zahlen numerisch, Konfektionsgrößen von XS bis 5XL, Rest alphabetisch.</summary>
    public static (int, double, string) Groessenfolge(string groesse)
    {
        string[] konfektion = ["XXS", "XS", "S", "M", "L", "XL", "XXL", "2XL", "3XL", "XXXL", "4XL", "5XL"];
        var index = Array.IndexOf(konfektion, groesse);
        if (index >= 0)
        {
            return (1, index, groesse);
        }

        var zahl = new string(groesse.TakeWhile(c => char.IsDigit(c) || c is ',' or '.').ToArray()).Replace(',', '.');
        return double.TryParse(zahl, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wert)
            ? (0, wert, groesse)
            : (2, 0, groesse);
    }

    private static string Text(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? wert.ToString();
}
