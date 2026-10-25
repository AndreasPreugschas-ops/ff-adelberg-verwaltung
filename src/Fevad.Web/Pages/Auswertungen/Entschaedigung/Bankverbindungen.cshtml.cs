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
    /// <summary>Eine hinterlegte Bankverbindung.</summary>
    /// <param name="MitgliedId">Kennung des Mitglieds.</param>
    /// <param name="Name">Anzeigename „Nachname, Vorname“.</param>
    /// <param name="Kontoinhaber">Abweichender Kontoinhaber.</param>
    /// <param name="Iban">IBAN in Vierergruppen für die Anzeige.</param>
    /// <param name="ErfasstAm">Tag der Erfassung.</param>
    public sealed record Zeile(Guid MitgliedId, string Name, string? Kontoinhaber, string Iban, DateOnly ErfasstAm);

    /// <summary>Hinterlegte Bankverbindungen.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Mitglieder zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Mitglieder { get; private set; } = [];

    /// <summary>Formulardaten der Bankverbindung.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Eingabefelder einer Bankverbindung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Mitglied, dem die Bankverbindung gehört.</summary>
        [Required(ErrorMessage = "Mitglied wählen."), Display(Name = "Mitglied")] public Guid? MitgliedId { get; set; }
        /// <summary>Kontoinhaber, falls nicht das Mitglied selbst.</summary>
        [StringLength(100), Display(Name = "Kontoinhaber, falls abweichend")] public string? Kontoinhaber { get; set; }
        /// <summary>IBAN; Leerzeichen sind erlaubt.</summary>
        [Required(ErrorMessage = "IBAN fehlt."), StringLength(42), Display(Name = "IBAN")] public string Iban { get; set; } = "";
    }

    /// <summary>Zeigt die Liste, auf Wunsch mit der Bankverbindung eines Mitglieds im Formular.</summary>
    /// <param name="mitglied">Mitglied, dessen Bankverbindung bearbeitet wird.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(Guid? mitglied, CancellationToken ct)
    {
        if (mitglied is { } id && await db.Bankverbindungen.AsNoTracking().FirstOrDefaultAsync(b => b.MitgliedId == id, ct) is { } b)
        {
            Formular = new Eingabe { MitgliedId = id, Kontoinhaber = b.Kontoinhaber, Iban = Domain.Entschaedigung.Iban.Formatieren(b.Iban) };
        }

        await LadenAsync(ct);
    }

    /// <summary>Prüft die IBAN und speichert die Bankverbindung des Mitglieds.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder die Seite mit Fehlern.</returns>
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

    /// <summary>Löscht die Bankverbindung des Mitglieds sofort und endgültig.</summary>
    /// <param name="mitgliedId">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Liste.</returns>
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
