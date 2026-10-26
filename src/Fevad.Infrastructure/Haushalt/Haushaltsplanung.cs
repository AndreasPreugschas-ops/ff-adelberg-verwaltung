using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Haushalt;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Haushalt;

/// <summary>Wertgrenze und Steuersatz, nach denen Positionen Investition oder Aufwand sind.</summary>
/// <param name="GrenzeNetto">Investition ab diesem Einzelpreis netto.</param>
/// <param name="SteuersatzProzent">Umsatzsteuer-Regelsatz.</param>
public sealed record Haushaltsgrenzen(decimal GrenzeNetto, decimal SteuersatzProzent);

/// <summary>Hinweis der Haushaltsüberwachung.</summary>
/// <param name="Warnung">Handlungsbedarf (rot) statt Information.</param>
/// <param name="Text">Hinweistext.</param>
public sealed record Haushaltshinweis(bool Warnung, string Text);

/// <summary>Unterjährige Überwachung eines Haushaltsjahres.</summary>
/// <param name="Jahr">Haushaltsjahr.</param>
/// <param name="Anmeldung">Anmeldung des Jahres oder null.</param>
/// <param name="Staende">Stand je Haushaltsstelle.</param>
/// <param name="NichtZugeordnet">Ist-Zeilen ohne passende Haushaltsstelle.</param>
/// <param name="IstStand">Buchungsstand des letzten Imports oder null.</param>
/// <param name="Hinweise">Hinweise und Warnungen.</param>
public sealed record Haushaltsueberblick(int Jahr, Haushaltsanmeldung? Anmeldung, IReadOnlyList<Haushaltsstand> Staende, IReadOnlyList<HaushaltIst> NichtZugeordnet,
    DateOnly? IstStand, IReadOnlyList<Haushaltshinweis> Hinweise);

/// <summary>
/// Haushaltsanmeldung (V29): Vorschlagszeilen aus den Daten von FEVAD. Der Gerätewart bestätigt oder streicht sie. Vorschläge kommen
/// aus offenen Beschaffungen (mit Z-Feu-Zuwendung), Ersatz nach Lebensdauer, Ist des Vorjahres (Treibstoff, Reparaturen,
/// Verbrauchsmaterial), Kostenansätzen (Werkstatt Göppingen, Fremdprüfungen, Versicherung, Wäsche), den Zuschüssen an die Kassen und dem
/// Nachbedarf der Kleiderkammer mit den Preisen der Lieferanten.
/// </summary>
public sealed class Haushaltsplanung(AppDbContext db, ParameterLeser parameter, Kleiderkammerstand kleiderkammer, TimeProvider zeit)
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

        var preise = await kleiderkammer.PreiseAsync(ct);

        // 1. Offene Beschaffungen dieses Haushaltsjahres, mit Z-Feu-Zuwendung aus dem Antrag.
        var beschaffungen = await db.Beschaffungen.AsNoTracking()
            .Where(b => b.Haushaltsjahr == jahr && (b.Status == BeschaffungStatus.Bedarf || b.Status == BeschaffungStatus.Angemeldet || b.Status == BeschaffungStatus.Genehmigt))
            .ToListAsync(ct);
        var antraege = await db.Foerderantraege.AsNoTracking().Include(a => a.Foerdergegenstand)
            .Where(a => a.Status != Foerderstatus.Abgelehnt).ToDictionaryAsync(a => a.BeschaffungId, ct);
        foreach (var b in beschaffungen)
        {
            var menge = Math.Max(1, b.Menge);
            // Nachbestellungen der Kleiderkammer rechnen mit dem Lieferantenpreis des Artikels, solange niemand anders geschätzt hat.
            var lieferantenpreis = b.ArtikelId is { } artikelId && preise.GetValueOrDefault(artikelId) is { } lp
                && (b.Kostenschaetzung is null || b.Kostenschaetzung == lp.Preis * b.Menge) ? lp : null;
            var einzel = lieferantenpreis?.Preis ?? (b.Kostenschaetzung ?? 0) / menge;
            antraege.TryGetValue(b.Id, out var antrag);
            var investiv = einzel / (1 + grenzen.SteuersatzProzent / 100m) >= grenzen.GrenzeNetto;
            var stelle = investiv ? (antrag is not null || (b.FahrzeugId is not null && b.GeraeteartId is null && b.ArtikelId is null) ? "FH-7832" : "FH-7831")
                : b.ArtikelId is not null ? "EH-4271" : "EH-4221";
            Neu($"beschaffung:{b.Id}", stelle, b.ArtikelId is not null ? Haushaltsbereich.Kleiderkammer : Haushaltsbereich.Technik, b.Bezeichnung, menge, einzel,
                lieferantenpreis is null ? Preisquelle.Schaetzung : Preisquelle.Lieferant, antrag is not null ? "§ 3 Abs. 1 FwG; Feuerwehrbedarfsplan; VwV Z-Feu" : "§ 3 Abs. 1 FwG",
                pflicht: b.Prioritaet == Prioritaet.Einsatzrelevant, weiter: p =>
                {
                    p.BeschaffungId = b.Id;
                    p.FahrzeugId = b.FahrzeugId;
                    p.MangelId = b.MangelId;
                    p.Prioritaet = b.Prioritaet;
                    p.Begruendung = b.Begruendung;
                    p.Zuwendung = antrag?.ErwarteterBetrag;
                    if (lieferantenpreis is not null)
                    {
                        p.Begruendung = string.Join("; ", new[] { b.Begruendung, Preisherkunft(lieferantenpreis) }.Where(t => !string.IsNullOrWhiteSpace(t)));
                    }
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

        // 6. Nachbedarf der Kleiderkammer, der noch in keiner Beschaffung steht, je Artikel mit dem Lieferantenpreis. Bei Einzelstücken
        // zählt nur Mindestbestand und fehlende Grundausstattung; den Ersatz nach Lebensdauer meldet schon Abschnitt 2 an.
        var bedarf = await kleiderkammer.BedarfAsync(Kleiderkammerbuchungen.Heute(zeit.GetUtcNow()), ct);
        foreach (var artikel in bedarf.GroupBy(z => new { z.ArtikelId, z.Artikel, Einzelstueck = z.GeraeteartId is not null }))
        {
            var menge = artikel.Sum(z => artikel.Key.Einzelstueck
                ? new Nachbestellbedarf(z.Bedarf.Mindestmenge, z.Bedarf.ImLager, z.Bedarf.FehlendeGrundausstattung, 0, 0, z.Bedarf.BereitsBeschafft).Offen
                : z.Bedarf.Offen);
            var preis = preise.GetValueOrDefault(artikel.Key.ArtikelId);
            var investiv = preis is not null && preis.Preis / (1 + grenzen.SteuersatzProzent / 100m) >= grenzen.GrenzeNetto;
            Neu($"kleiderkammer:{artikel.Key.ArtikelId}", investiv ? "FH-7831" : "EH-4271", Haushaltsbereich.Kleiderkammer,
                preis is null ? $"Nachbedarf {artikel.Key.Artikel} (Preis fehlt)" : $"Nachbedarf {artikel.Key.Artikel}", menge, preis?.Preis ?? 0,
                preis is null ? Preisquelle.Schaetzung : Preisquelle.Lieferant, "§ 3 Abs. 1 FwG; DGUV Vorschrift 49 § 12 (persönliche Schutzausrüstung)",
                pflicht: artikel.Any(z => z.Bedarf.FehlendeGrundausstattung > 0), weiter: p =>
                {
                    var gruende = artikel.Select(z => (z.Groesse.Length == 0 ? "" : $"Größe {z.Groesse}: ") + string.Join(", ", z.Bedarf.Gruende()));
                    p.Begruendung = string.Join("; ", gruende.Append(preis is null ? "Preis beim Artikel erfassen" : Preisherkunft(preis)));
                });
        }

        return neu;
    }

    /// <summary>Herkunft eines Lieferantenpreises für die Begründung.</summary>
    /// <param name="preis">Preis mit geladenem Lieferanten.</param>
    /// <returns>Text wie „Preis: Lieferant, Stand 01.09.2026“.</returns>
    private static string Preisherkunft(ArtikelPreis preis) =>
        $"Preis: {preis.Lieferant?.Name}" + (preis.StandAm is { } stand ? $", Stand {stand:dd.MM.yyyy}" : "");

    /// <summary>Zeilen für den Export der angemeldeten Positionen an die Kämmerei.</summary>
    /// <param name="anmeldung">Anmeldung mit Positionen und Haushaltsstellen.</param>
    /// <returns>Zeilen als Feld-Wert-Paare.</returns>
    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> ExportZeilen(Haushaltsanmeldung anmeldung) =>
        anmeldung.Angemeldet.OrderBy(p => p.Haushaltsstelle?.Sortierung).ThenByDescending(p => p.Prioritaet).ThenBy(p => p.Bezeichnung)
            .Select(p => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["Haushaltsjahr"] = anmeldung.Jahr, ["Produkt"] = p.Haushaltsstelle?.Produkt, ["Kostenstelle"] = p.Haushaltsstelle?.Kostenstelle,
                ["Innenauftrag"] = p.Haushaltsstelle?.Innenauftrag, ["Sachkonto"] = p.Haushaltsstelle?.Sachkonto,
                ["Teil"] = p.Haushaltsstelle?.Teil == Haushaltsteil.Finanzhaushalt ? "FH" : "EH", ["Bereich"] = Text(p.Bereich), ["Bezeichnung"] = p.Bezeichnung,
                ["Menge"] = p.Menge, ["Einzelpreis"] = p.Einzelpreis, ["Betrag"] = p.Betrag, ["BetragPlanjahr"] = p.BetragPlanjahr,
                ["Folgejahr1"] = p.RateFolgejahr1, ["Folgejahr2"] = p.RateFolgejahr2, ["Folgejahr3"] = p.RateFolgejahr3,
                ["Verpflichtungsermaechtigung"] = p.Verpflichtungsermaechtigung, ["Zuwendung"] = p.Zuwendung, ["Folgekosten"] = p.Folgekosten,
                ["Prioritaet"] = Text(p.Prioritaet), ["Pflicht"] = p.Pflicht ? "ja" : "nein", ["Begruendung"] = p.Begruendung,
                ["Rechtsgrundlage"] = p.Rechtsgrundlage, ["Bedarfsplan"] = p.Bedarfsplan, ["Ergebnis"] = p.Ergebnis is { } e ? Text(e) : null,
                ["BeschlossenerBetrag"] = p.BeschlossenerBetrag,
            }).ToList();

    /// <summary>
    /// Unterjährige Überwachung: Ansatz je Haushaltsstelle aus der Anmeldung (nach dem Beschluss laut Rückmeldung), Bindung aus bestellten
    /// Beschaffungen und Obligo, Ist aus dem SAP-Import. Dazu Hinweise auf vorläufige Haushaltsführung (§ 83 GemO), über- und außerplanmäßige
    /// Ausgaben (§ 84 GemO) und die Übertragung von Ermächtigungen (§ 21 GemHVO).
    /// </summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Überblick.</returns>
    public async Task<Haushaltsueberblick> UeberwachungAsync(int jahr, DateOnly heute, CancellationToken ct)
    {
        var anmeldung = await db.Haushaltsanmeldungen.AsNoTracking().Include(a => a.Positionen).ThenInclude(p => p.Haushaltsstelle)
            .FirstOrDefaultAsync(a => a.Jahr == jahr, ct);
        var stellen = await db.Haushaltsstellen.AsNoTracking().ToListAsync(ct);
        var ist = await db.HaushaltIst.AsNoTracking().Where(i => i.Jahr == jahr).ToListAsync(ct);

        // Gebunden: verknüpfte Beschaffungen, die bestellt, aber noch nicht geliefert sind.
        var verknuepft = anmeldung?.Angemeldet.Where(p => p.BeschaffungId != null).ToList() ?? [];
        var ids = verknuepft.Select(p => p.BeschaffungId!.Value).Distinct().ToList();
        var bestellt = await db.Beschaffungen.AsNoTracking().Where(b => ids.Contains(b.Id) && b.Status == BeschaffungStatus.Bestellt)
            .ToDictionaryAsync(b => b.Id, ct);
        var gebunden = verknuepft.Where(p => bestellt.ContainsKey(p.BeschaffungId!.Value))
            .GroupBy(p => p.HaushaltsstelleId)
            .ToDictionary(g => g.Key, g => g.Sum(p => bestellt[p.BeschaffungId!.Value].Kostenschaetzung ?? p.BetragPlanjahr));

        var staende = Haushaltsueberwachung.Berechnen(stellen, anmeldung, gebunden, ist);
        var nichtZugeordnet = Haushaltsueberwachung.NichtZugeordnet(stellen, ist);
        var warnungAb = await parameter.ZahlAsync(ParameterSchluessel.HaushaltWarnungAbProzent, heute, 90, ct);
        var uebertragungAb = Math.Clamp(await parameter.ZahlAsync(ParameterSchluessel.HaushaltUebertragungHinweisAbMonat, heute, 10, ct), 1, 12);
        var hinweise = new List<Haushaltshinweis>();

        if (heute.Year == jahr && anmeldung?.HaushaltBeschlossenAm is null)
        {
            hinweise.Add(new(true, "Der Haushalt ist noch nicht beschlossen: vorläufige Haushaltsführung nach § 83 GemO. Nur Ausgaben, zu denen die Gemeinde "
                + "rechtlich verpflichtet ist oder die für die Weiterführung notwendiger Aufgaben unaufschiebbar sind; neue Investitionen erst nach dem Beschluss."));
        }

        if (anmeldung is null)
        {
            hinweise.Add(new(false, $"Für {jahr} gibt es keine Haushaltsanmeldung; Ansätze fehlen."));
        }
        else if (anmeldung.Status != AnmeldungStatus.Beschlossen)
        {
            hinweise.Add(new(false, "Ansätze sind die angemeldeten Beträge; nach dem Haushaltsbeschluss die Rückmeldungen der Gemeinde eintragen."));
        }

        foreach (var s in staende.Where(s => s.Ueberschritten))
        {
            hinweise.Add(new(true, $"{s.Stelle.Bezeichnung}: Ansatz um {-s.Frei:N2} € überschritten. Über- oder außerplanmäßige "
                + (s.Stelle.Teil == Haushaltsteil.Finanzhaushalt ? "Auszahlung" : "Aufwendung")
                + " nach § 84 GemO: vor der Bestellung über die Kämmerei genehmigen lassen (Deckung angeben)."));
        }

        foreach (var s in staende.Where(s => !s.Ueberschritten && !s.Stelle.Einzahlung && s.Auslastung >= warnungAb))
        {
            hinweise.Add(new(false, $"{s.Stelle.Bezeichnung}: {s.Auslastung} % des Ansatzes ausgeschöpft oder gebunden, frei {s.Frei:N2} €."));
        }

        if (heute.Year == jahr && heute.Month >= uebertragungAb)
        {
            var offen = verknuepft.Where(p => p.Haushaltsstelle?.Teil == Haushaltsteil.Finanzhaushalt && Haushaltsueberwachung.Ansatz(p) > 0).Select(p => p.BeschaffungId!.Value).ToList();
            var nichtGeliefert = await db.Beschaffungen.AsNoTracking()
                .Where(b => offen.Contains(b.Id) && b.Status != BeschaffungStatus.Geliefert && b.Status != BeschaffungStatus.Abgelehnt)
                .Select(b => b.Bezeichnung).ToListAsync(ct);
            if (nichtGeliefert.Count > 0)
            {
                hinweise.Add(new(true, $"Noch nicht geliefert: {string.Join(", ", nichtGeliefert)}. Nicht verbrauchte Investitionsermächtigungen "
                    + "als Haushaltsrest übertragen (§ 21 GemHVO); bei der Kämmerei vor dem Jahresabschluss anmelden."));
            }

            if (staende.Any(s => s.Stelle.Teil == Haushaltsteil.Ergebnishaushalt && !s.Stelle.Einzahlung && s.Frei > 0 && s.Gebunden > 0))
            {
                hinweise.Add(new(false, "Bestellte, noch nicht bezahlte Aufwendungen: Übertragbarkeit ins Folgejahr mit der Kämmerei klären (§ 21 Abs. 2 GemHVO, Haushaltsvermerk)."));
            }
        }

        if (nichtZugeordnet.Count > 0)
        {
            hinweise.Add(new(false, $"{nichtZugeordnet.Count} Ist-Zeilen passen zu keiner Haushaltsstelle (Sachkonto, Kostenstelle oder Innenauftrag prüfen)."));
        }

        return new Haushaltsueberblick(jahr, anmeldung, staende, nichtZugeordnet, ist.Count == 0 ? null : ist.Max(i => i.Stand), hinweise);
    }

    /// <summary>Ergebnis eines Ist-Imports.</summary>
    /// <param name="Zeilen">Übernommene Zeilen.</param>
    /// <param name="Fehler">Meldungen zu Zeilen, die nicht übernommen wurden.</param>
    public sealed record IstErgebnis(int Zeilen, IReadOnlyList<string> Fehler);

    /// <summary>Spielt den Ist-Stand der Kämmerei ein und ersetzt den bisherigen Stand des Jahres; Zeilen anderer Jahre werden gemeldet.</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="inhalt">Dateiinhalt.</param>
    /// <param name="heute">Heutiger Tag als Stand, wenn die Datei keinen liefert.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Ergebnis.</returns>
    public async Task<IstErgebnis> IstEinspielenAsync(int jahr, string inhalt, DateOnly heute, CancellationToken ct)
    {
        var format = await db.Exportformate.AsNoTracking().FirstOrDefaultAsync(f => f.Art == ExportArt.HaushaltIst, ct) ?? Finanzexport.Standard(ExportArt.HaushaltIst);
        var zeilen = Finanzexport.HaushaltIstLesen(format, inhalt, out var lesefehler);
        var fehler = lesefehler.ToList();
        var passend = zeilen.Where(z => z.Jahr is null || z.Jahr == jahr).ToList();
        if (passend.Count < zeilen.Count)
        {
            fehler.Add($"{zeilen.Count - passend.Count} Zeilen gehören zu einem anderen Jahr und wurden übergangen.");
        }

        if (passend.Count == 0)
        {
            fehler.Add("Keine Zeile übernommen; der bisherige Stand bleibt.");
            return new IstErgebnis(0, fehler);
        }

        await db.HaushaltIst.Where(i => i.Jahr == jahr).ExecuteDeleteAsync(ct);
        db.HaushaltIst.AddRange(passend.Select(z => new HaushaltIst
        {
            Jahr = jahr, Sachkonto = Kuerzen(z.Sachkonto, 30)!, Kostenstelle = Kuerzen(z.Kostenstelle, 30), Innenauftrag = Kuerzen(z.Innenauftrag, 60),
            Ist = z.Ist, Obligo = z.Obligo, Stand = z.Stand ?? heute,
        }));
        await db.SaveChangesAsync(ct);
        return new IstErgebnis(passend.Count, fehler);
    }

    private static string? Kuerzen(string? text, int laenge) => text is null || text.Length <= laenge ? text : text[..laenge];

    private static string Text(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
            .OfType<System.ComponentModel.DataAnnotations.DisplayAttribute>().FirstOrDefault()?.Name ?? wert.ToString();

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
