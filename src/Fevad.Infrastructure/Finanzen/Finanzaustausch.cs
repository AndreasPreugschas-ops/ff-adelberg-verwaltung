using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Finanzen;

/// <summary>
/// Datenaustausch mit der Kämmerei (V23): liefert die Zeilen je Exportart für die eingestellte Spaltenzuordnung und spielt die Rückmeldung
/// der Gemeindekasse zu Kostenersatz-Forderungen als Bescheidschritte ein. Die Gemeinde nutzt SAP; Schlüssel für Personen ist die Personalnummer (F4).
/// </summary>
public sealed class Finanzaustausch(AppDbContext db, AbrechnungExport abrechnungen, TimeProvider zeit)
{
    /// <summary>Eingestelltes oder Standardformat einer Art.</summary>
    /// <param name="art">Die Art.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das Format; ungespeichert, solange keins eingerichtet ist.</returns>
    public async Task<Exportformat> FormatAsync(ExportArt art, CancellationToken ct) =>
        await db.Exportformate.AsNoTracking().FirstOrDefaultAsync(f => f.Art == art, ct) ?? Finanzexport.Standard(art);

    /// <summary>Sollstellungen: festgesetzte Bescheide zu Einsätzen des Jahres.</summary>
    /// <param name="jahr">Einsatzjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zeilen.</returns>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> SollstellungenAsync(int jahr, CancellationToken ct)
    {
        var (von, bis) = (Ortszeit.TagesbeginnUtc(new DateOnly(jahr, 1, 1)), Ortszeit.TagesbeginnUtc(new DateOnly(jahr + 1, 1, 1)));
        var bescheide = await db.Kostenbescheide.AsNoTracking().Include(b => b.Ereignisse).Include(b => b.Kostenschuldner)
            .Include(b => b.Kostenersatzvorgang).ThenInclude(v => v!.Einsatz)
            .Where(b => b.Kostenersatzvorgang!.Einsatz!.Alarmiert >= von && b.Kostenersatzvorgang.Einsatz.Alarmiert < bis)
            .AsSplitQuery().ToListAsync(ct);
        return bescheide.Where(b => b.Betrag is not null)
            .OrderBy(b => b.Kostenersatzvorgang!.Einsatz!.Alarmiert).ThenBy(b => b.Kassenzeichen)
            .Select(b =>
            {
                var e = b.Kostenersatzvorgang!.Einsatz!;
                return (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
                {
                    ["Kassenzeichen"] = b.Kassenzeichen, ["Schuldner"] = b.Kostenschuldner?.Name, ["Anschrift"] = b.Kostenschuldner?.Anschrift,
                    ["Betrag"] = b.Betrag, ["FaelligAm"] = b.FaelligAm, ["BekanntgegebenAm"] = b.BekanntgegebenAm,
                    ["Einsatznummer"] = e.Einsatznummer, ["Einsatzdatum"] = Ortszeit.Datum(e.Alarmiert),
                    ["Tatbestand"] = b.Kostenersatzvorgang.Tatbestand is { } t ? Text(t) : null,
                    ["Text"] = $"Kostenersatz Feuerwehreinsatz {e.Einsatznummer}",
                };
            })
            .ToList();
    }

    /// <summary>Entschädigung: eine Zeile je Person einer Abrechnung.</summary>
    /// <param name="abrechnungId">Kennung der Abrechnung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zeilen.</returns>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> EntschaedigungAsync(Guid abrechnungId, CancellationToken ct)
    {
        var daten = await abrechnungen.LadenAsync(abrechnungId, ct);
        var a = daten.Abrechnung;
        return daten.Summen.OrderBy(s => s.Personalnummer).Select(s => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
        {
            ["Personalnummer"] = s.Personalnummer, ["Nachname"] = s.Nachname, ["Vorname"] = s.Vorname, ["Stunden"] = s.Stunden, ["Betrag"] = s.Betrag,
            ["Von"] = a.Von, ["Bis"] = a.Bis, ["Abrechnung"] = a.Bezeichnung, ["Text"] = $"Entschädigung Feuerwehr {a.Von:MM/yyyy}–{a.Bis:MM/yyyy}",
        }).ToList();
    }

    /// <summary>
    /// Kameradschaftskassen: festgeschriebene Buchungen des Jahres mit Steuerbereich und Steuersatz. Ohne Jugendzugang erscheinen Buchungen der Kassen
    /// von Jugend- und Kinderfeuerwehr ohne Partner und Text, damit keine Namen von Jugendlichen oder Eltern in den Export gelangen.
    /// </summary>
    /// <param name="jahr">Kassenjahr.</param>
    /// <param name="mitJugend">Der Aufrufer hat Zugang zu Jugenddaten.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zeilen.</returns>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> KassenAsync(int jahr, bool mitJugend, CancellationToken ct)
    {
        var buchungen = await db.Buchungen.AsNoTracking().Include(b => b.Kasse).ThenInclude(k => k!.Abteilung)
            .Where(b => b.Jahr == jahr && b.FestgeschriebenAm != null)
            .OrderBy(b => b.Kasse!.Name).ThenBy(b => b.Belegnummer).ToListAsync(ct);
        return buchungen.Select(b =>
        {
            var abgeschirmt = !mitJugend && b.Kasse!.Abteilung?.Art is AbteilungArt.Jugend or AbteilungArt.Kinder;
            return (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["Kasse"] = b.Kasse!.Name, ["Jahr"] = b.Jahr, ["Belegnummer"] = b.Belegnummer, ["Datum"] = b.Datum, ["Art"] = Text(b.Art),
                ["Betrag"] = b.Betrag, ["Steuerbereich"] = Text(b.Steuerbereich), ["Steuersatz"] = Text(b.Steuersatz),
                ["Partner"] = abgeschirmt ? null : b.Partner, ["Text"] = abgeschirmt ? "(Jugendkasse)" : b.Text,
            };
        }).ToList();
    }

    /// <summary>Inventar: Geräte und Fahrzeuge mit Anlagendaten.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zeilen.</returns>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> InventarAsync(CancellationToken ct)
    {
        var fahrzeuge = await db.Fahrzeuge.AsNoTracking().OrderBy(f => f.Funkrufname).ToListAsync(ct);
        var geraete = await db.Geraete.AsNoTracking().OrderBy(g => g.Inventarnummer).ToListAsync(ct);
        var zeilen = fahrzeuge.Select(f => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
        {
            ["Inventarnummer"] = f.Funkrufname, ["Anlagennummer"] = f.Anlagennummer, ["Bezeichnung"] = $"{f.Typ} {f.Kennzeichen}".Trim(), ["Typ"] = "Fahrzeug",
            ["BeschafftAm"] = f.IndienstAm, ["Anschaffungswert"] = f.Anschaffungswert, ["Foerdermittel"] = f.Foerdermittel,
            ["NutzungsdauerJahre"] = f.NutzungsdauerJahre, ["Eigentuemer"] = Text(f.Eigentuemer), ["AusgesondertAm"] = f.AusserDienstAm,
        }).ToList();
        zeilen.AddRange(geraete.Select(g => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
        {
            ["Inventarnummer"] = g.Inventarnummer, ["Anlagennummer"] = g.Anlagennummer, ["Bezeichnung"] = g.Bezeichnung, ["Typ"] = "Gerät",
            ["BeschafftAm"] = g.BeschafftAm, ["Anschaffungswert"] = g.Anschaffungspreis, ["Foerdermittel"] = g.Foerdermittel,
            ["NutzungsdauerJahre"] = g.NutzungsdauerJahre, ["Eigentuemer"] = Text(g.Eigentuemer), ["AusgesondertAm"] = g.AusgesondertAm,
        }));
        return zeilen;
    }

    /// <summary>Ergebnis einer eingespielten Rückmeldung.</summary>
    /// <param name="Zahlungen">Erfasste Zahlungen.</param>
    /// <param name="Mahnungen">Erfasste Mahnungen.</param>
    /// <param name="Unveraendert">Zeilen ohne Änderung.</param>
    /// <param name="Fehler">Meldungen zu Zeilen, die nicht übernommen wurden.</param>
    public sealed record Ergebnis(int Zahlungen, int Mahnungen, int Unveraendert, IReadOnlyList<string> Fehler);

    /// <summary>
    /// Spielt die Rückmeldung ein: Ist mehr bezahlt als erfasst, entsteht eine Zahlung über die Differenz; „bezahlt“ ohne Betrag gleicht den offenen
    /// Betrag aus; „gemahnt“ erfasst eine Mahnung, wenn noch keine da ist. Unbekannte oder doppelte Kassenzeichen werden gemeldet.
    /// </summary>
    /// <param name="inhalt">Dateiinhalt.</param>
    /// <param name="bearbeiter">Anmeldename für die Schritte.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<Ergebnis> RueckmeldungEinspielenAsync(string inhalt, string? bearbeiter, CancellationToken ct)
    {
        var format = await FormatAsync(ExportArt.Rueckmeldung, ct);
        var zeilen = Finanzexport.RueckmeldungLesen(format, inhalt, out var lesefehler);
        var fehler = lesefehler.ToList();
        var kz = zeilen.Select(z => z.Kassenzeichen).Distinct().ToList();
        var bescheide = await db.Kostenbescheide.Include(b => b.Ereignisse).Include(b => b.Kostenersatzvorgang).ThenInclude(v => v!.Bescheide).ThenInclude(x => x.Ereignisse)
            .Where(b => b.Kassenzeichen != null && kz.Contains(b.Kassenzeichen)).AsSplitQuery().ToListAsync(ct);
        var heute = Ortszeit.Datum(zeit.GetUtcNow());
        var jetzt = zeit.GetUtcNow();
        int zahlungen = 0, mahnungen = 0, gleich = 0;
        foreach (var z in zeilen)
        {
            var treffer = bescheide.Where(b => b.Kassenzeichen == z.Kassenzeichen).ToList();
            if (treffer.Count != 1)
            {
                fehler.Add(treffer.Count == 0 ? $"Kassenzeichen {z.Kassenzeichen}: kein Bescheid bekannt." : $"Kassenzeichen {z.Kassenzeichen}: mehrfach vergeben.");
                continue;
            }

            var b = treffer[0];
            var tag = z.Datum ?? heute;
            var neu = 0;
            var differenz = z.Bezahlt is { } summe ? summe - b.Bezahlt : z.Status == "bezahlt" ? b.Offen : 0;
            if (differenz > 0)
            {
                if (b.Pruefen(BescheidSchritt.Zahlung, differenz, false) is { } f)
                {
                    fehler.Add($"Kassenzeichen {z.Kassenzeichen}: {f}");
                    continue;
                }

                Hinzufuegen(b, BescheidSchritt.Zahlung, tag, differenz);
                zahlungen++;
                neu++;
            }

            if (z.Status == "gemahnt" && !b.Ereignisse.Any(e => e.Schritt == BescheidSchritt.Mahnung) && b.Pruefen(BescheidSchritt.Mahnung, null, false) is null)
            {
                Hinzufuegen(b, BescheidSchritt.Mahnung, tag, null);
                mahnungen++;
                neu++;
            }

            if (neu == 0)
            {
                gleich++;
            }
        }

        // Ist jeder Bescheid eines Vorgangs erledigt, gilt der Kostenersatz des Einsatzes als abgeschlossen (Beginn L3).
        var vorgaenge = bescheide.Select(b => b.Kostenersatzvorgang!).Distinct().Where(v => v.Bescheide.All(x => x.Erledigt)).Select(v => v.EinsatzId).ToList();
        if (vorgaenge.Count > 0)
        {
            foreach (var einsatz in await db.Einsaetze.Where(e => vorgaenge.Contains(e.Id) && e.KostenersatzOffen).ToListAsync(ct))
            {
                einsatz.KostenersatzOffen = false;
                einsatz.KostenersatzAbgeschlossenAm = heute;
            }
        }

        await db.SaveChangesAsync(ct);
        return new Ergebnis(zahlungen, mahnungen, gleich, fehler);

        void Hinzufuegen(Kostenbescheid b, BescheidSchritt schritt, DateOnly tag, decimal? betrag)
        {
            var e = new BescheidEreignis
            {
                KostenbescheidId = b.Id, Schritt = schritt, Datum = tag, Betrag = betrag, Vermerk = "Rückmeldung Gemeindekasse", Bearbeiter = bearbeiter, ErfasstAm = jetzt,
            };
            db.BescheidEreignisse.Add(e);
            b.Ereignisse.Add(e);
        }
    }

    private static string Text(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
            .OfType<System.ComponentModel.DataAnnotations.DisplayAttribute>().FirstOrDefault()?.Name ?? wert.ToString();
}
