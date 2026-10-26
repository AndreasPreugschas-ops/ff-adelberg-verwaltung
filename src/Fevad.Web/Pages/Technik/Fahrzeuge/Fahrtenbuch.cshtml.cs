using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Fahrzeuge;

/// <summary>
/// Fahrtenbuch und Tankbuch eines Fahrzeugs mit Jahresverbrauch (V63). Fahrer sehen nur Gerätewart und Kommando (Technik prüfen);
/// Hauptamt und Kämmerei sehen Fahrten, Kilometer und Kosten ohne Namen.
/// </summary>
public class FahrtenbuchModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Funkrufname und Typ.</summary>
    public string Fahrzeug { get; private set; } = "";
    /// <summary>Kennung des Fahrzeugs.</summary>
    public Guid FahrzeugId { get; private set; }
    /// <summary>Angezeigtes Jahr.</summary>
    public int Jahr { get; private set; }
    /// <summary>Darf Fahrer sehen.</summary>
    public bool FahrerSichtbar { get; private set; }
    /// <summary>Darf Einträge löschen.</summary>
    public bool DarfLoeschen { get; private set; }
    /// <summary>Fahrten des Jahres.</summary>
    public IReadOnlyList<FahrtZeile> Fahrten { get; private set; } = [];
    /// <summary>Betankungen des Jahres.</summary>
    public IReadOnlyList<TankZeile> Betankungen { get; private set; } = [];
    /// <summary>Verbrauch je Jahr, jüngstes zuerst.</summary>
    public IReadOnlyList<Fahrzeugverbrauch.Jahreswert> Verbrauch { get; private set; } = [];

    /// <summary>Zeile einer Fahrt.</summary>
    /// <param name="Id">Kennung.</param>
    /// <param name="Beginn">Abfahrt.</param>
    /// <param name="Ende">Rückkehr.</param>
    /// <param name="Zweck">Anlass.</param>
    /// <param name="Ziel">Ziel.</param>
    /// <param name="Fahrer">Fahrer, wenn sichtbar.</param>
    /// <param name="KmBeginn">Stand bei Abfahrt.</param>
    /// <param name="KmEnde">Stand bei Rückkehr.</param>
    /// <param name="Strecke">Gefahrene km.</param>
    /// <param name="Bemerkung">Bemerkung.</param>
    public sealed record FahrtZeile(Guid Id, DateTimeOffset Beginn, DateTimeOffset? Ende, Fahrtzweck Zweck, string? Ziel, string? Fahrer, int? KmBeginn, int? KmEnde, int? Strecke, string? Bemerkung);

    /// <summary>Zeile einer Betankung.</summary>
    /// <param name="Id">Kennung.</param>
    /// <param name="Datum">Tag.</param>
    /// <param name="Kraftstoff">Kraftstoff.</param>
    /// <param name="Liter">Liter.</param>
    /// <param name="Betrag">Betrag.</param>
    /// <param name="Kilometerstand">km-Stand.</param>
    /// <param name="Beleg">Beleg.</param>
    /// <param name="GetanktVon">Wer getankt hat, wenn sichtbar.</param>
    public sealed record TankZeile(Guid Id, DateOnly Datum, Kraftstoff Kraftstoff, decimal Liter, decimal? Betrag, int? Kilometerstand, string? Beleg, string? GetanktVon);

    /// <summary>Zeigt das Fahrtenbuch eines Jahres.</summary>
    /// <param name="id">Kennung des Fahrzeugs.</param>
    /// <param name="jahr">Jahr; ohne Angabe das laufende.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, int? jahr, CancellationToken ct)
    {
        var fahrzeug = await db.Fahrzeuge.AsNoTracking().Where(f => f.Id == id).Select(f => new { f.Funkrufname, f.Typ }).FirstOrDefaultAsync(ct);
        if (fahrzeug is null)
        {
            return NotFound();
        }

        FahrzeugId = id;
        Fahrzeug = $"{fahrzeug.Funkrufname} ({fahrzeug.Typ})";
        Jahr = jahr ?? Anzeige.Heute(zeit).Year;
        FahrerSichtbar = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikPruefen)).Succeeded;
        DarfLoeschen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded;
        var (von, bis) = Anzeige.Jahr(Jahr);
        var sichtbar = FahrerSichtbar;

        Fahrten = (await db.Fahrten.AsNoTracking().Where(f => f.FahrzeugId == id && f.Beginn >= von && f.Beginn < bis).OrderBy(f => f.Beginn)
                .Select(f => new { f.Id, f.Beginn, f.Ende, f.Zweck, f.Ziel, Fahrer = sichtbar && f.Fahrer != null ? f.Fahrer.Nachname + ", " + f.Fahrer.Vorname : null, f.KmBeginn, f.KmEnde, f.Kilometer, f.Bemerkung })
                .ToListAsync(ct))
            .Select(f => new FahrtZeile(f.Id, f.Beginn, f.Ende, f.Zweck, f.Ziel, f.Fahrer, f.KmBeginn, f.KmEnde,
                new Fahrt { KmBeginn = f.KmBeginn, KmEnde = f.KmEnde, Kilometer = f.Kilometer }.Strecke, f.Bemerkung))
            .ToList();
        Betankungen = await db.Betankungen.AsNoTracking().Where(b => b.FahrzeugId == id && b.Datum.Year == Jahr).OrderBy(b => b.Datum)
            .Select(b => new TankZeile(b.Id, b.Datum, b.Kraftstoff, b.Liter, b.Betrag, b.Kilometerstand, b.Beleg,
                sichtbar && b.GetanktVon != null ? b.GetanktVon.Nachname + ", " + b.GetanktVon.Vorname : null))
            .ToListAsync(ct);

        var alleFahrten = (await db.Fahrten.AsNoTracking().Where(f => f.FahrzeugId == id).Select(f => new { f.Beginn, f.KmBeginn, f.KmEnde, f.Kilometer }).ToListAsync(ct))
            .Select(f => (Anzeige.LokalesDatum(f.Beginn).Year, new Fahrt { KmBeginn = f.KmBeginn, KmEnde = f.KmEnde, Kilometer = f.Kilometer }.Strecke));
        var alleTankungen = await db.Betankungen.AsNoTracking().Where(b => b.FahrzeugId == id).ToListAsync(ct);
        Verbrauch = Fahrzeugverbrauch.JeJahr(alleFahrten, alleTankungen);
        return Page();
    }

    /// <summary>Löscht eine Fahrt, z. B. eine doppelt erfasste.</summary>
    /// <param name="id">Kennung des Fahrzeugs.</param>
    /// <param name="fahrtId">Kennung der Fahrt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 403.</returns>
    public async Task<IActionResult> OnPostFahrtLoeschenAsync(Guid id, Guid fahrtId, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        if (await db.Fahrten.FirstOrDefaultAsync(f => f.Id == fahrtId && f.FahrzeugId == id, ct) is { } fahrt)
        {
            db.Fahrten.Remove(fahrt);
            await db.SaveChangesAsync(ct);
        }

        TempData["Meldung"] = "Fahrt gelöscht.";
        return RedirectToPage(new { id });
    }

    /// <summary>Löscht eine Betankung.</summary>
    /// <param name="id">Kennung des Fahrzeugs.</param>
    /// <param name="betankungId">Kennung der Betankung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 403.</returns>
    public async Task<IActionResult> OnPostTankenLoeschenAsync(Guid id, Guid betankungId, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.TechnikBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        if (await db.Betankungen.FirstOrDefaultAsync(b => b.Id == betankungId && b.FahrzeugId == id, ct) is { } betankung)
        {
            db.Betankungen.Remove(betankung);
            await db.SaveChangesAsync(ct);
        }

        TempData["Meldung"] = "Betankung gelöscht.";
        return RedirectToPage(new { id });
    }
}
