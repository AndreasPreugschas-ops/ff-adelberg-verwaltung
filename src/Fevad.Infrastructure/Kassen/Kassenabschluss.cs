using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Kassen;

/// <summary>
/// Jahresabschluss einer Kasse (V05): Jahresrechnung mit Plan und Ist, Kontoständen und Bestandsverzeichnis berechnen, das Jahr abschließen
/// und abgeschlossene Jahre für Ausnahmebuchungen liefern.
/// </summary>
/// <param name="db">Datenbank.</param>
/// <param name="zeit">Uhr für den Zeitpunkt des Abschlusses.</param>
public sealed class Kassenabschluss(AppDbContext db, TimeProvider zeit)
{
    /// <summary>Plan und Ist einer Position im Jahr.</summary>
    /// <param name="Position">Die Position.</param>
    /// <param name="Ansatz">Ansatz des Wirtschaftsplans, 0 ohne Plan.</param>
    /// <param name="Ist">Ist des Jahres.</param>
    public sealed record Planzeile(Planposition Position, decimal Ansatz, decimal Ist)
    {
        /// <summary>Ist minus Ansatz.</summary>
        public decimal Abweichung => Ist - Ansatz;
    }

    /// <summary>Bestand eines Kontos am Anfang und am Ende des Jahres.</summary>
    /// <param name="Konto">Das Konto.</param>
    /// <param name="Anfang">Bestand am 31.12. des Vorjahres.</param>
    /// <param name="Ende">Bestand am 31.12. des Jahres.</param>
    public sealed record Kontozeile(Zahlungsmittelkonto Konto, decimal Anfang, decimal Ende);

    /// <summary>Jahresrechnung einer Kasse, wie sie Jahresabschluss und Prüfbericht zeigen.</summary>
    /// <param name="Kasse">Die Kasse.</param>
    /// <param name="Jahr">Das Haushaltsjahr.</param>
    /// <param name="Einnahmen">Einnahmen ohne Umbuchungen, Stornos abgezogen.</param>
    /// <param name="Ausgaben">Ausgaben ohne Umbuchungen, Stornos abgezogen.</param>
    /// <param name="Konten">Kontostände am Anfang und Ende des Jahres.</param>
    /// <param name="Plan">Plan und Ist je Position; Positionen ohne Ansatz und Ist fehlen.</param>
    /// <param name="OhnePosition">Ist der Buchungen ohne Position (Einnahmen positiv, Ausgaben negativ).</param>
    /// <param name="PlanBeschlossen">Der Wirtschaftsplan des Jahres ist beschlossen.</param>
    /// <param name="Bestand">Gegenstände des Bestandsverzeichnisses am 31.12.</param>
    /// <param name="Ausnahmen">Ausnahmebuchungen des Jahres nach dem Abschluss.</param>
    /// <param name="Buchungen">Zahl der Buchungen im Jahr.</param>
    /// <param name="NichtFestgeschrieben">Buchungen des Jahres, die noch nicht festgeschrieben sind.</param>
    /// <param name="Abschluss">Der Abschluss, falls das Jahr abgeschlossen ist.</param>
    public sealed record Jahresrechnung(
        Kasse Kasse, int Jahr, decimal Einnahmen, decimal Ausgaben, IReadOnlyList<Kontozeile> Konten, IReadOnlyList<Planzeile> Plan,
        decimal OhnePosition, bool PlanBeschlossen, IReadOnlyList<Vermoegensgegenstand> Bestand, IReadOnlyList<Buchung> Ausnahmen,
        int Buchungen, int NichtFestgeschrieben, Jahresabschluss? Abschluss)
    {
        /// <summary>Summe der Anfangsbestände.</summary>
        public decimal Anfang => Konten.Sum(k => k.Anfang);

        /// <summary>Summe der Endbestände.</summary>
        public decimal Ende => Konten.Sum(k => k.Ende);

        /// <summary>Endbestände der Rücklagenkonten.</summary>
        public decimal Ruecklagen => Konten.Where(k => k.Konto.Art == KontoArt.Ruecklage).Sum(k => k.Ende);
    }

    /// <summary>Mitglieder, die am Tag als Kassenprüfer im Amt sind (laufende Funktion mit der Rolle Kassenprüfer).</summary>
    /// <param name="tag">Stichtag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Kennung und Anzeigename, nach Namen sortiert.</returns>
    public async Task<IReadOnlyList<(Guid Id, string Name)>> AmtierendePrueferAsync(DateOnly tag, CancellationToken ct = default) =>
        (await db.FunktionZuordnungen.AsNoTracking()
            .Where(z => z.Funktion!.VorgeschlageneRolle == Identitaet.Rollen.Kassenpruefer && z.GueltigAb <= tag && (z.GueltigBis == null || z.GueltigBis >= tag))
            .Select(z => new { z.MitgliedId, z.Mitglied!.Nachname, z.Mitglied.Vorname }).Distinct().ToListAsync(ct))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname).Select(m => (m.MitgliedId, $"{m.Nachname}, {m.Vorname}")).ToList();

    /// <summary>Abgeschlossene Jahre einer Kasse.</summary>
    /// <param name="kasseId">Die Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Jahre.</returns>
    public async Task<IReadOnlySet<int>> AbgeschlosseneJahreAsync(Guid kasseId, CancellationToken ct = default) =>
        (await db.Jahresabschluesse.AsNoTracking().Where(j => j.KasseId == kasseId).Select(j => j.Jahr).ToListAsync(ct)).ToHashSet();

    /// <summary>Berechnet die Jahresrechnung einer Kasse.</summary>
    /// <param name="kasse">Die Kasse.</param>
    /// <param name="jahr">Das Haushaltsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Jahresrechnung.</returns>
    public async Task<Jahresrechnung> RechnungAsync(Kasse kasse, int jahr, CancellationToken ct = default)
    {
        var ende = new DateOnly(jahr, 12, 31);
        var konten = await db.Zahlungsmittelkonten.AsNoTracking().Where(k => k.KasseId == kasse.Id)
            .OrderBy(k => k.Sortierung).ThenBy(k => k.Bezeichnung).ToListAsync(ct);
        var bisEnde = await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasse.Id && b.Datum <= ende).ToListAsync(ct);
        var imJahr = bisEnde.Where(b => b.Jahr == jahr).ToList();
        var kontozeilen = konten
            .Select(k => new Kontozeile(k, Kassenregeln.Kontostand(k, bisEnde, ende.AddYears(-1)), Kassenregeln.Kontostand(k, bisEnde, ende)))
            .Where(z => z.Konto.Aktiv || z.Anfang != 0 || z.Ende != 0)
            .ToList();

        var plan = await db.Wirtschaftsplaene.AsNoTracking().Include(w => w.Ansaetze).FirstOrDefaultAsync(w => w.KasseId == kasse.Id && w.Jahr == jahr, ct);
        var ansaetze = plan?.Ansaetze.ToDictionary(a => a.PlanpositionId, a => a.Betrag) ?? [];
        var ist = Kassenregeln.IstJePosition(imJahr.Where(b => b.Art != BuchungArt.Umbuchung));
        var positionen = await db.Planpositionen.AsNoTracking().Where(p => p.KasseId == kasse.Id).ToListAsync(ct);
        var planzeilen = positionen.Where(p => ansaetze.GetValueOrDefault(p.Id) != 0 || ist.ContainsKey(p.Id))
            .OrderBy(p => p.Richtung).ThenBy(p => p.Nummer, StringComparer.Ordinal)
            .Select(p => new Planzeile(p, ansaetze.GetValueOrDefault(p.Id), ist.GetValueOrDefault(p.Id)))
            .ToList();
        var ohnePosition = imJahr.Where(b => b.PlanpositionId is null && b.Art != BuchungArt.Umbuchung)
            .Sum(b => b.Art == BuchungArt.Einnahme ? Kassenregeln.Ist(b) : -Kassenregeln.Ist(b));

        var bestand = (await db.Vermoegensgegenstaende.AsNoTracking().Where(v => v.KasseId == kasse.Id && v.Anschaffung <= ende)
            .OrderBy(v => v.Anschaffung).ThenBy(v => v.Bezeichnung).ToListAsync(ct)).Where(v => v.ImBestand(ende)).ToList();
        var abschluss = await db.Jahresabschluesse.AsNoTracking().Include(j => j.Endbestaende).Include(j => j.EntlastungBeschluss)
            .FirstOrDefaultAsync(j => j.KasseId == kasse.Id && j.Jahr == jahr, ct);
        return new Jahresrechnung(
            kasse, jahr,
            imJahr.Where(b => b.Art == BuchungArt.Einnahme).Sum(Kassenregeln.Ist),
            imJahr.Where(b => b.Art == BuchungArt.Ausgabe).Sum(Kassenregeln.Ist),
            kontozeilen, planzeilen, ohnePosition, plan?.Status == WirtschaftsplanStatus.Beschlossen, bestand,
            imJahr.Where(b => b.AusnahmeBegruendung is not null).OrderBy(b => b.Belegnummer).ToList(),
            imJahr.Count, imJahr.Count(b => !b.Festgeschrieben), abschluss);
    }

    /// <summary>Gründe, warum das Jahr noch nicht abgeschlossen werden kann.</summary>
    /// <param name="r">Die Jahresrechnung.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Hinderungsgründe; leer, wenn abgeschlossen werden kann.</returns>
    public async Task<IReadOnlyList<string>> HindernisseAsync(Jahresrechnung r, DateOnly heute, CancellationToken ct = default)
    {
        if (r.Abschluss is not null)
        {
            return [$"Das Jahr {r.Jahr} ist bereits abgeschlossen."];
        }

        var vorjahrOffen = await db.Buchungen.AnyAsync(b => b.KasseId == r.Kasse.Id && b.Jahr == r.Jahr - 1, ct)
            && !await db.Jahresabschluesse.AnyAsync(j => j.KasseId == r.Kasse.Id && j.Jahr == r.Jahr - 1, ct);
        return Abschlussregeln.Hindernisse(r.Jahr, r.NichtFestgeschrieben, vorjahrOffen, heute);
    }

    /// <summary>Schließt das Jahr ab und hält Einnahmen, Ausgaben und Endbestände fest.</summary>
    /// <param name="kasse">Die Kasse.</param>
    /// <param name="jahr">Das Haushaltsjahr.</param>
    /// <param name="benutzerId">Konto, das abschließt.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Abschluss.</returns>
    /// <exception cref="InvalidOperationException">Das Jahr lässt sich noch nicht abschließen; die Meldung nennt die Gründe.</exception>
    public async Task<Jahresabschluss> AbschliessenAsync(Kasse kasse, int jahr, Guid? benutzerId, DateOnly heute, CancellationToken ct = default)
    {
        var r = await RechnungAsync(kasse, jahr, ct);
        if (await HindernisseAsync(r, heute, ct) is { Count: > 0 } gruende)
        {
            throw new InvalidOperationException(string.Join(" ", gruende));
        }

        var abschluss = new Jahresabschluss
        {
            KasseId = kasse.Id,
            Jahr = jahr,
            AbgeschlossenAm = zeit.GetUtcNow(),
            AbgeschlossenVon = benutzerId,
            Einnahmen = r.Einnahmen,
            Ausgaben = r.Ausgaben,
            Endbestaende = r.Konten.Select(k => new Kontoendbestand { KontoId = k.Konto.Id, Bestand = k.Ende }).ToList(),
        };
        db.Jahresabschluesse.Add(abschluss);
        await db.SaveChangesAsync(ct);
        return abschluss;
    }
}
