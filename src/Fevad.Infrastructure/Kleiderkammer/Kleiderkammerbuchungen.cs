using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fevad.Infrastructure.Kleiderkammer;

public sealed record AusgabePosition(Guid ArtikelId, string Groesse, int Menge, Guid? GeraetId = null);

/// <summary>Bei nicht ordnungsgemäßer Rückgabe sind die Mängel anzugeben; ein wieder eingelagertes Einzelstück bekommt sie als Mangel.</summary>
public sealed record RuecknahmePosition(Guid AusstattungId, int Menge, RuecknahmeZustand Zustand, bool Ordnungsgemaess = true, string? Maengel = null);

/// <summary>Fachlicher Fehler bei einer Buchung, z. B. zu wenig Bestand. Die Meldung ist für die Oberfläche gedacht.</summary>
public sealed class KleiderkammerFehler(string meldung) : Exception(meldung);

/// <summary>Bucht Ausgabe, Rücknahme und Zugang. Jede Ausgabe und Rücknahme ergibt einen Vorgang mit Quittungsnummer.</summary>
public sealed class Kleiderkammerbuchungen(AppDbContext db, TimeProvider zeit)
{
    private const int Versuche = 5;
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public Task<Kleiderkammervorgang> AusgebenAsync(Guid mitgliedId, IReadOnlyList<AusgabePosition> positionen, string? bemerkung, CancellationToken ct = default) =>
        AlleOderKeineAsync(() => AusgebenIntern(mitgliedId, positionen, bemerkung, ct));

    public Task<Kleiderkammervorgang> ZuruecknehmenAsync(Guid mitgliedId, IReadOnlyList<RuecknahmePosition> positionen, string? bemerkung, CancellationToken ct = default) =>
        AlleOderKeineAsync(() => ZuruecknehmenIntern(mitgliedId, positionen, bemerkung, ct));

    private async Task<Kleiderkammervorgang> AusgebenIntern(Guid mitgliedId, IReadOnlyList<AusgabePosition> positionen, string? bemerkung, CancellationToken ct)
    {
        if (positionen.Count == 0)
        {
            throw new KleiderkammerFehler("Keine Position zum Ausgeben gewählt.");
        }

        var jetzt = zeit.GetUtcNow();
        var heute = Heute(jetzt);
        var vorgang = await VorgangAsync(mitgliedId, VorgangArt.Ausgabe, jetzt, bemerkung, ct);

        var artikelIds = positionen.Select(p => p.ArtikelId).Distinct().ToList();
        var artikel = await db.Artikel.Where(a => artikelIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

        foreach (var position in positionen)
        {
            if (!artikel.TryGetValue(position.ArtikelId, out var a))
            {
                throw new KleiderkammerFehler("Artikel nicht gefunden.");
            }

            var groesse = Groesse.Normalisieren(position.Groesse);
            if (a.IstEinzelstueck)
            {
                db.Ausstattungen.Add(await EinzelstueckAusgebenAsync(a, position, mitgliedId, vorgang, heute, ct));
                continue;
            }

            if (position.Menge <= 0)
            {
                throw new KleiderkammerFehler($"{a.Bezeichnung}: Menge muss größer als 0 sein.");
            }

            var bestand = await db.Lagerbestaende.FirstOrDefaultAsync(l => l.ArtikelId == a.Id && l.Groesse == groesse, ct);
            if (bestand is null || bestand.Menge < position.Menge)
            {
                throw new KleiderkammerFehler($"{a.Bezeichnung} {Anzeige(groesse)}: nur {bestand?.Menge ?? 0} Stück im Lager.");
            }

            bestand.Menge -= position.Menge;
            db.Ausstattungen.Add(new Ausstattung
            {
                MitgliedId = mitgliedId,
                ArtikelId = a.Id,
                Groesse = groesse,
                Menge = position.Menge,
                AusgegebenAm = heute,
                AusgabeVorgangId = vorgang.Id,
            });
        }

        await MitNummerSpeichernAsync(vorgang, ct);
        return vorgang;
    }

    private async Task<Kleiderkammervorgang> ZuruecknehmenIntern(Guid mitgliedId, IReadOnlyList<RuecknahmePosition> positionen, string? bemerkung, CancellationToken ct)
    {
        if (positionen.Count == 0)
        {
            throw new KleiderkammerFehler("Keine Position zur Rücknahme gewählt.");
        }

        var jetzt = zeit.GetUtcNow();
        var heute = Heute(jetzt);
        var vorgang = await VorgangAsync(mitgliedId, VorgangArt.Ruecknahme, jetzt, bemerkung, ct);

        var ids = positionen.Select(p => p.AusstattungId).ToList();
        var auszusondern = new List<Geraet>();
        var offen = await db.Ausstattungen
            .Include(a => a.Artikel)
            .Include(a => a.Geraet)
            .Where(a => ids.Contains(a.Id) && a.MitgliedId == mitgliedId && a.ZurueckAm == null)
            .ToDictionaryAsync(a => a.Id, ct);

        foreach (var position in positionen)
        {
            if (!offen.TryGetValue(position.AusstattungId, out var zeile))
            {
                throw new KleiderkammerFehler("Die Ausstattung ist nicht (mehr) bei diesem Mitglied.");
            }

            if (position.Menge <= 0 || position.Menge > zeile.Menge)
            {
                throw new KleiderkammerFehler($"{zeile.Artikel!.Bezeichnung}: Rücknahme von 1 bis {zeile.Menge} Stück möglich.");
            }

            var maengel = string.IsNullOrWhiteSpace(position.Maengel) ? null : position.Maengel.Trim();
            if (!position.Ordnungsgemaess && maengel is null)
            {
                throw new KleiderkammerFehler($"{zeile.Artikel!.Bezeichnung}: Bitte die Mängel bei nicht ordnungsgemäßer Rückgabe angeben.");
            }

            if (maengel is { Length: > 1000 })
            {
                throw new KleiderkammerFehler($"{zeile.Artikel!.Bezeichnung}: Die Mängelbeschreibung ist zu lang (höchstens 1000 Zeichen).");
            }

            // Teilrückgabe: Rest bleibt als eigene Zeile beim Mitglied.
            if (position.Menge < zeile.Menge)
            {
                db.Ausstattungen.Add(new Ausstattung
                {
                    MitgliedId = zeile.MitgliedId,
                    ArtikelId = zeile.ArtikelId,
                    Groesse = zeile.Groesse,
                    Menge = zeile.Menge - position.Menge,
                    AusgegebenAm = zeile.AusgegebenAm,
                    AusgabeVorgangId = zeile.AusgabeVorgangId,
                });
                zeile.Menge = position.Menge;
            }

            zeile.ZurueckAm = heute;
            zeile.RuecknahmeVorgangId = vorgang.Id;
            zeile.Zustand = position.Zustand;
            zeile.RueckgabeOrdnungsgemaess = position.Ordnungsgemaess;
            zeile.RueckgabeMaengel = position.Ordnungsgemaess ? null : maengel;

            if (zeile.Geraet is { } geraet)
            {
                if (position.Zustand == RuecknahmeZustand.Aussondern)
                {
                    geraet.AusgesondertAm = heute;
                    auszusondern.Add(geraet);
                }
                else if (!position.Ordnungsgemaess)
                {
                    // Wieder eingelagert, aber mit Mängeln: der Gerätewart sieht sie in der Mängelliste.
                    db.Maengel.Add(new Mangel
                    {
                        GeraetId = geraet.Id,
                        Beschreibung = $"Bei Rückgabe festgestellt: {maengel}",
                        Schwere = MangelSchwere.Einschraenkung,
                        Status = MangelStatus.Offen,
                        GemeldetAm = jetzt,
                    });
                }
            }
            else if (position.Zustand == RuecknahmeZustand.Einlagern)
            {
                var bestand = await BestandAsync(zeile.ArtikelId, zeile.Groesse, ct);
                bestand.Menge += position.Menge;
            }
        }

        await MitNummerSpeichernAsync(vorgang, ct);

        // Der Aussonderungsgrund nennt die Quittung; deren Nummer steht erst nach dem Speichern fest.
        if (auszusondern.Count > 0)
        {
            foreach (var geraet in auszusondern)
            {
                geraet.Aussonderungsgrund ??= $"Rücknahme Kleiderkammer {vorgang.Nummer}";
            }

            await db.SaveChangesAsync(ct);
        }

        return vorgang;
    }

    /// <summary>Wareneingang eines Mengenartikels. Negative Menge bucht einen Abgang (z. B. Inventurdifferenz).</summary>
    public async Task<Lagerbestand> ZugangBuchenAsync(Guid artikelId, string groesse, int menge, CancellationToken ct = default)
    {
        var artikel = await db.Artikel.FirstOrDefaultAsync(a => a.Id == artikelId, ct) ?? throw new KleiderkammerFehler("Artikel nicht gefunden.");
        if (artikel.IstEinzelstueck)
        {
            throw new KleiderkammerFehler("Einzelstücke werden einzeln mit Inventarnummer angelegt.");
        }

        var bestand = await BestandAsync(artikelId, Groesse.Normalisieren(groesse), ct);
        if (bestand.Menge + menge < 0)
        {
            throw new KleiderkammerFehler($"Im Lager sind nur {bestand.Menge} Stück.");
        }

        bestand.Menge += menge;
        await db.SaveChangesAsync(ct);
        return bestand;
    }

    public async Task MindestmengeSetzenAsync(Guid artikelId, string groesse, int mindestmenge, CancellationToken ct = default)
    {
        if (mindestmenge < 0)
        {
            throw new KleiderkammerFehler("Die Mindestmenge darf nicht negativ sein.");
        }

        var bestand = await BestandAsync(artikelId, Groesse.Normalisieren(groesse), ct);
        bestand.Mindestmenge = mindestmenge;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Neues Einzelstück (Helm, Jacke, ...) als Gerät der Geräteart des Artikels, zunächst im Lager.</summary>
    public async Task<Geraet> EinzelstueckAnlegenAsync(
        Guid artikelId, string inventarnummer, string groesse, string? hersteller, string? seriennummer, DateOnly? herstelldatum, CancellationToken ct = default)
    {
        var artikel = await db.Artikel.FirstOrDefaultAsync(a => a.Id == artikelId, ct) ?? throw new KleiderkammerFehler("Artikel nicht gefunden.");
        if (artikel.GeraeteartId is not { } geraeteartId)
        {
            throw new KleiderkammerFehler("Mengenartikel werden über den Lagerbestand gebucht.");
        }

        inventarnummer = inventarnummer.Trim();
        if (await db.Geraete.IgnoreQueryFilters().AnyAsync(g => g.Inventarnummer == inventarnummer, ct))
        {
            throw new KleiderkammerFehler($"Die Inventarnummer {inventarnummer} ist schon vergeben.");
        }

        var heute = Heute(zeit.GetUtcNow());

        // Persönliche Ausrüstung zählt die Kleiderkammer über die Ausstattung; der Materialstamm ist daher nicht inventurpflichtig.
        var material = await Materialstamm.FindenOderAnlegenAsync(db, artikel.Bezeichnung, geraeteartId, hersteller, null, inventurpflichtig: false, ct);
        var geraet = new Geraet
        {
            Inventarnummer = inventarnummer,
            Bezeichnung = artikel.Bezeichnung,
            Groesse = Groesse.Normalisieren(groesse),
            Seriennummer = seriennummer,
            Herstelldatum = herstelldatum,
            IndienstAm = heute,
            Lagerort = "Kleiderkammer",
        };
        Materialstamm.Zuordnen(geraet, material);
        db.Geraete.Add(geraet);

        // Damit die Größe in der Bestandsübersicht auch ohne Mindestmenge auftaucht.
        await BestandAsync(artikelId, geraet.Groesse, ct);
        await db.SaveChangesAsync(ct);
        return geraet;
    }

    public async Task GroessenSetzenAsync(Guid mitgliedId, IReadOnlyDictionary<Groessenart, string?> groessen, CancellationToken ct = default)
    {
        var heute = Heute(zeit.GetUtcNow());
        var vorhanden = await db.Groessenangaben.Where(g => g.MitgliedId == mitgliedId).ToListAsync(ct);
        foreach (var (art, wert) in groessen)
        {
            var neu = Groesse.Normalisieren(wert);
            var angabe = vorhanden.FirstOrDefault(g => g.Groessenart == art);
            if (neu.Length == 0)
            {
                if (angabe is not null)
                {
                    db.Groessenangaben.Remove(angabe);
                }
            }
            else if (angabe is null)
            {
                db.Groessenangaben.Add(new Groessenangabe { MitgliedId = mitgliedId, Groessenart = art, Wert = neu, ErfasstAm = heute });
            }
            else if (angabe.Wert != neu)
            {
                angabe.Wert = neu;
                angabe.ErfasstAm = heute;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task QuittiertAsync(Guid vorgangId, CancellationToken ct = default)
    {
        var vorgang = await db.Kleiderkammervorgaenge.FirstOrDefaultAsync(v => v.Id == vorgangId, ct) ?? throw new KleiderkammerFehler("Vorgang nicht gefunden.");
        vorgang.QuittiertAm ??= zeit.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public static DateOnly Heute(DateTimeOffset jetzt) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(jetzt, Berlin).DateTime);

    /// <summary>
    /// Scheitert eine Position, wird nichts gebucht: Die halb fertigen Änderungen werden aus dem Kontext verworfen,
    /// damit ein späteres SaveChanges sie nicht doch noch speichert.
    /// </summary>
    private async Task<T> AlleOderKeineAsync<T>(Func<Task<T>> buchung)
    {
        try
        {
            return await buchung();
        }
        catch (KleiderkammerFehler)
        {
            // Neue Abhängige (Ausstattung) vor ihrem Vorgang lösen: wird zuerst der Vorgang gelöst, meldet EF Core eine
            // gekappte Pflichtbeziehung und die Seite endet mit Fehler 500 statt mit der Fehlermeldung an die Kleiderkammer.
            var neue = db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList();
            foreach (var eintrag in neue.OrderByDescending(e => e.Metadata.GetForeignKeys().Count(fk => neue.Any(p => p.Metadata == fk.PrincipalEntityType))))
            {
                eintrag.State = EntityState.Detached;
            }

            foreach (var eintrag in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Modified).ToList())
            {
                eintrag.CurrentValues.SetValues(eintrag.OriginalValues);
                eintrag.State = EntityState.Unchanged;
            }

            throw;
        }
    }

    private async Task<Ausstattung> EinzelstueckAusgebenAsync(
        Artikel artikel, AusgabePosition position, Guid mitgliedId, Kleiderkammervorgang vorgang, DateOnly heute, CancellationToken ct)
    {
        if (position.GeraetId is not { } geraetId)
        {
            throw new KleiderkammerFehler($"{artikel.Bezeichnung}: bitte das Stück (Inventarnummer) wählen.");
        }

        var geraet = await db.Geraete.FirstOrDefaultAsync(g => g.Id == geraetId, ct);
        if (geraet is null || geraet.GeraeteartId != artikel.GeraeteartId)
        {
            throw new KleiderkammerFehler($"{artikel.Bezeichnung}: das gewählte Stück gehört nicht zu diesem Artikel.");
        }

        if (geraet.AusgesondertAm is not null)
        {
            throw new KleiderkammerFehler($"{geraet.Inventarnummer} ist ausgesondert.");
        }

        if (await db.Ausstattungen.AnyAsync(a => a.GeraetId == geraetId && a.ZurueckAm == null, ct))
        {
            throw new KleiderkammerFehler($"{geraet.Inventarnummer} ist bereits ausgegeben.");
        }

        return new Ausstattung
        {
            MitgliedId = mitgliedId,
            ArtikelId = artikel.Id,
            Groesse = geraet.Groesse ?? Groesse.Normalisieren(position.Groesse),
            Menge = 1,
            GeraetId = geraet.Id,
            AusgegebenAm = heute,
            AusgabeVorgangId = vorgang.Id,
        };
    }

    private async Task<Kleiderkammervorgang> VorgangAsync(Guid mitgliedId, VorgangArt art, DateTimeOffset jetzt, string? bemerkung, CancellationToken ct)
    {
        if (!await db.Mitglieder.AnyAsync(m => m.Id == mitgliedId, ct))
        {
            throw new KleiderkammerFehler("Mitglied nicht gefunden.");
        }

        var vorgang = new Kleiderkammervorgang
        {
            Nummer = "",
            Art = art,
            MitgliedId = mitgliedId,
            Zeitpunkt = jetzt,
            Bemerkung = string.IsNullOrWhiteSpace(bemerkung) ? null : bemerkung.Trim(),
        };
        db.Kleiderkammervorgaenge.Add(vorgang);
        return vorgang;
    }

    private async Task<Lagerbestand> BestandAsync(Guid artikelId, string groesse, CancellationToken ct)
    {
        var bestand = db.Lagerbestaende.Local.FirstOrDefault(l => l.ArtikelId == artikelId && l.Groesse == groesse)
            ?? await db.Lagerbestaende.FirstOrDefaultAsync(l => l.ArtikelId == artikelId && l.Groesse == groesse, ct);
        if (bestand is null)
        {
            bestand = new Lagerbestand { ArtikelId = artikelId, Groesse = groesse };
            db.Lagerbestaende.Add(bestand);
        }

        return bestand;
    }

    private async Task MitNummerSpeichernAsync(Kleiderkammervorgang vorgang, CancellationToken ct)
    {
        var jahr = TimeZoneInfo.ConvertTime(vorgang.Zeitpunkt, Berlin).Year;
        var praefix = $"KK-{jahr}-";
        for (var versuch = 1; ; versuch++)
        {
            var nummern = await db.Kleiderkammervorgaenge.IgnoreQueryFilters().AsNoTracking()
                .Where(v => v.Nummer.StartsWith(praefix))
                .Select(v => v.Nummer)
                .ToListAsync(ct);
            var hoechste = nummern.Select(n => int.TryParse(n[praefix.Length..], out var z) ? z : 0).DefaultIfEmpty(0).Max();
            vorgang.Nummer = $"{praefix}{hoechste + 1:0000}";
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (versuch < Versuche
                && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
                && pg.ConstraintName?.Contains("nummer", StringComparison.Ordinal) == true)
            {
                // Zwei Vorgänge gleichzeitig: nächste Nummer probieren, Protokolleinträge des Fehlversuchs verwerfen.
                foreach (var eintrag in db.ChangeTracker.Entries<AuditEintrag>().Where(e => e.State == EntityState.Added).ToList())
                {
                    eintrag.State = EntityState.Detached;
                }
            }
        }
    }

    private static string Anzeige(string groesse) => groesse.Length == 0 ? "" : $"Gr. {groesse}";
}
