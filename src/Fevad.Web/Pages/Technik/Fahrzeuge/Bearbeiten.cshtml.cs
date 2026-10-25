using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Fahrzeuge;

/// <summary>Fahrzeug anlegen oder bearbeiten, mit Anlagendaten und Außerdienststellung (V60).</summary>
[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class BearbeitenModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Es wird ein neues Fahrzeug angelegt.</summary>
    public bool IstNeu => Formular.Id is null;
    /// <summary>Auswahl der Prüfpläne für Fahrzeuge.</summary>
    public IReadOnlyList<SelectListItem> Vorlagen { get; private set; } = [];

    /// <summary>Hinweis: Die Außerdienststellung braucht ab der Wertgrenze einen Beschluss.</summary>
    public bool BeschlussNoetig { get; private set; }

    /// <summary>Grenze für den Beschluss in Euro.</summary>
    public int BeschlussGrenze { get; private set; }

    /// <summary>Hinweis: Fördermittel mit laufender Zweckbindung, Fördergeber informieren.</summary>
    public bool FoerdergeberMitteilen { get; private set; }

    /// <summary>Formular eines Fahrzeugs.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung; leer bei neuem Fahrzeug.</summary>
        public Guid? Id { get; set; }
        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }

        /// <summary>Funkrufname wie in alamos.</summary>
        [Required(ErrorMessage = "Funkrufname fehlt."), StringLength(50), Display(Name = "Funkrufname (wie in alamos)")]
        public string Funkrufname { get; set; } = "";

        /// <summary>Fahrzeugtyp.</summary>
        [Required(ErrorMessage = "Typ fehlt."), StringLength(50), Display(Name = "Typ, z. B. HLF 10")]
        public string Typ { get; set; } = "";

        /// <summary>Amtliches Kennzeichen.</summary>
        [StringLength(20), Display(Name = "Kennzeichen")] public string? Kennzeichen { get; set; }
        /// <summary>Hersteller des Fahrgestells.</summary>
        [Display(Name = "Fahrgestell")] public string? Hersteller { get; set; }
        /// <summary>Aufbauhersteller.</summary>
        [Display(Name = "Aufbau")] public string? Aufbau { get; set; }
        /// <summary>Baujahr.</summary>
        [Range(1900, 2100, ErrorMessage = "Baujahr ungültig."), Display(Name = "Baujahr")] public int? Baujahr { get; set; }
        /// <summary>In Dienst seit.</summary>
        [Display(Name = "In Dienst seit")] public DateOnly? IndienstAm { get; set; }
        /// <summary>Außer Dienst seit.</summary>
        [Display(Name = "Außer Dienst seit")] public DateOnly? AusserDienstAm { get; set; }
        /// <summary>Zahl der Sitzplätze.</summary>
        [Range(1, 99), Display(Name = "Sitzplätze")] public int? Sitzplaetze { get; set; }
        /// <summary>ISSI des Funkgeräts.</summary>
        [Display(Name = "ISSI")] public string? Issi { get; set; }
        /// <summary>Prüfplan des Fahrzeugs.</summary>
        [Display(Name = "Prüfplan")] public Guid? PruefvorlageId { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }

        /// <summary>Anschaffungswert brutto.</summary>
        [Range(0, 99999999), Display(Name = "Anschaffungswert brutto (€)")] public decimal? Anschaffungswert { get; set; }

        /// <summary>Anlagenbuchhaltung und Förderung.</summary>
        public AnlageEingabe Anlage { get; set; } = new();

        /// <summary>Grund der Außerdienststellung.</summary>
        [StringLength(500), Display(Name = "Grund der Außerdienststellung")] public string? Aussonderungsgrund { get; set; }

        /// <summary>Verwertung nach der Außerdienststellung.</summary>
        [Display(Name = "Verwertung")] public Verwertung? Verwertung { get; set; }

        /// <summary>Verkaufserlös.</summary>
        [Range(0, 99999999), Display(Name = "Erlös (€)")] public decimal? Erloes { get; set; }

        /// <summary>Verweis auf den Beschluss.</summary>
        [StringLength(200), Display(Name = "Beschluss (Gremium, Datum, TOP)")] public string? Aussonderungsbeschluss { get; set; }

        /// <summary>Mitteilung an den Fördergeber.</summary>
        [Display(Name = "Fördergeber informiert am")] public DateOnly? FoerdergeberMitgeteiltAm { get; set; }
    }

    /// <summary>Zeigt ein Fahrzeug oder ein leeres Formular.</summary>
    /// <param name="id">Kennung des Fahrzeugs; leer für ein neues.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            var f = await db.Fahrzeuge.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (f is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = f.Id,
                Version = db.Entry(f).Property<uint>("Version").CurrentValue,
                Funkrufname = f.Funkrufname,
                Typ = f.Typ,
                Kennzeichen = f.Kennzeichen,
                Hersteller = f.Hersteller,
                Aufbau = f.Aufbau,
                Baujahr = f.Baujahr,
                IndienstAm = f.IndienstAm,
                AusserDienstAm = f.AusserDienstAm,
                Sitzplaetze = f.Sitzplaetze,
                Issi = f.Issi,
                PruefvorlageId = f.PruefvorlageId,
                Bemerkung = f.Bemerkung,
                Anschaffungswert = f.Anschaffungswert,
                Anlage = AnlageEingabe.Aus(f),
                Aussonderungsgrund = f.Aussonderungsgrund,
                Verwertung = f.Verwertung,
                Erloes = f.Erloes,
                Aussonderungsbeschluss = f.Aussonderungsbeschluss,
                FoerdergeberMitgeteiltAm = f.FoerdergeberMitgeteiltAm,
            };
        }

        await AuswahlLadenAsync(ct);
        return Page();
    }

    /// <summary>Speichert das Fahrzeug.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zu den Details, bei Fehlern die Seite oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var funkrufname = (Formular.Funkrufname ?? "").Trim();
        if (await db.Fahrzeuge.IgnoreQueryFilters().AnyAsync(f => f.Funkrufname == funkrufname && f.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Funkrufname", "Diesen Funkrufnamen gibt es schon.");
        }

        if (Formular.PruefvorlageId is not null && !await db.Geraetearten.AnyAsync(g => g.Id == Formular.PruefvorlageId, ct))
        {
            ModelState.AddModelError("Formular.PruefvorlageId", "Unbekannter Prüfplan.");
        }

        if (Formular.AusserDienstAm is not null)
        {
            foreach (var (feld, meldung) in Aussonderungsregeln.Pruefen(Formular.Verwertung, Formular.Erloes, Formular.Aussonderungsgrund))
            {
                ModelState.AddModelError($"Formular.{(feld == "Grund" ? nameof(Eingabe.Aussonderungsgrund) : feld)}", meldung);
            }
        }
        else if (Formular.Verwertung is not null || Formular.Erloes is not null)
        {
            ModelState.AddModelError("Formular.AusserDienstAm", "Verwertung und Erlös gibt es nur mit einem Datum „Außer Dienst seit“.");
        }

        if (!ModelState.IsValid)
        {
            await AuswahlLadenAsync(ct);
            return Page();
        }

        Fahrzeug fahrzeug;
        if (IstNeu)
        {
            fahrzeug = new Fahrzeug { Funkrufname = funkrufname, Typ = Formular.Typ };
            db.Fahrzeuge.Add(fahrzeug);
        }
        else
        {
            var vorhanden = await db.Fahrzeuge.FirstOrDefaultAsync(f => f.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            fahrzeug = vorhanden;
            db.Entry(fahrzeug).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        fahrzeug.Funkrufname = funkrufname;
        fahrzeug.Typ = Formular.Typ.Trim();
        fahrzeug.Kennzeichen = Formular.Kennzeichen;
        fahrzeug.Hersteller = Formular.Hersteller;
        fahrzeug.Aufbau = Formular.Aufbau;
        fahrzeug.Baujahr = Formular.Baujahr;
        fahrzeug.IndienstAm = Formular.IndienstAm;
        fahrzeug.AusserDienstAm = Formular.AusserDienstAm;
        fahrzeug.Sitzplaetze = Formular.Sitzplaetze;
        fahrzeug.Issi = Formular.Issi;
        fahrzeug.PruefvorlageId = Formular.PruefvorlageId;
        fahrzeug.Bemerkung = Formular.Bemerkung;
        fahrzeug.Anschaffungswert = Formular.Anschaffungswert;
        Formular.Anlage.Uebernehmen(fahrzeug);
        fahrzeug.Aussonderungsgrund = string.IsNullOrWhiteSpace(Formular.Aussonderungsgrund) ? null : Formular.Aussonderungsgrund.Trim();
        fahrzeug.Verwertung = Formular.Verwertung;
        fahrzeug.Erloes = Formular.Erloes;
        fahrzeug.Aussonderungsbeschluss = string.IsNullOrWhiteSpace(Formular.Aussonderungsbeschluss) ? null : Formular.Aussonderungsbeschluss.Trim();
        fahrzeug.FoerdergeberMitgeteiltAm = Formular.FoerdergeberMitgeteiltAm;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Das Fahrzeug wurde inzwischen von jemand anderem geändert. Bitte neu laden und die Änderungen erneut eintragen.");
            await AuswahlLadenAsync(ct);
            return Page();
        }

        TempData["Meldung"] = $"{fahrzeug.Funkrufname} gespeichert.";
        return RedirectToPage("Details", new { id = fahrzeug.Id });
    }

    private async Task AuswahlLadenAsync(CancellationToken ct)
    {
        Vorlagen = await db.Geraetearten
            .Where(g => g.FuerFahrzeuge && (g.Aktiv || g.Id == Formular.PruefvorlageId))
            .OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString()))
            .ToListAsync(ct);
        var heute = Anzeige.Heute(zeit);
        BeschlussGrenze = await parameter.ZahlAsync(ParameterSchluessel.AussonderungBeschlussAbWertEuro, heute, 1000, ct);
        BeschlussNoetig = Aussonderungsregeln.BeschlussNoetig(Formular.Anschaffungswert, BeschlussGrenze);
        var stand = new Fahrzeug { Funkrufname = "", Typ = "" };
        Formular.Anlage.Uebernehmen(stand);
        FoerdergeberMitteilen = Aussonderungsregeln.FoerdergeberMitteilen(stand, Formular.AusserDienstAm ?? heute);
    }
}
