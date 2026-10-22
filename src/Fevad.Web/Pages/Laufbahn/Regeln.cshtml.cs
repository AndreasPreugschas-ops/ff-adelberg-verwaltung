using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Laufbahn;

/// <summary>Ehrungsarten, Beförderungswege und Wartezeiten pflegen; Altersgrenzen anzeigen.</summary>
[Authorize(Policy = Berechtigungen.LaufbahnRegeln)]
public class RegelnModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    public static readonly string[] ParameterSchluessel =
    [
        Domain.Gemeinsam.ParameterSchluessel.ProbezeitMonate,
        LaufbahnParameter.AufnahmeEinsatzAbAlter,
        LaufbahnParameter.EinsatzdienstAbAlter,
        LaufbahnParameter.JugendEndeAlter,
        Domain.Gemeinsam.ParameterSchluessel.AltersabteilungAbAlter,
        Domain.Gemeinsam.ParameterSchluessel.AltersgrenzeEinsatzdienst,
        LaufbahnParameter.VorlaufMonate,
        LaufbahnParameter.EhrungJugendAbAlter,
        LaufbahnParameter.EhrungAltersabteilungBisAlter,
    ];

    public IReadOnlyList<Ehrungsart> Ehrungsarten { get; private set; } = [];
    public IReadOnlyList<Dienstgrad> Dienstgrade { get; private set; } = [];
    public ILookup<Guid, Befoerderungsweg> Wege { get; private set; } = Array.Empty<Befoerderungsweg>().ToLookup(w => w.DienstgradId);
    public IReadOnlyList<Parameter> Parameter { get; private set; } = [];
    public IReadOnlyList<string> QualifikationCodes { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Ehrungsarten = await db.Ehrungsarten.AsNoTracking().OrderBy(e => e.Sortierung).ToListAsync(ct);
        Dienstgrade = await db.Dienstgrade.AsNoTracking().OrderBy(d => d.Sortierung).ToListAsync(ct);
        Wege = (await db.Befoerderungswege.AsNoTracking().OrderBy(w => w.Bezeichnung).ToListAsync(ct)).ToLookup(w => w.DienstgradId);
        Parameter = (await db.Parameter.AsNoTracking()
                .Where(p => ParameterSchluessel.Contains(p.Schluessel) && p.GueltigAb <= heute && (p.GueltigBis == null || p.GueltigBis >= heute))
                .ToListAsync(ct))
            .GroupBy(p => p.Schluessel).Select(g => g.MaxBy(p => p.GueltigAb)!)
            .OrderBy(p => Array.IndexOf(ParameterSchluessel, p.Schluessel))
            .ToList();
        QualifikationCodes = await db.Qualifikationen.AsNoTracking().OrderBy(q => q.Sortierung).Select(q => q.Code).ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostEhrungsartAsync(
        Guid? id, string? code, string bezeichnung, EhrungGrundlage grundlage, int? jahre, string? reihe, string? verleiher, int vorlaufMonate,
        bool aktiv, CancellationToken ct)
    {
        Ehrungsart art;
        if (id is null)
        {
            if (string.IsNullOrWhiteSpace(code) || await db.Ehrungsarten.IgnoreQueryFilters().AnyAsync(e => e.Code == code.Trim(), ct))
            {
                TempData["Fehler"] = "Der Code fehlt oder ist schon vergeben.";
                return RedirectToPage();
            }

            art = new Ehrungsart { Code = code.Trim(), Bezeichnung = bezeichnung, Sortierung = await db.Ehrungsarten.MaxAsync(e => (int?)e.Sortierung, ct) + 10 ?? 10 };
            db.Ehrungsarten.Add(art);
        }
        else
        {
            art = await db.Ehrungsarten.SingleAsync(e => e.Id == id, ct);
        }

        if (string.IsNullOrWhiteSpace(bezeichnung) || (grundlage != EhrungGrundlage.Ohne && jahre is not > 0))
        {
            TempData["Fehler"] = "Bezeichnung und, außer bei „nur von Hand“, die Jahre angeben.";
            return RedirectToPage();
        }

        art.Bezeichnung = bezeichnung.Trim();
        art.Grundlage = grundlage;
        art.Jahre = grundlage == EhrungGrundlage.Ohne ? null : jahre;
        art.Reihe = string.IsNullOrWhiteSpace(reihe) ? null : reihe.Trim();
        art.Verleiher = string.IsNullOrWhiteSpace(verleiher) ? null : verleiher.Trim();
        art.VorlaufMonate = Math.Max(0, vorlaufMonate);
        art.Aktiv = aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{art.Bezeichnung} gespeichert.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostWartezeitAsync(Guid dienstgradId, int mindestwartezeitMonate, CancellationToken ct)
    {
        var dienstgrad = await db.Dienstgrade.SingleAsync(d => d.Id == dienstgradId, ct);
        dienstgrad.MindestwartezeitMonate = Math.Max(0, mindestwartezeitMonate);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Wartezeit nach {dienstgrad.Bezeichnung} gespeichert.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostWegAsync(
        Guid? id, Guid dienstgradId, string? bezeichnung, int mindestjahreImDienstgrad, string? alleQualifikationen, string? eineDerQualifikationen,
        bool probezeitBeendet, bool ueberspringenErlaubt, bool nurVonHand, string? hinweis, CancellationToken ct)
    {
        var alle = Codes(alleQualifikationen);
        var eine = Codes(eineDerQualifikationen);
        var bekannt = await db.Qualifikationen.Select(q => q.Code).ToListAsync(ct);
        var unbekannt = alle.Concat(eine).Where(c => !bekannt.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (unbekannt.Count > 0)
        {
            TempData["Fehler"] = $"Unbekannte Qualifikation: {string.Join(", ", unbekannt)}. Erlaubt sind die Codes aus dem Qualifikationskatalog.";
            return RedirectToPage();
        }

        var weg = id is null ? new Befoerderungsweg { DienstgradId = dienstgradId } : await db.Befoerderungswege.SingleAsync(w => w.Id == id, ct);
        if (id is null)
        {
            db.Befoerderungswege.Add(weg);
        }

        weg.Bezeichnung = string.IsNullOrWhiteSpace(bezeichnung) ? null : bezeichnung.Trim();
        weg.MindestjahreImDienstgrad = Math.Max(0, mindestjahreImDienstgrad);
        weg.AlleQualifikationen = alle;
        weg.EineDerQualifikationen = eine;
        weg.ProbezeitBeendet = probezeitBeendet;
        weg.UeberspringenErlaubt = ueberspringenErlaubt;
        weg.NurVonHand = nurVonHand;
        weg.Hinweis = string.IsNullOrWhiteSpace(hinweis) ? null : hinweis.Trim();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Beförderungsweg gespeichert.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostWegLoeschenAsync(Guid id, CancellationToken ct)
    {
        var weg = await db.Befoerderungswege.SingleAsync(w => w.Id == id, ct);
        db.Befoerderungswege.Remove(weg);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Beförderungsweg gelöscht.";
        return RedirectToPage();
    }

    private static List<string> Codes(string? text) =>
        (text ?? "").Split([',', ';', ' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant()).Distinct().ToList();
}
