using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>Kassenbuch einer Kasse für ein Jahr (V02): Buchungen mit Belegnummer, Filter nach Konto und Position, Festschreiben.</summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class BuchModel(AppDbContext db, Kassenzugriff zugriff, Kassenfuehrung kassen, ParameterLeser parameter, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Zeile des Kassenbuchs.</summary>
    /// <param name="Buchung">Die Buchung samt Konto und Position.</param>
    /// <param name="Belege">Zahl der Belegdateien.</param>
    /// <param name="Storniert">Die Buchung wurde storniert.</param>
    /// <param name="BelegFehlt">Weder Datei noch begründeter Eigenbeleg.</param>
    /// <param name="BeschlussFehlt">Ausgabe über der Grenze ohne Verweis auf einen Beschluss.</param>
    public sealed record Zeile(Buchung Buchung, int Belege, bool Storniert, bool BelegFehlt, bool BeschlussFehlt);

    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Gezeigtes Jahr.</summary>
    [BindProperty(SupportsGet = true)] public int Jahr { get; set; }

    /// <summary>Filter: nur dieses Konto.</summary>
    [BindProperty(SupportsGet = true)] public Guid? Konto { get; set; }

    /// <summary>Filter: nur diese Position.</summary>
    [BindProperty(SupportsGet = true)] public Guid? Position { get; set; }

    /// <summary>Festschreiben bis einschließlich diesem Tag.</summary>
    [BindProperty] public DateOnly? FestschreibenBis { get; set; }

    /// <summary>Buchungen des Jahres nach Filter, nach Belegnummer.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    /// <summary>Konten der Kasse für den Filter.</summary>
    public IReadOnlyList<SelectListItem> Konten { get; private set; } = [];

    /// <summary>Positionen der Kasse für den Filter.</summary>
    public IReadOnlyList<SelectListItem> Positionen { get; private set; } = [];

    /// <summary>Jahre mit Buchungen und das laufende Jahr.</summary>
    public IReadOnlyList<int> Jahre { get; private set; } = [];

    /// <summary>Der Benutzer darf buchen.</summary>
    public bool DarfBuchen { get; private set; }

    /// <summary>Zahlungspartner sichtbar (Jugendkasse nur mit Zugang).</summary>
    public bool PartnerSichtbar { get; private set; }

    /// <summary>Zeigt das Kassenbuch.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid kasse, CancellationToken ct) =>
        await LadenAsync(kasse, ct) ? Page() : NotFound();

    /// <summary>Schreibt alle Buchungen mit Beleg bis zum gewählten Tag fest.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostFestschreibenAsync(Guid kasse, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseBuchen, kasse, ct) is null)
        {
            return Forbid();
        }

        var bis = FestschreibenBis ?? Anzeige.Heute(zeit);
        var (fest, ohne) = await kassen.FestschreibenAsync(kasse, bis, benutzer.Id, ct);
        TempData["Meldung"] = $"{fest} Buchungen bis {Anzeige.Datum(bis)} festgeschrieben."
            + (ohne > 0 ? $" {ohne} Buchungen ohne Beleg bleiben offen." : "");
        return RedirectToPage(new { kasse, Jahr = bis.Year });
    }

    /// <summary>Kassenbuch als CSV für die Kämmerei oder den Rechnungsprüfer.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 404.</returns>
    public async Task<IActionResult> OnGetCsvAsync(Guid kasse, CancellationToken ct)
    {
        if (!await LadenAsync(kasse, ct))
        {
            return NotFound();
        }

        var zeilen = new List<string> { "Beleg;Datum;Art;Konto;Gegenkonto;Position;Partner;Text;Einnahme;Ausgabe;Belegart;Festgeschrieben;Storno von" };
        foreach (var z in Zeilen)
        {
            var b = z.Buchung;
            var ist = Kassenregeln.Ist(b);
            zeilen.Add(string.Join(';',
                b.Kennung, Anzeige.Datum(b.Datum), Anzeige.Text(b.Art), Csv(b.Konto?.Bezeichnung), Csv(b.Gegenkonto?.Bezeichnung),
                Csv(b.Planposition is { } p ? $"{p.Nummer} {p.Bezeichnung}" : null), Csv(PartnerSichtbar ? b.Partner : null), Csv(b.Text),
                b.Art == BuchungArt.Einnahme ? Anzeige.Zahl(ist) : "", b.Art == BuchungArt.Ausgabe ? Anzeige.Zahl(ist) : "",
                Anzeige.Text(b.Belegart), b.FestgeschriebenAm is { } f ? Anzeige.DatumZeit(f) : "", Csv(b.StornoVon?.Kennung)));
        }

        var inhalt = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", zeilen))).ToArray();
        return File(inhalt, "text/csv", $"Kassenbuch {Kasse.Name} {Jahr}.csv");
    }

    private static string Csv(string? text) =>
        string.IsNullOrEmpty(text) ? "" : text.Contains(';') || text.Contains('"') || text.Contains('\n') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;

    private async Task<bool> LadenAsync(Guid kasseId, CancellationToken ct)
    {
        if (await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, kasseId, ct) is not { } kasse)
        {
            return false;
        }

        Kasse = kasse;
        var heute = Anzeige.Heute(zeit);
        if (Jahr == 0)
        {
            Jahr = heute.Year;
        }

        DarfBuchen = await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse);
        PartnerSichtbar = await zugriff.PartnerSichtbarAsync(User, kasse);
        var grenze = await parameter.ZahlAsync(Domain.Gemeinsam.ParameterSchluessel.KasseAusschussbeschlussUeberEuro, heute, 100, ct);

        var abfrage = db.Buchungen.AsNoTracking().Include(b => b.Konto).Include(b => b.Gegenkonto).Include(b => b.Planposition).Include(b => b.StornoVon)
            .Where(b => b.KasseId == kasseId && b.Jahr == Jahr);
        if (Konto is { } konto)
        {
            abfrage = abfrage.Where(b => b.KontoId == konto || b.GegenkontoId == konto);
        }

        if (Position is { } position)
        {
            abfrage = abfrage.Where(b => b.PlanpositionId == position);
        }

        var buchungen = await abfrage.OrderBy(b => b.Belegnummer).Select(b => new { Buchung = b, Dateien = b.Belege.Count }).ToListAsync(ct);
        var storniert = await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasseId && b.StornoVonId != null)
            .Select(b => b.StornoVonId!.Value).ToListAsync(ct);
        Zeilen = buchungen.Select(x => new Zeile(
            x.Buchung, x.Dateien, storniert.Contains(x.Buchung.Id),
            !Kassenregeln.BelegVollstaendig(x.Buchung.Belegart, x.Buchung.EigenbelegBegruendung, x.Dateien),
            x.Buchung.StornoVonId is null && Kassenregeln.BeschlussHinweis(x.Buchung.Art, x.Buchung.Betrag, grenze)
                && x.Buchung.AusschussbeschlussId is null && string.IsNullOrWhiteSpace(x.Buchung.Beschlussverweis))).ToList();
        Konten = await Kassenauswahl.KontenAsync(db, kasseId, [Konto], ct);
        Positionen = await Kassenauswahl.PositionenAsync(db, kasseId, Position, ct);
        Jahre = (await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasseId).Select(b => b.Jahr).Distinct().ToListAsync(ct))
            .Append(heute.Year).Append(Jahr).Distinct().OrderDescending().ToList();
        return true;
    }
}
