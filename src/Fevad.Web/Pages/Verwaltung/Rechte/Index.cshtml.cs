using System.ComponentModel.DataAnnotations;
using System.Text;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Verwaltung.Rechte;

/// <summary>Rollen mit Zahl der Konten und Rechte als Übersicht; neue Rollen anlegen, wahlweise mit den Rechten einer Vorlage.</summary>
public class IndexModel(AppDbContext db, RoleManager<IdentityRole<Guid>> rollen, Rechteverzeichnis rechte) : PageModel
{
    /// <summary>Eine Rolle in der Übersicht.</summary>
    /// <param name="Name">Technischer Name der Rolle.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Beschreibung">Wofür die Rolle gedacht ist.</param>
    /// <param name="Systemrolle">Mitgelieferte Rolle, nicht löschbar.</param>
    /// <param name="Konten">Anzahl der Konten.</param>
    /// <param name="Rechte">Je Recht „✓“ für alle Abteilungen oder die Kürzel der Abteilungen.</param>
    public sealed record Zeile(string Name, string Bezeichnung, string? Beschreibung, bool Systemrolle, int Konten, IReadOnlyDictionary<string, string> Rechte);

    /// <summary>Die Rollen.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Rollen, deren Rechte eine neue Rolle übernehmen kann.</summary>
    public IReadOnlyList<SelectListItem> Vorlagen { get; private set; } = [];

    /// <summary>Bezeichnung der neuen Rolle.</summary>
    [BindProperty, Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")]
    public string? Bezeichnung { get; set; }

    /// <summary>Beschreibung der neuen Rolle.</summary>
    [BindProperty, StringLength(500), Display(Name = "Wofür")]
    public string? Beschreibung { get; set; }

    /// <summary>Rolle, deren Rechte übernommen werden.</summary>
    [BindProperty, Display(Name = "Rechte übernehmen von")]
    public string? Vorlage { get; set; }

    /// <summary>Lädt die Rollen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    /// <summary>Legt eine neue Rolle an, auf Wunsch mit den Rechten einer Vorlage.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur neuen Rolle oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var bezeichnung = Bezeichnung!.Trim();
        if (await db.Rollenprofile.AnyAsync(p => p.Bezeichnung == bezeichnung, ct))
        {
            ModelState.AddModelError(nameof(Bezeichnung), "Eine Rolle mit dieser Bezeichnung gibt es schon.");
            await LadenAsync(ct);
            return Page();
        }

        var name = await FreierNameAsync(bezeichnung);
        var ergebnis = await rollen.CreateAsync(new IdentityRole<Guid>(name) { Id = Guid.CreateVersion7() });
        if (!ergebnis.Succeeded)
        {
            ModelState.AddModelError(nameof(Bezeichnung), string.Join(" ", ergebnis.Errors.Select(e => e.Description)));
            await LadenAsync(ct);
            return Page();
        }

        db.Rollenprofile.Add(new Rollenprofil { Name = name, Bezeichnung = bezeichnung, Beschreibung = string.IsNullOrWhiteSpace(Beschreibung) ? null : Beschreibung.Trim() });
        if (!string.IsNullOrEmpty(Vorlage))
        {
            foreach (var r in await db.RollenRechte.AsNoTracking().Where(r => r.Rolle == Vorlage).ToListAsync(ct))
            {
                db.RollenRechte.Add(new RolleRecht { Rolle = name, Recht = r.Recht, Abteilungen = [.. r.Abteilungen] });
            }
        }

        await db.SaveChangesAsync(ct);
        rechte.Verwerfen();
        TempData["Meldung"] = $"Rolle „{bezeichnung}“ angelegt. Jetzt die Rechte prüfen.";
        return RedirectToPage("Bearbeiten", new { name });
    }

    /// <summary>Technischer Name ohne Umlaute und Leerzeichen, z. B. „Leitung Kinderfeuerwehr“ → LeitungKinderfeuerwehr.</summary>
    private async Task<string> FreierNameAsync(string bezeichnung)
    {
        var text = new StringBuilder();
        foreach (var teil in bezeichnung.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe")
                     .Replace("Ü", "Ue").Replace("ß", "ss").Split([' ', '-', '_', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            var sauber = new string(teil.Where(char.IsAsciiLetterOrDigit).ToArray());
            if (sauber.Length > 0)
            {
                text.Append(char.ToUpperInvariant(sauber[0])).Append(sauber[1..]);
            }
        }

        var basis = text.Length == 0 ? "Rolle" : text.ToString()[..Math.Min(text.Length, 56)];
        var name = basis;
        for (var i = 2; await rollen.RoleExistsAsync(name); i++)
        {
            name = $"{basis}{i}";
        }

        return name;
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        var profile = await db.Rollenprofile.AsNoTracking().ToListAsync(ct);
        var konten = await (from ur in db.UserRoles
                            join r in db.Roles on ur.RoleId equals r.Id
                            join b in db.Users on ur.UserId equals b.Id
                            where b.DeaktiviertAm == null && b.AnonymisiertAm == null
                            group ur by r.Name into g
                            select new { Name = g.Key!, Anzahl = g.Count() })
            .ToDictionaryAsync(x => x.Name, x => x.Anzahl, ct);
        var kuerzel = await db.Abteilungen.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Code, ct);
        var rechteJeRolle = (await db.RollenRechte.AsNoTracking().ToListAsync(ct)).ToLookup(r => r.Rolle);

        Zeilen = profile
            .OrderBy(p => p.Systemrolle ? Array.IndexOf(Rollen.Alle, p.Name) : int.MaxValue).ThenBy(p => p.Bezeichnung)
            .Select(p => new Zeile(p.Name, p.Bezeichnung, p.Beschreibung, p.Systemrolle, konten.GetValueOrDefault(p.Name),
                rechteJeRolle[p.Name].ToDictionary(r => r.Recht,
                    r => r.Abteilungen.Count == 0 ? "✓" : string.Join(", ", r.Abteilungen.Select(a => kuerzel.GetValueOrDefault(a, "?"))))))
            .ToList();
        Vorlagen = Zeilen.Select(z => new SelectListItem(z.Bezeichnung, z.Name)).ToList();
    }
}
