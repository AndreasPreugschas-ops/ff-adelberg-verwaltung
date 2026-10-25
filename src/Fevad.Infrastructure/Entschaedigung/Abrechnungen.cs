using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Entschaedigung;

/// <summary>Berechnet, speichert und schließt Entschädigungsabrechnungen ab.</summary>
public sealed class Abrechnungen(AppDbContext db, TimeProvider zeit)
{
    /// <summary>Parameter, deren Quelle so beginnt, sind Beispielwerte und sperren den Abschluss.</summary>
    public const string PlatzhalterQuelle = "Platzhalter";

    /// <summary>Ergebnis einer Berechnung.</summary>
    /// <param name="Posten">Die berechneten Posten.</param>
    /// <param name="Hinweise">Hinweise, z. B. fehlende Sätze oder offene Einsatzberichte.</param>
    /// <param name="PlatzhalterVerwendet">Mindestens ein Satz ist ein Beispielwert; dann ist kein Abschluss möglich.</param>
    public sealed record Ergebnis(List<AbrechnungsPosten> Posten, List<string> Hinweise, bool PlatzhalterVerwendet);

    /// <summary>Legt eine Abrechnung als Entwurf an und berechnet ihre Posten.</summary>
    /// <param name="bezeichnung">Bezeichnung der Abrechnung.</param>
    /// <param name="von">Erster Tag des Zeitraums.</param>
    /// <param name="bis">Letzter Tag des Zeitraums.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die gespeicherte Abrechnung.</returns>
    /// <exception cref="InvalidOperationException">Der Zeitraum ist ungültig.</exception>
    public async Task<Abrechnung> AnlegenAsync(string bezeichnung, DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        var fehler = Abrechnung.ZeitraumPruefen(von, bis);
        if (fehler.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", fehler));
        }

        var abrechnung = new Abrechnung { Bezeichnung = bezeichnung.Trim(), Von = von, Bis = bis, Status = AbrechnungStatus.Entwurf };
        var ergebnis = await BerechnenAsync(von, bis, ct);
        abrechnung.Posten.AddRange(ergebnis.Posten);
        abrechnung.Hinweise = Hinweistext(ergebnis);
        db.Abrechnungen.Add(abrechnung);
        await db.SaveChangesAsync(ct);
        return abrechnung;
    }

    /// <summary>Verwirft die Posten eines Entwurfs und berechnet sie aus dem aktuellen Datenstand neu.</summary>
    public async Task NeuBerechnenAsync(Guid id, CancellationToken ct = default)
    {
        var abrechnung = await db.Abrechnungen.SingleAsync(a => a.Id == id, ct);
        if (abrechnung.IstAbgeschlossen)
        {
            throw new InvalidOperationException("Eine abgeschlossene Abrechnung kann nicht neu berechnet werden.");
        }

        var ergebnis = await BerechnenAsync(abrechnung.Von, abrechnung.Bis, ct);

        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        // Entwurfsposten sind abgeleitete Daten und werden ersetzt, nicht protokolliert gelöscht.
        await db.AbrechnungsPosten.IgnoreQueryFilters().Where(p => p.AbrechnungId == id).ExecuteDeleteAsync(ct);
        foreach (var alt in db.ChangeTracker.Entries<AbrechnungsPosten>().Where(e => e.Entity.AbrechnungId == id).ToList())
        {
            alt.State = EntityState.Detached;
        }

        abrechnung.Posten.Clear();
        // Ausdrücklich hinzufügen: Posten haben schon eine Id und würden sonst als vorhanden gelten.
        db.AbrechnungsPosten.AddRange(ergebnis.Posten);
        abrechnung.Posten.AddRange(ergebnis.Posten);

        abrechnung.Hinweise = Hinweistext(ergebnis);
        abrechnung.GeaendertAm = zeit.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await transaktion.CommitAsync(ct);
    }

    /// <summary>
    /// Schließt den Entwurf ab. Verweigert, wenn Platzhalter-Sätze verwendet wurden oder ein Posten
    /// inzwischen in einer anderen abgeschlossenen Abrechnung steht.
    /// </summary>
    public async Task AbschliessenAsync(Guid id, Guid benutzerId, CancellationToken ct = default)
    {
        var abrechnung = await db.Abrechnungen.Include(a => a.Posten).SingleAsync(a => a.Id == id, ct);

        var aktuell = await BerechnenAsync(abrechnung.Von, abrechnung.Bis, ct);
        if (aktuell.PlatzhalterVerwendet)
        {
            throw new InvalidOperationException(
                "Die Abrechnung verwendet noch Beispielsätze (Platzhalter). Bitte zuerst die Sätze aus der Entschädigungssatzung eintragen und neu berechnen.");
        }

        if (!Gleich(abrechnung.Posten, aktuell.Posten))
        {
            throw new InvalidOperationException(
                "Seit der letzten Berechnung haben sich Einsätze, Dienste, Funktionen oder Sätze geändert. Bitte neu berechnen und prüfen.");
        }

        abrechnung.Abschliessen(benutzerId, zeit.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Löscht einen Entwurf samt Posten.</summary>
    /// <param name="id">Schlüssel der Abrechnung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <exception cref="InvalidOperationException">Die Abrechnung ist abgeschlossen.</exception>
    public async Task LoeschenAsync(Guid id, CancellationToken ct = default)
    {
        var abrechnung = await db.Abrechnungen.Include(a => a.Posten).SingleAsync(a => a.Id == id, ct);
        if (abrechnung.IstAbgeschlossen)
        {
            throw new InvalidOperationException("Eine abgeschlossene Abrechnung kann nicht gelöscht werden.");
        }

        db.AbrechnungsPosten.RemoveRange(abrechnung.Posten);
        db.Abrechnungen.Remove(abrechnung);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Berechnet alle offenen Posten im Zeitraum, ohne zu speichern.</summary>
    public async Task<Ergebnis> BerechnenAsync(DateOnly von, DateOnly bis, CancellationToken ct = default)
    {
        var parameter = await db.Parameter.AsNoTracking()
            .Where(p => p.Schluessel.StartsWith(Entschaedigungssaetze.Praefix))
            .ToListAsync(ct);
        var saetze = new Entschaedigungssaetze(parameter);

        var posten = new List<AbrechnungsPosten>();
        var hinweise = new List<string>();
        var verwendet = new Verwendet(saetze);

        var utcVon = Ortszeit.TagesbeginnUtc(von);
        var utcBis = Ortszeit.TagesbeginnUtc(bis.AddDays(1));

        var abgerechnet = db.AbrechnungsPosten.Where(p => p.Abrechnung!.Status == AbrechnungStatus.Abgeschlossen);

        await EinsaetzeAsync(saetze, utcVon, utcBis, abgerechnet, posten, hinweise, verwendet, ct);
        await DiensteAsync(saetze, utcVon, utcBis, abgerechnet, posten, hinweise, verwendet, ct);
        await LeitungAsync(saetze, utcVon, utcBis, abgerechnet, posten, hinweise, verwendet, ct);
        await PauschalenAsync(saetze, von, bis, abgerechnet, posten, verwendet, ct);

        var platzhalterVerwendet = verwendet.Platzhalter;
        if (platzhalterVerwendet)
        {
            hinweise.Insert(0, "Es werden Beispielsätze (Platzhalter) verwendet. Abschließen ist erst mit den Sätzen der Entschädigungssatzung möglich.");
        }

        return new Ergebnis(
            posten.OrderBy(p => p.Datum).ThenBy(p => p.Art).ThenBy(p => p.Bezeichnung).ToList(),
            hinweise,
            platzhalterVerwendet);
    }

    private async Task EinsaetzeAsync(
        Entschaedigungssaetze saetze, DateTimeOffset utcVon, DateTimeOffset utcBis, IQueryable<AbrechnungsPosten> abgerechnet,
        List<AbrechnungsPosten> posten, List<string> hinweise, Verwendet verwendet, CancellationToken ct)
    {
        var offeneBerichte = await db.Einsaetze.CountAsync(
            e => e.Alarmiert >= utcVon && e.Alarmiert < utcBis && e.Status != EinsatzStatus.Freigegeben, ct);
        if (offeneBerichte > 0)
        {
            hinweise.Add($"{offeneBerichte} Einsatzbericht(e) im Zeitraum sind noch nicht freigegeben und fehlen in der Abrechnung.");
        }

        var teilnahmen = await db.EinsatzTeilnahmen.AsNoTracking()
            .Where(t => t.Einsatz!.Status == EinsatzStatus.Freigegeben
                && t.Einsatz.Alarmiert >= utcVon && t.Einsatz.Alarmiert < utcBis
                && !abgerechnet.Any(p => p.EinsatzTeilnahmeId == t.Id))
            .Select(t => new
            {
                t.Id,
                t.MitgliedId,
                t.Von,
                t.Bis,
                t.Einsatz!.Einsatznummer,
                t.Einsatz.Stichwort,
                t.Einsatz.Alarmiert,
                t.Einsatz.EinsatzbereitschaftHergestellt,
                t.Einsatz.Einsatzende,
            })
            .ToListAsync(ct);

        var ohneSatz = 0;
        foreach (var t in teilnahmen)
        {
            var datum = Ortszeit.Datum(t.Alarmiert);
            // FwES Adelberg § 1 Abs. 2 rechnet bis zum Einsatzende; andere Satzungen bis zur Einsatzbereitschaft.
            var einsatzende = saetze.Zahl(Entschaedigungssaetze.EinsatzBisEinsatzbereitschaft, datum) == 1
                ? t.EinsatzbereitschaftHergestellt ?? t.Einsatzende
                : t.Einsatzende ?? t.EinsatzbereitschaftHergestellt;
            var beginn = t.Von ?? t.Alarmiert;
            var ende = t.Bis ?? einsatzende;
            var regel = saetze.Regel(Entschaedigungssaetze.VorgangEinsatz, datum);
            if (regel is null || !regel.Verguetet)
            {
                ohneSatz++;
                continue;
            }

            if (ende is null || ende <= beginn)
            {
                hinweise.Add($"Einsatz {t.Einsatznummer}: Teilnahme ohne gültige Endzeit, nicht abgerechnet.");
                continue;
            }

            if (TatsaechlicheHoehe(saetze, datum, beginn, ende.Value))
            {
                hinweise.Add($"Einsatz {t.Einsatznummer}: dauert länger als die Grenze für den Durchschnittssatz; Verdienstausfall und Auslagen "
                    + "in tatsächlicher Höhe einzeln abrechnen (§ 16 Abs. 4 FwG).");
                continue;
            }

            verwendet.Regel(Entschaedigungssaetze.VorgangEinsatz, regel, datum);
            var betrag = regel.Berechnen(ende.Value - beginn, Ortszeit.Kalendertage(beginn, ende.Value));
            posten.Add(Posten(PostenArt.Einsatz, Entschaedigungssaetze.VorgangEinsatz, t.MitgliedId, datum,
                $"Einsatz {t.Einsatznummer}{(string.IsNullOrWhiteSpace(t.Stichwort) ? "" : " " + t.Stichwort)}",
                ende.Value - beginn, betrag, einsatzTeilnahmeId: t.Id));
        }

        if (ohneSatz > 0)
        {
            hinweise.Add($"Für Einsätze ist kein Stundensatz hinterlegt; {ohneSatz} Teilnahme(n) nicht abgerechnet.");
        }
    }

    private async Task DiensteAsync(
        Entschaedigungssaetze saetze, DateTimeOffset utcVon, DateTimeOffset utcBis, IQueryable<AbrechnungsPosten> abgerechnet,
        List<AbrechnungsPosten> posten, List<string> hinweise, Verwendet verwendet, CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var geplant = await db.Dienste.CountAsync(
            d => d.Beginn >= utcVon && d.Beginn < utcBis && d.Status == DienstStatus.Geplant && d.Beginn < jetzt, ct);
        if (geplant > 0)
        {
            hinweise.Add($"{geplant} vergangene(r) Dienst(e) im Zeitraum sind noch nicht als durchgeführt markiert.");
        }

        var anwesenheiten = await db.Anwesenheiten.AsNoTracking()
            .Where(a => a.Status == AnwesenheitStatus.Anwesend
                && a.Dienst!.Status == DienstStatus.Durchgefuehrt
                && a.Dienst.Beginn >= utcVon && a.Dienst.Beginn < utcBis
                && !abgerechnet.Any(p => p.AnwesenheitId == a.Id))
            .Select(a => new { a.Id, a.MitgliedId, a.Von, a.Bis, a.Dienst!.Art, a.Dienst.Titel, a.Dienst.Beginn, a.Dienst.Ende })
            .ToListAsync(ct);

        foreach (var a in anwesenheiten)
        {
            var datum = Ortszeit.Datum(a.Beginn);
            var vorgang = Entschaedigungssaetze.Vorgang(a.Art);
            var regel = saetze.Regel(vorgang, datum);
            if (regel is null || !regel.Verguetet)
            {
                // Übungen und Sitzungen sind nach den meisten Satzungen unentgeltlich.
                continue;
            }

            var beginn = a.Von ?? a.Beginn;
            var ende = a.Bis ?? a.Ende;
            if (ende <= beginn)
            {
                continue;
            }

            if (TatsaechlicheHoehe(saetze, datum, beginn, ende))
            {
                hinweise.Add($"{a.Titel} ({Anzeige(datum)}): dauert länger als die Grenze für den Durchschnittssatz; Verdienstausfall und Auslagen "
                    + "in tatsächlicher Höhe einzeln abrechnen (§ 16 Abs. 4 FwG).");
                continue;
            }

            verwendet.Regel(vorgang, regel, datum);
            var betrag = regel.Berechnen(ende - beginn, Ortszeit.Kalendertage(beginn, ende));
            posten.Add(Posten(PostenArt.Dienst, vorgang, a.MitgliedId, datum, a.Titel, ende - beginn, betrag, anwesenheitId: a.Id));
        }
    }

    /// <summary>Leitungspauschale je durchgeführtem Dienst für den Verantwortlichen, z. B. 5 € je Übung (FwES Adelberg § 2 Abs. 3).</summary>
    private async Task LeitungAsync(
        Entschaedigungssaetze saetze, DateTimeOffset utcVon, DateTimeOffset utcBis, IQueryable<AbrechnungsPosten> abgerechnet,
        List<AbrechnungsPosten> posten, List<string> hinweise, Verwendet verwendet, CancellationToken ct)
    {
        var dienste = await db.Dienste.AsNoTracking()
            .Where(d => d.Status == DienstStatus.Durchgefuehrt
                && d.Beginn >= utcVon && d.Beginn < utcBis
                && !abgerechnet.Any(p => p.GeleiteterDienstId == d.Id))
            .Select(d => new { d.Id, d.Art, d.Titel, d.Beginn, d.VerantwortlicherId })
            .ToListAsync(ct);

        var ohneLeitung = 0;
        foreach (var d in dienste)
        {
            var datum = Ortszeit.Datum(d.Beginn);
            var vorgang = Entschaedigungssaetze.Vorgang(d.Art);
            if (saetze.Leitung(vorgang, datum) is not { } betrag || betrag <= 0)
            {
                continue;
            }

            if (d.VerantwortlicherId is not { } leiter)
            {
                ohneLeitung++;
                continue;
            }

            verwendet.Satz(Entschaedigungssaetze.Schluessel(vorgang, "SatzLeitung"), datum, datum);
            posten.Add(new AbrechnungsPosten
            {
                MitgliedId = leiter,
                Art = PostenArt.Leitung,
                Vorgang = vorgang,
                GeleiteterDienstId = d.Id,
                Datum = datum,
                Bezeichnung = Kuerzen($"Leitung {d.Titel}"),
                Menge = 1,
                Einheit = Entschaedigungseinheit.Pauschal,
                Satz = betrag,
                Betrag = betrag,
            });
        }

        if (ohneLeitung > 0)
        {
            hinweise.Add($"{ohneLeitung} durchgeführte(r) Dienst(e) mit Leitungspauschale haben keinen Verantwortlichen eingetragen.");
        }
    }

    private async Task PauschalenAsync(
        Entschaedigungssaetze saetze, DateOnly von, DateOnly bis, IQueryable<AbrechnungsPosten> abgerechnet,
        List<AbrechnungsPosten> posten, Verwendet verwendet, CancellationToken ct)
    {
        var codes = saetze.PauschaleFunktionen.ToList();
        if (codes.Count == 0)
        {
            return;
        }

        var zuordnungen = await db.FunktionZuordnungen.AsNoTracking()
            .Where(z => codes.Contains(z.Funktion!.Code) && z.GueltigAb <= bis && (z.GueltigBis == null || z.GueltigBis >= von))
            .Select(z => new { z.Id, z.MitgliedId, z.GueltigAb, z.GueltigBis, z.Funktion!.Code, z.Funktion.Bezeichnung })
            .ToListAsync(ct);
        var ids = zuordnungen.Select(z => z.Id).ToList();
        var bereits = (await abgerechnet
                .Where(p => p.FunktionZuordnungId != null && ids.Contains(p.FunktionZuordnungId.Value))
                .Select(p => new { p.FunktionZuordnungId, p.Datum, p.DatumBis })
                .ToListAsync(ct))
            .ToLookup(p => p.FunktionZuordnungId!.Value);

        foreach (var z in zuordnungen)
        {
            Func<DateOnly, bool> schonAbgerechnet = tag => bereits[z.Id].Any(p => p.Datum <= tag && tag <= (p.DatumBis ?? p.Datum));
            Func<DateOnly, decimal?> jahressatz = tag => saetze.Pauschale(z.Code, tag);
            var zwoelftel = saetze.Zahl(Entschaedigungssaetze.PauschaleZwoelftel, bis) == 1;
            var anteile = zwoelftel
                ? Funktionspauschale.BerechnenZwoelftel(von, bis, z.GueltigAb, z.GueltigBis, schonAbgerechnet, jahressatz)
                : Funktionspauschale.Berechnen(von, bis, z.GueltigAb, z.GueltigBis, schonAbgerechnet, jahressatz);
            foreach (var anteil in anteile)
            {
                verwendet.Satz(Entschaedigungssaetze.PauschalePraefix + z.Code, anteil.Von, anteil.Bis);
                posten.Add(new AbrechnungsPosten
                {
                    MitgliedId = z.MitgliedId,
                    Art = PostenArt.Funktionspauschale,
                    Vorgang = z.Code,
                    FunktionZuordnungId = z.Id,
                    Datum = anteil.Von,
                    DatumBis = anteil.Bis,
                    Bezeichnung = $"Pauschale {z.Bezeichnung}",
                    Menge = anteil.Tage,
                    Einheit = zwoelftel ? Entschaedigungseinheit.Monat : Entschaedigungseinheit.Jahresanteil,
                    Satz = anteil.Jahressatz,
                    Betrag = anteil.Betrag,
                });
            }
        }
    }

    /// <summary>Merkt sich, ob ein verwendeter Satz am jeweiligen Tag noch ein Beispielwert ist.</summary>
    private sealed class Verwendet(Entschaedigungssaetze saetze)
    {
        public bool Platzhalter { get; private set; }

        public void Regel(string vorgang, Entschaedigungsregel regel, DateOnly datum)
        {
            Satz(Entschaedigungssaetze.Schluessel(vorgang, regel.NachTagen ? "SatzTag" : regel.NachStaffel ? "StaffelErsterBetrag" : "SatzStunde"), datum, datum);
            Satz(Entschaedigungssaetze.Schluessel(vorgang, regel.NachTagen ? "SatzHalberTag" : "RundungMinuten"), datum, datum);
        }

        public void Satz(string schluessel, DateOnly von, DateOnly bis)
        {
            for (var tag = von; tag <= bis && !Platzhalter; tag = tag.AddDays(1))
            {
                Platzhalter = saetze.Gueltig(schluessel, tag)?.Quelle?.StartsWith(PlatzhalterQuelle, StringComparison.Ordinal) == true;
            }
        }
    }

    private static AbrechnungsPosten Posten(
        PostenArt art, string vorgang, Guid mitgliedId, DateOnly datum, string bezeichnung, TimeSpan dauer, Entschaedigungsbetrag betrag,
        Guid? einsatzTeilnahmeId = null, Guid? anwesenheitId = null) => new()
    {
        MitgliedId = mitgliedId,
        Art = art,
        Vorgang = vorgang,
        EinsatzTeilnahmeId = einsatzTeilnahmeId,
        AnwesenheitId = anwesenheitId,
        Datum = datum,
        Bezeichnung = Kuerzen(bezeichnung),
        Minuten = (int)Math.Round(dauer.TotalMinutes),
        Menge = betrag.Menge,
        Einheit = betrag.Einheit,
        Satz = betrag.Satz,
        Betrag = betrag.Betrag,
    };

    private static string Kuerzen(string text) => text.Length > 200 ? text[..200] : text;

    private static string Anzeige(DateOnly datum) => datum.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Die Dauer überschreitet die Grenze für den Durchschnittssatz; dann wird in tatsächlicher Höhe einzeln abgerechnet.</summary>
    private static bool TatsaechlicheHoehe(Entschaedigungssaetze saetze, DateOnly datum, DateTimeOffset beginn, DateTimeOffset ende) =>
        saetze.Zahl(Entschaedigungssaetze.TatsaechlicheHoeheUeberTage, datum) is > 0 and var grenze
        && Ortszeit.Kalendertage(beginn, ende) > grenze;

    private static string? Hinweistext(Ergebnis ergebnis) =>
        ergebnis.Hinweise.Count == 0 ? null : string.Join('\n', ergebnis.Hinweise);

    /// <summary>Vergleicht gespeicherte und neu berechnete Posten fachlich (Quelle, Menge, Betrag).</summary>
    private static bool Gleich(IEnumerable<AbrechnungsPosten> gespeichert, IEnumerable<AbrechnungsPosten> neu)
    {
        static string Zahl(decimal wert) => wert.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        static string Schluessel(AbrechnungsPosten p) =>
            $"{p.MitgliedId}|{p.EinsatzTeilnahmeId}|{p.AnwesenheitId}|{p.FunktionZuordnungId}|{p.GeleiteterDienstId}|{p.Datum}|{p.DatumBis}|{Zahl(p.Menge)}|{Zahl(p.Satz)}|{Zahl(p.Betrag)}";

        return gespeichert.Select(Schluessel).Order().SequenceEqual(neu.Select(Schluessel).Order());
    }
}
