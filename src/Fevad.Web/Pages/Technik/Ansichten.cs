using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik;

/// <summary>Eine Zeile im Prüfbuch; stornierte Prüfungen bleiben sichtbar.</summary>
public sealed record PruefbuchZeile(Guid Id, DateOnly Datum, Pruefart Pruefart, PruefErgebnis Ergebnis, string? Pruefer, DateOnly? NaechsteFaellig, bool Storniert = false);

/// <summary>Eine Zeile der Mängelliste.</summary>
/// <param name="Id">Kennung des Mangels.</param>
/// <param name="Gegenstand">Inventarnummer und Bezeichnung des Geräts oder Funkrufname des Fahrzeugs.</param>
/// <param name="GemeldetAm">Zeitpunkt der Meldung (UTC).</param>
/// <param name="Beschreibung">Beschreibung des Mangels.</param>
/// <param name="Schwere">Schwere.</param>
/// <param name="Status">Bearbeitungsstand.</param>
/// <param name="GemeldetVon">Name des meldenden Mitglieds.</param>
public sealed record MangelZeile(Guid Id, string? Gegenstand, DateTimeOffset GemeldetAm, string Beschreibung, MangelSchwere Schwere, MangelStatus Status, string? GemeldetVon);

/// <summary>Gemeinsame Abfragen der Detailseiten von Gerät und Fahrzeug.</summary>
public static class Ansichten
{
    /// <summary>Prüfbuch eines Geräts oder Fahrzeugs, jüngste Prüfung zuerst.</summary>
    /// <param name="db">Datenbankkontext.</param>
    /// <param name="geraetId">Kennung des Geräts.</param>
    /// <param name="fahrzeugId">Kennung des Fahrzeugs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zeilen des Prüfbuchs.</returns>
    public static async Task<IReadOnlyList<PruefbuchZeile>> PruefbuchAsync(AppDbContext db, Guid? geraetId, Guid? fahrzeugId, CancellationToken ct) =>
        await db.Pruefungen.AsNoTracking()
            .Where(p => (geraetId != null && p.GeraetId == geraetId) || (fahrzeugId != null && p.FahrzeugId == fahrzeugId))
            .OrderByDescending(p => p.Datum).ThenByDescending(p => p.ErstelltAm)
            .Select(p => new PruefbuchZeile(p.Id, p.Datum, p.Pruefart, p.Ergebnis,
                p.Pruefer != null ? p.Pruefer.Nachname + ", " + p.Pruefer.Vorname : p.PrueferExtern, p.NaechsteFaellig, p.StorniertAm != null))
            .ToListAsync(ct);

    /// <summary>Mängel als Zeilen, offene und schwere zuerst.</summary>
    /// <param name="quelle">Vorgefilterte Mängel.</param>
    /// <returns>Die Abfrage der Zeilen.</returns>
    public static IQueryable<MangelZeile> Maengel(IQueryable<Mangel> quelle) =>
        quelle.AsNoTracking()
            .OrderBy(m => m.Status).ThenByDescending(m => m.Schwere).ThenByDescending(m => m.GemeldetAm)
            .Select(m => new MangelZeile(m.Id,
                m.Geraet != null ? m.Geraet.Inventarnummer + " " + m.Geraet.Bezeichnung : m.Fahrzeug!.Funkrufname,
                m.GemeldetAm, m.Beschreibung, m.Schwere, m.Status,
                m.GemeldetVon != null ? m.GemeldetVon.Vorname + " " + m.GemeldetVon.Nachname : null));
}
