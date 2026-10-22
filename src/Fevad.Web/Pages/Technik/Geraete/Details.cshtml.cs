using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraete;

public class DetailsModel(AppDbContext db, Pruefstaende pruefstaende, Geraetecode geraetecode, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    public Geraet Geraet { get; private set; } = null!;
    public Pruefstand Stand { get; private set; } = null!;
    public IReadOnlyList<PruefbuchZeile> Pruefbuch { get; private set; } = [];
    public IReadOnlyList<MangelZeile> Maengel { get; private set; } = [];
    public string QrCodeSvg { get; private set; } = "";
    public string QrAdresse { get; private set; } = "";

    [BindProperty, Required(ErrorMessage = "Grund fehlt."), StringLength(200), Display(Name = "Grund der Aussonderung")]
    public string? Aussonderungsgrund { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await LadenAsync(id, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAussondernAsync(Guid id, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var geraet = await db.Geraete.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (geraet is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(id, ct);
            return Page();
        }

        geraet.AusgesondertAm ??= Anzeige.Heute(zeit);
        geraet.Aussonderungsgrund = Aussonderungsgrund!.Trim();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{geraet.Inventarnummer} ist ausgesondert.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var geraet = await db.Geraete.AsNoTracking()
            .Include(g => g.Geraeteart)
            .Include(g => g.Fahrzeug)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (geraet is null)
        {
            return false;
        }

        Geraet = geraet;
        Stand = (await pruefstaende.GeraeteAsync(Anzeige.Heute(zeit), geraetId: id, ct: ct)).Single();
        Pruefbuch = await Ansichten.PruefbuchAsync(db, id, null, ct);
        Maengel = await Ansichten.Maengel(db.Maengel.Where(m => m.GeraetId == id)).ToListAsync(ct);
        QrAdresse = geraetecode.Adresse(Request, geraet.Inventarnummer);
        QrCodeSvg = Geraetecode.Svg(QrAdresse);
        return true;
    }
}
