using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

/// <summary>Zeigt frisch erzeugte Wiederherstellungscodes genau einmal (über TempData).</summary>
public class WiederherstellungscodesModel : PageModel
{
    /// <summary>TempData-Schlüssel, unter dem die Codes übergeben werden.</summary>
    public const string TempDataSchluessel = "Wiederherstellungscodes";

    /// <summary>Die neuen Codes.</summary>
    public IReadOnlyList<string> Codes { get; private set; } = [];

    /// <summary>Zeigt die Codes einmal.</summary>
    /// <returns>Die Seite oder Weiterleitung zum Konto, wenn keine Codes vorliegen.</returns>
    public IActionResult OnGet()
    {
        if (TempData[TempDataSchluessel] is not string codes || codes.Length == 0)
        {
            return RedirectToPage("Index");
        }

        Codes = codes.Split(';');
        return Page();
    }
}
