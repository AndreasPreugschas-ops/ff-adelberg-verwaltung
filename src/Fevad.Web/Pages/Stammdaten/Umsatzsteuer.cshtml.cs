using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Vorbelegung = Fevad.Infrastructure.Persistenz.Seed.Stammdaten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Stammdaten;

/// <summary>
/// Steuerdaten für die Umsatzsteuer. Steuerpflichtig ist die Gemeinde (§ 2b UStG); die Kameradschaftskassen sind ihr
/// Sondervermögen (§ 18 FwG) und laufen unter ihrer Steuernummer. Die Steuerdaten stehen beim Datensatz der Organisation,
/// Sätze und Grenzen als Parameter mit Gültigkeitsbeginn.
/// </summary>
[Authorize(Policy = Berechtigungen.StammdatenOrganisation)]
public class UmsatzsteuerModel(AppDbContext db, Parametereinstellungen einstellungen, TimeProvider zeit) : PageModel
{
    private const string Saetze = "Steuersätze";
    private const string Grenzen = "Grenzen";

    /// <summary>Die einstellbaren Steuersätze und Umsatzgrenzen mit Vorgabe und Grenzen.</summary>
    public static IReadOnlyList<Einstellung> Alle { get; } =
    [
        new(Saetze, ParameterSchluessel.UmsatzsteuerRegelsatzProzent, "Regelsatz", "%", 19, 0, 100, "§ 12 Abs. 1 UStG"),
        new(Saetze, ParameterSchluessel.UmsatzsteuerErmaessigterSatzProzent, "Ermäßigter Satz", "%", 7, 0, 100,
            "§ 12 Abs. 2 UStG, z. B. für Speisen außer Getränken"),
        new(Grenzen, ParameterSchluessel.KleinunternehmerVorjahrEuro, "Kleinunternehmer: Umsatz im Vorjahr höchstens", "€", 25000, 0, 10_000_000,
            "§ 19 Abs. 1 UStG; gilt für die Gemeinde insgesamt, nicht für die Kasse allein"),
        new(Grenzen, ParameterSchluessel.KleinunternehmerLaufendesJahrEuro, "Kleinunternehmer: Umsatz im laufenden Jahr höchstens", "€", 100000, 0, 10_000_000,
            "§ 19 Abs. 1 UStG"),
        new(Grenzen, ParameterSchluessel.KameradschaftskasseUmsatzgrenzeEuro, "Kameradschaftskasse: Umsatzgrenze im Jahr", "€", 45000, 0, 10_000_000,
            "Angabe der Kämmerei; darüber führt sie die Feste und Verkäufe als eigenen Betrieb"),
        new(Grenzen, ParameterSchluessel.KoerperschaftsteuerFreibetragEuro, "Körperschaftsteuer: Freibetrag des Betriebs", "€", 5000, 0, 1_000_000,
            "§ 24 KStG"),
    ];

    /// <summary>Formulardaten der Steuerdaten.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();
    /// <summary>Eingestellte Sätze und Grenzen je Parameterschlüssel.</summary>
    [BindProperty] public Dictionary<string, int> Werte { get; set; } = [];

    /// <summary>Eingabefelder der Steuerdaten der Gemeinde.</summary>
    public sealed class Eingabe
    {
        /// <summary>Steuernummer der Gemeinde.</summary>
        [Display(Name = "Steuernummer der Gemeinde"), StringLength(20)]
        [RegularExpression(@"^[0-9][0-9 /]{8,18}[0-9]$", ErrorMessage = "Ziffern mit Schrägstrichen, z. B. 62/123/45678.")]
        public string? Steuernummer { get; set; }

        /// <summary>Umsatzsteuer-Identifikationsnummer der Gemeinde.</summary>
        [Display(Name = "Umsatzsteuer-Identifikationsnummer"), StringLength(20)]
        [RegularExpression(@"^DE ?[0-9]{9}$", ErrorMessage = "DE und neun Ziffern, z. B. DE123456789.")]
        public string? UmsatzsteuerId { get; set; }

        /// <summary>Zuständiges Finanzamt.</summary>
        [Display(Name = "Finanzamt"), StringLength(100)]
        public string? Finanzamt { get; set; }

        /// <summary>Tag, ab dem § 2b UStG gilt.</summary>
        [Display(Name = "§ 2b UStG gilt ab")]
        public DateOnly? UmsatzsteuerParagraph2bAb { get; set; }

        /// <summary>Die Kleinunternehmerregelung (§ 19 UStG) wird angewendet.</summary>
        [Display(Name = "Kleinunternehmerregelung (§ 19 UStG)")]
        public bool Kleinunternehmer { get; set; }

        /// <summary>Soll- oder Istversteuerung.</summary>
        [Display(Name = "Versteuerung")]
        public Versteuerungsart? Versteuerungsart { get; set; }

        /// <summary>Zeitraum der Umsatzsteuer-Voranmeldung.</summary>
        [Display(Name = "Voranmeldung")]
        public Voranmeldungszeitraum? Voranmeldungszeitraum { get; set; }

        /// <summary>Hinweis der Kämmerei.</summary>
        [Display(Name = "Hinweis der Kämmerei"), StringLength(500)]
        public string? UmsatzsteuerHinweis { get; set; }
    }

    /// <summary>Lädt Steuerdaten, Sätze und Grenzen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var o = await db.OrganisationAsync(ct);
        Formular = new Eingabe
        {
            Steuernummer = o.Steuernummer, UmsatzsteuerId = o.UmsatzsteuerId, Finanzamt = o.Finanzamt,
            UmsatzsteuerParagraph2bAb = o.UmsatzsteuerParagraph2bAb, Kleinunternehmer = o.Kleinunternehmer,
            Versteuerungsart = o.Versteuerungsart, Voranmeldungszeitraum = o.Voranmeldungszeitraum, UmsatzsteuerHinweis = o.UmsatzsteuerHinweis,
        };
        Werte = await einstellungen.LadenAsync(Alle, Anzeige.Heute(zeit), ct);
    }

    /// <summary>Speichert die Steuerdaten.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostSteuerdatenAsync(CancellationToken ct)
    {
        foreach (var schluessel in ModelState.Keys.Where(k => !k.StartsWith(nameof(Formular) + ".")).ToList())
        {
            ModelState.Remove(schluessel);
        }

        if (!ModelState.IsValid)
        {
            Werte = await einstellungen.LadenAsync(Alle, Anzeige.Heute(zeit), ct);
            return Page();
        }

        var o = await db.Organisationen.OrderBy(x => x.ErstelltAm).FirstOrDefaultAsync(ct);
        if (o is null)
        {
            o = Vorbelegung.Organisation;
            db.Organisationen.Add(o);
        }

        var f = Formular;
        o.Steuernummer = Text(f.Steuernummer);
        o.UmsatzsteuerId = Text(f.UmsatzsteuerId)?.Replace(" ", "", StringComparison.Ordinal);
        o.Finanzamt = Text(f.Finanzamt);
        o.UmsatzsteuerParagraph2bAb = f.UmsatzsteuerParagraph2bAb;
        o.Kleinunternehmer = f.Kleinunternehmer;
        o.Versteuerungsart = f.Versteuerungsart;
        o.Voranmeldungszeitraum = f.Voranmeldungszeitraum;
        o.UmsatzsteuerHinweis = Text(f.UmsatzsteuerHinweis);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Steuerdaten gespeichert.";
        return RedirectToPage();
    }

    /// <summary>Speichert geänderte Sätze und Grenzen ab heute.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostGrenzenAsync(CancellationToken ct)
    {
        foreach (var schluessel in ModelState.Keys.Where(k => k.StartsWith(nameof(Formular) + ".")).ToList())
        {
            ModelState.Remove(schluessel);
        }

        if (await einstellungen.SpeichernAsync(Alle, Werte, ModelState, Anzeige.Heute(zeit), ct) is not { } geaendert)
        {
            await FormularLadenAsync(ct);
            return Page();
        }

        TempData["Meldung"] = geaendert == 0 ? "Nichts geändert." : $"{geaendert} Werte gespeichert, gültig ab heute.";
        return RedirectToPage();
    }

    private async Task FormularLadenAsync(CancellationToken ct)
    {
        var werte = Werte;
        await OnGetAsync(ct);
        Werte = werte;
    }

    private static string? Text(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
}
