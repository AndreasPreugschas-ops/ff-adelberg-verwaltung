using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FfAdelberg.Web.Pages.Konto;

[AllowAnonymous]
public class GesperrtModel : PageModel
{
    public void OnGet()
    {
    }
}
