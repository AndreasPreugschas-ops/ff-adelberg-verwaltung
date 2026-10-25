using System.Text.Json;
using System.Text.Json.Serialization;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Konto;

/// <summary>Selbstauskunft (Art. 15 DSGVO): eigene Daten ansehen und als JSON herunterladen.</summary>
public class MeineDatenModel(Selbstauskunft auskunft, IAktuellerBenutzer benutzer, AppDbContext db, TimeProvider zeit) : PageModel
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Abschnitte bis zu dieser Zeilenzahl sind aufgeklappt.</summary>
    public const int OffenBis = 20;

    /// <summary>Die gespeicherten Daten zur eigenen Person, nach Abschnitten.</summary>
    public IReadOnlyList<Selbstauskunft.Abschnitt> Abschnitte { get; private set; } = [];

    /// <summary>Zeigt die eigenen Daten.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder die Anmeldung.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (benutzer.Id is not { } id)
        {
            return Challenge();
        }

        Abschnitte = await auskunft.LadenAsync(id, ct);
        return Page();
    }

    /// <summary>Liefert die eigenen Daten als JSON-Datei (Art. 20 DSGVO).</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder die Anmeldung.</returns>
    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        if (benutzer.Id is not { } id)
        {
            return Challenge();
        }

        var jetzt = zeit.GetUtcNow();
        var abschnitte = await auskunft.LadenAsync(id, ct);
        var inhalt = new
        {
            Erstellt = jetzt,
            Hinweis = "Auskunft nach Art. 15 DSGVO aus FEVAD. Zeiten in UTC.",
            Abschnitte = abschnitte.Select(a => new
            {
                a.Titel,
                Eintraege = a.Zeilen.Select(z => a.Spalten.Zip(z).ToDictionary(x => x.First, x => x.Second)),
            }),
        };
        var datei = $"fevad-auskunft-{Anzeige.LokalesDatum(jetzt):yyyy-MM-dd}.json";
        return File(JsonSerializer.SerializeToUtf8Bytes(inhalt, Json), "application/json", datei);
    }

    /// <summary>Dieselbe Auskunft als PDF, etwa zum Ausdrucken für die Akte.</summary>
    public async Task<IActionResult> OnGetPdfAsync(CancellationToken ct)
    {
        if (benutzer.Id is not { } id)
        {
            return Challenge();
        }

        var jetzt = zeit.GetUtcNow();
        var o = await db.OrganisationAsync(ct);
        var daten = new SelbstauskunftPdf.Daten(
            new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp),
            User.Identity?.Name ?? "", jetzt,
            (await auskunft.LadenAsync(id, ct))
                .Select(a => new SelbstauskunftPdf.Abschnitt(a.Titel, a.Spalten, a.Zeilen.Select(z => (IReadOnlyList<string>)z.Select(Text).ToList()).ToList()))
                .ToList());
        return File(SelbstauskunftPdf.Erzeugen(daten), "application/pdf", SelbstauskunftPdf.Dateiname(jetzt));
    }

    /// <summary>Text eines Werts für Anzeige und PDF.</summary>
    /// <param name="wert">Der Wert.</param>
    /// <returns>Der Text; „–“ für leere Werte.</returns>
    public static string Text(object? wert) => wert switch
    {
        null => "–",
        bool b => b ? "ja" : "nein",
        DateTimeOffset z => Anzeige.DatumZeit(z),
        DateOnly d => Anzeige.Datum(d),
        Enum e => Anzeige.Text(e),
        decimal d => Anzeige.Zahl(d),
        _ => wert.ToString() ?? "–",
    };
}
