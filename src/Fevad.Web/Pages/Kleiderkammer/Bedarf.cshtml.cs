using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Was nachbestellt werden muss: Mindestbestand, fehlende Grundausstattung und ablaufende Lebensdauer.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class BedarfModel(Kleiderkammerstand stand, ParameterLeser parameter, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Bedarfszeilen, dringendste zuerst.</summary>
    public IReadOnlyList<BedarfZeile> Zeilen { get; private set; } = [];
    /// <summary>Vorlauf in Monaten für ablaufende Lebensdauer.</summary>
    public int VorlaufMonate { get; private set; }
    /// <summary>Der Benutzer darf Beschaffungen anlegen.</summary>
    public bool DarfBeschaffen { get; private set; }
    /// <summary>Maßgeblicher Lieferantenpreis je Artikel.</summary>
    public IReadOnlyDictionary<Guid, ArtikelPreis> Preise { get; private set; } = new Dictionary<Guid, ArtikelPreis>();

    /// <summary>Kosten des noch zu bestellenden Bedarfs mit Lieferantenpreisen.</summary>
    public decimal Summe => Zeilen.Sum(z => z.Bedarf.Offen * (Preise.GetValueOrDefault(z.ArtikelId)?.Preis ?? 0));

    /// <summary>Gewählte Bedarfszeilen als Schlüssel „ArtikelId|Größe“.</summary>
    [BindProperty]
    public List<string> Auswahl { get; set; } = [];

    /// <summary>Schlüssel einer Bedarfszeile für die Auswahl.</summary>
    /// <param name="z">Die Bedarfszeile.</param>
    /// <returns>„ArtikelId|Größe“.</returns>
    public static string Schluessel(BedarfZeile z) => $"{z.ArtikelId}|{z.Groesse}";

    /// <summary>Lädt den Bedarf.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Zeilen = await stand.BedarfAsync(heute, ct);
        Preise = await stand.PreiseAsync(ct);
        VorlaufMonate = await parameter.ZahlAsync(ParameterSchluessel.KleiderkammerAussonderungVorlaufMonate, heute, 6, ct);
        DarfBeschaffen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.BeschaffungBearbeiten)).Succeeded;
    }

    /// <summary>Legt für die gewählten Zeilen Beschaffungen mit Status „Bedarf“ an.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder 403.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.BeschaffungBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        var auswahl = Auswahl
            .Select(s => s.Split('|', 2))
            .Where(t => t.Length == 2 && Guid.TryParse(t[0], out _))
            .Select(t => (Guid.Parse(t[0]), t[1]))
            .ToList();
        var anzahl = await stand.NachbestellungenAnlegenAsync(Anzeige.Heute(zeit), auswahl, ct);
        TempData["Meldung"] = anzahl == 0 ? "Nichts ausgewählt." : $"{anzahl} Beschaffung(en) mit Status „Bedarf“ angelegt.";
        return RedirectToPage();
    }
}
