using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kassen;
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
/// Eine Buchung im Kassenbuch (V02): anlegen, ändern solange nicht festgeschrieben, Belege hochladen, stornieren. Über der
/// Beschlussgrenze erscheint der Hinweis auf den Ausschussbeschluss mit Verweisfeld; gesperrt wird nichts (Andi, 25.10.2026).
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
[RequestSizeLimit(Hochladen.MaxAnfrageBytes), RequestFormLimits(MultipartBodyLengthLimit = Hochladen.MaxAnfrageBytes)]
public class BuchungModel(
    AppDbContext db, Kassenzugriff zugriff, Kassenfuehrung kassen, ParameterLeser parameter, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Die gespeicherte Buchung; null bei einer neuen.</summary>
    public Buchung? Buchung { get; private set; }

    /// <summary>Stornobuchung zu dieser Buchung, falls storniert.</summary>
    public Buchung? StorniertDurch { get; private set; }

    /// <summary>Vorbereitete Ausgabe, aus der gebucht wird.</summary>
    public Zahlungsanordnung? Anordnung { get; private set; }

    /// <summary>Wiederkehrende Buchung, aus der gebucht wird.</summary>
    public WiederkehrendeBuchung? Vorlage { get; private set; }

    /// <summary>Umsatz aus dem Kontoauszug, der mit dieser Buchung abgeglichen wird.</summary>
    public Kontoumsatz? Umsatz { get; private set; }

    /// <summary>Belegdateien (Kennung, Name, Prüfsumme).</summary>
    public IReadOnlyList<(Guid Id, string Dateiname, string Pruefsumme)> Belege { get; private set; } = [];

    /// <summary>Der Benutzer darf buchen.</summary>
    public bool DarfBuchen { get; private set; }

    /// <summary>Die Felder lassen sich ändern (neu oder nicht festgeschrieben, mit Buchungsrecht).</summary>
    public bool Aenderbar => DarfBuchen && Buchung is not { Festgeschrieben: true } && Buchung?.StornoVonId is null;

    /// <summary>Zahlungspartner sichtbar.</summary>
    public bool PartnerSichtbar { get; private set; }

    /// <summary>Beschlussgrenze in Euro (0 = aus).</summary>
    public int Beschlussgrenze { get; private set; }

    /// <summary>Konten der Kasse.</summary>
    public IReadOnlyList<SelectListItem> Konten { get; private set; } = [];

    /// <summary>Positionen der Kasse.</summary>
    public IReadOnlyList<SelectListItem> Positionen { get; private set; } = [];

    /// <summary>Hinweise je Position für die Anzeige beim Buchen.</summary>
    public IReadOnlyDictionary<string, string> Positionshinweise { get; private set; } = new Dictionary<string, string>();

    /// <summary>Steuerbereich je Position als Vorschlag beim Wechsel der Position.</summary>
    public IReadOnlyDictionary<string, string> Positionsbereiche { get; private set; } = new Dictionary<string, string>();

    /// <summary>Veranstaltungen der Kasse als Kostenträger.</summary>
    public IReadOnlyList<SelectListItem> Veranstaltungen { get; private set; } = [];

    /// <summary>Beschlüsse für den Verweis.</summary>
    public IReadOnlyList<SelectListItem> Beschluesse { get; private set; } = [];

    /// <summary>Eingabefelder.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Hochgeladene Belege.</summary>
    [BindProperty] public List<IFormFile> Dateien { get; set; } = [];

    /// <summary>Tag des Stornos.</summary>
    [BindProperty] public DateOnly? StornoDatum { get; set; }

    /// <summary>Grund des Stornos.</summary>
    [BindProperty, StringLength(300)] public string? StornoGrund { get; set; }

    /// <summary>Eingabefelder einer Buchung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Tag der Zahlung.</summary>
        [Required(ErrorMessage = "Bitte das Datum angeben."), Display(Name = "Datum")] public DateOnly? Datum { get; set; }

        /// <summary>Einnahme, Ausgabe oder Umbuchung.</summary>
        [Display(Name = "Art")] public BuchungArt Art { get; set; } = BuchungArt.Ausgabe;

        /// <summary>Betrag in Euro.</summary>
        [Required(ErrorMessage = "Bitte den Betrag angeben."), Range(0.01, 1_000_000, ErrorMessage = "Der Betrag muss größer als 0 sein."), Display(Name = "Betrag (Euro)")]
        public decimal? Betrag { get; set; }

        /// <summary>Konto; bei Umbuchung das abgebende.</summary>
        [Required(ErrorMessage = "Bitte das Konto wählen."), Display(Name = "Konto")] public Guid? KontoId { get; set; }

        /// <summary>Empfangendes Konto einer Umbuchung.</summary>
        [Display(Name = "Auf Konto")] public Guid? GegenkontoId { get; set; }

        /// <summary>Position des Wirtschaftsplans.</summary>
        [Display(Name = "Position")] public Guid? PlanpositionId { get; set; }

        /// <summary>Steuerlicher Bereich.</summary>
        [Display(Name = "Steuerbereich")] public Steuerbereich Steuerbereich { get; set; }

        /// <summary>Umsatzsteuersatz im wirtschaftlichen Bereich.</summary>
        [Display(Name = "Umsatzsteuersatz")] public Umsatzsteuersatz Steuersatz { get; set; }

        /// <summary>Veranstaltung als Kostenträger.</summary>
        [Display(Name = "Veranstaltung")] public Guid? VeranstaltungId { get; set; }

        /// <summary>Zahlungspartner.</summary>
        [StringLength(200), Display(Name = "Zahlungspartner")] public string? Partner { get; set; }

        /// <summary>Verwendungszweck.</summary>
        [Required(ErrorMessage = "Bitte den Verwendungszweck angeben."), StringLength(500), Display(Name = "Verwendungszweck")] public string Text { get; set; } = "";

        /// <summary>Art des Belegs.</summary>
        [Display(Name = "Belegart")] public Belegart Belegart { get; set; } = Belegart.Rechnung;

        /// <summary>Begründung des Eigenbelegs.</summary>
        [StringLength(500), Display(Name = "Begründung des Eigenbelegs")] public string? EigenbelegBegruendung { get; set; }

        /// <summary>Ausschussbeschluss aus dem Gremienmodul.</summary>
        [Display(Name = "Ausschussbeschluss")] public Guid? AusschussbeschlussId { get; set; }

        /// <summary>Verweis auf einen Beschluss außerhalb von FEVAD.</summary>
        [StringLength(200), Display(Name = "oder Verweis")] public string? Beschlussverweis { get; set; }
    }

    /// <summary>Zeigt eine Buchung oder das Formular für eine neue.</summary>
    /// <param name="id">Kennung der Buchung; leer für eine neue.</param>
    /// <param name="kasse">Kasse einer neuen Buchung.</param>
    /// <param name="anordnung">Vorbereitete Ausgabe, aus der gebucht wird.</param>
    /// <param name="vorlage">Wiederkehrende Buchung, aus der gebucht wird.</param>
    /// <param name="umsatz">Umsatz aus dem Kontoauszug; Tag, Konto und Betrag kommen von der Bank.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? kasse, Guid? anordnung, Guid? vorlage, Guid? umsatz, CancellationToken ct)
    {
        if (await LadenAsync(id, kasse, anordnung, vorlage, umsatz, ct) is { } fehler)
        {
            return fehler;
        }

        if (Buchung is { } b)
        {
            Formular = new Eingabe
            {
                Datum = b.Datum, Art = b.Art, Betrag = b.Betrag, KontoId = b.KontoId, GegenkontoId = b.GegenkontoId, PlanpositionId = b.PlanpositionId,
                Steuerbereich = b.Steuerbereich, Steuersatz = b.Steuersatz, VeranstaltungId = b.VeranstaltungId,
                Partner = PartnerSichtbar ? b.Partner : null, Text = b.Text, Belegart = b.Belegart, EigenbelegBegruendung = b.EigenbelegBegruendung,
                AusschussbeschlussId = b.AusschussbeschlussId, Beschlussverweis = b.Beschlussverweis,
            };
        }
        else if (Anordnung is { } a)
        {
            Formular = new Eingabe
            {
                Datum = Anzeige.Heute(zeit), Art = BuchungArt.Ausgabe, Betrag = a.Betrag, PlanpositionId = a.PlanpositionId, Partner = a.Empfaenger,
                Steuerbereich = Enum.Parse<Steuerbereich>(Positionsbereiche.GetValueOrDefault(a.PlanpositionId?.ToString() ?? "", nameof(Steuerbereich.Intern))),
                Text = a.Zweck, AusschussbeschlussId = a.AusschussbeschlussId,
                KontoId = Konten.Select(k => Guid.Parse(k.Value)).FirstOrDefault(),
            };
        }
        else if (Vorlage is { } w)
        {
            Formular = new Eingabe
            {
                Datum = Anzeige.Heute(zeit), Art = w.Art, Betrag = await Kassenauswahl.VorlageBetragAsync(w, parameter, ct), PlanpositionId = w.PlanpositionId,
                Steuerbereich = Enum.Parse<Steuerbereich>(Positionsbereiche.GetValueOrDefault(w.PlanpositionId?.ToString() ?? "", nameof(Steuerbereich.Intern))),
                Partner = PartnerSichtbar ? w.Partner : null, Text = Wiederkehrend.Text(w), Belegart = Belegart.Kontoauszug,
                KontoId = w.KontoId is { } k && Konten.Any(x => x.Value == k.ToString()) ? k : Konten.Select(x => Guid.Parse(x.Value)).FirstOrDefault(),
            };
        }
        else if (Umsatz is { } u)
        {
            Formular = new Eingabe
            {
                Art = u.Betrag > 0 ? BuchungArt.Einnahme : BuchungArt.Ausgabe, Betrag = Math.Abs(u.Betrag), Partner = PartnerSichtbar ? u.Gegenpartei : null,
                Text = u.Verwendungszweck is { Length: > 500 } lang ? lang[..500] : u.Verwendungszweck ?? "", Belegart = Belegart.Kontoauszug,
            };
        }
        else
        {
            Formular = new Eingabe { Datum = Anzeige.Heute(zeit), KontoId = Konten.Select(k => Guid.Parse(k.Value)).FirstOrDefault() };
        }

        // Beim Abgleich gelten Tag und Konto der Bank.
        if (Umsatz is not null && Buchung is null)
        {
            (Formular.Datum, Formular.KontoId) = (Umsatz.Buchungstag, Umsatz.KontoId);
        }

        return Page();
    }

    /// <summary>Speichert eine neue oder geänderte Buchung mit den hochgeladenen Belegen.</summary>
    /// <param name="id">Kennung der Buchung; leer für eine neue.</param>
    /// <param name="kasse">Kasse einer neuen Buchung.</param>
    /// <param name="anordnung">Vorbereitete Ausgabe, aus der gebucht wird.</param>
    /// <param name="vorlage">Wiederkehrende Buchung, aus der gebucht wird; ihre Fälligkeit rückt danach weiter.</param>
    /// <param name="umsatz">Umsatz aus dem Kontoauszug, der mit der neuen Buchung abgeglichen wird.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid? id, Guid? kasse, Guid? anordnung, Guid? vorlage, Guid? umsatz, CancellationToken ct)
    {
        if (await LadenAsync(id, kasse, anordnung, vorlage, umsatz, ct) is { } fehler)
        {
            return fehler;
        }

        if (!Aenderbar)
        {
            return Forbid();
        }

        var heute = Anzeige.Heute(zeit);
        var neueBelege = await Kassenauswahl.BelegeLesenAsync(Dateien, parameter, heute, ModelState, nameof(Dateien), ct);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var buchung = id is { } bid ? await db.Buchungen.FirstAsync(b => b.Id == bid, ct) : new Buchung { KasseId = Kasse.Id, Text = "" };
        if (Buchung is not null && Formular.Datum!.Value.Year != Buchung.Jahr)
        {
            ModelState.AddModelError("Formular.Datum", $"Die Belegnummer gehört zum Jahr {Buchung.Jahr}. Für ein anderes Jahr bitte stornieren und neu buchen.");
            return Page();
        }

        buchung.Datum = Formular.Datum!.Value;
        buchung.Art = Formular.Art;
        buchung.Betrag = Formular.Betrag!.Value;
        buchung.KontoId = Formular.KontoId!.Value;
        buchung.GegenkontoId = Formular.Art == BuchungArt.Umbuchung ? Formular.GegenkontoId : null;
        buchung.PlanpositionId = Formular.PlanpositionId;
        buchung.Steuerbereich = Formular.Art == BuchungArt.Umbuchung ? Steuerbereich.Intern : Formular.Steuerbereich;
        buchung.Steuersatz = buchung.Steuerbereich == Steuerbereich.Wirtschaftlich ? Formular.Steuersatz : Umsatzsteuersatz.Ohne;
        buchung.VeranstaltungId = Formular.Art != BuchungArt.Umbuchung && Veranstaltungen.Any(v => v.Value == Formular.VeranstaltungId?.ToString())
            ? Formular.VeranstaltungId : null;
        // Ohne Sicht auf die Partner der Jugendkasse bleibt der gespeicherte Partner unverändert.
        if (PartnerSichtbar)
        {
            buchung.Partner = Leer(Formular.Partner);
        }

        buchung.Text = Formular.Text.Trim();
        buchung.Belegart = Formular.Belegart;
        buchung.EigenbelegBegruendung = Formular.Belegart == Belegart.Eigenbeleg ? Leer(Formular.EigenbelegBegruendung) : null;
        buchung.AusschussbeschlussId = Formular.Art == BuchungArt.Ausgabe && Beschluesse.Any(b => b.Value == Formular.AusschussbeschlussId?.ToString())
            ? Formular.AusschussbeschlussId : null;
        buchung.Beschlussverweis = Formular.Art == BuchungArt.Ausgabe ? Leer(Formular.Beschlussverweis) : null;

        var konten = await db.Zahlungsmittelkonten.AsNoTracking().Where(k => k.KasseId == Kasse.Id).ToListAsync(ct);
        var positionen = await db.Planpositionen.AsNoTracking().Where(p => p.KasseId == Kasse.Id).ToListAsync(ct);
        foreach (var meldung in Kassenregeln.Pruefen(buchung, konten, positionen))
        {
            ModelState.AddModelError(string.Empty, meldung);
        }

        if (!ModelState.IsValid)
        {
            db.ChangeTracker.Clear();
            return Page();
        }

        foreach (var beleg in neueBelege)
        {
            buchung.Belege.Add(beleg);
        }

        if (Buchung is null)
        {
            if (Anordnung is not null)
            {
                var a = await db.Zahlungsanordnungen.Include(x => x.Belege).FirstAsync(x => x.Id == Anordnung.Id, ct);
                buchung.ZahlungsanordnungId = a.Id;
                foreach (var beleg in a.Belege)
                {
                    beleg.BuchungId = buchung.Id;
                }

                a.Status = AnordnungStatus.Gezahlt;
                a.BuchungId = buchung.Id;
            }
            else if (Vorlage is not null)
            {
                var w = await db.WiederkehrendeBuchungen.FirstAsync(x => x.Id == Vorlage.Id, ct);
                w.NaechsteFaelligkeit = Wiederkehrend.Naechste(w.NaechsteFaelligkeit, w.Rhythmus, w.Tag);
            }

            await kassen.BuchenAsync(buchung, ct);
            if (Umsatz is not null)
            {
                var u = await db.Kontoumsaetze.FirstAsync(x => x.Id == Umsatz.Id, ct);
                if (Kassenregeln.Wirkung(buchung, u.KontoId) == u.Betrag)
                {
                    (u.BuchungId, u.Status) = (buchung.Id, UmsatzStatus.Zugeordnet);
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    TempData["Fehler"] = "Betrag oder Konto weichen vom Umsatz der Bank ab; der Umsatz bleibt im Abgleich offen.";
                }
            }
        }
        else
        {
            await db.SaveChangesAsync(ct);
        }

        var dateien = Belege.Count + neueBelege.Count + (Anordnung?.Belege.Count ?? 0);
        var hinweise = new List<string> { $"Beleg {buchung.Kennung} gespeichert." };
        if (!Kassenregeln.BelegVollstaendig(buchung.Belegart, buchung.EigenbelegBegruendung, dateien))
        {
            hinweise.Add("Der Beleg fehlt noch; festschreiben lässt sich die Buchung erst mit Beleg.");
        }

        if (Kassenregeln.BeschlussHinweis(buchung.Art, buchung.Betrag, Beschlussgrenze) && buchung.AusschussbeschlussId is null && buchung.Beschlussverweis is null)
        {
            hinweise.Add($"Ausgaben über {Anzeige.Euro(Beschlussgrenze)} beschließt der Feuerwehrausschuss; bitte den Beschluss nachtragen.");
        }

        TempData["Meldung"] = string.Join(" ", hinweise);
        // Aus dem Abgleich geht es zurück zu den offenen Umsätzen.
        return Umsatz is not null && Buchung is null ? RedirectToPage("Kontoauszug", new { kasse = Kasse.Id }) : RedirectToPage(new { id = buchung.Id });
    }

    /// <summary>Storniert die Buchung mit einer Gegenbuchung.</summary>
    /// <param name="id">Kennung der Buchung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Stornobuchung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostStornoAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, null, null, null, null, ct) is { } fehler)
        {
            return fehler;
        }

        if (!DarfBuchen)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(StornoGrund))
        {
            ModelState.AddModelError(nameof(StornoGrund), "Bitte den Grund angeben.");
            return Page();
        }

        try
        {
            var storno = await kassen.StornierenAsync(id, StornoDatum ?? Anzeige.Heute(zeit), StornoGrund, benutzer.Id, ct);
            TempData["Meldung"] = $"Beleg {Buchung!.Kennung} storniert mit Beleg {storno.Kennung}.";
            return RedirectToPage(new { id = storno.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(StornoGrund), ex.Message);
            return Page();
        }
    }

    /// <summary>Entfernt eine Belegdatei, solange die Buchung nicht festgeschrieben ist.</summary>
    /// <param name="id">Kennung der Buchung.</param>
    /// <param name="belegId">Kennung des Belegs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostBelegEntfernenAsync(Guid id, Guid belegId, CancellationToken ct)
    {
        if (await LadenAsync(id, null, null, null, null, ct) is { } fehler)
        {
            return fehler;
        }

        if (!Aenderbar)
        {
            return Forbid();
        }

        var beleg = await db.Kassenbelege.FirstOrDefaultAsync(b => b.Id == belegId && b.BuchungId == id, ct);
        if (beleg is null)
        {
            return NotFound();
        }

        db.Kassenbelege.Remove(beleg);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Beleg „{beleg.Dateiname}“ entfernt.";
        return RedirectToPage(new { id });
    }

    /// <summary>Liefert eine Belegdatei aus.</summary>
    /// <param name="id">Kennung der Buchung.</param>
    /// <param name="belegId">Kennung des Belegs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 404.</returns>
    public async Task<IActionResult> OnGetBelegAsync(Guid id, Guid belegId, CancellationToken ct)
    {
        var kasseId = await db.Buchungen.Where(b => b.Id == id).Select(b => (Guid?)b.KasseId).FirstOrDefaultAsync(ct);
        if (kasseId is null || await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasseId.Value, ct) is null)
        {
            return NotFound();
        }

        var beleg = await db.Kassenbelege.AsNoTracking().FirstOrDefaultAsync(b => b.Id == belegId && b.BuchungId == id, ct);
        return beleg is null ? NotFound() : File(beleg.Inhalt, beleg.Inhaltstyp, beleg.Dateiname);
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<IActionResult?> LadenAsync(Guid? id, Guid? kasseId, Guid? anordnungId, Guid? vorlageId, Guid? umsatzId, CancellationToken ct)
    {
        if (id is { } bid)
        {
            Buchung = await db.Buchungen.AsNoTracking().Include(b => b.Konto).Include(b => b.Gegenkonto).Include(b => b.Planposition)
                .Include(b => b.StornoVon).Include(b => b.Ausschussbeschluss).FirstOrDefaultAsync(b => b.Id == bid, ct);
            if (Buchung is null)
            {
                return NotFound();
            }

            kasseId = Buchung.KasseId;
            StorniertDurch = await db.Buchungen.AsNoTracking().FirstOrDefaultAsync(b => b.StornoVonId == bid, ct);
            Belege = (await db.Kassenbelege.AsNoTracking().Where(b => b.BuchungId == bid).OrderBy(b => b.ErstelltAm)
                .Select(b => new { b.Id, b.Dateiname, b.Pruefsumme }).ToListAsync(ct)).Select(b => (b.Id, b.Dateiname, b.Pruefsumme)).ToList();
        }
        else if (anordnungId is { } aid)
        {
            Anordnung = await db.Zahlungsanordnungen.AsNoTracking().Include(a => a.Belege)
                .FirstOrDefaultAsync(a => a.Id == aid && a.Status == AnordnungStatus.Vorbereitet, ct);
            if (Anordnung is null)
            {
                return NotFound();
            }

            kasseId = Anordnung.KasseId;
        }
        else if (vorlageId is { } vid)
        {
            Vorlage = await db.WiederkehrendeBuchungen.AsNoTracking().FirstOrDefaultAsync(w => w.Id == vid && w.Aktiv, ct);
            if (Vorlage is null)
            {
                return NotFound();
            }

            kasseId = Vorlage.KasseId;
        }

        if (id is null && umsatzId is { } uid)
        {
            Umsatz = await db.Kontoumsaetze.AsNoTracking().Include(u => u.Konto).FirstOrDefaultAsync(u => u.Id == uid && u.Status == UmsatzStatus.Offen, ct);
            if (Umsatz is null || (kasseId is { } k && k != Umsatz.Konto!.KasseId))
            {
                return NotFound();
            }

            kasseId = Umsatz.Konto!.KasseId;
        }

        if (kasseId is null || await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasseId.Value, ct) is not { } kasse)
        {
            return NotFound();
        }

        Kasse = kasse;
        DarfBuchen = await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse);
        if (Buchung is null && !DarfBuchen)
        {
            return Forbid();
        }

        PartnerSichtbar = await zugriff.PartnerSichtbarAsync(User, kasse);
        var heute = Anzeige.Heute(zeit);
        Beschlussgrenze = await parameter.ZahlAsync(ParameterSchluessel.KasseAusschussbeschlussUeberEuro, heute, 100, ct);
        Konten = await Kassenauswahl.KontenAsync(db, kasse.Id, [Buchung?.KontoId, Buchung?.GegenkontoId], ct);
        Positionen = await Kassenauswahl.PositionenAsync(db, kasse.Id, Buchung?.PlanpositionId, ct);
        Positionshinweise = await db.Planpositionen.AsNoTracking().Where(p => p.KasseId == kasse.Id && p.Hinweis != null)
            .ToDictionaryAsync(p => p.Id.ToString(), p => p.Hinweis!, ct);
        Positionsbereiche = (await db.Planpositionen.AsNoTracking().Where(p => p.KasseId == kasse.Id)
            .Select(p => new { p.Id, p.Steuerbereich }).ToListAsync(ct)).ToDictionary(p => p.Id.ToString(), p => p.Steuerbereich.ToString());
        Veranstaltungen = await Kassenauswahl.VeranstaltungenAsync(db, kasse.Id, heute, Buchung?.VeranstaltungId, ct);
        Beschluesse = await Kassenauswahl.BeschluesseAsync(db, heute, Buchung?.AusschussbeschlussId ?? Anordnung?.AusschussbeschlussId, ct);
        return null;
    }
}
