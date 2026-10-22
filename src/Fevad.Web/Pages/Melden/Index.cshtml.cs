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
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public string? Geraetename { get; private set; }
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];

    public sealed class Eingabe
    {
        [StringLength(30), Display(Name = "Inventarnummer (steht am Gerät)")]
        public string? Nummer { get; set; }

        [Display(Name = "oder Fahrzeug")]
        public Guid? FahrzeugId { get; set; }

        [Required(ErrorMessage = "Bitte beschreiben, was nicht in Ordnung ist."), StringLength(2000), Display(Name = "Was ist nicht in Ordnung?")]
        public string Beschreibung { get; set; } = "";

        public MangelSchwere Schwere { get; set; } = MangelSchwere.Einschraenkung;
    }

    public async Task OnGetAsync(string? nummer, Guid? fahrzeugId, CancellationToken ct)
    {
        Formular.Nummer = nummer;
        Formular.FahrzeugId = fahrzeugId;
        await AuswahlLadenAsync(ct);
    }

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
