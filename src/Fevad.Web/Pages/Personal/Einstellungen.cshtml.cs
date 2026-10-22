using System.Globalization;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>
/// Intervalle und Warnfristen der Personalverwaltung. Eine Änderung legt einen neuen Parameterwert ab heute an;
/// der alte bleibt mit seinem Gültigkeitsbeginn erhalten (nachvollziehbar, wer wann was galt).
/// </summary>
[Authorize(Policy = Berechtigungen.AusbildungKataloge)]
public class EinstellungenModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    public sealed record Einstellung(string Gruppe, string Schluessel, string Bezeichnung, string Einheit, int Standard, int Min, int Max, string? Hinweis = null);

    public static IReadOnlyList<Einstellung> Alle { get; } =
    [
        new("Atemschutz", ParameterSchluessel.BelastungsuebungIntervallMonate, "Belastungsübung (Übungsanlage)", "Monate", 12, 1, 60, "FwDV 7: jährlich"),
        new("Atemschutz", ParameterSchluessel.EinsatzuebungIntervallMonate, "Einsatzübung", "Monate", 12, 1, 60, "FwDV 7: jährlich"),
        new("Atemschutz", ParameterSchluessel.AtemschutzUnterweisungIntervallMonate, "Atemschutz-Unterweisung", "Monate", 12, 1, 60, "FwDV 7: jährlich"),
        new("Atemschutz", ParameterSchluessel.VorsorgeIntervallMonate, "Vorsorge G 26.3, Standardintervall", "Monate", 36, 1, 60, "Vorschlag, wenn bei der Vorsorge kein Datum eingetragen wird"),
        new("Atemschutz", ParameterSchluessel.VorsorgeIntervallAbAlterMonate, "Vorsorge G 26.3, kürzeres Intervall", "Monate", 12, 1, 60),
        new("Atemschutz", ParameterSchluessel.VorsorgeKuerzeresIntervallAbAlter, "Kürzeres Intervall ab Lebensalter", "Jahre", 50, 18, 70),
        new("Warnfristen", ParameterSchluessel.FristWarnungTage, "Lehrgänge und Qualifikationen", "Tage vorher", 90, 0, 365),
        new("Warnfristen", ParameterSchluessel.NachweisWarnungTage, "Wiederkehrende Nachweise und Untersuchungen", "Tage vorher", 30, 0, 365),
        new("Warnfristen", ParameterSchluessel.AtemschutzWarnungTage, "Atemschutz", "Tage vorher", 60, 0, 365),
        new("Erinnerungsmail", ParameterSchluessel.ErinnerungIntervallTage, "Abstand der Mails", "Tage", 7, 1, 90),
        new("Erinnerungsmail", ParameterSchluessel.ErinnerungAbStunde, "Frühestens ab", "Uhr", 7, 0, 23, "24-Stunden-Format, deutsche Ortszeit"),
    ];

    [BindProperty]
    public Dictionary<string, int> Werte { get; set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        foreach (var e in Alle)
        {
            Werte[e.Schluessel] = await parameter.ZahlAsync(e.Schluessel, heute, e.Standard, ct);
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        foreach (var e in Alle)
        {
            if (!Werte.TryGetValue(e.Schluessel, out var wert) || wert < e.Min || wert > e.Max)
            {
                ModelState.AddModelError(string.Empty, $"{e.Bezeichnung}: erlaubt sind {e.Min} bis {e.Max} {e.Einheit}.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var geaendert = 0;
        foreach (var e in Alle)
        {
            var neu = Werte[e.Schluessel].ToString(CultureInfo.InvariantCulture);
            if (await parameter.WertAsync(e.Schluessel, heute, ct) == neu)
            {
                continue;
            }

            // Mehrere Änderungen am selben Tag überschreiben den Wert von heute.
            var vonHeute = await db.Parameter.FirstOrDefaultAsync(p => p.Schluessel == e.Schluessel && p.GueltigAb == heute, ct);
            if (vonHeute is null)
            {
                db.Parameter.Add(new Parameter { Schluessel = e.Schluessel, Wert = neu, GueltigAb = heute, Quelle = "Einstellung in FEVAD" });
            }
            else
            {
                vonHeute.Wert = neu;
            }

            geaendert++;
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = geaendert == 0 ? "Nichts geändert." : $"{geaendert} Einstellungen gespeichert, gültig ab heute.";
        return RedirectToPage();
    }
}
