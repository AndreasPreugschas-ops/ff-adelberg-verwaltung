using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraete;

/// <summary>Geräteakte mit Stammdaten, Prüfbuch, Mängeln, Anlagendaten und Aussonderung.</summary>
public class DetailsModel(AppDbContext db, Pruefstaende pruefstaende, Geraetecode geraetecode, IAuthorizationService autorisierung, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Das Gerät.</summary>
    public Geraet Geraet { get; private set; } = null!;
    /// <summary>Prüfstand des Geräts.</summary>
    public Pruefstand Stand { get; private set; } = null!;
    /// <summary>Prüfbuch des Geräts.</summary>
    public IReadOnlyList<PruefbuchZeile> Pruefbuch { get; private set; } = [];
    /// <summary>Offene Mängel am Gerät.</summary>
    public IReadOnlyList<MangelZeile> Maengel { get; private set; } = [];
    /// <summary>QR-Code zur Mängelmeldung als SVG.</summary>
    public string QrCodeSvg { get; private set; } = "";
    /// <summary>Adresse, auf die der QR-Code zeigt.</summary>
    public string QrAdresse { get; private set; } = "";

    /// <summary>Formular der Aussonderung bzw. Veräußerung (V60).</summary>
    [BindProperty]
    public AussonderungEingabe Aussonderung { get; set; } = new();

    /// <summary>Anschaffungswert erreicht die Grenze, ab der ein Beschluss nötig ist.</summary>
    public bool BeschlussNoetig { get; private set; }

    /// <summary>Grenze für den Beschluss in Euro.</summary>
    public int BeschlussGrenze { get; private set; }

    /// <summary>Das Gerät wartet noch auf die Eingangsprüfung (V60).</summary>
    public bool EingangspruefungOffen { get; private set; }

    /// <summary>Fördermittel mit laufender Zweckbindung: Fördergeber informieren.</summary>
    public bool FoerdergeberMitteilen { get; private set; }

    /// <summary>Angaben zur Aussonderung und Verwertung.</summary>
    public sealed class AussonderungEingabe
    {
        /// <summary>Tag der Aussonderung; leer heißt heute.</summary>
        [Display(Name = "Ausgesondert am")] public DateOnly? Datum { get; set; }

        /// <summary>Begründung.</summary>
        [StringLength(200), Display(Name = "Grund der Aussonderung")] public string? Grund { get; set; }

        /// <summary>Was mit dem Gerät geschieht.</summary>
        [Display(Name = "Verwertung")] public Verwertung? Verwertung { get; set; }

        /// <summary>Verkaufserlös.</summary>
        [Range(0, 9999999), Display(Name = "Erlös (€)")] public decimal? Erloes { get; set; }

        /// <summary>Verweis auf den Beschluss.</summary>
        [StringLength(200), Display(Name = "Beschluss (Gremium, Datum, TOP)")] public string? Beschluss { get; set; }

        /// <summary>Mitteilung an den Fördergeber.</summary>
        [Display(Name = "Fördergeber informiert am")] public DateOnly? FoerdergeberMitgeteiltAm { get; set; }
    }

    /// <summary>Zeigt das Gerät mit Prüfbuch, Mängeln und QR-Code.</summary>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Aussonderung = new AussonderungEingabe
        {
            Datum = Geraet.AusgesondertAm,
            Grund = Geraet.Aussonderungsgrund,
            Verwertung = Geraet.Verwertung,
            Erloes = Geraet.Erloes,
            Beschluss = Geraet.Aussonderungsbeschluss,
            FoerdergeberMitgeteiltAm = Geraet.FoerdergeberMitgeteiltAm,
        };
        return Page();
    }

    /// <summary>Sondert das Gerät aus oder ergänzt die Angaben zur Verwertung; Beschluss und Mitteilung sind Hinweise, keine Sperre.</summary>
    public async Task<IActionResult> OnPostAussondernAsync(Guid id, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var geraet = await db.Geraete.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (geraet is null)
        {
            return NotFound();
        }

        foreach (var (feld, meldung) in Aussonderungsregeln.Pruefen(Aussonderung.Verwertung, Aussonderung.Erloes, Aussonderung.Grund))
        {
            ModelState.AddModelError($"{nameof(Aussonderung)}.{feld}", meldung);
        }

        var heute = Anzeige.Heute(zeit);
        if (Aussonderung.Datum > heute)
        {
            ModelState.AddModelError("Aussonderung.Datum", "Das Datum liegt in der Zukunft.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(id, ct);
            return Page();
        }

        var neu = geraet.AusgesondertAm is null;
        geraet.AusgesondertAm = Aussonderung.Datum ?? geraet.AusgesondertAm ?? heute;
        geraet.Aussonderungsgrund = Aussonderung.Grund!.Trim();
        geraet.Verwertung = Aussonderung.Verwertung;
        geraet.Erloes = Aussonderung.Erloes;
        geraet.Aussonderungsbeschluss = string.IsNullOrWhiteSpace(Aussonderung.Beschluss) ? null : Aussonderung.Beschluss.Trim();
        geraet.FoerdergeberMitgeteiltAm = Aussonderung.FoerdergeberMitgeteiltAm;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = neu ? $"{geraet.Inventarnummer} ist ausgesondert." : $"Aussonderung von {geraet.Inventarnummer} gespeichert.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var geraet = await db.Geraete.AsNoTracking()
            .Include(g => g.Geraeteart)
            .Include(g => g.Material)
            .Include(g => g.Inventurgruppe)
            .Include(g => g.Fahrzeug)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (geraet is null)
        {
            return false;
        }

        Geraet = geraet;
        Stand = (await pruefstaende.GeraeteAsync(Anzeige.Heute(zeit), geraetId: id, ct: ct)).Single();
        Pruefbuch = await Ansichten.PruefbuchAsync(db, id, null, ct);
        Maengel = await Ansichten.Maengel(db.Maengel.Where(m => m.GeraetId == id)).ToListAsync(ct);
        QrAdresse = geraetecode.Adresse(Request, geraet.Inventarnummer);
        QrCodeSvg = Geraetecode.Svg(QrAdresse);
        EingangspruefungOffen = await db.Maengel.AnyAsync(m => m.GeraetId == id && m.SperreBisPruefung && m.EinsatzId == null && m.Status != MangelStatus.Behoben, ct);
        var heute = Anzeige.Heute(zeit);
        BeschlussGrenze = await parameter.ZahlAsync(Domain.Gemeinsam.ParameterSchluessel.AussonderungBeschlussAbWertEuro, heute, 1000, ct);
        BeschlussNoetig = Aussonderungsregeln.BeschlussNoetig(geraet.Anschaffungspreis, BeschlussGrenze);
        FoerdergeberMitteilen = Aussonderungsregeln.FoerdergeberMitteilen(geraet, geraet.AusgesondertAm ?? heute);
        return true;
    }
}
