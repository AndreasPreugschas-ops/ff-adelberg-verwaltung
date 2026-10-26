using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Haushalt;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Haushalt;

/// <summary>Wertgrenze und Steuersatz, nach denen Positionen Investition oder Aufwand sind.</summary>
/// <param name="GrenzeNetto">Investition ab diesem Einzelpreis netto.</param>
/// <param name="SteuersatzProzent">Umsatzsteuer-Regelsatz.</param>
public sealed record Haushaltsgrenzen(decimal GrenzeNetto, decimal SteuersatzProzent);

/// <summary>
/// Haushaltsanmeldung (V29): Vorschlagszeilen aus den Daten von FEVAD. Der Gerätewart bestätigt oder streicht sie. Vorschläge kommen
/// aus offenen Beschaffungen (mit Z-Feu-Zuwendung), Ersatz nach Lebensdauer, Ist des Vorjahres (Treibstoff, Reparaturen,
/// Verbrauchsmaterial), Kostenansätzen (Werkstatt Göppingen, Fremdprüfungen, Versicherung, Wäsche) und den Zuschüssen an die Kassen.
/// </summary>
public sealed class Haushaltsplanung(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Liest Wertgrenze und Steuersatz.</summary>
    /// <param name="stichtag">Tag, für den die Parameter gelten.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Grenzen.</returns>
    public async Task<Haushaltsgrenzen> GrenzenAsync(DateOnly stichtag, CancellationToken ct) => new(
        await parameter.DezimalAsync(ParameterSchluessel.HaushaltInvestitionAbWertEuroNetto, stichtag, ct) ?? 1000m,
        await parameter.DezimalAsync(ParameterSchluessel.UmsatzsteuerRegelsatzProzent, stichtag, ct) ?? 19m);

    /// <summary>Erzeugt Vorschlagszeilen, die in der Anmeldung noch fehlen; fügt nichts hinzu.</summary>
    /// <param name="anmeldung">Anmeldung mit geladenen Positionen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Neue Positionen im Status Vorschlag.</returns>
    public async Task<IReadOnlyList<Haushaltsposition>> VorschlaegeAsync(Haushaltsanmeldung anmeldung, CancellationToken ct)
    {
        var jahr = anmeldung.Jahr;
        var vorjahrAb = new DateOnly(jahr - 2, 1, 1);
        var vorjahrBis = new DateOnly(jahr - 2, 12, 31);
        // Die Anmeldung entsteht im Herbst vor dem Planjahr: „Vorjahr“ ist das letzte abgeschlossene Jahr.
        var stichtag = new DateOnly(jahr - 1, 9, 1);
        var grenzen = await GrenzenAsync(stichtag, ct);
        var stellen = await db.Haushaltsstellen.AsNoTracking().Where(h => h.Aktiv).ToDictionaryAsync(h => h.Code, ct);
        var vorhanden = anmeldung.Positionen.Select(p => p.Herkunft).Where(h => h != null).ToHashSet();
        var neu = new List<Haushaltsposition>();

        void Neu(string herkunft, string stelle, Haushaltsbereich bereich, string bezeichnung, decimal menge, decimal preis, Preisquelle quelle,
            string? rechtsgrundlage, bool pflicht = false, Action<Haushaltsposition>? weiter = null)
        {
            // Ohne Menge gibt es nichts anzumelden; ein Ist von 0 auch nicht. Ein Kostenansatz von 0 bleibt als „Satz fehlt“ stehen.
            if (vorhanden.Contains(herkunft) || !stellen.TryGetValue(stelle, out var haushaltsstelle) || menge <= 0 || (quelle == Preisquelle.Vorjahr && preis <= 0))
            {
                return;
            }

            var position = new Haushaltsposition
            {
                HaushaltsanmeldungId = anmeldung.Id, HaushaltsstelleId = haushaltsstelle.Id, Haushaltsstelle = haushaltsstelle, Bereich = bereich,
                Bezeichnung = bezeichnung.Length > 200 ? bezeichnung[..200] : bezeichnung, Menge = menge, Einzelpreis = Math.Round(preis, 2),
                Preisquelle = quelle, Rechtsgrundlage = rechtsgrundlage, Pflicht = pflicht, Status = PositionStatus.Vorschlag, Herkunft = herkunft,
            };
            weiter?.Invoke(position);
            neu.Add(position);
            vorhanden.Add(herkunft);
        }

        // 1. Offene Beschaffungen dieses Haushaltsjahres, mit Z-Feu-Zuwendung aus dem Antrag.
        var beschaffungen = await db.Beschaffungen.AsNoTracking()
            .Where(b => b.Haushaltsjahr == jahr && (b.Status == BeschaffungStatus.Bedarf || b.Status == BeschaffungStatus.Angemeldet || b.Status == BeschaffungStatus.Genehmigt))
            .ToListAsync(ct);
        var antraege = await db.Foerderantraege.AsNoTracking().Include(a => a.Foerdergegenstand)
            .Where(a => a.Status != Foerderstatus.Abgelehnt).ToDictionaryAsync(a => a.BeschaffungId, ct);
        foreach (var b in beschaffungen)
        {
            var menge = Math.Max(1, b.Menge);
            var einzel = (b.Kostenschaetzung ?? 0) / menge;
            antraege.TryGetValue(b.Id, out var antrag);
            var investiv = einzel / (1 + grenzen.SteuersatzProzent / 100m) >= grenzen.GrenzeNetto;
            var stelle = investiv ? (antrag is not null || (b.FahrzeugId is not null && b.GeraeteartId is null && b.ArtikelId is null) ? "FH-7832" : "FH-7831")
                : b.ArtikelId is not null ? "EH-4271" : "EH-4221";
            Neu($"beschaffung:{b.Id}", stelle, b.ArtikelId is not null ? Haushaltsbereich.Kleiderkammer : Haushaltsbereich.Technik, b.Bezeichnung, menge, einzel,
                Preisquelle.Schaetzung, antrag is not null ? "§ 3 Abs. 1 FwG; Feuerwehrbedarfsplan; VwV Z-Feu" : "§ 3 Abs. 1 FwG",
                pflicht: b.Prioritaet == Prioritaet.Einsatzrelevant, weiter: p =>
                {
                    p.BeschaffungId = b.Id;
                    p.FahrzeugId = b.FahrzeugId;
                    p.MangelId = b.MangelId;
                    p.Prioritaet = b.Prioritaet;
                    p.Begruendung = b.Begruendung;
                    p.Zuwendung = antrag?.ErwarteterBetrag;
                });
        }

        // 2. Ersatz nach Lebensdauer: Geräte, deren Aussonderung im Planjahr fällig wird, je Material.
        var geraete = await db.Geraete.AsNoTracking().Include(g => g.Geraeteart).Include(g => g.Material)
            .Where(g => g.AusgesondertAm == null && g.Geraeteart!.AussonderungNachJahren != null).ToListAsync(ct);
        foreach (var gruppe in geraete
            .Where(g => g.AussonderungFaellig(g.Geraeteart!.AussonderungNachJahren) is { } f && f.Year == jahr)
            .GroupBy(g => g.MaterialId))
        {
            var erstes = gruppe.First();
            var preis = erstes.Material?.Stueckwert ?? gruppe.Where(g => g.Anschaffungspreis != null).Select(g => g.Anschaffungspreis!.Value).DefaultIfEmpty(0).Average();
            var psa = erstes.Geraeteart!.Code.StartsWith("PSA-", StringComparison.Ordinal);
            var investiv = preis / (1 + grenzen.SteuersatzProzent / 100m) >= grenzen.GrenzeNetto;
            Neu($"ersatz:{gruppe.Key}", investiv ? "FH-7831" : psa ? "EH-4271" : "EH-4221", psa ? Haushaltsbereich.Kleiderkammer : Haushaltsbereich.Technik,
                $"Ersatz {erstes.Material?.Bezeichnung ?? erstes.Bezeichnung} (Lebensdauer endet {jahr})", gruppe.Count(), preis, Preisquelle.Kaufpreis,
                "DGUV G 305-002 / Herstellerangabe zur Lebensdauer", pflicht: true,
                weiter: p => p.Begruendung = $"{gruppe.Count()} Stück erreichen {jahr} das Ende der Lebensdauer ({erstes.Geraeteart.AussonderungNachJahren} Jahre).");
        }

        // 3. Ist des Vorjahres: Treibstoff, Reparaturen an Fahrzeugen und Geräten, Verbrauchsmaterial.
        var treibstoff = await db.Betankungen.AsNoTracking().Where(b => b.Datum >= vorjahrAb && b.Datum <= vorjahrBis).SumAsync(b => b.Betrag ?? 0, ct);
        Neu("ist:treibstoff", "EH-4251", Haushaltsbereich.Technik, $"Treibstoff (Ist {jahr - 2})", 1, treibstoff, Preisquelle.Vorjahr, "§ 3 Abs. 1 FwG", pflicht: true);
        var abUtc = new DateTimeOffset(vorjahrAb.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var bisUtc = abUtc.AddYears(1);
        var reparaturen = await db.Maengel.AsNoTracking().Where(m => m.BehobenAm >= abUtc && m.BehobenAm < bisUtc && m.Kosten != null && !m.Garantiefall)
            .GroupBy(m => m.FahrzeugId != null && m.GeraetId == null).Select(g => new { Fahrzeug = g.Key, Summe = g.Sum(m => m.Kosten!.Value) }).ToListAsync(ct);
        Neu("ist:reparatur-fahrzeuge", "EH-4221", Haushaltsbereich.Technik, $"Reparaturen an Fahrzeugen (Ist {jahr - 2})", 1,
            reparaturen.Where(r => r.Fahrzeug).Sum(r => r.Summe), Preisquelle.Vorjahr, "§ 3 Abs. 1 FwG", pflicht: true);
        Neu("ist:reparatur-geraete", "EH-4221", Haushaltsbereich.Technik, $"Reparaturen an Geräten (Ist {jahr - 2})", 1,
            reparaturen.Where(r => !r.Fahrzeug).Sum(r => r.Summe), Preisquelle.Vorjahr, "§ 3 Abs. 1 FwG", pflicht: true);
        var verbrauch = await db.Lagerbewegungen.AsNoTracking()
            .Where(b => b.Datum >= vorjahrAb && b.Datum <= vorjahrBis && (b.Art == LagerbewegungArt.Entnahme || b.Art == LagerbewegungArt.Verfall) && b.Stueckpreis != null)
            .SumAsync(b => b.Menge * b.Stueckpreis!.Value, ct);
        Neu("ist:verbrauch", "EH-4291", Haushaltsbereich.Technik, $"Verbrauchsmaterial (Verbrauch und Verfall {jahr - 2})", 1, verbrauch, Preisquelle.Vorjahr, "§ 3 Abs. 1 FwG");

        // 4. Kostenansätze: Menge aus dem Vorjahr bzw. dem Bestand mal Satz; Satz 0 bleibt als „Satz fehlt“ sichtbar.
        var positionen = await db.Werkstattpositionen.AsNoTracking().Include(p => p.Werkstattauftrag)
            .Where(p => p.Werkstattauftrag!.ZurueckAm >= vorjahrAb && p.Werkstattauftrag.ZurueckAm <= vorjahrBis)
            .Select(p => new { p.Werkstattauftrag!.Werkstatt, p.Leistungen }).ToListAsync(ct);
        var fremdpruefungen = await db.Pruefungen.AsNoTracking()
            .Where(p => p.Datum >= vorjahrAb && p.Datum <= vorjahrBis && p.PrueferExtern != null && p.StorniertAm == null && p.GeraetId != null)
            .GroupBy(p => p.Geraet!.Geraeteart!.Code).Select(g => new { Code = g.Key, Anzahl = g.Count() }).ToDictionaryAsync(g => g.Code, g => g.Anzahl, ct);
        var fahrzeuge = await db.Fahrzeuge.CountAsync(f => f.AusserDienstAm == null, ct);
        var waeschen = await db.Pflegeeintraege.CountAsync(p => p.Datum >= vorjahrAb && p.Datum <= vorjahrBis && p.Art == Pflegeart.Waesche, ct);
        foreach (var ansatz in await db.Kostenansaetze.AsNoTracking().Where(k => k.Aktiv).OrderBy(k => k.Sortierung).ToListAsync(ct))
        {
            decimal menge = ansatz.Bezug switch
            {
                Kostenbezug.Schlauchwerkstatt => positionen.Count(p => p.Werkstatt == Ruecklauf.Schlauchwerkstatt),
                Kostenbezug.Atemschutzwerkstatt => positionen.Count(p => p.Werkstatt == Ruecklauf.Atemschutzwerkstatt && p.Leistungen.HasFlag(Werkstattleistung.Geprueft)),
                Kostenbezug.Flaschenfuellung => positionen.Count(p => p.Werkstatt == Ruecklauf.Atemschutzwerkstatt && p.Leistungen.HasFlag(Werkstattleistung.Gefuellt)),
                Kostenbezug.Fremdpruefung => ansatz.GeraeteartCode is { } code && fremdpruefungen.TryGetValue(code, out var anzahl) ? anzahl : 0,
                Kostenbezug.JeFahrzeug => fahrzeuge,
                Kostenbezug.Waesche => waeschen,
                _ => 0,
            };
            var bereich = ansatz.HaushaltsstelleCode.EndsWith("-AS", StringComparison.Ordinal) ? Haushaltsbereich.Atemschutz
                : ansatz.Bezug == Kostenbezug.Waesche ? Haushaltsbereich.Kleiderkammer
                : ansatz.HaushaltsstelleCode == "EH-4211" ? Haushaltsbereich.Geraetehaus : Haushaltsbereich.Technik;
            Neu($"ansatz:{ansatz.Code}", ansatz.HaushaltsstelleCode, bereich, ansatz.Satz == 0 ? $"{ansatz.Bezeichnung} (Satz fehlt)" : ansatz.Bezeichnung,
                menge, ansatz.Satz, Preisquelle.Kostenansatz, ansatz.Bezug == Kostenbezug.Fremdpruefung ? "DGUV G 305-002" : "§ 3 Abs. 1 FwG", pflicht: true,
                weiter: p => p.Begruendung = $"Menge: {Anzeige(ansatz.Bezug)}; Satz: {ansatz.Quelle}");
        }

        // 5. Zuschüsse der Gemeinde an die Kameradschaftskasse (Entschädigungssatzung § 5).
        var zuschuss = await parameter.DezimalAsync(ParameterSchluessel.KameradschaftskasseZuschussEuro, stichtag, ct) ?? 0;
        var zuschussJugend = await parameter.DezimalAsync(ParameterSchluessel.KameradschaftskasseZuschussJugendEuro, stichtag, ct) ?? 0;
        Neu("zuschuss:kasse", "EH-4318", Haushaltsbereich.Allgemein, "Zuschuss an die Kameradschaftskasse", 1, zuschuss, Preisquelle.Kostenansatz,
            "Entschädigungssatzung § 5 Abs. 1", pflicht: true);
        Neu("zuschuss:jugend", "EH-4318", Haushaltsbereich.Jugend, "Zuschuss zur Förderung der Jugendfeuerwehr", 1, zuschussJugend, Preisquelle.Kostenansatz,
            "Entschädigungssatzung § 5 Abs. 2", pflicht: true);

        return neu;
    }

    /// <summary>Kurztext der Bezugsgröße für die Begründung.</summary>
    private static string Anzeige(Kostenbezug bezug) => bezug switch
    {
        Kostenbezug.Schlauchwerkstatt => "Schläuche in der Schlauchwerkstatt im Vorjahr",
        Kostenbezug.Atemschutzwerkstatt => "geprüfte Geräte aus der Atemschutzwerkstatt im Vorjahr",
        Kostenbezug.Flaschenfuellung => "gefüllte Flaschen im Vorjahr",
        Kostenbezug.Fremdpruefung => "Fremdprüfungen im Vorjahr",
        Kostenbezug.JeFahrzeug => "Fahrzeuge im Dienst",
        Kostenbezug.Waesche => "Wäschen im Vorjahr",
        _ => bezug.ToString(),
    };
}
