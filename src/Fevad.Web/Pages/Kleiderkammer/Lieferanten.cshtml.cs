using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Lieferanten der Kleiderkammer; die Preise je Artikel stehen beim Artikel.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class LieferantenModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Formulardaten des Lieferanten.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Alle Lieferanten mit Zahl der erfassten Preise.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Der Benutzer darf Lieferanten bearbeiten.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Ein Lieferant in der Liste.</summary>
    /// <param name="Lieferant">Der Lieferant.</param>
    /// <param name="Preise">Zahl der Artikel mit Preis bei diesem Lieferanten.</param>
    public sealed record Zeile(Lieferant Lieferant, int Preise);

    /// <summary>Eingabefelder eines Lieferanten.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung; <c>null</c> beim Anlegen.</summary>
        public Guid? Id { get; set; }
        /// <summary>Firmenname.</summary>
        [Required(ErrorMessage = "Name fehlt."), StringLength(200), Display(Name = "Firma")] public string Name { get; set; } = "";
        /// <summary>Kundennummer.</summary>
        [StringLength(50), Display(Name = "Unsere Kundennummer")] public string? Kundennummer { get; set; }
        /// <summary>Telefon der Firma.</summary>
        [StringLength(50), Display(Name = "Telefon")] public string? Telefon { get; set; }
        /// <summary>E-Mail-Adresse der Firma.</summary>
        [StringLength(200), EmailAddress(ErrorMessage = "E-Mail-Adresse ungültig."), Display(Name = "E-Mail")] public string? EMail { get; set; }
        /// <summary>Webseite oder Bestellportal.</summary>
        [StringLength(300), Display(Name = "Webseite")] public string? Webseite { get; set; }
        /// <summary>Freitext.</summary>
        [StringLength(1000), Display(Name = "Bemerkung (keine Ansprechpersonen)")] public string? Bemerkung { get; set; }
        /// <summary>Lieferant wird noch genutzt.</summary>
        [Display(Name = "aktiv")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>Zeigt die Liste; mit <paramref name="id"/> steht der Lieferant im Formular.</summary>
    /// <param name="id">Kennung des zu bearbeitenden Lieferanten.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            if (await db.Lieferanten.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct) is not { } l)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = l.Id, Name = l.Name, Kundennummer = l.Kundennummer, Telefon = l.Telefon, EMail = l.EMail, Webseite = l.Webseite,
                Bemerkung = l.Bemerkung, Aktiv = l.Aktiv,
            };
        }

        await LadenAsync(ct);
        return Page();
    }

    /// <summary>Legt den Lieferanten an oder speichert ihn.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Liste, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var name = Formular.Name.Trim();
        if (await db.Lieferanten.AnyAsync(l => l.Name == name && l.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Name", "Diesen Lieferanten gibt es schon.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        Lieferant? lieferant;
        if (Formular.Id is { } id)
        {
            lieferant = await db.Lieferanten.FirstOrDefaultAsync(l => l.Id == id, ct);
            if (lieferant is null)
            {
                return NotFound();
            }
        }
        else
        {
            lieferant = new Lieferant { Name = name };
            db.Lieferanten.Add(lieferant);
        }

        lieferant.Name = name;
        lieferant.Kundennummer = Leer(Formular.Kundennummer);
        lieferant.Telefon = Leer(Formular.Telefon);
        lieferant.EMail = Leer(Formular.EMail);
        lieferant.Webseite = Leer(Formular.Webseite);
        lieferant.Bemerkung = Leer(Formular.Bemerkung);
        lieferant.Aktiv = Formular.Aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Lieferant „{lieferant.Name}“ gespeichert.";
        return RedirectToPage();
    }

    private async Task<bool> DarfAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded;

    private async Task LadenAsync(CancellationToken ct)
    {
        DarfBearbeiten = await DarfAsync();
        Zeilen = await db.Lieferanten.AsNoTracking()
            .OrderByDescending(l => l.Aktiv).ThenBy(l => l.Name)
            .Select(l => new Zeile(l, l.Preise.Count))
            .ToListAsync(ct);
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
