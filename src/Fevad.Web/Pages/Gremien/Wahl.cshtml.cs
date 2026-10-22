using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gremien;

/// <summary>
/// Wahlgang erfassen: Bewerber und Stimmen eintragen, Ergebnis feststellen, Annahme der Wahl
/// und Zustimmung des Gemeinderats vermerken, danach in die Funktionen der Mitglieder übernehmen.
/// </summary>
public class WahlModel(AppDbContext db, Gremienarbeit gremien, IAuthorizationService autorisierung) : PageModel
{
    [BindProperty] public Eingabe Formular { get; set; } = new();
    [BindProperty] public List<BewerberEingabe> Bewerber { get; set; } = [];
    [BindProperty] public bool BisherigeBeenden { get; set; } = true;

    public Wahl? Wahl { get; private set; }
    public Sitzung? Sitzung { get; private set; }
    public bool DarfBearbeiten { get; private set; }
    public bool DarfUebernehmen { get; private set; }
    public IReadOnlyList<Auswahl.Person> Mitglieder { get; private set; } = [];
    public IReadOnlyList<FunktionWahl> Funktionen { get; private set; } = [];
    public IReadOnlyList<string> Hinweise { get; private set; } = [];
    public IReadOnlyList<string> BisherigeInhaber { get; private set; } = [];

    /// <summary>Funktion mit Vorgaben für das Formular: Kommandant und Stellvertreter brauchen absolute Mehrheit und den Gemeinderat.</summary>
    public sealed record FunktionWahl(Guid Id, string Bezeichnung, int? AmtszeitJahre, bool Fuehrung);

    public sealed class Eingabe
    {
        [Required(ErrorMessage = "Funktion fehlt."), Display(Name = "Funktion")] public Guid? FunktionId { get; set; }
        [Range(1, 9), Display(Name = "Wahlgang")] public int Wahlgang { get; set; } = 1;
        [Display(Name = "Verfahren")] public Wahlverfahren Verfahren { get; set; }
        [Display(Name = "geheime Wahl")] public bool Geheim { get; set; } = true;
        [Range(1, 20), Display(Name = "Sitze")] public int Sitze { get; set; } = 1;
        [Range(0, 999), Display(Name = "anwesende Wahlberechtigte")] public int? Stimmberechtigte { get; set; }
        [Range(0, 999), Display(Name = "abgegebene Stimmzettel")] public int? AbgegebeneStimmzettel { get; set; }
        [Range(0, 999), Display(Name = "ungültige Stimmzettel")] public int? UngueltigeStimmzettel { get; set; }
        [Range(0, 9999), Display(Name = "gültige Stimmen")] public int? GueltigeStimmen { get; set; }
        [Display(Name = "Amtsbeginn")] public DateOnly? Amtsbeginn { get; set; }
        [Range(1, 20), Display(Name = "Amtszeit (Jahre)")] public int? AmtszeitJahre { get; set; }
        [Display(Name = "Zustimmung des Gemeinderats nötig")] public bool ZustimmungGemeinderatErforderlich { get; set; }
        [Display(Name = "Zustimmung Gemeinderat am")] public DateOnly? ZustimmungGemeinderatAm { get; set; }
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    public sealed class BewerberEingabe
    {
        public Guid? MitgliedId { get; set; }
        [Range(0, 999)] public int Stimmen { get; set; }
        public bool Gewaehlt { get; set; }
        public DateOnly? AngenommenAm { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? sitzungId, CancellationToken ct)
    {
        if (!await LadenAsync(id, sitzungId, ct))
        {
            return NotFound();
        }

        if (Wahl is null)
        {
            if (!DarfBearbeiten)
            {
                return Forbid();
            }

            Formular.Stimmberechtigte = Sitzung!.Teilnahmen.Count(t => t.Stimmberechtigt && t.Status == TeilnahmeStatus.Anwesend) is > 0 and var n ? n : null;
            Formular.Amtsbeginn = Gremienarbeit.Sitzungstag(Sitzung);
            return Page();
        }

        var w = Wahl;
        Formular = new Eingabe
        {
            FunktionId = w.FunktionId,
            Wahlgang = w.Wahlgang,
            Verfahren = w.Verfahren,
            Geheim = w.Geheim,
            Sitze = w.Sitze,
            Stimmberechtigte = w.Stimmberechtigte,
            AbgegebeneStimmzettel = w.AbgegebeneStimmzettel,
            UngueltigeStimmzettel = w.UngueltigeStimmzettel,
            GueltigeStimmen = w.GueltigeStimmen,
            Amtsbeginn = w.Amtsbeginn,
            AmtszeitJahre = w.AmtszeitJahre,
            ZustimmungGemeinderatErforderlich = w.ZustimmungGemeinderatErforderlich,
            ZustimmungGemeinderatAm = w.ZustimmungGemeinderatAm,
            Bemerkung = w.Bemerkung,
        };
        Bewerber = w.Bewerber.OrderBy(b => b.Platz ?? int.MaxValue).ThenBy(b => b.Mitglied?.Nachname)
            .Select(b => new BewerberEingabe { MitgliedId = b.MitgliedId, Stimmen = b.Stimmen, Gewaehlt = b.Gewaehlt, AngenommenAm = b.AngenommenAm })
            .ToList();
        return Page();
    }

    /// <summary>Speichert Angaben und Stimmen; „gewählt“ bleibt, wie von Hand gesetzt.</summary>
    public async Task<IActionResult> OnPostAsync(Guid? id, Guid? sitzungId, CancellationToken ct) =>
        await SpeichernAsync(id, sitzungId, feststellen: false, ct);

    /// <summary>Speichert und ermittelt die Gewählten aus den Stimmen.</summary>
    public async Task<IActionResult> OnPostFeststellenAsync(Guid? id, Guid? sitzungId, CancellationToken ct) =>
        await SpeichernAsync(id, sitzungId, feststellen: true, ct);

    public async Task<IActionResult> OnPostUebernehmenAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, null, ct) || Wahl is null)
        {
            return NotFound();
        }

        if (!DarfUebernehmen)
        {
            return Forbid();
        }

        try
        {
            var anzahl = await gremien.WahlUebernehmenAsync(Wahl, BisherigeBeenden, ct);
            TempData["Meldung"] = $"{anzahl} Funktion(en) mit Amtszeit bis {Wahl.Amtsende:dd.MM.yyyy} angelegt.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Fehler"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }

    private async Task<IActionResult> SpeichernAsync(Guid? id, Guid? sitzungId, bool feststellen, CancellationToken ct)
    {
        if (!await LadenAsync(id, sitzungId, ct))
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        if (Wahl?.Status == WahlStatus.Uebernommen)
        {
            TempData["Fehler"] = "Die Wahl ist schon in die Funktionen übernommen und kann nicht mehr geändert werden.";
            return RedirectToPage(new { id });
        }

        var funktion = Formular.FunktionId is { } fid ? await db.Funktionen.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fid, ct) : null;
        if (Formular.FunktionId is not null && funktion is null)
        {
            ModelState.AddModelError("Formular.FunktionId", "Unbekannte Funktion.");
        }

        var bekannt = Mitglieder.Select(m => m.Id).ToHashSet();
        var eingaben = Bewerber.Where(b => b.MitgliedId is not null).ToList();
        if (eingaben.Any(b => !bekannt.Contains(b.MitgliedId!.Value) && Wahl?.Bewerber.All(x => x.MitgliedId != b.MitgliedId) != false))
        {
            ModelState.AddModelError(nameof(Bewerber), "Unbekannter Bewerber.");
        }

        if (eingaben.GroupBy(b => b.MitgliedId).Any(g => g.Count() > 1))
        {
            ModelState.AddModelError(nameof(Bewerber), "Ein Bewerber steht doppelt in der Liste.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var w = Wahl;
        if (w is null)
        {
            w = new Wahl { SitzungId = Sitzung!.Id };
            db.Wahlen.Add(w);
        }

        w.FunktionId = funktion!.Id;
        w.Wahlgang = Formular.Wahlgang;
        w.Verfahren = Formular.Verfahren;
        w.Geheim = Formular.Geheim;
        w.Sitze = Formular.Sitze;
        w.Stimmberechtigte = Formular.Stimmberechtigte;
        w.AbgegebeneStimmzettel = Formular.AbgegebeneStimmzettel;
        w.UngueltigeStimmzettel = Formular.UngueltigeStimmzettel;
        w.GueltigeStimmen = Formular.GueltigeStimmen;
        w.Amtsbeginn = Formular.Amtsbeginn;
        // Ohne Angabe gilt die Amtszeit aus dem Funktionskatalog (z. B. 5 Jahre nach Satzung).
        w.AmtszeitJahre = Formular.AmtszeitJahre ?? funktion.AmtszeitJahre;
        w.ZustimmungGemeinderatErforderlich = Formular.ZustimmungGemeinderatErforderlich;
        w.ZustimmungGemeinderatAm = Formular.ZustimmungGemeinderatAm;
        w.Bemerkung = string.IsNullOrWhiteSpace(Formular.Bemerkung) ? null : Formular.Bemerkung.Trim();

        foreach (var weg in w.Bewerber.Where(b => eingaben.All(e => e.MitgliedId != b.MitgliedId)).ToList())
        {
            w.Bewerber.Remove(weg);
            db.WahlBewerber.Remove(weg);
        }

        foreach (var e in eingaben)
        {
            var b = w.Bewerber.FirstOrDefault(x => x.MitgliedId == e.MitgliedId);
            if (b is null)
            {
                b = new WahlBewerber { WahlId = w.Id, MitgliedId = e.MitgliedId!.Value };
                w.Bewerber.Add(b);
                db.WahlBewerber.Add(b);
            }

            b.Stimmen = e.Stimmen;
            b.Gewaehlt = e.Gewaehlt;
            b.AngenommenAm = e.AngenommenAm;
        }

        if (feststellen)
        {
            var ergebnis = w.ErgebnisFeststellen();
            w.Status = WahlStatus.Festgestellt;
            // Annahme bleibt nur bei weiterhin Gewählten stehen.
            w.Bewerber.Where(b => !b.Gewaehlt).ToList().ForEach(b => b.AngenommenAm = null);
            TempData["Meldung"] = ergebnis.Gewaehlte.Count == 0
                ? "Niemand ist gewählt."
                : $"Gewählt: {string.Join(", ", ergebnis.Gewaehlte.Select(b => Mitglieder.FirstOrDefault(m => m.Id == b.MitgliedId)?.Name))}.";
            if (ergebnis.Hinweise.Count > 0)
            {
                TempData["Fehler"] = string.Join(" ", ergebnis.Hinweise);
            }
        }
        else
        {
            TempData["Meldung"] = "Wahl gespeichert.";
        }

        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { id = w.Id });
    }

    private async Task<bool> LadenAsync(Guid? id, Guid? sitzungId, CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.GremienBearbeiten)).Succeeded;
        DarfUebernehmen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.WahlenUebernehmen)).Succeeded;

        if (id is not null)
        {
            Wahl = await db.Wahlen.Include(w => w.Bewerber).ThenInclude(b => b.Mitglied).Include(w => w.Funktion)
                .FirstOrDefaultAsync(w => w.Id == id, ct);
            if (Wahl is null)
            {
                return false;
            }

            sitzungId = Wahl.SitzungId;
        }

        Sitzung = sitzungId is null ? null : await db.Sitzungen.Include(s => s.Teilnahmen).FirstOrDefaultAsync(s => s.Id == sitzungId, ct);
        if (Sitzung is null)
        {
            return false;
        }

        Mitglieder = await Auswahl.AktiveMitgliederAsync(db, ct);
        Funktionen = await db.Funktionen.AsNoTracking().Where(f => f.Aktiv).OrderBy(f => f.Sortierung)
            .Select(f => new FunktionWahl(f.Id, f.Bezeichnung, f.AmtszeitJahre,
                f.VorgeschlageneRolle == Rollen.Kommandant || f.VorgeschlageneRolle == Rollen.Stellvertreter))
            .ToListAsync(ct);

        if (Wahl is not null)
        {
            Hinweise = Wahl.Status == WahlStatus.Festgestellt
                ? PruefenVorUebernahme(Wahl, Sitzung)
                : [];
            var ende = Wahl.Amtsbeginn?.AddDays(-1);
            BisherigeInhaber = ende is null ? [] : await db.FunktionZuordnungen.AsNoTracking()
                .Where(f => f.FunktionId == Wahl.FunktionId && f.GueltigAb <= ende && (f.GueltigBis == null || f.GueltigBis > ende))
                .Select(f => f.Mitglied!.Nachname + ", " + f.Mitglied.Vorname)
                .ToListAsync(ct);
        }

        return true;
    }

    private static List<string> PruefenVorUebernahme(Wahl wahl, Sitzung sitzung)
    {
        var hinweise = new List<string>();
        if (wahl.ZustimmungGemeinderatErforderlich && sitzung.DemBuergermeisterVorgelegtAm is null)
        {
            hinweise.Add("Die Niederschrift binnen einer Woche dem Bürgermeister zur Vorlage an den Gemeinderat übergeben und das Datum bei der Sitzung eintragen.");
        }

        if (wahl.Amtsbeginn is null)
        {
            hinweise.Add("Amtsbeginn eintragen.");
        }

        if (wahl.AmtszeitJahre is null)
        {
            hinweise.Add("Amtszeit eintragen (oder im Funktionskatalog hinterlegen).");
        }

        if (wahl.Bewerber.Any(b => b.Gewaehlt && b.AngenommenAm is null))
        {
            hinweise.Add("Bei allen Gewählten das Datum der Annahme der Wahl eintragen.");
        }

        if (wahl.ZustimmungGemeinderatErforderlich && wahl.ZustimmungGemeinderatAm is null)
        {
            hinweise.Add("Die Zustimmung des Gemeinderats steht noch aus. Erst danach bestellt der Bürgermeister.");
        }

        return hinweise;
    }
}
