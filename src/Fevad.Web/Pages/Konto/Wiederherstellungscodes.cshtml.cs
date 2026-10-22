using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

/// <summary>Zeigt frisch erzeugte Wiederherstellungscodes genau einmal (über TempData).</summary>
public class WiederherstellungscodesModel : PageModel
{
    public const string TempDataSchluessel = "Wiederherstellungscodes";

    public IReadOnlyList<string> Codes { get; private set; } = [];

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
