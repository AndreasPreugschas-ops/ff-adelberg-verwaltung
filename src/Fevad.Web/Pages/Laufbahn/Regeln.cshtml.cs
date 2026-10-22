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
        LaufbahnParameter.EhrungKindererziehungMonate,
        LaufbahnParameter.EhrungLeistungsspangeAbEintritt,
        LaufbahnParameter.EhrungAntragsschlussVerband,
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
        FunktionCodes = await db.Funktionen.AsNoTracking().OrderBy(x => x.Sortierung).Select(x => x.Code).ToListAsync(ct);
    }

    /// <summary>Eingaben für eine Ehrungsart; leere Texte werden zu null.</summary>
    public sealed class EhrungsartFormular
    {
        public Guid? Id { get; set; }
        public string? Code { get; set; }
        public string? Bezeichnung { get; set; }
        public EhrungGrundlage Grundlage { get; set; }
        public int? Jahre { get; set; }
        public string? FunktionCode { get; set; }
        public Guid? VorstufeId { get; set; }
        public int? MindestabstandJahre { get; set; }
        public string? NichtMitQualifikationen { get; set; }
        public string? Reihe { get; set; }
        public string? Verleiher { get; set; }
        public string? Antragsweg { get; set; }
        public string? Kontingent { get; set; }
        public int VorlaufWochen { get; set; }
        public bool MitAntragsschluss { get; set; }
        public bool Aktiv { get; set; }
    }

    public IReadOnlyList<string> FunktionCodes { get; private set; } = [];

    public async Task<IActionResult> OnPostEhrungsartAsync(EhrungsartFormular f, CancellationToken ct)
    {
        var fehler = await PruefenAsync(f, ct);
        if (fehler is not null)
        {
            TempData["Fehler"] = fehler;
            return RedirectToPage();
        }

        Ehrungsart art;
        if (f.Id is null)
        {
            art = new Ehrungsart { Code = f.Code!.Trim(), Bezeichnung = "", Sortierung = await db.Ehrungsarten.MaxAsync(e => (int?)e.Sortierung, ct) + 10 ?? 10 };
            db.Ehrungsarten.Add(art);
        }
        else
        {
            art = await db.Ehrungsarten.SingleAsync(e => e.Id == f.Id, ct);
        }

        art.Bezeichnung = f.Bezeichnung!.Trim();
        art.Grundlage = f.Grundlage;
        art.Jahre = f.Grundlage == EhrungGrundlage.Ohne ? null : f.Jahre;
        art.FunktionCode = f.Grundlage == EhrungGrundlage.Funktion ? Text(f.FunktionCode)?.ToUpperInvariant() : null;
        art.VorstufeId = f.VorstufeId == art.Id ? null : f.VorstufeId;
        art.MindestabstandJahre = art.VorstufeId is null || f.MindestabstandJahre is not > 0 ? null : f.MindestabstandJahre;
        art.NichtMitQualifikationen = Codes(f.NichtMitQualifikationen);
        art.Reihe = Text(f.Reihe);
        art.Verleiher = Text(f.Verleiher);
        art.Antragsweg = Text(f.Antragsweg);
        art.Kontingent = Text(f.Kontingent);
        art.VorlaufWochen = Math.Max(0, f.VorlaufWochen);
        art.MitAntragsschluss = f.MitAntragsschluss;
        art.Aktiv = f.Aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{art.Bezeichnung} gespeichert.";
        return RedirectToPage();
    }

    private async Task<string?> PruefenAsync(EhrungsartFormular f, CancellationToken ct)
    {
        if (f.Id is null && (string.IsNullOrWhiteSpace(f.Code) || await db.Ehrungsarten.IgnoreQueryFilters().AnyAsync(e => e.Code == f.Code.Trim(), ct)))
        {
            return "Der Code fehlt oder ist schon vergeben.";
        }

        if (string.IsNullOrWhiteSpace(f.Bezeichnung) || (f.Grundlage != EhrungGrundlage.Ohne && f.Jahre is not > 0))
        {
            return "Bezeichnung und, außer bei „nur von Hand“, die Jahre angeben.";
        }

        if (f.Grundlage == EhrungGrundlage.Funktion
            && (string.IsNullOrWhiteSpace(f.FunktionCode) || !await db.Funktionen.AnyAsync(x => x.Code == f.FunktionCode.Trim().ToUpper(), ct)))
        {
            return "Bei „Jahre in Funktion“ den Code einer Funktion angeben.";
        }

        var unbekannt = await UnbekannteQualifikationenAsync(Codes(f.NichtMitQualifikationen), ct);
        return unbekannt.Count > 0 ? $"Unbekannte Qualifikation: {string.Join(", ", unbekannt)}." : null;
    }

    private async Task<List<string>> UnbekannteQualifikationenAsync(IEnumerable<string> codes, CancellationToken ct)
    {
        var bekannt = await db.Qualifikationen.Select(q => q.Code).ToListAsync(ct);
        return codes.Where(c => !bekannt.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    private static string? Text(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();

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
        var unbekannt = await UnbekannteQualifikationenAsync(alle.Concat(eine), ct);
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
