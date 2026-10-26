using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Vorbelegung = Fevad.Infrastructure.Persistenz.Seed.Stammdaten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Stammdaten;

/// <summary>
/// Anschriften der Feuerwehr und der Gemeinde und das Logo. Alle drei stehen auf den Antragsformularen; die Gemeinde ist als
/// Trägerin die Verantwortliche in der Datenschutzinformation.
/// </summary>
[Authorize(Policy = Berechtigungen.StammdatenOrganisation)]
[RequestSizeLimit(MaxLogoBytes + 64 * 1024), RequestFormLimits(MultipartBodyLengthLimit = MaxLogoBytes + 64 * 1024)]
public class FeuerwehrModel(AppDbContext db) : PageModel
{
    /// <summary>Höchste Dateigröße des Logos in Byte (1 MB).</summary>
    public const int MaxLogoBytes = 1024 * 1024;

    /// <summary>Formulardaten der Anschriften.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();
    /// <summary>Hochgeladenes Logo (SVG, PNG oder JPEG).</summary>
    [BindProperty, Display(Name = "Logo")] public IFormFile? Datei { get; set; }

    /// <summary>Ein Logo ist hinterlegt.</summary>
    public bool HatLogo { get; private set; }

    /// <summary>Eingabefelder der Anschriften von Feuerwehr und Gemeinde.</summary>
    public sealed class Eingabe
    {
        /// <summary>Name der Feuerwehr.</summary>
        [Display(Name = "Name der Feuerwehr"), Required(ErrorMessage = "Name fehlt."), StringLength(200)]
        public string? FeuerwehrName { get; set; }

        /// <summary>Straße und Hausnummer der Feuerwehr.</summary>
        [Display(Name = "Straße und Hausnummer"), Required(ErrorMessage = "Straße fehlt."), StringLength(200)]
        public string? FeuerwehrStrasse { get; set; }

        /// <summary>Postleitzahl der Feuerwehr.</summary>
        [Display(Name = "PLZ"), Required(ErrorMessage = "PLZ fehlt."), RegularExpression("^[0-9]{5}$", ErrorMessage = "Fünf Ziffern.")]
        public string? FeuerwehrPlz { get; set; }

        /// <summary>Ort der Feuerwehr.</summary>
        [Display(Name = "Ort"), Required(ErrorMessage = "Ort fehlt."), StringLength(100)]
        public string? FeuerwehrOrt { get; set; }

        /// <summary>Telefon der Feuerwehr.</summary>
        [Display(Name = "Telefon"), StringLength(50)]
        public string? FeuerwehrTelefon { get; set; }

        /// <summary>E-Mail-Adresse der Feuerwehr.</summary>
        [Display(Name = "E-Mail"), EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), StringLength(200)]
        public string? FeuerwehrEmail { get; set; }

        /// <summary>Name der Gemeinde bzw. Verwaltung als Trägerin.</summary>
        [Display(Name = "Name der Gemeinde bzw. Verwaltung"), Required(ErrorMessage = "Name fehlt."), StringLength(200)]
        public string? GemeindeName { get; set; }

        /// <summary>Vertretung der Gemeinde, z. B. der Bürgermeister.</summary>
        [Display(Name = "Vertreten durch"), StringLength(200)]
        public string? GemeindeVertretung { get; set; }

        /// <summary>Straße und Hausnummer der Gemeinde.</summary>
        [Display(Name = "Straße und Hausnummer"), Required(ErrorMessage = "Straße fehlt."), StringLength(200)]
        public string? GemeindeStrasse { get; set; }

        /// <summary>Postleitzahl der Gemeinde.</summary>
        [Display(Name = "PLZ"), Required(ErrorMessage = "PLZ fehlt."), RegularExpression("^[0-9]{5}$", ErrorMessage = "Fünf Ziffern.")]
        public string? GemeindePlz { get; set; }

        /// <summary>Ort der Gemeinde.</summary>
        [Display(Name = "Ort"), Required(ErrorMessage = "Ort fehlt."), StringLength(100)]
        public string? GemeindeOrt { get; set; }

        /// <summary>Telefon der Gemeinde.</summary>
        [Display(Name = "Telefon"), StringLength(50)]
        public string? GemeindeTelefon { get; set; }

        /// <summary>E-Mail-Adresse der Gemeinde.</summary>
        [Display(Name = "E-Mail"), EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), StringLength(200)]
        public string? GemeindeEmail { get; set; }
        /// <summary>Name des Bürgermeisters.</summary>
        [StringLength(200), Display(Name = "Bürgermeister/in (Name)")] public string? BuergermeisterName { get; set; }
        /// <summary>E-Mail-Adresse für Einladungen an den Bürgermeister.</summary>
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), StringLength(200), Display(Name = "E-Mail für Einladungen an den Bürgermeister")]
        public string? BuergermeisterEmail { get; set; }
    }

    /// <summary>Lädt die gespeicherten Anschriften.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var o = await db.OrganisationAsync(ct);
        HatLogo = o.Logo is not null;
        Formular = new Eingabe
        {
            FeuerwehrName = o.FeuerwehrName, FeuerwehrStrasse = o.FeuerwehrStrasse, FeuerwehrPlz = o.FeuerwehrPlz, FeuerwehrOrt = o.FeuerwehrOrt,
            FeuerwehrTelefon = o.FeuerwehrTelefon, FeuerwehrEmail = o.FeuerwehrEmail,
            GemeindeName = o.GemeindeName, GemeindeVertretung = o.GemeindeVertretung, GemeindeStrasse = o.GemeindeStrasse,
            GemeindePlz = o.GemeindePlz, GemeindeOrt = o.GemeindeOrt, GemeindeTelefon = o.GemeindeTelefon, GemeindeEmail = o.GemeindeEmail,
            BuergermeisterName = o.BuergermeisterName, BuergermeisterEmail = o.BuergermeisterEmail,
        };
    }

    /// <summary>Speichert die Anschriften.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAnschriftenAsync(CancellationToken ct)
    {
        ModelState.Remove(nameof(Datei));
        if (!ModelState.IsValid)
        {
            HatLogo = await db.Organisationen.AnyAsync(o => o.Logo != null, ct);
            return Page();
        }

        var o = await LadenAsync(ct);
        var f = Formular;
        o.FeuerwehrName = f.FeuerwehrName!.Trim();
        o.FeuerwehrStrasse = f.FeuerwehrStrasse!.Trim();
        o.FeuerwehrPlz = f.FeuerwehrPlz!.Trim();
        o.FeuerwehrOrt = f.FeuerwehrOrt!.Trim();
        o.FeuerwehrTelefon = Text(f.FeuerwehrTelefon);
        o.FeuerwehrEmail = Text(f.FeuerwehrEmail);
        o.GemeindeName = f.GemeindeName!.Trim();
        o.GemeindeVertretung = Text(f.GemeindeVertretung);
        o.GemeindeStrasse = f.GemeindeStrasse!.Trim();
        o.GemeindePlz = f.GemeindePlz!.Trim();
        o.GemeindeOrt = f.GemeindeOrt!.Trim();
        o.GemeindeTelefon = Text(f.GemeindeTelefon);
        o.GemeindeEmail = Text(f.GemeindeEmail);
        o.BuergermeisterName = Text(f.BuergermeisterName);
        o.BuergermeisterEmail = Text(f.BuergermeisterEmail);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Anschriften gespeichert.";
        return RedirectToPage();
    }

    /// <summary>Prüft und speichert das hochgeladene Logo.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder Fehler.</returns>
    public async Task<IActionResult> OnPostLogoAsync(CancellationToken ct)
    {
        if (Datei is null || Datei.Length == 0)
        {
            TempData["Fehler"] = "Bitte eine Bilddatei auswählen.";
            return RedirectToPage();
        }

        if (Datei.Length > MaxLogoBytes)
        {
            TempData["Fehler"] = "Das Logo ist zu groß (höchstens 1 MB).";
            return RedirectToPage();
        }

        using var puffer = new MemoryStream();
        await using (var inhalt = Datei.OpenReadStream())
        {
            await inhalt.CopyToAsync(puffer, ct);
        }

        var bytes = puffer.ToArray();
        if (Bildtyp(bytes) is not { } typ)
        {
            TempData["Fehler"] = "Nur SVG, PNG oder JPEG; die SVG-Datei muss sich lesen lassen.";
            return RedirectToPage();
        }

        var o = await LadenAsync(ct);
        o.Logo = bytes;
        o.LogoInhaltstyp = typ;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Logo gespeichert. Es erscheint ab sofort auf den Formularen.";
        return RedirectToPage();
    }

    /// <summary>Entfernt das Logo.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung.</returns>
    public async Task<IActionResult> OnPostLogoEntfernenAsync(CancellationToken ct)
    {
        var o = await LadenAsync(ct);
        o.Logo = null;
        o.LogoInhaltstyp = null;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Logo entfernt.";
        return RedirectToPage();
    }

    /// <summary>Liefert das Logo mit einer Sicherheitsrichtlinie, die Skripte in SVG-Dateien unterbindet.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Bilddatei oder 404 ohne Logo.</returns>
    public async Task<IActionResult> OnGetLogoAsync(CancellationToken ct)
    {
        var o = await db.OrganisationAsync(ct);
        if (o.Logo is null)
        {
            return NotFound();
        }

        // Eine SVG-Datei kann Skripte enthalten; direkt aufgerufen darf sie nichts ausführen.
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        return File(o.Logo, o.LogoInhaltstyp ?? "image/png");
    }

    /// <summary>
    /// Am Inhalt erkannt, nicht an der Endung: PNG beginnt mit 89 50 4E 47, JPEG mit FF D8 FF, SVG ist Text mit einem
    /// &lt;svg&gt;-Element, den das PDF lesen kann.
    /// </summary>
    public static string? Bildtyp(byte[] bytes)
    {
        if (bytes is [0x89, 0x50, 0x4E, 0x47, ..])
        {
            return "image/png";
        }

        if (bytes is [0xFF, 0xD8, 0xFF, ..])
        {
            return "image/jpeg";
        }

        var text = System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return text.StartsWith('<') && text.Contains("<svg", StringComparison.Ordinal) && AntragPdf.SvgLesbar(text)
            ? AntragPdf.SvgInhaltstyp
            : null;
    }

    /// <summary>Der eine Datensatz; fehlt er (alte Datenbank), wird er mit der Vorbelegung angelegt.</summary>
    private async Task<Organisation> LadenAsync(CancellationToken ct)
    {
        if (await db.Organisationen.OrderBy(o => o.ErstelltAm).FirstOrDefaultAsync(ct) is { } vorhanden)
        {
            return vorhanden;
        }

        var neu = Vorbelegung.Organisation;
        db.Organisationen.Add(neu);
        return neu;
    }

    private static string? Text(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
}
