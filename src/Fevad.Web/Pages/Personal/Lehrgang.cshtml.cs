using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Ausbildung;
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
/// Lehrgangsteilnahme anlegen und fortschreiben: angemeldet, zugewiesen, Zeitraum, bestanden, abgeschlossen. Mit „abgeschlossen“
/// und „bestanden“ entsteht der Nachweis in der Personalakte; wird das Ergebnis zurückgenommen, fällt er wieder weg.
/// </summary>
[Authorize(Policy = Berechtigungen.AusbildungBearbeiten)]
public class LehrgangModel(AppDbContext db, Datenbereich datenbereich, TimeProvider zeit) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public string Mitglied { get; private set; } = "";
    public Guid MitgliedId { get; private set; }
    public IReadOnlyList<SelectListItem> Lehrgaenge { get; private set; } = [];
    public bool IstNeu => Formular.Id is null;

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }
        [Required(ErrorMessage = "Lehrgang fehlt."), Display(Name = "Lehrgang")] public Guid? QualifikationId { get; set; }
        [Display(Name = "Angemeldet am")] public DateOnly? AngemeldetAm { get; set; }
        [Display(Name = "Platz zugewiesen am")] public DateOnly? ZugewiesenAm { get; set; }
        [Display(Name = "Von")] public DateOnly? Von { get; set; }
        [Display(Name = "Bis")] public DateOnly? Bis { get; set; }
        [StringLength(200), Display(Name = "Lehrgangsort")] public string? Lehrgangsort { get; set; }
        [Display(Name = "Bestanden")] public bool Bestanden { get; set; }
        [Display(Name = "Abgeschlossen")] public bool Abgeschlossen { get; set; }
        [StringLength(500), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid mitgliedId, Guid? id, Guid? lehrgang, CancellationToken ct)
    {
        if (!await LadenAsync(mitgliedId, ct))
        {
            return NotFound();
        }

        if (id is null)
        {
            Formular = new Eingabe { QualifikationId = lehrgang, AngemeldetAm = Anzeige.Heute(zeit) };
            return Page();
        }

        var t = await db.Lehrgangsteilnahmen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.MitgliedId == mitgliedId, ct);
        if (t is null)
        {
            return NotFound();
        }

        Formular = new Eingabe
        {
            Id = t.Id, QualifikationId = t.QualifikationId, AngemeldetAm = t.AngemeldetAm, ZugewiesenAm = t.ZugewiesenAm, Von = t.Von, Bis = t.Bis,
            Lehrgangsort = t.Lehrgangsort, Bestanden = t.Bestanden, Abgeschlossen = t.Abgeschlossen, Bemerkung = t.Bemerkung,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid mitgliedId, CancellationToken ct)
    {
        if (!await LadenAsync(mitgliedId, ct))
        {
            return NotFound();
        }

        var f = Formular;
        foreach (var fehler in Lehrgangsteilnahme.Fehler(f.AngemeldetAm, f.ZugewiesenAm, f.Von, f.Bis, f.Abgeschlossen))
        {
            ModelState.AddModelError("", fehler);
        }

        var qualifikation = f.QualifikationId is { } qid
            ? await db.Qualifikationen.FirstOrDefaultAsync(q => q.Id == qid && q.Art == QualifikationArt.Lehrgang, ct)
            : null;
        if (qualifikation is null)
        {
            ModelState.AddModelError("Formular.QualifikationId", "Lehrgang fehlt.");
        }

        Lehrgangsteilnahme? t = null;
        if (f.Id is { } tid && (t = await db.Lehrgangsteilnahmen.Include(x => x.Nachweis).FirstOrDefaultAsync(x => x.Id == tid && x.MitgliedId == mitgliedId, ct)) is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (t is null)
        {
            t = new Lehrgangsteilnahme { MitgliedId = mitgliedId };
            db.Lehrgangsteilnahmen.Add(t);
        }

        t.QualifikationId = qualifikation!.Id;
        t.AngemeldetAm = f.AngemeldetAm;
        t.ZugewiesenAm = f.ZugewiesenAm;
        t.Von = f.Von;
        t.Bis = f.Bis;
        t.Lehrgangsort = Leer(f.Lehrgangsort);
        t.Bestanden = f.Bestanden;
        t.Abgeschlossen = f.Abgeschlossen;
        t.Bemerkung = Leer(f.Bemerkung);

        string meldung;
        if (t.Abgeschlossen && t.Bestanden)
        {
            // Erworben am letzten Lehrgangstag; ein schon angelegter Nachweis wird nachgezogen.
            var nachweis = t.Nachweis ?? new QualifikationNachweis { MitgliedId = mitgliedId };
            if (t.Nachweis is null)
            {
                db.QualifikationNachweise.Add(nachweis);
                t.Nachweis = nachweis;
            }

            var erworben = (t.Bis ?? t.Von)!.Value;
            if (nachweis.QualifikationId != qualifikation.Id || nachweis.ErworbenAm != erworben)
            {
                nachweis.GueltigBis = null;
            }

            nachweis.QualifikationId = qualifikation.Id;
            nachweis.ErworbenAm = erworben;
            nachweis.Lehrgangsort = t.Lehrgangsort;
            nachweis.GueltigkeitVorbelegen(qualifikation);
            meldung = $"{qualifikation.Bezeichnung} bestanden und in die Personalakte eingetragen.";
        }
        else
        {
            if (t.Nachweis is { } alt)
            {
                db.QualifikationNachweise.Remove(alt);
                t.Nachweis = null;
                t.NachweisId = null;
            }

            meldung = t.Abgeschlossen ? $"{qualifikation.Bezeichnung} abgeschlossen, nicht bestanden." : $"{qualifikation.Bezeichnung}: {Anzeige.Text(t.Stand(Anzeige.Heute(zeit)))}.";
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = meldung;
        return RedirectToPage("Akte", null, new { id = mitgliedId }, "lehrgaenge");
    }

    public async Task<IActionResult> OnPostLoeschenAsync(Guid mitgliedId, Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(mitgliedId, ct))
        {
            return NotFound();
        }

        var t = await db.Lehrgangsteilnahmen.FirstOrDefaultAsync(x => x.Id == id && x.MitgliedId == mitgliedId, ct);
        if (t is null)
        {
            return NotFound();
        }

        // Ein schon eingetragener Nachweis bleibt; er steht für sich in der Personalakte.
        db.Lehrgangsteilnahmen.Remove(t);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Lehrgangsteilnahme gelöscht.";
        return RedirectToPage("Akte", null, new { id = mitgliedId }, "lehrgaenge");
    }

    private async Task<bool> LadenAsync(Guid mitgliedId, CancellationToken ct)
    {
        var mitglied = await (await datenbereich.OhneJugendAsync(db.Mitglieder, User, Anzeige.Heute(zeit))).AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mitgliedId, ct);
        if (mitglied is null)
        {
            return false;
        }

        (MitgliedId, Mitglied) = (mitglied.Id, mitglied.Anzeigename);
        Lehrgaenge = await db.Qualifikationen.AsNoTracking()
            .Where(q => q.Art == QualifikationArt.Lehrgang && (q.Aktiv || q.Id == Formular.QualifikationId))
            .OrderBy(q => q.Sortierung)
            .Select(q => new SelectListItem(q.Bezeichnung, q.Id.ToString()))
            .ToListAsync(ct);
        return true;
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
