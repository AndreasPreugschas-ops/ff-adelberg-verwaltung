using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitglieder;

/// <summary>Mit Mitglieder.Bearbeiten alle Mitglieder, mit Jugend.Bearbeiten nur die Jugendfeuerwehr (siehe <see cref="Datenbereich"/>).</summary>
public class BearbeitenModel(AppDbContext db, Datenbereich datenbereich, TimeProvider zeit) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public bool IstNeu => Formular.Id is null;
    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];
    public IReadOnlyList<Mitgliedschaft> Verlauf { get; private set; } = [];

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }

        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }

        [Required(ErrorMessage = "Personalnummer fehlt."), StringLength(20), Display(Name = "Personalnummer")]
        public string Personalnummer { get; set; } = "";

        [Required(ErrorMessage = "Vorname fehlt."), StringLength(100), Display(Name = "Vorname")]
        public string Vorname { get; set; } = "";

        [Required(ErrorMessage = "Nachname fehlt."), StringLength(100), Display(Name = "Nachname")]
        public string Nachname { get; set; } = "";

        [Required(ErrorMessage = "Geburtsdatum fehlt."), Display(Name = "Geburtsdatum")]
        public DateOnly? Geburtsdatum { get; set; }

        [Display(Name = "Straße")] public string? Strasse { get; set; }
        [Display(Name = "PLZ")] public string? Plz { get; set; }
        [Display(Name = "Ort")] public string? Ort { get; set; }

        [Phone, Display(Name = "Mobil (+49...)")] public string? TelefonMobil { get; set; }
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), Display(Name = "E-Mail")] public string? Email { get; set; }

        [Display(Name = "Abteilung")] public Guid? AbteilungId { get; set; }
        [Display(Name = "Eintritt")] public DateOnly? Eintritt { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (!await datenbereich.DarfBearbeitenAsync(User, db.Mitglieder, id, Anzeige.Heute(zeit), ct))
        {
            return Forbid();
        }

        if (id is not null)
        {
            var mitglied = await db.Mitglieder.FirstOrDefaultAsync(m => m.Id == id, ct);
            if (mitglied is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = mitglied.Id,
                Version = db.Entry(mitglied).Property<uint>("Version").CurrentValue,
                Personalnummer = mitglied.Personalnummer,
                Vorname = mitglied.Vorname,
                Nachname = mitglied.Nachname,
                Geburtsdatum = mitglied.Geburtsdatum,
                Strasse = mitglied.Strasse,
                Plz = mitglied.Plz,
                Ort = mitglied.Ort,
                TelefonMobil = mitglied.TelefonMobil,
                Email = mitglied.Email,
            };
        }
        else
        {
            Formular.Eintritt = Anzeige.Heute(zeit);
        }

        await AuswahlLadenAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await datenbereich.DarfBearbeitenAsync(User, db.Mitglieder, Formular.Id, Anzeige.Heute(zeit), ct))
        {
            return Forbid();
        }

        if (IstNeu && Formular.AbteilungId is { } abteilungId && await datenbereich.NurJugendAnlegenAsync(User)
            && !await db.Abteilungen.AnyAsync(a => a.Id == abteilungId && a.Code == AbteilungCode.Jugend, ct))
        {
            ModelState.AddModelError("Formular.AbteilungId", "Du kannst nur Mitglieder der Jugendfeuerwehr anlegen.");
        }

        if (IstNeu && Formular.AbteilungId is null)
        {
            ModelState.AddModelError("Formular.AbteilungId", "Abteilung fehlt.");
        }

        // Der eindeutige Index gilt auch für gelöschte Mitglieder.
        var personalnummer = Formular.Personalnummer.Trim();
        if (await db.Mitglieder.IgnoreQueryFilters().AnyAsync(m => m.Personalnummer == personalnummer && m.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Personalnummer", "Diese Personalnummer ist schon vergeben.");
        }

        if (!ModelState.IsValid)
        {
            await AuswahlLadenAsync(ct);
            return Page();
        }

        Mitglied mitglied;
        if (IstNeu)
        {
            mitglied = new Mitglied
            {
                Personalnummer = Formular.Personalnummer,
                Vorname = Formular.Vorname,
                Nachname = Formular.Nachname,
            };
            mitglied.Mitgliedschaften.Add(new Mitgliedschaft
            {
                AbteilungId = Formular.AbteilungId!.Value,
                Status = MitgliedStatus.Probezeit,
                GueltigAb = Formular.Eintritt ?? Anzeige.Heute(zeit),
            });
            db.Mitglieder.Add(mitglied);
        }
        else
        {
            var vorhanden = await db.Mitglieder.FirstOrDefaultAsync(m => m.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            mitglied = vorhanden;
            db.Entry(mitglied).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        mitglied.Personalnummer = personalnummer;
        mitglied.Vorname = Formular.Vorname.Trim();
        mitglied.Nachname = Formular.Nachname.Trim();
        mitglied.Geburtsdatum = Formular.Geburtsdatum!.Value;
        mitglied.Strasse = Formular.Strasse;
        mitglied.Plz = Formular.Plz;
        mitglied.Ort = Formular.Ort;
        mitglied.TelefonMobil = Formular.TelefonMobil;
        mitglied.Email = Formular.Email;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Das Mitglied wurde inzwischen von jemand anderem geändert. Bitte neu laden und die Änderungen erneut eintragen.");
            await AuswahlLadenAsync(ct);
            return Page();
        }

        TempData["Meldung"] = $"{mitglied.Anzeigename} gespeichert.";
        return RedirectToPage("Index");
    }

    private async Task AuswahlLadenAsync(CancellationToken ct)
    {
        var nurJugend = await datenbereich.NurJugendAnlegenAsync(User);
        Abteilungen = await db.Abteilungen
            .Where(a => a.Aktiv && (!nurJugend || a.Code == AbteilungCode.Jugend))
            .OrderBy(a => a.Sortierung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString()))
            .ToListAsync(ct);

        if (Formular.Id is not null)
        {
            Verlauf = await db.Mitgliedschaften
                .Include(m => m.Abteilung)
                .Where(m => m.MitgliedId == Formular.Id)
                .OrderByDescending(m => m.GueltigAb)
                .ToListAsync(ct);
        }
    }
}
