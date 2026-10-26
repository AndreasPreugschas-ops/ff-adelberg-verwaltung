using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Einsaetze;

/// <summary>
/// Verzeichnis externer Stellen (V20): Polizei, Rettungsdienst, Kreisbrandmeister, THW, Energieversorger, Wasserbehörde, Bauhof, PSNV
/// mit dienstlicher Erreichbarkeit. Im Einsatzbericht lassen sie sich als beteiligte Organisation übernehmen statt jedes Mal frei zu tippen.
/// Lesen mit <see cref="Berechtigungen.EinsaetzeLesen"/>, pflegen mit <see cref="Berechtigungen.EinsaetzeAao"/>.
/// </summary>
[Authorize(Policy = Berechtigungen.EinsaetzeLesen)]
public class ExterneStellenModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Eingabe einer Stelle.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung beim Bearbeiten, sonst null.</summary>
        public Guid? Id { get; set; }
        /// <summary>Art der Stelle.</summary>
        [Display(Name = "Art")] public OrganisationArt Art { get; set; }
        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bitte angeben."), StringLength(200), Display(Name = "Bezeichnung")] public string Bezeichnung { get; set; } = "";
        /// <summary>Telefon.</summary>
        [StringLength(50), Display(Name = "Telefon (dienstlich)")] public string? Telefon { get; set; }
        /// <summary>Mailadresse.</summary>
        [StringLength(200), EmailAddress(ErrorMessage = "Keine gültige Mailadresse."), Display(Name = "Mail (dienstlich)")] public string? Email { get; set; }
        /// <summary>Anschrift.</summary>
        [StringLength(300), Display(Name = "Anschrift")] public string? Anschrift { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
        /// <summary>Wird noch angeboten.</summary>
        [Display(Name = "aktiv")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>Alle Stellen, aktive zuerst, nach Art und Bezeichnung.</summary>
    public IReadOnlyList<ExterneStelle> Stellen { get; private set; } = [];

    /// <summary>Darf pflegen.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Formular für neue oder geänderte Stelle.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Zeigt das Verzeichnis, auf Wunsch mit einer Stelle im Formular.</summary>
    /// <param name="bearbeiten">Kennung der zu bearbeitenden Stelle.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? bearbeiten, CancellationToken ct)
    {
        await LadenAsync(ct);
        if (bearbeiten is { } id && DarfBearbeiten && Stellen.FirstOrDefault(s => s.Id == id) is { } s)
        {
            Formular = new Eingabe
            {
                Id = s.Id, Art = s.Art, Bezeichnung = s.Bezeichnung, Telefon = s.Telefon, Email = s.Email, Anschrift = s.Anschrift,
                Bemerkung = s.Bemerkung, Aktiv = s.Aktiv,
            };
        }

        return Page();
    }

    /// <summary>Legt eine Stelle an oder ändert sie.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern oder 403.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        ExterneStelle stelle;
        if (Formular.Id is { } id)
        {
            if (await db.ExterneStellen.FirstOrDefaultAsync(s => s.Id == id, ct) is not { } vorhanden)
            {
                return NotFound();
            }

            stelle = vorhanden;
        }
        else
        {
            stelle = new ExterneStelle { Bezeichnung = "" };
            db.ExterneStellen.Add(stelle);
        }

        stelle.Art = Formular.Art;
        stelle.Bezeichnung = Formular.Bezeichnung.Trim();
        stelle.Telefon = Leer(Formular.Telefon);
        stelle.Email = Leer(Formular.Email);
        stelle.Anschrift = Leer(Formular.Anschrift);
        stelle.Bemerkung = Leer(Formular.Bemerkung);
        stelle.Aktiv = Formular.Aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"„{stelle.Bezeichnung}“ gespeichert.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeAao)).Succeeded;
        Stellen = (await db.ExterneStellen.AsNoTracking().ToListAsync(ct))
            .OrderByDescending(s => s.Aktiv).ThenBy(s => s.Art).ThenBy(s => s.Bezeichnung, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
