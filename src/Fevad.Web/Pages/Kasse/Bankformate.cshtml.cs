using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// CSV-Formate der Banken für den Kontoauszug-Import (V07, Stammdaten): Spalten werden über ihre Überschrift zugeordnet. Für
/// camt.053 braucht es kein Format.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseEinrichten)]
public class BankformateModel(AppDbContext db) : PageModel
{
    /// <summary>Alle Formate nach Name.</summary>
    public IReadOnlyList<Bankformat> Formate { get; private set; } = [];

    /// <summary>Das bearbeitete Format; null bei einem neuen.</summary>
    public Guid? BearbeitetId { get; private set; }

    /// <summary>Eingaben.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Felder eines Bankformats.</summary>
    public sealed class Eingabe
    {
        /// <summary>Name.</summary>
        [Required(ErrorMessage = "Bitte einen Namen angeben."), StringLength(200), Display(Name = "Name")] public string Name { get; set; } = "";

        /// <summary>Zeilen vor der Überschrift.</summary>
        [Range(0, 50), Display(Name = "Zeilen vor der Überschrift überspringen")] public int KopfzeilenUeberspringen { get; set; }

        /// <summary>Spalte Buchungstag.</summary>
        [Required(ErrorMessage = "Bitte die Spalte des Buchungstags angeben."), StringLength(100), Display(Name = "Spalte Buchungstag")]
        public string SpalteBuchungstag { get; set; } = "";

        /// <summary>Spalte Wertstellung.</summary>
        [StringLength(100), Display(Name = "Spalte Wertstellung")] public string? SpalteValuta { get; set; }

        /// <summary>Spalte Betrag.</summary>
        [StringLength(100), Display(Name = "Spalte Betrag (Ausgänge negativ)")] public string? SpalteBetrag { get; set; }

        /// <summary>Spalte Soll.</summary>
        [StringLength(100), Display(Name = "oder Spalte Soll (Ausgänge)")] public string? SpalteSoll { get; set; }

        /// <summary>Spalte Haben.</summary>
        [StringLength(100), Display(Name = "und Spalte Haben (Eingänge)")] public string? SpalteHaben { get; set; }

        /// <summary>Spalte Soll/Haben-Kennzeichen.</summary>
        [StringLength(100), Display(Name = "Spalte Kennzeichen S/H")] public string? SpalteSollHaben { get; set; }

        /// <summary>Spalte Gegenpartei.</summary>
        [StringLength(100), Display(Name = "Spalte Gegenpartei (Name)")] public string? SpalteGegenpartei { get; set; }

        /// <summary>Spalte Verwendungszweck.</summary>
        [StringLength(100), Display(Name = "Spalte Verwendungszweck")] public string? SpalteVerwendungszweck { get; set; }

        /// <summary>Spalte Referenz.</summary>
        [StringLength(100), Display(Name = "Spalte Ende-zu-Ende-Referenz")] public string? SpalteReferenz { get; set; }

        /// <summary>Datumsformat.</summary>
        [Required, StringLength(20), Display(Name = "Datumsformat")] public string Datumsformat { get; set; } = "dd.MM.yyyy";

        /// <summary>Dezimalkomma.</summary>
        [Display(Name = "Beträge mit Dezimalkomma (1.234,56)")] public bool Dezimalkomma { get; set; } = true;
    }

    /// <summary>Zeigt die Liste, auf Wunsch mit einem Format im Formular.</summary>
    /// <param name="id">Zu bearbeitendes Format.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        await LadenAsync(ct);
        if (id is { } fid)
        {
            if (Formate.FirstOrDefault(f => f.Id == fid) is not { } f)
            {
                return NotFound();
            }

            BearbeitetId = fid;
            Formular = new Eingabe
            {
                Name = f.Name, KopfzeilenUeberspringen = f.KopfzeilenUeberspringen, SpalteBuchungstag = f.SpalteBuchungstag, SpalteValuta = f.SpalteValuta,
                SpalteBetrag = f.SpalteBetrag, SpalteSoll = f.SpalteSoll, SpalteHaben = f.SpalteHaben, SpalteSollHaben = f.SpalteSollHaben,
                SpalteGegenpartei = f.SpalteGegenpartei, SpalteVerwendungszweck = f.SpalteVerwendungszweck, SpalteReferenz = f.SpalteReferenz,
                Datumsformat = f.Datumsformat, Dezimalkomma = f.Dezimalkomma,
            };
        }

        return Page();
    }

    /// <summary>Speichert ein neues oder geändertes Format.</summary>
    /// <param name="id">Geändertes Format; leer für ein neues.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid? id, CancellationToken ct)
    {
        await LadenAsync(ct);
        BearbeitetId = id;
        if (string.IsNullOrWhiteSpace(Formular.SpalteBetrag) && (string.IsNullOrWhiteSpace(Formular.SpalteSoll) || string.IsNullOrWhiteSpace(Formular.SpalteHaben)))
        {
            ModelState.AddModelError("Formular.SpalteBetrag", "Bitte die Spalte Betrag angeben oder Soll und Haben.");
        }

        if (Formate.Any(f => f.Id != id && string.Equals(f.Name, Formular.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("Formular.Name", "Diesen Namen gibt es schon.");
        }

        try
        {
            _ = DateOnly.FromDateTime(DateTime.Today).ToString(Formular.Datumsformat);
        }
        catch (FormatException)
        {
            ModelState.AddModelError("Formular.Datumsformat", "Das Datumsformat ist ungültig, z. B. dd.MM.yyyy.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var f = id is { } fid ? await db.Bankformate.FirstOrDefaultAsync(x => x.Id == fid, ct) : null;
        if (id is not null && f is null)
        {
            return NotFound();
        }

        if (f is null)
        {
            f = new Bankformat { Name = "", SpalteBuchungstag = "" };
            db.Bankformate.Add(f);
        }

        static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        f.Name = Formular.Name.Trim();
        f.KopfzeilenUeberspringen = Formular.KopfzeilenUeberspringen;
        f.SpalteBuchungstag = Formular.SpalteBuchungstag.Trim();
        f.SpalteValuta = Leer(Formular.SpalteValuta);
        f.SpalteBetrag = Leer(Formular.SpalteBetrag);
        f.SpalteSoll = Leer(Formular.SpalteSoll);
        f.SpalteHaben = Leer(Formular.SpalteHaben);
        f.SpalteSollHaben = Leer(Formular.SpalteSollHaben);
        f.SpalteGegenpartei = Leer(Formular.SpalteGegenpartei);
        f.SpalteVerwendungszweck = Leer(Formular.SpalteVerwendungszweck);
        f.SpalteReferenz = Leer(Formular.SpalteReferenz);
        f.Datumsformat = Formular.Datumsformat.Trim();
        f.Dezimalkomma = Formular.Dezimalkomma;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Format „{f.Name}“ gespeichert.";
        return RedirectToPage();
    }

    /// <summary>Entfernt ein Format; bereits eingelesene Umsätze bleiben.</summary>
    /// <param name="id">Das Format.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, CancellationToken ct)
    {
        var f = await db.Bankformate.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null)
        {
            return NotFound();
        }

        db.Bankformate.Remove(f);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Format „{f.Name}“ entfernt.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct) => Formate = await db.Bankformate.AsNoTracking().OrderBy(f => f.Name).ToListAsync(ct);
}
