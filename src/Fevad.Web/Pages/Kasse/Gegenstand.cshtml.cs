using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Kassen;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Gegenstand im Bestandsverzeichnis aufnehmen oder ändern (V04), auch aus einer Ausgabe oder einem Gerät der Geräteverwaltung, und den
/// Abgang mit Grund und Erlös vermerken. Nur mit Buchungsrecht für die Kasse.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class GegenstandModel(AppDbContext db, Kassenzugriff zugriff, TimeProvider zeit) : PageModel
{
    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Der gespeicherte Gegenstand; null bei einem neuen.</summary>
    public Vermoegensgegenstand? Gegenstand { get; private set; }

    /// <summary>Ausgabe, aus der aufgenommen wird oder die verknüpft ist.</summary>
    public Buchung? Buchung { get; private set; }

    /// <summary>Gerät, aus dem übernommen wird oder das verknüpft ist.</summary>
    public Geraet? Geraet { get; private set; }

    /// <summary>Eingaben.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Felder eines Gegenstands.</summary>
    public sealed class Eingabe
    {
        /// <summary>Inventarnummer.</summary>
        [StringLength(50), Display(Name = "Inventarnummer")] public string? Inventarnummer { get; set; }

        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bitte die Bezeichnung angeben."), StringLength(200), Display(Name = "Bezeichnung")] public string Bezeichnung { get; set; } = "";

        /// <summary>Tag der Anschaffung.</summary>
        [Required(ErrorMessage = "Bitte das Datum der Anschaffung angeben."), Display(Name = "Anschaffung")] public DateOnly? Anschaffung { get; set; }

        /// <summary>Anschaffungswert.</summary>
        [Required(ErrorMessage = "Bitte den Wert angeben."), Range(0, 10_000_000, ErrorMessage = "Der Wert darf nicht negativ sein."), Display(Name = "Wert (Euro)")]
        public decimal? Wert { get; set; }

        /// <summary>Standort.</summary>
        [StringLength(200), Display(Name = "Standort")] public string? Standort { get; set; }

        /// <summary>Freitext.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }

        /// <summary>Tag des Abgangs.</summary>
        [Display(Name = "Abgang am")] public DateOnly? AbgangAm { get; set; }

        /// <summary>Grund des Abgangs.</summary>
        [StringLength(200), Display(Name = "Grund des Abgangs")] public string? AbgangGrund { get; set; }

        /// <summary>Erlös bei Verkauf.</summary>
        [Range(0, 10_000_000), Display(Name = "Erlös (Euro)")] public decimal? Erloes { get; set; }
    }

    /// <summary>Zeigt das Formular.</summary>
    /// <param name="id">Der Gegenstand; leer für einen neuen.</param>
    /// <param name="kasse">Kasse eines neuen Gegenstands.</param>
    /// <param name="buchung">Ausgabe, aus der aufgenommen wird.</param>
    /// <param name="geraet">Gerät, das übernommen wird.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? kasse, Guid? buchung, Guid? geraet, CancellationToken ct)
    {
        if (await LadenAsync(id, kasse, buchung, geraet, ct) is { } fehler)
        {
            return fehler;
        }

        if (Gegenstand is { } g)
        {
            Formular = new Eingabe
            {
                Inventarnummer = g.Inventarnummer, Bezeichnung = g.Bezeichnung, Anschaffung = g.Anschaffung, Wert = g.Wert, Standort = g.Standort,
                Bemerkung = g.Bemerkung, AbgangAm = g.AbgangAm, AbgangGrund = g.AbgangGrund, Erloes = g.Erloes,
            };
        }
        else
        {
            var jahr = Buchung?.Datum.Year ?? Anzeige.Heute(zeit).Year;
            var anzahl = await db.Vermoegensgegenstaende.IgnoreQueryFilters().CountAsync(v => v.KasseId == Kasse.Id && v.Anschaffung.Year == jahr, ct);
            Formular = new Eingabe
            {
                Inventarnummer = Geraet?.Inventarnummer ?? $"K-{jahr}-{anzahl + 1:000}",
                Bezeichnung = Geraet?.Bezeichnung ?? Buchung?.Text ?? "",
                Anschaffung = Buchung?.Datum ?? Geraet?.IndienstAm ?? Anzeige.Heute(zeit),
                Wert = Buchung?.Betrag ?? Geraet?.Anschaffungspreis,
                Standort = Geraet?.Lagerort,
            };
        }

        return Page();
    }

    /// <summary>Speichert den Gegenstand.</summary>
    /// <param name="id">Der Gegenstand; leer für einen neuen.</param>
    /// <param name="kasse">Kasse eines neuen Gegenstands.</param>
    /// <param name="buchung">Ausgabe, aus der aufgenommen wird.</param>
    /// <param name="geraet">Gerät, das übernommen wird.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(Guid? id, Guid? kasse, Guid? buchung, Guid? geraet, CancellationToken ct)
    {
        if (await LadenAsync(id, kasse, buchung, geraet, ct) is { } fehler)
        {
            return fehler;
        }

        if (Formular.AbgangAm is { } ab && Formular.Anschaffung is { } an && ab < an)
        {
            ModelState.AddModelError("Formular.AbgangAm", "Der Abgang kann nicht vor der Anschaffung liegen.");
        }

        if (Formular.AbgangAm is not null && string.IsNullOrWhiteSpace(Formular.AbgangGrund))
        {
            ModelState.AddModelError("Formular.AbgangGrund", "Bitte den Grund des Abgangs angeben, z. B. „verkauft“ oder „defekt entsorgt“.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var g = id is { } gid ? await db.Vermoegensgegenstaende.FirstAsync(v => v.Id == gid, ct)
            : new Vermoegensgegenstand { KasseId = Kasse.Id, Bezeichnung = "", BuchungId = Buchung?.Id, GeraetId = Geraet?.Id };
        g.Inventarnummer = Leer(Formular.Inventarnummer);
        g.Bezeichnung = Formular.Bezeichnung.Trim();
        g.Anschaffung = Formular.Anschaffung!.Value;
        g.Wert = Formular.Wert!.Value;
        g.Standort = Leer(Formular.Standort);
        g.Bemerkung = Leer(Formular.Bemerkung);
        g.AbgangAm = Formular.AbgangAm;
        g.AbgangGrund = Formular.AbgangAm is null ? null : Leer(Formular.AbgangGrund);
        g.Erloes = Formular.AbgangAm is null ? null : Formular.Erloes;
        if (id is null)
        {
            db.Vermoegensgegenstaende.Add(g);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"„{g.Bezeichnung}“ gespeichert.";
        return RedirectToPage("Bestand", new { kasse = Kasse.Id });
    }

    /// <summary>Entfernt einen versehentlich aufgenommenen Gegenstand; ein echter Abgang wird mit Datum und Grund vermerkt.</summary>
    /// <param name="id">Der Gegenstand.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, null, null, null, ct) is { } fehler)
        {
            return fehler;
        }

        db.Vermoegensgegenstaende.Remove(await db.Vermoegensgegenstaende.FirstAsync(v => v.Id == id, ct));
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"„{Gegenstand!.Bezeichnung}“ aus dem Verzeichnis entfernt.";
        return RedirectToPage("Bestand", new { kasse = Kasse.Id });
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<IActionResult?> LadenAsync(Guid? id, Guid? kasseId, Guid? buchungId, Guid? geraetId, CancellationToken ct)
    {
        if (id is { } gid)
        {
            Gegenstand = await db.Vermoegensgegenstaende.AsNoTracking().FirstOrDefaultAsync(v => v.Id == gid, ct);
            if (Gegenstand is null)
            {
                return NotFound();
            }

            (kasseId, buchungId, geraetId) = (Gegenstand.KasseId, Gegenstand.BuchungId, Gegenstand.GeraetId);
        }

        if (kasseId is null || await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasseId.Value, ct) is not { } kasse)
        {
            return NotFound();
        }

        if (!await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse))
        {
            return Forbid();
        }

        Kasse = kasse;
        if (buchungId is { } bid)
        {
            Buchung = await db.Buchungen.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bid && b.KasseId == kasse.Id, ct);
            if (Buchung is null && id is null)
            {
                return NotFound();
            }
        }

        if (geraetId is { } rid)
        {
            Geraet = await db.Geraete.AsNoTracking().FirstOrDefaultAsync(g => g.Id == rid, ct);
            if (Geraet is null && id is null)
            {
                return NotFound();
            }
        }

        return null;
    }
}
