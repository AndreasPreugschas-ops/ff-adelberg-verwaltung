using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Einsaetze;

/// <summary>Leeres Papierformular des Einsatzberichts für den Ausfall von FEVAD (V18); vorab ausdrucken und in die Fahrzeuge legen.</summary>
[Authorize(Policy = Berechtigungen.EinsaetzeBearbeiten)]
public class PapierformularModel(AppDbContext db) : PageModel
{
    /// <summary>Liefert das Formular als PDF.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var o = await db.OrganisationAsync(ct);
        var kopf = new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp);
        return File(EinsatzberichtPdf.Papierformular(kopf), "application/pdf", "Einsatzbericht-Papierformular.pdf");
    }
}
