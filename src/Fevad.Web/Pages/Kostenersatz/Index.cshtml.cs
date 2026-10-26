using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kostenersatz;

/// <summary>
/// Übersicht der Kostenersatz-Vorgänge eines Jahres (V21): offene Empfehlungen für die Gemeinde, Einsätze ohne Vorgang, bei denen Kostenersatz
/// in Frage kommt (§ 2 Abs. 2 FwG oder als offen markiert), und die Zahl der Vorgänge je Tatbestand.
/// </summary>
[Authorize(Policy = Berechtigungen.KostenersatzLesen)]
public class IndexModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Zeile der Übersicht.</summary>
    /// <param name="EinsatzId">Kennung des Einsatzes.</param>
    /// <param name="Einsatznummer">Einsatznummer.</param>
    /// <param name="Alarmiert">Alarmzeit (UTC).</param>
    /// <param name="Stichwort">Stichwort.</param>
    /// <param name="Vorgang">Der Vorgang oder null.</param>
    public sealed record Zeile(Guid EinsatzId, string Einsatznummer, DateTimeOffset Alarmiert, string? Stichwort, Kostenersatzvorgang? Vorgang);

    /// <summary>Gewähltes Jahr.</summary>
    public int Jahr { get; private set; }

    /// <summary>Vorgänge des Jahres.</summary>
    public IReadOnlyList<Zeile> Vorgaenge { get; private set; } = [];

    /// <summary>Einsätze des Jahres ohne Vorgang, bei denen Kostenersatz in Frage kommt.</summary>
    public IReadOnlyList<Zeile> OhneVorgang { get; private set; } = [];

    /// <summary>Zahl der Vorgänge je Tatbestand.</summary>
    public IReadOnlyList<(Kostentatbestand? Tatbestand, int Anzahl)> JeTatbestand { get; private set; } = [];

    /// <summary>Lädt die Übersicht.</summary>
    /// <param name="jahr">Jahr; ohne Angabe das laufende.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(int? jahr, CancellationToken ct)
    {
        Jahr = jahr ?? Anzeige.Heute(zeit).Year;
        var (von, bis) = Anzeige.Jahr(Jahr);
        var vorgaenge = await db.Kostenersatzvorgaenge.AsNoTracking().Include(v => v.Einsatz)
            .Where(v => v.Einsatz!.Alarmiert >= von && v.Einsatz.Alarmiert < bis)
            .ToListAsync(ct);
        Vorgaenge = vorgaenge
            .Select(v => new Zeile(v.EinsatzId, v.Einsatz!.Einsatznummer, v.Einsatz.Alarmiert, v.Einsatz.Stichwort, v))
            .OrderBy(z => z.Vorgang!.Stand == KostenersatzStand.Entschieden).ThenByDescending(z => z.Alarmiert)
            .ToList();
        JeTatbestand = vorgaenge.GroupBy(v => v.Tatbestand).OrderBy(g => g.Key is null).ThenBy(g => g.Key).Select(g => (g.Key, g.Count())).ToList();
        OhneVorgang = await db.Einsaetze.AsNoTracking()
            .Where(e => e.Alarmiert >= von && e.Alarmiert < bis && (e.Rechtsgrundlage == Rechtsgrundlage.Para2Abs2 || e.KostenersatzOffen)
                && !db.Kostenersatzvorgaenge.Any(v => v.EinsatzId == e.Id))
            .OrderByDescending(e => e.Alarmiert)
            .Select(e => new Zeile(e.Id, e.Einsatznummer, e.Alarmiert, e.Stichwort, null))
            .ToListAsync(ct);
    }
}
