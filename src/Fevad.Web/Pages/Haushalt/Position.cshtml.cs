using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Haushalt;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Haushalt;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Haushalt;

/// <summary>Position der Haushaltsanmeldung anlegen oder bearbeiten (V29).</summary>
[Authorize(Policy = Berechtigungen.HaushaltZumelden)]
public class PositionModel(AppDbContext db, Haushaltsplanung planung, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Haushaltsjahr.</summary>
    public int Jahr { get; private set; }

    /// <summary>Haushaltsstellen zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Stellen { get; private set; } = [];

    /// <summary>Bereiche zur Auswahl; ohne Recht für alle Bereiche nur die Jugendfeuerwehr.</summary>
    public IReadOnlyList<SelectListItem> Bereiche { get; private set; } = [];

    /// <summary>Wertgrenze und Steuersatz.</summary>
    public Haushaltsgrenzen Grenzen { get; private set; } = new(1000, 19);

    /// <summary>Formular.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Eingaben zur Position.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung bei Bearbeitung.</summary>
        public Guid? Id { get; set; }
        /// <summary>Haushaltsstelle.</summary>
        [Required(ErrorMessage = "Haushaltsstelle fehlt."), Display(Name = "Haushaltsstelle (Kostenträger)")] public Guid? HaushaltsstelleId { get; set; }
        /// <summary>Bereich.</summary>
        [Display(Name = "Bereich")] public Haushaltsbereich Bereich { get; set; } = Haushaltsbereich.Technik;
        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(200), Display(Name = "Bezeichnung")] public string? Bezeichnung { get; set; }
        /// <summary>Menge.</summary>
        [Display(Name = "Menge")] public string? Menge { get; set; } = "1";
        /// <summary>Einzelpreis.</summary>
        [Display(Name = "Einzelpreis brutto (€)")] public string? Einzelpreis { get; set; }
        /// <summary>Preisquelle.</summary>
        [Display(Name = "Preisquelle")] public Preisquelle Preisquelle { get; set; }
        /// <summary>Priorität.</summary>
        [Display(Name = "Priorität")] public Prioritaet Prioritaet { get; set; } = Prioritaet.Normal;
        /// <summary>Pflicht.</summary>
        [Display(Name = "Pflichtaufgabe")] public bool Pflicht { get; set; }
        /// <summary>Begründung.</summary>
        [StringLength(2000), Display(Name = "Begründung")] public string? Begruendung { get; set; }
        /// <summary>Rechtsgrundlage.</summary>
        [StringLength(300), Display(Name = "Rechtsgrundlage")] public string? Rechtsgrundlage { get; set; }
        /// <summary>Folgen bei Ablehnung.</summary>
        [StringLength(1000), Display(Name = "Folgen bei Ablehnung")] public string? FolgenBeiAblehnung { get; set; }
        /// <summary>Rate Folgejahr 1.</summary>
        [Display(Name = "Rate Folgejahr 1 (€)")] public string? Rate1 { get; set; }
        /// <summary>Rate Folgejahr 2.</summary>
        [Display(Name = "Rate Folgejahr 2 (€)")] public string? Rate2 { get; set; }
        /// <summary>Rate Folgejahr 3.</summary>
        [Display(Name = "Rate Folgejahr 3 (€)")] public string? Rate3 { get; set; }
        /// <summary>Verpflichtungsermächtigung.</summary>
        [Display(Name = "Verpflichtungsermächtigung (€)")] public string? Verpflichtungsermaechtigung { get; set; }
        /// <summary>Zuwendung.</summary>
        [Display(Name = "Erwartete Zuwendung (€)")] public string? Zuwendung { get; set; }
        /// <summary>Folgekosten.</summary>
        [Display(Name = "Folgekosten je Jahr (€)")] public string? Folgekosten { get; set; }
        /// <summary>Bedarfsplan.</summary>
        [StringLength(200), Display(Name = "Bezug Feuerwehrbedarfsplan")] public string? Bedarfsplan { get; set; }
    }

    /// <summary>Zeigt das Formular.</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="id">Position bei Bearbeitung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Seite, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(int jahr, Guid? id, CancellationToken ct)
    {
        if (await AnmeldungAsync(jahr, ct) is not { } anmeldung)
        {
            return NotFound();
        }

        if (id is not null)
        {
            if (anmeldung.Positionen.FirstOrDefault(p => p.Id == id) is not { } p)
            {
                return NotFound();
            }

            if (!await DarfBereichAsync(p.Bereich))
            {
                return Forbid();
            }

            Formular = new Eingabe
            {
                Id = p.Id, HaushaltsstelleId = p.HaushaltsstelleId, Bereich = p.Bereich, Bezeichnung = p.Bezeichnung, Menge = p.Menge.ToString("0.##"),
                Einzelpreis = p.Einzelpreis.ToString("0.00"), Preisquelle = p.Preisquelle, Prioritaet = p.Prioritaet, Pflicht = p.Pflicht, Begruendung = p.Begruendung,
                Rechtsgrundlage = p.Rechtsgrundlage, FolgenBeiAblehnung = p.FolgenBeiAblehnung, Rate1 = Text(p.RateFolgejahr1), Rate2 = Text(p.RateFolgejahr2),
                Rate3 = Text(p.RateFolgejahr3), Verpflichtungsermaechtigung = Text(p.Verpflichtungsermaechtigung), Zuwendung = Text(p.Zuwendung),
                Folgekosten = Text(p.Folgekosten), Bedarfsplan = p.Bedarfsplan,
            };
        }
        else if (!await DarfAsync(Berechtigungen.HaushaltAlleBereiche))
        {
            Formular.Bereich = Haushaltsbereich.Jugend;
        }

        await AuswahlAsync(jahr, ct);
        return Page();
    }

    /// <summary>Speichert die Position; eine neue oder geänderte Position gilt als angemeldet.</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(int jahr, CancellationToken ct)
    {
        if (await AnmeldungAsync(jahr, ct, verfolgen: true) is not { } anmeldung)
        {
            return NotFound();
        }

        if (!anmeldung.Offen)
        {
            TempData["Fehler"] = "Die Anmeldung ist schon bei der Kämmerei.";
            return RedirectToPage("Anmeldung", new { jahr });
        }

        var position = Formular.Id is { } id ? anmeldung.Positionen.FirstOrDefault(p => p.Id == id) : null;
        if (Formular.Id is not null && position is null)
        {
            return NotFound();
        }

        if (!await DarfBereichAsync(Formular.Bereich) || (position is not null && !await DarfBereichAsync(position.Bereich)))
        {
            return Forbid();
        }

        decimal? Betrag(string? text, string feld, bool pflicht = false)
        {
            var wert = Entschaedigungssaetze.Lesen(text);
            if ((pflicht && wert is null) || (!string.IsNullOrWhiteSpace(text) && wert is null or < 0))
            {
                ModelState.AddModelError($"Formular.{feld}", "Betrag ungültig, z. B. 1250 oder 1250,50.");
            }

            return wert;
        }

        var menge = Betrag(Formular.Menge, nameof(Eingabe.Menge), pflicht: true);
        var preis = Betrag(Formular.Einzelpreis, nameof(Eingabe.Einzelpreis), pflicht: true);
        var rate1 = Betrag(Formular.Rate1, nameof(Eingabe.Rate1));
        var rate2 = Betrag(Formular.Rate2, nameof(Eingabe.Rate2));
        var rate3 = Betrag(Formular.Rate3, nameof(Eingabe.Rate3));
        var ve = Betrag(Formular.Verpflichtungsermaechtigung, nameof(Eingabe.Verpflichtungsermaechtigung));
        var zuwendung = Betrag(Formular.Zuwendung, nameof(Eingabe.Zuwendung));
        var folgekosten = Betrag(Formular.Folgekosten, nameof(Eingabe.Folgekosten));
        if (Formular.HaushaltsstelleId is { } sid && !await db.Haushaltsstellen.AnyAsync(h => h.Id == sid, ct))
        {
            ModelState.AddModelError("Formular.HaushaltsstelleId", "Unbekannte Haushaltsstelle.");
        }

        if (!ModelState.IsValid)
        {
            await AuswahlAsync(jahr, ct);
            return Page();
        }

        if (position is null)
        {
            position = new Haushaltsposition { HaushaltsanmeldungId = anmeldung.Id, Bezeichnung = "" };
            db.Haushaltspositionen.Add(position);
        }

        position.HaushaltsstelleId = Formular.HaushaltsstelleId!.Value;
        position.Bereich = Formular.Bereich;
        position.Bezeichnung = Formular.Bezeichnung!.Trim();
        position.Menge = menge!.Value;
        position.Einzelpreis = preis!.Value;
        position.Preisquelle = Formular.Preisquelle;
        position.Prioritaet = Formular.Prioritaet;
        position.Pflicht = Formular.Pflicht;
        position.Begruendung = Kurz(Formular.Begruendung);
        position.Rechtsgrundlage = Kurz(Formular.Rechtsgrundlage);
        position.FolgenBeiAblehnung = Kurz(Formular.FolgenBeiAblehnung);
        position.RateFolgejahr1 = rate1;
        position.RateFolgejahr2 = rate2;
        position.RateFolgejahr3 = rate3;
        position.Verpflichtungsermaechtigung = ve;
        position.Zuwendung = zuwendung;
        position.Folgekosten = folgekosten;
        position.Bedarfsplan = Kurz(Formular.Bedarfsplan);
        position.Status = PositionStatus.Angemeldet;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"„{position.Bezeichnung}“ gespeichert.";
        return RedirectToPage("Anmeldung", null, new { jahr }, $"p-{position.Id}");
    }

    private static string? Text(decimal? wert) => wert?.ToString("0.00");

    private static string? Kurz(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private async Task<bool> DarfBereichAsync(Haushaltsbereich bereich) => bereich == Haushaltsbereich.Jugend || await DarfAsync(Berechtigungen.HaushaltAlleBereiche);

    private async Task<Haushaltsanmeldung?> AnmeldungAsync(int jahr, CancellationToken ct, bool verfolgen = false)
    {
        Jahr = jahr;
        var abfrage = db.Haushaltsanmeldungen.Include(a => a.Positionen);
        return await (verfolgen ? abfrage : abfrage.AsNoTracking()).FirstOrDefaultAsync(a => a.Jahr == jahr, ct);
    }

    private async Task AuswahlAsync(int jahr, CancellationToken ct)
    {
        Grenzen = await planung.GrenzenAsync(new DateOnly(jahr - 1, 9, 1), ct);
        Stellen = await db.Haushaltsstellen.Where(h => h.Aktiv || h.Id == Formular.HaushaltsstelleId).OrderBy(h => h.Sortierung)
            .Select(h => new SelectListItem($"{h.Sachkonto} {h.Bezeichnung}{(h.Innenauftrag == null ? "" : " (" + h.Innenauftrag + ")")} – {(h.Teil == Haushaltsteil.Finanzhaushalt ? "FH" : "EH")}", h.Id.ToString()))
            .ToListAsync(ct);
        var alle = await DarfAsync(Berechtigungen.HaushaltAlleBereiche);
        Bereiche = Enum.GetValues<Haushaltsbereich>().Where(b => alle || b == Haushaltsbereich.Jugend)
            .Select(b => new SelectListItem(Anzeige.Text(b), b.ToString())).ToList();
    }
}
