using Fevad.Domain.Atemschutz;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Import;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Import;

/// <summary>Gemeinsame Hilfen der Importe: Werte einer Zeile lesen und Fehler sammeln.</summary>
/// <param name="Werte">Werte der Zeile je Zielfeld.</param>
internal sealed record Importzeile(IReadOnlyDictionary<string, string?> Werte)
{
    /// <summary>Fehler dieser Zeile.</summary>
    public List<string> Fehler { get; } = [];

    /// <summary>Der Text des Felds oder null.</summary>
    public string? Text(string feld) => Werte.GetValueOrDefault(feld);

    /// <summary>Ein Datum; ein unlesbarer Wert wird als Fehler gemerkt.</summary>
    public DateOnly? Datum(string feld, string name)
    {
        if (Text(feld) is not { } text)
        {
            return null;
        }

        if (Altwert.Datum(text, out var d))
        {
            return d;
        }

        Fehler.Add($"{name} „{text}“ ist kein Datum.");
        return null;
    }

    /// <summary>Eine ganze Zahl; ein unlesbarer Wert wird als Fehler gemerkt.</summary>
    public int? Zahl(string feld, string name)
    {
        if (Text(feld) is not { } text)
        {
            return null;
        }

        if (int.TryParse(text, out var z))
        {
            return z;
        }

        Fehler.Add($"{name} „{text}“ ist keine ganze Zahl.");
        return null;
    }

    /// <summary>Ein Betrag; ein unlesbarer Wert wird als Fehler gemerkt.</summary>
    public decimal? Betrag(string feld, string name)
    {
        if (Text(feld) is not { } text)
        {
            return null;
        }

        if (Altwert.Betrag(text, out var b))
        {
            return b;
        }

        Fehler.Add($"{name} „{text}“ ist kein Betrag.");
        return null;
    }

    /// <summary>Ein Zeitpunkt aus Datum und optionaler Uhrzeit in Ortszeit; ein unlesbarer Wert wird als Fehler gemerkt.</summary>
    public DateTimeOffset? Zeitpunkt(string feld, string uhrzeitFeld, string name)
    {
        if (Text(feld) is not { } text)
        {
            return null;
        }

        if (Altwert.Zeitpunkt(text, Text(uhrzeitFeld), out var z))
        {
            return z;
        }

        Fehler.Add($"{name} „{(Text(uhrzeitFeld) is { } u ? $"{text} {u}" : text)}“ ist kein Zeitpunkt.");
        return null;
    }
}

/// <summary>Personen nach Personalnummer oder MP-Feuer-Kennung.</summary>
internal sealed class Personensuche
{
    private readonly Dictionary<string, Guid> _nachNummer;
    private readonly Dictionary<string, Guid> _nachMp;

    private Personensuche(Dictionary<string, Guid> nachNummer, Dictionary<string, Guid> nachMp) => (_nachNummer, _nachMp) = (nachNummer, nachMp);

    /// <summary>Lädt alle Personen.</summary>
    public static async Task<Personensuche> LadenAsync(AppDbContext db, CancellationToken ct)
    {
        var mitglieder = await db.Mitglieder.Select(m => new { m.Id, m.Personalnummer, m.LegacyMpFeuerId }).ToListAsync(ct);
        return new(mitglieder.ToDictionary(m => m.Personalnummer, m => m.Id, StringComparer.OrdinalIgnoreCase),
            mitglieder.Where(m => m.LegacyMpFeuerId is not null).GroupBy(m => m.LegacyMpFeuerId!).ToDictionary(g => g.Key, g => g.First().Id));
    }

    /// <summary>Die Person zur Personalnummer der Zeile; fehlt sie, wird ein Fehler gemerkt.</summary>
    public Guid? Finden(Importzeile zeile)
    {
        var pn = zeile.Text("Personalnummer");
        Guid? id = pn is null ? null : _nachNummer.TryGetValue(pn, out var a) ? a : _nachMp.TryGetValue(pn, out var b) ? b : null;
        if (id is null)
        {
            zeile.Fehler.Add(pn is null ? "Personalnummer fehlt." : $"Personalnummer „{pn}“ gibt es in FEVAD nicht.");
        }

        return id;
    }
}

/// <summary>
/// Geräte anlegen oder ergänzen, erkannt an der Inventarnummer. Fehlt der Materialstamm, wird er aus Bezeichnung, Hersteller
/// und Typ angelegt. Eine letzte Prüfung wird als bestandene Prüfung nach der ersten Position des Prüfplans eingetragen.
/// </summary>
internal sealed class Geraeteimport(AppDbContext db)
{
    /// <summary>Verarbeitet alle Zeilen.</summary>
    public async Task<Importergebnis> AusfuehrenAsync(IReadOnlyList<(int Nummer, IReadOnlyDictionary<string, string?> Werte)> zeilen, CancellationToken ct)
    {
        var geraete = await db.Geraete.Include(g => g.Pruefungen).ToDictionaryAsync(g => g.Inventarnummer, StringComparer.OrdinalIgnoreCase, ct);
        var arten = await db.Geraetearten.Include(a => a.Pruefplan).ToListAsync(ct);
        var artSuche = new Katalogsuche<Geraeteart>(arten);
        var materialien = await db.Materialien.ToListAsync(ct);
        var fahrzeuge = await db.Fahrzeuge.ToListAsync(ct);
        var gruppen = await db.Inventurgruppen.ToListAsync(ct);
        var z = new Zaehler();

        foreach (var (nummer, werte) in zeilen)
        {
            z.Zeilen++;
            var zeile = new Importzeile(werte);
            var inventarnummer = zeile.Text("Inventarnummer");
            if (inventarnummer is null)
            {
                z.Fehler(nummer, "Inventarnummer fehlt.");
                continue;
            }

            var geraet = geraete.GetValueOrDefault(inventarnummer);
            var art = zeile.Text("Geraeteart") is { } artText ? artSuche.Finden(artText) : null;
            if (zeile.Text("Geraeteart") is { } at && art is null)
            {
                zeile.Fehler.Add($"Geräteart „{at}“ gibt es in FEVAD nicht.");
            }

            if (geraet is null && (zeile.Text("Bezeichnung") is null || art is null))
            {
                zeile.Fehler.Add("Neues Gerät braucht Bezeichnung und Geräteart.");
            }

            var baujahr = zeile.Zahl("Baujahr", "Baujahr");
            var herstellung = zeile.Datum("Herstelldatum", "Herstelldatum");
            var beschafft = zeile.Datum("BeschafftAm", "Beschafft am");
            var preis = zeile.Betrag("Anschaffungspreis", "Anschaffungspreis");
            var indienst = zeile.Datum("IndienstAm", "In Dienst seit");
            var letztePruefung = zeile.Datum("LetztePruefung", "Letzte Prüfung");
            var naechstePruefung = zeile.Datum("NaechstePruefung", "Nächste Prüfung");
            var fahrzeug = zeile.Text("Fahrzeug") is { } f
                ? fahrzeuge.FirstOrDefault(x => Altwert.Schluessel(x.Funkrufname) == Altwert.Schluessel(f) || Altwert.Schluessel(x.Kennzeichen) == Altwert.Schluessel(f))
                : null;
            if (zeile.Text("Fahrzeug") is { } ft && fahrzeug is null)
            {
                zeile.Fehler.Add($"Fahrzeug „{ft}“ gibt es in FEVAD nicht.");
            }

            var gruppe = zeile.Text("Inventurgruppe") is { } g ? gruppen.FirstOrDefault(x => Altwert.Schluessel(x.Bezeichnung) == Altwert.Schluessel(g)) : null;
            if (zeile.Text("Inventurgruppe") is { } gt && gruppe is null)
            {
                zeile.Fehler.Add($"Inventurgruppe „{gt}“ gibt es in FEVAD nicht.");
            }

            zeile.Fehler.AddRange(Laengen.Pruefen<Geraet>(db, werte, Importziel.Geraete, ("Inventarnummer", null), ("Bezeichnung", null),
                ("Seriennummer", null), ("Lagerort", null), ("Bemerkung", null)));
            zeile.Fehler.AddRange(Laengen.Pruefen<Material>(db, werte, Importziel.Geraete, ("Material", nameof(Material.Bezeichnung)), ("Hersteller", null), ("Typ", null)));
            if (zeile.Fehler.Count > 0)
            {
                z.Fehler(nummer, string.Join(" ", zeile.Fehler));
                continue;
            }

            var neu = geraet is null;
            art ??= arten.Single(a => a.Id == geraet!.GeraeteartId);
            if (geraet is null)
            {
                var materialName = zeile.Text("Material") ?? zeile.Text("Bezeichnung")!;
                var material = materialien.FirstOrDefault(m => m.GeraeteartId == art.Id && Altwert.Schluessel(m.Bezeichnung) == Altwert.Schluessel(materialName)
                    && (zeile.Text("Typ") is null || Altwert.Schluessel(m.Typ) == Altwert.Schluessel(zeile.Text("Typ"))));
                if (material is null)
                {
                    material = new Material { Bezeichnung = materialName, GeraeteartId = art.Id, Hersteller = zeile.Text("Hersteller"), Typ = zeile.Text("Typ"), Bemerkung = "aus Altdatenimport" };
                    db.Materialien.Add(material);
                    materialien.Add(material);
                }

                geraet = new Geraet { Inventarnummer = inventarnummer, Bezeichnung = zeile.Text("Bezeichnung")!, MaterialId = material.Id, GeraeteartId = art.Id };
                db.Geraete.Add(geraet);
                geraete[inventarnummer] = geraet;
            }

            geraet.Bezeichnung = zeile.Text("Bezeichnung") ?? geraet.Bezeichnung;
            geraet.Seriennummer = zeile.Text("Seriennummer") ?? geraet.Seriennummer;
            geraet.Baujahr = baujahr ?? geraet.Baujahr;
            geraet.Herstelldatum = herstellung ?? geraet.Herstelldatum;
            geraet.BeschafftAm = beschafft ?? geraet.BeschafftAm;
            geraet.Anschaffungspreis = preis ?? geraet.Anschaffungspreis;
            geraet.IndienstAm = indienst ?? geraet.IndienstAm;
            geraet.FahrzeugId = fahrzeug?.Id ?? geraet.FahrzeugId;
            geraet.Lagerort = zeile.Text("Lagerort") ?? geraet.Lagerort;
            geraet.InventurgruppeId = gruppe?.Id ?? geraet.InventurgruppeId;
            geraet.Bemerkung = zeile.Text("Bemerkung") ?? geraet.Bemerkung;

            if (letztePruefung is { } datum && !geraet.Pruefungen.Any(p => p.Datum == datum))
            {
                var position = art.Pruefplan.OrderBy(p => p.IntervallMonate).FirstOrDefault();
                geraet.Pruefungen.Add(new Pruefung
                {
                    Pruefart = position?.Pruefart ?? Pruefart.Sichtpruefung, PruefplanPositionId = position?.Id, Datum = datum, Ergebnis = PruefErgebnis.Bestanden,
                    NaechsteFaellig = naechstePruefung ?? (position is null ? null : datum.AddMonths(position.IntervallMonate)),
                    Abgeschlossen = true, Bemerkung = "aus Altdatenimport",
                });
            }

            if (neu)
            {
                z.Angelegt++;
            }
            else
            {
                z.Aktualisiert++;
            }
        }

        return z.Ergebnis();
    }
}

/// <summary>
/// Atemschutzfristen je Person: Vorsorge nur mit Untersuchungstag und „gültig bis“ (keine Befunde; eine gültige Vorsorge bedeutet
/// „keine Bedenken“), dazu die letzten Belastungs- und Einsatzübungen und Unterweisungen als Nachweise. Die Nachweise brauchen wie
/// alle anderen die Bestätigung durch Kommando und Atemschutzwart.
/// </summary>
internal sealed class Atemschutzimport(AppDbContext db)
{
    private static readonly (string Feld, string Name, AtemschutzNachweisArt Art)[] Nachweise =
    [
        ("Belastungsuebung", "Belastungsübung", AtemschutzNachweisArt.Belastungsuebung),
        ("Einsatzuebung", "Einsatzübung", AtemschutzNachweisArt.Einsatzuebung),
        ("Unterweisung", "Unterweisung", AtemschutzNachweisArt.Unterweisung),
    ];

    /// <summary>Verarbeitet alle Zeilen.</summary>
    public async Task<Importergebnis> AusfuehrenAsync(IReadOnlyList<(int Nummer, IReadOnlyDictionary<string, string?> Werte)> zeilen, CancellationToken ct)
    {
        var personen = await Personensuche.LadenAsync(db, ct);
        var vorsorgen = await db.AtemschutzVorsorgen.Select(v => new { v.MitgliedId, v.UntersuchtAm }).ToListAsync(ct);
        var vorhandeneVorsorgen = vorsorgen.Select(v => (v.MitgliedId, v.UntersuchtAm)).ToHashSet();
        var nachweise = await db.AtemschutzNachweise.Select(n => new { n.MitgliedId, n.Datum, n.Art }).ToListAsync(ct);
        var vorhandeneNachweise = nachweise.Select(n => (n.MitgliedId, n.Datum, n.Art)).ToHashSet();
        var z = new Zaehler();

        foreach (var (nummer, werte) in zeilen)
        {
            z.Zeilen++;
            var zeile = new Importzeile(werte);
            var mitgliedId = personen.Finden(zeile);
            var untersucht = zeile.Datum("VorsorgeAm", "Vorsorge am");
            var gueltigBis = zeile.Datum("VorsorgeGueltigBis", "Vorsorge gültig bis");
            if ((untersucht is null) != (gueltigBis is null) && zeile.Fehler.Count == 0)
            {
                zeile.Fehler.Add("Für die Vorsorge braucht es Untersuchungstag und „gültig bis“.");
            }

            var daten = Nachweise.Select(n => (n.Art, Datum: zeile.Datum(n.Feld, n.Name))).ToList();
            if (zeile.Fehler.Count > 0)
            {
                z.Fehler(nummer, string.Join(" ", zeile.Fehler));
                continue;
            }

            var angelegt = 0;
            if (untersucht is { } am && gueltigBis is { } bis && vorhandeneVorsorgen.Add((mitgliedId!.Value, am)))
            {
                db.AtemschutzVorsorgen.Add(new AtemschutzVorsorge { MitgliedId = mitgliedId.Value, UntersuchtAm = am, NaechsteFaellig = bis, Ergebnis = VorsorgeErgebnis.Geeignet, Bemerkung = "aus Altdatenimport" });
                angelegt++;
            }

            foreach (var (art, datum) in daten)
            {
                if (datum is { } d && vorhandeneNachweise.Add((mitgliedId!.Value, d, art)))
                {
                    db.AtemschutzNachweise.Add(new AtemschutzNachweis { MitgliedId = mitgliedId.Value, Datum = d, Art = art, Taetigkeit = "aus Altdatenimport" });
                    angelegt++;
                }
            }

            z.Angelegt += angelegt;
        }

        return z.Ergebnis();
    }
}

/// <summary>
/// Einsätze anlegen oder ergänzen, erkannt an der Einsatznummer. Importierte Einsätze bleiben „in Bearbeitung“, damit sie nicht
/// nachträglich in Entschädigung und Nachweise laufen; freigegebene Einsätze werden nicht überschrieben.
/// </summary>
internal sealed class Einsatzimport(AppDbContext db)
{
    /// <summary>Verarbeitet alle Zeilen.</summary>
    public async Task<Importergebnis> AusfuehrenAsync(IReadOnlyList<(int Nummer, IReadOnlyDictionary<string, string?> Werte)> zeilen, CancellationToken ct)
    {
        var einsaetze = await db.Einsaetze.ToDictionaryAsync(e => e.Einsatznummer, StringComparer.OrdinalIgnoreCase, ct);
        var kategorien = new Katalogsuche<StatistikKategorie>(await db.StatistikKategorien.ToListAsync(ct));
        var personen = await Personensuche.LadenAsync(db, ct);
        var z = new Zaehler();

        foreach (var (nummer, werte) in zeilen)
        {
            z.Zeilen++;
            var zeile = new Importzeile(werte);
            var einsatznummer = zeile.Text("Einsatznummer");
            if (einsatznummer is null)
            {
                z.Fehler(nummer, "Einsatznummer fehlt.");
                continue;
            }

            var einsatz = einsaetze.GetValueOrDefault(einsatznummer);
            if (einsatz?.Status == EinsatzStatus.Freigegeben)
            {
                z.Fehler(nummer, $"Einsatz „{einsatznummer}“ ist schon freigegeben und bleibt unverändert.");
                continue;
            }

            var alarmiert = zeile.Zeitpunkt("Alarmiert", "AlarmiertUhrzeit", "Alarmiert");
            if (einsatz is null && alarmiert is null && zeile.Fehler.Count == 0)
            {
                zeile.Fehler.Add("Alarmzeit fehlt.");
            }

            DateTimeOffset? ende = null;
            if (zeile.Text("Einsatzende") is not null)
            {
                ende = zeile.Zeitpunkt("Einsatzende", "EinsatzendeUhrzeit", "Einsatzende");
            }
            else if (zeile.Text("EinsatzendeUhrzeit") is { } nurZeit && (alarmiert ?? einsatz?.Alarmiert) is { } beginn)
            {
                // Nur die Uhrzeit: am Alarmtag, nach Mitternacht am Folgetag.
                if (Altwert.Zeitpunkt(Fevad.Domain.Gemeinsam.Ortszeit.Datum(beginn).ToString("dd.MM.yyyy"), nurZeit, out var e))
                {
                    ende = e < beginn ? e.AddDays(1) : e;
                }
                else
                {
                    zeile.Fehler.Add($"Einsatzende „{nurZeit}“ ist keine Uhrzeit.");
                }
            }

            if (ende is { } en && (alarmiert ?? einsatz?.Alarmiert) is { } al && en < al)
            {
                zeile.Fehler.Add("Einsatzende liegt vor der Alarmierung.");
            }

            var kategorie = zeile.Text("Kategorie") is { } k ? kategorien.Finden(k) : null;
            if (zeile.Text("Kategorie") is { } kt && kategorie is null)
            {
                zeile.Fehler.Add($"Kategorie „{kt}“ gibt es in FEVAD nicht.");
            }

            Guid? leiter = null;
            if (zeile.Text("Einsatzleiter") is { } el)
            {
                leiter = personen.Finden(new Importzeile(new Dictionary<string, string?> { ["Personalnummer"] = el }));
                if (leiter is null)
                {
                    zeile.Fehler.Add($"Einsatzleiter „{el}“ gibt es in FEVAD nicht.");
                }
            }

            zeile.Fehler.AddRange(Laengen.Pruefen<Einsatz>(db, werte, Importziel.Einsaetze, ("Einsatznummer", null), ("Stichwort", null), ("Meldung", null),
                ("Strasse", null), ("Hausnummer", null), ("Plz", null), ("Ort", null), ("Objekt", null)));
            if (zeile.Fehler.Count > 0)
            {
                z.Fehler(nummer, string.Join(" ", zeile.Fehler));
                continue;
            }

            var neu = einsatz is null;
            if (einsatz is null)
            {
                einsatz = new Einsatz { Einsatznummer = einsatznummer, Alarmiert = alarmiert!.Value, Status = EinsatzStatus.InBearbeitung };
                db.Einsaetze.Add(einsatz);
                einsaetze[einsatznummer] = einsatz;
            }

            einsatz.Alarmiert = alarmiert ?? einsatz.Alarmiert;
            einsatz.Einsatzende = ende ?? einsatz.Einsatzende;
            einsatz.Stichwort = zeile.Text("Stichwort") ?? einsatz.Stichwort;
            einsatz.Meldung = zeile.Text("Meldung") ?? einsatz.Meldung;
            einsatz.Strasse = zeile.Text("Strasse") ?? einsatz.Strasse;
            einsatz.Hausnummer = zeile.Text("Hausnummer") ?? einsatz.Hausnummer;
            einsatz.Plz = zeile.Text("Plz") ?? einsatz.Plz;
            einsatz.Ort = zeile.Text("Ort") ?? einsatz.Ort;
            einsatz.Objekt = zeile.Text("Objekt") ?? einsatz.Objekt;
            einsatz.StatistikKategorieId = kategorie?.Id ?? einsatz.StatistikKategorieId;
            einsatz.EinsatzleiterId = leiter ?? einsatz.EinsatzleiterId;
            einsatz.Lagebericht = zeile.Text("Lagebericht") ?? einsatz.Lagebericht;
            if (einsatz.Status == EinsatzStatus.Entwurf)
            {
                einsatz.Status = EinsatzStatus.InBearbeitung;
            }

            if (neu)
            {
                z.Angelegt++;
            }
            else
            {
                z.Aktualisiert++;
            }
        }

        return z.Ergebnis();
    }
}

/// <summary>Teilnahme von Personen an vorhandenen Einsätzen; doppelte Teilnahmen werden nur ergänzt.</summary>
internal sealed class Teilnahmeimport(AppDbContext db)
{
    /// <summary>Verarbeitet alle Zeilen.</summary>
    public async Task<Importergebnis> AusfuehrenAsync(IReadOnlyList<(int Nummer, IReadOnlyDictionary<string, string?> Werte)> zeilen, CancellationToken ct)
    {
        var einsaetze = await db.Einsaetze.Select(e => new { e.Id, e.Einsatznummer, e.Status }).ToListAsync(ct);
        var nachNummer = einsaetze.ToDictionary(e => e.Einsatznummer, StringComparer.OrdinalIgnoreCase);
        var personen = await Personensuche.LadenAsync(db, ct);
        var teilnahmen = await db.EinsatzTeilnahmen.ToListAsync(ct);
        var z = new Zaehler();

        foreach (var (nummer, werte) in zeilen)
        {
            z.Zeilen++;
            var zeile = new Importzeile(werte);
            var einsatz = zeile.Text("Einsatznummer") is { } en ? nachNummer.GetValueOrDefault(en) : null;
            if (einsatz is null)
            {
                zeile.Fehler.Add(zeile.Text("Einsatznummer") is { } t ? $"Einsatz „{t}“ gibt es in FEVAD nicht." : "Einsatznummer fehlt.");
            }
            else if (einsatz.Status == EinsatzStatus.Freigegeben)
            {
                zeile.Fehler.Add($"Einsatz „{einsatz.Einsatznummer}“ ist schon freigegeben und bleibt unverändert.");
            }

            var mitgliedId = personen.Finden(zeile);
            zeile.Fehler.AddRange(Laengen.Pruefen<EinsatzTeilnahme>(db, werte, Importziel.Einsatzteilnahmen, ("Funktion", null)));
            if (zeile.Fehler.Count > 0)
            {
                z.Fehler(nummer, string.Join(" ", zeile.Fehler));
                continue;
            }

            var teilnahme = teilnahmen.FirstOrDefault(t => t.EinsatzId == einsatz!.Id && t.MitgliedId == mitgliedId);
            if (teilnahme is null)
            {
                teilnahme = new EinsatzTeilnahme { EinsatzId = einsatz!.Id, MitgliedId = mitgliedId!.Value, Funktion = zeile.Text("Funktion") };
                db.EinsatzTeilnahmen.Add(teilnahme);
                teilnahmen.Add(teilnahme);
                z.Angelegt++;
            }
            else if (zeile.Text("Funktion") is { } funktion && teilnahme.Funktion != funktion)
            {
                teilnahme.Funktion = funktion;
                z.Aktualisiert++;
            }
        }

        return z.Ergebnis();
    }
}
