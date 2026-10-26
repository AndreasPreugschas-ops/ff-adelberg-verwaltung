using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Beschaffung.Foerderung;

/// <summary>Übersicht der Z-Feu-Anträge mit Fristen und Katalog der Fördergegenstände (V30).</summary>
public class IndexModel(AppDbContext db, Foerderverfahren verfahren, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Anträge mit Fristen.</summary>
    public IReadOnlyList<(Foerderantrag Antrag, Foerderfristen Fristen)> Antraege { get; private set; } = [];

    /// <summary>Katalog.</summary>
    public IReadOnlyList<Foerdergegenstand> Katalog { get; private set; } = [];

    /// <summary>Fördergegenstand, der gerade bearbeitet wird.</summary>
    public Guid? Bearbeiten { get; private set; }

    /// <summary>Ob der Nutzer den Katalog pflegen darf.</summary>
    public bool DarfKatalog { get; private set; }

    /// <summary>Heute in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Formular für einen Katalogeintrag.</summary>
    [BindProperty]
    public KatalogEingabe Formular { get; set; } = new();

    /// <summary>Eingaben zu einem Fördergegenstand.</summary>
    public sealed class KatalogEingabe
    {
        /// <summary>Festbetrag.</summary>
        [Display(Name = "Festbetrag (€)")] public string? Festbetrag { get; set; }
        /// <summary>Festbetrag mit Rahmenvertrag.</summary>
        [Display(Name = "mit Rahmenvertrag (€)")] public string? FestbetragRahmenvertrag { get; set; }
        /// <summary>Zweckbindung.</summary>
        [Range(1, 40, ErrorMessage = "Zweckbindung 1 bis 40 Jahre."), Display(Name = "Zweckbindung (Jahre)")] public int ZweckbindungJahre { get; set; }
        /// <summary>Gültig bis.</summary>
        [Display(Name = "Gültig bis")] public DateOnly? GueltigBis { get; set; }
        /// <summary>Fundstelle.</summary>
        [StringLength(300), Display(Name = "Fundstelle")] public string? Fundstelle { get; set; }
        /// <summary>Aktiv.</summary>
        [Display(Name = "aktiv")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>Zeigt die Übersicht, auf Wunsch mit Bearbeitungsformular für einen Katalogeintrag.</summary>
    /// <param name="bearbeiten">Fördergegenstand zum Bearbeiten.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Seite oder 403.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? bearbeiten, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.BeschaffungLesen))
        {
            return Forbid();
        }

        await LadenAsync(ct);
        if (DarfKatalog && Katalog.FirstOrDefault(k => k.Id == bearbeiten) is { } g)
        {
            Bearbeiten = g.Id;
            Formular = new KatalogEingabe
            {
                Festbetrag = g.Festbetrag.ToString("0.00"), FestbetragRahmenvertrag = g.FestbetragRahmenvertrag?.ToString("0.00"),
                ZweckbindungJahre = g.ZweckbindungJahre, GueltigBis = g.GueltigBis, Fundstelle = g.Fundstelle, Aktiv = g.Aktiv,
            };
        }

        return Page();
    }

    /// <summary>Speichert einen Katalogeintrag (Kommandant/Stellvertreter).</summary>
    /// <param name="id">Fördergegenstand.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, Seite mit Fehlern oder 403/404.</returns>
    public async Task<IActionResult> OnPostKatalogAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.BeschaffungFreigeben))
        {
            return Forbid();
        }

        var g = await db.Foerdergegenstaende.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null)
        {
            return NotFound();
        }

        var festbetrag = Entschaedigungssaetze.Lesen(Formular.Festbetrag);
        var rahmen = Entschaedigungssaetze.Lesen(Formular.FestbetragRahmenvertrag);
        if (festbetrag is null or < 0)
        {
            ModelState.AddModelError("Formular.Festbetrag", "Festbetrag ungültig.");
        }

        if (!string.IsNullOrWhiteSpace(Formular.FestbetragRahmenvertrag) && rahmen is null or < 0)
        {
            ModelState.AddModelError("Formular.FestbetragRahmenvertrag", "Betrag ungültig.");
        }

        if (Formular.GueltigBis is null)
        {
            ModelState.AddModelError("Formular.GueltigBis", "Gültig bis fehlt.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            Bearbeiten = id;
            return Page();
        }

        g.Festbetrag = festbetrag!.Value;
        g.FestbetragRahmenvertrag = rahmen;
        g.ZweckbindungJahre = Formular.ZweckbindungJahre;
        g.GueltigBis = Formular.GueltigBis!.Value;
        g.Fundstelle = string.IsNullOrWhiteSpace(Formular.Fundstelle) ? null : Formular.Fundstelle.Trim();
        g.Aktiv = Formular.Aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{g.Bezeichnung} gespeichert.";
        return RedirectToPage();
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private async Task LadenAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        DarfKatalog = await DarfAsync(Berechtigungen.BeschaffungFreigeben);
        Katalog = await db.Foerdergegenstaende.AsNoTracking().OrderBy(g => g.Sortierung).ToListAsync(ct);
        var antraege = await db.Foerderantraege.AsNoTracking().Include(a => a.Beschaffung).Include(a => a.Foerdergegenstand)
            .OrderBy(a => a.Status == Foerderstatus.Ausgezahlt || a.Status == Foerderstatus.Abgelehnt).ThenBy(a => a.Antragsjahr).ToListAsync(ct);
        var liste = new List<(Foerderantrag, Foerderfristen)>();
        foreach (var a in antraege)
        {
            liste.Add((a, await verfahren.FristenAsync(a, a.Beschaffung?.GeliefertAm, Heute, ct)));
        }

        Antraege = liste;
    }
}
