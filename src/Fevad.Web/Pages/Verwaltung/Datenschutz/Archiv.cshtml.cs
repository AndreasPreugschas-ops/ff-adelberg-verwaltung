using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Verwaltung.Datenschutz;

/// <summary>
/// Archivanbietung (V65, Landesarchivgesetz BW): Bewertungskatalog, Anbietungsverzeichnis an das zuständige Archiv, Entscheidung des Archivs
/// je Unterlage, Archivpaket und Übergabe. Der Löschlauf löscht angebotene Unterlagen erst nach Kassation, Übergabe oder Fristablauf.
/// </summary>
public class ArchivModel(AppDbContext db, Archivpaket paket, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Zeile des Bewertungskatalogs mit den aktuellen Parameterwerten.</summary>
    /// <param name="Bewertung">Eintrag des Katalogs.</param>
    /// <param name="Wert">„archivwürdig“ oder „kassabel“.</param>
    /// <param name="Frist">Aufbewahrungsfrist aus den Parametern.</param>
    public sealed record Katalogzeile(Archivierung.Bewertung Bewertung, string Wert, string? Frist);

    /// <summary>Zuständiges Archiv.</summary>
    public string Stelle { get; private set; } = "";

    /// <summary>Entscheidungsfrist des Archivs in Monaten.</summary>
    public int FristMonate { get; private set; }

    /// <summary>Stichtag.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Bewertungskatalog.</summary>
    public IReadOnlyList<Katalogzeile> Katalog { get; private set; } = [];

    /// <summary>Offene Angebote: noch nicht gelöscht.</summary>
    public IReadOnlyList<Archivangebot> Offene { get; private set; } = [];

    /// <summary>Die Einstellungen aufgeklappt zeigen, etwa nach einem Eingabefehler.</summary>
    public bool EinstellungenOffen { get; private set; }

    /// <summary>Die zuletzt gelöschten Unterlagen als Nachweis (§ 6 Abs. 4 LArchG).</summary>
    public IReadOnlyList<Archivangebot> Erledigte { get; private set; } = [];

    /// <summary>Zeigt Katalog und Angebote.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    /// <summary>
    /// Anbietungsverzeichnis als CSV: alle Unterlagen ohne Entscheidung. Was noch nicht angeboten war, gilt ab heute als angeboten; ab dann
    /// läuft die Entscheidungsfrist des Archivs.
    /// </summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die CSV-Datei.</returns>
    public async Task<IActionResult> OnPostVerzeichnisAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        var liste = await db.Archivangebote.Where(a => a.ErledigtAm == null && a.Entscheidung == Archiventscheidung.Offen)
            .OrderBy(a => a.Art).ThenBy(a => a.Jahr).ThenBy(a => a.Bezeichnung).ToListAsync(ct);
        foreach (var a in liste.Where(a => a.AngebotenAm is null))
        {
            a.AngebotenAm = Heute;
        }

        await db.SaveChangesAsync(ct);
        var text = new StringBuilder($"Anbietungsverzeichnis an {Stelle};;;;\r\nNr;Art;Bezeichnung;Jahr;Angeboten am\r\n");
        var nr = 0;
        foreach (var a in liste)
        {
            text.Append($"{++nr};{Archivierung.Anzeigename(a.Art)};{Feld(a.Bezeichnung)};{a.Jahr};{a.AngebotenAm:dd.MM.yyyy}\r\n");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
        return File(bytes, "text/csv", $"anbietungsverzeichnis-{Heute:yyyy-MM-dd}.csv");
    }

    /// <summary>Trägt die Entscheidung des Archivs für die markierten Unterlagen ein.</summary>
    /// <param name="ids">Markierte Angebote.</param>
    /// <param name="entscheidung">Übernehmen oder nicht archivwürdig.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung.</returns>
    public async Task<IActionResult> OnPostEntscheidenAsync(List<Guid> ids, Archiventscheidung entscheidung, CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        var liste = await db.Archivangebote.Where(a => ids.Contains(a.Id) && a.ErledigtAm == null && a.UebergebenAm == null).ToListAsync(ct);
        foreach (var a in liste)
        {
            a.Entscheidung = entscheidung;
            a.EntschiedenAm = entscheidung == Archiventscheidung.Offen ? null : Heute;
            a.AngebotenAm ??= Heute;
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{liste.Count} {(liste.Count == 1 ? "Unterlage" : "Unterlagen")}: {Archivierung.Text(entscheidung)}.";
        return RedirectToPage();
    }

    /// <summary>Archivpaket (ZIP) mit den markierten Unterlagen, die das Archiv übernimmt; die Prüfsumme wird je Unterlage vermerkt.</summary>
    /// <param name="ids">Markierte Angebote; ohne Auswahl alle übernommenen, noch nicht übergebenen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das ZIP oder eine Weiterleitung, wenn nichts zu übergeben ist.</returns>
    public async Task<IActionResult> OnPostPaketAsync(List<Guid> ids, CancellationToken ct)
    {
        var liste = await db.Archivangebote
            .Where(a => a.ErledigtAm == null && a.Entscheidung == Archiventscheidung.Uebernehmen && a.UebergebenAm == null && (ids.Count == 0 || ids.Contains(a.Id)))
            .ToListAsync(ct);
        if (liste.Count == 0)
        {
            TempData["Fehler"] = "Keine übernommenen Unterlagen ausgewählt, die noch übergeben werden müssen.";
            return RedirectToPage();
        }

        var jetzt = zeit.GetUtcNow();
        var zip = await paket.ErstellenAsync(liste, jetzt, ct);
        var summe = Convert.ToHexStringLower(SHA256.HashData(zip));
        foreach (var a in liste)
        {
            a.Pruefsumme = summe;
        }

        await db.SaveChangesAsync(ct);
        return File(zip, "application/zip", $"archivpaket-{Anzeige.LokalesDatum(jetzt):yyyy-MM-dd}.zip");
    }

    /// <summary>Bestätigt die Übergabe des Archivpakets; danach darf der Löschlauf die Unterlagen löschen.</summary>
    /// <param name="ids">Markierte Angebote.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung.</returns>
    public async Task<IActionResult> OnPostUebergebenAsync(List<Guid> ids, CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        var liste = await db.Archivangebote
            .Where(a => ids.Contains(a.Id) && a.ErledigtAm == null && a.Entscheidung == Archiventscheidung.Uebernehmen && a.Pruefsumme != null && a.UebergebenAm == null)
            .ToListAsync(ct);
        foreach (var a in liste)
        {
            a.UebergebenAm = Heute;
        }

        await db.SaveChangesAsync(ct);
        TempData[liste.Count == 0 ? "Fehler" : "Meldung"] = liste.Count == 0
            ? "Übergabe geht nur für übernommene Unterlagen, deren Archivpaket schon erstellt ist."
            : $"Übergabe von {liste.Count} {(liste.Count == 1 ? "Unterlage" : "Unterlagen")} vermerkt. Der nächste Löschlauf löscht sie in FEVAD.";
        return RedirectToPage();
    }

    /// <summary>
    /// Speichert Archivstelle, Entscheidungsfrist, Aufbewahrungsfristen und Bewertung je Unterlage als neue Parameterwerte ab heute
    /// (Andi, 26.10.2026: Annahmen einstellbar, bis das Archiv bestätigt).
    /// </summary>
    /// <param name="stelle">Zuständiges Archiv.</param>
    /// <param name="fristMonate">Entscheidungsfrist des Archivs in Monaten.</param>
    /// <param name="aufbewahrung">Aufbewahrungsfrist je Art der Unterlage.</param>
    /// <param name="bewertung">„archivwürdig“ oder „kassabel“ je Art der Unterlage.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostEinstellungenAsync(
        string? stelle, int fristMonate, Dictionary<string, int> aufbewahrung, Dictionary<string, string> bewertung, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(stelle) || stelle.Length > 200)
        {
            ModelState.AddModelError(string.Empty, "Bitte das zuständige Archiv angeben (höchstens 200 Zeichen).");
        }

        if (fristMonate is < 1 or > 60)
        {
            ModelState.AddModelError(string.Empty, "Die Entscheidungsfrist muss zwischen 1 und 60 Monaten liegen.");
        }

        foreach (var b in Archivierung.Katalog)
        {
            if (!aufbewahrung.TryGetValue(b.Art.ToString(), out var jahre) || jahre is < 1 or > 100)
            {
                ModelState.AddModelError(string.Empty, $"{Archivierung.Anzeigename(b.Art)}: Aufbewahrung zwischen 1 und 100 eintragen.");
            }
        }

        if (!ModelState.IsValid)
        {
            EinstellungenOffen = true;
            await LadenAsync(ct);
            return Page();
        }

        Heute = Anzeige.Heute(zeit);
        var geaendert = 0;
        geaendert += await SetzenAsync(ParameterSchluessel.ArchivStelle, stelle!.Trim(), ct);
        geaendert += await SetzenAsync(ParameterSchluessel.ArchivEntscheidungFristMonate, fristMonate.ToString(CultureInfo.InvariantCulture), ct);
        foreach (var b in Archivierung.Katalog)
        {
            geaendert += await SetzenAsync(b.FristSchluessel, aufbewahrung[b.Art.ToString()].ToString(CultureInfo.InvariantCulture), ct);
            var wert = bewertung.TryGetValue(b.Art.ToString(), out var w) && w == Archivierung.Kassabel ? Archivierung.Kassabel : "archivwürdig";
            geaendert += await SetzenAsync(ParameterSchluessel.ArchivBewertungPraefix + b.Art, wert, ct);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = geaendert == 0 ? "Keine Änderung." : $"{geaendert} {(geaendert == 1 ? "Einstellung" : "Einstellungen")} ab heute geändert.";
        return RedirectToPage();
    }

    private async Task<int> SetzenAsync(string schluessel, string wert, CancellationToken ct)
    {
        if (await parameter.WertAsync(schluessel, Heute, ct) == wert)
        {
            return 0;
        }

        // Mehrere Änderungen am selben Tag überschreiben den Wert von heute.
        var vonHeute = await db.Parameter.FirstOrDefaultAsync(p => p.Schluessel == schluessel && p.GueltigAb == Heute, ct);
        if (vonHeute is null)
        {
            db.Parameter.Add(new Parameter { Schluessel = schluessel, Wert = wert, GueltigAb = Heute, Quelle = "Einstellung in FEVAD (Archivanbietung)" });
        }
        else
        {
            vonHeute.Wert = wert;
        }

        return 1;
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        Stelle = await parameter.WertAsync(ParameterSchluessel.ArchivStelle, Heute, ct) ?? "Archiv";
        FristMonate = await parameter.ZahlAsync(ParameterSchluessel.ArchivEntscheidungFristMonate, Heute, 12, ct);
        var katalog = new List<Katalogzeile>();
        foreach (var b in Archivierung.Katalog)
        {
            katalog.Add(new(b, await parameter.WertAsync(ParameterSchluessel.ArchivBewertungPraefix + b.Art, Heute, ct) ?? "archivwürdig",
                await parameter.WertAsync(b.FristSchluessel, Heute, ct)));
        }

        Katalog = katalog;
        Offene = await db.Archivangebote.AsNoTracking().Where(a => a.ErledigtAm == null)
            .OrderBy(a => a.Entscheidung).ThenBy(a => a.Art).ThenBy(a => a.Jahr).ThenBy(a => a.Bezeichnung).ToListAsync(ct);
        Erledigte = await db.Archivangebote.AsNoTracking().Where(a => a.ErledigtAm != null).OrderByDescending(a => a.ErledigtAm).Take(50).ToListAsync(ct);
    }

    private static string Feld(string text) => text.IndexOfAny([';', '"']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
}
