using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    UserManager<Benutzer> konten, Kontolinks links, TimeProvider zeit) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Es wird ein neues Mitglied angelegt.</summary>
    public bool IstNeu => Formular.Id is null;
    /// <summary>Auswahl der Abteilungen für den Eintritt.</summary>
    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];
    /// <summary>Mitgliedschaften des Mitglieds, jüngste zuerst.</summary>
    public IReadOnlyList<Mitgliedschaft> Verlauf { get; private set; } = [];
    /// <summary>Darf Beruf und Arbeitgeber pflegen.</summary>
    public bool DarfArbeitgeber { get; private set; }
    /// <summary>Heutiges Datum in Ortszeit.</summary>
    public DateOnly Heute => Anzeige.Heute(zeit);

    /// <summary>Alter, ab dem die Erziehungsberechtigten gelöscht werden (Löschkonzept L15).</summary>
    public int ElternBisAlter { get; private set; } = 18;

    /// <summary>Erziehungsberechtigte nur bei neuen und minderjährigen Mitgliedern; bei Volljährigen bleiben sie verborgen und unverändert.</summary>
    public bool ElternSichtbar { get; private set; } = true;

    /// <summary>Formular der Stammdaten.</summary>
    public sealed class Eingabe
    {
        /// <summary>Stand der Laufbahn; null ohne Recht Laufbahn.Lesen.</summary>
        public Guid? Id { get; set; }

        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }

        /// <summary>Personalnummer.</summary>
        [Required(ErrorMessage = "Personalnummer fehlt."), StringLength(20), Display(Name = "Personalnummer")]
        public string Personalnummer { get; set; } = "";

        /// <summary>Vorname.</summary>
        [Required(ErrorMessage = "Vorname fehlt."), StringLength(100), Display(Name = "Vorname")]
        public string Vorname { get; set; } = "";

        /// <summary>Nachname.</summary>
        [Required(ErrorMessage = "Nachname fehlt."), StringLength(100), Display(Name = "Nachname")]
        public string Nachname { get; set; } = "";

        /// <summary>Geburtsdatum.</summary>
        [Required(ErrorMessage = "Geburtsdatum fehlt."), Display(Name = "Geburtsdatum")]
        public DateOnly? Geburtsdatum { get; set; }

        /// <summary>Geschlecht.</summary>
        [Display(Name = "Geschlecht")] public Geschlecht? Geschlecht { get; set; }

        /// <summary>Straße und Hausnummer.</summary>
        [Display(Name = "Straße")] public string? Strasse { get; set; }
        /// <summary>Postleitzahl.</summary>
        [Display(Name = "PLZ")] public string? Plz { get; set; }
        /// <summary>Wohnort.</summary>
        [Display(Name = "Ort")] public string? Ort { get; set; }

        /// <summary>Mobilnummer.</summary>
        [Phone, StringLength(20), Display(Name = "Mobil (+49...)")] public string? TelefonMobil { get; set; }
        /// <summary>Private Festnetznummer.</summary>
        [Phone, StringLength(20), Display(Name = "Telefon privat")] public string? TelefonFestnetz { get; set; }
        /// <summary>E-Mail-Adresse.</summary>
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), StringLength(200), Display(Name = "E-Mail")] public string? Email { get; set; }

        /// <summary>Person, die im Notfall verständigt wird.</summary>
        [StringLength(100), Display(Name = "Notfallkontakt")] public string? NotfallkontaktName { get; set; }
        /// <summary>Telefonnummer des Notfallkontakts.</summary>
        [Phone, StringLength(20), Display(Name = "Telefon Notfallkontakt")] public string? NotfallkontaktTelefon { get; set; }

        /// <summary>Nummer des DJF-Mitgliedsausweises (Jugendfeuerwehr, Leistungsspange).</summary>
        [StringLength(30), Display(Name = "DJF-Ausweisnummer")] public string? DjfAusweisnummer { get; set; }
        /// <summary>Ausgabedatum des DJF-Ausweises.</summary>
        [Display(Name = "DJF-Ausweis ausgegeben am")] public DateOnly? DjfAusweisAm { get; set; }

        /// <summary>Beruf.</summary>
        [StringLength(100), Display(Name = "Beruf")] public string? Beruf { get; set; }
        /// <summary>Arbeitgeber.</summary>
        [StringLength(200), Display(Name = "Arbeitgeber")] public string? Arbeitgeber { get; set; }
        /// <summary>Straße des Arbeitgebers.</summary>
        [StringLength(200), Display(Name = "Straße")] public string? ArbeitgeberStrasse { get; set; }
        /// <summary>Postleitzahl des Arbeitgebers.</summary>
        [StringLength(10), Display(Name = "PLZ")] public string? ArbeitgeberPlz { get; set; }
        /// <summary>Arbeitsort.</summary>
        [StringLength(100), Display(Name = "Ort")] public string? Arbeitsort { get; set; }
        /// <summary>Dienstliche Telefonnummer.</summary>
        [Phone, StringLength(20), Display(Name = "Telefon dienstlich")] public string? ArbeitgeberTelefon { get; set; }
        /// <summary>Dienstliche Mobilnummer.</summary>
        [Phone, StringLength(20), Display(Name = "Mobil dienstlich")] public string? ArbeitgeberMobil { get; set; }
        /// <summary>Dienstliche E-Mail-Adresse.</summary>
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), StringLength(200), Display(Name = "E-Mail dienstlich")] public string? ArbeitgeberEmail { get; set; }


        /// <summary>Abteilung beim Eintritt eines neuen Mitglieds.</summary>
        [Display(Name = "Abteilung")] public Guid? AbteilungId { get; set; }
        /// <summary>Tag des Eintritts.</summary>
        [Display(Name = "Eintritt")] public DateOnly? Eintritt { get; set; }

        /// <summary>Immer zwei Felder; leer gelassene werden nicht gespeichert bzw. gelöscht.</summary>
        public List<ElternEingabe> Erziehungsberechtigte { get; set; } = [new(), new()];
    }

    /// <summary>Eingabe eines Erziehungsberechtigten.</summary>
    public sealed class ElternEingabe
    {
        /// <summary>Sorgeverhältnis.</summary>
        [Display(Name = "Verhältnis")] public Sorgeverhaeltnis? Verhaeltnis { get; set; }
        /// <summary>Vorname.</summary>
        [StringLength(100), Display(Name = "Vorname")] public string? Vorname { get; set; }
        /// <summary>Nachname.</summary>
        [StringLength(100), Display(Name = "Nachname")] public string? Nachname { get; set; }
        /// <summary>Straße und Hausnummer.</summary>
        [StringLength(200), Display(Name = "Straße")] public string? Strasse { get; set; }
        /// <summary>Postleitzahl.</summary>
        [StringLength(10), Display(Name = "PLZ")] public string? Plz { get; set; }
        /// <summary>Wohnort.</summary>
        [StringLength(100), Display(Name = "Ort")] public string? Ort { get; set; }
        /// <summary>Telefon.</summary>
        [Phone, StringLength(20), Display(Name = "Telefon")] public string? Telefon { get; set; }
        /// <summary>Mobilnummer.</summary>
        [Phone, StringLength(20), Display(Name = "Mobil")] public string? Mobil { get; set; }
        /// <summary>E-Mail-Adresse.</summary>
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), StringLength(200), Display(Name = "E-Mail")] public string? Email { get; set; }

        /// <summary>Elternkonto: eigenes Benutzerkonto, das die Termine des Kindes sieht und dafür zu- oder absagt.</summary>
        [Display(Name = "Elternkonto")] public bool Elternkonto { get; set; }
        /// <summary>Die Datenschutzinformation für Eltern wurde ausgehändigt.</summary>
        [Display(Name = "Datenschutzinformation für Eltern ausgehändigt")] public bool DatenschutzinformationErhalten { get; set; }
        /// <summary>Für diesen Erziehungsberechtigten wird kein Elternkonto angelegt.</summary>
        [Display(Name = "Sperre: kein Elternkonto")] public bool KeineKontozuordnung { get; set; }

        /// <summary>Nur zur Anzeige: das zugeordnete Konto.</summary>
        public string? KontoEmail { get; set; }
        /// <summary>Nur zur Anzeige: seit wann das Konto zugeordnet ist.</summary>
        public DateTimeOffset? KontoSeit { get; set; }

        /// <summary>Alle Namens- und Kontaktfelder sind leer.</summary>
        public bool IstLeer => new[] { Vorname, Nachname, Strasse, Plz, Ort, Telefon, Mobil, Email }.All(string.IsNullOrWhiteSpace);
    }

    /// <summary>Zeigt ein Mitglied oder ein leeres Formular.</summary>
    /// <param name="id">Kennung des Mitglieds; leer für ein neues.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
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
                NotfallkontaktName = mitglied.NotfallkontaktName,
                NotfallkontaktTelefon = mitglied.NotfallkontaktTelefon,
                DjfAusweisnummer = mitglied.DjfAusweisnummer,
                DjfAusweisAm = mitglied.DjfAusweisAm,
                Email = mitglied.Email,
            };
            ElternSichtbar = mitglied.AlterAm(Heute) < await ElternBisAlterAsync(ct);
            foreach (var e in ElternSichtbar
                ? await db.Erziehungsberechtigte.Where(e => e.MitgliedId == mitglied.Id && e.Nummer >= 1 && e.Nummer <= 2).ToListAsync(ct)
                : [])
            {
                Formular.Erziehungsberechtigte[e.Nummer - 1] = new ElternEingabe
                {
                    Verhaeltnis = e.Verhaeltnis, Vorname = e.Vorname, Nachname = e.Nachname, Strasse = e.Strasse, Plz = e.Plz, Ort = e.Ort,
                    Telefon = e.Telefon, Mobil = e.Mobil, Email = e.Email,
                    Elternkonto = e.KontoId is not null, DatenschutzinformationErhalten = e.DatenschutzinformationAm is not null,
                    KeineKontozuordnung = e.KeineKontozuordnung, KontoSeit = e.KontoZugeordnetAm,
                    KontoEmail = e.KontoId is { } kontoId ? await db.Users.Where(u => u.Id == kontoId).Select(u => u.Email).FirstOrDefaultAsync(ct) : null,
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

    /// <summary>Speichert die Stammdaten, Erziehungsberechtigten und Elternkonten.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Akte, bei Fehlern die Seite, sonst 403/404.</returns>
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
        // Abgelöste Anschriften bleiben bis zur Löschfrist (L34) in der Akte.
        if (!IstNeu && FruehereAnschrift.Privat(mitglied, Formular.Strasse, Formular.Plz, Formular.Ort, Heute) is { } privat)
        {
            db.FruehereAnschriften.Add(privat);
        }

        mitglied.Strasse = Formular.Strasse;
        mitglied.Plz = Formular.Plz;
        mitglied.Ort = Formular.Ort;
        mitglied.Geschlecht = Formular.Geschlecht;
        mitglied.TelefonMobil = Formular.TelefonMobil;
        mitglied.TelefonFestnetz = Formular.TelefonFestnetz;
        mitglied.Email = Formular.Email;
        mitglied.NotfallkontaktName = Leer(Formular.NotfallkontaktName);
        mitglied.NotfallkontaktTelefon = Leer(Formular.NotfallkontaktTelefon);
        mitglied.DjfAusweisnummer = Leer(Formular.DjfAusweisnummer);
        mitglied.DjfAusweisAm = Formular.DjfAusweisAm;

        // Ohne Recht sind die Felder nicht im Formular; vorhandene Angaben bleiben dann unverändert.
        if (await DarfArbeitgeberAsync())
        {
            if (!IstNeu && FruehereAnschrift.Beruflich(mitglied, Leer(Formular.Arbeitgeber), Leer(Formular.ArbeitgeberStrasse),
                    Leer(Formular.ArbeitgeberPlz), Leer(Formular.Arbeitsort), Heute) is { } beruflich)
            {
                db.FruehereAnschriften.Add(beruflich);
            }

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
        var elternkontenNeu = new List<Erziehungsberechtigter>();
        var elternkontoVerwaist = false;
        for (var i = 0; i < (ElternSichtbar ? 2 : 0); i++)
        {
            var e = Formular.Erziehungsberechtigte[i];
            var alt = vorhandeneEltern.FirstOrDefault(x => x.Nummer == i + 1);
            if (e.IstLeer)
            {
                if (alt is not null)
                {
                    elternkontoVerwaist |= alt.KontoId is not null;
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

            // Elternkonto: nur mit E-Mail-Adresse und ausgehändigter Datenschutzinformation, nie bei gesetzter Sperre.
            alt.KeineKontozuordnung = e.KeineKontozuordnung;
            if (e.DatenschutzinformationErhalten)
            {
                alt.DatenschutzinformationAm ??= Heute;
            }

            if (e.Elternkonto && !e.KeineKontozuordnung)
            {
                if (alt.Email is null)
                {
                    ModelState.AddModelError($"Formular.Erziehungsberechtigte[{i}].Email", "Für ein Elternkonto braucht es die E-Mail-Adresse.");
                }
                else if (alt.KontoId is null && alt.DatenschutzinformationAm is null)
                {
                    ModelState.AddModelError($"Formular.Erziehungsberechtigte[{i}].DatenschutzinformationErhalten", "Bitte bestätigen, dass die Datenschutzinformation für Eltern ausgehändigt wurde.");
                }
                else if (alt.KontoId is null)
                {
                    elternkontenNeu.Add(alt);
                }
            }
            else if (alt.KontoId is not null)
            {
                alt.KontoId = null;
                alt.KontoZugeordnetAm = null;
                elternkontoVerwaist = true;
            }
        }

        if (!ModelState.IsValid)
        {
            await AuswahlLadenAsync(ct);
            return Page();
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

        var meldungen = new List<string> { $"{mitglied.Anzeigename} gespeichert." };
        foreach (var eltern in elternkontenNeu)
        {
            meldungen.Add(await ElternkontoZuordnenAsync(eltern, ct));
        }

        if (elternkontenNeu.Count > 0 || elternkontoVerwaist)
        {
            await db.SaveChangesAsync(ct);
        }

        if (elternkontoVerwaist)
        {
            // Konten ohne zugeordnetes Kind werden deaktiviert; der Löschlauf anonymisiert sie später (L18).
            await Elternkonten.VerwaisteDeaktivierenAsync(db, zeit.GetUtcNow(), ct);
        }

        TempData["Meldung"] = string.Join(" ", meldungen);
        return RedirectToPage("Akte", new { id = mitglied.Id });
    }

    /// <summary>
    /// Legt das Elternkonto an (Rolle Erziehungsberechtigt) oder ordnet ein bestehendes Konto mit dieser E-Mail-Adresse zu, z. B. das
    /// eines Mitglieds oder das Elternkonto für ein Geschwisterkind. Ein neues Konto bekommt den Einrichtungslink per E-Mail oder zur Anzeige.
    /// </summary>
    private async Task<string> ElternkontoZuordnenAsync(Erziehungsberechtigter eltern, CancellationToken ct)
    {
        var email = eltern.Email!;
        var konto = await konten.FindByEmailAsync(email);
        var neu = konto is null;
        if (konto is null)
        {
            konto = new Benutzer { UserName = email, Email = email };
            var ergebnis = await konten.CreateAsync(konto);
            if (!ergebnis.Succeeded)
            {
                return $"Elternkonto {email} nicht angelegt: {string.Join(", ", ergebnis.Errors.Select(f => f.Description))}";
            }
        }
        else if (konto.IstAnonymisiert)
        {
            return $"Elternkonto {email} nicht möglich: Das Konto wurde schon gelöscht.";
        }
        else if (konto.IstDeaktiviert && konto.MitgliedId is null && await konten.IsInRoleAsync(konto, Rollen.Erziehungsberechtigt))
        {
            konto.DeaktiviertAm = null;
            await konten.UpdateAsync(konto);
        }

        if (!await konten.IsInRoleAsync(konto, Rollen.Erziehungsberechtigt))
        {
            await konten.AddToRoleAsync(konto, Rollen.Erziehungsberechtigt);
        }

        eltern.KontoId = konto.Id;
        eltern.KontoZugeordnetAm = zeit.GetUtcNow();
        if (!neu)
        {
            return $"Elternkonto {email} zugeordnet.";
        }

        var link = await links.PasswortLinkAsync(HttpContext, konto);
        if (links.MailAktiv)
        {
            await links.EinrichtungSendenAsync(konto, link, ct);
            return $"Elternkonto {email} angelegt, Einrichtungslink per E-Mail verschickt.";
        }

        TempData["Einrichtungslink"] = link.Adresse;
        TempData["EinrichtungslinkBis"] = Anzeige.DatumZeit(link.GueltigBis);
        return $"Elternkonto {email} angelegt.";
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
