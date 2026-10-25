using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Steuerübersicht aller Kassen für ein Jahr (V03): Steuerampel gegen die Umsatzgrenze, Umsätze je Kasse und Bereich, Hinweis auf
/// § 2b UStG und Export „Steuergrundlagen“ für die Kämmerei. Nur mit Leserecht für alle Kassen.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class SteuerModel(AppDbContext db, Kassenzugriff zugriff, Steuerauswertung auswertung, TimeProvider zeit) : PageModel
{
    /// <summary>Gezeigtes Jahr.</summary>
    [BindProperty(SupportsGet = true)] public int Jahr { get; set; }

    /// <summary>Auswertung des Jahres.</summary>
    public Steuerauswertung.Stand Stand { get; private set; } = null!;

    /// <summary>Jahre mit Buchungen und das laufende.</summary>
    public IReadOnlyList<int> Jahre { get; private set; } = [];

    /// <summary>Zeigt die Übersicht.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 403.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => await LadenAsync(ct) ? Page() : Forbid();

    /// <summary>
    /// Steuergrundlagen als CSV: alle Buchungen außerhalb des internen Bereichs mit Bereich, Satz, Brutto, Netto und Umsatzsteuer.
    /// Zahlungspartner der Jugendkasse bleiben ohne Zugang zur Jugendfeuerwehr leer.
    /// </summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 403.</returns>
    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        if (!await LadenAsync(ct))
        {
            return Forbid();
        }

        var partnerSichtbar = new Dictionary<Guid, bool>();
        foreach (var k in Stand.Kassen)
        {
            partnerSichtbar[k.Kasse.Id] = await zugriff.PartnerSichtbarAsync(User, k.Kasse);
        }

        var zeilen = new List<string> { "Kasse;Beleg;Datum;Art;Steuerbereich;Steuersatz;Satz %;Brutto;Netto;Umsatzsteuer;Position;Veranstaltung;Partner;Text;Storno von" };
        foreach (var z in Stand.Zeilen)
        {
            var b = z.Buchung;
            zeilen.Add(string.Join(';',
                Kassenauswahl.Csv(b.Kasse?.Name), b.Kennung, Anzeige.Datum(b.Datum), Anzeige.Text(b.Art), Anzeige.Text(b.Steuerbereich), Anzeige.Text(b.Steuersatz),
                Anzeige.Zahl(z.SatzProzent), Anzeige.Zahl(z.Brutto), Anzeige.Zahl(z.Netto), Anzeige.Zahl(z.Umsatzsteuer),
                Kassenauswahl.Csv(b.Planposition is { } p ? $"{p.Nummer} {p.Bezeichnung}" : null), Kassenauswahl.Csv(b.Veranstaltung?.Name),
                Kassenauswahl.Csv(partnerSichtbar.GetValueOrDefault(b.KasseId) ? b.Partner : null), Kassenauswahl.Csv(b.Text), Kassenauswahl.Csv(b.StornoVon?.Kennung)));
        }

        return File(Kassenauswahl.CsvDatei(zeilen), "text/csv", $"Steuergrundlagen Kassen {Jahr}.csv");
    }

    private async Task<bool> LadenAsync(CancellationToken ct)
    {
        if (!await zugriff.AlleAsync(User, Berechtigungen.KasseLesen))
        {
            return false;
        }

        var heute = Anzeige.Heute(zeit);
        if (Jahr == 0)
        {
            Jahr = heute.Year;
        }

        Stand = await auswertung.AuswertenAsync(Jahr, ct);
        Jahre = (await db.Buchungen.AsNoTracking().Select(b => b.Jahr).Distinct().ToListAsync(ct))
            .Append(heute.Year).Append(Jahr).Distinct().OrderDescending().ToList();
        return true;
    }
}
