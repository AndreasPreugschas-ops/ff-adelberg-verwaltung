using Fevad.Infrastructure.Haushalt;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Haushalt;

/// <summary>
/// Unterjährige Haushaltsüberwachung (V29): Ansatz, Bindung, Ist und freie Mittel je Haushaltsstelle mit Hinweisen nach §§ 83, 84 GemO
/// und § 21 GemHVO. Das Ist kommt als CSV aus SAP; einspielen dürfen Kommandant, Stellvertreter und die Warte.
/// </summary>
[Authorize(Policy = Berechtigungen.HaushaltLesen)]
public class UeberwachungModel(Haushaltsplanung planung, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Der Überblick.</summary>
    public Haushaltsueberblick Ueberblick { get; private set; } = null!;

    /// <summary>Ob der Nutzer das Ist einspielen darf.</summary>
    public bool DarfEinspielen { get; private set; }

    /// <summary>Datei mit dem Ist-Stand.</summary>
    [BindProperty] public IFormFile? Datei { get; set; }

    /// <summary>Zeigt die Überwachung.</summary>
    /// <param name="jahr">Haushaltsjahr; ohne Angabe das laufende.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(int? jahr, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Ueberblick = await planung.UeberwachungAsync(jahr ?? heute.Year, heute, ct);
        DarfEinspielen = await DarfAsync();
    }

    /// <summary>Spielt den Ist-Stand aus SAP ein und ersetzt den bisherigen Stand des Jahres.</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 403.</returns>
    public async Task<IActionResult> OnPostAsync(int jahr, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        if (Datei is not { Length: > 0 and < 2_000_000 })
        {
            TempData["Fehler"] = "Bitte eine Datei bis 2 MB wählen.";
            return RedirectToPage(new { jahr });
        }

        string inhalt;
        await using (var strom = Datei.OpenReadStream())
        {
            using var leser = new StreamReader(strom, detectEncodingFromByteOrderMarks: true);
            inhalt = await leser.ReadToEndAsync(ct);
        }

        var e = await planung.IstEinspielenAsync(jahr, inhalt, Anzeige.Heute(zeit), ct);
        if (e.Zeilen > 0)
        {
            TempData["Meldung"] = $"Ist-Stand eingespielt: {e.Zeilen} Zeilen.";
        }

        if (e.Fehler.Count > 0)
        {
            TempData["Fehler"] = string.Join(" ", e.Fehler.Take(10)) + (e.Fehler.Count > 10 ? $" (und {e.Fehler.Count - 10} weitere)" : "");
        }

        return RedirectToPage(new { jahr });
    }

    private async Task<bool> DarfAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.HaushaltAlleBereiche)).Succeeded;
}
