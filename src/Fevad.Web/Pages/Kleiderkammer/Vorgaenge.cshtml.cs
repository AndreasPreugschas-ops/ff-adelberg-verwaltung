using Fevad.Domain.Kleiderkammer;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kleiderkammer;

/// <summary>Alle Ausgaben und Rückgaben der Kleiderkammer über alle Mitglieder, mit offenen Quittungen.</summary>
[Authorize(Policy = Berechtigungen.KleiderkammerLesen)]
public class VorgaengeModel(AppDbContext db) : PageModel
{
    /// <summary>Nur Ausgaben oder nur Rücknahmen; leer heißt beide.</summary>
    [BindProperty(SupportsGet = true)] public VorgangArt? Art { get; set; }

    /// <summary>Nur Vorgänge ohne Unterschrift oder Bestätigung.</summary>
    [BindProperty(SupportsGet = true)] public bool NurOffene { get; set; }

    /// <summary>Suchtext für Name, Personalnummer oder Quittungsnummer.</summary>
    [BindProperty(SupportsGet = true)] public string? Suche { get; set; }

    /// <summary>Angezeigte Seite der Liste, ab 1.</summary>
    [BindProperty(SupportsGet = true, Name = "Seite")] public int Seitennummer { get; set; } = 1;

    /// <summary>Die Vorgänge der aktuellen Seite.</summary>
    public Seite<Zeile> Zeilen { get; private set; } = Seite<Zeile>.Leer;

    /// <summary>Ein Vorgang in der Liste.</summary>
    /// <param name="Id">Kennung des Vorgangs.</param>
    /// <param name="Nummer">Quittungsnummer.</param>
    /// <param name="Art">Ausgabe oder Rücknahme.</param>
    /// <param name="Zeitpunkt">Zeitpunkt.</param>
    /// <param name="MitgliedId">Das Mitglied.</param>
    /// <param name="Name">Anzeigename „Nachname, Vorname“.</param>
    /// <param name="Stuecke">Anzahl der Stücke im Vorgang.</param>
    /// <param name="QuittiertAm">Unterschrieben oder bestätigt am.</param>
    /// <param name="Digital">In FEVAD bestätigt.</param>
    public sealed record Zeile(Guid Id, string Nummer, VorgangArt Art, DateTimeOffset Zeitpunkt, Guid MitgliedId, string Name, int Stuecke, DateTimeOffset? QuittiertAm, bool Digital);

    /// <summary>Lädt die Vorgänge nach Filter, neueste zuerst.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var abfrage = db.Kleiderkammervorgaenge.AsNoTracking();
        if (Art is { } art)
        {
            abfrage = abfrage.Where(v => v.Art == art);
        }

        if (NurOffene)
        {
            abfrage = abfrage.Where(v => v.QuittiertAm == null);
        }

        if (!string.IsNullOrWhiteSpace(Suche))
        {
            var muster = $"%{Suche.Trim()}%";
            abfrage = abfrage.Where(v => EF.Functions.ILike(v.Nummer, muster) || EF.Functions.ILike(v.Mitglied!.Nachname, muster)
                || EF.Functions.ILike(v.Mitglied!.Vorname, muster) || EF.Functions.ILike(v.Mitglied!.Personalnummer, muster));
        }

        var daten = await abfrage
            .OrderByDescending(v => v.Zeitpunkt)
            .Select(v => new
            {
                v.Id, v.Nummer, v.Art, v.Zeitpunkt, v.MitgliedId, v.Mitglied!.Nachname, v.Mitglied.Vorname, v.QuittiertAm, v.QuittiertDigital,
                Stuecke = v.Art == VorgangArt.Ausgabe
                    ? db.Ausstattungen.Where(a => a.AusgabeVorgangId == v.Id).Sum(a => (int?)a.Menge) ?? 0
                    : db.Ausstattungen.Where(a => a.RuecknahmeVorgangId == v.Id).Sum(a => (int?)a.Menge) ?? 0,
            })
            .ToListAsync(ct);
        Zeilen = daten
            .Select(v => new Zeile(v.Id, v.Nummer, v.Art, v.Zeitpunkt, v.MitgliedId, $"{v.Nachname}, {v.Vorname}", v.Stuecke, v.QuittiertAm, v.QuittiertDigital))
            .ToList()
            .Seite(Seitennummer);
    }
}
