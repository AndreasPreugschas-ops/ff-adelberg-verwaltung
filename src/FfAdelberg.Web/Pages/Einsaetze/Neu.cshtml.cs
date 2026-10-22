using System.ComponentModel.DataAnnotations;
using FfAdelberg.Infrastructure.Einsaetze;
using FfAdelberg.Infrastructure.Identitaet;
using FfAdelberg.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FfAdelberg.Web.Pages.Einsaetze;

/// <summary>Bericht von Hand anlegen, z. B. wenn kein FE2-Alarm ankam. Die Einsatznummer vergibt das System.</summary>
[Authorize(Policy = Berechtigungen.EinsaetzeBearbeiten)]
public class NeuModel(Einsatzberichte berichte, TimeProvider zeit) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Alarmzeit fehlt."), Display(Name = "Alarmiert")]
    public string Alarmiert { get; set; } = "";

    public void OnGet() => Alarmiert = ZeitEingabe.Text(zeit.GetUtcNow());

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ZeitEingabe.TryLesen(Alarmiert, null, out var alarmiert) || alarmiert is null)
        {
            ModelState.AddModelError(nameof(Alarmiert), "Bitte als TT.MM.JJJJ HH:MM eingeben.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var einsatz = await berichte.NeuAnlegenAsync(alarmiert!.Value, ct);
        TempData["Meldung"] = $"Einsatz {einsatz.Einsatznummer} angelegt.";
        return RedirectToPage("Bericht", new { id = einsatz.Id });
    }
}
