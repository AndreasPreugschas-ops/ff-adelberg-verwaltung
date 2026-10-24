using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Mitglieder;

/// <summary>
/// Mit Mitglieder.Bearbeiten alle Mitglieder, mit Jugend.Bearbeiten nur Jugend- und Kinderfeuerwehr (siehe <see cref="Datenbereich"/>).
/// Beruf und Arbeitgeber nur mit Mitglieder.Arbeitgeber.
/// </summary>
public class BearbeitenModel(
    AppDbContext db, Datenbereich datenbereich, Laufbahnabfrage laufbahn, ParameterLeser parameter, IAuthorizationService autorisierung,
    TimeProvider zeit) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public bool IstNeu => Formular.Id is null;
    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];
    public IReadOnlyList<Mitgliedschaft> Verlauf { get; private set; } = [];
    public bool DarfArbeitgeber { get; private set; }
    public DateOnly Heute => Anzeige.Heute(zeit);
    public DateOnly? FotoWiderrufAm { get; private set; }

    /// <summary>Alter, ab dem die Erziehungsberechtigten gelöscht werden (Löschkonzept L15).</summary>
    public int ElternBisAlter { get; private set; } = 18;

    /// <summary>Erziehungsberechtigte nur bei neuen und minderjährigen Mitgliedern; bei Volljährigen bleiben sie verborgen und unverändert.</summary>
    public bool ElternSichtbar { get; private set; } = true;

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

        [Display(Name = "Geschlecht")] public Geschlecht? Geschlecht { get; set; }

        [Display(Name = "Straße")] public string? Strasse { get; set; }
        [Display(Name = "PLZ")] public string? Plz { get; set; }
        [Display(Name = "Ort")] public string? Ort { get; set; }

        [Phone, StringLength(20), Display(Name = "Mobil (+49...)")] public string? TelefonMobil { get; set; }
        [Phone, StringLength(20), Display(Name = "Telefon privat")] public string? TelefonFestnetz { get; set; }
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), StringLength(200), Display(Name = "E-Mail")] public string? Email { get; set; }

        [StringLength(100), Display(Name = "Beruf")] public string? Beruf { get; set; }
        [StringLength(200), Display(Name = "Arbeitgeber")] public string? Arbeitgeber { get; set; }
        [StringLength(200), Display(Name = "Straße")] public string? ArbeitgeberStrasse { get; set; }
        [StringLength(10), Display(Name = "PLZ")] public string? ArbeitgeberPlz { get; set; }
        [StringLength(100), Display(Name = "Ort")] public string? Arbeitsort { get; set; }
        [Phone, StringLength(20), Display(Name = "Telefon dienstlich")] public string? ArbeitgeberTelefon { get; set; }
        [Phone, StringLength(20), Display(Name = "Mobil dienstlich")] public string? ArbeitgeberMobil { get; set; }
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), StringLength(200), Display(Name = "E-Mail dienstlich")] public string? ArbeitgeberEmail { get; set; }

        /// <summary>Einwilligung in Fotos für die Öffentlichkeitsarbeit (Art. 7 DSGVO); Abwählen gilt als Widerruf ab heute.</summary>
        [Display(Name = "Einwilligung in Fotos für die Öffentlichkeitsarbeit")] public bool FotoEinwilligung { get; set; }
        [Display(Name = "erteilt am")] public DateOnly? FotoEinwilligungAm { get; set; }

        [Display(Name = "Abteilung")] public Guid? AbteilungId { get; set; }
        [Display(Name = "Eintritt")] public DateOnly? Eintritt { get; set; }

        /// <summary>Immer zwei Felder; leer gelassene werden nicht gespeichert bzw. gelöscht.</summary>
        public List<ElternEingabe> Erziehungsberechtigte { get; set; } = [new(), new()];
    }

    public sealed class ElternEingabe
    {
        [Display(Name = "Verhältnis")] public Sorgeverhaeltnis? Verhaeltnis { get; set; }
        [StringLength(100), Display(Name = "Vorname")] public string? Vorname { get; set; }
        [StringLength(100), Display(Name = "Nachname")] public string? Nachname { get; set; }
        [StringLength(200), Display(Name = "Straße")] public string? Strasse { get; set; }
        [StringLength(10), Display(Name = "PLZ")] public string? Plz { get; set; }
        [StringLength(100), Display(Name = "Ort")] public string? Ort { get; set; }
        [Phone, StringLength(20), Display(Name = "Telefon")] public string? Telefon { get; set; }
        [Phone, StringLength(20), Display(Name = "Mobil")] public string? Mobil { get; set; }
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), StringLength(200), Display(Name = "E-Mail")] public string? Email { get; set; }

        public bool IstLeer => new[] { Vorname, Nachname, Strasse, Plz, Ort, Telefon, Mobil, Email }.All(string.IsNullOrWhiteSpace);
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
                Geschlecht = mitglied.Geschlecht,
                TelefonMobil = mitglied.TelefonMobil,
                TelefonFestnetz = mitglied.TelefonFestnetz,
                Email = mitglied.Email,
                FotoEinwilligung = mitglied.FotoEinwilligung,
                FotoEinwilligungAm = mitglied.FotoEinwilligungAm,
            };
            FotoWiderrufAm = mitglied.FotoWiderrufAm;
            ElternSichtbar = mitglied.AlterAm(Heute) < await ElternBisAlterAsync(ct);
            foreach (var e in ElternSichtbar
                ? await db.Erziehungsberechtigte.Where(e => e.MitgliedId == mitglied.Id && e.Nummer >= 1 && e.Nummer <= 2).ToListAsync(ct)
                : [])
            {
                Formular.Erziehungsberechtigte[e.Nummer - 1] = new ElternEingabe
                {
                    Verhaeltnis = e.Verhaeltnis, Vorname = e.Vorname, Nachname = e.Nachname, Strasse = e.Strasse, Plz = e.Plz, Ort = e.Ort,
                    Telefon = e.Telefon, Mobil = e.Mobil, Email = e.Email,
                };
            }

            if (await DarfArbeitgeberAsync())
            {
                Formular.Beruf = mitglied.Beruf;
                Formular.Arbeitgeber = mitglied.Arbeitgeber;
                Formular.ArbeitgeberStrasse = mitglied.ArbeitgeberStrasse;
                Formular.ArbeitgeberPlz = mitglied.ArbeitgeberPlz;
                Formular.Arbeitsort = mitglied.Arbeitsort;
                Formular.ArbeitgeberTelefon = mitglied.ArbeitgeberTelefon;
                Formular.ArbeitgeberMobil = mitglied.ArbeitgeberMobil;
                Formular.ArbeitgeberEmail = mitglied.ArbeitgeberEmail;
            }
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

        if (IstNeu && Formular.AbteilungId is { } abteilungId && await datenbereich.AnlegeabteilungenAsync(User) is { } erlaubt
            && !erlaubt.Contains(abteilungId))
        {
            ModelState.AddModelError("Formular.AbteilungId", "In dieser Abteilung darfst du keine Mitglieder anlegen.");
        }

        while (Formular.Erziehungsberechtigte.Count < 2)
        {
            Formular.Erziehungsberechtigte.Add(new());
        }

        for (var i = 0; i < 2; i++)
        {
            var e = Formular.Erziehungsberechtigte[i];
            if (!e.IstLeer && (string.IsNullOrWhiteSpace(e.Vorname) || string.IsNullOrWhiteSpace(e.Nachname)))
            {
                ModelState.AddModelError($"Formular.Erziehungsberechtigte[{i}].Nachname", "Vor- und Nachname angeben oder alle Felder leeren.");
            }
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
            // Probezeit nur in der Einsatzabteilung (Satzung § 3 Abs. 2); das Ende lässt sich in der Laufbahn ändern.
            var abteilung = await db.Abteilungen.SingleAsync(a => a.Id == Formular.AbteilungId!.Value, ct);
            var eintritt = Formular.Eintritt ?? Anzeige.Heute(zeit);
            mitglied.Mitgliedschaften.Add(new Mitgliedschaft
            {
                AbteilungId = abteilung.Id,
                Status = abteilung.MitProbezeit ? MitgliedStatus.Probezeit : MitgliedStatus.Aktiv,
                GueltigAb = eintritt,
                ProbezeitBis = abteilung.MitProbezeit ? eintritt.AddMonths((await laufbahn.RegelnAsync(eintritt, ct)).ProbezeitMonate) : null,
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
        mitglied.Geschlecht = Formular.Geschlecht;
        mitglied.TelefonMobil = Formular.TelefonMobil;
        mitglied.TelefonFestnetz = Formular.TelefonFestnetz;
        mitglied.Email = Formular.Email;
        FotoEinwilligungUebernehmen(mitglied, Formular.FotoEinwilligung, Formular.FotoEinwilligungAm, Heute);

        // Ohne Recht sind die Felder nicht im Formular; vorhandene Angaben bleiben dann unverändert.
        if (await DarfArbeitgeberAsync())
        {
            mitglied.Beruf = Leer(Formular.Beruf);
            mitglied.Arbeitgeber = Leer(Formular.Arbeitgeber);
            mitglied.ArbeitgeberStrasse = Leer(Formular.ArbeitgeberStrasse);
            mitglied.ArbeitgeberPlz = Leer(Formular.ArbeitgeberPlz);
            mitglied.Arbeitsort = Leer(Formular.Arbeitsort);
            mitglied.ArbeitgeberTelefon = Leer(Formular.ArbeitgeberTelefon);
            mitglied.ArbeitgeberMobil = Leer(Formular.ArbeitgeberMobil);
            mitglied.ArbeitgeberEmail = Leer(Formular.ArbeitgeberEmail);
        }

        // Bei Volljährigen bleiben die Erziehungsberechtigten unberührt; der Löschlauf entfernt sie.
        ElternSichtbar = mitglied.AlterAm(Heute) < await ElternBisAlterAsync(ct);
        var vorhandeneEltern = IstNeu || !ElternSichtbar ? [] : await db.Erziehungsberechtigte.Where(e => e.MitgliedId == mitglied.Id).ToListAsync(ct);
        for (var i = 0; i < (ElternSichtbar ? 2 : 0); i++)
        {
            var e = Formular.Erziehungsberechtigte[i];
            var alt = vorhandeneEltern.FirstOrDefault(x => x.Nummer == i + 1);
            if (e.IstLeer)
            {
                if (alt is not null)
                {
                    db.Erziehungsberechtigte.Remove(alt);
                }

                continue;
            }

            if (alt is null)
            {
                alt = new Erziehungsberechtigter { Mitglied = mitglied, Nummer = i + 1, Vorname = "", Nachname = "" };
                db.Erziehungsberechtigte.Add(alt);
            }

            alt.Verhaeltnis = e.Verhaeltnis;
            alt.Vorname = e.Vorname!.Trim();
            alt.Nachname = e.Nachname!.Trim();
            alt.Strasse = Leer(e.Strasse);
            alt.Plz = Leer(e.Plz);
            alt.Ort = Leer(e.Ort);
            alt.Telefon = Leer(e.Telefon);
            alt.Mobil = Leer(e.Mobil);
            alt.Email = Leer(e.Email);
        }

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
        return RedirectToPage("Akte", new { id = mitglied.Id });
    }

    /// <summary>
    /// Neue Einwilligung mit Datum (ohne Angabe heute), Abwählen als Widerruf ab heute. Das Datum der Einwilligung bleibt
    /// nach einem Widerruf als Nachweis stehen.
    /// </summary>
    public static void FotoEinwilligungUebernehmen(Mitglied mitglied, bool einwilligung, DateOnly? erteiltAm, DateOnly heute)
    {
        if (einwilligung)
        {
            mitglied.FotoEinwilligungAm = mitglied.FotoEinwilligung ? erteiltAm ?? mitglied.FotoEinwilligungAm : erteiltAm ?? heute;
            mitglied.FotoWiderrufAm = null;
        }
        else if (mitglied.FotoEinwilligung)
        {
            mitglied.FotoWiderrufAm = heute;
        }
    }

    private async Task<bool> DarfArbeitgeberAsync() =>
        (await autorisierung.AuthorizeAsync(User, Berechtigungen.MitgliederArbeitgeber)).Succeeded;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private Task<int> ElternBisAlterAsync(CancellationToken ct) =>
        parameter.ZahlAsync(Fevad.Domain.Gemeinsam.ParameterSchluessel.ErziehungsberechtigteLoeschenAbAlter, Heute, 18, ct);

    private async Task AuswahlLadenAsync(CancellationToken ct)
    {
        DarfArbeitgeber = await DarfArbeitgeberAsync();
        ElternBisAlter = await ElternBisAlterAsync(ct);
        var erlaubt = (await datenbereich.AnlegeabteilungenAsync(User))?.ToList();
        Abteilungen = await db.Abteilungen
            .Where(a => a.Aktiv && (erlaubt == null || erlaubt.Contains(a.Id)))
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
