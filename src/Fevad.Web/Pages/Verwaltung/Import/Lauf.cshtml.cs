using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Import;
using Fevad.Infrastructure.Import;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Verwaltung.Import;

/// <summary>
/// Ein Importlauf (V68): Spalten zuordnen (auch aus einer gespeicherten Vorlage), Vorschau, Probelauf mit Meldungen je Zeile,
/// Übernahme mit Freigabevermerk oder Verwerfen. Übernommen werden kann erst nach einem Probelauf mit der aktuellen Zuordnung.
/// </summary>
public class LaufModel(AppDbContext db, Importdienst import) : PageModel
{
    /// <summary>Zeilen der Vorschau.</summary>
    public const int VorschauZeilen = 5;

    /// <summary>Der Lauf.</summary>
    public Importlauf Lauf { get; private set; } = null!;
    /// <summary>Die Felder des Ziels.</summary>
    public IReadOnlyList<Zielfeld> Felder { get; private set; } = [];
    /// <summary>Die Spaltenüberschriften der Datei; leer, wenn die Datei schon gelöscht ist.</summary>
    public IReadOnlyList<string> Spalten { get; private set; } = [];
    /// <summary>Die ersten Datenzeilen.</summary>
    public IReadOnlyList<string[]> Vorschau { get; private set; } = [];
    /// <summary>Gespeicherte Vorlagen für dieses Ziel.</summary>
    public IReadOnlyList<Importvorlage> Vorlagen { get; private set; } = [];
    /// <summary>Meldungen des letzten Probelaufs oder der Übernahme.</summary>
    public IReadOnlyList<Importmeldung> Meldungen { get; private set; } = [];

    /// <summary>Feldzuordnung: Zielfeld → Spaltenüberschrift.</summary>
    [BindProperty] public Dictionary<string, string?> Zuordnung { get; set; } = [];
    /// <summary>Name, unter dem die Zuordnung als Vorlage gespeichert wird; leer speichert keine Vorlage.</summary>
    [BindProperty, StringLength(100), Display(Name = "Als Vorlage speichern unter")] public string? VorlageName { get; set; }
    /// <summary>Gewählte Vorlage.</summary>
    [BindProperty] public Guid? VorlageId { get; set; }
    /// <summary>Freigabevermerk für die Übernahme.</summary>
    [BindProperty, StringLength(500), Display(Name = "Freigabe")] public string? Freigabe { get; set; }

    /// <summary>Zeigt den Lauf.</summary>
    /// <param name="id">Kennung des Laufs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) => await LadenAsync(id, ct) ? Page() : NotFound();

    /// <summary>Speichert die Zuordnung, auf Wunsch auch als Vorlage. Ein früherer Probelauf gilt danach nicht mehr.</summary>
    /// <param name="id">Kennung des Laufs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zum Lauf.</returns>
    public async Task<IActionResult> OnPostZuordnenAsync(Guid id, CancellationToken ct)
    {
        if (await VorbereitetAsync(id, ct) is not { } lauf)
        {
            return NotFound();
        }

        await ZuordnungSpeichernAsync(lauf, ct);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = string.IsNullOrWhiteSpace(VorlageName) ? "Zuordnung gespeichert." : $"Zuordnung gespeichert, Vorlage „{VorlageName.Trim()}“ ebenso.";
        return RedirectToPage(new { id });
    }

    /// <summary>Übernimmt die Zuordnung einer gespeicherten Vorlage.</summary>
    /// <param name="id">Kennung des Laufs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zum Lauf.</returns>
    public async Task<IActionResult> OnPostVorlageAsync(Guid id, CancellationToken ct)
    {
        if (await VorbereitetAsync(id, ct) is not { } lauf)
        {
            return NotFound();
        }

        if (await db.Importvorlagen.AsNoTracking().FirstOrDefaultAsync(v => v.Id == VorlageId && v.Ziel == lauf.Ziel, ct) is { } vorlage)
        {
            lauf.Zuordnung = vorlage.Zuordnung;
            lauf.ProbelaufAm = null;
            await db.SaveChangesAsync(ct);
            TempData["Meldung"] = $"Zuordnung aus „{vorlage.Name}“ übernommen.";
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Speichert die Zuordnung und macht den Probelauf: rechnet alles durch und speichert nur das Ergebnis.</summary>
    /// <param name="id">Kennung des Laufs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zum Lauf.</returns>
    public async Task<IActionResult> OnPostProbelaufAsync(Guid id, CancellationToken ct)
    {
        if (await VorbereitetAsync(id, ct) is not { } lauf)
        {
            return NotFound();
        }

        await ZuordnungSpeichernAsync(lauf, ct);
        await db.SaveChangesAsync(ct);
        try
        {
            var e = await import.AusfuehrenAsync(id, probelauf: true, User.Identity?.Name, ct);
            TempData["Meldung"] = $"Probelauf: {e.Zeilen} Zeilen, {e.Angelegt} neu, {e.Aktualisiert} ergänzt, {e.Fehlerhaft} mit Fehlern. Gespeichert wurde nichts.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            TempData["Fehler"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Übernahme der fehlerfreien Zeilen; braucht einen Probelauf mit der aktuellen Zuordnung und den Freigabevermerk.</summary>
    /// <param name="id">Kennung des Laufs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zum Lauf.</returns>
    public async Task<IActionResult> OnPostUebernehmenAsync(Guid id, CancellationToken ct)
    {
        if (await VorbereitetAsync(id, ct) is not { } lauf)
        {
            return NotFound();
        }

        if (lauf.ProbelaufAm is null)
        {
            TempData["Fehler"] = "Bitte zuerst einen Probelauf mit dieser Zuordnung machen.";
            return RedirectToPage(new { id });
        }

        if (string.IsNullOrWhiteSpace(Freigabe))
        {
            TempData["Fehler"] = "Bitte die Freigabe vermerken (wer hat wann zugestimmt).";
            return RedirectToPage(new { id });
        }

        lauf.Freigabe = Freigabe.Trim();
        await db.SaveChangesAsync(ct);
        try
        {
            var e = await import.AusfuehrenAsync(id, probelauf: false, User.Identity?.Name, ct);
            TempData["Meldung"] = $"Übernommen: {e.Angelegt} neu, {e.Aktualisiert} ergänzt, {e.Fehlerhaft} Zeilen übersprungen. Die Datei ist gelöscht.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            TempData["Fehler"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Verwirft den Lauf und löscht die Datei; das Protokoll bleibt.</summary>
    /// <param name="id">Kennung des Laufs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zur Übersicht.</returns>
    public async Task<IActionResult> OnPostVerwerfenAsync(Guid id, CancellationToken ct)
    {
        if (await VorbereitetAsync(id, ct) is not { } lauf)
        {
            return NotFound();
        }

        lauf.Status = ImportlaufStatus.Verworfen;
        lauf.Inhalt = [];
        lauf.Meldungen = Importdienst.MeldungenOhneWerte(lauf.Meldungen);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Import „{lauf.Dateiname}“ verworfen, die Datei ist gelöscht.";
        return RedirectToPage("Index");
    }

    /// <summary>Übernimmt die gebundene Zuordnung in den Lauf und merkt sie auf Wunsch als Vorlage vor; gespeichert wird danach.</summary>
    private async Task ZuordnungSpeichernAsync(Importlauf lauf, CancellationToken ct)
    {
        var felder = Importdienst.Felder(lauf.Ziel).Select(f => f.Schluessel).ToHashSet();
        var json = Importdienst.ZuordnungSchreiben(Zuordnung.Where(z => felder.Contains(z.Key)).ToDictionary());
        lauf.Zuordnung = json;
        lauf.ProbelaufAm = null;
        if (!string.IsNullOrWhiteSpace(VorlageName))
        {
            var name = VorlageName.Trim();
            var vorlage = await db.Importvorlagen.FirstOrDefaultAsync(v => v.Ziel == lauf.Ziel && v.Name == name, ct);
            if (vorlage is null)
            {
                db.Importvorlagen.Add(new Importvorlage { Name = name, Ziel = lauf.Ziel, Zuordnung = json });
            }
            else
            {
                vorlage.Zuordnung = json;
            }
        }

    }

    private Task<Importlauf?> VorbereitetAsync(Guid id, CancellationToken ct) =>
        db.Importlaeufe.FirstOrDefaultAsync(l => l.Id == id && l.Status == ImportlaufStatus.Vorbereitet, ct);

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        if (await db.Importlaeufe.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct) is not { } lauf)
        {
            return false;
        }

        Lauf = lauf;
        Felder = Importdienst.Felder(lauf.Ziel);
        Meldungen = Importdienst.MeldungenLesen(lauf.Meldungen);
        Vorlagen = await db.Importvorlagen.AsNoTracking().Where(v => v.Ziel == lauf.Ziel).OrderBy(v => v.Name).ToListAsync(ct);
        if (lauf.Status == ImportlaufStatus.Vorbereitet && lauf.Inhalt.Length > 0)
        {
            try
            {
                var tabelle = Tabellenleser.Lesen(lauf.Inhalt, lauf.Dateiname);
                Spalten = tabelle.Spalten;
                Vorschau = tabelle.Zeilen.Take(VorschauZeilen).ToList();
            }
            catch (FormatException ex)
            {
                TempData["Fehler"] = ex.Message;
            }
        }

        var gespeichert = Importdienst.ZuordnungLesen(lauf.Zuordnung);
        Zuordnung = (gespeichert.Count > 0 ? gespeichert : Importdienst.Vorschlag(lauf.Ziel, Spalten))
            .Where(z => Spalten.Count == 0 || Spalten.Contains(z.Value))
            .ToDictionary(z => z.Key, z => (string?)z.Value);
        return true;
    }
}
