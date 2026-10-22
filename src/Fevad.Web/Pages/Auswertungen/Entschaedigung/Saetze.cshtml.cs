using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Auswertungen.Entschaedigung;

/// <summary>
/// Sätze, Rundung und Funktionspauschalen nach der Feuerwehr-Entschädigungssatzung pflegen.
/// Jede Änderung ist eine neue Version mit „gültig ab“; ältere Abrechnungen bleiben dadurch nachvollziehbar.
/// </summary>
public class SaetzeModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    public sealed record Feld(string Schluessel, string Bezeichnung);

    public sealed record Version(string Schluessel, string Wert, DateOnly GueltigAb, DateOnly? GueltigBis, string? Quelle, bool Platzhalter);

    public IReadOnlyList<Feld> Felder { get; private set; } = [];
    public IReadOnlyList<Version> Versionen { get; private set; } = [];
    public DateOnly Heute { get; private set; }
    public bool DarfPflegen { get; private set; }

    [BindProperty]
    public Formular Neu { get; set; } = new();

    public sealed class Formular
    {
        [Required(ErrorMessage = "Bitte einen Satz wählen."), Display(Name = "Satz")]
        public string Schluessel { get; set; } = "";

        [Required(ErrorMessage = "Wert fehlt."), Display(Name = "Wert")]
        public string Wert { get; set; } = "";

        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Gültig ab")]
        public DateOnly? GueltigAb { get; set; }

        [StringLength(200), Display(Name = "Quelle")]
        public string? Quelle { get; set; }
    }

    public string Bezeichnung(string schluessel) =>
        Felder.FirstOrDefault(f => f.Schluessel == schluessel)?.Bezeichnung ?? schluessel;

    public IEnumerable<SelectListItem> Auswahl => Felder.Select(f => new SelectListItem(f.Bezeichnung, f.Schluessel));

    /// <summary>Am Tag gültiger Wert oder null.</summary>
    public Version? Aktuell(string schluessel) => Versionen
        .Where(v => v.Schluessel == schluessel && v.GueltigAb <= Heute && (v.GueltigBis is null || v.GueltigBis >= Heute))
        .MaxBy(v => v.GueltigAb);

    public async Task OnGetAsync(string? schluessel, CancellationToken ct)
    {
        await LadenAsync(ct);
        Neu = new Formular
        {
            Schluessel = schluessel ?? "",
            GueltigAb = new DateOnly(Heute.Year, 1, 1),
            Quelle = "Feuerwehr-Entschädigungssatzung Adelberg",
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.EntschaedigungSaetze)).Succeeded)
        {
            return Forbid();
        }

        await LadenAsync(ct);
        if (!string.IsNullOrEmpty(Neu.Schluessel) && Felder.All(f => f.Schluessel != Neu.Schluessel))
        {
            ModelState.AddModelError("Neu.Schluessel", "Unbekannter Satz.");
        }

        var wert = Entschaedigungssaetze.Lesen(Neu.Wert);
        if (!string.IsNullOrWhiteSpace(Neu.Wert) && (wert is null || wert < 0))
        {
            ModelState.AddModelError("Neu.Wert", "Bitte eine Zahl ab 0 eingeben, z. B. 15 oder 12,50.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var ab = Neu.GueltigAb!.Value;
        var vorhanden = await db.Parameter.Where(p => p.Schluessel == Neu.Schluessel).ToListAsync(ct);
        var text = wert!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var quelle = string.IsNullOrWhiteSpace(Neu.Quelle) ? null : Neu.Quelle.Trim();

        if (vorhanden.FirstOrDefault(p => p.GueltigAb == ab) is { } gleich)
        {
            gleich.Wert = text;
            gleich.Quelle = quelle;
        }
        else
        {
            // Vorgänger endet am Vortag, ein späterer Nachfolger begrenzt die neue Version.
            foreach (var vorher in vorhanden.Where(p => p.GueltigAb < ab && (p.GueltigBis is null || p.GueltigBis >= ab)))
            {
                vorher.GueltigBis = ab.AddDays(-1);
            }

            var nachfolger = vorhanden.Where(p => p.GueltigAb > ab).MinBy(p => p.GueltigAb);
            db.Parameter.Add(new Parameter
            {
                Schluessel = Neu.Schluessel,
                Wert = text,
                GueltigAb = ab,
                GueltigBis = nachfolger?.GueltigAb.AddDays(-1),
                Quelle = quelle,
            });
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{Bezeichnung(Neu.Schluessel)}: {Neu.Wert} ab {Anzeige.Datum(ab)} gespeichert. Offene Entwürfe bitte neu berechnen.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        DarfPflegen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EntschaedigungSaetze)).Succeeded;

        var felder = new List<Feld>();
        void Vorgang(string vorgang, string name)
        {
            felder.Add(new(Entschaedigungssaetze.Schluessel(vorgang, "SatzStunde"), $"{name}: Satz je Stunde (€)"));
            felder.Add(new(Entschaedigungssaetze.Schluessel(vorgang, "RundungMinuten"), $"{name}: angefangene Minuten aufrunden auf (60 = Stunde, 30 = halbe, 1 = genau)"));
            felder.Add(new(Entschaedigungssaetze.Schluessel(vorgang, "SatzTag"), $"{name}: Satz je Tag (€, statt Stundensatz)"));
            felder.Add(new(Entschaedigungssaetze.Schluessel(vorgang, "SatzHalberTag"), $"{name}: Satz halber Tag (€)"));
            felder.Add(new(Entschaedigungssaetze.Schluessel(vorgang, "HalberTagUnterStunden"), $"{name}: halber Tag unter … Stunden"));
        }

        Vorgang(Entschaedigungssaetze.VorgangEinsatz, "Einsatz");
        foreach (var art in Enum.GetValues<DienstArt>())
        {
            Vorgang(Entschaedigungssaetze.Vorgang(art), Anzeige.Text(art));
        }

        var funktionen = await db.Funktionen.AsNoTracking().OrderBy(f => f.Sortierung).ToListAsync(ct);
        felder.AddRange(funktionen.Select(f => new Feld(Entschaedigungssaetze.PauschalePraefix + f.Code, $"Pauschale {f.Bezeichnung} (€ je Jahr)")));
        Felder = felder;

        Versionen = await db.Parameter.AsNoTracking()
            .Where(p => p.Schluessel.StartsWith(Entschaedigungssaetze.Praefix))
            .OrderBy(p => p.Schluessel).ThenByDescending(p => p.GueltigAb)
            .Select(p => new Version(p.Schluessel, p.Wert, p.GueltigAb, p.GueltigBis, p.Quelle,
                p.Quelle != null && p.Quelle.StartsWith(Abrechnungen.PlatzhalterQuelle)))
            .ToListAsync(ct);
    }
}
