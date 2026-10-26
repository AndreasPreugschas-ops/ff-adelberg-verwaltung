using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages;

/// <summary>
/// Kontaktliste für Mitglieder (V42): Mobilnummer und Mail der volljährigen aktiven Mitglieder, die im Einwilligungsregister der Art
/// <see cref="Einwilligungsart.CodeKontaktliste"/> zugestimmt haben. Sehen dürfen sie nur Konten aktiver Mitglieder, keine Elternkonten.
/// Ein Widerruf wirkt sofort, weil die Liste bei jedem Aufruf neu entsteht.
/// </summary>
public class KontaktlisteModel(AppDbContext db, Einwilligungsregister register, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Ein Eintrag der Liste.</summary>
    /// <param name="Name">„Nachname, Vorname“.</param>
    /// <param name="Abteilungen">Aktuelle Abteilungen.</param>
    /// <param name="Mobil">Mobilnummer.</param>
    /// <param name="Email">Mailadresse.</param>
    public sealed record Kontakt(string Name, string Abteilungen, string? Mobil, string? Email);

    /// <summary>Die Einträge nach Namen.</summary>
    public IReadOnlyList<Kontakt> Kontakte { get; private set; } = [];

    /// <summary>Das eigene Mitglied steht in der Liste.</summary>
    public bool SelbstEnthalten { get; private set; }

    /// <summary>Zeigt die Liste.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 403 für Konten ohne aktive Mitgliedschaft.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var eigenesMitglied = benutzer.Id is { } konto
            ? await db.Users.Where(u => u.Id == konto).Select(u => u.MitgliedId).FirstOrDefaultAsync(ct)
            : null;
        var aktiv = Aktiv(heute);
        if (eigenesMitglied is not { } selbst || !await db.Mitglieder.Where(aktiv).AnyAsync(m => m.Id == selbst, ct))
        {
            return Forbid();
        }

        var artId = await db.Einwilligungsarten.Where(a => a.Code == Einwilligungsart.CodeKontaktliste && a.Aktiv).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        if (artId is null)
        {
            return Page();
        }

        var erklaerungen = await db.Einwilligungen.AsNoTracking().Where(e => e.EinwilligungsartId == artId).ToListAsync(ct);
        var zugestimmt = erklaerungen.GroupBy(e => e.MitgliedId)
            .Where(g => Einwilligungsregeln.Aktuell(g) is { Erteilt: true, WiderrufenAm: null })
            .Select(g => g.Key).ToList();
        var volljaehrigAb = (await register.GrenzenAsync(heute, ct)).VolljaehrigAb;
        var mitglieder = await db.Mitglieder.AsNoTracking().Where(aktiv).Where(m => zugestimmt.Contains(m.Id))
            .Select(m => new
            {
                m.Id, m.Nachname, m.Vorname, m.Geburtsdatum, m.TelefonMobil, m.Email,
                Abteilungen = m.Mitgliedschaften.Where(s => !s.Vordienstzeit && s.Status != MitgliedStatus.Ausgeschieden && s.GueltigAb <= heute
                    && (s.GueltigBis == null || s.GueltigBis >= heute)).Select(s => s.Abteilung!.Bezeichnung).ToList(),
            })
            .ToListAsync(ct);
        Kontakte = mitglieder.Where(m => Mitglied.Alter(m.Geburtsdatum, heute) >= volljaehrigAb)
            .OrderBy(m => m.Nachname, StringComparer.CurrentCulture).ThenBy(m => m.Vorname, StringComparer.CurrentCulture)
            .Select(m => new Kontakt($"{m.Nachname}, {m.Vorname}", string.Join(", ", m.Abteilungen.Order()), m.TelefonMobil, m.Email))
            .ToList();
        SelbstEnthalten = mitglieder.Any(m => m.Id == selbst);
        return Page();
    }

    private static System.Linq.Expressions.Expression<Func<Mitglied, bool>> Aktiv(DateOnly heute) =>
        m => m.AnonymisiertAm == null && m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.Status != MitgliedStatus.Ausgeschieden
            && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute));
}
