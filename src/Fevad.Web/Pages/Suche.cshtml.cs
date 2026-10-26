using Fevad.Domain.Dokumente;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages;

/// <summary>
/// Suche über alle Bereiche (V70): Mitglieder, Fahrzeuge, Geräte, Einsätze, Dienste und Dokumente. Jeder Bereich erscheint nur mit dem Recht,
/// das auch seine Liste verlangt; Mitglieder nach dem Datenbereich samt Abschirmung der Jugendfeuerwehr, Dienste nach den planbaren Abteilungen,
/// vertrauliche Dokumente nur mit eigenem Recht.
/// </summary>
[Authorize]
public class SucheModel(AppDbContext db, Datenbereich datenbereich, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Höchstzahl der Treffer je Bereich.</summary>
    public const int ProBereich = 10;

    /// <summary>Ein Treffer.</summary>
    /// <param name="Titel">Hauptzeile.</param>
    /// <param name="Zusatz">Zweite Zeile, z. B. Datum oder Nummer.</param>
    /// <param name="Link">Ziel in FEVAD.</param>
    public sealed record Treffer(string Titel, string? Zusatz, string Link);

    /// <summary>Treffer eines Bereichs.</summary>
    /// <param name="Bereich">Überschrift.</param>
    /// <param name="Liste">Die Treffer, höchstens <see cref="ProBereich"/>.</param>
    public sealed record Gruppe(string Bereich, IReadOnlyList<Treffer> Liste);

    /// <summary>Suchbegriff.</summary>
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    /// <summary>Treffer je Bereich; nur Bereiche mit Treffern.</summary>
    public IReadOnlyList<Gruppe> Gruppen { get; private set; } = [];

    /// <summary>Der Suchbegriff ist lang genug.</summary>
    public bool Gesucht { get; private set; }

    /// <summary>Sucht in allen Bereichen, für die das Konto ein Recht hat.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var begriff = Q?.Trim() ?? "";
        if (begriff.Length < 2)
        {
            return;
        }

        Gesucht = true;
        var muster = $"%{begriff.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        var heute = Anzeige.Heute(zeit);
        var gruppen = new List<Gruppe>();

        if (await DarfAsync(Berechtigungen.MitgliederSehen))
        {
            var sichtbar = await datenbereich.Filtern(db.Mitglieder.AsNoTracking(), User, heute);
            gruppen.Add(new("Mitglieder", (await sichtbar
                    .Where(m => EF.Functions.ILike(m.Nachname, muster) || EF.Functions.ILike(m.Vorname, muster) || EF.Functions.ILike(m.Personalnummer, muster)
                        || EF.Functions.ILike(m.Vorname + " " + m.Nachname, muster))
                    .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname).Take(ProBereich)
                    .Select(m => new { m.Id, m.Nachname, m.Vorname, m.Personalnummer }).ToListAsync(ct))
                .Select(m => new Treffer($"{m.Nachname}, {m.Vorname}", m.Personalnummer, $"/Mitglieder/Akte/{m.Id}")).ToList()));
        }

        if (await DarfAsync(Berechtigungen.TechnikLesen))
        {
            gruppen.Add(new("Fahrzeuge", (await db.Fahrzeuge.AsNoTracking()
                    .Where(f => EF.Functions.ILike(f.Funkrufname, muster) || EF.Functions.ILike(f.Typ, muster) || (f.Kennzeichen != null && EF.Functions.ILike(f.Kennzeichen, muster)))
                    .OrderBy(f => f.Funkrufname).Take(ProBereich).Select(f => new { f.Id, f.Funkrufname, f.Typ, f.Kennzeichen }).ToListAsync(ct))
                .Select(f => new Treffer(f.Funkrufname, string.Join(", ", new[] { f.Typ, f.Kennzeichen }.Where(t => t is not null)), $"/Technik/Fahrzeuge/Details/{f.Id}"))
                .ToList()));
            gruppen.Add(new("Geräte", (await db.Geraete.AsNoTracking()
                    .Where(g => EF.Functions.ILike(g.Inventarnummer, muster) || EF.Functions.ILike(g.Bezeichnung, muster)
                        || (g.Seriennummer != null && EF.Functions.ILike(g.Seriennummer, muster)))
                    .OrderBy(g => g.Inventarnummer).Take(ProBereich).Select(g => new { g.Id, g.Inventarnummer, g.Bezeichnung }).ToListAsync(ct))
                .Select(g => new Treffer(g.Bezeichnung, g.Inventarnummer, $"/Technik/Geraete/Details/{g.Id}")).ToList()));
        }

        if (await DarfAsync(Berechtigungen.EinsaetzeLesen))
        {
            gruppen.Add(new("Einsätze", (await db.Einsaetze.AsNoTracking()
                    .Where(e => EF.Functions.ILike(e.Einsatznummer, muster) || (e.Stichwort != null && EF.Functions.ILike(e.Stichwort, muster))
                        || (e.Ort != null && EF.Functions.ILike(e.Ort, muster)) || (e.StichwortBeschreibung != null && EF.Functions.ILike(e.StichwortBeschreibung, muster)))
                    .OrderByDescending(e => e.Alarmiert).Take(ProBereich).Select(e => new { e.Id, e.Einsatznummer, e.Stichwort, e.Ort, e.Alarmiert }).ToListAsync(ct))
                .Select(e => new Treffer($"{e.Stichwort ?? e.Einsatznummer} {e.Ort}".Trim(), $"{e.Einsatznummer}, {Anzeige.DatumZeit(e.Alarmiert)} Uhr", $"/Einsaetze/Bericht/{e.Id}"))
                .ToList()));
        }

        if (await DarfAsync(Berechtigungen.DiensteBearbeiten))
        {
            var abteilungen = await datenbereich.DienstabteilungenAsync(User);
            gruppen.Add(new("Dienste", (await db.Dienste.AsNoTracking()
                    .Where(d => abteilungen == null || d.Abteilungen.Any(a => abteilungen.Contains(a.Id)))
                    .Where(d => EF.Functions.ILike(d.Titel, muster) || (d.Thema != null && EF.Functions.ILike(d.Thema, muster)) || (d.Ort != null && EF.Functions.ILike(d.Ort, muster)))
                    .OrderByDescending(d => d.Beginn).Take(ProBereich).Select(d => new { d.Id, d.Titel, d.Beginn }).ToListAsync(ct))
                .Select(d => new Treffer(d.Titel, $"{Anzeige.DatumZeit(d.Beginn)} Uhr", $"/Dienste/Bearbeiten/{d.Id}")).ToList()));
        }

        if (await DarfAsync(Berechtigungen.DokumenteLesen))
        {
            var vertraulich = await DarfAsync(Berechtigungen.DokumenteVertraulichLesen);
            gruppen.Add(new("Dokumente", (await db.Dokumente.AsNoTracking()
                    .Where(d => vertraulich || d.Sichtbarkeit == DokumentSichtbarkeit.Alle)
                    .Where(d => EF.Functions.ILike(d.Titel, muster) || (d.Beschreibung != null && EF.Functions.ILike(d.Beschreibung, muster)))
                    .OrderBy(d => d.AufgehobenAm != null).ThenBy(d => d.Titel).Take(ProBereich)
                    .Select(d => new { d.Id, d.Titel, Kategorie = d.Kategorie!.Bezeichnung, d.AufgehobenAm }).ToListAsync(ct))
                .Select(d => new Treffer(d.Titel, d.AufgehobenAm is null ? d.Kategorie : $"{d.Kategorie}, aufgehoben", $"/Dokumente/Detail/{d.Id}")).ToList()));
        }

        Gruppen = gruppen.Where(g => g.Liste.Count > 0).ToList();
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;
}
