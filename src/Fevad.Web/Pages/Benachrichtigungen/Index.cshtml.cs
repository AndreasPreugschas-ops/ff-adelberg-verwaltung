using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Benachrichtigungen;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Benachrichtigungen;

/// <summary>
/// Eigene Benachrichtigungen (V72): neueste zuerst, als gelesen markieren, öffnen. Jedes angemeldete Konto sieht nur seine eigenen;
/// das Ziel des Links prüft seine Rechte selbst.
/// </summary>
public class IndexModel(Benachrichtigungsdienst dienst, IAktuellerBenutzer benutzer) : PageModel
{
    /// <summary>Höchstzahl der angezeigten Benachrichtigungen.</summary>
    public const int Anzahl = 100;

    /// <summary>Die angezeigten Benachrichtigungen.</summary>
    public IReadOnlyList<Benachrichtigung> Liste { get; private set; } = [];

    /// <summary>Nur ungelesene zeigen.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Ungelesen { get; set; }

    /// <summary>Zeigt die Benachrichtigungen des Kontos.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (benutzer.Id is not { } konto)
        {
            return Challenge();
        }

        Liste = await dienst.ListeAsync(konto, Ungelesen, Anzahl, ct);
        return Page();
    }

    /// <summary>Öffnet eine Benachrichtigung: vermerkt sie als gelesen und springt zu ihrem Ziel in FEVAD.</summary>
    /// <param name="id">Die Benachrichtigung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Ziel oder zurück zur Liste.</returns>
    public async Task<IActionResult> OnPostOeffnenAsync(Guid id, CancellationToken ct)
    {
        if (benutzer.Id is not { } konto)
        {
            return Challenge();
        }

        var b = await dienst.GelesenAsync(konto, id, ct);
        return b is { Link: { } link } && Benachrichtigung.IstInternerLink(link) ? LocalRedirect(link) : RedirectToPage();
    }

    /// <summary>Vermerkt eine Benachrichtigung als gelesen.</summary>
    /// <param name="id">Die Benachrichtigung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Liste.</returns>
    public async Task<IActionResult> OnPostGelesenAsync(Guid id, CancellationToken ct)
    {
        if (benutzer.Id is not { } konto)
        {
            return Challenge();
        }

        await dienst.GelesenAsync(konto, id, ct);
        return RedirectToPage(new { Ungelesen });
    }

    /// <summary>Vermerkt alle Benachrichtigungen des Kontos als gelesen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Liste mit Meldung.</returns>
    public async Task<IActionResult> OnPostAlleGelesenAsync(CancellationToken ct)
    {
        if (benutzer.Id is not { } konto)
        {
            return Challenge();
        }

        var n = await dienst.AlleGelesenAsync(konto, ct);
        TempData["Meldung"] = n == 0 ? "Es gab nichts Ungelesenes." : $"{n} Benachrichtigung(en) als gelesen markiert.";
        return RedirectToPage();
    }
}
