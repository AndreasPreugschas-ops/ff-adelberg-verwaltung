using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Technik;

/// <summary>Prüf- und Einsatzstand eines Geräts oder Fahrzeugs am Stichtag. Wird nie gespeichert.</summary>
public sealed record Pruefstand(
    Guid Id,
    bool IstFahrzeug,
    string Nummer,
    string Bezeichnung,
    string? Geraeteart,
    Guid? FahrzeugId,
    string? Standort,
    GeraetZustand Zustand,
    IReadOnlyList<Faelligkeit> Faelligkeiten,
    DateOnly? AussonderungFaellig,
    Fristampel Ampel)
{
    public DateOnly? NaechsterTermin => Pruefplanung.NaechsterTermin(Faelligkeiten);
    public bool NieGeprueft => Faelligkeiten.Any(f => f.NieGeprueft);
}

/// <summary>Berechnet Prüffristen, Zustand und Aussonderung für Geräte und Fahrzeuge.</summary>
public sealed class Pruefstaende(AppDbContext db, ParameterLeser parameter) : IFristQuelle
{
    public async Task<IReadOnlyList<Pruefstand>> GeraeteAsync(
        DateOnly stichtag, Guid? geraetId = null, Guid? fahrzeugId = null, bool mitAusgesonderten = false, CancellationToken ct = default)
    {
        var warnungTage = await parameter.ZahlAsync(ParameterSchluessel.PruefungWarnungTage, stichtag, 30, ct);
        var aussonderungWarnungTage = await parameter.ZahlAsync(ParameterSchluessel.FristWarnungTage, stichtag, 90, ct);

        var abfrage = db.Geraete.AsNoTracking();
        if (geraetId is not null)
        {
            abfrage = abfrage.Where(g => g.Id == geraetId);
        }
        else
        {
            if (!mitAusgesonderten)
            {
                abfrage = abfrage.Where(g => g.AusgesondertAm == null);
            }

            if (fahrzeugId is not null)
            {
                abfrage = abfrage.Where(g => g.FahrzeugId == fahrzeugId);
            }
        }

        var geraete = await abfrage
            .Select(g => new
            {
                g.Id,
                g.Inventarnummer,
                g.Bezeichnung,
                g.GeraeteartId,
                Art = g.Geraeteart!.Bezeichnung,
                g.Geraeteart.AussonderungNachJahren,
                g.FahrzeugId,
                Fahrzeug = g.Fahrzeug!.Funkrufname,
                g.Lagerort,
                g.Herstelldatum,
                g.Baujahr,
                g.IndienstAm,
                g.AusgesondertAm,
            })
            .ToListAsync(ct);

        var ids = geraete.Select(g => g.Id).ToList();
        var plaene = await PlaeneAsync(geraete.Select(g => g.GeraeteartId), ct);
        var pruefungen = (await db.Pruefungen.AsNoTracking()
                .Where(p => p.GeraetId != null && ids.Contains(p.GeraetId.Value))
                .Select(p => new { GeraetId = p.GeraetId!.Value, Kurz = new PruefungKurz(p.Pruefart, p.Datum, p.Ergebnis, p.NaechsteFaellig) })
                .ToListAsync(ct))
            .ToLookup(p => p.GeraetId, p => p.Kurz);
        var maengel = (await db.Maengel.AsNoTracking()
                .Where(m => m.GeraetId != null && ids.Contains(m.GeraetId.Value) && m.Status != MangelStatus.Behoben)
                .Select(m => new { GeraetId = m.GeraetId!.Value, m.Schwere })
                .ToListAsync(ct))
            .ToLookup(m => m.GeraetId, m => m.Schwere);

        return geraete
            .Select(g =>
            {
                var faelligkeiten = Pruefplanung.Berechnen(plaene[g.GeraeteartId], pruefungen[g.Id], stichtag, warnungTage);
                var geraet = new Geraet { Inventarnummer = g.Inventarnummer, Bezeichnung = g.Bezeichnung, Herstelldatum = g.Herstelldatum, Baujahr = g.Baujahr, IndienstAm = g.IndienstAm };
                var aussonderung = geraet.AussonderungFaellig(g.AussonderungNachJahren);
                var zustand = Geraet.ZustandBerechnen(g.AusgesondertAm, maengel[g.Id]);
                var ampel = zustand == GeraetZustand.Ausgesondert
                    ? Fristampel.Gruen
                    : new[] { Pruefplanung.Ampel(faelligkeiten), Frist.Bewerten(aussonderung, stichtag, aussonderungWarnungTage) }.Max();
                var standort = g.Fahrzeug is null ? g.Lagerort : g.Lagerort is null ? g.Fahrzeug : $"{g.Fahrzeug}, {g.Lagerort}";
                return new Pruefstand(g.Id, false, g.Inventarnummer, g.Bezeichnung, g.Art, g.FahrzeugId, standort, zustand, faelligkeiten, aussonderung, ampel);
            })
            .OrderBy(p => p.Nummer)
            .ToList();
    }

    public async Task<IReadOnlyList<Pruefstand>> FahrzeugeAsync(DateOnly stichtag, Guid? fahrzeugId = null, CancellationToken ct = default)
    {
        var warnungTage = await parameter.ZahlAsync(ParameterSchluessel.PruefungWarnungTage, stichtag, 30, ct);

        var abfrage = db.Fahrzeuge.AsNoTracking().Where(f => f.AusserDienstAm == null);
        if (fahrzeugId is not null)
        {
            abfrage = db.Fahrzeuge.AsNoTracking().Where(f => f.Id == fahrzeugId);
        }

        var fahrzeuge = await abfrage
            .Select(f => new { f.Id, f.Funkrufname, f.Typ, f.PruefvorlageId, Vorlage = f.Pruefvorlage!.Bezeichnung, f.AusserDienstAm })
            .ToListAsync(ct);

        var ids = fahrzeuge.Select(f => f.Id).ToList();
        var plaene = await PlaeneAsync(fahrzeuge.Where(f => f.PruefvorlageId != null).Select(f => f.PruefvorlageId!.Value), ct);
        var pruefungen = (await db.Pruefungen.AsNoTracking()
                .Where(p => p.FahrzeugId != null && ids.Contains(p.FahrzeugId.Value))
                .Select(p => new { FahrzeugId = p.FahrzeugId!.Value, Kurz = new PruefungKurz(p.Pruefart, p.Datum, p.Ergebnis, p.NaechsteFaellig) })
                .ToListAsync(ct))
            .ToLookup(p => p.FahrzeugId, p => p.Kurz);
        var maengel = (await db.Maengel.AsNoTracking()
                .Where(m => m.FahrzeugId != null && ids.Contains(m.FahrzeugId.Value) && m.Status != MangelStatus.Behoben)
                .Select(m => new { FahrzeugId = m.FahrzeugId!.Value, m.Schwere })
                .ToListAsync(ct))
            .ToLookup(m => m.FahrzeugId, m => m.Schwere);

        return fahrzeuge
            .Select(f =>
            {
                var plan = f.PruefvorlageId is null ? [] : plaene[f.PruefvorlageId.Value];
                var faelligkeiten = Pruefplanung.Berechnen(plan, pruefungen[f.Id], stichtag, warnungTage);
                var zustand = Geraet.ZustandBerechnen(f.AusserDienstAm, maengel[f.Id]);
                return new Pruefstand(f.Id, true, f.Funkrufname, f.Typ, f.Vorlage, f.Id, null, zustand, faelligkeiten, null, Pruefplanung.Ampel(faelligkeiten));
            })
            .OrderBy(p => p.Nummer)
            .ToList();
    }

    /// <summary>Fristen für die tägliche Erinnerung: alles, was nicht grün ist, an die Prüfberechtigten.</summary>
    public async Task<IReadOnlyList<Fristhinweis>> FaelligeAsync(DateOnly stichtag, CancellationToken ct)
    {
        var aussonderungWarnungTage = await parameter.ZahlAsync(ParameterSchluessel.FristWarnungTage, stichtag, 90, ct);
        var staende = (await FahrzeugeAsync(stichtag, ct: ct)).Concat(await GeraeteAsync(stichtag, ct: ct));
        var hinweise = new List<Fristhinweis>();
        foreach (var stand in staende)
        {
            var gegenstand = $"{stand.Nummer} {stand.Bezeichnung}";
            hinweise.AddRange(stand.Faelligkeiten
                .Where(f => f.Ampel != Fristampel.Gruen)
                .Select(f => new Fristhinweis("Technik", gegenstand, f.NieGeprueft ? $"{Text(f.Pruefart)} (noch nie geprüft)" : Text(f.Pruefart),
                    f.FaelligAm, f.Ampel, Berechtigungen.TechnikPruefen)));

            var aussonderungAmpel = Frist.Bewerten(stand.AussonderungFaellig, stichtag, aussonderungWarnungTage);
            if (aussonderungAmpel != Fristampel.Gruen)
            {
                hinweise.Add(new Fristhinweis("Technik", gegenstand, "Aussonderung (Lebensdauer)", stand.AussonderungFaellig,
                    aussonderungAmpel, Berechtigungen.TechnikBearbeiten));
            }
        }

        return hinweise;
    }

    private async Task<ILookup<Guid, PruefplanPosition>> PlaeneAsync(IEnumerable<Guid> geraeteartIds, CancellationToken ct)
    {
        var ids = geraeteartIds.Distinct().ToList();
        return (await db.PruefplanPositionen.AsNoTracking().Where(p => ids.Contains(p.GeraeteartId)).ToListAsync(ct))
            .ToLookup(p => p.GeraeteartId);
    }

    private static string Text(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? wert.ToString();
}
