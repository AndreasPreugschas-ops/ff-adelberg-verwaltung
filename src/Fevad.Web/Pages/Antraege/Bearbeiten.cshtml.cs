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
/// Antrag ausfüllen, drucken und entscheiden. Anträge zu einem Mitglied (Übertritt, Volljährigkeit, Altersabteilung, Ausscheiden) sind
/// aus dem Mitglied vorbelegt. Altersgrenzen und der Vorlauf (Parameter <c>Antraege.Vorlauf.Monate</c>) verhindern das Speichern.
/// Abgeschlossene Anträge sind nur noch lesbar; der Löschlauf entfernt sie nach Frist (L32), Entwürfe nach L33.
/// </summary>
[Authorize(Policy = Berechtigungen.AntraegeSehen)]
public class BearbeitenModel(
    AppDbContext db, Antragsbereich bereich, Antragsannahme annahme, Laufbahnabfrage laufbahn, ParameterLeser parameter,
    IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Formulardaten des Antrags.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Der bearbeitete Antrag; <c>null</c> beim Anlegen.</summary>
    public Antrag? Antrag { get; private set; }
    /// <summary>Die Seite legt einen neuen Antrag an.</summary>
    public bool IstNeu => Formular.Id is null;
    /// <summary>Der Antrag ist abgeschlossen und nur noch lesbar.</summary>
    public bool Gesperrt => Antrag?.Abgeschlossen == true;
    /// <summary>Der Benutzer darf über den Antrag entscheiden.</summary>
    public bool DarfEntscheiden { get; private set; }
    /// <summary>Heutiges Datum in Ortszeit.</summary>
    public DateOnly Heute => Anzeige.Heute(zeit);
    /// <summary>Abteilungen zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];
    /// <summary>Hinweise zu Altersgrenzen und Vorlauf.</summary>
    public IReadOnlyList<string> Hinweise { get; private set; } = [];
    /// <summary>Vorgeschlagene nächste Personalnummer für die Annahme.</summary>
    public string? PersonalnummerVorschlag { get; private set; }
    /// <summary>Die Annahme beginnt mit einer Probezeit.</summary>
    public bool MitProbezeit { get; private set; }
    /// <summary>Name des Mitglieds, auf das sich der Antrag bezieht.</summary>
    public string? MitgliedName { get; private set; }

    /// <summary>Wiedereintritt: Name und Personalnummer des früheren Mitglieds.</summary>
    public string? WiedereintrittName { get; private set; }

    /// <summary>Beim neuen Aufnahmeantrag: frühere Mitglieder, die wieder eintreten könnten.</summary>
    public IReadOnlyList<SelectListItem> Ehemalige { get; private set; } = [];

    /// <summary>Beim neuen Antrag auf Aufnahme in die Einsatzabteilung: Jugendliche, für die stattdessen der Übertritt geht.</summary>
    public IReadOnlyList<SelectListItem> Jugendliche { get; private set; } = [];

    /// <summary>Beim Ausscheiden: Gründe zur gewählten Abteilung und ob die Altersabteilung in Frage kommt.</summary>
    public IReadOnlyList<SelectListItem> Gruende { get; private set; } = [];
    /// <summary>Beim Ausscheiden aus der Einsatzabteilung kann die Altersabteilung gewählt werden.</summary>
    public bool AltersabteilungWaehlbar { get; private set; }
    /// <summary>Mindestalter für die Altersabteilung.</summary>
    public int AltersabteilungAbAlter { get; private set; }

    /// <summary>Eingabefelder eines Antrags.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung des Antrags; <c>null</c> beim Anlegen.</summary>
        public Guid? Id { get; set; }
        /// <summary>Zeilenversion zur Erkennung gleichzeitiger Änderungen.</summary>
        public uint Version { get; set; }
        /// <summary>Art des Antrags.</summary>
        public AntragArt Art { get; set; }
        /// <summary>Mitglied, auf das sich der Antrag bezieht (nicht bei Aufnahme).</summary>
        public Guid? MitgliedId { get; set; }

        /// <summary>Aufnahme eines früheren Mitglieds; steht mit dem Anlegen fest.</summary>
        public Guid? WiedereintrittVonId { get; set; }

        /// <summary>Beantragte Abteilung.</summary>
        [Required(ErrorMessage = "Abteilung fehlt."), Display(Name = "Abteilung")] public Guid? AbteilungId { get; set; }
        /// <summary>Gewünschter Beginn bzw. gewünschtes Ende.</summary>
        [Display(Name = "Gewünscht ab")] public DateOnly? GewuenschtAb { get; set; }
        /// <summary>Grund beim Ausscheiden.</summary>
        [Display(Name = "Grund")] public Entlassungsgrund? Entlassungsgrund { get; set; }
        /// <summary>Beim Ausscheiden in die Altersabteilung übernehmen.</summary>
        [Display(Name = "In die Altersabteilung übernehmen")] public bool InAltersabteilung { get; set; }

        /// <summary>Vorname.</summary>
        [Required(ErrorMessage = "Vorname fehlt."), StringLength(100), Display(Name = "Vorname")] public string Vorname { get; set; } = "";
        /// <summary>Nachname.</summary>
        [Required(ErrorMessage = "Nachname fehlt."), StringLength(100), Display(Name = "Nachname")] public string Nachname { get; set; } = "";
        /// <summary>Geburtsdatum.</summary>
        [Required(ErrorMessage = "Geburtsdatum fehlt."), Display(Name = "Geburtsdatum")] public DateOnly? Geburtsdatum { get; set; }
        /// <summary>Geburtsort.</summary>
        [StringLength(100), Display(Name = "Geburtsort")] public string? Geburtsort { get; set; }
        /// <summary>Geschlecht.</summary>
        [Display(Name = "Geschlecht")] public Geschlecht? Geschlecht { get; set; }
        /// <summary>Straße und Hausnummer.</summary>
        [StringLength(200), Display(Name = "Straße")] public string? Strasse { get; set; }
        /// <summary>Postleitzahl.</summary>
        [StringLength(10), Display(Name = "PLZ")] public string? Plz { get; set; }
        /// <summary>Ort.</summary>
        [StringLength(100), Display(Name = "Ort")] public string? Ort { get; set; }
        /// <summary>Telefon.</summary>
        [Phone, StringLength(20), Display(Name = "Telefon")] public string? Telefon { get; set; }
        /// <summary>Mobiltelefon.</summary>
        [Phone, StringLength(20), Display(Name = "Mobil")] public string? Mobil { get; set; }
        /// <summary>E-Mail-Adresse.</summary>
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), StringLength(200), Display(Name = "E-Mail")] public string? Email { get; set; }

        /// <summary>War schon Mitglied einer Feuerwehr.</summary>
        [Display(Name = "War schon Mitglied einer Feuerwehr")] public bool FrueherFeuerwehr { get; set; }
        /// <summary>Name der früheren Feuerwehr.</summary>
        [StringLength(200), Display(Name = "Feuerwehr")] public string? FrueherFeuerwehrName { get; set; }
        /// <summary>Beginn der früheren Mitgliedschaft.</summary>
        [Display(Name = "von")] public DateOnly? FrueherVon { get; set; }
        /// <summary>Ende der früheren Mitgliedschaft.</summary>
        [Display(Name = "bis")] public DateOnly? FrueherBis { get; set; }
        /// <summary>Grund des Ausscheidens aus der früheren Feuerwehr.</summary>
        [Display(Name = "Grund des Ausscheidens")] public Ausscheidensgrund? FrueherAustrittsgrund { get; set; }
        /// <summary>Lehrgänge und Qualifikationen aus der früheren Feuerwehr.</summary>
        [StringLength(500), Display(Name = "Lehrgänge und Qualifikationen")] public string? FrueherLehrgaenge { get; set; }
        /// <summary>Letzter Dienstgrad in der früheren Feuerwehr.</summary>
        [StringLength(100), Display(Name = "Letzter Dienstgrad")] public string? FrueherDienstgrad { get; set; }
        /// <summary>Ist derzeit Mitglied einer anderen Feuerwehr.</summary>
        [Display(Name = "Derzeit Mitglied einer anderen Feuerwehr")] public bool AndereFeuerwehrAktiv { get; set; }
        /// <summary>Name der anderen Feuerwehr.</summary>
        [StringLength(200), Display(Name = "Welche Feuerwehr")] public string? AndereFeuerwehrName { get; set; }
        /// <summary>Wurde aus einer Feuerwehr ausgeschlossen.</summary>
        [Display(Name = "Aus einer Feuerwehr ausgeschlossen")] public bool FrueherAusgeschlossen { get; set; }

        /// <summary>Nicht vorbelegt: leer, bis ja oder nein angekreuzt ist.</summary>
        [Display(Name = "Fotos für die Öffentlichkeitsarbeit")] public bool? FotoEinwilligung { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }

        /// <summary>Erziehungsberechtigte bei Minderjährigen; zwei Zeilen.</summary>
        public List<ElternEingabe> Erziehungsberechtigte { get; set; } = [new(), new()];
    }

    /// <summary>Zeigt einen Antrag oder ein neues, je nach Art vorbelegtes Formular.</summary>
    /// <param name="id">Kennung des Antrags; <c>null</c> legt neu an.</param>
    /// <param name="art">Art des neuen Antrags.</param>
    /// <param name="mitglied">Mitglied, aus dem der neue Antrag vorbelegt wird.</param>
    /// <param name="wiedereintritt">Früheres Mitglied bei einem Wiedereintritt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, Weiterleitung zur Liste ohne Art, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, AntragArt? art, Guid? mitglied, Guid? wiedereintritt, CancellationToken ct)
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
            else if (wiedereintritt is { } frueher && !await AusEhemaligemAsync(frueher, ct))
            {
                return NotFound();
            }
        }

        await AuswahlLadenAsync(ct);
        return Page();
    }

    /// <summary>Speichert den Antrag oder legt ihn als Entwurf an.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Antrag, die Seite mit Fehlern, 403 oder 404.</returns>
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
            Formular.WiedereintrittVonId = antrag.WiedereintrittVonId;
        }
        else if (Antragsregeln.MitMitglied(Formular.Art))
        {
            Formular.WiedereintrittVonId = null;
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

            if (Formular.WiedereintrittVonId is { } frueher && await EhemaligerAsync(frueher, ct) is null)
            {
                return NotFound();
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
            antrag = new Antrag
            {
                Art = Formular.Art, Status = AntragStatus.Entwurf, MitgliedId = Formular.MitgliedId, WiedereintrittVonId = Formular.WiedereintrittVonId,
                Vorname = "", Nachname = "",
            };
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
        var hinweise = Antragsregeln.Hinweise(antrag.Art, antrag.Geburtsdatum, antrag.GewuenschtAb ?? Heute, regeln);
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

        var rahmen = await bereich.RahmenAsync(antrag.AbteilungId, Heute, ct);
        return File(AntragPdf.Erzeugen(antrag.Art, antrag, rahmen), "application/pdf", AntragPdf.Dateiname(antrag.Art, antrag));
    }

    /// <summary>Trägt einen Entwurf als eingegangen ein.</summary>
    /// <param name="id">Kennung des Antrags.</param>
    /// <param name="am">Eingangstag; ohne Angabe heute.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Antrag mit Meldung oder Fehler, 403 oder 404.</returns>
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

    /// <summary>Nimmt den Antrag an und führt ihn aus (Aufnahme, Übertritt, Wechsel, Ausscheiden).</summary>
    /// <param name="id">Kennung des Antrags.</param>
    /// <param name="ab">Wirksam ab; ohne Angabe der gewünschte Tag.</param>
    /// <param name="personalnummer">Personalnummer bei Aufnahme.</param>
    /// <param name="probezeit">Wahl der Probezeit.</param>
    /// <param name="probezeitBis">Ende einer verkürzten Probezeit.</param>
    /// <param name="probezeitBemerkung">Begründung zur Probezeit.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Antrag mit Meldung oder Fehler, 403 oder 404.</returns>
    public Task<IActionResult> OnPostAnnehmenAsync(
        Guid id, DateOnly? ab, string? personalnummer, ProbezeitWahl probezeit, DateOnly? probezeitBis, string? probezeitBemerkung, CancellationToken ct) =>
        StatusAsync(id, true, async antrag =>
        {
            if (antrag.Status != AntragStatus.Eingegangen)
            {
                throw new InvalidOperationException("Erst annehmen, wenn der unterschriebene Antrag eingegangen ist.");
            }

            var beginn = ab ?? antrag.GewuenschtAb ?? Heute;
            if (Antragsregeln.MitBeginn(antrag.Art) && beginn < Heute && !await DarfNacherfassenAsync())
            {
                throw new InvalidOperationException(Nacherfassung);
            }

            return await annahme.AnnehmenAsync(antrag,
                new Antragsannahme.Angaben(beginn, personalnummer, probezeit, probezeitBis, probezeitBemerkung), Heute, ct);
        }, ct);

    /// <summary>Lehnt einen eingegangenen Antrag ab.</summary>
    /// <param name="id">Kennung des Antrags.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Antrag mit Meldung oder Fehler, 403 oder 404.</returns>
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

    /// <summary>Trägt den Antrag als zurückgezogen ein.</summary>
    /// <param name="id">Kennung des Antrags.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Antrag mit Meldung oder Fehler, 403 oder 404.</returns>
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

    /// <summary>
    /// Früheres Mitglied, das wieder eintreten kann: in einer freigegebenen Abteilung gewesen, heute ohne Mitgliedschaft,
    /// nicht anonymisiert und nicht verstorben. Sonst null.
    /// </summary>
    private async Task<Mitglied?> EhemaligerAsync(Guid mitgliedId, CancellationToken ct)
    {
        var freigabe = await bereich.FreigabeAsync(User);
        var m = await db.Mitglieder.AsNoTracking().Include(x => x.Mitgliedschaften).ThenInclude(s => s.Abteilung)
            .SingleOrDefaultAsync(x => x.Id == mitgliedId, ct);
        return m is not null && Wiedereintritt.Hindernis(m, Heute) is null
            && m.Mitgliedschaften.Any(s => !s.Vordienstzeit && freigabe.Umfasst(s.AbteilungId)) ? m : null;
    }

    /// <summary>Wiedereintritt: Angaben aus dem früheren Mitglied vorbelegen; Vordienstzeit und Lehrgänge sind schon in FEVAD.</summary>
    private async Task<bool> AusEhemaligemAsync(Guid mitgliedId, CancellationToken ct)
    {
        if (await EhemaligerAsync(mitgliedId, ct) is not { } m)
        {
            return false;
        }

        Formular.WiedereintrittVonId = m.Id;
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
        // Vorschlag: die Abteilung, in der das Mitglied zuletzt war, wenn sie zur Antragsart passt.
        var ziel = Antragsregeln.Zielabteilung(Formular.Art);
        Formular.AbteilungId = m.Mitgliedschaften.Where(s => !s.Vordienstzeit && (ziel is null || s.Abteilung!.Art == ziel) && s.Abteilung!.Aktiv)
            .OrderByDescending(s => s.GueltigAb).FirstOrDefault()?.AbteilungId;
        return true;
    }

    /// <summary>Frühere Mitglieder für die Auswahl „Wiedereintritt“; bei der Jugendfeuerwehr nur frühere Jugendliche.</summary>
    private async Task<IReadOnlyList<SelectListItem>> EhemaligeAsync(Freigabe freigabe, CancellationToken ct)
    {
        var heute = Heute;
        var jugend = Antragsregeln.IstJugendaufnahme(Formular.Art);
        var ids = freigabe.Abteilungen.ToList();
        var kandidaten = await db.Mitglieder.AsNoTracking()
            .Where(m => m.AnonymisiertAm == null
                && m.Mitgliedschaften.Any(s => !s.Vordienstzeit && (freigabe.Alle || ids.Contains(s.AbteilungId)) && (!jugend || s.Abteilung!.Art == AbteilungArt.Jugend || s.Abteilung.Art == AbteilungArt.Kinder))
                && !m.Mitgliedschaften.Any(s => !s.Vordienstzeit && (s.GueltigBis == null || s.GueltigBis >= heute))
                && !m.Mitgliedschaften.Any(s => s.Austrittsgrund == Austrittsgrund.Tod))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, m.Nachname, m.Vorname, m.Personalnummer, Bis = m.Mitgliedschaften.Max(s => s.GueltigBis) })
            .ToListAsync(ct);
        return kandidaten
            .Select(k => new SelectListItem($"{k.Nachname}, {k.Vorname} (Nr. {k.Personalnummer}, bis {Anzeige.Datum(k.Bis)})", k.Id.ToString()))
            .ToList();
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

        var aktuell = m.Mitgliedschaften.Where(s => !s.Vordienstzeit && s.IstAktivAm(heute)).OrderBy(s => s.Abteilung!.Art != AbteilungArt.Einsatz).ToList();
        var einsatz = aktuell.FirstOrDefault(s => s.Abteilung!.Art == AbteilungArt.Einsatz);
        // Beim Übertritt die bisherige Einsatzabteilung, sonst keine: die Zielabteilung wählt, wer den Antrag erfasst.
        Formular.AbteilungId = Formular.Art == AntragArt.Ausscheiden ? aktuell.FirstOrDefault()?.AbteilungId : einsatz?.AbteilungId;
        if (Formular.Art == AntragArt.Ausscheiden)
        {
            // Satzung § 6 Abs. 1: ohne gegenteilige Erklärung in die Altersabteilung.
            Formular.InAltersabteilung = einsatz is not null;
        }
        else if (Formular.Art == AntragArt.Altersabteilung)
        {
            var regeln = await laufbahn.RegelnAsync(heute, ct);
            var moeglich = Domain.Laufbahn.Laufbahnhinweise.Geburtstag(m.Geburtsdatum, regeln.AltersabteilungAbAlter);
            Formular.GewuenschtAb = moeglich > heute ? moeglich : null;
        }
        else if (Formular.Art == AntragArt.UebertrittEinsatz)
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
            Id = a.Id, Art = a.Art, MitgliedId = a.MitgliedId, WiedereintrittVonId = a.WiedereintrittVonId, AbteilungId = a.AbteilungId, GewuenschtAb = a.GewuenschtAb,
            Vorname = a.Vorname, Nachname = a.Nachname, Geburtsdatum = a.Geburtsdatum, Geburtsort = a.Geburtsort, Geschlecht = a.Geschlecht,
            Strasse = a.Strasse, Plz = a.Plz, Ort = a.Ort, Telefon = a.Telefon, Mobil = a.Mobil, Email = a.Email,
            FrueherFeuerwehr = a.FrueherFeuerwehr, FrueherFeuerwehrName = a.FrueherFeuerwehrName, FrueherVon = a.FrueherVon, FrueherBis = a.FrueherBis,
            FrueherAustrittsgrund = a.FrueherAustrittsgrund, FrueherLehrgaenge = a.FrueherLehrgaenge, FrueherDienstgrad = a.FrueherDienstgrad,
            AndereFeuerwehrAktiv = a.AndereFeuerwehrAktiv, AndereFeuerwehrName = a.AndereFeuerwehrName, FrueherAusgeschlossen = a.FrueherAusgeschlossen,
            FotoEinwilligung = a.FotoEinwilligung, Bemerkung = a.Bemerkung, Entlassungsgrund = a.Entlassungsgrund, InAltersabteilung = a.InAltersabteilung,
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

        var ziel = Antragsregeln.Zielabteilung(Formular.Art);
        if (Formular.MitgliedId is { } mitgliedId && Formular.Art is AntragArt.Altersabteilung or AntragArt.Ausscheiden or AntragArt.UebertrittEinsatz)
        {
            // Die Abteilung, aus der gewechselt oder ausgeschieden wird, muss eine aktuelle des Mitglieds sein; der Übertritt setzt die Jugendfeuerwehr voraus.
            var aktuell = await AktuelleMitgliedschaftenAsync(mitgliedId, ct);
            if (Formular.Art == AntragArt.UebertrittEinsatz)
            {
                if (!aktuell.Any(s => s.Art == AbteilungArt.Jugend))
                {
                    ModelState.AddModelError("", "Der Übertritt geht nur aus der Jugendfeuerwehr.");
                }
            }
            else if (aktuell.FirstOrDefault(s => s.AbteilungId == Formular.AbteilungId && (ziel is null || s.Art == ziel)) is not { } bisher)
            {
                ModelState.AddModelError("Formular.AbteilungId", Formular.Art == AntragArt.Altersabteilung
                    ? "In die Altersabteilung wechselt nur, wer derzeit in dieser Einsatzabteilung ist."
                    : "Das Mitglied ist derzeit nicht in dieser Abteilung.");
            }
            else if (Formular.Art == AntragArt.Ausscheiden)
            {
                bisherigeArt = bisher.Art;
                if (Formular.Entlassungsgrund is not { } grund)
                {
                    ModelState.AddModelError("Formular.Entlassungsgrund", "Grund fehlt.");
                }
                else if (!Antragsregeln.Gruende(bisher.Art, bisher.Status == MitgliedStatus.Probezeit).Contains(grund))
                {
                    ModelState.AddModelError("Formular.Entlassungsgrund", "Dieser Grund passt nicht zur Abteilung bzw. zur Probezeit.");
                }
            }
        }
        else if (Formular.AbteilungId is { } abteilungId
            && !await db.Abteilungen.AnyAsync(a => a.Id == abteilungId && a.Aktiv && (ziel == null || a.Art == ziel), ct))
        {
            ModelState.AddModelError("Formular.AbteilungId", "Diese Abteilung passt nicht zum Antrag.");
        }
        else if (Formular.AbteilungId is { } zielId && !Antragsregeln.MitMitglied(Formular.Art)
            && !(await bereich.FreigabeAsync(User)).Umfasst(zielId))
        {
            ModelState.AddModelError("Formular.AbteilungId", "Für diese Abteilung darfst du keine Anträge bearbeiten.");
        }

        if (Formular.Geburtsdatum is { } geburtsdatum)
        {
            foreach (var fehler in Antragsregeln.Fehler(Formular.Art, geburtsdatum, Formular.GewuenschtAb, Heute, await laufbahn.RegelnAsync(Heute, ct),
                await VolljaehrigAbAsync(ct), await parameter.ZahlAsync(ParameterSchluessel.AntraegeVorlaufMonate, Heute, 6, ct)))
            {
                ModelState.AddModelError("Formular.GewuenschtAb", fehler);
            }

            // Wer schon in der Jugendfeuerwehr ist, kommt über den Übertritt, nicht über eine neue Aufnahme.
            var vorname = Formular.Vorname.Trim();
            var nachname = Formular.Nachname.Trim();
            var heute = Heute;
            if (Formular.Art == AntragArt.EinsatzEintritt && await db.Mitglieder.AnyAsync(m => m.Vorname == vorname && m.Nachname == nachname
                && m.Geburtsdatum == geburtsdatum && m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.Abteilung!.Art == AbteilungArt.Jugend
                    && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)), ct))
            {
                ModelState.AddModelError("", $"{nachname}, {vorname} ist in der Jugendfeuerwehr; bitte den Antrag auf Übertritt nutzen.");
            }
        }

        if (Formular.FrueherVon > Formular.FrueherBis)
        {
            ModelState.AddModelError("Formular.FrueherBis", "Das Ende liegt vor dem Beginn.");
        }

        // Ein Beginn vor heute ist Nacherfassung von Altdaten und braucht das eigene Recht (Andi, 25.10.2026).
        if (Antragsregeln.MitBeginn(Formular.Art) && Formular.GewuenschtAb < Heute && !await DarfNacherfassenAsync())
        {
            ModelState.AddModelError("Formular.GewuenschtAb", Nacherfassung);
        }
    }

    private const string Nacherfassung = "Der Beginn liegt vor heute. Rückwirkend erfassen darf nur, wer das Recht „Anträge rückwirkend erfassen“ hat.";

    private async Task<bool> DarfNacherfassenAsync() => (await autorisierung.AuthorizeAsync(User, Berechtigungen.AntraegeNacherfassen)).Succeeded;

    /// <summary>Art der bisherigen Abteilung beim Ausscheiden, gesetzt von <see cref="PruefenAsync"/>.</summary>
    private AbteilungArt? bisherigeArt;

    private sealed record Aktuell(Guid AbteilungId, string Bezeichnung, AbteilungArt Art, MitgliedStatus Status);

    private Task<List<Aktuell>> AktuelleMitgliedschaftenAsync(Guid mitgliedId, CancellationToken ct)
    {
        var heute = Heute;
        return db.Mitgliedschaften.AsNoTracking()
            .Where(s => s.MitgliedId == mitgliedId && !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute))
            .OrderBy(s => s.Abteilung!.Sortierung)
            .Select(s => new Aktuell(s.AbteilungId, s.Abteilung!.Bezeichnung, s.Abteilung.Art, s.Status))
            .ToListAsync(ct);
    }

    private void Uebernehmen(Antrag antrag)
    {
        var f = Formular;
        antrag.Entlassungsgrund = f.Art == AntragArt.Ausscheiden ? f.Entlassungsgrund : null;
        antrag.InAltersabteilung = f.Art == AntragArt.Ausscheiden && f.InAltersabteilung && Antragsregeln.AltersabteilungMoeglich(bisherigeArt ?? AbteilungArt.Einsatz, f.Entlassungsgrund);
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

        antrag.FotoEinwilligung = Antragsregeln.MitFotos(f.Art) ? f.FotoEinwilligung : null;
        antrag.Bemerkung = Leer(f.Bemerkung);

        for (var i = 0; i < 2; i++)
        {
            var e = f.Erziehungsberechtigte[i];
            var alt = antrag.Erziehungsberechtigte.FirstOrDefault(x => x.Nummer == i + 1);
            // Erziehungsberechtigte nur, wo sie unterschreiben; Volljährige brauchen keine.
            if (e.IstLeer || !Antragsregeln.UnterschriftEltern(f.Art))
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
        var heute = Heute;
        if (Formular.MitgliedId is { } mitgliedId && Formular.Art is AntragArt.Altersabteilung or AntragArt.Ausscheiden)
        {
            // Gewechselt oder ausgeschieden wird aus einer Abteilung, in der das Mitglied gerade ist.
            var aktuell = (await AktuelleMitgliedschaftenAsync(mitgliedId, ct)).Where(s => ziel is null || s.Art == ziel).ToList();
            Abteilungen = aktuell.Select(a => new SelectListItem(a.Bezeichnung, a.AbteilungId.ToString())).ToList();
            var bisher = aktuell.FirstOrDefault(a => a.AbteilungId == Formular.AbteilungId) ?? aktuell.FirstOrDefault();
            if (Formular.Art == AntragArt.Ausscheiden && bisher is not null)
            {
                var gruende = Antragsregeln.Gruende(bisher.Art, bisher.Status == MitgliedStatus.Probezeit);
                Gruende = gruende.Select(g => new SelectListItem(Anzeige.Text(g), g.ToString())).ToList();
                AltersabteilungWaehlbar = bisher.Art == AbteilungArt.Einsatz;
            }
        }
        else
        {
            Abteilungen = (await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv && (ziel == null || a.Art == ziel)).OrderBy(a => a.Sortierung)
                    .Select(a => new { a.Id, a.Bezeichnung }).ToListAsync(ct))
                .Where(a => mitMitglied || freigabe.Umfasst(a.Id))
                .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString()))
                .ToList();
        }

        Formular.AbteilungId ??= Abteilungen.Count == 1 ? Guid.Parse(Abteilungen[0].Value) : null;
        MitProbezeit = Antragsregeln.MitProbezeit(Formular.Art);
        AltersabteilungAbAlter = (await laufbahn.RegelnAsync(heute, ct)).AltersabteilungAbAlter;
        if (Formular.MitgliedId is { } id)
        {
            MitgliedName = await db.Mitglieder.Where(m => m.Id == id).Select(m => m.Nachname + ", " + m.Vorname).FirstOrDefaultAsync(ct);
        }

        if (IstNeu && Formular.Art == AntragArt.EinsatzEintritt)
        {
            Jugendliche = await UebertrittMoeglichAsync(freigabe, ct);
        }

        if (IstNeu && Formular.WiedereintrittVonId is null && Formular.Art is AntragArt.EinsatzEintritt or AntragArt.JugendEintritt or AntragArt.KinderEintritt)
        {
            Ehemalige = await EhemaligeAsync(freigabe, ct);
        }

        if (Formular.WiedereintrittVonId is { } frueher)
        {
            WiedereintrittName = await db.Mitglieder.Where(m => m.Id == frueher)
                .Select(m => m.Nachname + ", " + m.Vorname + " (Nr. " + m.Personalnummer + ")").FirstOrDefaultAsync(ct);
        }

        if (Antrag is { Abgeschlossen: false } && Formular.Geburtsdatum is { } geburtsdatum)
        {
            Hinweise = Antragsregeln.Hinweise(Formular.Art, geburtsdatum, Formular.GewuenschtAb ?? heute, await laufbahn.RegelnAsync(heute, ct));
            if (Antrag is { MitgliedId: null, WiedereintrittVonId: null, Status: AntragStatus.Eingegangen } && DarfEntscheiden)
            {
                PersonalnummerVorschlag = await annahme.PersonalnummerVorschlagAsync(ct);
            }
        }
    }

    /// <summary>Mitglieder freigegebener Jugendfeuerwehren, die innerhalb des Vorlaufs das Übertrittsalter erreichen.</summary>
    private async Task<IReadOnlyList<SelectListItem>> UebertrittMoeglichAsync(Freigabe freigabe, CancellationToken ct)
    {
        var heute = Heute;
        var regeln = await laufbahn.RegelnAsync(heute, ct);
        var bis = heute.AddMonths(await parameter.ZahlAsync(ParameterSchluessel.AntraegeVorlaufMonate, heute, 6, ct));
        var ids = freigabe.Abteilungen.ToList();
        var jugend = await db.Mitgliedschaften.AsNoTracking()
            .Where(s => !s.Vordienstzeit && s.Abteilung!.Art == AbteilungArt.Jugend && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (freigabe.Alle || ids.Contains(s.AbteilungId)))
            .Select(s => new { s.MitgliedId, s.Mitglied!.Nachname, s.Mitglied.Vorname, s.Mitglied.Geburtsdatum })
            .ToListAsync(ct);
        return jugend.Where(j => Mitglied.Alter(j.Geburtsdatum, bis) >= regeln.AufnahmeEinsatzAbAlter).DistinctBy(j => j.MitgliedId)
            .OrderBy(j => j.Nachname).ThenBy(j => j.Vorname)
            .Select(j => new SelectListItem($"{j.Nachname}, {j.Vorname} ({Mitglied.Alter(j.Geburtsdatum, heute)})", j.MitgliedId.ToString()))
            .ToList();
    }

    private Task<int> VolljaehrigAbAsync(CancellationToken ct) => parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, Heute, 18, ct);

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
