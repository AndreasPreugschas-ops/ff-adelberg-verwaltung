using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Technik;

/// <summary>Was in der Vorschau ansteht.</summary>
public enum Vorschauart
{
    /// <summary>Wiederkehrende Prüfung oder Grundüberholung, meist durch Hersteller oder Werkstatt.</summary>
    [Display(Name = "Prüfung/Überholung")] Pruefung,
    /// <summary>Bauteil erreicht seine Lebensdauer.</summary>
    [Display(Name = "Bauteiltausch")] Bauteiltausch,
    /// <summary>Gerät erreicht seine Lebensdauer und muss ersetzt werden.</summary>
    [Display(Name = "Ersatz")] Ersatz,
}

/// <summary>Eine Zeile der Vorschau: so viele Geräte einer Art brauchen im Jahr diese Maßnahme.</summary>
/// <param name="Jahr">Kalenderjahr.</param>
/// <param name="Art">Prüfung, Bauteiltausch oder Ersatz.</param>
/// <param name="Geraeteart">Bezeichnung der Geräteart.</param>
/// <param name="Massnahme">Prüfart oder Bauteil.</param>
/// <param name="Inventarnummern">Betroffene Geräte (ein Gerät kann bei kurzen Intervallen mehrfach stehen).</param>
/// <param name="Ueberfaellig">Davon schon heute überfällig.</param>
public sealed record Vorschauzeile(int Jahr, Vorschauart Art, string Geraeteart, string Massnahme, IReadOnlyList<string> Inventarnummern, int Ueberfaellig)
{
    /// <summary>Anzahl der Maßnahmen.</summary>
    public int Anzahl => Inventarnummern.Count;
}

/// <summary>
/// Wartungsvorschau für die Haushaltsplanung (V61): Grundüberholungen, Flaschenprüfungen, Bauteiltausch und Ersatz wegen Lebensdauer
/// je Kalenderjahr. Kurze Intervalle (unter 24 Monaten) zählen nur, wenn eine Fremdfirma prüft (Herstellerprüfung).
/// </summary>
public sealed class Wartungsvorschau(AppDbContext db, Pruefstaende pruefstaende, ParameterLeser parameter)
{
    /// <summary>Berechnet die Vorschau ab dem Stichtag bis Ende des letzten Vorschaujahrs.</summary>
    /// <param name="stichtag">Heute.</param>
    /// <param name="ruecklauf">Nur Gerätearten mit diesem Rücklaufweg, z. B. Atemschutzwerkstatt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zeilen nach Jahr, Art und Geräteart.</returns>
    public async Task<IReadOnlyList<Vorschauzeile>> BerechnenAsync(DateOnly stichtag, Ruecklauf? ruecklauf, CancellationToken ct)
    {
        var jahre = Math.Max(1, await parameter.ZahlAsync(ParameterSchluessel.WartungsvorschauJahre, stichtag, 3, ct));
        var bis = new DateOnly(stichtag.Year + jahre - 1, 12, 31);
        var mitBauteilen = await parameter.ZahlAsync(ParameterSchluessel.BauteildatenAktiv, stichtag, 0, ct) == 1;

        var arten = await db.Geraete.AsNoTracking()
            .Where(g => g.AusgesondertAm == null && (ruecklauf == null || g.Geraeteart!.Ruecklauf == ruecklauf))
            .Select(g => new { g.Id, Art = g.Geraeteart!.Bezeichnung })
            .ToDictionaryAsync(g => g.Id, g => g.Art, ct);
        var staende = (await pruefstaende.GeraeteAsync(stichtag, ct: ct)).Where(s => arten.ContainsKey(s.Id)).ToList();

        var eintraege = new List<(DateOnly Datum, Vorschauart Art, string Geraeteart, string Massnahme, string Nummer, bool Ueberfaellig)>();
        foreach (var stand in staende)
        {
            var art = arten[stand.Id];
            foreach (var faelligkeit in stand.Faelligkeiten.Where(f => f.IntervallMonate >= 24 || f.Pruefart == Pruefart.Hersteller))
            {
                var ueberfaellig = faelligkeit.FaelligAm is null || faelligkeit.FaelligAm < stichtag;
                eintraege.AddRange(Pruefplanung.Termine(faelligkeit.FaelligAm, faelligkeit.IntervallMonate, stichtag, bis)
                    .Select((t, i) => (t, Vorschauart.Pruefung, art, Text(faelligkeit.Pruefart), stand.Nummer, i == 0 && ueberfaellig)));
            }

            if (stand.AussonderungFaellig is { } ende && ende <= bis)
            {
                eintraege.Add((ende < stichtag ? stichtag : ende, Vorschauart.Ersatz, art, "Lebensdauer erreicht", stand.Nummer, ende < stichtag));
            }
        }

        if (mitBauteilen)
        {
            var ids = arten.Keys.ToList();
            var bauteile = await db.Bauteile.AsNoTracking()
                .Where(b => b.AusgebautAm == null && ids.Contains(b.GeraetId))
                .Select(b => new { b.GeraetId, b.Geraet!.Inventarnummer, b.EingebautAm, b.HergestelltAm, b.Bauteilart!.Bezeichnung, b.Bauteilart.EingebautMonate, b.Bauteilart.GesamtMonate })
                .ToListAsync(ct);
            foreach (var b in bauteile)
            {
                var faellig = new Bauteil { EingebautAm = b.EingebautAm, HergestelltAm = b.HergestelltAm }.TauschFaellig(b.EingebautMonate, b.GesamtMonate);
                if (faellig is { } f && f <= bis)
                {
                    eintraege.Add((f < stichtag ? stichtag : f, Vorschauart.Bauteiltausch, arten[b.GeraetId], b.Bezeichnung, b.Inventarnummer, f < stichtag));
                }
            }
        }

        return eintraege
            .GroupBy(e => (e.Datum.Year, e.Art, e.Geraeteart, e.Massnahme))
            .Select(g => new Vorschauzeile(g.Key.Year, g.Key.Art, g.Key.Geraeteart, g.Key.Massnahme,
                g.Select(e => e.Nummer).Order(StringComparer.Ordinal).ToList(), g.Count(e => e.Ueberfaellig)))
            .OrderBy(z => z.Jahr).ThenBy(z => z.Art).ThenBy(z => z.Geraeteart, StringComparer.CurrentCulture).ThenBy(z => z.Massnahme, StringComparer.CurrentCulture)
            .ToList();
    }

    private static string Text(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? wert.ToString();
}
