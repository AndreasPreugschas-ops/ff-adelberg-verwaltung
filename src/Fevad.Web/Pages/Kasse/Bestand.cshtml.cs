using Fevad.Domain.Gemeinsam;
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
/// Bestandsverzeichnis der Kassen (V04, Satzung § 12 Abs. 3): Gegenstände aus dem Sondervermögen mit Anschaffung, Wert, Beleg, Standort und
/// Abgang. Ausgaben ab der Wertgrenze schlägt FEVAD zur Aufnahme vor; Geräte der Geräteverwaltung mit Eigentümer Kameradschaftskasse lassen
/// sich übernehmen. Lesen mit Kassen-Leserecht, pflegen mit Buchungsrecht.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class BestandModel(AppDbContext db, Kassenzugriff zugriff, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Bestandsverzeichnis einer Kasse.</summary>
    /// <param name="Kasse">Die Kasse.</param>
    /// <param name="DarfBuchen">Der Benutzer darf das Verzeichnis pflegen.</param>
    /// <param name="Gegenstaende">Gegenstände, im Bestand zuerst, dann die abgegangenen.</param>
    /// <param name="Vorschlaege">Ausgaben ab der Wertgrenze, die noch nicht im Verzeichnis stehen.</param>
    /// <param name="Geraete">Geräte mit Eigentümer Kameradschaftskasse, die noch nicht im Verzeichnis stehen.</param>
    public sealed record Abschnitt(
        Domain.Kassen.Kasse Kasse, bool DarfBuchen, IReadOnlyList<Vermoegensgegenstand> Gegenstaende, IReadOnlyList<Buchung> Vorschlaege,
        IReadOnlyList<Geraet> Geraete)
    {
        /// <summary>Wert der Gegenstände im Bestand.</summary>
        public decimal Summe => Gegenstaende.Where(g => g.AbgangAm is null).Sum(g => g.Wert);
    }

    /// <summary>Die Kassen mit ihrem Verzeichnis.</summary>
    public IReadOnlyList<Abschnitt> Abschnitte { get; private set; } = [];

    /// <summary>Wertgrenze in Euro (0 = keine Vorschläge).</summary>
    public int Wertgrenze { get; private set; }

    /// <summary>Gewählte Kasse; leer zeigt alle lesbaren.</summary>
    [BindProperty(SupportsGet = true)] public Guid? Kasse { get; set; }

    /// <summary>Zeigt das Verzeichnis.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        return Page();
    }

    /// <summary>Lädt das Verzeichnis einer Kasse als CSV.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die CSV-Datei oder 404.</returns>
    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        if (Kasse is null || Abschnitte.Count != 1)
        {
            return NotFound();
        }

        var a = Abschnitte[0];
        var zeilen = new List<string> { "Inventarnummer;Bezeichnung;Anschaffung;Wert;Beleg;Standort;Gerät;Abgang;Grund;Erlös;Bemerkung" };
        zeilen.AddRange(a.Gegenstaende.Select(g => string.Join(';',
            Kassenauswahl.Csv(g.Inventarnummer), Kassenauswahl.Csv(g.Bezeichnung), Anzeige.Datum(g.Anschaffung), Anzeige.Zahl(g.Wert),
            g.Buchung?.Kennung, Kassenauswahl.Csv(g.Standort), Kassenauswahl.Csv(g.Geraet?.Inventarnummer),
            g.AbgangAm is { } ab ? Anzeige.Datum(ab) : "", Kassenauswahl.Csv(g.AbgangGrund), g.Erloes is { } e ? Anzeige.Zahl(e) : "",
            Kassenauswahl.Csv(g.Bemerkung))));
        return File(Kassenauswahl.CsvDatei(zeilen), "text/csv", $"Bestandsverzeichnis {a.Kasse.Name} {Anzeige.Heute(zeit):yyyy-MM-dd}.csv");
    }

    /// <summary>Markiert eine vorgeschlagene Ausgabe als „kein Gegenstand“, z. B. Verpflegung für ein Fest.</summary>
    /// <param name="buchungId">Die Buchung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostKeinGegenstandAsync(Guid buchungId, CancellationToken ct)
    {
        var buchung = await db.Buchungen.Include(b => b.Kasse).FirstOrDefaultAsync(b => b.Id == buchungId, ct);
        if (buchung?.Kasse is null || await zugriff.LadenAsync(User, Berechtigungen.KasseLesen, buchung.KasseId, ct) is null)
        {
            return NotFound();
        }

        if (!await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, buchung.Kasse))
        {
            return Forbid();
        }

        buchung.KeinGegenstand = true;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Beleg {buchung.Kennung} ist kein Gegenstand für das Bestandsverzeichnis.";
        return RedirectToPage(new { Kasse });
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Wertgrenze = await parameter.ZahlAsync(ParameterSchluessel.KasseBestandsverzeichnisAbWertEuro, heute, 500, ct);
        var kassen = (await zugriff.KassenAsync(User, Berechtigungen.KasseLesen, auchInaktive: true, ct))
            .Where(k => Kasse is null ? k.Aktiv : k.Id == Kasse).ToList();
        var ids = kassen.Select(k => k.Id).ToList();
        var gegenstaende = await db.Vermoegensgegenstaende.AsNoTracking().Include(g => g.Buchung).Include(g => g.Geraet)
            .Where(g => ids.Contains(g.KasseId)).OrderBy(g => g.AbgangAm != null).ThenBy(g => g.Anschaffung).ThenBy(g => g.Bezeichnung).ToListAsync(ct);
        var verknuepft = gegenstaende.Where(g => g.BuchungId is not null).Select(g => g.BuchungId!.Value).ToHashSet();
        var geraeteVerknuepft = gegenstaende.Where(g => g.GeraetId is not null).Select(g => g.GeraetId!.Value).ToHashSet();
        // Vorschläge nur aus dem laufenden Jahr und dem Vorjahr; ältere Ausgaben trägt der Kassenwart bei der Ersteinrichtung selbst nach.
        var buchungen = await db.Buchungen.AsNoTracking()
            .Where(b => ids.Contains(b.KasseId) && b.Jahr >= heute.Year - 1 && b.Art == BuchungArt.Ausgabe)
            .ToListAsync(ct);
        var kameradschaftsgeraete = await db.Geraete.AsNoTracking()
            .Where(g => g.Eigentuemer == Eigentuemer.Kameradschaftskasse && g.AusgesondertAm == null).OrderBy(g => g.Inventarnummer).ToListAsync(ct);

        var abschnitte = new List<Abschnitt>();
        foreach (var k in kassen)
        {
            var darfBuchen = await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, k);
            abschnitte.Add(new Abschnitt(
                k, darfBuchen, gegenstaende.Where(g => g.KasseId == k.Id).ToList(),
                darfBuchen ? Abschlussregeln.Vorschlaege(buchungen.Where(b => b.KasseId == k.Id), Wertgrenze, verknuepft) : [],
                darfBuchen && k.AbteilungId is null ? kameradschaftsgeraete.Where(g => !geraeteVerknuepft.Contains(g.Id)).ToList() : []));
        }

        Abschnitte = abschnitte;
    }
}
