using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Druckbare Quittung über Ausgabe oder Rücknahme, mit Feldern für beide Unterschriften.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class QuittungModel(AppDbContext db, Kleiderkammerbuchungen buchungen, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Der Vorgang (Ausgabe oder Rücknahme).</summary>
    public Kleiderkammervorgang Vorgang { get; private set; } = null!;
    /// <summary>Name des Mitglieds.</summary>
    public string Name { get; private set; } = "";
    /// <summary>Personalnummer des Mitglieds.</summary>
    public string Personalnummer { get; private set; } = "";
    /// <summary>Positionen des Vorgangs.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Der Benutzer darf den Eingang der Unterschrift vermerken.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Eine Position auf der Quittung.</summary>
    /// <param name="Artikel">Bezeichnung des Artikels.</param>
    /// <param name="Groesse">Größe.</param>
    /// <param name="Menge">Menge.</param>
    /// <param name="Inventarnummer">Inventarnummer bei Einzelstücken.</param>
    /// <param name="Seriennummer">Seriennummer bei Einzelstücken.</param>
    /// <param name="Zustand">Bei Rücknahme: einlagern oder aussondern.</param>
    /// <param name="Ordnungsgemaess">Bei Rücknahme: in ordnungsgemäßem Zustand.</param>
    /// <param name="Maengel">Bei Rücknahme: festgestellte Mängel.</param>
    public sealed record Zeile(string Artikel, string Groesse, int Menge, string? Inventarnummer, string? Seriennummer, RuecknahmeZustand? Zustand,
        bool? Ordnungsgemaess, string? Maengel);

    /// <summary>Zeigt die Quittung.</summary>
    /// <param name="id">Kennung des Vorgangs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await LadenAsync(id, ct) ? Page() : NotFound();

    /// <summary>Ausgabe- bzw. Rückgabebestätigung als PDF zum Unterschreiben.</summary>
    public async Task<IActionResult> OnGetPdfAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        var organisation = await db.OrganisationAsync(ct);
        var daten = new QuittungPdf.Daten(
            new AntragPdf.Kopfdaten(organisation.FeuerwehrName, organisation.FeuerwehrAnschrift, null, null, organisation.Logo, organisation.LogoInhaltstyp),
            organisation.GemeindeName, Vorgang.Nummer, Vorgang.Art, Vorgang.Zeitpunkt, Name, Personalnummer,
            Zeilen.Select(z => new QuittungPdf.Position(z.Inventarnummer, z.Artikel, z.Groesse, z.Menge, z.Seriennummer, z.Ordnungsgemaess, z.Maengel)).ToList(),
            Vorgang.Bemerkung);
        return File(QuittungPdf.Erzeugen(daten), "application/pdf", QuittungPdf.Dateiname(daten));
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var vorgang = await db.Kleiderkammervorgaenge.AsNoTracking().Include(v => v.Mitglied).FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vorgang is null)
        {
            return false;
        }

        Vorgang = vorgang;
        Name = $"{vorgang.Mitglied!.Vorname} {vorgang.Mitglied.Nachname}";
        Personalnummer = vorgang.Mitglied.Personalnummer;
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded;

        var abfrage = vorgang.Art == VorgangArt.Ausgabe
            ? db.Ausstattungen.Where(a => a.AusgabeVorgangId == id)
            : db.Ausstattungen.Where(a => a.RuecknahmeVorgangId == id);
        var zeilen = await abfrage.AsNoTracking()
            .OrderBy(a => a.Artikel!.Sortierung).ThenBy(a => a.Groesse)
            .Select(a => new { Artikel = a.Artikel!.Bezeichnung, a.Groesse, a.Menge, a.Geraet!.Inventarnummer, a.Geraet.Seriennummer, a.Zustand, a.RueckgabeOrdnungsgemaess, a.RueckgabeMaengel })
            .ToListAsync(ct);

        // Teilrückgaben teilen Zeilen; auf der Ausgabequittung erscheinen sie wieder zusammengefasst.
        Zeilen = zeilen
            .GroupBy(a => vorgang.Art == VorgangArt.Ausgabe
                ? (a.Artikel, a.Groesse, a.Inventarnummer, a.Seriennummer, Zustand: (RuecknahmeZustand?)null, Ordnungsgemaess: (bool?)null, Maengel: (string?)null)
                : (a.Artikel, a.Groesse, a.Inventarnummer, a.Seriennummer, a.Zustand, Ordnungsgemaess: a.RueckgabeOrdnungsgemaess, Maengel: a.RueckgabeMaengel))
            .Select(g => new Zeile(g.Key.Artikel, g.Key.Groesse, g.Sum(a => a.Menge), g.Key.Inventarnummer, g.Key.Seriennummer, g.Key.Zustand,
                g.Key.Ordnungsgemaess, g.Key.Maengel))
            .ToList();
        return true;
    }

    /// <summary>Vermerkt, dass die unterschriebene Quittung vorliegt.</summary>
    /// <param name="id">Kennung des Vorgangs.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Quittung oder 403.</returns>
    public async Task<IActionResult> OnPostQuittiertAsync(Guid id, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.KleiderkammerBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        await buchungen.QuittiertAsync(id, ct);
        TempData["Meldung"] = "Unterschriebene Quittung vermerkt.";
        return RedirectToPage(new { id });
    }
}
