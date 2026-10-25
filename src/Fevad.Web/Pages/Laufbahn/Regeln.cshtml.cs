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
    /// <summary>Parameter der Laufbahn, die auf der Seite gezeigt und geändert werden.</summary>
    public static readonly string[] ParameterSchluessel =
    [
        Domain.Gemeinsam.ParameterSchluessel.ProbezeitMonate,
        LaufbahnParameter.AufnahmeEinsatzAbAlter,
        LaufbahnParameter.EinsatzdienstAbAlter,
        LaufbahnParameter.JugendEndeAlter,
        LaufbahnParameter.JugendAufnahmeBisAlter,
        LaufbahnParameter.JugendAufnahmeAbAlter,
        LaufbahnParameter.KinderAufnahmeAbAlter,
        LaufbahnParameter.KinderEndeAlter,
        Domain.Gemeinsam.ParameterSchluessel.AltersabteilungAbAlter,
        Domain.Gemeinsam.ParameterSchluessel.AltersgrenzeEinsatzdienst,
        LaufbahnParameter.VorlaufMonate,
        LaufbahnParameter.EhrungJugendAbAlter,
        LaufbahnParameter.EhrungAltersabteilungBisAlter,
        LaufbahnParameter.EhrungKindererziehungMonate,
        Domain.Gemeinsam.ParameterSchluessel.LeistungsspangePflichtAbEintritt,
        LaufbahnParameter.EhrungAntragsschlussVerband,
    ];

    /// <summary>Alle Ehrungsarten.</summary>
    public IReadOnlyList<Ehrungsart> Ehrungsarten { get; private set; } = [];
    /// <summary>Alle Dienstgrade nach Rang.</summary>
    public IReadOnlyList<Dienstgrad> Dienstgrade { get; private set; } = [];
    /// <summary>Beförderungswege je Ziel-Dienstgrad.</summary>
    public ILookup<Guid, Befoerderungsweg> Wege { get; private set; } = Array.Empty<Befoerderungsweg>().ToLookup(w => w.DienstgradId);
    /// <summary>Aktuelle Werte der Laufbahnparameter.</summary>
    public IReadOnlyList<Parameter> Parameter { get; private set; } = [];
    /// <summary>Codes aller Qualifikationen, als Hilfe für die Eingabe.</summary>
    public IReadOnlyList<string> QualifikationCodes { get; private set; } = [];

    /// <summary>Lädt Ehrungsarten, Dienstgrade, Wege und Parameter.</summary>
    /// <param name="ct">Abbruchsignal.</param>
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
        /// <summary>Kennung der Ehrungsart; <c>null</c> legt neu an.</summary>
        public Guid? Id { get; set; }
        /// <summary>Kurzcode der Ehrungsart.</summary>
        public string? Code { get; set; }
        /// <summary>Bezeichnung der Ehrungsart.</summary>
        public string? Bezeichnung { get; set; }
        /// <summary>Grundlage der Jahre (Einsatzdienst, Zugehörigkeit, Funktion oder nur von Hand).</summary>
        public EhrungGrundlage Grundlage { get; set; }
        /// <summary>Erforderliche Jahre.</summary>
        public int? Jahre { get; set; }
        /// <summary>Code der Funktion bei Grundlage Funktion, z. B. „JW“.</summary>
        public string? FunktionCode { get; set; }
        /// <summary>Vorausgesetzte Vorstufe.</summary>
        public Guid? VorstufeId { get; set; }
        /// <summary>Mindestabstand zur Verleihung der Vorstufe in Jahren.</summary>
        public int? MindestabstandJahre { get; set; }
        /// <summary>Qualifikationscodes, bei denen kein Vorschlag erfolgt, durch Komma getrennt.</summary>
        public string? NichtMitQualifikationen { get; set; }
        /// <summary>Reihe, zu der die Ehrung gehört, z. B. „FEZ“.</summary>
        public string? Reihe { get; set; }
        /// <summary>Wer verleiht, z. B. „Landratsamt“.</summary>
        public string? Verleiher { get; set; }
        /// <summary>Weg des Antrags.</summary>
        public string? Antragsweg { get; set; }
        /// <summary>Kontingent als Hinweis.</summary>
        public string? Kontingent { get; set; }
        /// <summary>Vorlauf in Wochen für den Vorschlag.</summary>
        public int VorlaufWochen { get; set; }
        /// <summary>Verbandsehrung mit gemeinsamem Antragsschluss.</summary>
        public bool MitAntragsschluss { get; set; }
        /// <summary>Die Ehrungsart wird vorgeschlagen und angeboten.</summary>
        public bool Aktiv { get; set; }
    }

    /// <summary>Codes aller Funktionen, als Hilfe für die Eingabe.</summary>
    public IReadOnlyList<string> FunktionCodes { get; private set; } = [];

    /// <summary>Legt eine Ehrungsart an oder ändert sie.</summary>
    /// <param name="f">Die Eingaben.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder Fehler.</returns>
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

        // Schon eingetragene Codes bleiben erlaubt, auch wenn der Katalog sie (noch) nicht kennt, z. B. die Jugendflammen.
        var vorhanden = f.Id is null ? [] : await db.Ehrungsarten.Where(e => e.Id == f.Id).Select(e => e.NichtMitQualifikationen).SingleOrDefaultAsync(ct) ?? [];
        var unbekannt = await UnbekannteQualifikationenAsync(Codes(f.NichtMitQualifikationen).Except(vorhanden, StringComparer.OrdinalIgnoreCase), ct);
        return unbekannt.Count > 0 ? $"Unbekannte Qualifikation: {string.Join(", ", unbekannt)}." : null;
    }

    private async Task<List<string>> UnbekannteQualifikationenAsync(IEnumerable<string> codes, CancellationToken ct)
    {
        var bekannt = await db.Qualifikationen.Select(q => q.Code).ToListAsync(ct);
        return codes.Where(c => !bekannt.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    private static string? Text(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();

    /// <summary>Speichert die Mindestwartezeit bis zur nächsten Beförderung.</summary>
    /// <param name="dienstgradId">Kennung des Dienstgrads.</param>
    /// <param name="mindestwartezeitMonate">Wartezeit in Monaten; negative Werte zählen als 0.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung.</returns>
    public async Task<IActionResult> OnPostWartezeitAsync(Guid dienstgradId, int mindestwartezeitMonate, CancellationToken ct)
    {
        var dienstgrad = await db.Dienstgrade.SingleAsync(d => d.Id == dienstgradId, ct);
        dienstgrad.MindestwartezeitMonate = Math.Max(0, mindestwartezeitMonate);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Wartezeit nach {dienstgrad.Bezeichnung} gespeichert.";
        return RedirectToPage();
    }

    /// <summary>Legt einen Beförderungsweg an oder ändert ihn.</summary>
    /// <param name="id">Kennung des Wegs; <c>null</c> legt neu an.</param>
    /// <param name="dienstgradId">Ziel-Dienstgrad.</param>
    /// <param name="bezeichnung">Bezeichnung des Wegs.</param>
    /// <param name="mindestjahreImDienstgrad">Mindestjahre im bisherigen Dienstgrad.</param>
    /// <param name="alleQualifikationen">Qualifikationscodes, die alle vorliegen müssen.</param>
    /// <param name="eineDerQualifikationen">Qualifikationscodes, von denen eine vorliegen muss.</param>
    /// <param name="probezeitBeendet">Der Weg setzt eine bestandene Probezeit voraus.</param>
    /// <param name="ueberspringenErlaubt">Darf aus jedem niedrigeren Dienstgrad erreicht werden.</param>
    /// <param name="nurVonHand">Kein automatischer Vorschlag.</param>
    /// <param name="hinweis">Weitere Bedingung zum Lesen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder Fehler bei unbekannten Codes.</returns>
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

    /// <summary>Löscht einen Beförderungsweg.</summary>
    /// <param name="id">Kennung des Wegs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung.</returns>
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
