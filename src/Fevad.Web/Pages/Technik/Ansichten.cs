using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik;

/// <summary>Eine Zeile im Prüfbuch; stornierte Prüfungen bleiben sichtbar.</summary>
public sealed record PruefbuchZeile(Guid Id, DateOnly Datum, Pruefart Pruefart, PruefErgebnis Ergebnis, string? Pruefer, DateOnly? NaechsteFaellig, bool Storniert = false);

public sealed record MangelZeile(Guid Id, string? Gegenstand, DateTimeOffset GemeldetAm, string Beschreibung, MangelSchwere Schwere, MangelStatus Status, string? GemeldetVon);

/// <summary>Gemeinsame Abfragen der Detailseiten von Gerät und Fahrzeug.</summary>
public static class Ansichten
{
    public static async Task<IReadOnlyList<PruefbuchZeile>> PruefbuchAsync(AppDbContext db, Guid? geraetId, Guid? fahrzeugId, CancellationToken ct) =>
        await db.Pruefungen.AsNoTracking()
            .Where(p => (geraetId != null && p.GeraetId == geraetId) || (fahrzeugId != null && p.FahrzeugId == fahrzeugId))
            .OrderByDescending(p => p.Datum).ThenByDescending(p => p.ErstelltAm)
            .Select(p => new PruefbuchZeile(p.Id, p.Datum, p.Pruefart, p.Ergebnis,
                p.Pruefer != null ? p.Pruefer.Nachname + ", " + p.Pruefer.Vorname : p.PrueferExtern, p.NaechsteFaellig, p.StorniertAm != null))
            .ToListAsync(ct);

    public static IQueryable<MangelZeile> Maengel(IQueryable<Mangel> quelle) =>
        quelle.AsNoTracking()
            .OrderBy(m => m.Status).ThenByDescending(m => m.Schwere).ThenByDescending(m => m.GemeldetAm)
            .Select(m => new MangelZeile(m.Id,
                m.Geraet != null ? m.Geraet.Inventarnummer + " " + m.Geraet.Bezeichnung : m.Fahrzeug!.Funkrufname,
                m.GemeldetAm, m.Beschreibung, m.Schwere, m.Status,
                m.GemeldetVon != null ? m.GemeldetVon.Vorname + " " + m.GemeldetVon.Nachname : null));
}
