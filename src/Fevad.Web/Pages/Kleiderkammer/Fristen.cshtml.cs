using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Prüffristen und Lebensdauer aller PSA-Einzelstücke, das Dringendste zuerst.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class FristenModel(Kleiderkammerstand stand, TimeProvider zeit) : PageModel
{
    /// <summary>Inventarnummer, Artikel, Größe oder Träger.</summary>
    [BindProperty(SupportsGet = true)] public string? Suche { get; set; }
    [BindProperty(SupportsGet = true)] public bool NurFaellige { get; set; }
    [BindProperty(SupportsGet = true, Name = "Seite")] public int Seitennummer { get; set; } = 1;

    public Seite<PsaStand> Zeilen { get; private set; } = Seite<PsaStand>.Leer;

    public async Task OnGetAsync(CancellationToken ct)
    {
        var suche = Suche?.Trim();
        Zeilen = (await stand.PsaAsync(Anzeige.Heute(zeit), ct))
            .Where(p => !NurFaellige || p.Stand.Ampel != Fristampel.Gruen)
            .Where(p => string.IsNullOrEmpty(suche) || Trifft(p, suche))
            .OrderByDescending(p => p.Stand.Ampel)
            .ThenBy(p => new[] { p.Stand.NaechsterTermin, p.Stand.AussonderungFaellig }.Where(d => d is not null).DefaultIfEmpty(DateOnly.MaxValue).Min())
            .ThenBy(p => p.Stand.Nummer)
            .ToList()
            .Seite(Seitennummer);
    }

    private static bool Trifft(PsaStand p, string suche) =>
        new[] { p.Stand.Nummer, p.Artikel, p.Groesse, p.Traeger }
            .Any(t => t?.Contains(suche, StringComparison.CurrentCultureIgnoreCase) == true);
}
