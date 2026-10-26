using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Kommunikation;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitteilungen;

/// <summary>Mitteilung als Entwurf anlegen oder ändern (V42); veröffentlicht wird auf der Detailseite nach Blick auf die Empfängerzahl.</summary>
[Authorize(Policy = Berechtigungen.MitteilungenVerfassen)]
public class BearbeitenModel(AppDbContext db, Schreibrecht schreibrecht, TimeProvider zeit) : PageModel
{
    /// <summary>Formularfelder.</summary>
    public sealed class Eingabe
    {
        /// <summary>Titel.</summary>
        [Required(ErrorMessage = "Bitte einen Titel angeben.")]
        [StringLength(200)]
        [Display(Name = "Titel")]
        public string Titel { get; set; } = "";

        /// <summary>Text.</summary>
        [Required(ErrorMessage = "Bitte einen Text angeben.")]
        [StringLength(Mitteilung.TextLaenge)]
        [Display(Name = "Text")]
        public string Text { get; set; } = "";

        /// <summary>Zielgruppe.</summary>
        [Display(Name = "An")]
        public MitteilungZiel Ziel { get; set; }

        /// <summary>Gewählte Abteilungen.</summary>
        public List<Guid> Abteilungen { get; set; } = [];

        /// <summary>Gewählte Jugendveranstaltung.</summary>
        [Display(Name = "Veranstaltung")]
        public Guid? JugendveranstaltungId { get; set; }

        /// <summary>Lesebestätigung erbitten.</summary>
        [Display(Name = "Lesebestätigung erbitten")]
        public bool Lesebestaetigung { get; set; }

        /// <summary>Zusätzlich sofort per Mail.</summary>
        [Display(Name = "dringend: zusätzlich sofort eine Mail mit Titel und Link schicken")]
        public bool SofortPerMail { get; set; }

        /// <summary>Oben anzeigen bis.</summary>
        [Display(Name = "Aktuell bis")]
        public DateOnly? AktuellBis { get; set; }
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
            var m = await db.Mitteilungen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null)
            {
                return NotFound();
            }

            if (m.Status != MitteilungStatus.Entwurf || !await schreibrecht.DarfAsync(User, m, ct))
            {
                return Forbid();
            }

            Formular = new Eingabe
            {
                Titel = m.Titel, Text = m.Text, Ziel = m.Ziel, Abteilungen = m.Abteilungen, JugendveranstaltungId = m.JugendveranstaltungId,
                Lesebestaetigung = m.Lesebestaetigung, SofortPerMail = m.SofortPerMail, AktuellBis = m.AktuellBis,
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
        Mitteilung m;
        if (Id is { } id)
        {
            var vorhanden = await db.Mitteilungen.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            if (vorhanden.Status != MitteilungStatus.Entwurf || !await schreibrecht.DarfAsync(User, vorhanden, ct))
            {
                return Forbid();
            }

            m = vorhanden;
        }
        else
        {
            m = new Mitteilung { Titel = "", Text = "" };
        }

        m.Titel = Formular.Titel?.Trim() ?? "";
        m.Text = Formular.Text?.Trim() ?? "";
        m.Ziel = Formular.Ziel;
        m.Abteilungen = Formular.Ziel == MitteilungZiel.Veranstaltung ? [] : Formular.Abteilungen.Distinct().ToList();
        m.JugendveranstaltungId = Formular.Ziel == MitteilungZiel.Veranstaltung ? Formular.JugendveranstaltungId : null;
        m.Lesebestaetigung = Formular.Lesebestaetigung;
        m.SofortPerMail = Formular.SofortPerMail;
        m.AktuellBis = Formular.AktuellBis;

        foreach (var fehler in m.Pruefen())
        {
            ModelState.AddModelError(string.Empty, fehler);
        }

        var erlaubt = Abteilungen.Where(a => Formular.Ziel != MitteilungZiel.Eltern || a.Jugend).Select(a => a.Id).ToHashSet();
        if (m.Abteilungen.Any(a => !erlaubt.Contains(a)))
        {
            ModelState.AddModelError(string.Empty, Formular.Ziel == MitteilungZiel.Eltern
                ? "Elternpost geht nur an Jugend- oder Kinderfeuerwehr, für die du schreiben darfst."
                : "An diese Abteilung darfst du nicht schreiben.");
        }

        if (m.JugendveranstaltungId is { } v && Veranstaltungen.All(x => x.Id != v))
        {
            ModelState.AddModelError(string.Empty, "Diese Veranstaltung steht nicht zur Auswahl.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Id is null)
        {
            db.Mitteilungen.Add(m);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Entwurf gespeichert. Prüfe die Empfänger und veröffentliche die Mitteilung.";
        return RedirectToPage("Details", new { id = m.Id });
    }

    /// <summary>Löscht einen Entwurf.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Übersicht.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(CancellationToken ct)
    {
        var m = await db.Mitteilungen.FirstOrDefaultAsync(x => x.Id == Id, ct);
        if (m is null)
        {
            return NotFound();
        }

        if (m.Status != MitteilungStatus.Entwurf || !await schreibrecht.DarfAsync(User, m, ct))
        {
            return Forbid();
        }

        db.Mitteilungen.Remove(m);
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
