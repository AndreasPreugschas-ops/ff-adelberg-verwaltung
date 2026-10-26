using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraete;

/// <summary>Gerät anlegen oder bearbeiten, auch aus dem Eingang einer Beschaffung.</summary>
[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class BearbeitenModel(AppDbContext db, ParameterLeser parameter, UserManager<Benutzer> benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Es wird ein neues Gerät angelegt.</summary>
    public bool IstNeu => Formular.Id is null;
    /// <summary>Auswahl der Gerätearten.</summary>
    public IReadOnlyList<SelectListItem> Geraetearten { get; private set; } = [];
    /// <summary>Auswahl der Materialstämme.</summary>
    public IReadOnlyList<SelectListItem> Materialien { get; private set; } = [];
    /// <summary>Auswahl der Fahrzeuge.</summary>
    public IReadOnlyList<SelectListItem> Fahrzeuge { get; private set; } = [];
    /// <summary>Auswahl der Inventurgruppen.</summary>
    public IReadOnlyList<SelectListItem> Inventurgruppen { get; private set; } = [];

    /// <summary>Wert der Materialauswahl für „neuen Materialstamm anlegen“.</summary>
    public const string NeuesMaterial = "neu";

    /// <summary>Formular eines Geräts.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung; leer bei neuem Gerät.</summary>
        public Guid? Id { get; set; }
        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }
        /// <summary>Beschaffung, aus deren Eingang das Gerät angelegt wird.</summary>
        public Guid? BeschaffungId { get; set; }

        /// <summary>Eindeutige Inventarnummer.</summary>
        [Required(ErrorMessage = "Inventarnummer fehlt."), StringLength(30), Display(Name = "Inventarnummer")]
        public string Inventarnummer { get; set; } = "";

        /// <summary>Id eines Materialstamms oder <see cref="NeuesMaterial"/>.</summary>
        [Required(ErrorMessage = "Bitte einen Materialstamm wählen oder neu anlegen."), Display(Name = "Materialstamm")]
        public string? Material { get; set; }

        /// <summary>Bezeichnung; leer übernimmt die Bezeichnung des Materialstamms.</summary>
        [StringLength(200), Display(Name = "Bezeichnung (leer = wie Materialstamm)")]
        public string? Bezeichnung { get; set; }

        /// <summary>Angaben für einen neuen Materialstamm.</summary>
        public MaterialEingabe Neu { get; set; } = new();

        /// <summary>Seriennummer.</summary>
        [StringLength(100), Display(Name = "Seriennummer")] public string? Seriennummer { get; set; }
        /// <summary>Baujahr.</summary>
        [Range(1900, 2100, ErrorMessage = "Baujahr ungültig."), Display(Name = "Baujahr")] public int? Baujahr { get; set; }
        /// <summary>Herstelldatum.</summary>
        [Display(Name = "Herstelldatum")] public DateOnly? Herstelldatum { get; set; }
        /// <summary>Nutzungsende laut Prägung (Composite-Flaschen).</summary>
        [Display(Name = "Nutzbar bis (laut Prägung)")] public DateOnly? NutzbarBis { get; set; }
        /// <summary>Größe bei PSA.</summary>
        [StringLength(20), Display(Name = "Größe")] public string? Groesse { get; set; }
        /// <summary>Tag der Beschaffung.</summary>
        [Display(Name = "Beschafft am")] public DateOnly? BeschafftAm { get; set; }
        /// <summary>Anschaffungspreis brutto in Euro.</summary>
        [Range(0, 9999999), Display(Name = "Anschaffungspreis brutto (€)")] public decimal? Anschaffungspreis { get; set; }
        /// <summary>In Dienst seit.</summary>
        [Display(Name = "In Dienst seit")] public DateOnly? IndienstAm { get; set; }
        /// <summary>Fahrzeug, auf dem das Gerät verlastet ist.</summary>
        [Display(Name = "Verlastet auf")] public Guid? FahrzeugId { get; set; }
        /// <summary>Lagerort oder Fach.</summary>
        [StringLength(50), Display(Name = "Lagerort / Fach")] public string? Lagerort { get; set; }
        /// <summary>Inventurgruppe.</summary>
        [Display(Name = "Inventurgruppe")] public Guid? InventurgruppeId { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }

        /// <summary>Anlagenbuchhaltung und Förderung (V60).</summary>
        public AnlageEingabe Anlage { get; set; } = new();

        /// <summary>Nur beim Anlegen: Gerät bis zur Eingangsprüfung sperren (V60).</summary>
        [Display(Name = "Eingangsprüfung vor der ersten Nutzung fordern (Gerät bis dahin gesperrt)")]
        public bool Eingangspruefung { get; set; }
    }

    /// <summary>Neuer Materialstamm direkt bei der Erfassung des Geräts.</summary>
    public sealed class MaterialEingabe
    {
        /// <summary>Bezeichnung des Materials.</summary>
        [StringLength(200), Display(Name = "Bezeichnung")] public string? Bezeichnung { get; set; }
        /// <summary>Hersteller.</summary>
        [StringLength(100), Display(Name = "Hersteller")] public string? Hersteller { get; set; }
        /// <summary>Typ oder Modell.</summary>
        [StringLength(100), Display(Name = "Typ / Modell")] public string? Typ { get; set; }
        /// <summary>Teilenummer des Herstellers.</summary>
        [StringLength(100), Display(Name = "Herstellerteilenummer")] public string? Herstellerteilenummer { get; set; }
        /// <summary>Geräteart; bestimmt den Prüfplan.</summary>
        [Display(Name = "Geräteart (bestimmt den Prüfplan)")] public Guid? GeraeteartId { get; set; }
        /// <summary>Stückwert netto in Euro.</summary>
        [Range(0, 9999999), Display(Name = "Stückwert netto (€)")] public decimal? Stueckwert { get; set; }
        /// <summary>Stücke werden bei Inventuren gezählt.</summary>
        [Display(Name = "inventurpflichtig")] public bool Inventurpflichtig { get; set; } = true;
    }

    /// <summary>Zeigt ein Gerät oder ein leeres Formular, auf Wunsch vorbelegt aus einer Beschaffung oder einem Materialstamm.</summary>
    /// <param name="id">Kennung des Geräts; leer für ein neues.</param>
    /// <param name="beschaffungId">Beschaffung, aus deren Eingang das Gerät entsteht.</param>
    /// <param name="materialId">Vorgewählter Materialstamm.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? beschaffungId, Guid? materialId, CancellationToken ct)
    {
        if (id is not null)
        {
            var g = await db.Geraete.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (g is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = g.Id,
                Version = db.Entry(g).Property<uint>("Version").CurrentValue,
                BeschaffungId = g.BeschaffungId,
                Inventarnummer = g.Inventarnummer,
                Material = g.MaterialId.ToString(),
                Bezeichnung = g.Bezeichnung,
                Seriennummer = g.Seriennummer,
                Baujahr = g.Baujahr,
                Herstelldatum = g.Herstelldatum,
                NutzbarBis = g.NutzbarBis,
                Groesse = g.Groesse,
                BeschafftAm = g.BeschafftAm,
                Anschaffungspreis = g.Anschaffungspreis,
                IndienstAm = g.IndienstAm,
                FahrzeugId = g.FahrzeugId,
                Lagerort = g.Lagerort,
                InventurgruppeId = g.InventurgruppeId,
                Bemerkung = g.Bemerkung,
                Anlage = AnlageEingabe.Aus(g),
            };
        }
        else if (beschaffungId is not null && await db.Beschaffungen.AsNoTracking().FirstOrDefaultAsync(b => b.Id == beschaffungId, ct) is { } beschaffung)
        {
            // Eingang einer Beschaffung: Angaben übernehmen, Inventarnummer vergibt der Gerätewart.
            var stueckpreis = beschaffung.Istkosten ?? beschaffung.Kostenschaetzung;
            Formular = new Eingabe
            {
                BeschaffungId = beschaffung.Id,
                Material = NeuesMaterial,
                Neu = new MaterialEingabe
                {
                    Bezeichnung = beschaffung.Bezeichnung,
                    GeraeteartId = beschaffung.GeraeteartId,
                    Stueckwert = stueckpreis is null ? null : Math.Round(stueckpreis.Value / Math.Max(beschaffung.Menge, 1), 2),
                },
                FahrzeugId = beschaffung.FahrzeugId,
                BeschafftAm = beschaffung.GeliefertAm,
                IndienstAm = beschaffung.GeliefertAm,
                Anschaffungspreis = stueckpreis is null ? null : Math.Round(stueckpreis.Value / Math.Max(beschaffung.Menge, 1), 2),
                Eingangspruefung = true,
            };
        }
        else
        {
            Formular.IndienstAm = Anzeige.Heute(zeit);
            Formular.Material = materialId?.ToString();
        }

        await AuswahlLadenAsync(ct);
        return Page();
    }

    /// <summary>Speichert das Gerät, bei Bedarf mit neuem Materialstamm.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zu den Details, bei Fehlern die Seite oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Der eindeutige Index gilt auch für gelöschte Geräte.
        var nummer = (Formular.Inventarnummer ?? "").Trim();
        if (await db.Geraete.IgnoreQueryFilters().AnyAsync(g => g.Inventarnummer == nummer && g.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Inventarnummer", "Diese Inventarnummer ist schon vergeben.");
        }

        Material? material = null;
        if (Formular.Material == NeuesMaterial)
        {
            if (string.IsNullOrWhiteSpace(Formular.Neu.Bezeichnung))
            {
                ModelState.AddModelError("Formular.Neu.Bezeichnung", "Bezeichnung des Materialstamms fehlt.");
            }

            if (Formular.Neu.GeraeteartId is null || !await db.Geraetearten.AnyAsync(g => g.Id == Formular.Neu.GeraeteartId && !g.FuerFahrzeuge, ct))
            {
                ModelState.AddModelError("Formular.Neu.GeraeteartId", "Bitte eine Geräteart wählen.");
            }
        }
        else if (Guid.TryParse(Formular.Material, out var materialId))
        {
            material = await db.Materialien.FirstOrDefaultAsync(m => m.Id == materialId, ct);
            if (material is null)
            {
                ModelState.AddModelError("Formular.Material", "Unbekannter Materialstamm.");
            }
        }

        if (Formular.FahrzeugId is not null && !await db.Fahrzeuge.AnyAsync(f => f.Id == Formular.FahrzeugId, ct))
        {
            ModelState.AddModelError("Formular.FahrzeugId", "Unbekanntes Fahrzeug.");
        }

        if (Formular.BeschaffungId is not null && !await db.Beschaffungen.AnyAsync(b => b.Id == Formular.BeschaffungId, ct))
        {
            Formular.BeschaffungId = null;
        }

        if (Formular.InventurgruppeId is not null && !await db.Inventurgruppen.AnyAsync(g => g.Id == Formular.InventurgruppeId, ct))
        {
            ModelState.AddModelError("Formular.InventurgruppeId", "Unbekannte Inventurgruppe.");
        }

        var inventurpflichtig = material?.Inventurpflichtig ?? Formular.Neu.Inventurpflichtig;
        if (ModelState.IsValid && inventurpflichtig && Formular.InventurgruppeId is null && Formular.FahrzeugId is null)
        {
            ModelState.AddModelError("Formular.InventurgruppeId", "Das Material ist inventurpflichtig. Bitte eine Inventurgruppe wählen oder das Gerät auf ein Fahrzeug verlasten.");
        }

        if (!ModelState.IsValid)
        {
            await AuswahlLadenAsync(ct);
            return Page();
        }

        if (material is null)
        {
            var heute = Anzeige.Heute(zeit);
            var abWert = await parameter.ZahlAsync(ParameterSchluessel.InventarisierungAbWertEuro, heute, 150, ct);
            material = new Material
            {
                Bezeichnung = Formular.Neu.Bezeichnung!.Trim(),
                Hersteller = Leer(Formular.Neu.Hersteller),
                Typ = Leer(Formular.Neu.Typ),
                Herstellerteilenummer = Leer(Formular.Neu.Herstellerteilenummer),
                GeraeteartId = Formular.Neu.GeraeteartId!.Value,
                Stueckwert = Formular.Neu.Stueckwert,
                Inventarisierungspflichtig = Domain.Technik.Material.InventarisierungVorschlagen(Formular.Neu.Stueckwert, abWert) || inventurpflichtig,
                Inventurpflichtig = inventurpflichtig,
            };
            db.Materialien.Add(material);
        }

        Geraet geraet;
        if (IstNeu)
        {
            geraet = new Geraet { Inventarnummer = nummer, Bezeichnung = material.Bezeichnung, BeschaffungId = Formular.BeschaffungId };
            db.Geraete.Add(geraet);
        }
        else
        {
            var vorhanden = await db.Geraete.FirstOrDefaultAsync(g => g.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            geraet = vorhanden;
            db.Entry(geraet).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        geraet.Inventarnummer = nummer;
        geraet.Bezeichnung = Leer(Formular.Bezeichnung) ?? material.Bezeichnung;
        Materialstamm.Zuordnen(geraet, material);
        geraet.Seriennummer = Formular.Seriennummer;
        geraet.Baujahr = Formular.Baujahr;
        geraet.Herstelldatum = Formular.Herstelldatum;
        geraet.NutzbarBis = Formular.NutzbarBis;
        geraet.Groesse = Formular.Groesse;
        geraet.BeschafftAm = Formular.BeschafftAm;
        geraet.Anschaffungspreis = Formular.Anschaffungspreis ?? material.Stueckwert;
        geraet.IndienstAm = Formular.IndienstAm;
        geraet.FahrzeugId = Formular.FahrzeugId;
        geraet.Lagerort = Formular.Lagerort;
        geraet.Bemerkung = Formular.Bemerkung;
        Formular.Anlage.Uebernehmen(geraet);
        if (IstNeu && Formular.Eingangspruefung)
        {
            db.Maengel.Add(Mangel.Eingangssperre(geraet.Id, zeit.GetUtcNow(), (await benutzer.GetUserAsync(User))?.MitgliedId));
        }

        // Ohne eigene Wahl zählt ein verlastetes Gerät in der Inventurgruppe seines Fahrzeugs.
        geraet.InventurgruppeId = Formular.InventurgruppeId
            ?? (Formular.FahrzeugId is { } fahrzeugId ? (await Materialstamm.FahrzeuggruppeAsync(db, fahrzeugId, ct)).Id : null);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Das Gerät wurde inzwischen von jemand anderem geändert. Bitte neu laden und die Änderungen erneut eintragen.");
            await AuswahlLadenAsync(ct);
            return Page();
        }

        TempData["Meldung"] = $"{geraet.Inventarnummer} {geraet.Bezeichnung} gespeichert.";
        return RedirectToPage("Details", new { id = geraet.Id });
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task AuswahlLadenAsync(CancellationToken ct)
    {
        var gewaehlt = Guid.TryParse(Formular.Material, out var materialId) ? materialId : (Guid?)null;
        var materialien = await db.Materialien
            .Where(m => m.Aktiv || m.Id == gewaehlt)
            .OrderBy(m => m.Geraeteart!.Sortierung).ThenBy(m => m.Bezeichnung).ThenBy(m => m.Hersteller)
            .Select(m => new { m.Id, m.Bezeichnung, m.Hersteller, m.Typ, m.Herstellerteilenummer, Art = m.Geraeteart!.Bezeichnung })
            .ToListAsync(ct);
        var gruppen = materialien.Select(m => m.Art).Distinct().ToDictionary(a => a, a => new SelectListGroup { Name = a });
        Materialien = materialien
            .Select(m => new SelectListItem(
                string.Join(" · ", new[] { m.Bezeichnung, m.Hersteller, m.Typ, m.Herstellerteilenummer }.Where(t => !string.IsNullOrWhiteSpace(t))),
                m.Id.ToString()) { Group = gruppen[m.Art] })
            .ToList();
        Inventurgruppen = await db.Inventurgruppen
            .Where(g => g.Aktiv || g.Id == Formular.InventurgruppeId)
            .OrderBy(g => g.Sortierung).ThenBy(g => g.Bezeichnung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString()))
            .ToListAsync(ct);
        Geraetearten = await db.Geraetearten
            .Where(g => !g.FuerFahrzeuge && (g.Aktiv || g.Id == Formular.Neu.GeraeteartId))
            .OrderBy(g => g.Sortierung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString()))
            .ToListAsync(ct);
        Fahrzeuge = await db.Fahrzeuge
            .Where(f => f.AusserDienstAm == null || f.Id == Formular.FahrzeugId)
            .OrderBy(f => f.Funkrufname)
            .Select(f => new SelectListItem(f.Funkrufname, f.Id.ToString()))
            .ToListAsync(ct);
    }
}
