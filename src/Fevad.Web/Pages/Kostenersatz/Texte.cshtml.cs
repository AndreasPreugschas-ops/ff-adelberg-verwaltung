using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kostenersatz;

/// <summary>Textbausteine für Anhörung und Kostenersatzbescheid (V22); pflegt das Hauptamt. Neue Texte gelten ab heute, die alten bleiben erhalten.</summary>
[Authorize(Policy = Berechtigungen.KostenersatzEntscheiden)]
public class TexteModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Höchstlänge eines Textbausteins.</summary>
    public const int MaxLaenge = 2000;

    /// <summary>Die Textbausteine mit Bezeichnung.</summary>
    public static readonly (string Schluessel, string Bezeichnung)[] Bausteine =
    [
        (ParameterSchluessel.KostenersatzTextAnhoerung, "Anschreiben zur Anhörung"),
        (ParameterSchluessel.KostenersatzTextBegruendung, "Begründung des Bescheids"),
        (ParameterSchluessel.KostenersatzTextZahlung, "Zahlungshinweis mit Bankverbindung der Gemeindekasse"),
        (ParameterSchluessel.KostenersatzTextRechtsbehelf, "Rechtsbehelfsbelehrung"),
    ];

    /// <summary>Texte je Parameterschlüssel.</summary>
    [BindProperty]
    public Dictionary<string, string?> Texte { get; set; } = [];

    /// <summary>Lädt die heute gültigen Texte.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        foreach (var (schluessel, _) in Bausteine)
        {
            Texte[schluessel] = await parameter.WertAsync(schluessel, heute, ct);
        }
    }

    /// <summary>Speichert geänderte Texte mit Gültigkeit ab heute.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var geaendert = 0;
        foreach (var (schluessel, bezeichnung) in Bausteine)
        {
            var neu = Texte.GetValueOrDefault(schluessel)?.Trim();
            if (string.IsNullOrEmpty(neu) || neu.Length > MaxLaenge)
            {
                TempData["Fehler"] = $"{bezeichnung}: bitte angeben (höchstens {MaxLaenge} Zeichen).";
                return RedirectToPage();
            }

            if (await parameter.WertAsync(schluessel, heute, ct) == neu)
            {
                continue;
            }

            var vonHeute = await db.Parameter.FirstOrDefaultAsync(p => p.Schluessel == schluessel && p.GueltigAb == heute, ct);
            if (vonHeute is null)
            {
                db.Parameter.Add(new Parameter { Schluessel = schluessel, Wert = neu, GueltigAb = heute, Quelle = "Einstellung in FEVAD" });
            }
            else
            {
                vonHeute.Wert = neu;
            }

            geaendert++;
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = geaendert == 0 ? "Nichts geändert." : "Textbausteine gespeichert, gültig ab heute.";
        return RedirectToPage();
    }

    /// <summary>Die erlaubten Platzhalter als Text.</summary>
    public static string Platzhalter => string.Join(", ", Bescheidtexte.Platzhalter);
}
