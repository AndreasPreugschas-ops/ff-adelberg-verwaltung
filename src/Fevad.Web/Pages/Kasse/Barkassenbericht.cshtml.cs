using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Barkassenbericht einer Kassenstelle an einem Festtag (V03): Wechselgeld, Zählprotokoll, zwei Zählende, PDF zum Unterschreiben und
/// Übernahme der Einnahme ins Kassenbuch mit dem PDF als Beleg. Nach der Übernahme ist der Bericht nicht mehr änderbar.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class BarkassenberichtModel(AppDbContext db, Kassenzugriff zugriff, Kassenfuehrung kassen, TimeProvider zeit) : PageModel
{
    /// <summary>Die Veranstaltung.</summary>
    public Veranstaltung Veranstaltung { get; private set; } = null!;

    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Der gespeicherte Bericht; null bei einem neuen.</summary>
    public Barkassenbericht? Bericht { get; private set; }

    /// <summary>Der Benutzer darf buchen.</summary>
    public bool DarfBuchen { get; private set; }

    /// <summary>Die Felder lassen sich ändern.</summary>
    public bool Aenderbar => DarfBuchen && Bericht?.BuchungId is null && (Bericht is not null || !Veranstaltung.Abgerechnet);

    /// <summary>Konten der Kasse für die Übernahme.</summary>
    public IReadOnlyList<SelectListItem> Konten { get; private set; } = [];

    /// <summary>Einnahmepositionen für die Übernahme.</summary>
    public IReadOnlyList<SelectListItem> Positionen { get; private set; } = [];

    /// <summary>Eingabefelder.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Konto, auf das die Einnahme gebucht wird (meist die Barkasse).</summary>
    [BindProperty, Display(Name = "Konto")] public Guid? KontoId { get; set; }

    /// <summary>Position, auf die die Einnahme gebucht wird.</summary>
    [BindProperty, Display(Name = "Position")] public Guid? PlanpositionId { get; set; }

    /// <summary>Eingabefelder eines Barkassenberichts.</summary>
    public sealed class Eingabe
    {
        /// <summary>Festtag.</summary>
        [Required(ErrorMessage = "Bitte den Tag angeben."), Display(Name = "Festtag")] public DateOnly? Tag { get; set; }

        /// <summary>Kassenstelle.</summary>
        [Required(ErrorMessage = "Bitte die Kassenstelle angeben."), StringLength(100), Display(Name = "Kassenstelle")] public string Stelle { get; set; } = "";

        /// <summary>Wechselgeld zu Beginn.</summary>
        [Range(0, 100_000, ErrorMessage = "Das Wechselgeld darf nicht negativ sein."), Display(Name = "Wechselgeld (Euro)")] public decimal Wechselgeld { get; set; }

        /// <summary>Erste zählende Person.</summary>
        [Required(ErrorMessage = "Bitte angeben, wer gezählt hat."), StringLength(150), Display(Name = "Gezählt von")] public string GezaehltVon { get; set; } = "";

        /// <summary>Zweite zählende Person.</summary>
        [Required(ErrorMessage = "Bitte die zweite Person angeben."), StringLength(150), Display(Name = "Zweite Person")] public string ZweitePerson { get; set; } = "";

        /// <summary>Umsatzsteuersatz der Einnahme.</summary>
        [Display(Name = "Umsatzsteuersatz")] public Umsatzsteuersatz Steuersatz { get; set; } = Umsatzsteuersatz.Regel;

        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }

        /// <summary>Gezählte Stücke je Wert in Cent.</summary>
        public Dictionary<int, int> Anzahl { get; set; } = [];
    }

    /// <summary>Zeigt einen Bericht oder das Formular für einen neuen.</summary>
    /// <param name="id">Kennung des Berichts; leer für einen neuen.</param>
    /// <param name="veranstaltung">Veranstaltung eines neuen Berichts.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? veranstaltung, CancellationToken ct)
    {
        if (await LadenAsync(id, veranstaltung, ct) is { } fehler)
        {
            return fehler;
        }

        if (Bericht is { } b)
        {
            Formular = new Eingabe
            {
                Tag = b.Tag, Stelle = b.Stelle, Wechselgeld = b.Wechselgeld, GezaehltVon = b.GezaehltVon, ZweitePerson = b.ZweitePerson,
                Steuersatz = b.Steuersatz, Bemerkung = b.Bemerkung, Anzahl = b.Zaehlung.ToDictionary(z => Cent(z.Wert), z => z.Anzahl),
            };
        }
        else
        {
            var heute = Anzeige.Heute(zeit);
            var tag = heute >= Veranstaltung.Beginn && heute <= (Veranstaltung.Ende ?? Veranstaltung.Beginn) ? heute : Veranstaltung.Beginn;
            Formular = new Eingabe { Tag = tag };
        }

        return Page();
    }

    /// <summary>Speichert den Bericht.</summary>
    /// <param name="id">Kennung des Berichts; leer für einen neuen.</param>
    /// <param name="veranstaltung">Veranstaltung eines neuen Berichts.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid? id, Guid? veranstaltung, CancellationToken ct)
    {
        if (await LadenAsync(id, veranstaltung, ct) is { } fehler)
        {
            return fehler;
        }

        if (!Aenderbar)
        {
            return Forbid();
        }

        if (Formular.Anzahl.Any(a => a.Value < 0))
        {
            ModelState.AddModelError("Formular.Anzahl", "Anzahlen dürfen nicht negativ sein.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var bericht = id is { } bid
            ? await db.Barkassenberichte.Include(b => b.Zaehlung).FirstAsync(b => b.Id == bid, ct)
            : new Barkassenbericht { VeranstaltungId = Veranstaltung.Id, Stelle = "", GezaehltVon = "", ZweitePerson = "" };
        bericht.Tag = Formular.Tag!.Value;
        bericht.Stelle = Formular.Stelle.Trim();
        bericht.Wechselgeld = Formular.Wechselgeld;
        bericht.GezaehltVon = Formular.GezaehltVon.Trim();
        bericht.ZweitePerson = Formular.ZweitePerson.Trim();
        bericht.Steuersatz = Formular.Steuersatz;
        bericht.Bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim();
        foreach (var wert in Zaehlposition.Stueckelung)
        {
            var anzahl = Formular.Anzahl.GetValueOrDefault(Cent(wert));
            var zeile = bericht.Zaehlung.FirstOrDefault(z => z.Wert == wert);
            if (zeile is null && anzahl > 0)
            {
                bericht.Zaehlung.Add(new Zaehlposition { Wert = wert, Anzahl = anzahl });
            }
            else if (zeile is not null)
            {
                zeile.Anzahl = anzahl;
            }
        }

        if (id is null)
        {
            db.Barkassenberichte.Add(bericht);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Barkassenbericht gespeichert; Einnahme {Anzeige.Euro(bericht.Einnahme)}.";
        return RedirectToPage(new { id = bericht.Id });
    }

    /// <summary>Übernimmt die Einnahme als Buchung ins Kassenbuch; das PDF des Berichts wird Beleg.</summary>
    /// <param name="id">Kennung des Berichts.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Buchung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostBuchenAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, null, ct) is { } fehler)
        {
            return fehler;
        }

        if (!DarfBuchen || Bericht!.BuchungId is not null)
        {
            return Forbid();
        }

        FormularAusBericht();
        if (Bericht.Einnahme <= 0)
        {
            ModelState.AddModelError(string.Empty, "Der Bericht ergibt keine Einnahme; ein Fehlbetrag wird als Ausgabe mit Eigenbeleg gebucht.");
            return Page();
        }

        var buchung = new Buchung
        {
            KasseId = Kasse.Id, Datum = Bericht.Tag, Art = BuchungArt.Einnahme, Betrag = Bericht.Einnahme, KontoId = KontoId ?? Guid.Empty,
            PlanpositionId = PlanpositionId, Steuerbereich = Steuerbereich.Wirtschaftlich, Steuersatz = Bericht.Steuersatz, VeranstaltungId = Veranstaltung.Id,
            Text = $"{Veranstaltung.Name}: {Bericht.Stelle} am {Anzeige.Datum(Bericht.Tag)}", Belegart = Belegart.Kassenbericht,
        };
        var konten = await db.Zahlungsmittelkonten.AsNoTracking().Where(k => k.KasseId == Kasse.Id).ToListAsync(ct);
        var positionen = await db.Planpositionen.AsNoTracking().Where(p => p.KasseId == Kasse.Id).ToListAsync(ct);
        foreach (var meldung in Kassenregeln.Pruefen(buchung, konten, positionen))
        {
            ModelState.AddModelError(string.Empty, meldung);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var daten = await PdfDatenAsync(ct);
        buchung.Belege.Add(Kassenfuehrung.Beleg(BarkassenberichtPdf.Dateiname(daten), "application/pdf", BarkassenberichtPdf.Erzeugen(daten)));
        await kassen.BuchenAsync(buchung, ct);
        var bericht = await db.Barkassenberichte.FirstAsync(b => b.Id == id, ct);
        bericht.BuchungId = buchung.Id;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Einnahme {Anzeige.Euro(buchung.Betrag)} als Beleg {buchung.Kennung} gebucht; das Zählprotokoll hängt als Beleg an.";
        return RedirectToPage("Buchung", new { id = buchung.Id });
    }

    /// <summary>Löscht einen noch nicht gebuchten Bericht.</summary>
    /// <param name="id">Kennung des Berichts.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Veranstaltung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, null, ct) is { } fehler)
        {
            return fehler;
        }

        if (!DarfBuchen || Bericht!.BuchungId is not null)
        {
            return Forbid();
        }

        var bericht = await db.Barkassenberichte.FirstAsync(b => b.Id == id, ct);
        db.Barkassenberichte.Remove(bericht);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Barkassenbericht gelöscht.";
        return RedirectToPage("Veranstaltung", new { id = Veranstaltung.Id });
    }

    /// <summary>Bericht als PDF zum Unterschreiben.</summary>
    /// <param name="id">Kennung des Berichts.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, null, ct) is { } fehler)
        {
            return fehler;
        }

        var daten = await PdfDatenAsync(ct);
        return File(BarkassenberichtPdf.Erzeugen(daten), "application/pdf", BarkassenberichtPdf.Dateiname(daten));
    }

    private static int Cent(decimal wert) => (int)(wert * 100);

    private void FormularAusBericht()
    {
        var b = Bericht!;
        Formular = new Eingabe
        {
            Tag = b.Tag, Stelle = b.Stelle, Wechselgeld = b.Wechselgeld, GezaehltVon = b.GezaehltVon, ZweitePerson = b.ZweitePerson,
            Steuersatz = b.Steuersatz, Bemerkung = b.Bemerkung, Anzahl = b.Zaehlung.ToDictionary(z => Cent(z.Wert), z => z.Anzahl),
        };
        // Die Übernahme schickt nur Konto und Position; Fehler des leeren Berichtsformulars (ohne Präfix gebunden) gelten nicht.
        ModelState.Clear();
    }

    private async Task<BarkassenberichtPdf.Daten> PdfDatenAsync(CancellationToken ct)
    {
        var organisation = await db.Organisationen.AsNoTracking().FirstAsync(ct);
        var kopf = new AntragPdf.Kopfdaten(organisation.FeuerwehrName, organisation.FeuerwehrAnschrift, null, null, organisation.Logo, organisation.LogoInhaltstyp);
        return new BarkassenberichtPdf.Daten(kopf, Kasse.Name, Veranstaltung.Name, Bericht!, Anzeige.Text(Bericht!.Steuersatz));
    }

    private async Task<IActionResult?> LadenAsync(Guid? id, Guid? veranstaltungId, CancellationToken ct)
    {
        if (id is { } bid)
        {
            Bericht = await db.Barkassenberichte.AsNoTracking().Include(b => b.Zaehlung).Include(b => b.Buchung).FirstOrDefaultAsync(b => b.Id == bid, ct);
            if (Bericht is null)
            {
                return NotFound();
            }

            veranstaltungId = Bericht.VeranstaltungId;
        }

        var v = veranstaltungId is { } vid ? await db.Veranstaltungen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vid, ct) : null;
        if (v is null || await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, v.KasseId, ct) is not { } kasse)
        {
            return NotFound();
        }

        Veranstaltung = v;
        Kasse = kasse;
        DarfBuchen = await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse);
        if (Bericht is null && !DarfBuchen)
        {
            return Forbid();
        }

        var konten = await db.Zahlungsmittelkonten.AsNoTracking().Where(k => k.KasseId == kasse.Id && k.Aktiv)
            .OrderBy(k => k.Art != KontoArt.Barkasse).ThenBy(k => k.Sortierung).ToListAsync(ct);
        Konten = konten.Select(k => new SelectListItem(k.Bezeichnung, k.Id.ToString())).ToList();
        var positionen = await db.Planpositionen.AsNoTracking().Where(p => p.KasseId == kasse.Id && p.Aktiv && p.Richtung == Planrichtung.Einnahme)
            .ToListAsync(ct);
        Positionen = positionen.OrderBy(p => p.Steuerbereich != Steuerbereich.Wirtschaftlich).ThenBy(p => p.Nummer, StringComparer.Ordinal)
            .Select(p => new SelectListItem($"{p.Nummer} {p.Bezeichnung}", p.Id.ToString())).ToList();
        KontoId ??= konten.Select(k => (Guid?)k.Id).FirstOrDefault();
        PlanpositionId ??= Positionen.Select(p => (Guid?)Guid.Parse(p.Value)).FirstOrDefault();
        return null;
    }
}
