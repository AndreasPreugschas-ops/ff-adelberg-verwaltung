using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Gemeinde;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gemeinde;

/// <summary>
/// Ein Vorgang „Entscheidung der Gemeinde nötig“ (V27): Vorlagen-Entwurf aus Mustertexten je Anlass, Vorlage an die Gemeinde, Entscheidung mit
/// Vermerk und PDF der Vorlage. Vorbereiten darf die Feuerwehr, vorlegen und die Entscheidung eintragen nur das Hauptamt.
/// </summary>
[Authorize(Policy = Berechtigungen.GemeindeVorgaenge)]
public class VorgangModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Eingabe der Vorlage.</summary>
    public sealed class VorgangEingabe
    {
        /// <summary>Anlass.</summary>
        [Display(Name = "Anlass")] public GemeindevorgangArt Art { get; set; }
        /// <summary>Betreff.</summary>
        [Required(ErrorMessage = "Bitte angeben."), StringLength(300), Display(Name = "Betreff")] public string Betreff { get; set; } = "";
        /// <summary>Betroffenes Mitglied.</summary>
        [Display(Name = "Betroffenes Mitglied")] public Guid? MitgliedId { get; set; }
        /// <summary>Zuständiges Gremium.</summary>
        [Required(ErrorMessage = "Bitte angeben."), StringLength(50), Display(Name = "Zuständig")] public string Zustaendig { get; set; } = Gemeindevorlagen.Gemeinderat;
        /// <summary>Sachverhalt.</summary>
        [StringLength(4000), Display(Name = "Sachverhalt")] public string? Sachverhalt { get; set; }
        /// <summary>Rechtsgrundlage.</summary>
        [StringLength(500), Display(Name = "Rechtsgrundlage")] public string? Rechtsgrundlage { get; set; }
        /// <summary>Beschlussvorschlag.</summary>
        [StringLength(2000), Display(Name = "Beschlussvorschlag")] public string? Beschlussvorschlag { get; set; }
    }

    /// <summary>Eingabe der Entscheidung.</summary>
    public sealed class EntscheidungEingabe
    {
        /// <summary>Ergebnis.</summary>
        [Display(Name = "Ergebnis")] public GemeindeErgebnis Ergebnis { get; set; }
        /// <summary>Tag der Entscheidung.</summary>
        [Required(ErrorMessage = "Bitte angeben."), Display(Name = "Entschieden am")] public DateOnly? Datum { get; set; }
        /// <summary>Vermerk.</summary>
        [StringLength(1000), Display(Name = "Vermerk (Sitzung, TOP, Änderungen)")] public string? Vermerk { get; set; }
    }

    /// <summary>Der Vorgang; null beim Anlegen.</summary>
    public Gemeindevorgang? Vorgang { get; private set; }

    /// <summary>Der Benutzer darf vorlegen und entscheiden.</summary>
    public bool DarfEntscheiden { get; private set; }

    /// <summary>Mitglieder zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Mitgliederauswahl { get; private set; } = [];

    /// <summary>Anlässe zur Auswahl.</summary>
    public static IReadOnlyList<SelectListItem> Anlaesse { get; } =
        Enum.GetValues<GemeindevorgangArt>().Select(a => new SelectListItem(Anzeige.Text(a), a.ToString())).ToList();

    /// <summary>Ergebnisse zur Auswahl.</summary>
    public static IReadOnlyList<SelectListItem> Ergebnisse { get; } =
        Enum.GetValues<GemeindeErgebnis>().Select(e => new SelectListItem(Anzeige.Text(e), e.ToString())).ToList();

    /// <summary>Formular der Vorlage.</summary>
    [BindProperty]
    public VorgangEingabe Formular { get; set; } = new();

    /// <summary>Formular der Entscheidung.</summary>
    [BindProperty]
    public EntscheidungEingabe Entscheidung { get; set; } = new();

    /// <summary>Zeigt den Vorgang oder ein neues Formular mit den Mustertexten des Anlasses.</summary>
    /// <param name="id">Kennung oder null zum Anlegen.</param>
    /// <param name="art">Anlass für die Mustertexte beim Anlegen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, GemeindevorgangArt? art, CancellationToken ct)
    {
        if (id is { } vid)
        {
            Vorgang = await db.Gemeindevorgaenge.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vid, ct);
            if (Vorgang is null)
            {
                return NotFound();
            }

            var v = Vorgang;
            Formular = new VorgangEingabe
            {
                Art = v.Art, Betreff = v.Betreff, MitgliedId = v.MitgliedId, Zustaendig = v.Zustaendig, Sachverhalt = v.Sachverhalt,
                Rechtsgrundlage = v.Rechtsgrundlage, Beschlussvorschlag = v.Beschlussvorschlag,
            };
            Entscheidung = new EntscheidungEingabe { Datum = Anzeige.Heute(zeit) };
        }
        else
        {
            var a = art ?? GemeindevorgangArt.ZustimmungWahl;
            var (zustaendig, rechtsgrundlage, vorschlag) = Gemeindevorlagen.Muster(a);
            Formular = new VorgangEingabe { Art = a, Betreff = Anzeige.Text(a), Zustaendig = zustaendig, Rechtsgrundlage = rechtsgrundlage, Beschlussvorschlag = vorschlag };
        }

        await AnzeigeAsync(ct);
        return Page();
    }

    /// <summary>Legt den Vorgang an oder speichert die Vorlage; nach der Entscheidung nicht mehr.</summary>
    /// <param name="id">Kennung oder null zum Anlegen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern oder 404.</returns>
    public async Task<IActionResult> OnPostSpeichernAsync(Guid? id, CancellationToken ct)
    {
        NurPruefen(nameof(Formular));

        Gemeindevorgang? v = null;
        if (id is { } vid)
        {
            v = await db.Gemeindevorgaenge.FirstOrDefaultAsync(x => x.Id == vid, ct);
            if (v is null)
            {
                return NotFound();
            }

            if (v.Stand is GemeindevorgangStand.Entschieden or GemeindevorgangStand.Zurueckgezogen)
            {
                ModelState.AddModelError(string.Empty, "Ein entschiedener oder zurückgezogener Vorgang wird nicht mehr geändert.");
            }
        }

        var heute = Anzeige.Heute(zeit);
        if (Formular.MitgliedId is { } mid && !await Auswahl.WaehlbarAsync(db, heute, mid, v?.MitgliedId, ct))
        {
            ModelState.AddModelError("Formular.MitgliedId", "Dieses Mitglied ist hier nicht wählbar.");
        }

        if (!ModelState.IsValid)
        {
            Vorgang = v;
            await AnzeigeAsync(ct);
            return Page();
        }

        if (v is null)
        {
            v = new Gemeindevorgang { Betreff = "", Zustaendig = "" };
            db.Gemeindevorgaenge.Add(v);
        }

        var f = Formular;
        v.Art = f.Art;
        v.Betreff = f.Betreff.Trim();
        v.MitgliedId = f.MitgliedId;
        v.Zustaendig = f.Zustaendig.Trim();
        v.Sachverhalt = Leer(f.Sachverhalt);
        v.Rechtsgrundlage = Leer(f.Rechtsgrundlage);
        v.Beschlussvorschlag = Leer(f.Beschlussvorschlag);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Vorgang gespeichert.";
        return RedirectToPage(new { id = v.Id });
    }

    /// <summary>Vermerkt die Vorlage an die Gemeinde mit heutigem Datum.</summary>
    /// <param name="id">Kennung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostVorlegenAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfEntscheidenAsync())
        {
            return Forbid();
        }

        var v = await db.Gemeindevorgaenge.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null)
        {
            return NotFound();
        }

        if (v.Stand == GemeindevorgangStand.Entwurf)
        {
            v.VorgelegtAm = Anzeige.Heute(zeit);
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Trägt die Entscheidung der Gemeinde ein.</summary>
    /// <param name="id">Kennung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostEntscheidenAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfEntscheidenAsync())
        {
            return Forbid();
        }

        var v = await db.Gemeindevorgaenge.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null)
        {
            return NotFound();
        }

        NurPruefen(nameof(Entscheidung));

        if (v.Stand != GemeindevorgangStand.Vorgelegt)
        {
            ModelState.AddModelError(string.Empty, "Entschieden werden kann nur ein vorgelegter Vorgang.");
        }

        if (Entscheidung.Ergebnis == GemeindeErgebnis.Geaendert && string.IsNullOrWhiteSpace(Entscheidung.Vermerk))
        {
            ModelState.AddModelError("Entscheidung.Vermerk", "Bitte die Änderungen vermerken.");
        }

        if (!ModelState.IsValid)
        {
            Vorgang = v;
            Formular = new VorgangEingabe
            {
                Art = v.Art, Betreff = v.Betreff, MitgliedId = v.MitgliedId, Zustaendig = v.Zustaendig, Sachverhalt = v.Sachverhalt,
                Rechtsgrundlage = v.Rechtsgrundlage, Beschlussvorschlag = v.Beschlussvorschlag,
            };
            await AnzeigeAsync(ct);
            return Page();
        }

        v.Ergebnis = Entscheidung.Ergebnis;
        v.EntschiedenAm = Entscheidung.Datum;
        v.Entscheidungsvermerk = Leer(Entscheidung.Vermerk);
        v.EingetragenVon = User.Identity?.Name;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Entscheidung eingetragen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Zieht einen noch nicht entschiedenen Vorgang zurück.</summary>
    /// <param name="id">Kennung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostZurueckziehenAsync(Guid id, CancellationToken ct)
    {
        var v = await db.Gemeindevorgaenge.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null)
        {
            return NotFound();
        }

        if (v.Ergebnis is null)
        {
            v.Zurueckgezogen = true;
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Gibt die Vorlage als PDF aus.</summary>
    /// <param name="id">Kennung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(Guid id, CancellationToken ct)
    {
        var v = await db.Gemeindevorgaenge.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null)
        {
            return NotFound();
        }

        var o = await db.OrganisationAsync(ct);
        return File(GemeindePdf.Vorlage(new AntragPdf.Kopfdaten(o.GemeindeName, o.GemeindeAnschrift, null, null, null), v, Anzeige.Heute(zeit)),
            "application/pdf", $"Vorlage-{v.Art}-{Anzeige.Heute(zeit):yyyy-MM-dd}.pdf");
    }

    /// <summary>
    /// Behält nur die Prüfergebnisse des abgeschickten Formulars. Das andere Formular fehlt in der Anfrage; der Binder versucht es dann ohne
    /// Präfix und meldet seine Pflichtfelder unter dem bloßen Feldnamen.
    /// </summary>
    private void NurPruefen(string praefix)
    {
        foreach (var k in ModelState.Keys.Where(k => !k.StartsWith(praefix + ".", StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(k);
        }
    }

    private async Task AnzeigeAsync(CancellationToken ct)
    {
        DarfEntscheiden = await DarfEntscheidenAsync();
        Mitgliederauswahl = await Auswahl.MitgliederAsync(db, Anzeige.Heute(zeit), Vorgang?.MitgliedId, ct);
    }

    private async Task<bool> DarfEntscheidenAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.GemeindeEntscheidungen)).Succeeded;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
