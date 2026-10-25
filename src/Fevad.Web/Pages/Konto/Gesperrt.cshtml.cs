using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

/// <summary>Hinweis, dass das Konto nach zu vielen Fehlversuchen vorübergehend gesperrt ist.</summary>
[AllowAnonymous]
public class GesperrtModel : PageModel
{
    /// <summary>Zeigt den Hinweis.</summary>
    public void OnGet()
    {
    }
}
