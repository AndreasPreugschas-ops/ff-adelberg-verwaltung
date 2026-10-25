using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Stammdaten;

/// <summary>
/// Abteilungen und Funktionen der Feuerwehr pflegen. Regeln: mindestens eine aktive Einsatzabteilung, die Kinderfeuerwehr
/// hängt an einer Jugendfeuerwehr, die Art einer Abteilung mit Mitgliedschaften bleibt fest (an ihr hängen Probezeit,
/// Altersgrenzen und Dienstzeit), und eine Abteilung mit aktuellen Mitgliedern wird nicht abgeschaltet.
/// </summary>
[Authorize(Policy = Berechtigungen.StammdatenOrganisation)]
public class OrganisationModel(AppDbContext db, Rechteverzeichnis rechte, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Abteilung in der Liste.</summary>
    /// <param name="Id">Kennung der Abteilung.</param>
    /// <param name="Code">Kurzcode der Abteilung.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Kurzbezeichnung">Kurzbezeichnung.</param>
    /// <param name="Art">Art der Abteilung.</param>
    /// <param name="Uebergeordnete">Bezeichnung der übergeordneten Abteilung (Kinderfeuerwehr unter Jugendfeuerwehr).</param>
    /// <param name="Aktiv">Die Abteilung ist aktiv.</param>
    /// <param name="Mitglieder">Anzahl der aktuellen Mitglieder.</param>
    /// <param name="Anschrift">Anschrift des Standorts in einer Zeile.</param>
    public sealed record AbteilungZeile(
        Guid Id, string Code, string Bezeichnung, string? Kurzbezeichnung, AbteilungArt Art, string? Uebergeordnete, bool Aktiv, int Mitglieder, string? Anschrift);
    /// <summary>Eine Funktion in der Liste.</summary>
    /// <param name="Id">Kennung der Funktion.</param>
    /// <param name="Code">Kurzcode der Funktion.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="AmtszeitJahre">Amtszeit bei Wahl in Jahren.</param>
    /// <param name="VorgeschlageneRolle">Rolle, die typischerweise vergeben wird.</param>
    /// <param name="Aktiv">Die Funktion ist aktiv.</param>
    /// <param name="Inhaber">Anzahl der aktuellen Inhaber.</param>
    /// <param name="Grundlage">Rechtsgrundlage.</param>
    public sealed record FunktionZeile(
        Guid Id, string Code, string Bezeichnung, int? AmtszeitJahre, string? VorgeschlageneRolle, bool Aktiv, int Inhaber, string? Grundlage);

    /// <summary>Alle Abteilungen.</summary>
    public IReadOnlyList<AbteilungZeile> Abteilungen { get; private set; } = [];
    /// <summary>Alle Funktionen.</summary>
    public IReadOnlyList<FunktionZeile> Funktionen { get; private set; } = [];
    /// <summary>Jugendfeuerwehren zur Auswahl als übergeordnete Abteilung der Kinderfeuerwehr.</summary>
    public IReadOnlyList<SelectListItem> Jugendabteilungen { get; private set; } = [];
    /// <summary>Rollen zur Auswahl als vorgeschlagene Rolle.</summary>
    public IReadOnlyList<SelectListItem> RollenAuswahl { get; private set; } = [];

    /// <summary>Gewählte Abteilung oder Funktion; ohne Auswahl legt das jeweilige Formular neu an.</summary>
    public Guid? AbteilungAuswahl { get; private set; }
    /// <summary>Gewählte Funktion; ohne Auswahl legt das Formular neu an.</summary>
    public Guid? FunktionAuswahl { get; private set; }

    /// <summary>Die Art der gewählten Abteilung ist nicht mehr änderbar.</summary>
    public bool ArtFest { get; private set; }

    /// <summary>Formulardaten der Abteilung.</summary>
    [BindProperty] public AbteilungFormular Abteilung { get; set; } = new();
    /// <summary>Formulardaten der Funktion.</summary>
    [BindProperty] public FunktionFormular Funktion { get; set; } = new();

    /// <summary>Eingabefelder einer Abteilung.</summary>
    public sealed class AbteilungFormular
    {
        /// <summary>Bezeichnung der Abteilung.</summary>
        [Display(Name = "Bezeichnung"), Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100)]
        public string? Bezeichnung { get; set; }

        /// <summary>Kurzbezeichnung der Abteilung.</summary>
        [Display(Name = "Kurzbezeichnung"), StringLength(20)]
        public string? Kurzbezeichnung { get; set; }

        /// <summary>Art der Abteilung; nicht mehr änderbar, sobald es Mitgliedschaften gibt.</summary>
        [Display(Name = "Art")]
        public AbteilungArt Art { get; set; }

        /// <summary>Übergeordnete Abteilung (nur Kinderfeuerwehr unter Jugendfeuerwehr).</summary>
        [Display(Name = "Gehört zu")]
        public Guid? UebergeordneteId { get; set; }

        /// <summary>Anschrift des Standorts; steht im Kopf der Antragsformulare.</summary>
        [Display(Name = "Straße und Hausnummer"), Required(ErrorMessage = "Straße fehlt."), StringLength(200)]
        public string? Strasse { get; set; }

        /// <summary>Postleitzahl des Standorts.</summary>
        [Display(Name = "PLZ"), Required(ErrorMessage = "PLZ fehlt."), RegularExpression("^[0-9]{5}$", ErrorMessage = "Fünf Ziffern.")]
        public string? Plz { get; set; }

        /// <summary>Ort des Standorts.</summary>
        [Display(Name = "Ort"), Required(ErrorMessage = "Ort fehlt."), StringLength(100)]
        public string? Ort { get; set; }

        /// <summary>Sortierschlüssel.</summary>
        [Display(Name = "Sortierung")]
        public int Sortierung { get; set; }

        /// <summary>Die Abteilung ist aktiv.</summary>
        [Display(Name = "aktiv")]
        public bool Aktiv { get; set; } = true;
    }

    /// <summary>Eingabefelder einer Funktion.</summary>
    public sealed class FunktionFormular
    {
        /// <summary>Kurzcode der Funktion.</summary>
        [Display(Name = "Code"), StringLength(20)]
        [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Nur Buchstaben, Ziffern und Bindestrich.")]
        public string? Code { get; set; }

        /// <summary>Bezeichnung der Funktion.</summary>
        [Display(Name = "Bezeichnung"), Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100)]
        public string? Bezeichnung { get; set; }

        /// <summary>Kurzbezeichnung der Funktion.</summary>
        [Display(Name = "Kurzbezeichnung"), StringLength(20)]
        public string? Kurzbezeichnung { get; set; }

        /// <summary>Amtszeit bei Wahl in Jahren.</summary>
        [Display(Name = "Amtszeit in Jahren"), Range(1, 10, ErrorMessage = "Zwischen 1 und 10 Jahren.")]
        public int? AmtszeitJahre { get; set; }

        /// <summary>Rolle, die bei dieser Funktion typischerweise vergeben wird; wird nicht automatisch zugewiesen.</summary>
        [Display(Name = "Vorgeschlagene Rolle")]
        public string? VorgeschlageneRolle { get; set; }

        /// <summary>Rechtsgrundlage zur Anzeige.</summary>
        [Display(Name = "Rechtsgrundlage"), StringLength(200)]
        public string? Grundlage { get; set; }

        /// <summary>Nötige Qualifikationen: „|“ heißt oder, „;“ trennt Gruppen, die alle erfüllt sein müssen.</summary>
        [Display(Name = "Voraussetzungen (Qualifikationscodes)"), StringLength(200)]
        [RegularExpression(@"^[A-Za-z0-9\-|; ]*$", ErrorMessage = "Nur Codes, getrennt mit | (oder) und ; (und).")]
        public string? Voraussetzungen { get; set; }

        /// <summary>Abteilungsart, zu der die Funktion gehört; leer für die ganze Feuerwehr.</summary>
        [Display(Name = "Gehört zu Abteilungsart")]
        public AbteilungArt? Abteilungsart { get; set; }

        /// <summary>Die Funktion wird gewählt.</summary>
        [Display(Name = "wird gewählt")]
        public bool MitWahl { get; set; }

        /// <summary>Die Funktion braucht einen Beschluss bzw. eine Bestellung.</summary>
        [Display(Name = "braucht Beschluss bzw. Bestellung")]
        public bool MitBeschluss { get; set; } = true;

        /// <summary>Wer beschließt oder bestellt, z. B. „Gemeinderat“.</summary>
        [Display(Name = "Beschluss durch"), StringLength(100)]
        public string? BeschlussDurch { get; set; }

        /// <summary>Sortierschlüssel.</summary>
        [Display(Name = "Sortierung")]
        public int Sortierung { get; set; }

        /// <summary>Die Funktion ist aktiv.</summary>
        [Display(Name = "aktiv")]
        public bool Aktiv { get; set; } = true;
    }

    /// <summary>Lädt Abteilungen und Funktionen und füllt die Formulare mit der gewählten.</summary>
    /// <param name="abteilung">Gewählte Abteilung.</param>
    /// <param name="funktion">Gewählte Funktion.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(Guid? abteilung, Guid? funktion, CancellationToken ct)
    {
        await LadenAsync(ct);
        if (abteilung is { } aId && await db.Abteilungen.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aId, ct) is { } a)
        {
            AbteilungAuswahl = a.Id;
            ArtFest = await db.Mitgliedschaften.IgnoreQueryFilters().AnyAsync(s => s.AbteilungId == a.Id, ct);
            Abteilung = new AbteilungFormular
            {
                Bezeichnung = a.Bezeichnung, Kurzbezeichnung = a.Kurzbezeichnung, Art = a.Art,
                UebergeordneteId = a.UebergeordneteId, Sortierung = a.Sortierung, Aktiv = a.Aktiv,
                Strasse = a.Strasse, Plz = a.Plz, Ort = a.Ort,
            };
        }
        else
        {
            Abteilung.Sortierung = Abteilungen.Count * 10 + 10;
        }

        if (funktion is { } fId && await db.Funktionen.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fId, ct) is { } f)
        {
            FunktionAuswahl = f.Id;
            Funktion = new FunktionFormular
            {
                Code = f.Code, Bezeichnung = f.Bezeichnung, Kurzbezeichnung = f.Kurzbezeichnung, AmtszeitJahre = f.AmtszeitJahre,
                VorgeschlageneRolle = f.VorgeschlageneRolle, Sortierung = f.Sortierung, Aktiv = f.Aktiv,
                Grundlage = f.Grundlage, Voraussetzungen = f.Voraussetzungen, Abteilungsart = f.Abteilungsart,
                MitWahl = f.MitWahl, MitBeschluss = f.MitBeschluss, BeschlussDurch = f.BeschlussDurch,
            };
        }
        else
        {
            Funktion.Sortierung = Funktionen.Count * 10 + 10;
        }
    }

    /// <summary>Speichert eine Abteilung oder legt sie an und prüft die Regeln der Gliederung.</summary>
    /// <param name="id">Kennung der Abteilung; <c>null</c> legt neu an.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAbteilungAsync(Guid? id, CancellationToken ct)
    {
        NurFormularPruefen(nameof(Abteilung));
        var abteilung = id is null ? null : await db.Abteilungen.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (id is not null && abteilung is null)
        {
            return NotFound();
        }

        var f = Abteilung;
        var hatMitgliedschaften = abteilung is not null
            && await db.Mitgliedschaften.IgnoreQueryFilters().AnyAsync(s => s.AbteilungId == abteilung.Id, ct);
        if (abteilung is not null && hatMitgliedschaften && f.Art != abteilung.Art)
        {
            ModelState.AddModelError("Abteilung.Art", "Die Abteilung hat schon Mitgliedschaften; ihre Art bleibt deshalb fest.");
        }

        if (f.Art == AbteilungArt.Kinder)
        {
            var oben = f.UebergeordneteId is { } o ? await db.Abteilungen.AsNoTracking().FirstOrDefaultAsync(a => a.Id == o, ct) : null;
            if (oben is null || oben.Art != AbteilungArt.Jugend)
            {
                ModelState.AddModelError("Abteilung.UebergeordneteId", "Die Kinderfeuerwehr gehört zu einer Jugendfeuerwehr.");
            }
        }

        var heute = Anzeige.Heute(zeit);
        if (abteilung is not null && abteilung.Aktiv && !f.Aktiv)
        {
            if (await db.Mitgliedschaften.AnyAsync(s => s.AbteilungId == abteilung.Id && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute), ct))
            {
                ModelState.AddModelError("Abteilung.Aktiv", "Die Abteilung hat noch aktuelle Mitglieder. Erst übertreten oder austreten lassen.");
            }

            if (await db.Abteilungen.AnyAsync(a => a.UebergeordneteId == abteilung.Id && a.Aktiv, ct))
            {
                ModelState.AddModelError("Abteilung.Aktiv", "Erst die zugehörigen Unterabteilungen abschalten.");
            }
        }

        // Mindestens eine aktive Einsatzabteilung muss bleiben.
        var bleibtEinsatz = await db.Abteilungen.AnyAsync(a => a.Art == AbteilungArt.Einsatz && a.Aktiv && a.Id != id, ct)
            || (f.Art == AbteilungArt.Einsatz && f.Aktiv);
        if (!bleibtEinsatz)
        {
            ModelState.AddModelError("Abteilung.Aktiv", "Mindestens eine Einsatzabteilung muss aktiv bleiben.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            AbteilungAuswahl = id;
            ArtFest = hatMitgliedschaften;
            return Page();
        }

        if (abteilung is null)
        {
            abteilung = new Abteilung { Code = await NeuerCodeAsync(f.Art, ct), Bezeichnung = "" };
            db.Abteilungen.Add(abteilung);
        }

        abteilung.Bezeichnung = f.Bezeichnung!.Trim();
        abteilung.Kurzbezeichnung = Text(f.Kurzbezeichnung);
        abteilung.Art = f.Art;
        abteilung.UebergeordneteId = f.Art == AbteilungArt.Kinder ? f.UebergeordneteId : null;
        abteilung.Sortierung = f.Sortierung;
        abteilung.Aktiv = f.Aktiv;
        abteilung.Strasse = f.Strasse!.Trim();
        abteilung.Plz = f.Plz!.Trim();
        abteilung.Ort = f.Ort!.Trim();
        await db.SaveChangesAsync(ct);
        // Freigaben schließen Unterabteilungen ein und die Jugend-Policies hängen an der Art.
        rechte.Verwerfen();
        TempData["Meldung"] = $"Abteilung „{abteilung.Bezeichnung}“ gespeichert.";
        return RedirectToPage(new { abteilung = abteilung.Id });
    }

    /// <summary>Speichert eine Funktion oder legt sie an.</summary>
    /// <param name="id">Kennung der Funktion; <c>null</c> legt neu an.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostFunktionAsync(Guid? id, CancellationToken ct)
    {
        NurFormularPruefen(nameof(Funktion));
        var funktion = id is null ? null : await db.Funktionen.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (id is not null && funktion is null)
        {
            return NotFound();
        }

        var f = Funktion;
        var code = Text(f.Code)?.ToUpperInvariant();
        if (funktion is null)
        {
            // Der Code ist fest, weil Ehrungsarten („Jahre in Funktion“) und Wahlen ihn verwenden.
            if (code is null)
            {
                ModelState.AddModelError("Funktion.Code", "Code fehlt.");
            }
            else if (await db.Funktionen.IgnoreQueryFilters().AnyAsync(x => x.Code == code, ct))
            {
                ModelState.AddModelError("Funktion.Code", "Diesen Code gibt es schon.");
            }
        }

        if (Text(f.VorgeschlageneRolle) is { } rolle && !await db.Roles.AnyAsync(r => r.Name == rolle, ct))
        {
            ModelState.AddModelError("Funktion.VorgeschlageneRolle", "Unbekannte Rolle.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            FunktionAuswahl = id;
            return Page();
        }

        if (funktion is null)
        {
            funktion = new Funktion { Code = code!, Bezeichnung = "" };
            db.Funktionen.Add(funktion);
        }

        funktion.Bezeichnung = f.Bezeichnung!.Trim();
        funktion.Kurzbezeichnung = Text(f.Kurzbezeichnung);
        funktion.AmtszeitJahre = f.AmtszeitJahre;
        funktion.VorgeschlageneRolle = Text(f.VorgeschlageneRolle);
        funktion.Grundlage = Text(f.Grundlage);
        funktion.Voraussetzungen = Text(f.Voraussetzungen)?.ToUpperInvariant();
        funktion.Abteilungsart = f.Abteilungsart;
        funktion.MitWahl = f.MitWahl;
        funktion.MitBeschluss = f.MitBeschluss;
        funktion.BeschlussDurch = f.MitBeschluss ? Text(f.BeschlussDurch) : null;
        funktion.Sortierung = f.Sortierung;
        funktion.Aktiv = f.Aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Funktion „{funktion.Bezeichnung}“ gespeichert.";
        return RedirectToPage(new { funktion = funktion.Id });
    }

    /// <summary>Kürzel aus der Art plus laufender Nummer, z. B. EA2 für die zweite Einsatzabteilung.</summary>
    private async Task<string> NeuerCodeAsync(AbteilungArt art, CancellationToken ct)
    {
        var praefix = art switch
        {
            AbteilungArt.Einsatz => "EA",
            AbteilungArt.Jugend => "JF",
            AbteilungArt.Kinder => "KF",
            AbteilungArt.Alters => "AA",
            _ => "MU",
        };
        var vorhandene = await db.Abteilungen.IgnoreQueryFilters().Select(a => a.Code).ToListAsync(ct);
        if (!vorhandene.Contains(praefix))
        {
            return praefix;
        }

        var nummer = 2;
        while (vorhandene.Contains($"{praefix}{nummer}"))
        {
            nummer++;
        }

        return $"{praefix}{nummer}";
    }

    /// <summary>
    /// Beide Formulare stehen auf einer Seite; das nicht abgeschickte bindet ohne Präfix leer und meldet Pflichtfelder.
    /// Diese Fehler zählen nicht.
    /// </summary>
    private void NurFormularPruefen(string praefix)
    {
        foreach (var schluessel in ModelState.Keys.Where(k => !k.StartsWith(praefix + ".")).ToList())
        {
            ModelState.Remove(schluessel);
        }
    }

    private static string? Text(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();

    private async Task LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Abteilungen = await db.Abteilungen.AsNoTracking().OrderBy(a => a.Sortierung)
            .Select(a => new AbteilungZeile(a.Id, a.Code, a.Bezeichnung, a.Kurzbezeichnung, a.Art, a.Uebergeordnete!.Bezeichnung, a.Aktiv,
                db.Mitgliedschaften.Count(s => s.AbteilungId == a.Id && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)),
                a.Strasse == null || a.Ort == null ? null : a.Strasse + ", " + a.Plz + " " + a.Ort))
            .ToListAsync(ct);
        Funktionen = await db.Funktionen.AsNoTracking().OrderBy(x => x.Sortierung)
            .Select(x => new FunktionZeile(x.Id, x.Code, x.Bezeichnung, x.AmtszeitJahre, x.VorgeschlageneRolle, x.Aktiv,
                db.FunktionZuordnungen.Count(z => z.FunktionId == x.Id && z.GueltigAb <= heute && (z.GueltigBis == null || z.GueltigBis >= heute)), x.Grundlage))
            .ToListAsync(ct);
        RollenAuswahl = await db.Rollenprofile.AsNoTracking().OrderBy(r => r.Bezeichnung)
            .Select(r => new SelectListItem(r.Bezeichnung, r.Name)).ToListAsync(ct);
        Jugendabteilungen = Abteilungen.Where(a => a.Art == AbteilungArt.Jugend)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToList();
    }
}
