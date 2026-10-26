using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dokumente;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Vorbelegung = Fevad.Infrastructure.Persistenz.Seed.Stammdaten;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Werkstatt;

/// <summary>
/// Werkstattauftrag (V61) mit Lieferschein und Rückgabe: Leistungen je Gerät, Prüfung ins Prüfbuch, Werkstattprotokoll als Anhang.
/// </summary>
public class DetailsModel(AppDbContext db, Werkstattauftraege auftraege, IAuthorizationService autorisierung, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Der Auftrag.</summary>
    public Werkstattauftrag Auftrag { get; private set; } = null!;

    /// <summary>Positionen mit Gerät und wählbaren Prüfarten.</summary>
    public IReadOnlyList<PositionAnsicht> Positionen { get; private set; } = [];

    /// <summary>Angehängte Protokolle ohne Inhalt.</summary>
    public IReadOnlyList<(Guid Id, string Dateiname)> Protokolle { get; private set; } = [];

    /// <summary>Name der abgebenden Person; nur für Prüfberechtigte.</summary>
    public string? AbgegebenVon { get; private set; }

    /// <summary>Zurückbuchen darf, wer TechnikPruefen hat.</summary>
    public bool DarfPruefen { get; private set; }

    /// <summary>Eingaben der Rückgabe.</summary>
    [BindProperty]
    public RueckgabeEingabe Rueckgabe { get; set; } = new();

    /// <summary>Hochgeladene Werkstattprotokolle.</summary>
    [BindProperty]
    public List<IFormFile> Dateien { get; set; } = [];

    /// <summary>Ein Gerät im Auftrag.</summary>
    /// <param name="Id">Kennung der Position.</param>
    /// <param name="GeraetId">Kennung des Geräts.</param>
    /// <param name="Inventarnummer">Nummer am Gerät.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Geraeteart">Geräteart.</param>
    /// <param name="Seriennummer">Seriennummer.</param>
    /// <param name="Leistungen">Erfasste Leistungen.</param>
    /// <param name="PruefungId">Entstandene Prüfung.</param>
    /// <param name="Bemerkung">Bemerkung der Werkstatt.</param>
    /// <param name="Pruefarten">Prüfplanpositionen der Geräteart.</param>
    /// <param name="Flasche">Atemluftflasche: „gefüllt“ vorbelegen.</param>
    public sealed record PositionAnsicht(Guid Id, Guid GeraetId, string Inventarnummer, string Bezeichnung, string Geraeteart, string? Seriennummer,
        Werkstattleistung Leistungen, Guid? PruefungId, string? Bemerkung, IReadOnlyList<(Guid Id, Pruefart Pruefart, int IntervallMonate)> Pruefarten, bool Flasche);

    /// <summary>Formular der Rückgabe.</summary>
    public sealed class RueckgabeEingabe
    {
        /// <summary>Tag der Rückgabe.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Zurück am")]
        public DateOnly? ZurueckAm { get; set; }

        /// <summary>Lieferschein- oder Protokollnummer der Werkstatt.</summary>
        [StringLength(100), Display(Name = "Beleg der Werkstatt (Nummer)")]
        public string? Beleg { get; set; }

        /// <summary>Rückmeldung je Gerät.</summary>
        public List<Zeile> Zeilen { get; set; } = [];
    }

    /// <summary>Rückmeldung zu einem Gerät.</summary>
    public sealed class Zeile
    {
        /// <summary>Kennung der Position.</summary>
        public Guid PositionId { get; set; }
        /// <summary>Gereinigt.</summary>
        public bool Gereinigt { get; set; }
        /// <summary>Geprüft.</summary>
        public bool Geprueft { get; set; }
        /// <summary>Gefüllt.</summary>
        public bool Gefuellt { get; set; }
        /// <summary>Instand gesetzt.</summary>
        public bool Instandgesetzt { get; set; }
        /// <summary>Prüfplanposition der Prüfung.</summary>
        public Guid? PruefplanPositionId { get; set; }
        /// <summary>Ergebnis.</summary>
        public PruefErgebnis Ergebnis { get; set; } = PruefErgebnis.Bestanden;
        /// <summary>Befund.</summary>
        [StringLength(2000)] public string? Mangel { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(500)] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt den Auftrag; bei offenem Auftrag das Rückgabeformular, vorbelegt mit „gereinigt, geprüft“.</summary>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Rueckgabe.ZurueckAm = Anzeige.Heute(zeit);
        Rueckgabe.Zeilen = Positionen.Select(p => new Zeile
        {
            PositionId = p.Id,
            Gereinigt = true,
            Geprueft = p.Pruefarten.Count > 0,
            Gefuellt = p.Flasche,
            // Vorbelegt „nach Gebrauch“, sonst die erste Prüfart des Plans.
            PruefplanPositionId = p.Pruefarten.Where(a => a.Pruefart == Pruefart.NachGebrauch).Concat(p.Pruefarten).Select(a => (Guid?)a.Id).FirstOrDefault(),
        }).ToList();
        return Page();
    }

    /// <summary>Bucht die Rückgabe.</summary>
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded)
        {
            return Forbid();
        }

        var auftrag = await db.Werkstattauftraege.Include(w => w.Positionen).FirstOrDefaultAsync(w => w.Id == id, ct);
        if (auftrag is null)
        {
            return NotFound();
        }

        var heute = Anzeige.Heute(zeit);
        if (Rueckgabe.ZurueckAm > heute)
        {
            ModelState.AddModelError("Rueckgabe.ZurueckAm", "Das Datum liegt in der Zukunft.");
        }

        var protokolle = await ProtokolleLesenAsync(heute, ct);
        if (ModelState.IsValid)
        {
            var meldungen = Rueckgabe.Zeilen.Select(z => new Werkstattauftraege.Rueckmeldung(
                z.PositionId,
                (z.Gereinigt ? Werkstattleistung.Gereinigt : 0) | (z.Geprueft ? Werkstattleistung.Geprueft : 0)
                    | (z.Gefuellt ? Werkstattleistung.Gefuellt : 0) | (z.Instandgesetzt ? Werkstattleistung.Instandgesetzt : 0),
                z.PruefplanPositionId, z.Ergebnis, z.Mangel, z.Bemerkung)).ToList();
            if (await auftraege.ZurueckAsync(auftrag, Rueckgabe.ZurueckAm!.Value, Rueckgabe.Beleg, meldungen, protokolle, ct) is { } fehler)
            {
                ModelState.AddModelError("", fehler);
            }
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(id, ct);
            return Page();
        }

        await db.SaveChangesAsync(ct);
        var geprueft = auftrag.Positionen.Count(p => p.PruefungId is not null);
        TempData["Meldung"] = $"{auftrag.Nummer} zurückgebucht: {auftrag.Positionen.Count} Geräte, {geprueft} Prüfungen im Prüfbuch.";
        return RedirectToPage(new { id });
    }

    /// <summary>Lieferschein als PDF.</summary>
    public async Task<IActionResult> OnGetLieferscheinAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        var heute = Anzeige.Heute(zeit);
        var nachEinsatz = await db.Maengel.AsNoTracking()
            .Where(m => m.GeraetId != null && m.EinsatzId != null && m.SperreBisPruefung && m.Status != MangelStatus.Behoben)
            .Select(m => m.GeraetId!.Value)
            .ToListAsync(ct);
        var o = await db.OrganisationAsync(ct);
        var daten = new LieferscheinPdf.Daten(
            new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp),
            Auftrag.Nummer, Auftrag.Werkstattname, Auftrag.AbgegebenAm, AbgegebenVon, Auftrag.Bemerkung,
            Positionen.Select(p => new LieferscheinPdf.Zeile(p.Inventarnummer, p.Bezeichnung, p.Seriennummer, nachEinsatz.Contains(p.GeraetId) ? "nach Einsatz" : null)).ToList());
        return File(LieferscheinPdf.Erzeugen(daten), "application/pdf", LieferscheinPdf.Dateiname(daten));
    }

    /// <summary>Liefert ein angehängtes Werkstattprotokoll aus.</summary>
    public async Task<IActionResult> OnGetProtokollAsync(Guid id, Guid protokollId, CancellationToken ct)
    {
        var protokoll = await db.Werkstattprotokolle.AsNoTracking().FirstOrDefaultAsync(p => p.Id == protokollId && p.WerkstattauftragId == id, ct);
        return protokoll is null ? NotFound() : File(protokoll.Inhalt, protokoll.Inhaltstyp, protokoll.Dateiname);
    }

    private async Task<List<Werkstattprotokoll>> ProtokolleLesenAsync(DateOnly heute, CancellationToken ct)
    {
        var protokolle = new List<Werkstattprotokoll>();
        var megabyte = await parameter.ZahlAsync(ParameterSchluessel.DokumentMaxMegabyte, heute, 20, ct);
        foreach (var datei in Dateien.Where(d => d.Length > 0))
        {
            var name = Dateitypen.Bereinigen(datei.FileName);
            var typ = Dateitypen.Inhaltstyp(name);
            if (typ is not ("application/pdf" or "image/png" or "image/jpeg"))
            {
                ModelState.AddModelError(nameof(Dateien), $"„{name}“: nur PDF, JPG oder PNG.");
                continue;
            }

            if (datei.Length > megabyte * 1024L * 1024L)
            {
                ModelState.AddModelError(nameof(Dateien), $"„{name}“ ist größer als {megabyte} MB.");
                continue;
            }

            using var speicher = new MemoryStream();
            await datei.CopyToAsync(speicher, ct);
            var inhalt = speicher.ToArray();
            if (typ != "application/pdf")
            {
                if (Fotobereinigung.Bereinigen(inhalt, typ) is not { } ohneMetadaten)
                {
                    ModelState.AddModelError(nameof(Dateien), $"„{name}“ ist kein gültiges Bild.");
                    continue;
                }

                inhalt = ohneMetadaten;
            }
            else if (!inhalt.AsSpan().StartsWith("%PDF"u8))
            {
                ModelState.AddModelError(nameof(Dateien), $"„{name}“ ist kein gültiges PDF.");
                continue;
            }

            protokolle.Add(new Werkstattprotokoll { Dateiname = name, Inhaltstyp = typ, Inhalt = inhalt });
        }

        return protokolle;
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var auftrag = await db.Werkstattauftraege.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        if (auftrag is null)
        {
            return false;
        }

        Auftrag = auftrag;
        DarfPruefen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded;
        AbgegebenVon = DarfPruefen && auftrag.AbgegebenVonId is { } von
            ? await db.Mitglieder.Where(m => m.Id == von).Select(m => m.Vorname + " " + m.Nachname).FirstOrDefaultAsync(ct)
            : null;
        var positionen = await db.Werkstattpositionen.AsNoTracking()
            .Where(p => p.WerkstattauftragId == id)
            .Select(p => new
            {
                p.Id, p.GeraetId, p.Geraet!.Inventarnummer, p.Geraet.Bezeichnung, Art = p.Geraet.Geraeteart!.Bezeichnung, ArtCode = p.Geraet.Geraeteart.Code,
                p.Geraet.GeraeteartId, p.Geraet.Seriennummer, p.Leistungen, p.PruefungId, p.Bemerkung,
            })
            .ToListAsync(ct);
        var artIds = positionen.Select(p => p.GeraeteartId).Distinct().ToList();
        var plaene = (await db.PruefplanPositionen.AsNoTracking().Where(p => artIds.Contains(p.GeraeteartId)).ToListAsync(ct))
            .ToLookup(p => p.GeraeteartId);
        Positionen = positionen
            .OrderBy(p => p.Art, StringComparer.CurrentCulture).ThenBy(p => p.Inventarnummer, StringComparer.Ordinal)
            .Select(p => new PositionAnsicht(p.Id, p.GeraetId, p.Inventarnummer, p.Bezeichnung, p.Art, p.Seriennummer, p.Leistungen, p.PruefungId, p.Bemerkung,
                plaene[p.GeraeteartId].OrderBy(x => x.Pruefart).Select(x => (x.Id, x.Pruefart, x.IntervallMonate)).ToList(),
                p.ArtCode.StartsWith(Vorbelegung.FlaschenCodeAnfang, StringComparison.Ordinal)))
            .ToList();
        Protokolle = (await db.Werkstattprotokolle.AsNoTracking().Where(p => p.WerkstattauftragId == id).OrderBy(p => p.ErstelltAm)
            .Select(p => new { p.Id, p.Dateiname }).ToListAsync(ct)).Select(p => (p.Id, p.Dateiname)).ToList();
        return true;
    }
}
