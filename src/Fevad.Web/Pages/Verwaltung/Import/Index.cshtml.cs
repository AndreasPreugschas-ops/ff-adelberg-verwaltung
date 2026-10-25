using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Import;
using Fevad.Infrastructure.Import;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Verwaltung.Import;

/// <summary>
/// Altdatenimport (V68): CSV- oder XLSX-Datei hochladen, etwa einen Export aus MP-Feuer, und die bisherigen Läufe als Protokoll
/// sehen. Zuordnung, Probelauf und Übernahme folgen auf der Seite des Laufs.
/// </summary>
[RequestSizeLimit(MaxBytes), RequestFormLimits(MultipartBodyLengthLimit = MaxBytes)]
public class IndexModel(AppDbContext db) : PageModel
{
    /// <summary>Technische Obergrenze einer Importdatei; 20 000 Zeilen passen auch als CSV weit darunter.</summary>
    public const long MaxBytes = 20L * 1024 * 1024;

    /// <summary>Kurzfassung eines Laufs für die Liste.</summary>
    /// <param name="Id">Kennung des Laufs.</param>
    /// <param name="Dateiname">Name der Datei.</param>
    /// <param name="Ziel">Was importiert wird.</param>
    /// <param name="Status">Stand des Laufs.</param>
    /// <param name="Angelegt">Zeitpunkt des Hochladens in UTC.</param>
    /// <param name="Zeilen">Datenzeilen beim letzten Probelauf oder der Übernahme.</param>
    /// <param name="Neu">Angelegte Datensätze.</param>
    /// <param name="Ergaenzt">Ergänzte Datensätze.</param>
    /// <param name="Fehlerhaft">Übersprungene Zeilen.</param>
    /// <param name="UebernommenVon">Wer übernommen hat.</param>
    public sealed record Zeile(Guid Id, string Dateiname, Importziel Ziel, ImportlaufStatus Status, DateTimeOffset Angelegt, int Zeilen, int Neu,
        int Ergaenzt, int Fehlerhaft, string? UebernommenVon);

    /// <summary>Was die neue Datei enthält.</summary>
    [BindProperty, Display(Name = "Inhalt der Datei")] public Importziel Ziel { get; set; }
    /// <summary>Die hochgeladene Datei.</summary>
    [BindProperty, Display(Name = "Datei")] public IFormFile? Datei { get; set; }

    /// <summary>Bisherige Läufe, neueste zuerst.</summary>
    public IReadOnlyList<Zeile> Laeufe { get; private set; } = [];

    /// <summary>Zeigt Formular und Protokoll.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    /// <summary>Liest die Datei probeweise ein und legt den Lauf an; danach geht es zur Zuordnung.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiter zum Lauf oder die Seite mit Fehler.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Datei is null || Datei.Length == 0)
        {
            ModelState.AddModelError(nameof(Datei), "Bitte eine Datei auswählen.");
        }
        else if (Datei.Length > MaxBytes)
        {
            ModelState.AddModelError(nameof(Datei), "Die Datei ist größer als 20 MB.");
        }

        byte[] inhalt = [];
        if (ModelState.IsValid)
        {
            using var speicher = new MemoryStream();
            await Datei!.CopyToAsync(speicher, ct);
            inhalt = speicher.ToArray();
            try
            {
                Tabellenleser.Lesen(inhalt, Datei.FileName);
            }
            catch (FormatException ex)
            {
                ModelState.AddModelError(nameof(Datei), ex.Message);
            }
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var name = Path.GetFileName(Datei!.FileName);
        var lauf = new Importlauf { Ziel = Ziel, Status = ImportlaufStatus.Vorbereitet, Dateiname = name.Length > 200 ? name[^200..] : name, Inhalt = inhalt };
        db.Importlaeufe.Add(lauf);
        await db.SaveChangesAsync(ct);
        return RedirectToPage("Lauf", new { id = lauf.Id });
    }

    private async Task LadenAsync(CancellationToken ct) =>
        Laeufe = await db.Importlaeufe.AsNoTracking().OrderByDescending(l => l.ErstelltAm)
            .Select(l => new Zeile(l.Id, l.Dateiname, l.Ziel, l.Status, l.ErstelltAm, l.Zeilen, l.Angelegt, l.Aktualisiert, l.Fehlerhaft, l.UebernommenVon))
            .Take(100)
            .ToListAsync(ct);
}
