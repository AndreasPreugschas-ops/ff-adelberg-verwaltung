using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Inventur;

[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class ErfassenModel(AppDbContext db, UserManager<Benutzer> benutzer, TimeProvider zeit) : PageModel
{
    public Domain.Technik.Inventur Inventur { get; private set; } = null!;
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public bool NurOffene { get; set; }

    public sealed record Zeile(Guid PositionId, Guid GeraetId, string Inventarnummer, string Bezeichnung, string? SollStandort, InventurBefund? Befund, string? Bemerkung, bool Abgeschlossen);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await LadenAsync(id, ct) ? Page() : NotFound();

    /// <summary>Inventarnummer eingeben oder scannen: zählt als „vorhanden“.</summary>
    public async Task<IActionResult> OnPostScannenAsync(Guid id, string? nummer, CancellationToken ct)
    {
        nummer = nummer?.Trim();
        var position = await db.InventurPositionen
            .Where(p => p.InventurId == id && p.Inventur!.AbgeschlossenAm == null && p.Geraet!.Inventarnummer == nummer)
            .FirstOrDefaultAsync(ct);
        if (position is null)
        {
            TempData["Fehler"] = $"„{nummer}“ gehört nicht zu dieser Inventur.";
        }
        else if (await BefundSetzenAsync(position, InventurBefund.Vorhanden, position.Bemerkung, ct))
        {
            TempData["Meldung"] = $"{nummer} vorhanden.";
        }
        else
        {
            TempData["Fehler"] = Abgeschlossen;
        }

        return RedirectToPage(new { id, NurOffene });
    }

    /// <summary>Befund einer Zeile; per HTMX kommt nur die Zeile zurück.</summary>
    public async Task<IActionResult> OnPostBefundAsync(Guid id, Guid positionId, InventurBefund befund, string? bemerkung, CancellationToken ct)
    {
        var position = await db.InventurPositionen
            .FirstOrDefaultAsync(p => p.Id == positionId && p.InventurId == id && p.Inventur!.AbgeschlossenAm == null, ct);
        if (position is null)
        {
            return NotFound();
        }

        // Ein geleertes Bemerkungsfeld löscht die Bemerkung.
        if (!await BefundSetzenAsync(position, befund, bemerkung, ct))
        {
            if (Request.Headers.ContainsKey("HX-Request"))
            {
                Response.Headers["HX-Refresh"] = "true";
                return new EmptyResult();
            }

            TempData["Fehler"] = Abgeschlossen;
            return RedirectToPage(new { id, NurOffene });
        }

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            var zeile = await ZeilenAbfrage(id, positionId).SingleAsync(ct);
            return Partial("_Zeile", zeile);
        }

        return RedirectToPage(new { id, NurOffene });
    }

    /// <summary>Abschluss: beschädigte Geräte werden als Mangel gemeldet, danach ist nichts mehr änderbar.</summary>
    public async Task<IActionResult> OnPostAbschliessenAsync(Guid id, CancellationToken ct)
    {
        var inventur = await db.Inventuren.Include(i => i.Positionen).FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inventur is null)
        {
            return NotFound();
        }

        if (inventur.Abgeschlossen)
        {
            return RedirectToPage(new { id });
        }

        var jetzt = zeit.GetUtcNow();
        var mitglied = (await benutzer.GetUserAsync(User))?.MitgliedId;
        foreach (var p in inventur.Positionen.Where(p => p.Befund == InventurBefund.Beschaedigt))
        {
            db.Maengel.Add(new Mangel
            {
                GeraetId = p.GeraetId,
                Beschreibung = $"Bei der {inventur.Bezeichnung} als beschädigt erfasst" + (p.Bemerkung is null ? "." : $": {p.Bemerkung}"),
                Schwere = MangelSchwere.Einschraenkung,
                Status = MangelStatus.Offen,
                GemeldetAm = jetzt,
                GemeldetVonId = mitglied,
            });
        }

        inventur.AbgeschlossenAm = jetzt;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Fehler"] = "Während des Abschlusses wurde noch ein Befund erfasst. Bitte prüfen und erneut abschließen.";
            return RedirectToPage(new { id });
        }

        TempData["Meldung"] = "Inventur abgeschlossen.";
        return RedirectToPage(new { id });
    }

    private const string Abgeschlossen = "Die Inventur wurde inzwischen abgeschlossen. Der Befund wurde nicht gespeichert.";

    /// <summary>
    /// Speichert den Befund und erhöht dabei xmin der Inventur. So schlägt entweder der Befund oder der
    /// gleichzeitige Abschluss fehl, und die beim Abschluss erzeugten Mängel passen immer zu den Befunden.
    /// </summary>
    private async Task<bool> BefundSetzenAsync(InventurPosition position, InventurBefund befund, string? bemerkung, CancellationToken ct)
    {
        var inventur = await db.Inventuren.FirstAsync(i => i.Id == position.InventurId, ct);
        if (inventur.Abgeschlossen)
        {
            return false;
        }

        position.Befund = befund;
        position.Bemerkung = string.IsNullOrWhiteSpace(bemerkung) ? null : bemerkung.Trim();
        position.ErfasstAm = zeit.GetUtcNow();
        position.ErfasstVon = (await benutzer.GetUserAsync(User))?.Id;
        db.Entry(inventur).State = EntityState.Modified;
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private IQueryable<Zeile> ZeilenAbfrage(Guid id, Guid? positionId = null, bool nurOffene = false) =>
        db.InventurPositionen.AsNoTracking()
            .Where(p => p.InventurId == id && (positionId == null || p.Id == positionId) && (!nurOffene || p.Befund == null))
            .OrderBy(p => p.SollStandort).ThenBy(p => p.Geraet!.Inventarnummer)
            .Select(p => new Zeile(p.Id, p.GeraetId, p.Geraet!.Inventarnummer, p.Geraet.Bezeichnung, p.SollStandort, p.Befund, p.Bemerkung, p.Inventur!.AbgeschlossenAm != null));

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var inventur = await db.Inventuren.AsNoTracking().Include(i => i.Inventurgruppe).FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inventur is null)
        {
            return false;
        }

        Inventur = inventur;
        Zeilen = await ZeilenAbfrage(id, nurOffene: NurOffene).ToListAsync(ct);
        Gesamt = await db.InventurPositionen.CountAsync(p => p.InventurId == id, ct);
        Erfasst = await db.InventurPositionen.CountAsync(p => p.InventurId == id && p.Befund != null, ct);
        return true;
    }

    public int Gesamt { get; private set; }
    public int Erfasst { get; private set; }
}
