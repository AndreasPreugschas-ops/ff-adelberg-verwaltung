using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Melden;

/// <summary>Mangel melden: für jedes Mitglied, typischerweise über den QR-Code am Gerät.</summary>
public class IndexModel(AppDbContext db, UserManager<Benutzer> benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Bezeichnung des Geräts zur eingegebenen Inventarnummer.</summary>
    public string? Geraetename { get; private set; }
    /// <summary>Auswahl der Fahrzeuge im Dienst.</summary>
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];

    /// <summary>Formular einer Mangelmeldung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Inventarnummer des Geräts.</summary>
        [StringLength(30), Display(Name = "Inventarnummer (steht am Gerät)")]
        public string? Nummer { get; set; }

        /// <summary>Fahrzeug, wenn kein Gerät angegeben ist.</summary>
        [Display(Name = "oder Fahrzeug")]
        public Guid? FahrzeugId { get; set; }

        /// <summary>Was nicht in Ordnung ist.</summary>
        [Required(ErrorMessage = "Bitte beschreiben, was nicht in Ordnung ist."), StringLength(2000), Display(Name = "Was ist nicht in Ordnung?")]
        public string Beschreibung { get; set; } = "";

        /// <summary>Schwere des Mangels.</summary>
        public MangelSchwere Schwere { get; set; } = MangelSchwere.Einschraenkung;
    }

    /// <summary>Zeigt das Formular, vorbelegt aus dem QR-Code.</summary>
    /// <param name="nummer">Inventarnummer des Geräts.</param>
    /// <param name="fahrzeugId">Kennung des Fahrzeugs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(string? nummer, Guid? fahrzeugId, CancellationToken ct)
    {
        Formular.Nummer = nummer;
        Formular.FahrzeugId = fahrzeugId;
        await AuswahlLadenAsync(ct);
    }

    /// <summary>Speichert die Meldung als offenen Mangel.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var nummer = Formular.Nummer?.Trim();
        Guid? geraetId = null;
        if (!string.IsNullOrEmpty(nummer))
        {
            geraetId = await db.Geraete.Where(g => g.Inventarnummer == nummer && g.AusgesondertAm == null).Select(g => (Guid?)g.Id).FirstOrDefaultAsync(ct);
            if (geraetId is null)
            {
                ModelState.AddModelError("Formular.Nummer", "Kein Gerät mit dieser Inventarnummer.");
            }
        }
        else if (Formular.FahrzeugId is null || !await db.Fahrzeuge.AnyAsync(f => f.Id == Formular.FahrzeugId, ct))
        {
            ModelState.AddModelError("Formular.Nummer", "Bitte Inventarnummer eingeben oder ein Fahrzeug wählen.");
        }

        if (!ModelState.IsValid)
        {
            await AuswahlLadenAsync(ct);
            return Page();
        }

        var konto = await benutzer.GetUserAsync(User);
        db.Maengel.Add(new Mangel
        {
            GeraetId = geraetId,
            FahrzeugId = geraetId is null ? Formular.FahrzeugId : null,
            Beschreibung = Formular.Beschreibung.Trim(),
            Schwere = Formular.Schwere,
            Status = MangelStatus.Offen,
            GemeldetAm = zeit.GetUtcNow(),
            GemeldetVonId = konto?.MitgliedId,
        });
        await db.SaveChangesAsync(ct);

        TempData["Meldung"] = "Danke, der Mangel ist gemeldet. Der Gerätewart kümmert sich darum.";
        return RedirectToPage();
    }

    private async Task AuswahlLadenAsync(CancellationToken ct)
    {
        Fahrzeuge = await db.Fahrzeuge
            .Where(f => f.AusserDienstAm == null)
            .OrderBy(f => f.Funkrufname)
            .Select(f => new SelectListItem(f.Funkrufname, f.Id.ToString()))
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(Formular.Nummer))
        {
            var nummer = Formular.Nummer.Trim();
            Geraetename = await db.Geraete.Where(g => g.Inventarnummer == nummer).Select(g => g.Bezeichnung).FirstOrDefaultAsync(ct);
        }
    }
}
