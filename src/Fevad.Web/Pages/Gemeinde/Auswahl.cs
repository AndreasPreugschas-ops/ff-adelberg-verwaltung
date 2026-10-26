using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gemeinde;

/// <summary>Mitgliederauswahl für die Gemeinde-Seiten; Angehörige nur der Jugend- oder Kinderfeuerwehr erscheinen nie (Jugenddaten bleiben abgeschirmt).</summary>
public static class Auswahl
{
    /// <summary>Mitglieder mit einer laufenden Mitgliedschaft außerhalb von Jugend- und Kinderfeuerwehr, dazu ein bereits gewähltes.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="heute">Stichtag.</param>
    /// <param name="gewaehlt">Bereits gewähltes Mitglied, das in der Liste bleiben soll.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Auswahlliste „Nachname, Vorname (Personalnummer)“.</returns>
    public static async Task<IReadOnlyList<SelectListItem>> MitgliederAsync(AppDbContext db, DateOnly heute, Guid? gewaehlt, CancellationToken ct) =>
        await db.Mitglieder.AsNoTracking()
            .Where(m => m.Id == gewaehlt || m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && s.Abteilung!.Art != AbteilungArt.Jugend && s.Abteilung.Art != AbteilungArt.Kinder))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new SelectListItem(m.Nachname + ", " + m.Vorname + " (" + m.Personalnummer + ")", m.Id.ToString()))
            .ToListAsync(ct);

    /// <summary>Prüft, ob ein Mitglied für die Gemeinde-Seiten wählbar ist.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="heute">Stichtag.</param>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="bisher">Bisher gewähltes Mitglied, das wählbar bleibt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>true, wenn wählbar.</returns>
    public static async Task<bool> WaehlbarAsync(AppDbContext db, DateOnly heute, Guid id, Guid? bisher, CancellationToken ct) =>
        id == bisher || await db.Mitglieder.AnyAsync(m => m.Id == id && m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GueltigAb <= heute
            && (s.GueltigBis == null || s.GueltigBis >= heute) && s.Abteilung!.Art != AbteilungArt.Jugend && s.Abteilung.Art != AbteilungArt.Kinder), ct);
}
