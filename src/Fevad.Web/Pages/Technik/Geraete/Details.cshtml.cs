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

    /// <summary>Offener Werkstattauftrag, in dem das Gerät gerade ist (V61).</summary>
    public (Guid Id, string Nummer, DateOnly AbgegebenAm)? Werkstatt { get; private set; }

    /// <summary>Bauteildaten sind eingeschaltet und für die Geräteart gibt es Bauteilarten oder schon Bauteile.</summary>
    public bool BauteileAnzeigen { get; private set; }

    /// <summary>Pflege-Lebenslauf (V63), neueste zuerst.</summary>
    public IReadOnlyList<PflegeZeile> Pflege { get; private set; } = [];

    /// <summary>Wäschen gegen die Herstellergrenze der Geräteart.</summary>
    public Pflegestand Pflegestand { get; private set; } = new(0, null);

    /// <summary>Offene Ausleihe des Geräts (V63).</summary>
    public Ausleihe? Ausleihe { get; private set; }

    /// <summary>Dokumente zum Gerät oder zu seiner Geräteart, die der Benutzer lesen darf (V63).</summary>
    public IReadOnlyList<(Guid Id, string Titel, bool ZurArt)> Dokumente { get; private set; } = [];

    /// <summary>Neuer Eintrag im Pflege-Lebenslauf.</summary>
    [BindProperty]
    public PflegeEingabe NeuePflege { get; set; } = new();

    /// <summary>Ein Eintrag im Pflege-Lebenslauf.</summary>
    /// <param name="Id">Kennung des Eintrags.</param>
    /// <param name="Datum">Tag der Maßnahme.</param>
    /// <param name="Art">Art der Maßnahme.</param>
    /// <param name="Einsatznummer">Einsatz, nach dem gepflegt wurde.</param>
    /// <param name="Durchfuehrung">Firma oder Stelle.</param>
    /// <param name="Bemerkung">Bemerkung.</param>
    public sealed record PflegeZeile(Guid Id, DateOnly Datum, Pflegeart Art, string? Einsatznummer, string? Durchfuehrung, string? Bemerkung);

    /// <summary>Formular für einen Pflegeeintrag.</summary>
    public sealed class PflegeEingabe
    {
        /// <summary>Tag der Maßnahme; leer heißt heute.</summary>
        [Display(Name = "Datum")] public DateOnly? Datum { get; set; }

        /// <summary>Art der Maßnahme.</summary>
        [Display(Name = "Maßnahme")] public Pflegeart Art { get; set; } = Pflegeart.Waesche;

        /// <summary>Firma oder Stelle, keine Namen.</summary>
        [StringLength(100), Display(Name = "Durchgeführt von (Firma oder Stelle)")] public string? Durchfuehrung { get; set; }

        /// <summary>Bemerkung (Sachstand).</summary>
        [StringLength(200), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

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

    /// <summary>Trägt eine Pflegemaßnahme in den Lebenslauf ein (V63).</summary>
    public async Task<IActionResult> OnPostPflegeAsync(Guid id, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded)
        {
            return Forbid();
        }

        if (!await db.Geraete.AnyAsync(g => g.Id == id && g.AusgesondertAm == null, ct))
        {
            return NotFound();
        }

        var heute = Anzeige.Heute(zeit);
        if (NeuePflege.Datum > heute)
        {
            ModelState.AddModelError("NeuePflege.Datum", "Das Datum liegt in der Zukunft.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(id, ct);
            return Page();
        }

        db.Pflegeeintraege.Add(new Pflegeeintrag
        {
            GeraetId = id,
            Datum = NeuePflege.Datum ?? heute,
            Art = NeuePflege.Art,
            Durchfuehrung = string.IsNullOrWhiteSpace(NeuePflege.Durchfuehrung) ? null : NeuePflege.Durchfuehrung.Trim(),
            Bemerkung = string.IsNullOrWhiteSpace(NeuePflege.Bemerkung) ? null : NeuePflege.Bemerkung.Trim(),
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{Anzeige.Text(NeuePflege.Art)} eingetragen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Entfernt einen falsch erfassten Pflegeeintrag.</summary>
    public async Task<IActionResult> OnPostPflegeLoeschenAsync(Guid id, Guid eintragId, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var eintrag = await db.Pflegeeintraege.FirstOrDefaultAsync(p => p.Id == eintragId && p.GeraetId == id, ct);
        if (eintrag is null)
        {
            return NotFound();
        }

        db.Pflegeeintraege.Remove(eintrag);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Pflegeeintrag entfernt.";
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
        Werkstatt = (await db.Werkstattpositionen.AsNoTracking()
                .Where(p => p.GeraetId == id && p.Werkstattauftrag!.ZurueckAm == null)
                .Select(p => new { p.WerkstattauftragId, p.Werkstattauftrag!.Nummer, p.Werkstattauftrag.AbgegebenAm })
                .ToListAsync(ct))
            .Select(w => ((Guid, string, DateOnly)?)(w.WerkstattauftragId, w.Nummer, w.AbgegebenAm)).FirstOrDefault();
        Ausleihe = await db.Ausleihen.AsNoTracking().FirstOrDefaultAsync(a => a.GeraetId == id && a.ZurueckAm == null, ct);
        if ((await autorisierung.AuthorizeAsync(User, Berechtigungen.DokumenteLesen)).Succeeded)
        {
            var vertraulich = (await autorisierung.AuthorizeAsync(User, Berechtigungen.DokumenteVertraulichLesen)).Succeeded;
            Dokumente = (await db.Dokumente.AsNoTracking()
                    .Where(d => d.AufgehobenAm == null && (d.GeraetId == id || d.GeraeteartId == geraet.GeraeteartId))
                    .Where(d => vertraulich || d.Sichtbarkeit == Domain.Dokumente.DokumentSichtbarkeit.Alle)
                    .OrderBy(d => d.GeraetId == null).ThenBy(d => d.Titel)
                    .Select(d => new { d.Id, d.Titel, ZurArt = d.GeraetId == null })
                    .ToListAsync(ct))
                .Select(d => (d.Id, d.Titel, d.ZurArt)).ToList();
        }

        Pflege = await db.Pflegeeintraege.AsNoTracking().Where(p => p.GeraetId == id)
            .OrderByDescending(p => p.Datum).ThenByDescending(p => p.ErstelltAm)
            .Select(p => new PflegeZeile(p.Id, p.Datum, p.Art, p.Einsatz != null ? p.Einsatz.Einsatznummer : null, p.Durchfuehrung, p.Bemerkung))
            .ToListAsync(ct);
        Pflegestand = new Pflegestand(Pflege.Count(p => p.Art == Pflegeart.Waesche), geraet.Geraeteart?.HoechstWaeschen);
        BauteileAnzeigen = await parameter.ZahlAsync(Domain.Gemeinsam.ParameterSchluessel.BauteildatenAktiv, heute, 0, ct) == 1
            && (await db.Bauteilarten.AnyAsync(b => b.GeraeteartId == geraet.GeraeteartId, ct) || await db.Bauteile.AnyAsync(b => b.GeraetId == id, ct));
        return true;
    }
}
