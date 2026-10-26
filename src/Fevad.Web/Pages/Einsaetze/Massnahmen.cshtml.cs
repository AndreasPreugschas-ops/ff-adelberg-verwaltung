using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Einsaetze;

/// <summary>Offene Maßnahmen aus allen Einsatznachbesprechungen (V17): Vorschläge zur Freigabe und freigegebene Maßnahmen mit Termin.</summary>
[Authorize(Policy = Berechtigungen.EinsaetzeBearbeiten)]
public class MassnahmenModel(Massnahmenliste massnahmen, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Offene Maßnahmen, Vorschläge zuerst.</summary>
    public IReadOnlyList<Massnahme> Offene { get; private set; } = [];
    /// <summary>Heutiges Datum für die Ampel.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Tage vor dem Termin, ab denen gelb gilt.</summary>
    public int WarnTage { get; private set; }

    /// <summary>Lädt die offenen Maßnahmen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        WarnTage = await parameter.ZahlAsync(ParameterSchluessel.MassnahmeWarnTage, Heute, 14, ct);
        Offene = await massnahmen.Offene().ToListAsync(ct);
    }
}
