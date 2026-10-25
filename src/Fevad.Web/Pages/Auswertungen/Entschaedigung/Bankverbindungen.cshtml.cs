using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Auswertungen.Entschaedigung;

/// <summary>
/// Bankverbindungen für Auszahlungen: freiwillig, nur bei Bedarf und nur für Kassier und Kommandant. Entfernen löscht sofort
/// (nicht nur als gelöscht markiert); nach dem Austritt löscht der Löschlauf (L37).
/// </summary>
[Authorize(Policy = Berechtigungen.KasseBankdaten)]
public class BankverbindungenModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    public sealed record Zeile(Guid MitgliedId, string Name, string? Kontoinhaber, string Iban, DateOnly ErfasstAm);

    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Mitglieder { get; private set; } = [];

    [BindProperty] public Eingabe Formular { get; set; } = new();

    public sealed class Eingabe
    {
        [Required(ErrorMessage = "Mitglied wählen."), Display(Name = "Mitglied")] public Guid? MitgliedId { get; set; }
        [StringLength(100), Display(Name = "Kontoinhaber, falls abweichend")] public string? Kontoinhaber { get; set; }
        [Required(ErrorMessage = "IBAN fehlt."), StringLength(42), Display(Name = "IBAN")] public string Iban { get; set; } = "";
    }

    public async Task OnGetAsync(Guid? mitglied, CancellationToken ct)
    {
        if (mitglied is { } id && await db.Bankverbindungen.AsNoTracking().FirstOrDefaultAsync(b => b.MitgliedId == id, ct) is { } b)
        {
            Formular = new Eingabe { MitgliedId = id, Kontoinhaber = b.Kontoinhaber, Iban = Domain.Entschaedigung.Iban.Formatieren(b.Iban) };
        }

        await LadenAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var iban = Domain.Entschaedigung.Iban.Normalisieren(Formular.Iban);
        if (iban is null)
        {
            ModelState.AddModelError("Formular.Iban", "Die IBAN ist ungültig (Länge oder Prüfziffer).");
        }

        var heute = Anzeige.Heute(zeit);
        if (Formular.MitgliedId is { } id && !await AktivAsync(heute).AnyAsync(m => m.Id == id, ct))
        {
            ModelState.AddModelError("Formular.MitgliedId", "Bankverbindungen nur für aktive Mitglieder.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var vorhanden = await db.Bankverbindungen.FirstOrDefaultAsync(b => b.MitgliedId == Formular.MitgliedId, ct);
        if (vorhanden is null)
        {
            db.Bankverbindungen.Add(new Bankverbindung { MitgliedId = Formular.MitgliedId!.Value, Iban = iban!, Kontoinhaber = Leer(Formular.Kontoinhaber), ErfasstAm = heute });
        }
        else
        {
            vorhanden.Iban = iban!;
            vorhanden.Kontoinhaber = Leer(Formular.Kontoinhaber);
            vorhanden.ErfasstAm = heute;
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Bankverbindung gespeichert.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEntfernenAsync(Guid mitgliedId, CancellationToken ct)
    {
        var ids = await db.Bankverbindungen.IgnoreQueryFilters().Where(b => b.MitgliedId == mitgliedId).Select(b => b.Id).ToListAsync(ct);
        await db.Bankverbindungen.IgnoreQueryFilters().Where(b => ids.Contains(b.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<Bankverbindung>(ids, ct);
        TempData["Meldung"] = ids.Count > 0 ? "Bankverbindung gelöscht." : "Keine Bankverbindung vorhanden.";
        return RedirectToPage();
    }

    private IQueryable<Domain.Mitglieder.Mitglied> AktivAsync(DateOnly heute) =>
        db.Mitglieder.AsNoTracking().Where(m => m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)));

    private async Task LadenAsync(CancellationToken ct)
    {
        Zeilen = (await db.Bankverbindungen.AsNoTracking()
                .OrderBy(b => b.Mitglied!.Nachname).ThenBy(b => b.Mitglied!.Vorname)
                .Select(b => new { b.MitgliedId, b.Mitglied!.Nachname, b.Mitglied.Vorname, b.Kontoinhaber, b.Iban, b.ErfasstAm })
                .ToListAsync(ct))
            .Select(b => new Zeile(b.MitgliedId, $"{b.Nachname}, {b.Vorname}", b.Kontoinhaber, Domain.Entschaedigung.Iban.Formatieren(b.Iban), b.ErfasstAm))
            .ToList();
        Mitglieder = await AktivAsync(Anzeige.Heute(zeit)).OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new SelectListItem(m.Nachname + ", " + m.Vorname + " (" + m.Personalnummer + ")", m.Id.ToString()))
            .ToListAsync(ct);
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
