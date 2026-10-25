using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Atemschutz;
using Fevad.Infrastructure.Atemschutz;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Selbstmeldung „heute bzw. vorübergehend nicht einsetzbar“ für aktive Atemschutzgeräteträger, ohne Grund (V62).
/// Sofort wirksam; was länger als die Höchstdauer ist, genehmigen Kommando oder Atemschutzwart.
/// </summary>
public class AtemschutzModel(AppDbContext db, Atemschutzabfrage abfrage, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Eigene Abmeldungen, jüngste zuerst.</summary>
    public IReadOnlyList<AtemschutzSperre> Sperren { get; private set; } = [];
    /// <summary>Höchstdauer ohne Genehmigung.</summary>
    public int HoechstTage { get; private set; }
    /// <summary>Heutiges Datum in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Formular „heute bzw. bis … nicht einsetzbar“.</summary>
    public sealed class Eingabe
    {
        [Required(ErrorMessage = "Beginn fehlt.")] public DateOnly? Von { get; set; }
        [Required(ErrorMessage = "Ende fehlt.")] public DateOnly? Bis { get; set; }
    }

    /// <summary>Zeigt das Formular; nur für gekennzeichnete Geräteträger.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (await MitgliedIdAsync(ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        Heute = Anzeige.Heute(zeit);
        HoechstTage = await abfrage.HoechstTageAsync(Heute, ct);
        Sperren = await db.AtemschutzSperren.AsNoTracking()
            .Where(s => s.MitgliedId == mitgliedId && s.AufgehobenAm == null && s.Bis >= Heute)
            .OrderBy(s => s.Von)
            .ToListAsync(ct);
        return Page();
    }

    /// <summary>Legt die Selbstmeldung an; sie ist sofort wirksam.</summary>
    public async Task<IActionResult> OnPostAsync(Eingabe eingabe, CancellationToken ct)
    {
        if (await MitgliedIdAsync(ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        var heute = Anzeige.Heute(zeit);
        if (!ModelState.IsValid || eingabe.Bis < eingabe.Von || eingabe.Bis < heute)
        {
            TempData["Fehler"] = eingabe.Bis < eingabe.Von ? "Das Ende liegt vor dem Beginn." : "Bitte Beginn und Ende angeben; das Ende darf nicht in der Vergangenheit liegen.";
            return RedirectToPage();
        }

        var sperre = new AtemschutzSperre
        {
            MitgliedId = mitgliedId, Von = eingabe.Von!.Value < heute ? heute : eingabe.Von.Value, Bis = eingabe.Bis!.Value,
            Selbstmeldung = true, GemeldetAm = zeit.GetUtcNow(), GemeldetVon = benutzer.Id,
        };
        db.AtemschutzSperren.Add(sperre);
        await db.SaveChangesAsync(ct);

        var hoechst = await abfrage.HoechstTageAsync(heute, ct);
        TempData["Meldung"] = sperre.GenehmigungOffen(hoechst)
            ? $"Gemeldet. Du giltst ab sofort als nicht einsetzbar. Länger als {hoechst} Tage muss das Kommando oder der Atemschutzwart genehmigen."
            : $"Gemeldet: nicht einsetzbar vom {Anzeige.Datum(sperre.Von)} bis {Anzeige.Datum(sperre.Bis)}.";
        return RedirectToPage();
    }

    /// <summary>Eigene Selbstmeldung vorzeitig beenden; Abmeldungen durch Kommando oder Atemschutzwart hebt nur das Kommando auf.</summary>
    public async Task<IActionResult> OnPostAufhebenAsync(Guid sperreId, CancellationToken ct)
    {
        if (await MitgliedIdAsync(ct) is not { } mitgliedId)
        {
            return NotFound();
        }

        var sperre = await db.AtemschutzSperren.FirstOrDefaultAsync(s => s.Id == sperreId && s.MitgliedId == mitgliedId && s.Selbstmeldung && s.AufgehobenAm == null, ct);
        if (sperre is null)
        {
            return NotFound();
        }

        sperre.AufgehobenAm = zeit.GetUtcNow();
        sperre.AufgehobenVon = benutzer.Id;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Abmeldung beendet.";
        return RedirectToPage();
    }

    private async Task<Guid?> MitgliedIdAsync(CancellationToken ct) =>
        benutzer.Id is { } id
            ? await db.Users.Where(u => u.Id == id && u.MitgliedId != null && db.Mitglieder.Any(m => m.Id == u.MitgliedId && m.AtemschutzAktiv))
                .Select(u => u.MitgliedId).FirstOrDefaultAsync(ct)
            : null;
}
