using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Spenden- und Sponsoringregister einer Kasse (V08): Einträge eines Jahres mit Stand von Annahmebeschluss und Dankschreiben, Summen und
/// die Liste für den Jahresbericht der Gemeinde an die Rechtsaufsicht als CSV. Lesen mit Kassen-Leserecht; bei der Kasse der Jugendfeuerwehr
/// ohne Zugang zu ihr ohne Geber.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class SpendenModel(AppDbContext db, Kassenzugriff zugriff, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Die Kasse.</summary>
    public Domain.Kassen.Kasse Kasse { get; private set; } = null!;

    /// <summary>Angezeigtes Jahr.</summary>
    [BindProperty(SupportsGet = true)] public int Jahr { get; set; }

    /// <summary>Jahre mit Einträgen und das laufende.</summary>
    public IReadOnlyList<int> Jahre { get; private set; } = [];

    /// <summary>Einträge des Jahres.</summary>
    public IReadOnlyList<Zuwendung> Eintraege { get; private set; } = [];

    /// <summary>Der Benutzer darf erfassen.</summary>
    public bool DarfBuchen { get; private set; }

    /// <summary>Der Benutzer darf die Geber sehen.</summary>
    public bool PartnerSichtbar { get; private set; }

    /// <summary>Wertgrenze für den Annahmebeschluss aus dem Parameter.</summary>
    public decimal AnnahmeAb { get; private set; }

    /// <summary>Zeigt das Register.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid kasse, CancellationToken ct) => await LadenAsync(kasse, ct) ? Page() : NotFound();

    /// <summary>Liste für den Jahresbericht an die Rechtsaufsicht (§ 78 Abs. 4 GemO) als CSV.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 404.</returns>
    public async Task<IActionResult> OnGetCsvAsync(Guid kasse, CancellationToken ct)
    {
        if (!await LadenAsync(kasse, ct))
        {
            return NotFound();
        }

        var zeilen = new List<string> { "Eingang;Geber;Art;Betrag oder Wert;Sache;Zweck;Zweckgebunden;Sponsoring;Annahme am;Beschluss" };
        zeilen.AddRange(Eintraege.Select(z => string.Join(';',
            Anzeige.Datum(z.Eingang), Csv(PartnerSichtbar ? z.Geber : null), Anzeige.Text(z.Art), Anzeige.Zahl(z.Betrag), Csv(z.Sache), Csv(z.Zweck),
            z.Zweckgebunden ? "ja" : "nein", z.Sponsoring ? "ja" : "nein", z.AnnahmeAm is { } a ? Anzeige.Datum(a) : "", Csv(z.AnnahmeVerweis))));
        return File(Kassenauswahl.CsvDatei(zeilen), "text/csv", $"Spendenregister {Kasse.Name} {Jahr}.csv");

        static string Csv(string? text) => Kassenauswahl.Csv(text?.ReplaceLineEndings(" "));
    }

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
        AnnahmeAb = await parameter.ZahlAsync(ParameterSchluessel.KasseZuwendungAnnahmeAbEuro, heute, 0, ct);
        var (beginn, ende) = (new DateOnly(Jahr, 1, 1), new DateOnly(Jahr, 12, 31));
        Eintraege = await db.Zuwendungen.AsNoTracking().Include(z => z.Buchung)
            .Where(z => z.KasseId == kasseId && z.Eingang >= beginn && z.Eingang <= ende).OrderBy(z => z.Eingang).ThenBy(z => z.Geber).ToListAsync(ct);
        Jahre = (await db.Zuwendungen.Where(z => z.KasseId == kasseId).Select(z => z.Eingang.Year).Distinct().ToListAsync(ct))
            .Append(heute.Year).Append(Jahr).Distinct().OrderDescending().ToList();
        return true;
    }
}
