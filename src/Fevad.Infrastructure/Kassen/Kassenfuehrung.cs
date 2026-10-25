using System.Security.Cryptography;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kassen;
using Fevad.Domain.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fevad.Infrastructure.Kassen;

/// <summary>
/// Kassenbuch führen (V01, V02, V06): Kassen einrichten, Belegnummern vergeben, stornieren, festschreiben, Wirtschaftspläne anlegen
/// und Kontostände berechnen.
/// </summary>
public sealed class Kassenfuehrung(AppDbContext db, TimeProvider zeit)
{
    private const int Versuche = 5;

    /// <summary>Name der Kasse der Gesamtwehr, die beim ersten Start angelegt wird.</summary>
    public const string NameKameradschaftskasse = "Kameradschaftskasse";

    /// <summary>Name der Kasse der Jugendfeuerwehr, die beim ersten Start angelegt wird.</summary>
    public const string NameJugendkasse = "Kasse der Jugendfeuerwehr";

    /// <summary>
    /// Legt beim ersten Start die beiden Sondervermögen an (Andi, 25.10.2026): Kameradschaftskasse und Kasse der Jugendfeuerwehr,
    /// je mit Girokonto und Barkasse ab dem 1. Januar des laufenden Jahres und der Gliederung nach dem LFV-Muster. Gibt es schon
    /// eine Kasse, passiert nichts.
    /// </summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="heute">Heutiger Tag; bestimmt den Beginn der Konten.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public static async Task KassenAnlegenAsync(AppDbContext db, DateOnly heute, CancellationToken ct = default)
    {
        if (await db.Kassen.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        var jugend = await db.Abteilungen.Where(a => a.Code == AbteilungCode.Jugend).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        var beginn = new DateOnly(heute.Year, 1, 1);
        db.Kassen.Add(Neu(NameKameradschaftskasse, null, 10, beginn, Kassenregeln.MusterKameradschaftskasse));
        db.Kassen.Add(Neu(NameJugendkasse, jugend, 20, beginn, Kassenregeln.MusterJugendkasse));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Neue Kasse mit Girokonto, Barkasse und Gliederung.</summary>
    /// <param name="name">Name der Kasse.</param>
    /// <param name="abteilungId">Abteilung, der die Kasse gehört.</param>
    /// <param name="sortierung">Reihenfolge.</param>
    /// <param name="beginn">Beginn der Konten.</param>
    /// <param name="muster">Gliederung.</param>
    /// <returns>Die Kasse, noch nicht gespeichert.</returns>
    public static Kasse Neu(
        string name, Guid? abteilungId, int sortierung, DateOnly beginn,
        IEnumerable<(string Nummer, string Bezeichnung, Planrichtung Richtung, bool Deckungsfaehig, string? Hinweis)> muster) => new()
    {
        Name = name,
        Rechtsform = KassenRechtsform.SondervermoegenFwG,
        AbteilungId = abteilungId,
        Sortierung = sortierung,
        Konten =
        [
            new() { Bezeichnung = "Girokonto", Art = KontoArt.Girokonto, Eroeffnet = beginn, Sortierung = 10 },
            new() { Bezeichnung = "Barkasse", Art = KontoArt.Barkasse, Eroeffnet = beginn, Sortierung = 20 },
        ],
        Positionen = muster.Select(m => new Planposition
        {
            Nummer = m.Nummer, Bezeichnung = m.Bezeichnung, Richtung = m.Richtung, Deckungsfaehig = m.Deckungsfaehig, Hinweis = m.Hinweis,
        }).ToList(),
    };

    /// <summary>Speichert eine neue Buchung mit der nächsten Belegnummer ihrer Kasse im Jahr des Buchungstags.</summary>
    /// <param name="buchung">Die geprüfte Buchung ohne Belegnummer; Belege können angehängt sein.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task BuchenAsync(Buchung buchung, CancellationToken ct = default)
    {
        buchung.Jahr = buchung.Datum.Year;
        db.Buchungen.Add(buchung);
        for (var versuch = 1; ; versuch++)
        {
            // Auch gelöschte Buchungen zählen mit: eine Belegnummer wird nie zweimal vergeben.
            buchung.Belegnummer = await db.Buchungen.IgnoreQueryFilters().AsNoTracking()
                .Where(b => b.KasseId == buchung.KasseId && b.Jahr == buchung.Jahr)
                .MaxAsync(b => (int?)b.Belegnummer, ct) is { } hoechste ? hoechste + 1 : 1;
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (versuch < Versuche
                && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                foreach (var eintrag in db.ChangeTracker.Entries<AuditEintrag>().Where(e => e.State == EntityState.Added).ToList())
                {
                    eintrag.State = EntityState.Detached;
                }
            }
        }
    }

    /// <summary>Storniert eine Buchung durch eine Gegenbuchung; die neue Buchung ist sofort festgeschrieben.</summary>
    /// <param name="buchungId">Die zu stornierende Buchung.</param>
    /// <param name="datum">Tag des Stornos.</param>
    /// <param name="grund">Grund des Stornos.</param>
    /// <param name="benutzerId">Konto, das storniert.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Stornobuchung.</returns>
    /// <exception cref="InvalidOperationException">Die Buchung ist selbst ein Storno oder wurde schon storniert.</exception>
    public async Task<Buchung> StornierenAsync(Guid buchungId, DateOnly datum, string grund, Guid? benutzerId, CancellationToken ct = default)
    {
        var original = await db.Buchungen.FirstAsync(b => b.Id == buchungId, ct);
        if (original.StornoVonId is not null)
        {
            throw new InvalidOperationException("Ein Storno lässt sich nicht stornieren; bitte neu buchen.");
        }

        if (await db.Buchungen.AnyAsync(b => b.StornoVonId == buchungId, ct))
        {
            throw new InvalidOperationException($"Beleg {original.Kennung} ist bereits storniert.");
        }

        if (datum < original.Datum)
        {
            throw new InvalidOperationException("Das Storno kann nicht vor der Buchung liegen.");
        }

        var storno = Kassenregeln.Storno(original, datum, grund.Trim());
        storno.FestgeschriebenAm = zeit.GetUtcNow();
        storno.FestgeschriebenVon = benutzerId;
        await BuchenAsync(storno, ct);
        return storno;
    }

    /// <summary>
    /// Schreibt alle offenen Buchungen einer Kasse bis zu einem Tag fest, die einen vollständigen Beleg haben. Buchungen ohne Beleg
    /// bleiben offen und werden gezählt.
    /// </summary>
    /// <param name="kasseId">Die Kasse.</param>
    /// <param name="bis">Letzter Buchungstag, der festgeschrieben wird.</param>
    /// <param name="benutzerId">Konto, das festschreibt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der festgeschriebenen Buchungen und der wegen fehlendem Beleg offen gebliebenen.</returns>
    public async Task<(int Festgeschrieben, int OhneBeleg)> FestschreibenAsync(Guid kasseId, DateOnly bis, Guid? benutzerId, CancellationToken ct = default)
    {
        var offen = await db.Buchungen
            .Where(b => b.KasseId == kasseId && b.FestgeschriebenAm == null && b.Datum <= bis)
            .Select(b => new { Buchung = b, Dateien = b.Belege.Count })
            .ToListAsync(ct);
        var jetzt = zeit.GetUtcNow();
        var anzahl = 0;
        foreach (var o in offen.Where(o => Kassenregeln.BelegVollstaendig(o.Buchung.Belegart, o.Buchung.EigenbelegBegruendung, o.Dateien)))
        {
            o.Buchung.FestgeschriebenAm = jetzt;
            o.Buchung.FestgeschriebenVon = benutzerId;
            anzahl++;
        }

        await db.SaveChangesAsync(ct);
        return (anzahl, offen.Count - anzahl);
    }

    /// <summary>
    /// Legt den Wirtschaftsplan eines Jahres als Entwurf an: Ansätze aus dem Vorjahresplan, sonst 0 für jede aktive Position. Gibt es
    /// den Plan schon, wird er zurückgegeben.
    /// </summary>
    /// <param name="kasseId">Die Kasse.</param>
    /// <param name="jahr">Das Haushaltsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Plan.</returns>
    public async Task<Wirtschaftsplan> PlanAnlegenAsync(Guid kasseId, int jahr, CancellationToken ct = default)
    {
        var vorhanden = await db.Wirtschaftsplaene.Include(w => w.Ansaetze).FirstOrDefaultAsync(w => w.KasseId == kasseId && w.Jahr == jahr, ct);
        if (vorhanden is not null)
        {
            return vorhanden;
        }

        var vorjahr = await db.Planansaetze.AsNoTracking()
            .Where(a => a.Wirtschaftsplan!.KasseId == kasseId && a.Wirtschaftsplan.Jahr == jahr - 1)
            .ToDictionaryAsync(a => a.PlanpositionId, ct);
        var positionen = await db.Planpositionen.AsNoTracking().Where(p => p.KasseId == kasseId && p.Aktiv).Select(p => p.Id).ToListAsync(ct);
        var plan = new Wirtschaftsplan
        {
            KasseId = kasseId,
            Jahr = jahr,
            Status = WirtschaftsplanStatus.Entwurf,
            Ansaetze = positionen.Select(p => new Planansatz
            {
                PlanpositionId = p,
                Betrag = vorjahr.GetValueOrDefault(p)?.Betrag ?? 0,
                Erlaeuterung = vorjahr.GetValueOrDefault(p)?.Erlaeuterung,
            }).ToList(),
        };
        db.Wirtschaftsplaene.Add(plan);
        await db.SaveChangesAsync(ct);
        return plan;
    }

    /// <summary>Kontostände aller Konten einer Kasse am Ende eines Tages.</summary>
    /// <param name="kasseId">Die Kasse.</param>
    /// <param name="bis">Stichtag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Konto und Bestand, in der Reihenfolge der Konten.</returns>
    public async Task<IReadOnlyList<(Zahlungsmittelkonto Konto, decimal Bestand)>> KontostaendeAsync(Guid kasseId, DateOnly bis, CancellationToken ct = default)
    {
        var konten = await db.Zahlungsmittelkonten.AsNoTracking().Where(k => k.KasseId == kasseId)
            .OrderBy(k => k.Sortierung).ThenBy(k => k.Bezeichnung).ToListAsync(ct);
        var buchungen = await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasseId && b.Datum <= bis).ToListAsync(ct);
        return konten.Select(k => (k, Kassenregeln.Kontostand(k, buchungen, bis))).ToList();
    }

    /// <summary>Neue Belegdatei mit Prüfsumme.</summary>
    /// <param name="dateiname">Bereinigter Dateiname.</param>
    /// <param name="inhaltstyp">MIME-Typ.</param>
    /// <param name="inhalt">Inhalt, bei Bildern schon ohne Metadaten.</param>
    /// <returns>Der Beleg ohne Bezug.</returns>
    public static Kassenbeleg Beleg(string dateiname, string inhaltstyp, byte[] inhalt) => new()
    {
        Dateiname = dateiname,
        Inhaltstyp = inhaltstyp,
        Inhalt = inhalt,
        Pruefsumme = Convert.ToHexStringLower(SHA256.HashData(inhalt)),
    };
}
