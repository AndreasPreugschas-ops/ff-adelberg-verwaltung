using Fevad.Domain.Gemeinsam;
using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Unfaelle;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Unfaelle;

/// <summary>
/// Dienstunfall erfassen und bearbeiten, Angaben für die Unfallanzeige als PDF. Erfassen dürfen auch Führungskräfte an der
/// Einsatzstelle (Unfaelle.Anlegen); lesen, melden und abschließen nur Kommandant und Stellvertreter.
/// </summary>
public class BearbeitenModel(AppDbContext db, Datenbereich bereich, ParameterLeser parameter, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    public const int LeereZeugen = 2;

    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public Guid? Id { get; private set; }
    public bool DarfBearbeiten { get; private set; }
    public DateOnly? AnzeigeBis { get; private set; }
    public IReadOnlyList<SelectListItem> Mitglieder { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Einsaetze { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Dienste { get; private set; } = [];

    public sealed class Eingabe
    {
        public uint Version { get; set; }

        [Required(ErrorMessage = "Bitte die verletzte Person wählen."), Display(Name = "Verletzte Person")]
        public Guid? MitgliedId { get; set; }

        [Display(Name = "Anlass")] public UnfallAnlass Anlass { get; set; }
        [Display(Name = "Einsatz")] public Guid? EinsatzId { get; set; }
        [Display(Name = "Dienst")] public Guid? DienstId { get; set; }

        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Datum")] public DateOnly? Datum { get; set; }
        [Required(ErrorMessage = "Uhrzeit fehlt."), Display(Name = "Uhrzeit")] public string Uhrzeit { get; set; } = "";

        [Required(ErrorMessage = "Unfallort fehlt."), StringLength(200), Display(Name = "Unfallort")] public string Ort { get; set; } = "";
        [StringLength(200), Display(Name = "Tätigkeit beim Unfall")] public string? Taetigkeit { get; set; }

        [Required(ErrorMessage = "Hergang fehlt."), StringLength(4000), Display(Name = "Hergang")] public string Hergang { get; set; } = "";
        [StringLength(200), Display(Name = "Verletzte Körperteile")] public string? VerletzteKoerperteile { get; set; }
        [StringLength(200), Display(Name = "Art der Verletzung")] public string? ArtDerVerletzung { get; set; }
        [Display(Name = "Tödlicher Unfall")] public bool Toedlich { get; set; }
        [StringLength(200), Display(Name = "Erstbehandlung (Arzt oder Krankenhaus)")] public string? Erstbehandlung { get; set; }
        [StringLength(100), Display(Name = "Krankenkasse")] public string? Krankenkasse { get; set; }
        [Display(Name = "Dienst eingestellt am")] public string? DienstEingestellt { get; set; }
        [Display(Name = "Voraussichtlich mehr als drei Tage arbeitsunfähig")] public bool MehrAlsDreiTage { get; set; }
        [StringLength(200), Display(Name = "Zuerst Kenntnis genommen")] public string? ZuerstKenntnis { get; set; }

        [Display(Name = "Status")] public UnfallStatus Status { get; set; }
        [StringLength(50), Display(Name = "Aktenzeichen der Unfallkasse")] public string? Aktenzeichen { get; set; }
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }

        public List<ZeugeEingabe> Zeugen { get; set; } = [];
    }

    public sealed class ZeugeEingabe
    {
        [StringLength(200)] public string? Name { get; set; }
        [StringLength(300)] public string? Kontakt { get; set; }
        public bool Augenzeuge { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is null)
        {
            if (!await DarfAsync(Berechtigungen.UnfaelleAnlegen))
            {
                return Forbid();
            }

            var jetzt = Anzeige.Lokal(zeit.GetUtcNow());
            Formular = new Eingabe { Datum = DateOnly.FromDateTime(jetzt.DateTime), Uhrzeit = jetzt.ToString("HH:mm") };
        }
        else
        {
            if (!await DarfAsync(Berechtigungen.UnfaelleLesen))
            {
                return Forbid();
            }

            var u = await db.Unfaelle.Include(x => x.Zeugen).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (u is null)
            {
                return NotFound();
            }

            var lokal = Anzeige.Lokal(u.Zeitpunkt);
            Formular = new Eingabe
            {
                Version = db.Entry(u).Property<uint>("Version").CurrentValue,
                MitgliedId = u.MitgliedId, Anlass = u.Anlass, EinsatzId = u.EinsatzId, DienstId = u.DienstId,
                Datum = DateOnly.FromDateTime(lokal.DateTime), Uhrzeit = lokal.ToString("HH:mm"), Ort = u.Ort, Taetigkeit = u.Taetigkeit,
                Hergang = u.Hergang, VerletzteKoerperteile = u.VerletzteKoerperteile, ArtDerVerletzung = u.ArtDerVerletzung, Toedlich = u.Toedlich,
                Erstbehandlung = u.Erstbehandlung, Krankenkasse = u.Krankenkasse, DienstEingestellt = u.DienstEingestelltAm is { } e ? ZeitEingabe.Text(e) : null,
                MehrAlsDreiTage = u.MehrAlsDreiTageArbeitsunfaehig, ZuerstKenntnis = u.ZuerstKenntnis, Status = u.Status, Aktenzeichen = u.Aktenzeichen,
                Bemerkung = u.Bemerkung,
                Zeugen = u.Zeugen.OrderBy(z => z.ErstelltAm).Select(z => new ZeugeEingabe { Name = z.Name, Kontakt = z.Kontakt, Augenzeuge = z.Augenzeuge }).ToList(),
            };
        }

        await LadenAsync(id, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid? id, CancellationToken ct)
    {
        if (!await DarfAsync(id is null ? Berechtigungen.UnfaelleAnlegen : Berechtigungen.UnfaelleBearbeiten))
        {
            return Forbid();
        }

        var unfall = id is null ? null : await db.Unfaelle.Include(u => u.Zeugen).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (id is not null && unfall is null)
        {
            return NotFound();
        }

        DateTimeOffset? zeitpunkt = null;
        if (Formular.Datum is { } datum)
        {
            if (ZeitEingabe.TryUhrzeit(Formular.Uhrzeit, out var uhrzeit))
            {
                zeitpunkt = Anzeige.AlsUtc(datum, uhrzeit);
            }
            else if (!string.IsNullOrWhiteSpace(Formular.Uhrzeit))
            {
                ModelState.AddModelError("Formular.Uhrzeit", "Uhrzeit bitte als HH:MM (24 Stunden).");
            }
        }

        if (zeitpunkt > zeit.GetUtcNow().AddMinutes(5))
        {
            ModelState.AddModelError("Formular.Datum", "Der Unfall liegt in der Zukunft.");
        }

        DateTimeOffset? eingestellt = null;
        if (!ZeitEingabe.TryLesen(Formular.DienstEingestellt, zeitpunkt, out eingestellt))
        {
            ModelState.AddModelError("Formular.DienstEingestellt", "Bitte als TT.MM.JJJJ HH:MM oder nur HH:MM angeben.");
        }

        var mitglieder = await MitgliederAbfrageAsync(unfall?.MitgliedId, ct);
        if (Formular.MitgliedId is { } m && !await mitglieder.AnyAsync(x => x.Id == m, ct))
        {
            ModelState.AddModelError("Formular.MitgliedId", "Diese Person steht nicht zur Auswahl.");
        }

        if (!ModelState.IsValid || zeitpunkt is null)
        {
            await LadenAsync(id, ct);
            return Page();
        }

        var heute = Anzeige.Heute(zeit);
        if (unfall is null)
        {
            unfall = new Unfall { Ort = Formular.Ort.Trim(), Hergang = Formular.Hergang.Trim() };
            db.Unfaelle.Add(unfall);
        }
        else
        {
            db.Entry(unfall).Property<uint>("Version").OriginalValue = Formular.Version;
            unfall.StatusSetzen(Formular.Status, heute);
            unfall.Aktenzeichen = Leer(Formular.Aktenzeichen);
        }

        unfall.MitgliedId = Formular.MitgliedId!.Value;
        unfall.Anlass = Formular.Anlass;
        unfall.EinsatzId = Formular.Anlass == UnfallAnlass.Einsatz ? Formular.EinsatzId : null;
        unfall.DienstId = Formular.Anlass == UnfallAnlass.Dienst ? Formular.DienstId : null;
        unfall.Zeitpunkt = zeitpunkt.Value;
        unfall.Ort = Formular.Ort.Trim();
        unfall.Taetigkeit = Leer(Formular.Taetigkeit);
        unfall.Hergang = Formular.Hergang.Trim();
        unfall.VerletzteKoerperteile = Leer(Formular.VerletzteKoerperteile);
        unfall.ArtDerVerletzung = Leer(Formular.ArtDerVerletzung);
        unfall.Toedlich = Formular.Toedlich;
        unfall.Erstbehandlung = Leer(Formular.Erstbehandlung);
        unfall.Krankenkasse = Leer(Formular.Krankenkasse);
        unfall.DienstEingestelltAm = eingestellt;
        unfall.MehrAlsDreiTageArbeitsunfaehig = Formular.MehrAlsDreiTage;
        unfall.ZuerstKenntnis = Leer(Formular.ZuerstKenntnis);
        unfall.Bemerkung = Leer(Formular.Bemerkung);

        // Zeugen werden als Ganzes ersetzt; entfernte löscht der Löschlauf (L36) beim nächsten Lauf endgültig.
        db.UnfallZeugen.RemoveRange(unfall.Zeugen);
        unfall.Zeugen = Formular.Zeugen.Where(z => !string.IsNullOrWhiteSpace(z.Name))
            .Select(z => new UnfallZeuge { Name = z.Name!.Trim(), Kontakt = Leer(z.Kontakt), Augenzeuge = z.Augenzeuge }).ToList();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Der Unfall wurde inzwischen von jemand anderem geändert. Bitte neu laden.");
            await LadenAsync(id, ct);
            return Page();
        }

        if (!await DarfAsync(Berechtigungen.UnfaelleLesen))
        {
            TempData["Meldung"] = "Unfall erfasst. Kommandant und Stellvertreter sehen ihn und kümmern sich um die Unfallanzeige.";
            return RedirectToPage("/Index");
        }

        TempData["Meldung"] = "Unfall gespeichert.";
        return RedirectToPage(new { id = unfall.Id });
    }

    /// <summary>Angaben zur Unfallanzeige als PDF; der Dateiname enthält keinen Namen.</summary>
    public async Task<IActionResult> OnGetPdfAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.UnfaelleLesen))
        {
            return Forbid();
        }

        var u = await db.Unfaelle.AsNoTracking().Include(x => x.Mitglied).Include(x => x.Zeugen).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null)
        {
            return NotFound();
        }

        var o = await db.OrganisationAsync(ct);
        var frist = await parameter.ZahlAsync(ParameterSchluessel.UnfallAnzeigefristTage, Anzeige.Heute(zeit), 3, ct);
        var m = u.Mitglied!;
        var bezug = u.EinsatzId is { } e
            ? await db.Einsaetze.Where(x => x.Id == e).Select(x => "Einsatz " + x.Einsatznummer).FirstOrDefaultAsync(ct)
            : u.DienstId is { } d ? await db.Dienste.Where(x => x.Id == d).Select(x => x.Titel).FirstOrDefaultAsync(ct) : null;
        var daten = new UnfallanzeigePdf.Daten(
            new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp), o.GemeindeName, o.GemeindeAnschrift,
            $"{m.Vorname} {m.Nachname}", Anschrift.Zeile(m.Strasse, m.Plz, m.Ort), m.Geburtsdatum, m.Geschlecht is { } g ? Anzeige.Text(g) : null,
            m.Arbeitgeber, u.Krankenkasse, u, bezug,
            u.Zeugen.OrderBy(z => z.ErstelltAm).Select(z => new UnfallanzeigePdf.Zeuge(z.Name, z.Kontakt, z.Augenzeuge)).ToList(), u.AnzeigeBis(frist));
        return File(UnfallanzeigePdf.Erzeugen(daten), "application/pdf", UnfallanzeigePdf.Dateiname(u));
    }

    /// <summary>Versehentlich angelegte Unfälle ausblenden; der Löschlauf entfernt sie beim nächsten Lauf endgültig (L36).</summary>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.UnfaelleBearbeiten))
        {
            return Forbid();
        }

        var u = await db.Unfaelle.Include(x => x.Zeugen).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null)
        {
            return NotFound();
        }

        db.UnfallZeugen.RemoveRange(u.Zeugen);
        db.Unfaelle.Remove(u);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Unfall gelöscht.";
        return RedirectToPage("Index");
    }

    private async Task LadenAsync(Guid? id, CancellationToken ct)
    {
        Id = id;
        DarfBearbeiten = id is null ? await DarfAsync(Berechtigungen.UnfaelleAnlegen) : await DarfAsync(Berechtigungen.UnfaelleBearbeiten);
        while (Formular.Zeugen.Count(z => string.IsNullOrWhiteSpace(z.Name)) < LeereZeugen)
        {
            Formular.Zeugen.Add(new ZeugeEingabe());
        }

        var unfall = id is null ? null : await db.Unfaelle.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (unfall is not null)
        {
            AnzeigeBis = unfall.AnzeigeBis(await parameter.ZahlAsync(ParameterSchluessel.UnfallAnzeigefristTage, Anzeige.Heute(zeit), 3, ct));
        }

        Mitglieder = await (await MitgliederAbfrageAsync(unfall?.MitgliedId, ct))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new SelectListItem(m.Nachname + ", " + m.Vorname, m.Id.ToString())).ToListAsync(ct);

        var seit = zeit.GetUtcNow().AddDays(-90);
        Einsaetze = (await db.Einsaetze.AsNoTracking()
                .Where(e => e.Alarmiert >= seit || e.Id == Formular.EinsatzId)
                .OrderByDescending(e => e.Alarmiert)
                .Select(e => new { e.Id, e.Alarmiert, e.Einsatznummer, e.Stichwort }).ToListAsync(ct))
            .Select(e => new SelectListItem($"{Anzeige.DatumZeit(e.Alarmiert)} · {e.Einsatznummer} {e.Stichwort}", e.Id.ToString())).ToList();
        Dienste = (await db.Dienste.AsNoTracking()
                .Where(d => (d.Beginn >= seit && d.Beginn <= zeit.GetUtcNow() && d.AlsDienst) || d.Id == Formular.DienstId)
                .OrderByDescending(d => d.Beginn)
                .Select(d => new { d.Id, d.Beginn, d.Titel }).ToListAsync(ct))
            .Select(d => new SelectListItem($"{Anzeige.DatumZeit(d.Beginn)} · {d.Titel}", d.Id.ToString())).ToList();
    }

    /// <summary>Wer heute Mitglied ist (Jugendfeuerwehr nur mit Jugendzugang), dazu die schon eingetragene Person.</summary>
    private async Task<IQueryable<Fevad.Domain.Mitglieder.Mitglied>> MitgliederAbfrageAsync(Guid? bisher, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var aktiv = db.Mitglieder.Where(m => m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute.AddDays(-90))));
        var sichtbar = await bereich.OhneJugendAsync(aktiv, User, heute);
        return bisher is { } b ? sichtbar.Union(db.Mitglieder.Where(m => m.Id == b)) : sichtbar;
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
