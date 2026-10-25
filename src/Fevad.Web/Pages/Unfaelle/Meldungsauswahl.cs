using Fevad.Domain.Dokumente;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Unfaelle;

/// <summary>Gemeinsame Auswahllisten und Foto-Prüfung für Unfall- und Schadensmeldungen (V40).</summary>
public static class Meldungsauswahl
{
    /// <summary>Aktive Verletzungsarten in Katalogreihenfolge, dazu die schon eingetragene, auch wenn sie inaktiv ist.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="bisher">Bereits gewählte Verletzungsart.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Auswahlliste.</returns>
    public static async Task<IReadOnlyList<SelectListItem>> VerletzungsartenAsync(AppDbContext db, Guid? bisher, CancellationToken ct) =>
        await db.Verletzungsarten.AsNoTracking()
            .Where(v => v.Aktiv || v.Id == bisher)
            .OrderBy(v => v.Sortierung)
            .Select(v => new SelectListItem(v.Bezeichnung, v.Id.ToString()))
            .ToListAsync(ct);

    /// <summary>
    /// Liest hochgeladene Fotos: nur PNG und JPEG, Anzahl und Größe nach den Parametern
    /// <see cref="ParameterSchluessel.MeldungFotosHoechstAnzahl"/> und <see cref="ParameterSchluessel.MeldungFotoHoechstMegabyte"/>.
    /// Fehler landen im ModelState unter <paramref name="feld"/>.
    /// </summary>
    /// <param name="dateien">Hochgeladene Dateien.</param>
    /// <param name="parameter">Parameterleser.</param>
    /// <param name="heute">Stichtag der Parameter.</param>
    /// <param name="modelState">ModelState für Fehlermeldungen.</param>
    /// <param name="feld">Name des Formularfelds.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Fotos ohne Bezug; der Aufrufer hängt sie an Unfall oder Sachschaden.</returns>
    public static async Task<List<Meldungsfoto>> FotosLesenAsync(IReadOnlyList<IFormFile> dateien, ParameterLeser parameter, DateOnly heute,
        ModelStateDictionary modelState, string feld, CancellationToken ct)
    {
        var fotos = new List<Meldungsfoto>();
        var dateienMitInhalt = dateien.Where(d => d.Length > 0).ToList();
        if (dateienMitInhalt.Count == 0)
        {
            return fotos;
        }

        var anzahl = await parameter.ZahlAsync(ParameterSchluessel.MeldungFotosHoechstAnzahl, heute, 5, ct);
        var megabyte = await parameter.ZahlAsync(ParameterSchluessel.MeldungFotoHoechstMegabyte, heute, 8, ct);
        if (dateienMitInhalt.Count > anzahl)
        {
            modelState.AddModelError(feld, $"Höchstens {anzahl} Fotos.");
            return fotos;
        }

        foreach (var datei in dateienMitInhalt)
        {
            var name = Dateitypen.Bereinigen(datei.FileName);
            var typ = Dateitypen.Inhaltstyp(name);
            if (typ is not ("image/png" or "image/jpeg"))
            {
                modelState.AddModelError(feld, $"„{name}“ ist kein Foto (nur JPG oder PNG).");
                continue;
            }

            if (datei.Length > megabyte * 1024L * 1024L)
            {
                modelState.AddModelError(feld, $"„{name}“ ist größer als {megabyte} MB.");
                continue;
            }

            using var speicher = new MemoryStream();
            await datei.CopyToAsync(speicher, ct);
            fotos.Add(new Meldungsfoto { Dateiname = name, Inhaltstyp = typ, Inhalt = speicher.ToArray() });
        }

        return fotos;
    }
}
