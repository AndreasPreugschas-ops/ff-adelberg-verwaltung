using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Personal;

/// <summary>Fristenübersicht der Einsatzabteilung: Lehrgänge mit Ablauf, wiederkehrende Nachweise, Atemschutz.</summary>
public class IndexModel(Personalfristenabfrage fristen, TimeProvider zeit, IAuthorizationService autorisierung) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Fristbereich? Bereich { get; set; }

    /// <summary>Auch gültige Fristen zeigen, nicht nur gelbe und rote.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Alle { get; set; }

    public IReadOnlyList<Fristeintrag> Eintraege { get; private set; } = [];
    public int Abgelaufen { get; private set; }
    public int LaufenAb { get; private set; }
    public bool DarfBearbeiten { get; private set; }
    public bool DarfKataloge { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var gesundheit = await DarfAsync(Berechtigungen.AtemschutzLesen);
        DarfBearbeiten = await DarfAsync(Berechtigungen.AusbildungBearbeiten) || await DarfAsync(Berechtigungen.AusbildungNachweise) || await DarfAsync(Berechtigungen.AtemschutzBearbeiten);
        DarfKataloge = await DarfAsync(Berechtigungen.AusbildungKataloge);

        var sichtbar = (await fristen.LadenAsync(heute, ct: ct))
            .Where(f => gesundheit || !f.Gesundheitsbezogen)
            .ToList();
        Abgelaufen = sichtbar.Count(f => f.Ampel == Fristampel.Rot);
        LaufenAb = sichtbar.Count(f => f.Ampel == Fristampel.Gelb);

        Eintraege = sichtbar
            .Where(f => Bereich is null || f.Bereich == Bereich)
            .Where(f => Alle || f.Ampel != Fristampel.Gruen)
            .OrderByDescending(f => f.Ampel)
            .ThenBy(f => f.FaelligAm ?? DateOnly.MinValue)
            .ThenBy(f => f.Mitglied)
            .ToList();
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;
}
