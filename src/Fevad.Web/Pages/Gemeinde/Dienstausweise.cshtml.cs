using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Gemeinde;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gemeinde;

/// <summary>
/// Dienstausweise (Satzung § 3 Abs. 6, V27): ausstellen und drucken mit Lichtbild, das nur für den Druck hochgeladen und nicht gespeichert wird;
/// Gültigkeit aus dem Parameter, Rückgabe beim Ausscheiden. Angehörige nur der Jugend- oder Kinderfeuerwehr erscheinen hier nicht.
/// </summary>
[Authorize(Policy = Berechtigungen.Dienstausweise)]
public class DienstausweiseModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Höchstgröße des Lichtbilds in Bytes.</summary>
    public const int LichtbildMaxBytes = 5 * 1024 * 1024;

    /// <summary>Eine Zeile der Liste.</summary>
    /// <param name="MitgliedId">Kennung des Mitglieds.</param>
    /// <param name="Name">„Nachname, Vorname“.</param>
    /// <param name="Personalnummer">Personalnummer.</param>
    /// <param name="Aktiv">Das Mitglied hat eine laufende Mitgliedschaft.</param>
    /// <param name="Ausweis">Ausgegebener Ausweis oder null.</param>
    public sealed record Zeile(Guid MitgliedId, string Name, string Personalnummer, bool Aktiv, Dienstausweis? Ausweis);

    /// <summary>Aktive Mitglieder mit ihrem ausgegebenen Ausweis.</summary>
    public IReadOnlyList<Zeile> Mitglieder { get; private set; } = [];

    /// <summary>Ausgeschiedene, deren Ausweis noch nicht zurück ist.</summary>
    public IReadOnlyList<Zeile> RueckgabeOffen { get; private set; } = [];

    /// <summary>Heute.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Vorschlag für „gültig bis“ bei neuen Ausweisen.</summary>
    [BindProperty, Display(Name = "Gültig bis")]
    public DateOnly? GueltigBis { get; set; }

    /// <summary>Lichtbild für den Druck (JPEG oder PNG).</summary>
    [BindProperty]
    public IFormFile? Lichtbild { get; set; }

    /// <summary>Lädt die Liste.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    /// <summary>Stellt einen neuen Ausweis aus, nimmt einen ausgegebenen als ersetzt zurück und liefert das PDF.</summary>
    /// <param name="mitgliedId">Kennung des Mitglieds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF, die Seite mit Fehler oder 404.</returns>
    public async Task<IActionResult> OnPostAusstellenAsync(Guid mitgliedId, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        if (!await Auswahl.WaehlbarAsync(db, heute, mitgliedId, null, ct))
        {
            return NotFound();
        }

        var (bild, fehler) = await LichtbildLesenAsync(ct);
        if (fehler is not null || GueltigBis is not { } bis || bis <= heute)
        {
            TempData["Fehler"] = fehler ?? "Bitte ein Gültigkeitsdatum in der Zukunft angeben.";
            return RedirectToPage();
        }

        var mitglied = await db.Mitglieder.AsNoTracking().FirstAsync(m => m.Id == mitgliedId, ct);
        var bisher = await db.Dienstausweise.Where(d => d.MitgliedId == mitgliedId).ToListAsync(ct);
        foreach (var alt in bisher.Where(d => d.Ausgegeben))
        {
            alt.ZurueckAm = heute;
            alt.Vermerk ??= "ersetzt durch neuen Ausweis";
        }

        var ausweis = new Dienstausweis { MitgliedId = mitgliedId, Nummer = Dienstausweis.NaechsteNummer(mitglied.Personalnummer, bisher.Count), AusgestelltAm = heute, GueltigBis = bis };
        db.Dienstausweise.Add(ausweis);
        await db.SaveChangesAsync(ct);
        return await PdfAsync(mitglied, ausweis, bild, ct);
    }

    /// <summary>Druckt einen ausgegebenen Ausweis erneut, z. B. mit neuem Lichtbild.</summary>
    /// <param name="id">Kennung des Ausweises.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnPostDruckenAsync(Guid id, CancellationToken ct)
    {
        var ausweis = await db.Dienstausweise.AsNoTracking().Include(d => d.Mitglied).FirstOrDefaultAsync(d => d.Id == id && d.ZurueckAm == null, ct);
        if (ausweis is null || !await Auswahl.WaehlbarAsync(db, Anzeige.Heute(zeit), ausweis.MitgliedId, null, ct))
        {
            return NotFound();
        }

        var (bild, fehler) = await LichtbildLesenAsync(ct);
        if (fehler is not null)
        {
            TempData["Fehler"] = fehler;
            return RedirectToPage();
        }

        return await PdfAsync(ausweis.Mitglied!, ausweis, bild, ct);
    }

    /// <summary>Vermerkt die Rückgabe oder den Verlust eines Ausweises.</summary>
    /// <param name="id">Kennung des Ausweises.</param>
    /// <param name="vermerk">Vermerk, z. B. „verloren gemeldet“.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostRueckgabeAsync(Guid id, string? vermerk, CancellationToken ct)
    {
        var ausweis = await db.Dienstausweise.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (ausweis is null)
        {
            return NotFound();
        }

        ausweis.ZurueckAm ??= Anzeige.Heute(zeit);
        ausweis.Vermerk = string.IsNullOrWhiteSpace(vermerk) ? ausweis.Vermerk : vermerk.Trim()[..Math.Min(vermerk.Trim().Length, 300)];
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Rückgabe von Ausweis {ausweis.Nummer} vermerkt.";
        return RedirectToPage();
    }

    private async Task<IActionResult> PdfAsync(Mitglied m, Dienstausweis ausweis, byte[]? bild, CancellationToken ct)
    {
        var o = await db.OrganisationAsync(ct);
        var daten = new GemeindePdf.Ausweisdaten(o.GemeindeName, o.FeuerwehrName, o.Logo, o.LogoInhaltstyp == AntragPdf.SvgInhaltstyp,
            $"{m.Vorname} {m.Nachname}", m.Geburtsdatum, ausweis, bild, o.GemeindeVertretung);
        return File(GemeindePdf.Dienstausweis(daten), "application/pdf", $"Dienstausweis-{ausweis.Nummer}.pdf");
    }

    private async Task<(byte[]? Bild, string? Fehler)> LichtbildLesenAsync(CancellationToken ct)
    {
        if (Lichtbild is null || Lichtbild.Length == 0)
        {
            return (null, null);
        }

        if (Lichtbild.Length > LichtbildMaxBytes)
        {
            return (null, "Das Lichtbild ist größer als 5 MB.");
        }

        using var speicher = new MemoryStream();
        await Lichtbild.CopyToAsync(speicher, ct);
        var bytes = speicher.ToArray();
        var jpeg = bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        var png = bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        return jpeg || png ? (bytes, null) : (null, "Bitte ein Lichtbild als JPEG oder PNG hochladen.");
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        var heute = Heute;
        GueltigBis = heute.AddYears(await parameter.ZahlAsync(ParameterSchluessel.DienstausweisGueltigkeitJahre, heute, 5, ct));
        var aktiv = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && s.Abteilung!.Art != AbteilungArt.Jugend && s.Abteilung.Art != AbteilungArt.Kinder))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, Name = m.Nachname + ", " + m.Vorname, m.Personalnummer })
            .ToListAsync(ct);
        var ausgegeben = await db.Dienstausweise.AsNoTracking().Include(d => d.Mitglied).Where(d => d.ZurueckAm == null).ToListAsync(ct);
        var nachMitglied = ausgegeben.GroupBy(d => d.MitgliedId).ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.AusgestelltAm).First());
        var aktivIds = aktiv.Select(m => m.Id).ToHashSet();
        Mitglieder = aktiv.Select(m => new Zeile(m.Id, m.Name, m.Personalnummer, true, nachMitglied.GetValueOrDefault(m.Id))).ToList();
        // Ausweise Ausgeschiedener; Jugendliche haben hier nie einen Ausweis bekommen, weil nur wählbare Mitglieder einen erhalten.
        RueckgabeOffen = ausgegeben.Where(d => !aktivIds.Contains(d.MitgliedId))
            .Select(d => new Zeile(d.MitgliedId, $"{d.Mitglied!.Nachname}, {d.Mitglied.Vorname}", d.Mitglied.Personalnummer, false, d))
            .OrderBy(z => z.Name).ToList();
    }
}
