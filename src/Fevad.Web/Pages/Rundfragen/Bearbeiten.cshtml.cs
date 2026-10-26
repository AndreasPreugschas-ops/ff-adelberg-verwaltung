using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Kommunikation;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Mitteilungen;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Rundfragen;

/// <summary>Rundfrage oder Umfrage als Entwurf anlegen oder ändern (V42); verschickt wird auf der Detailseite nach Blick auf die Empfängerzahl.</summary>
[Authorize(Policy = Berechtigungen.MitteilungenVerfassen)]
public class BearbeitenModel(AppDbContext db, Schreibrecht schreibrecht, TimeProvider zeit) : PageModel
{
    /// <summary>Formularfelder.</summary>
    public sealed class Eingabe
    {
        /// <summary>Frage.</summary>
        [Required(ErrorMessage = "Bitte die Frage angeben.")]
        [StringLength(200)]
        [Display(Name = "Frage")]
        public string Titel { get; set; } = "";

        /// <summary>Erläuterung.</summary>
        [StringLength(2000)]
        [Display(Name = "Erläuterung")]
        public string? Text { get; set; }

        /// <summary>Art.</summary>
        [Display(Name = "Art")]
        public RundfrageArt Art { get; set; }

        /// <summary>Zielgruppe.</summary>
        [Display(Name = "An")]
        public MitteilungZiel Ziel { get; set; }

        /// <summary>Gewählte Abteilungen.</summary>
        public List<Guid> Abteilungen { get; set; } = [];

        /// <summary>Gewählte Jugendveranstaltung.</summary>
        [Display(Name = "Veranstaltung")]
        public Guid? JugendveranstaltungId { get; set; }

        /// <summary>Tag, um den es geht.</summary>
        [Display(Name = "Für den Tag")]
        public DateOnly? Tag { get; set; }

        /// <summary>Antwortmöglichkeiten, eine je Zeile.</summary>
        [Display(Name = "Antwortmöglichkeiten (eine je Zeile)")]
        public string? Optionen { get; set; }

        /// <summary>Mehrfachauswahl.</summary>
        [Display(Name = "mehrere Antworten zulassen")]
        public bool Mehrfachauswahl { get; set; }

        /// <summary>Anonym.</summary>
        [Display(Name = "anonym (du siehst nur Summen, Antworten lassen sich nicht ändern)")]
        public bool Anonym { get; set; }

        /// <summary>Antworten bis.</summary>
        [Display(Name = "Antworten bis einschließlich")]
        public DateOnly? AntwortBis { get; set; }
    }

    /// <summary>Kennung beim Bearbeiten.</summary>
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }

    /// <summary>Formular.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Abteilungen, an die der Benutzer schreiben darf.</summary>
    public IReadOnlyList<Schreibrecht.Abteilungswahl> Abteilungen { get; private set; } = [];

    /// <summary>Kommende Jugendveranstaltungen in diesen Abteilungen.</summary>
    public IReadOnlyList<(Guid Id, string Titel)> Veranstaltungen { get; private set; } = [];

    /// <summary>Zeigt das Formular.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 404 oder 403.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id is { } id)
        {
            var r = await db.Rundfragen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (r is null)
            {
                return NotFound();
            }

            if (r.Status != RundfrageStatus.Entwurf || !await schreibrecht.DarfAsync(User, r.Ziel, r.Abteilungen, r.JugendveranstaltungId, ct))
            {
                return Forbid();
            }

            Formular = new Eingabe
            {
                Titel = r.Titel, Text = r.Text, Art = r.Art, Ziel = r.Ziel, Abteilungen = r.Abteilungen, JugendveranstaltungId = r.JugendveranstaltungId,
                Tag = r.Tag, Optionen = string.Join("\n", r.Optionen), Mehrfachauswahl = r.Mehrfachauswahl, Anonym = r.Anonym, AntwortBis = r.AntwortBis,
            };
        }

        await AuswahlLadenAsync(ct);
        return Page();
    }

    /// <summary>Speichert den Entwurf.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiter zur Detailseite oder das Formular mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await AuswahlLadenAsync(ct);
        Rundfrage r;
        if (Id is { } id)
        {
            var vorhanden = await db.Rundfragen.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            if (vorhanden.Status != RundfrageStatus.Entwurf || !await schreibrecht.DarfAsync(User, vorhanden.Ziel, vorhanden.Abteilungen, vorhanden.JugendveranstaltungId, ct))
            {
                return Forbid();
            }

            r = vorhanden;
        }
        else
        {
            r = new Rundfrage { Titel = "" };
        }

        var umfrage = Formular.Art == RundfrageArt.Umfrage;
        r.Titel = Formular.Titel?.Trim() ?? "";
        r.Text = string.IsNullOrWhiteSpace(Formular.Text) ? null : Formular.Text.Trim();
        r.Art = Formular.Art;
        r.Ziel = Formular.Ziel;
        r.Abteilungen = Formular.Ziel == MitteilungZiel.Veranstaltung ? [] : Formular.Abteilungen.Distinct().ToList();
        r.JugendveranstaltungId = Formular.Ziel == MitteilungZiel.Veranstaltung ? Formular.JugendveranstaltungId : null;
        r.Tag = umfrage ? null : Formular.Tag;
        r.Optionen = umfrage
            ? (Formular.Optionen ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList()
            : [];
        r.Mehrfachauswahl = umfrage && Formular.Mehrfachauswahl;
        r.Anonym = umfrage && Formular.Anonym;
        r.AntwortBis = Formular.AntwortBis;

        foreach (var fehler in r.Pruefen())
        {
            ModelState.AddModelError(string.Empty, fehler);
        }

        var erlaubt = Abteilungen.Where(a => Formular.Ziel != MitteilungZiel.Eltern || a.Jugend).Select(a => a.Id).ToHashSet();
        if (r.Abteilungen.Any(a => !erlaubt.Contains(a)))
        {
            ModelState.AddModelError(string.Empty, Formular.Ziel == MitteilungZiel.Eltern
                ? "Fragen an Eltern gehen nur an Jugend- oder Kinderfeuerwehr, für die du schreiben darfst."
                : "An diese Abteilung darfst du nicht schreiben.");
        }

        if (r.JugendveranstaltungId is { } v && Veranstaltungen.All(x => x.Id != v))
        {
            ModelState.AddModelError(string.Empty, "Diese Veranstaltung steht nicht zur Auswahl.");
        }

        if (r.AntwortBis is { } bis && bis < Anzeige.Heute(zeit))
        {
            ModelState.AddModelError(string.Empty, "Die Antwortfrist liegt in der Vergangenheit.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Id is null)
        {
            db.Rundfragen.Add(r);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Entwurf gespeichert. Prüfe die Empfänger und verschicke die Rundfrage.";
        return RedirectToPage("Details", new { id = r.Id });
    }

    /// <summary>Löscht einen Entwurf.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Übersicht.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(CancellationToken ct)
    {
        var r = await db.Rundfragen.FirstOrDefaultAsync(x => x.Id == Id, ct);
        if (r is null)
        {
            return NotFound();
        }

        if (r.Status != RundfrageStatus.Entwurf || !await schreibrecht.DarfAsync(User, r.Ziel, r.Abteilungen, r.JugendveranstaltungId, ct))
        {
            return Forbid();
        }

        db.Rundfragen.Remove(r);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Entwurf gelöscht.";
        return RedirectToPage("Index");
    }

    private async Task AuswahlLadenAsync(CancellationToken ct)
    {
        Abteilungen = await schreibrecht.AbteilungenAsync(User, ct);
        var erlaubt = Abteilungen.Select(a => a.Id).ToHashSet();
        var ab = zeit.GetUtcNow().AddDays(-30);
        var kandidaten = await db.Jugendveranstaltungen.AsNoTracking()
            .Where(v => v.Dienst!.Beginn >= ab)
            .OrderBy(v => v.Dienst!.Beginn)
            .Select(v => new { v.Id, v.Dienst!.Titel, v.Dienst.Beginn, Abteilungen = v.Dienst.Abteilungen.Select(a => a.Id).ToList() })
            .ToListAsync(ct);
        Veranstaltungen = kandidaten.Where(k => k.Abteilungen.Count > 0 && k.Abteilungen.All(erlaubt.Contains))
            .Select(k => (k.Id, $"{k.Titel} ({Anzeige.DatumZeit(k.Beginn)})")).ToList();
    }
}
