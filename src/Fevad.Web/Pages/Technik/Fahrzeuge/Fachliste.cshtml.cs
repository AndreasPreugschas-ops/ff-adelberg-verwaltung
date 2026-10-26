using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Fahrzeuge;

/// <summary>Fachliste eines Fahrzeugs als PDF (V58); ohne ID die Geräte im Gerätehaus, die keinem Fahrzeug zugeordnet sind.</summary>
public class FachlisteModel(AppDbContext db, Pruefstaende pruefstaende, TimeProvider zeit) : PageModel
{
    /// <summary>Liefert das PDF oder 404 bei unbekanntem Fahrzeug.</summary>
    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        string ort;
        if (id is null)
        {
            ort = "Gerätehaus";
        }
        else
        {
            var fahrzeug = await db.Fahrzeuge.AsNoTracking().Where(f => f.Id == id).Select(f => new { f.Funkrufname, f.Typ }).FirstOrDefaultAsync(ct);
            if (fahrzeug is null)
            {
                return NotFound();
            }

            ort = $"{fahrzeug.Funkrufname} ({fahrzeug.Typ})";
        }

        var faecher = await db.Geraete.AsNoTracking()
            .Where(g => g.AusgesondertAm == null && g.FahrzeugId == id)
            .ToDictionaryAsync(g => g.Id, g => g.Lagerort, ct);
        var staende = (await pruefstaende.GeraeteAsync(Anzeige.Heute(zeit), ct: ct)).Where(s => faecher.ContainsKey(s.Id));
        // Mengenpositionen des Beladeplans (V59) stehen ohne Inventarnummer mit ihrer Soll-Menge in der Liste.
        var mengen = id is null ? [] : await db.Beladepositionen.AsNoTracking()
            .Where(p => p.FahrzeugId == id && p.MaterialId == null && p.GeraeteartId == null)
            .OrderBy(p => p.Sortierung)
            .Select(p => new FachlistePdf.Zeile(p.Fach, $"Soll {p.Menge}", p.Bezeichnung, null, ""))
            .ToListAsync(ct);
        var o = await db.OrganisationAsync(ct);
        var daten = new FachlistePdf.Daten(
            new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp), ort,
            Anzeige.Lokal(zeit.GetUtcNow()).DateTime,
            staende.Select(s => new FachlistePdf.Zeile(faecher[s.Id], s.Nummer, s.Bezeichnung, s.NaechsterTermin, Anzeige.Text(s.Zustand))).Concat(mengen).ToList());
        return File(FachlistePdf.Erzeugen(daten), "application/pdf", FachlistePdf.Dateiname(daten));
    }
}
