using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kostenersatz;

/// <summary>
/// Kalkulationshilfe für den Personalstundensatz nach § 34 Abs. 5 FwG (V24): Jahreskosten der Einsatzabteilung aus vier Haushaltsjahren,
/// Angehörige (Vorschlag aus den aktiven Mitgliedschaften), Stunden und Entschädigungssatz aus den Parametern. Jede Kalkulation bleibt erhalten,
/// die letzte steuert die Wiedervorlage; das PDF ist die Anlage zur Gemeinderatsvorlage.
/// </summary>
[Authorize(Policy = Berechtigungen.KostenersatzKalkulation)]
public class KalkulationModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Eingabe einer neuen Kalkulation.</summary>
    public sealed class KalkulationEingabe
    {
        /// <summary>Erstes der vier Haushaltsjahre.</summary>
        [Range(2000, 2100, ErrorMessage = "Bitte ein Jahr angeben."), Display(Name = "Erstes Haushaltsjahr")] public int ErstesJahr { get; set; }
        /// <summary>Jahreskosten im ersten Jahr.</summary>
        [Range(0, 99999999, ErrorMessage = "0 bis 99.999.999 €."), Display(Name = "Kosten 1. Jahr (€)")] public decimal Kosten1 { get; set; }
        /// <summary>Jahreskosten im zweiten Jahr.</summary>
        [Range(0, 99999999, ErrorMessage = "0 bis 99.999.999 €."), Display(Name = "Kosten 2. Jahr (€)")] public decimal Kosten2 { get; set; }
        /// <summary>Jahreskosten im dritten Jahr.</summary>
        [Range(0, 99999999, ErrorMessage = "0 bis 99.999.999 €."), Display(Name = "Kosten 3. Jahr (€)")] public decimal Kosten3 { get; set; }
        /// <summary>Jahreskosten im vierten Jahr.</summary>
        [Range(0, 99999999, ErrorMessage = "0 bis 99.999.999 €."), Display(Name = "Kosten 4. Jahr (€)")] public decimal Kosten4 { get; set; }
        /// <summary>Angehörige der Einsatzabteilung.</summary>
        [Range(1, 9999, ErrorMessage = "Mindestens 1."), Display(Name = "Angehörige der Einsatzabteilung")] public int Angehoerige { get; set; }
        /// <summary>Stunden je Angehörigem.</summary>
        [Range(1, 9999, ErrorMessage = "Mindestens 1."), Display(Name = "Stunden je Angehörigem und Jahr")] public decimal StundenJeAngehoerigem { get; set; }
        /// <summary>Entschädigungssatz je Stunde.</summary>
        [Range(0, 9999, ErrorMessage = "0 bis 9.999 €."), Display(Name = "Entschädigungssatz je Stunde (€)")] public decimal Entschaedigungssatz { get; set; }
        /// <summary>Erläuterung.</summary>
        [StringLength(2000), Display(Name = "Erläuterung (Haushaltsstellen, Abgrenzung)")] public string? Erlaeuterung { get; set; }
    }

    /// <summary>Bisherige Kalkulationen, neueste zuerst.</summary>
    public IReadOnlyList<Satzkalkulation> Kalkulationen { get; private set; } = [];

    /// <summary>Abstand der Wiedervorlage in Jahren.</summary>
    public int WiedervorlageJahre { get; private set; }

    /// <summary>Heutiges Datum.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Formular, vorbelegt mit Vorschlägen.</summary>
    [BindProperty]
    public KalkulationEingabe Formular { get; set; } = new();

    /// <summary>Lädt die Kalkulationen und belegt das Formular vor.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        var letzte = Kalkulationen.FirstOrDefault();
        Formular = new KalkulationEingabe
        {
            ErstesJahr = Heute.Year - 4,
            Kosten1 = letzte?.Kosten2 ?? 0,
            Kosten2 = letzte?.Kosten3 ?? 0,
            Kosten3 = letzte?.Kosten4 ?? 0,
            Angehoerige = await db.Mitgliedschaften.Where(s => !s.Vordienstzeit && s.Abteilung!.Art == AbteilungArt.Einsatz
                    && s.GueltigAb <= Heute && (s.GueltigBis == null || s.GueltigBis >= Heute) && s.Status != MitgliedStatus.Ausgeschieden)
                .Select(s => s.MitgliedId).Distinct().CountAsync(ct),
            StundenJeAngehoerigem = await parameter.DezimalAsync(ParameterSchluessel.KostenersatzKalkulationStunden, Heute, ct) ?? 80,
            Entschaedigungssatz = await parameter.DezimalAsync(Entschaedigungssaetze.Schluessel(Entschaedigungssaetze.VorgangEinsatz, "SatzStunde"), Heute, ct) ?? 0,
        };
        if (letzte is not null && letzte.ErstesJahr + 1 != Formular.ErstesJahr)
        {
            // Nur ein Jahr später lassen sich die Vorjahre übernehmen; sonst neu eintragen.
            Formular.Kosten1 = Formular.Kosten2 = Formular.Kosten3 = 0;
        }
    }

    /// <summary>Speichert eine neue Kalkulation mit heutigem Datum.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var f = Formular;
        db.Satzkalkulationen.Add(new Satzkalkulation
        {
            KalkuliertAm = Anzeige.Heute(zeit), ErstesJahr = f.ErstesJahr, Kosten1 = f.Kosten1, Kosten2 = f.Kosten2, Kosten3 = f.Kosten3, Kosten4 = f.Kosten4,
            Angehoerige = f.Angehoerige, StundenJeAngehoerigem = f.StundenJeAngehoerigem, Entschaedigungssatz = f.Entschaedigungssatz,
            Erlaeuterung = string.IsNullOrWhiteSpace(f.Erlaeuterung) ? null : f.Erlaeuterung.Trim(), Bearbeiter = User.Identity?.Name,
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Kalkulation gespeichert.";
        return RedirectToPage();
    }

    /// <summary>Gibt eine Kalkulation als PDF aus.</summary>
    /// <param name="id">Kennung der Kalkulation.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(Guid id, CancellationToken ct)
    {
        var k = await db.Satzkalkulationen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (k is null)
        {
            return NotFound();
        }

        var jahre = await parameter.ZahlAsync(ParameterSchluessel.KostenersatzKalkulationWiedervorlageJahre, Anzeige.Heute(zeit), 4, ct);
        var o = await db.OrganisationAsync(ct);
        return File(SatzkalkulationPdf.Erzeugen(new AntragPdf.Kopfdaten(o.GemeindeName, o.GemeindeAnschrift, null, null, null), k, k.WiedervorlageAm(jahre)),
            "application/pdf", $"Stundensatz-Kalkulation-{k.KalkuliertAm:yyyy-MM-dd}.pdf");
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        WiedervorlageJahre = await parameter.ZahlAsync(ParameterSchluessel.KostenersatzKalkulationWiedervorlageJahre, Heute, 4, ct);
        Kalkulationen = await db.Satzkalkulationen.AsNoTracking().OrderByDescending(k => k.KalkuliertAm).ThenByDescending(k => k.ErstesJahr).ToListAsync(ct);
    }
}
