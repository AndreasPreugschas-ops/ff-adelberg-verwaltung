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

namespace Fevad.Web.Pages.Mitglieder;

/// <summary>
/// Züge und Gruppen je Abteilung (z. B. Zug 1 mit Gruppe 1 und 2, Jugendgruppe) anlegen und Mitglieder zuordnen; dienen
/// auch als Zielgruppe im Dienstplan. Ohne Abteilung gilt eine Gruppe abteilungsübergreifend (z. B. Führungsgruppe).
/// </summary>
public class GruppenModel(AppDbContext db, TimeProvider zeit, IAuthorizationService autorisierung, Datenbereich datenbereich) : PageModel
{
    /// <summary>Alle Gruppen und Züge im Datenbereich.</summary>
    public IReadOnlyList<Zeile> Gruppen { get; private set; } = [];
    /// <summary>Die gewählte Gruppe.</summary>
    public Gruppe? Auswahl { get; private set; }
    /// <summary>Mitglieder, die der gewählten Gruppe zugeordnet werden können.</summary>
    public IReadOnlyList<Kandidat> Kandidaten { get; private set; } = [];
    /// <summary>Darf Gruppen anlegen und besetzen.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Auswahl der Abteilungen.</summary>
    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];

    /// <summary>Züge als mögliche Übergeordnete; Wert ist die Id, Gruppe ist die Abteilung.</summary>
    public IReadOnlyList<(Guid Id, string Bezeichnung, Guid? AbteilungId)> Zuege { get; private set; } = [];

    /// <summary>Art der neuen Gruppe.</summary>
    [BindProperty] public GruppeArt Art { get; set; }
    /// <summary>Abteilung der neuen Gruppe; leer gilt sie abteilungsübergreifend.</summary>
    [BindProperty] public Guid? AbteilungId { get; set; }
    /// <summary>Übergeordneter Zug.</summary>
    [BindProperty] public Guid? UebergeordneteId { get; set; }

    /// <summary>Bezeichnung der neuen Gruppe.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Bezeichnung fehlt.")]
    [StringLength(100)]
    public string? Bezeichnung { get; set; }

    /// <summary>Eine Zeile der Gruppenliste.</summary>
    /// <param name="Id">Kennung der Gruppe.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Art">Zug oder Gruppe.</param>
    /// <param name="Abteilung">Abteilung.</param>
    /// <param name="Zug">Übergeordneter Zug.</param>
    /// <param name="Aktiv">Die Gruppe ist aktiv.</param>
    /// <param name="Mitglieder">Zahl der Mitglieder.</param>
    public sealed record Zeile(Guid Id, string Bezeichnung, GruppeArt Art, string? Abteilung, string? Zug, bool Aktiv, int Mitglieder);
    /// <summary>Ein Mitglied, das zugeordnet werden kann.</summary>
    /// <param name="Id">Kennung des Mitglieds.</param>
    /// <param name="Name">Name.</param>
    /// <param name="Abteilung">Abteilung.</param>
    /// <param name="Zugeordnet">Ist der Gruppe schon zugeordnet.</param>
    public sealed record Kandidat(Guid Id, string Name, string? Abteilung, bool Zugeordnet);

    /// <summary>Zeigt die Gruppen, auf Wunsch mit einer gewählten Gruppe.</summary>
    /// <param name="id">Kennung der gewählten Gruppe.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(Guid? id, CancellationToken ct) => await LadenAsync(id, ct);

    /// <summary>Stand der Laufbahn; null ohne Recht Laufbahn.Lesen.</summary>
    /// <summary>Stand der Laufbahn; null ohne Recht Laufbahn.Lesen.</summary>
    /// <summary>Stand der Laufbahn; null ohne Recht Laufbahn.Lesen.</summary>
    public async Task<IActionResult> OnPostAnlegenAsync(CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        await GliederungPruefenAsync(null, ct);
        if (!ModelState.IsValid)
        {
            await LadenAsync(null, ct);
            return Page();
        }

        var gruppe = new Gruppe
        {
            Code = "G" + Guid.CreateVersion7().ToString("N")[^8..].ToUpperInvariant(),
            Bezeichnung = Bezeichnung!.Trim(),
            Art = Art,
            AbteilungId = AbteilungId,
            UebergeordneteId = Art == GruppeArt.Gruppe ? UebergeordneteId : null,
            Sortierung = await db.Gruppen.IgnoreQueryFilters().CountAsync(ct) * 10 + 10,
        };
        db.Gruppen.Add(gruppe);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{Anzeige.Text(gruppe.Art)} „{gruppe.Bezeichnung}“ angelegt. Jetzt Mitglieder zuordnen.";
        return RedirectToPage(new { id = gruppe.Id });
    }

    /// <summary>Speichert die angehakten Mitglieder als vollständige Besetzung der Gruppe.</summary>
    public async Task<IActionResult> OnPostZuordnenAsync(Guid id, List<Guid> mitglieder, bool aktiv, CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var gruppe = await db.Gruppen.Include(g => g.Mitglieder).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gruppe is null)
        {
            return NotFound();
        }

        // Bezeichnung und Gliederung werden mitgespeichert; die Bezeichnung ist hier nicht Pflicht.
        ModelState.Remove(nameof(Bezeichnung));
        await GliederungPruefenAsync(id, ct);
        if (!ModelState.IsValid)
        {
            await LadenAsync(id, ct);
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(Bezeichnung))
        {
            gruppe.Bezeichnung = Bezeichnung.Trim();
        }

        gruppe.Art = Art;
        gruppe.AbteilungId = AbteilungId;
        gruppe.UebergeordneteId = Art == GruppeArt.Gruppe ? UebergeordneteId : null;

        var gewaehlt = mitglieder.ToHashSet();
        db.GruppenMitgliedschaften.RemoveRange(gruppe.Mitglieder.Where(m => !gewaehlt.Contains(m.MitgliedId)));
        foreach (var mitgliedId in gewaehlt.Except(gruppe.Mitglieder.Select(m => m.MitgliedId)))
        {
            db.GruppenMitgliedschaften.Add(new GruppenMitgliedschaft { GruppeId = id, MitgliedId = mitgliedId });
        }

        gruppe.Aktiv = aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{Anzeige.Text(gruppe.Art)} „{gruppe.Bezeichnung}“ gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Eine Gruppe gehört zu höchstens einem Zug derselben Abteilung; ein Zug hat keinen übergeordneten Zug.</summary>
    private async Task GliederungPruefenAsync(Guid? eigeneId, CancellationToken ct)
    {
        if (Art != GruppeArt.Gruppe || UebergeordneteId is not { } zugId)
        {
            return;
        }

        var zug = await db.Gruppen.AsNoTracking().FirstOrDefaultAsync(g => g.Id == zugId, ct);
        if (zug is null || zug.Art != GruppeArt.Zug || zug.Id == eigeneId)
        {
            ModelState.AddModelError(nameof(UebergeordneteId), "Bitte einen Zug wählen.");
        }
        else if (zug.AbteilungId != AbteilungId)
        {
            ModelState.AddModelError(nameof(UebergeordneteId), "Der Zug gehört zu einer anderen Abteilung.");
        }
    }

    private async Task<bool> DarfAsync() =>
        (await autorisierung.AuthorizeAsync(User, Berechtigungen.MitgliederBearbeiten)).Succeeded;

    private async Task LadenAsync(Guid? id, CancellationToken ct)
    {
        DarfBearbeiten = await DarfAsync();
        Gruppen = await db.Gruppen.AsNoTracking()
            .OrderBy(g => g.Abteilung == null ? int.MaxValue : g.Abteilung.Sortierung)
            .ThenBy(g => g.Uebergeordnete == null ? g.Sortierung : g.Uebergeordnete.Sortierung)
            .ThenBy(g => g.Art == GruppeArt.Zug ? 0 : 1).ThenBy(g => g.Sortierung).ThenBy(g => g.Bezeichnung)
            .Select(g => new Zeile(g.Id, g.Bezeichnung, g.Art, g.Abteilung!.Bezeichnung, g.Uebergeordnete!.Bezeichnung, g.Aktiv, g.Mitglieder.Count))
            .ToListAsync(ct);
        Abteilungen = await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv).OrderBy(a => a.Sortierung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToListAsync(ct);
        Zuege = (await db.Gruppen.AsNoTracking().Where(g => g.Art == GruppeArt.Zug && g.Aktiv).OrderBy(g => g.Sortierung)
                .Select(g => new { g.Id, g.Bezeichnung, g.AbteilungId }).ToListAsync(ct))
            .Select(g => (g.Id, g.Bezeichnung, g.AbteilungId)).ToList();

        if (id is null)
        {
            return;
        }

        Auswahl = await db.Gruppen.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (Auswahl is null)
        {
            return;
        }

        Art = Auswahl.Art;
        AbteilungId = Auswahl.AbteilungId;
        UebergeordneteId = Auswahl.UebergeordneteId;

        // Mit Abteilung nur deren aktuelle Mitglieder; schon Zugeordnete bleiben sichtbar, um sie abwählen zu können.
        var heute = Anzeige.Heute(zeit);
        var abteilungId = Auswahl.AbteilungId;
        var zugeordnet = await db.GruppenMitgliedschaften.Where(g => g.GruppeId == id).Select(g => g.MitgliedId).ToListAsync(ct);
        Kandidaten = (await (await datenbereich.OhneJugendAsync(db.Mitglieder.AsNoTracking(), User, heute))
                .Where(m => zugeordnet.Contains(m.Id)
                    || m.Mitgliedschaften.Any(s => s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                        && (abteilungId == null || s.AbteilungId == abteilungId)))
                .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
                .Select(m => new
                {
                    m.Id,
                    m.Nachname,
                    m.Vorname,
                    Abteilung = m.Mitgliedschaften
                        .Where(s => s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute))
                        .Select(s => s.Abteilung!.Kurzbezeichnung ?? s.Abteilung.Code)
                        .FirstOrDefault(),
                })
                .ToListAsync(ct))
            .Select(m => new Kandidat(m.Id, $"{m.Nachname}, {m.Vorname}", m.Abteilung, zugeordnet.Contains(m.Id)))
            .ToList();
    }
}
