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
public class OrganisationModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    public sealed record AbteilungZeile(Guid Id, string Code, string Bezeichnung, string? Kurzbezeichnung, AbteilungArt Art, string? Uebergeordnete, bool Aktiv, int Mitglieder);
    public sealed record FunktionZeile(Guid Id, string Code, string Bezeichnung, int? AmtszeitJahre, string? VorgeschlageneRolle, bool Aktiv, int Inhaber);

    public IReadOnlyList<AbteilungZeile> Abteilungen { get; private set; } = [];
    public IReadOnlyList<FunktionZeile> Funktionen { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Jugendabteilungen { get; private set; } = [];
    public static IReadOnlyList<SelectListItem> RollenAuswahl { get; } = Rollen.Alle.Select(r => new SelectListItem(r, r)).ToList();

    /// <summary>Gewählte Abteilung oder Funktion; ohne Auswahl legt das jeweilige Formular neu an.</summary>
    public Guid? AbteilungAuswahl { get; private set; }
    public Guid? FunktionAuswahl { get; private set; }

    /// <summary>Die Art der gewählten Abteilung ist nicht mehr änderbar.</summary>
    public bool ArtFest { get; private set; }

    [BindProperty] public AbteilungFormular Abteilung { get; set; } = new();
    [BindProperty] public FunktionFormular Funktion { get; set; } = new();

    public sealed class AbteilungFormular
    {
        [Display(Name = "Bezeichnung"), Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100)]
        public string? Bezeichnung { get; set; }

        [Display(Name = "Kurzbezeichnung"), StringLength(20)]
        public string? Kurzbezeichnung { get; set; }

        [Display(Name = "Art")]
        public AbteilungArt Art { get; set; }

        [Display(Name = "Gehört zu")]
        public Guid? UebergeordneteId { get; set; }

        [Display(Name = "Sortierung")]
        public int Sortierung { get; set; }

        [Display(Name = "aktiv")]
        public bool Aktiv { get; set; } = true;
    }

    public sealed class FunktionFormular
    {
        [Display(Name = "Code"), StringLength(20)]
        [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Nur Buchstaben, Ziffern und Bindestrich.")]
        public string? Code { get; set; }

        [Display(Name = "Bezeichnung"), Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100)]
        public string? Bezeichnung { get; set; }

        [Display(Name = "Kurzbezeichnung"), StringLength(20)]
        public string? Kurzbezeichnung { get; set; }

        [Display(Name = "Amtszeit in Jahren"), Range(1, 10, ErrorMessage = "Zwischen 1 und 10 Jahren.")]
        public int? AmtszeitJahre { get; set; }

        [Display(Name = "Vorgeschlagene Rolle")]
        public string? VorgeschlageneRolle { get; set; }

        [Display(Name = "Sortierung")]
        public int Sortierung { get; set; }

        [Display(Name = "aktiv")]
        public bool Aktiv { get; set; } = true;
    }

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
            };
        }
        else
        {
            Funktion.Sortierung = Funktionen.Count * 10 + 10;
        }
    }

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
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Abteilung „{abteilung.Bezeichnung}“ gespeichert.";
        return RedirectToPage(new { abteilung = abteilung.Id });
    }

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

        if (Text(f.VorgeschlageneRolle) is { } rolle && !Rollen.Alle.Contains(rolle))
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
                db.Mitgliedschaften.Count(s => s.AbteilungId == a.Id && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute))))
            .ToListAsync(ct);
        Funktionen = await db.Funktionen.AsNoTracking().OrderBy(x => x.Sortierung)
            .Select(x => new FunktionZeile(x.Id, x.Code, x.Bezeichnung, x.AmtszeitJahre, x.VorgeschlageneRolle, x.Aktiv,
                db.FunktionZuordnungen.Count(z => z.FunktionId == x.Id && z.GueltigAb <= heute && (z.GueltigBis == null || z.GueltigBis >= heute))))
            .ToListAsync(ct);
        Jugendabteilungen = Abteilungen.Where(a => a.Art == AbteilungArt.Jugend)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToList();
    }
}
