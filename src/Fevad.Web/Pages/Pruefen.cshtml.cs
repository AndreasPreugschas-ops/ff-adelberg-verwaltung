using Fevad.Domain.Entschaedigung;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages;

/// <summary>
/// Öffentliche Prüfseite für Arbeitgeberbescheinigungen (V33, E8): Wer den Prüfcode vom Papier eingibt, sieht die ausgestellten Angaben
/// zum Vergleich. Gezeigt werden nur Vorname und Anfangsbuchstabe des Nachnamens, Arbeitgeber, Zeitraum und Zeiten. Begrenzt je Adresse.
/// </summary>
[AllowAnonymous]
[EnableRateLimiting(Ratenbegrenzung)]
public class PruefenModel(AppDbContext db) : PageModel
{
    /// <summary>Name der Ratenbegrenzung für die Prüfseite.</summary>
    public const string Ratenbegrenzung = "Pruefcode";

    /// <summary>Eingegebener Prüfcode.</summary>
    public string? Code { get; private set; }

    /// <summary>Gefundene Bescheinigung oder null.</summary>
    public Arbeitgeberbescheinigung? Bescheinigung { get; private set; }

    /// <summary>Name der Feuerwehr.</summary>
    public string Feuerwehr { get; private set; } = "";

    /// <summary>Vorname und Anfangsbuchstabe des Nachnamens.</summary>
    public string Person { get; private set; } = "";

    /// <summary>Es wurde ein Code eingegeben, aber keiner gefunden.</summary>
    public bool NichtGefunden { get; private set; }

    /// <summary>Sucht die Bescheinigung zum Prüfcode.</summary>
    /// <param name="code">Prüfcode, Groß- und Kleinschreibung und Bindestriche egal.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(string? code, CancellationToken ct)
    {
        Feuerwehr = (await db.OrganisationAsync(ct)).FeuerwehrName;
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        Code = Arbeitgeberbescheinigung.PruefcodeNormalisieren(code);
        Bescheinigung = Code is null ? null : await db.Arbeitgeberbescheinigungen.AsNoTracking().Include(b => b.Mitglied)
            .FirstOrDefaultAsync(b => b.Pruefcode == Code, ct);
        NichtGefunden = Bescheinigung is null;
        if (Bescheinigung?.Mitglied is { } m)
        {
            Person = $"{m.Vorname} {(m.Nachname.Length > 0 ? m.Nachname[..1] + "." : "")}";
        }
    }
}
