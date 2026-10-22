using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>
/// Personalakte eines Mitglieds: Qualifikationen, wiederkehrende Nachweise und Atemschutz.
/// Die Vorsorge (Gesundheitsdatum) sieht nur, wer Atemschutz lesen darf.
/// </summary>
public class AkteModel(AppDbContext db, Personalfristenabfrage fristen, ParameterLeser parameter, TimeProvider zeit, IAuthorizationService autorisierung) : PageModel
{
    public Mitglied? Mitglied { get; private set; }
    public IReadOnlyList<Fristeintrag> Fristen { get; private set; } = [];
    public IReadOnlyList<QualifikationNachweis> Qualifikationen { get; private set; } = [];
    public IReadOnlyList<Nachweis> Nachweise { get; private set; } = [];
    public IReadOnlyList<AtemschutzVorsorge> Vorsorgen { get; private set; } = [];
    public IReadOnlyList<AtemschutzNachweis> AtemschutzNachweise { get; private set; } = [];
    public AtemschutzStatus? Atemschutz { get; private set; }

    public IReadOnlyList<SelectListItem> Qualifikationsauswahl { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Nachweisauswahl { get; private set; } = [];
    public DateOnly Heute { get; private set; }

    public bool DarfBearbeiten { get; private set; }
    public bool DarfNachweise { get; private set; }
    public bool DarfAtemschutzLesen { get; private set; }
    public bool DarfAtemschutzBearbeiten { get; private set; }

    public static IReadOnlyList<SelectListItem> Atemschutzarten { get; } =
        Enum.GetValues<AtemschutzNachweisArt>().Select(a => new SelectListItem(Anzeige.Text(a), a.ToString())).ToList();

    public static IReadOnlyList<SelectListItem> Vorsorgeergebnisse { get; } =
        Enum.GetValues<VorsorgeErgebnis>().Select(e => new SelectListItem(Anzeige.Text(e), e.ToString())).ToList();

    public sealed class QualifikationEingabe
    {
        [Required(ErrorMessage = "Qualifikation fehlt.")] public Guid? QualifikationId { get; set; }
        [Required(ErrorMessage = "Datum fehlt.")] public DateOnly? ErworbenAm { get; set; }
        public DateOnly? GueltigBis { get; set; }
        [StringLength(200)] public string? Lehrgangsort { get; set; }
        [Range(0, 9999, ErrorMessage = "Stunden 0 bis 9999.")] public decimal? Stunden { get; set; }
        [StringLength(500)] public string? Bemerkung { get; set; }
    }

    public sealed class NachweisEingabe
    {
        [Required(ErrorMessage = "Nachweis fehlt.")] public Guid? NachweisartId { get; set; }
        [Required(ErrorMessage = "Datum fehlt.")] public DateOnly? Datum { get; set; }
        public DateOnly? GueltigBis { get; set; }
        [StringLength(100)] public string? DurchgefuehrtVon { get; set; }
        [StringLength(500)] public string? Bemerkung { get; set; }
    }

    public sealed class VorsorgeEingabe
    {
        [Required(ErrorMessage = "Datum fehlt.")] public DateOnly? UntersuchtAm { get; set; }
        [Required(ErrorMessage = "Ergebnis fehlt.")] public VorsorgeErgebnis? Ergebnis { get; set; }
        /// <summary>Leer: aus den Parametern vorgeschlagen (Intervall nach Lebensalter).</summary>
        public DateOnly? NaechsteFaellig { get; set; }
        [StringLength(200)] public string? Bemerkung { get; set; }
    }

    public sealed class AtemschutzEingabe
    {
        [Required(ErrorMessage = "Art fehlt.")] public AtemschutzNachweisArt? Art { get; set; }
        [Required(ErrorMessage = "Datum fehlt.")] public DateOnly? Datum { get; set; }
        [Range(0, 999, ErrorMessage = "Minuten 0 bis 999.")] public int? Minuten { get; set; }
        [StringLength(100)] public string? Geraetetyp { get; set; }
        [StringLength(200)] public string? Ort { get; set; }
        [StringLength(200)] public string? Taetigkeit { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Mitglied = await db.Mitglieder.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (Mitglied is null)
        {
            return NotFound();
        }

        Heute = Anzeige.Heute(zeit);
        DarfBearbeiten = await DarfAsync(Berechtigungen.AusbildungBearbeiten);
        DarfNachweise = await DarfAsync(Berechtigungen.AusbildungNachweise);
        DarfAtemschutzLesen = await DarfAsync(Berechtigungen.AtemschutzLesen);
        DarfAtemschutzBearbeiten = await DarfAsync(Berechtigungen.AtemschutzBearbeiten);

        Fristen = (await fristen.LadenAsync(Heute, id, ct))
            .Where(f => DarfAtemschutzLesen || !f.Gesundheitsbezogen)
            .ToList();

        Qualifikationen = await db.QualifikationNachweise.AsNoTracking().Include(q => q.Qualifikation)
            .Where(q => q.MitgliedId == id)
            .OrderBy(q => q.Qualifikation!.Sortierung).ThenByDescending(q => q.ErworbenAm)
            .ToListAsync(ct);
        Nachweise = await db.Nachweise.AsNoTracking().Include(n => n.Nachweisart)
            .Where(n => n.MitgliedId == id && (DarfAtemschutzLesen || !n.Nachweisart!.Gesundheitsbezogen))
            .OrderByDescending(n => n.Datum)
            .ToListAsync(ct);
        AtemschutzNachweise = await db.AtemschutzNachweise.AsNoTracking()
            .Where(n => n.MitgliedId == id)
            .OrderByDescending(n => n.Datum)
            .ToListAsync(ct);

        if (DarfAtemschutzLesen)
        {
            Vorsorgen = await db.AtemschutzVorsorgen.AsNoTracking()
                .Where(v => v.MitgliedId == id)
                .OrderByDescending(v => v.UntersuchtAm)
                .ToListAsync(ct);

            var agt = Qualifikationen.Any(q => q.Qualifikation?.Code == Qualifikation.CodeAtemschutz && q.ErworbenAm <= Heute);
            if (agt || Vorsorgen.Count > 0)
            {
                var einstellungen = await fristen.EinstellungenAsync(Heute, ct);
                Atemschutz = AtemschutzStatus.Berechnen(agt, Vorsorgen, AtemschutzNachweise, Heute,
                    einstellungen.Intervall, einstellungen.AtemschutzWarnungTage);
            }
        }

        Qualifikationsauswahl = await db.Qualifikationen.AsNoTracking()
            .Where(q => q.Aktiv)
            .OrderBy(q => q.Art).ThenBy(q => q.Sortierung)
            .Select(q => new SelectListItem(q.Bezeichnung, q.Id.ToString()))
            .ToListAsync(ct);
        Nachweisauswahl = await db.Nachweisarten.AsNoTracking()
            .Where(a => a.Aktiv && (a.Gesundheitsbezogen ? DarfAtemschutzBearbeiten : DarfNachweise))
            .OrderBy(a => a.Sortierung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString()))
            .ToListAsync(ct);

        return Page();
    }

    public async Task<IActionResult> OnPostQualifikationAsync(Guid id, QualifikationEingabe eingabe, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AusbildungBearbeiten))
        {
            return Forbid();
        }

        var qualifikation = eingabe.QualifikationId is { } qid ? await db.Qualifikationen.FirstOrDefaultAsync(q => q.Id == qid, ct) : null;
        if (!ModelState.IsValid || qualifikation is null || !await MitgliedVorhandenAsync(id, ct))
        {
            return Fehler(id);
        }

        var nachweis = new QualifikationNachweis
        {
            MitgliedId = id,
            QualifikationId = qualifikation.Id,
            ErworbenAm = eingabe.ErworbenAm!.Value,
            GueltigBis = eingabe.GueltigBis,
            Lehrgangsort = Leer(eingabe.Lehrgangsort),
            Stunden = eingabe.Stunden,
            Bemerkung = Leer(eingabe.Bemerkung),
        };
        nachweis.GueltigkeitVorbelegen(qualifikation);
        db.QualifikationNachweise.Add(nachweis);
        await db.SaveChangesAsync(ct);

        TempData["Meldung"] = $"{qualifikation.Bezeichnung} eingetragen.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostNachweisAsync(Guid id, NachweisEingabe eingabe, CancellationToken ct)
    {
        // Untersuchungen mit Gesundheitsbezug trägt ein, wer Atemschutz bearbeiten darf, alles andere wer Nachweise erfassen darf.
        var art = eingabe.NachweisartId is { } aid ? await db.Nachweisarten.FirstOrDefaultAsync(a => a.Id == aid, ct) : null;
        if (!await DarfAsync(art is { Gesundheitsbezogen: true } ? Berechtigungen.AtemschutzBearbeiten : Berechtigungen.AusbildungNachweise))
        {
            return Forbid();
        }

        if (!ModelState.IsValid || art is null || !await MitgliedVorhandenAsync(id, ct))
        {
            return Fehler(id);
        }

        db.Nachweise.Add(new Nachweis
        {
            MitgliedId = id,
            NachweisartId = art.Id,
            Datum = eingabe.Datum!.Value,
            GueltigBis = eingabe.GueltigBis ?? Nachweis.GueltigkeitAus(eingabe.Datum.Value, art),
            DurchgefuehrtVon = Leer(eingabe.DurchgefuehrtVon),
            Bemerkung = Leer(eingabe.Bemerkung),
        });
        await db.SaveChangesAsync(ct);

        TempData["Meldung"] = $"{art.Bezeichnung} eingetragen.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostVorsorgeAsync(Guid id, VorsorgeEingabe eingabe, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AtemschutzBearbeiten))
        {
            return Forbid();
        }

        var geburtsdatum = await db.Mitglieder.Where(m => m.Id == id).Select(m => (DateOnly?)m.Geburtsdatum).FirstOrDefaultAsync(ct);
        if (!ModelState.IsValid || geburtsdatum is null)
        {
            return Fehler(id);
        }

        var untersucht = eingabe.UntersuchtAm!.Value;
        var naechste = eingabe.NaechsteFaellig ?? AtemschutzVorsorge.NaechsteVorschlagen(untersucht, geburtsdatum.Value,
            await parameter.ZahlAsync(ParameterSchluessel.VorsorgeIntervallMonate, untersucht, 36, ct),
            await parameter.ZahlAsync(ParameterSchluessel.VorsorgeIntervallAbAlterMonate, untersucht, 12, ct),
            await parameter.ZahlAsync(ParameterSchluessel.VorsorgeKuerzeresIntervallAbAlter, untersucht, 50, ct));

        db.AtemschutzVorsorgen.Add(new AtemschutzVorsorge
        {
            MitgliedId = id,
            UntersuchtAm = untersucht,
            Ergebnis = eingabe.Ergebnis!.Value,
            NaechsteFaellig = naechste,
            Bemerkung = Leer(eingabe.Bemerkung),
        });
        await db.SaveChangesAsync(ct);

        TempData["Meldung"] = $"Vorsorge eingetragen, nächste fällig am {Anzeige.Datum(naechste)}.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAtemschutzAsync(Guid id, AtemschutzEingabe eingabe, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AtemschutzBearbeiten))
        {
            return Forbid();
        }

        if (!ModelState.IsValid || !await MitgliedVorhandenAsync(id, ct))
        {
            return Fehler(id);
        }

        db.AtemschutzNachweise.Add(new AtemschutzNachweis
        {
            MitgliedId = id,
            Art = eingabe.Art!.Value,
            Datum = eingabe.Datum!.Value,
            Minuten = eingabe.Minuten,
            Geraetetyp = Leer(eingabe.Geraetetyp),
            Ort = Leer(eingabe.Ort),
            Taetigkeit = Leer(eingabe.Taetigkeit),
        });
        await db.SaveChangesAsync(ct);

        TempData["Meldung"] = $"{Anzeige.Text(eingabe.Art)} eingetragen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Löscht einen Eintrag (Soft-Delete, bleibt im Änderungsprotokoll).</summary>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, string typ, Guid eintragId, CancellationToken ct)
    {
        var (policy, eintrag) = typ switch
        {
            "qualifikation" => (Berechtigungen.AusbildungBearbeiten, (Entitaet?)await db.QualifikationNachweise.FirstOrDefaultAsync(e => e.Id == eintragId && e.MitgliedId == id, ct)),
            "nachweis" => await db.Nachweise.Include(e => e.Nachweisart).FirstOrDefaultAsync(e => e.Id == eintragId && e.MitgliedId == id, ct) is { } n
                ? (n.Nachweisart!.Gesundheitsbezogen ? Berechtigungen.AtemschutzBearbeiten : Berechtigungen.AusbildungNachweise, n)
                : (Berechtigungen.AusbildungNachweise, null),
            "vorsorge" => (Berechtigungen.AtemschutzBearbeiten, await db.AtemschutzVorsorgen.FirstOrDefaultAsync(e => e.Id == eintragId && e.MitgliedId == id, ct)),
            "atemschutz" => (Berechtigungen.AtemschutzBearbeiten, await db.AtemschutzNachweise.FirstOrDefaultAsync(e => e.Id == eintragId && e.MitgliedId == id, ct)),
            _ => (Berechtigungen.AusbildungBearbeiten, null),
        };

        if (!await DarfAsync(policy))
        {
            return Forbid();
        }

        if (eintrag is null)
        {
            return NotFound();
        }

        db.Remove(eintrag);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Eintrag gelöscht.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private Task<bool> MitgliedVorhandenAsync(Guid id, CancellationToken ct) => db.Mitglieder.AnyAsync(m => m.Id == id, ct);

    private RedirectToPageResult Fehler(Guid id)
    {
        var meldungen = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => m.Length > 0).Distinct().ToList();
        TempData["Fehler"] = meldungen.Count > 0 ? string.Join(" ", meldungen) : "Eingabe unvollständig.";
        return RedirectToPage(new { id });
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
