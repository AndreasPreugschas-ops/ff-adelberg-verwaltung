using Fevad.Domain.Entschaedigung;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Auswertungen.Entschaedigung;

public class AbrechnungModel(
    AppDbContext db, Abrechnungen abrechnungen, AbrechnungExport export, IAuthorizationService autorisierung, IAktuellerBenutzer aktuellerBenutzer)
    : PageModel
{
    public AbrechnungExport.Daten Daten { get; private set; } = null!;
    public Abrechnung Abrechnung => Daten.Abrechnung;
    public string? AbgeschlossenVon { get; private set; }
    public bool DarfAbrechnen { get; private set; }
    public bool DarfAbschliessen { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Abrechnungen.AnyAsync(a => a.Id == id, ct))
        {
            return NotFound();
        }

        Daten = await export.LadenAsync(id, ct);
        DarfAbrechnen = await DarfAsync(Berechtigungen.EntschaedigungAbrechnen);
        DarfAbschliessen = await DarfAsync(Berechtigungen.EntschaedigungAbschliessen);
        if (Abrechnung.AbgeschlossenVon is { } benutzer)
        {
            AbgeschlossenVon = await db.Users.Where(u => u.Id == benutzer).Select(u => u.UserName).SingleOrDefaultAsync(ct);
        }

        return Page();
    }

    /// <summary>Exporte: <c>summen</c> und <c>posten</c> als CSV, <c>excel</c> mit beiden Blättern.</summary>
    public async Task<IActionResult> OnGetExportAsync(Guid id, string format, CancellationToken ct)
    {
        if (!await db.Abrechnungen.AnyAsync(a => a.Id == id, ct))
        {
            return NotFound();
        }

        var daten = await export.LadenAsync(id, ct);
        return format switch
        {
            "summen" => File(AbrechnungExport.SummenCsv(daten), "text/csv; charset=utf-8", AbrechnungExport.Dateiname(daten.Abrechnung, "csv").Replace("Entschaedigung_", "Auszahlung_")),
            "posten" => File(AbrechnungExport.PostenCsv(daten), "text/csv; charset=utf-8", AbrechnungExport.Dateiname(daten.Abrechnung, "csv")),
            "excel" => File(AbrechnungExport.Excel(daten), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", AbrechnungExport.Dateiname(daten.Abrechnung, "xlsx")),
            _ => BadRequest(),
        };
    }

    public Task<IActionResult> OnPostNeuBerechnenAsync(Guid id, CancellationToken ct) =>
        AusfuehrenAsync(id, Berechtigungen.EntschaedigungAbrechnen, () => abrechnungen.NeuBerechnenAsync(id, ct), "Neu berechnet.");

    public Task<IActionResult> OnPostAbschliessenAsync(Guid id, CancellationToken ct) =>
        AusfuehrenAsync(id, Berechtigungen.EntschaedigungAbschliessen, () => abrechnungen.AbschliessenAsync(id, aktuellerBenutzer.Id ?? Guid.Empty, ct),
            "Abrechnung abgeschlossen. Sie kann jetzt an die Gemeindekasse gehen.");

    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, CancellationToken ct)
    {
        var ergebnis = await AusfuehrenAsync(id, Berechtigungen.EntschaedigungAbrechnen, () => abrechnungen.LoeschenAsync(id, ct), "Entwurf gelöscht.");
        return ergebnis is RedirectToPageResult && TempData.Peek("Fehler") is null ? RedirectToPage("Index") : ergebnis;
    }

    private async Task<IActionResult> AusfuehrenAsync(Guid id, string policy, Func<Task> aktion, string meldung)
    {
        if (!await DarfAsync(policy))
        {
            return Forbid();
        }

        if (!await db.Abrechnungen.AnyAsync(a => a.Id == id))
        {
            return NotFound();
        }

        try
        {
            await aktion();
            TempData["Meldung"] = meldung;
        }
        catch (InvalidOperationException fehler)
        {
            TempData["Fehler"] = fehler.Message;
        }

        return RedirectToPage(new { id });
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;
}
