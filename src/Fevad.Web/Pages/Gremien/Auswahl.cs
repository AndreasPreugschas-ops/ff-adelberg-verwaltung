using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gremien;

/// <summary>Auswahllisten für die Gremienseiten.</summary>
public static class Auswahl
{
    public sealed record Person(Guid Id, string Name, bool Einsatzabteilung);

    /// <summary>Mitglieder mit laufender Mitgliedschaft; Einsatzabteilung markiert (Wahlrecht).</summary>
    public static async Task<IReadOnlyList<Person>> AktiveMitgliederAsync(AppDbContext db, CancellationToken ct) =>
        await db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s => s.GueltigBis == null && s.Status != MitgliedStatus.Ausgeschieden))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new Person(m.Id, m.Nachname + ", " + m.Vorname,
                m.Mitgliedschaften.Any(s => s.GueltigBis == null && s.Status != MitgliedStatus.Ausgeschieden && s.Abteilung!.Art == AbteilungArt.Einsatz)))
            .ToListAsync(ct);

    public static IReadOnlyList<SelectListItem> Liste(IEnumerable<Person> personen) =>
        personen.Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToList();
}
