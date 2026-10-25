using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages;

/// <summary>Fehlerseite für unbehandelte Ausnahmen.</summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
[AllowAnonymous]
public class ErrorModel : PageModel
{
    /// <summary>Kennung der Anfrage zur Suche im Protokoll.</summary>
    public string? RequestId { get; set; }

    /// <summary>Die Kennung der Anfrage wird angezeigt.</summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>Übernimmt die Kennung der aktuellen Anfrage.</summary>
    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}

