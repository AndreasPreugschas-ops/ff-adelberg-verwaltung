using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Verwaltung.Rechte;

/// <summary>
/// Rechte einer Rolle als Matrix je Bereich. Schutzschranken: Rechte auf Gesundheitsdaten nur mit ausdrücklicher Bestätigung,
/// Rechte auf Gesundheits- und Jugenddaten erst nach der Freigabe durch eine zweite Person (F18), der Administrator behält die
/// Benutzer- und Systemverwaltung, mitgelieferte Rollen und Rollen mit Konten bleiben bestehen.
/// </summary>
public class BearbeitenModel(
    AppDbContext db,
    RoleManager<IdentityRole<Guid>> rollen,
    Rechteverzeichnis rechte,
    IAktuellerBenutzer aktuell,
    Fevad.Infrastructure.Benachrichtigungen.Benachrichtigungsdienst benachrichtigungen) : PageModel
{
    /// <summary>Rechte, die auf die zweite Freigabe warten; sie erscheinen angehakt mit Hinweis.</summary>
    public IReadOnlySet<string> Wartend { get; private set; } = new HashSet<string>();

    /// <summary>Die bearbeitete Rolle.</summary>
    public Rollenprofil Profil { get; private set; } = null!;
    /// <summary>Anzahl der Konten mit dieser Rolle.</summary>
    public int Konten { get; private set; }
    /// <summary>Abteilungen zur Einschränkung eines Rechts.</summary>
    public IReadOnlyList<(Guid Id, string Bezeichnung, bool Unterabteilung)> Abteilungen { get; private set; } = [];

    /// <summary>Aktuelle (oder nach einem Fehler die eingegebenen) Rechte: Recht → Abteilungen, leer = alle.</summary>
    public Dictionary<string, List<Guid>> Gewaehlt { get; private set; } = [];

    /// <summary>Bezeichnung der Rolle.</summary>
    [BindProperty, Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")]
    public string? Bezeichnung { get; set; }

    /// <summary>Beschreibung, wofür die Rolle gedacht ist.</summary>
    [BindProperty, StringLength(500), Display(Name = "Wofür")]
    public string? Beschreibung { get; set; }

    /// <summary>Bestätigung, dass die Rolle Rechte auf Gesundheitsdaten braucht.</summary>
    [BindProperty, Display(Name = "Ich bestätige, dass diese Rolle Gesundheitsdaten braucht (Art. 9 DSGVO).")]
    public bool GesundheitBestaetigt { get; set; }

    /// <summary>Die Rolle ist die Administratorrolle.</summary>
    public bool IstAdministrator => Profil.Name == Fevad.Infrastructure.Identitaet.Rollen.Administrator;

    /// <summary>Zeigt die Rechte der Rolle.</summary>
    /// <param name="name">Technischer Name der Rolle.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(string name, CancellationToken ct)
    {
        if (!await LadenAsync(name, ct))
        {
            return NotFound();
        }

        Bezeichnung = Profil.Bezeichnung;
        Beschreibung = Profil.Beschreibung;
        Gewaehlt = await db.RollenRechte.AsNoTracking().Where(r => r.Rolle == name).ToDictionaryAsync(r => r.Recht, r => r.Abteilungen, ct);
        var wartend = await OffeneAsync(name, ct);
        foreach (var f in wartend)
        {
            Gewaehlt[f.Recht!] = f.Abteilungen;
        }

        Wartend = wartend.Select(f => f.Recht!).ToHashSet();
        return Page();
    }

    /// <summary>Speichert Bezeichnung, Beschreibung und Rechte der Rolle unter Beachtung der Schutzschranken.</summary>
    /// <param name="name">Technischer Name der Rolle.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, die Seite mit Fehlern oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(string name, CancellationToken ct)
    {
        if (!await LadenAsync(name, ct))
        {
            return NotFound();
        }

        // Formular: "recht" je angehaktem Recht, "umfang:{recht}" = alle|auswahl, "abt:{recht}" je freigegebener Abteilung.
        var abteilungIds = Abteilungen.Select(a => a.Id).ToHashSet();
        Gewaehlt = [];
        foreach (var recht in Berechtigungen.Katalog.Where(r => Request.Form["recht"].Contains(r.Name)))
        {
            var auswahl = recht.Abteilungsbezug && Request.Form[$"umfang:{recht.Name}"] == "auswahl";
            Gewaehlt[recht.Name] = auswahl
                ? Request.Form[$"abt:{recht.Name}"].Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty).Where(abteilungIds.Contains).Distinct().ToList()
                : [];
            if (auswahl && Gewaehlt[recht.Name].Count == 0)
            {
                ModelState.AddModelError(string.Empty, $"„{recht.Bezeichnung}“: mindestens eine Abteilung wählen oder „alle“.");
            }
        }

        if (IstAdministrator)
        {
            foreach (var recht in Berechtigungen.Unentziehbar)
            {
                Gewaehlt[recht] = [];
            }
        }

        var bisher = await db.RollenRechte.Where(r => r.Rolle == name).ToListAsync(ct);
        var neueGesundheit = Berechtigungen.Katalog
            .Where(r => r.Gesundheitsdaten && Gewaehlt.ContainsKey(r.Name) && bisher.All(b => b.Recht != r.Name)).ToList();
        if (neueGesundheit.Count > 0 && !GesundheitBestaetigt)
        {
            ModelState.AddModelError(nameof(GesundheitBestaetigt),
                $"Gesundheitsdaten ({string.Join(", ", neueGesundheit.Select(r => r.Bezeichnung))}): bitte die Bestätigung anhaken.");
        }

        var bezeichnung = Bezeichnung?.Trim();
        if (bezeichnung is not null && await db.Rollenprofile.AnyAsync(p => p.Bezeichnung == bezeichnung && p.Name != name, ct))
        {
            ModelState.AddModelError(nameof(Bezeichnung), "Eine andere Rolle heißt schon so.");
        }

        if (!ModelState.IsValid)
        {
            Wartend = (await OffeneAsync(name, ct)).Select(f => f.Recht!).ToHashSet();
            return Page();
        }

        // Vier-Augen (F18): Was Gesundheits- oder Jugenddaten öffnet, wartet auf eine zweite Person; alles andere gilt sofort.
        var jugend = (await rechte.StandAsync(ct)).Jugendabteilungen;
        var zurueck = Gewaehlt
            .Where(g => Berechtigungen.BrauchtFreigabe(g.Key, bisher.FirstOrDefault(b => b.Recht == g.Key)?.Abteilungen, g.Value, jugend))
            .ToList();
        foreach (var (recht, _) in zurueck)
        {
            if (bisher.FirstOrDefault(b => b.Recht == recht) is { } alt)
            {
                Gewaehlt[recht] = alt.Abteilungen;
            }
            else
            {
                Gewaehlt.Remove(recht);
            }
        }

        // Offene Anträge dieser Rolle ersetzt das Formular: was nicht mehr angehakt oder neu beantragt ist, entfällt.
        db.Rechtefreigaben.RemoveRange(await db.Rechtefreigaben
            .Where(f => f.Art == RechtefreigabeArt.Rollenrecht && f.Rolle == name && f.Freigegeben == null).ToListAsync(ct));
        foreach (var (recht, abteilungen) in zurueck)
        {
            db.Rechtefreigaben.Add(new Rechtefreigabe
            {
                Art = RechtefreigabeArt.Rollenrecht, Rolle = name, Recht = recht, Abteilungen = abteilungen, BeantragtVon = aktuell.Id ?? Guid.Empty,
            });
        }

        foreach (var alt in bisher)
        {
            if (!Gewaehlt.TryGetValue(alt.Recht, out var abteilungen))
            {
                db.RollenRechte.Remove(alt);
            }
            else if (!alt.Abteilungen.ToHashSet().SetEquals(abteilungen))
            {
                alt.Abteilungen = abteilungen;
            }
        }

        foreach (var (recht, abteilungen) in Gewaehlt.Where(g => bisher.All(b => b.Recht != g.Key)))
        {
            db.RollenRechte.Add(new RolleRecht { Rolle = name, Recht = recht, Abteilungen = abteilungen });
        }

        var profil = await db.Rollenprofile.SingleAsync(p => p.Id == Profil.Id, ct);
        profil.Bezeichnung = bezeichnung!;
        profil.Beschreibung = string.IsNullOrWhiteSpace(Beschreibung) ? null : Beschreibung.Trim();
        await db.SaveChangesAsync(ct);
        rechte.Verwerfen();
        if (zurueck.Count > 0)
        {
            await benachrichtigungen.AnRechtAsync(Berechtigungen.RechteFreigeben,
                new(Fevad.Domain.Gemeinsam.Benachrichtigungsart.Aufgabe, $"Rechte der Rolle „{profil.Bezeichnung}“ warten auf deine Freigabe", "/Verwaltung/Rechtefreigaben"),
                aktuell.Id, ct);
        }

        TempData["Meldung"] = zurueck.Count == 0
            ? $"Rechte der Rolle „{profil.Bezeichnung}“ gespeichert. Sie gelten sofort."
            : $"Rechte der Rolle „{profil.Bezeichnung}“ gespeichert. {zurueck.Count} {(zurueck.Count == 1 ? "Recht betrifft" : "Rechte betreffen")} Gesundheits- oder "
              + $"Jugenddaten und {(zurueck.Count == 1 ? "wirkt" : "wirken")} erst, wenn eine zweite Person sie unter „Rechte freigeben“ freigibt.";
        return RedirectToPage(new { name });
    }

    /// <summary>Löscht eine selbst angelegte Rolle ohne Konten.</summary>
    /// <param name="name">Technischer Name der Rolle.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Liste, mit Meldung zurück zur Rolle oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(string name, CancellationToken ct)
    {
        if (!await LadenAsync(name, ct))
        {
            return NotFound();
        }

        if (Profil.Systemrolle || Konten > 0)
        {
            TempData["Meldung"] = Profil.Systemrolle ? "Mitgelieferte Rollen lassen sich nicht löschen." : "Die Rolle ist noch Konten zugeordnet.";
            return RedirectToPage(new { name });
        }

        db.RollenRechte.RemoveRange(await db.RollenRechte.Where(r => r.Rolle == name).ToListAsync(ct));
        db.Rechtefreigaben.RemoveRange(await db.Rechtefreigaben.Where(f => f.Rolle == name && f.Freigegeben == null).ToListAsync(ct));
        db.Rollenprofile.Remove(await db.Rollenprofile.SingleAsync(p => p.Id == Profil.Id, ct));
        await db.SaveChangesAsync(ct);
        if (await rollen.FindByNameAsync(name) is { } rolle)
        {
            await rollen.DeleteAsync(rolle);
        }

        rechte.Verwerfen();
        TempData["Meldung"] = $"Rolle „{Profil.Bezeichnung}“ gelöscht.";
        return RedirectToPage("Index");
    }

    private Task<List<Rechtefreigabe>> OffeneAsync(string name, CancellationToken ct) =>
        db.Rechtefreigaben.AsNoTracking().Where(f => f.Art == RechtefreigabeArt.Rollenrecht && f.Rolle == name && f.Freigegeben == null).ToListAsync(ct);

    private async Task<bool> LadenAsync(string name, CancellationToken ct)
    {
        if (!await rollen.RoleExistsAsync(name))
        {
            return false;
        }

        // Eine Rolle ohne Profil (z. B. von Hand in der Datenbank angelegt) bekommt eines mit ihrem Namen.
        if (await db.Rollenprofile.AsNoTracking().FirstOrDefaultAsync(p => p.Name == name, ct) is not { } profil)
        {
            profil = new Rollenprofil { Name = name, Bezeichnung = name };
            db.Rollenprofile.Add(profil);
            await db.SaveChangesAsync(ct);
            db.Entry(profil).State = EntityState.Detached;
        }

        Profil = profil;
        Konten = await (from ur in db.UserRoles
                        join r in db.Roles on ur.RoleId equals r.Id
                        join b in db.Users on ur.UserId equals b.Id
                        where r.Name == name && b.AnonymisiertAm == null
                        select ur).CountAsync(ct);
        Abteilungen = (await db.Abteilungen.AsNoTracking().OrderBy(a => a.Sortierung).ToListAsync(ct))
            .Select(a => (a.Id, a.Aktiv ? a.Bezeichnung : $"{a.Bezeichnung} (inaktiv)", a.UebergeordneteId is not null))
            .ToList();
        return true;
    }
}
