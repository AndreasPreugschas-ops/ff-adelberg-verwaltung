using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Unfaelle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Maengel;

/// <summary>
/// Mangel ansehen und bearbeiten: Status, Schwere, Maßnahme, Werkstatt, Kosten und Garantie, Fotos sowie Ersatzgerät im Fach
/// und der Schritt „wieder verladen“ (V63).
/// </summary>
public class BearbeitenModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit, ParameterLeser parameter, Fahrzeugausfall fahrzeugausfall) : PageModel
{
    /// <summary>Lagerort des defekten Geräts, solange das Ersatzgerät in seinem Fach liegt.</summary>
    public const string AblageDefekt = "Werkstatt";

    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Neue Fotos zum Mangel.</summary>
    [BindProperty]
    public List<IFormFile> Fotos { get; set; } = [];

    /// <summary>Der Mangel.</summary>
    public MangelZeile Mangel { get; private set; } = null!;
    /// <summary>Betroffenes Gerät.</summary>
    public Guid? GeraetId { get; private set; }
    /// <summary>Betroffenes Fahrzeug.</summary>
    public Guid? FahrzeugId { get; private set; }
    /// <summary>Darf Mängel bearbeiten.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Beschaffung, die zu diesem Mangel angelegt wurde.</summary>
    public Guid? BeschaffungId { get; private set; }
    /// <summary>Gespeicherte Fotos (Kennung und Name).</summary>
    public IReadOnlyList<(Guid Id, string Dateiname)> Fotoliste { get; private set; } = [];
    /// <summary>Zeitpunkt, zu dem der Kommandant über den Fahrzeugausfall informiert wurde.</summary>
    public DateTimeOffset? KommandantInformiertAm { get; private set; }
    /// <summary>Stand von Ersatzgerät und Wiederverladen; leer bei Fahrzeugmängeln.</summary>
    public Ersatzstand? Ersatz { get; private set; }
    /// <summary>Mögliche Ersatzgeräte derselben Geräteart.</summary>
    public IReadOnlyList<SelectListItem> Ersatzgeraete { get; private set; } = [];

    /// <summary>Wo das defekte Gerät hingehört und was als Ersatz im Fach liegt.</summary>
    /// <param name="Fahrzeug">Fahrzeug des defekten Geräts bzw. sein Ursprung.</param>
    /// <param name="Fach">Fach des defekten Geräts bzw. sein Ursprung.</param>
    /// <param name="ErsatzId">Eingesetztes Ersatzgerät.</param>
    /// <param name="ErsatzName">Inventarnummer und Bezeichnung des Ersatzgeräts.</param>
    /// <param name="Eingesetzt">Ersatz liegt noch im Fach.</param>
    /// <param name="WiederVerladenAm">Tag des Wiederverladens.</param>
    public sealed record Ersatzstand(string? Fahrzeug, string? Fach, Guid? ErsatzId, string? ErsatzName, bool Eingesetzt, DateOnly? WiederVerladenAm);

    /// <summary>Formular eines Mangels.</summary>
    public sealed class Eingabe
    {
        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }
        /// <summary>Bearbeitungsstand.</summary>
        public MangelStatus Status { get; set; }
        /// <summary>Schwere.</summary>
        public MangelSchwere Schwere { get; set; }

        /// <summary>Was getan wurde oder getan werden soll.</summary>
        [StringLength(2000), Display(Name = "Maßnahme")]
        public string? Massnahme { get; set; }

        /// <summary>Wer den Mangel behebt.</summary>
        [Display(Name = "Werkstatt")]
        public MangelWerkstatt Werkstatt { get; set; }

        /// <summary>Name der fremden Werkstatt oder Fachfirma.</summary>
        [StringLength(200), Display(Name = "Name der Werkstatt / Fachfirma")]
        public string? Werkstattname { get; set; }

        /// <summary>Kosten brutto.</summary>
        [Range(0, 1_000_000, ErrorMessage = "Bitte einen Betrag zwischen 0 und 1.000.000 € eingeben."), Display(Name = "Kosten brutto (€)")]
        public decimal? Kosten { get; set; }

        /// <summary>Behebung über Garantie oder Gewährleistung.</summary>
        [Display(Name = "Garantie- bzw. Gewährleistungsfall")]
        public bool Garantiefall { get; set; }

        /// <summary>Ende der Garantie.</summary>
        [Display(Name = "Garantie bis")]
        public DateOnly? GarantieBis { get; set; }
    }

    /// <summary>Zeigt den Mangel.</summary>
    /// <param name="id">Kennung des Mangels.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var mangel = await db.Maengel.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mangel is null)
        {
            return NotFound();
        }

        Formular = new Eingabe
        {
            Version = db.Entry(mangel).Property<uint>("Version").CurrentValue,
            Status = mangel.Status,
            Schwere = mangel.Schwere,
            Massnahme = mangel.Massnahme,
            Werkstatt = mangel.Werkstatt,
            Werkstattname = mangel.Werkstattname,
            Kosten = mangel.Kosten,
            Garantiefall = mangel.Garantiefall,
            GarantieBis = mangel.GarantieBis,
        };
        await LadenAsync(id, ct);
        return Page();
    }

    /// <summary>Speichert den Mangel; „behoben“ setzt den Zeitpunkt der Behebung. Neue Fotos werden angehängt.</summary>
    /// <param name="id">Kennung des Mangels.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Liste, bei Fehlern die Seite, sonst 403/404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var mangel = await db.Maengel.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mangel is null)
        {
            return NotFound();
        }

        var fotos = await Meldungsauswahl.FotosLesenAsync(Fotos, parameter, Anzeige.Heute(zeit), ModelState, nameof(Fotos), ct);
        if (!ModelState.IsValid)
        {
            await LadenAsync(id, ct);
            return Page();
        }

        db.Entry(mangel).Property<uint>("Version").OriginalValue = Formular.Version;
        mangel.Schwere = Formular.Schwere;
        mangel.Massnahme = Formular.Massnahme;
        mangel.Werkstatt = Formular.Werkstatt;
        mangel.Werkstattname = Formular.Werkstatt == MangelWerkstatt.Extern && !string.IsNullOrWhiteSpace(Formular.Werkstattname) ? Formular.Werkstattname.Trim() : null;
        mangel.Kosten = Formular.Kosten;
        mangel.Garantiefall = Formular.Garantiefall;
        mangel.GarantieBis = Formular.GarantieBis;
        if (mangel.Status != Formular.Status)
        {
            mangel.Status = Formular.Status;
            mangel.BehobenAm = Formular.Status == MangelStatus.Behoben ? zeit.GetUtcNow() : null;
        }

        foreach (var f in fotos)
        {
            db.Mangelfotos.Add(new Mangelfoto { MangelId = id, Dateiname = f.Dateiname, Inhaltstyp = f.Inhaltstyp, Inhalt = f.Inhalt });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Der Mangel wurde inzwischen von jemand anderem geändert. Bitte neu laden.");
            await LadenAsync(id, ct);
            return Page();
        }

        await fahrzeugausfall.OffeneMeldenAsync(ct);
        TempData["Meldung"] = mangel.Status == MangelStatus.Behoben && mangel.ErsatzEingesetzt
            ? "Mangel gespeichert. Das reparierte Gerät noch wieder verladen."
            : "Mangel gespeichert.";
        return mangel.ErsatzEingesetzt ? RedirectToPage(new { id }) : RedirectToPage("Index");
    }

    /// <summary>Legt ein Ersatzgerät in das Fach des defekten Geräts; das defekte geht in die Werkstatt.</summary>
    /// <param name="id">Kennung des Mangels.</param>
    /// <param name="ersatzId">Kennung des Ersatzgeräts.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Seite oder 403/404.</returns>
    public async Task<IActionResult> OnPostErsatzAsync(Guid id, Guid ersatzId, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var mangel = await db.Maengel.FirstOrDefaultAsync(m => m.Id == id, ct);
        var defekt = mangel?.GeraetId is { } g ? await db.Geraete.FirstOrDefaultAsync(x => x.Id == g, ct) : null;
        if (mangel is null || defekt is null)
        {
            return NotFound();
        }

        if (!(await ErsatzAbfrage(defekt).Select(x => x.Id).ToListAsync(ct)).Contains(ersatzId) || defekt.FahrzeugId is null || mangel.ErsatzEingesetzt)
        {
            TempData["Fehler"] = "Dieses Ersatzgerät passt nicht oder es steckt schon ein Ersatz im Fach.";
            return RedirectToPage(new { id });
        }

        var ersatz = await db.Geraete.SingleAsync(x => x.Id == ersatzId, ct);
        mangel.ErsatzEinsetzen(defekt, ersatz, AblageDefekt);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{ersatz.Inventarnummer} liegt jetzt in Fach {ersatz.Lagerort}; {defekt.Inventarnummer} ist in der Werkstatt.";
        return RedirectToPage(new { id });
    }

    /// <summary>Schritt „wieder verladen“: das reparierte Gerät zurück ins Fach, das Ersatzgerät an seinen alten Platz.</summary>
    /// <param name="id">Kennung des Mangels.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Seite oder 403/404.</returns>
    public async Task<IActionResult> OnPostWiederVerladenAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var mangel = await db.Maengel.FirstOrDefaultAsync(m => m.Id == id, ct);
        var defekt = mangel?.GeraetId is { } g ? await db.Geraete.FirstOrDefaultAsync(x => x.Id == g, ct) : null;
        if (mangel is null || defekt is null)
        {
            return NotFound();
        }

        if (!mangel.ErsatzEingesetzt)
        {
            return RedirectToPage(new { id });
        }

        var ersatz = await db.Geraete.FirstOrDefaultAsync(x => x.Id == mangel.ErsatzGeraetId && x.AusgesondertAm == null, ct);
        mangel.WiederVerladen(defekt, ersatz, Anzeige.Heute(zeit));
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{defekt.Inventarnummer} ist wieder verladen" + (ersatz is null ? "." : $", {ersatz.Inventarnummer} liegt wieder an seinem alten Platz.");
        return RedirectToPage(new { id });
    }

    /// <summary>Zeigt ein Foto.</summary>
    /// <param name="id">Kennung des Mangels.</param>
    /// <param name="fotoId">Kennung des Fotos.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das Bild oder 404.</returns>
    public async Task<IActionResult> OnGetFotoAsync(Guid id, Guid fotoId, CancellationToken ct)
    {
        var foto = await db.Mangelfotos.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fotoId && f.MangelId == id, ct);
        return foto is null ? NotFound() : File(foto.Inhalt, foto.Inhaltstyp);
    }

    /// <summary>Löscht ein Foto, z. B. wenn darauf eine Person zu sehen ist.</summary>
    /// <param name="id">Kennung des Mangels.</param>
    /// <param name="fotoId">Kennung des Fotos.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Seite oder 403.</returns>
    public async Task<IActionResult> OnPostFotoLoeschenAsync(Guid id, Guid fotoId, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var foto = await db.Mangelfotos.FirstOrDefaultAsync(f => f.Id == fotoId && f.MangelId == id, ct);
        if (foto is not null)
        {
            db.Mangelfotos.Remove(foto);
            await db.SaveChangesAsync(ct);
            TempData["Meldung"] = "Foto gelöscht.";
        }

        return RedirectToPage(new { id });
    }

    private async Task<bool> DarfAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded;

    /// <summary>Ersatz: gleiche Geräteart, nicht ausgesondert, ohne offenen Mangel „außer Betrieb“ und nicht in einer Werkstatt.</summary>
    private IQueryable<Geraet> ErsatzAbfrage(Geraet defekt) =>
        db.Geraete.Where(x => x.Id != defekt.Id && x.GeraeteartId == defekt.GeraeteartId && x.AusgesondertAm == null
            && !db.Maengel.Any(m => m.GeraetId == x.Id && m.Status != MangelStatus.Behoben && m.Schwere == MangelSchwere.AusserBetrieb)
            && !db.Werkstattpositionen.Any(p => p.GeraetId == x.Id && p.Werkstattauftrag!.ZurueckAm == null));

    private async Task LadenAsync(Guid id, CancellationToken ct)
    {
        Mangel = await Ansichten.Maengel(db.Maengel.Where(m => m.Id == id)).SingleAsync(ct);
        var m = await db.Maengel.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        GeraetId = m.GeraetId;
        FahrzeugId = m.FahrzeugId;
        KommandantInformiertAm = m.KommandantInformiertAm;
        BeschaffungId = await db.Beschaffungen.Where(b => b.MangelId == id).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        DarfBearbeiten = await DarfAsync();
        Fotoliste = (await db.Mangelfotos.AsNoTracking().Where(f => f.MangelId == id).OrderBy(f => f.ErstelltAm)
            .Select(f => new { f.Id, f.Dateiname }).ToListAsync(ct)).Select(f => (f.Id, f.Dateiname)).ToList();

        if (m.GeraetId is not { } geraetId || await db.Geraete.AsNoTracking().FirstOrDefaultAsync(x => x.Id == geraetId, ct) is not { } defekt)
        {
            return;
        }

        var fahrzeugId = m.ErsatzGeraetId is null ? defekt.FahrzeugId : m.UrsprungFahrzeugId;
        var fahrzeug = fahrzeugId is null ? null : await db.Fahrzeuge.Where(f => f.Id == fahrzeugId).Select(f => f.Funkrufname).FirstOrDefaultAsync(ct);
        var ersatzName = m.ErsatzGeraetId is null ? null
            : await db.Geraete.Where(x => x.Id == m.ErsatzGeraetId).Select(x => x.Inventarnummer + " " + x.Bezeichnung).FirstOrDefaultAsync(ct);
        Ersatz = new Ersatzstand(fahrzeug, m.ErsatzGeraetId is null ? defekt.Lagerort : m.UrsprungLagerort, m.ErsatzGeraetId, ersatzName, m.ErsatzEingesetzt, m.WiederVerladenAm);
        if (DarfBearbeiten && !m.ErsatzEingesetzt && defekt.FahrzeugId is not null)
        {
            Ersatzgeraete = await ErsatzAbfrage(defekt)
                .OrderBy(x => x.MaterialId == defekt.MaterialId ? 0 : 1).ThenBy(x => x.FahrzeugId == null ? 0 : 1).ThenBy(x => x.Inventarnummer)
                .Select(x => new SelectListItem(x.Inventarnummer + " " + x.Bezeichnung + (x.Fahrzeug != null ? " (auf " + x.Fahrzeug.Funkrufname + ")" : x.Lagerort != null ? " (" + x.Lagerort + ")" : ""), x.Id.ToString()))
                .Take(50)
                .ToListAsync(ct);
        }
    }
}
