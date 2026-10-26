using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Benachrichtigungen;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Verwaltung;

/// <summary>
/// Gesamtexport (V70): alle Daten als ZIP mit JSON je Tabelle und den gespeicherten Dateien, als Ausstiegsweg neben dem Datenbank-Dump.
/// Nur für Administratoren; jeder Export steht im Änderungsprotokoll und wird den übrigen Administratoren gemeldet.
/// </summary>
[Authorize(Policy = Berechtigungen.Gesamtexport)]
public class ExportModel(Gesamtexport export, Benachrichtigungsdienst benachrichtigungen, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Bisherige Exporte, neueste zuerst.</summary>
    public IReadOnlyList<(DateTimeOffset Zeitpunkt, string? Konto)> Bisherige { get; private set; } = [];

    /// <summary>Zeigt Hinweise und bisherige Exporte.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => Bisherige = await export.BisherigeAsync(ct);

    /// <summary>Erstellt den Export in einer temporären Datei und liefert ihn aus; die Datei verschwindet nach dem Herunterladen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das ZIP.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var pfad = Path.GetTempFileName();
        var datei = new FileStream(pfad, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            var umfang = await export.SchreibenAsync(datei, ct);
            datei.Position = 0;
            await benachrichtigungen.AnRechtAsync(Berechtigungen.Gesamtexport,
                new(Benachrichtigungsart.Hinweis, $"Gesamtexport erstellt von {User.Identity?.Name}", "/Verwaltung/Export",
                    Text: $"{umfang.Tabellen} Tabellen, {umfang.Zeilen} Zeilen, {umfang.Dateien} Dateien"), benutzer.Id, ct);
            return File(datei, "application/zip", $"FEVAD-Gesamtexport-{Anzeige.Heute(zeit):yyyy-MM-dd}.zip");
        }
        catch
        {
            await datei.DisposeAsync();
            throw;
        }
    }
}
