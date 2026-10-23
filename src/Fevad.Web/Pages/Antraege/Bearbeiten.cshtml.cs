using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Fevad.Web.Pages.Laufbahn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ElternEingabe = Fevad.Web.Pages.Mitglieder.BearbeitenModel.ElternEingabe;

namespace Fevad.Web.Pages.Antraege;

/// <summary>
/// Antrag ausfüllen, drucken und entscheiden. Bei Übertritt und Volljährigkeit sind die Angaben aus dem Mitglied vorbelegt.
/// Abgeschlossene Anträge sind nur noch lesbar; der Löschlauf entfernt sie nach Frist (L32), Entwürfe nach L33.
/// </summary>
[Authorize(Policy = Berechtigungen.AntraegeSehen)]
public class BearbeitenModel(
    AppDbContext db, Antragsbereich bereich, Antragsannahme annahme, Laufbahnabfrage laufbahn, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public Antrag? Antrag { get; private set; }
    public bool IstNeu => Formular.Id is null;
    public bool Gesperrt => Antrag?.Abgeschlossen == true;
    public bool DarfEntscheiden { get; private set; }
    public DateOnly Heute => Anzeige.Heute(zeit);
    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];
    public IReadOnlyList<string> Hinweise { get; private set; } = [];
    public string? PersonalnummerVorschlag { get; private set; }
    public bool MitProbezeit { get; private set; }
    public string? MitgliedName { get; private set; }

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }
        public uint Version { get; set; }
        public AntragArt Art { get; set; }
        public Guid? MitgliedId { get; set; }

        [Required(ErrorMessage = "Abteilung fehlt."), Display(Name = "Abteilung")] public Guid? AbteilungId { get; set; }
        [Display(Name = "Gewünscht ab")] public DateOnly? GewuenschtAb { get; set; }

        [Required(ErrorMessage = "Vorname fehlt."), StringLength(100), Display(Name = "Vorname")] public string Vorname { get; set; } = "";
        [Required(ErrorMessage = "Nachname fehlt."), StringLength(100), Display(Name = "Nachname")] public string Nachname { get; set; } = "";
        [Required(ErrorMessage = "Geburtsdatum fehlt."), Display(Name = "Geburtsdatum")] public DateOnly? Geburtsdatum { get; set; }
        [StringLength(100), Display(Name = "Geburtsort")] public string? Geburtsort { get; set; }
        [Display(Name = "Geschlecht")] public Geschlecht? Geschlecht { get; set; }
        [StringLength(200), Display(Name = "Straße")] public string? Strasse { get; set; }
        [StringLength(10), Display(Name = "PLZ")] public string? Plz { get; set; }
        [StringLength(100), Display(Name = "Ort")] public string? Ort { get; set; }
        [Phone, StringLength(20), Display(Name = "Telefon")] public string? Telefon { get; set; }
        [Phone, StringLength(20), Display(Name = "Mobil")] public string? Mobil { get; set; }
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), StringLength(200), Display(Name = "E-Mail")] public string? Email { get; set; }

        [Display(Name = "War schon Mitglied einer Feuerwehr")] public bool FrueherFeuerwehr { get; set; }
        [StringLength(200), Display(Name = "Feuerwehr")] public string? FrueherFeuerwehrName { get; set; }
        [Display(Name = "von")] public DateOnly? FrueherVon { get; set; }
        [Display(Name = "bis")] public DateOnly? FrueherBis { get; set; }
        [Display(Name = "Grund des Ausscheidens")] public Ausscheidensgrund? FrueherAustrittsgrund { get; set; }
        [StringLength(500), Display(Name = "Lehrgänge und Qualifikationen")] public string? FrueherLehrgaenge { get; set; }
        [StringLength(100), Display(Name = "Letzter Dienstgrad")] public string? FrueherDienstgrad { get; set; }
        [Display(Name = "Derzeit Mitglied einer anderen Feuerwehr")] public bool AndereFeuerwehrAktiv { get; set; }
        [StringLength(200), Display(Name = "Welche Feuerwehr")] public string? AndereFeuerwehrName { get; set; }
        [Display(Name = "Aus einer Feuerwehr ausgeschlossen")] public bool FrueherAusgeschlossen { get; set; }

        /// <summary>Nicht vorbelegt: leer, bis ja oder nein angekreuzt ist.</summary>
        [Display(Name = "Fotos für die Öffentlichkeitsarbeit")] public bool? FotoEinwilligung { get; set; }
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }

        public List<ElternEingabe> Erziehungsberechtigte { get; set; } = [new(), new()];
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, AntragArt? art, Guid? mitglied, CancellationToken ct)
    {
        if (id is { } antragId)
        {
            if (await LadenAsync(antragId, ct) is not { } antrag)
            {
                return NotFound();
            }

            Formular = AusAntrag(antrag);
        }
        else
        {
            if (art is not { } neueArt)
            {
                return RedirectToPage("Index");
            }

            Formular = new Eingabe { Art = neueArt };
            if (Antragsregeln.MitMitglied(neueArt))
            {
                if (mitglied is null || !await AusMitgliedAsync(mitglied.Value, ct))
                {
                    return NotFound();
                }
            }
            else if (!await DarfAufnehmenAsync(neueArt, ct))
            {
                return Forbid();
            }
        }

        await AuswahlLadenAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        while (Formular.Erziehungsberechtigte.Count < 2)
        {
            Formular.Erziehungsberechtigte.Add(new());
        }

        Antrag? antrag = null;
        if (Formular.Id is { } id)
        {
            antrag = await LadenAsync(id, ct);
            if (antrag is null)
            {
                return NotFound();
            }

            if (antrag.Abgeschlossen)
            {
                TempData["Fehler"] = "Der Antrag ist abgeschlossen und kann nicht mehr geändert werden.";
                return RedirectToPage(new { id });
            }

            // Art und Mitglied stehen mit dem Anlegen fest.
            Formular.Art = antrag.Art;
            Formular.MitgliedId = antrag.MitgliedId;
        }
        else if (Antragsregeln.MitMitglied(Formular.Art))
        {
            if (Formular.MitgliedId is not { } mitgliedId || !await MitgliedSichtbarAsync(mitgliedId, ct))
            {
                return NotFound();
            }
        }
        else
        {
            Formular.MitgliedId = null;
            if (!await DarfAufnehmenAsync(Formular.Art, ct))
            {
                return Forbid();
            }
        }

        await PruefenAsync(ct);
        if (!ModelState.IsValid)
        {
            Antrag = antrag;
            await AuswahlLadenAsync(ct);
            return Page();
        }

        if (antrag is null)
        {
            antrag = new Antrag { Art = Formular.Art, Status = AntragStatus.Entwurf, MitgliedId = Formular.MitgliedId, Vorname = "", Nachname = "" };
            db.Antraege.Add(antrag);
        }
        else
        {
            db.Entry(antrag).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        Uebernehmen(antrag);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Fehler"] = "Der Antrag wurde inzwischen von jemand anderem geändert. Bitte die Änderungen erneut eintragen.";
            return RedirectToPage(new { id = antrag.Id });
        }

        var regeln = await laufbahn.RegelnAsync(Heute, ct);
        var hinweise = Antragsregeln.Hinweise(antrag.Art, antrag.Geburtsdatum, antrag.GewuenschtAb ?? Heute, regeln, await VolljaehrigAbAsync(ct));
        TempData["Meldung"] = hinweise.Count == 0 ? "Antrag gespeichert." : $"Antrag gespeichert. Hinweis: {string.Join(" ", hinweise)}";
        return RedirectToPage(new { id = antrag.Id });
    }

    /// <summary>Ausgefüllt als PDF zum Unterschreiben.</summary>
    public async Task<IActionResult> OnGetPdfAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is not { } antrag)
        {
            return NotFound();
        }

        var rahmen = await bereich.RahmenAsync(antrag.Abteilung?.Bezeichnung, Heute, ct);
        return File(AntragPdf.Erzeugen(antrag.Art, antrag, rahmen), "application/pdf", AntragPdf.Dateiname(antrag.Art, antrag));
    }

    public Task<IActionResult> OnPostEingegangenAsync(Guid id, DateOnly? am, CancellationToken ct) =>
        StatusAsync(id, false, antrag =>
        {
            if (antrag.Status != AntragStatus.Entwurf)
            {
                throw new InvalidOperationException("Nur ein Entwurf kann als eingegangen eingetragen werden.");
            }

            antrag.Status = AntragStatus.Eingegangen;
            antrag.EingegangenAm = am ?? Heute;
            return Task.FromResult($"Unterschrieben eingegangen am {Anzeige.Datum(antrag.EingegangenAm)}.");
        }, ct);

    public Task<IActionResult> OnPostAnnehmenAsync(
        Guid id, DateOnly? ab, string? personalnummer, ProbezeitWahl probezeit, DateOnly? probezeitBis, string? probezeitBemerkung, CancellationToken ct) =>
        StatusAsync(id, true, antrag =>
        {
            if (antrag.Status != AntragStatus.Eingegangen)
            {
                throw new InvalidOperationException("Erst annehmen, wenn der unterschriebene Antrag eingegangen ist.");
            }

            return annahme.AnnehmenAsync(antrag,
                new Antragsannahme.Angaben(ab ?? antrag.GewuenschtAb ?? Heute, personalnummer, probezeit, probezeitBis, probezeitBemerkung), Heute, ct);
        }, ct);

    public Task<IActionResult> OnPostAblehnenAsync(Guid id, CancellationToken ct) =>
        StatusAsync(id, true, antrag =>
        {
            if (antrag.Status != AntragStatus.Eingegangen)
            {
                throw new InvalidOperationException("Ablehnen geht nur bei einem eingegangenen Antrag; einen Entwurf löschen.");
            }

            antrag.Status = AntragStatus.Abgelehnt;
            antrag.EntschiedenAm = Heute;
            return Task.FromResult("Antrag abgelehnt.");
        }, ct);

    public Task<IActionResult> OnPostZurueckziehenAsync(Guid id, CancellationToken ct) =>
        StatusAsync(id, false, antrag =>
        {
            if (antrag.Abgeschlossen)
            {
                throw new InvalidOperationException("Der Antrag ist schon abgeschlossen.");
            }

            antrag.Status = AntragStatus.Zurueckgezogen;
            antrag.EntschiedenAm = Heute;
            return Task.FromResult("Antrag als zurückgezogen eingetragen.");
        }, ct);

    /// <summary>Einen Entwurf verwerfen; der Löschlauf entfernt ihn beim nächsten Lauf endgültig (L33).</summary>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is not { } antrag)
        {
            return NotFound();
        }

        if (antrag.Status != AntragStatus.Entwurf)
        {
            TempData["Fehler"] = "Nur Entwürfe können gelöscht werden; einen eingegangenen Antrag als zurückgezogen eintragen.";
            return RedirectToPage(new { id });
        }

        db.Antraege.Remove(antrag);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Entwurf gelöscht.";
        return RedirectToPage("Index");
    }

    private async Task<IActionResult> StatusAsync(Guid id, bool entscheiden, Func<Antrag, Task<string>> aenderung, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is not { } antrag)
        {
            return NotFound();
        }

        if (entscheiden && !DarfEntscheiden)
        {
            return Forbid();
        }

        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        try
        {
            TempData["Meldung"] = await aenderung(antrag);
            await db.SaveChangesAsync(ct);
            await transaktion.CommitAsync(ct);
        }
        catch (InvalidOperationException fehler)
        {
            TempData.Remove("Meldung");
            TempData["Fehler"] = fehler.Message;
        }
        catch (DbUpdateException)
        {
            TempData.Remove("Meldung");
            TempData["Fehler"] = "Speichern nicht möglich. Vermutlich hat jemand anderes gerade etwas geändert; bitte neu laden.";
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Lädt einen sichtbaren Antrag (verfolgt, mit Erziehungsberechtigten) und setzt <see cref="DarfEntscheiden"/>.</summary>
    private async Task<Antrag?> LadenAsync(Guid id, CancellationToken ct)
    {
        var freigabe = await bereich.FreigabeAsync(User);
        Antrag = await Antragsbereich.Sichtbar(db.Antraege, freigabe, Heute)
            .Include(a => a.Erziehungsberechtigte).Include(a => a.Abteilung).Include(a => a.Mitglied)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        DarfEntscheiden = Antrag is not null && freigabe.Umfasst(Antrag.AbteilungId);
        return Antrag;
    }

    private async Task<bool> DarfAufnehmenAsync(AntragArt art, CancellationToken ct)
    {
        var freigabe = await bereich.FreigabeAsync(User);
        var ziel = Antragsregeln.Zielabteilung(art);
        var ids = await db.Abteilungen.Where(a => a.Aktiv && a.Art == ziel).Select(a => a.Id).ToListAsync(ct);
        return ids.Any(freigabe.Umfasst);
    }

    private async Task<bool> MitgliedSichtbarAsync(Guid mitgliedId, CancellationToken ct)
    {
        var freigabe = await bereich.FreigabeAsync(User);
        var ids = freigabe.Abteilungen.ToList();
        var heute = Heute;
        return await db.Mitglieder.AnyAsync(m => m.Id == mitgliedId && (freigabe.Alle || m.Mitgliedschaften.Any(s => !s.Vordienstzeit
            && ids.Contains(s.AbteilungId) && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute))), ct);
    }

    /// <summary>Neuer Antrag zu einem Mitglied: Angaben und Erziehungsberechtigte übernehmen, Zielabteilung vorschlagen.</summary>
    private async Task<bool> AusMitgliedAsync(Guid mitgliedId, CancellationToken ct)
    {
        if (!await MitgliedSichtbarAsync(mitgliedId, ct))
        {
            return false;
        }

        var heute = Heute;
        var m = await db.Mitglieder.AsNoTracking().Include(x => x.Erziehungsberechtigte).Include(x => x.Mitgliedschaften).ThenInclude(s => s.Abteilung)
            .SingleAsync(x => x.Id == mitgliedId, ct);
        Formular.MitgliedId = m.Id;
        Formular.Vorname = m.Vorname;
        Formular.Nachname = m.Nachname;
        Formular.Geburtsdatum = m.Geburtsdatum;
        Formular.Geschlecht = m.Geschlecht;
        Formular.Strasse = m.Strasse;
        Formular.Plz = m.Plz;
        Formular.Ort = m.Ort;
        Formular.Telefon = m.TelefonFestnetz;
        Formular.Mobil = m.TelefonMobil;
        Formular.Email = m.Email;
        foreach (var e in m.Erziehungsberechtigte.Where(e => e.Nummer is 1 or 2))
        {
            Formular.Erziehungsberechtigte[e.Nummer - 1] = new ElternEingabe
            {
                Verhaeltnis = e.Verhaeltnis, Vorname = e.Vorname, Nachname = e.Nachname, Strasse = e.Strasse, Plz = e.Plz, Ort = e.Ort,
                Telefon = e.Telefon, Mobil = e.Mobil, Email = e.Email,
            };
        }

        var einsatz = m.Mitgliedschaften.FirstOrDefault(s => !s.Vordienstzeit && s.IstAktivAm(heute) && s.Abteilung!.Art == AbteilungArt.Einsatz);
        Formular.AbteilungId = einsatz?.AbteilungId;
        if (Formular.Art == AntragArt.UebertrittEinsatz)
        {
            var regeln = await laufbahn.RegelnAsync(heute, ct);
            var moeglich = Domain.Laufbahn.Laufbahnhinweise.Geburtstag(m.Geburtsdatum, regeln.AufnahmeEinsatzAbAlter);
            Formular.GewuenschtAb = moeglich > heute ? moeglich : null;
        }
        else if (Formular.Art == AntragArt.Volljaehrigkeit)
        {
            var volljaehrig = Domain.Laufbahn.Laufbahnhinweise.Geburtstag(m.Geburtsdatum, await VolljaehrigAbAsync(ct));
            Formular.GewuenschtAb = volljaehrig > heute ? volljaehrig : null;
        }

        return true;
    }

    private static Eingabe AusAntrag(Antrag a)
    {
        var formular = new Eingabe
        {
            Id = a.Id, Art = a.Art, MitgliedId = a.MitgliedId, AbteilungId = a.AbteilungId, GewuenschtAb = a.GewuenschtAb,
            Vorname = a.Vorname, Nachname = a.Nachname, Geburtsdatum = a.Geburtsdatum, Geburtsort = a.Geburtsort, Geschlecht = a.Geschlecht,
            Strasse = a.Strasse, Plz = a.Plz, Ort = a.Ort, Telefon = a.Telefon, Mobil = a.Mobil, Email = a.Email,
            FrueherFeuerwehr = a.FrueherFeuerwehr, FrueherFeuerwehrName = a.FrueherFeuerwehrName, FrueherVon = a.FrueherVon, FrueherBis = a.FrueherBis,
            FrueherAustrittsgrund = a.FrueherAustrittsgrund, FrueherLehrgaenge = a.FrueherLehrgaenge, FrueherDienstgrad = a.FrueherDienstgrad,
            AndereFeuerwehrAktiv = a.AndereFeuerwehrAktiv, AndereFeuerwehrName = a.AndereFeuerwehrName, FrueherAusgeschlossen = a.FrueherAusgeschlossen,
            FotoEinwilligung = a.FotoEinwilligung, Bemerkung = a.Bemerkung,
        };
        foreach (var e in a.Erziehungsberechtigte.Where(e => e.Nummer is 1 or 2))
        {
            formular.Erziehungsberechtigte[e.Nummer - 1] = new ElternEingabe
            {
                Verhaeltnis = e.Verhaeltnis, Vorname = e.Vorname, Nachname = e.Nachname, Strasse = e.Strasse, Plz = e.Plz, Ort = e.Ort,
                Telefon = e.Telefon, Mobil = e.Mobil, Email = e.Email,
            };
        }

        return formular;
    }

    private async Task PruefenAsync(CancellationToken ct)
    {
        for (var i = 0; i < 2; i++)
        {
            var e = Formular.Erziehungsberechtigte[i];
            if (!e.IstLeer && (string.IsNullOrWhiteSpace(e.Vorname) || string.IsNullOrWhiteSpace(e.Nachname)))
            {
                ModelState.AddModelError($"Formular.Erziehungsberechtigte[{i}].Nachname", "Vor- und Nachname angeben oder alle Felder leeren.");
            }
        }

        if (Formular.AbteilungId is { } abteilungId
            && !await db.Abteilungen.AnyAsync(a => a.Id == abteilungId && a.Aktiv && a.Art == Antragsregeln.Zielabteilung(Formular.Art), ct))
        {
            ModelState.AddModelError("Formular.AbteilungId", "Diese Abteilung passt nicht zum Antrag.");
        }
        else if (Formular.AbteilungId is { } ziel && !Antragsregeln.MitMitglied(Formular.Art)
            && !(await bereich.FreigabeAsync(User)).Umfasst(ziel))
        {
            ModelState.AddModelError("Formular.AbteilungId", "Für diese Abteilung darfst du keine Anträge bearbeiten.");
        }

        if (Formular.FrueherVon > Formular.FrueherBis)
        {
            ModelState.AddModelError("Formular.FrueherBis", "Das Ende liegt vor dem Beginn.");
        }
    }

    private void Uebernehmen(Antrag antrag)
    {
        var f = Formular;
        var vorgeschichte = f.Art == AntragArt.EinsatzEintritt;
        antrag.AbteilungId = f.AbteilungId!.Value;
        antrag.GewuenschtAb = f.GewuenschtAb;
        antrag.Vorname = f.Vorname.Trim();
        antrag.Nachname = f.Nachname.Trim();
        antrag.Geburtsdatum = f.Geburtsdatum!.Value;
        antrag.Geburtsort = Leer(f.Geburtsort);
        antrag.Geschlecht = f.Geschlecht;
        antrag.Strasse = Leer(f.Strasse);
        antrag.Plz = Leer(f.Plz);
        antrag.Ort = Leer(f.Ort);
        antrag.Telefon = Leer(f.Telefon);
        antrag.Mobil = Leer(f.Mobil);
        antrag.Email = Leer(f.Email);

        // Die Vorgeschichte gehört nur zur Aufnahme in die Einsatzabteilung; sonst nichts davon speichern.
        antrag.FrueherFeuerwehr = vorgeschichte && f.FrueherFeuerwehr;
        antrag.FrueherFeuerwehrName = antrag.FrueherFeuerwehr ? Leer(f.FrueherFeuerwehrName) : null;
        antrag.FrueherVon = antrag.FrueherFeuerwehr ? f.FrueherVon : null;
        antrag.FrueherBis = antrag.FrueherFeuerwehr ? f.FrueherBis : null;
        antrag.FrueherAustrittsgrund = antrag.FrueherFeuerwehr ? f.FrueherAustrittsgrund : null;
        antrag.FrueherLehrgaenge = antrag.FrueherFeuerwehr ? Leer(f.FrueherLehrgaenge) : null;
        antrag.FrueherDienstgrad = antrag.FrueherFeuerwehr ? Leer(f.FrueherDienstgrad) : null;
        antrag.AndereFeuerwehrAktiv = vorgeschichte && f.AndereFeuerwehrAktiv;
        antrag.AndereFeuerwehrName = antrag.AndereFeuerwehrAktiv ? Leer(f.AndereFeuerwehrName) : null;
        antrag.FrueherAusgeschlossen = vorgeschichte && f.FrueherAusgeschlossen;

        antrag.FotoEinwilligung = f.FotoEinwilligung;
        antrag.Bemerkung = Leer(f.Bemerkung);

        for (var i = 0; i < 2; i++)
        {
            var e = f.Erziehungsberechtigte[i];
            var alt = antrag.Erziehungsberechtigte.FirstOrDefault(x => x.Nummer == i + 1);
            if (e.IstLeer || f.Art == AntragArt.Volljaehrigkeit)
            {
                if (alt is not null)
                {
                    db.AntragElternteile.Remove(alt);
                }

                continue;
            }

            if (alt is null)
            {
                alt = new AntragElternteil { Nummer = i + 1, Vorname = "", Nachname = "" };
                antrag.Erziehungsberechtigte.Add(alt);
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
    }

    private async Task AuswahlLadenAsync(CancellationToken ct)
    {
        var ziel = Antragsregeln.Zielabteilung(Formular.Art);
        var freigabe = await bereich.FreigabeAsync(User);
        var mitMitglied = Antragsregeln.MitMitglied(Formular.Art);
        Abteilungen = (await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv && a.Art == ziel).OrderBy(a => a.Sortierung)
                .Select(a => new { a.Id, a.Bezeichnung }).ToListAsync(ct))
            .Where(a => mitMitglied || freigabe.Umfasst(a.Id))
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString()))
            .ToList();
        Formular.AbteilungId ??= Abteilungen.Count == 1 ? Guid.Parse(Abteilungen[0].Value) : null;
        MitProbezeit = ziel == AbteilungArt.Einsatz;
        if (Formular.MitgliedId is { } mitgliedId)
        {
            MitgliedName = await db.Mitglieder.Where(m => m.Id == mitgliedId).Select(m => m.Nachname + ", " + m.Vorname).FirstOrDefaultAsync(ct);
        }

        if (Antrag is { Abgeschlossen: false } && Formular.Geburtsdatum is { } geburtsdatum)
        {
            Hinweise = Antragsregeln.Hinweise(Formular.Art, geburtsdatum, Formular.GewuenschtAb ?? Heute, await laufbahn.RegelnAsync(Heute, ct),
                await VolljaehrigAbAsync(ct));
            if (Antrag is { MitgliedId: null, Status: AntragStatus.Eingegangen } && DarfEntscheiden)
            {
                PersonalnummerVorschlag = await annahme.PersonalnummerVorschlagAsync(ct);
            }
        }
    }

    private Task<int> VolljaehrigAbAsync(CancellationToken ct) => parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, Heute, 18, ct);

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
