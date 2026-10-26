using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kostenersatz;

/// <summary>
/// Übersicht der Kostenersatz-Vorgänge eines Jahres (V21): offene Empfehlungen für die Gemeinde, Einsätze ohne Vorgang, bei denen Kostenersatz
/// in Frage kommt (§ 2 Abs. 2 FwG oder als offen markiert), die Forderungen aus den Bescheiden (V22) mit Fälligkeit und Verjährungs-Wiedervorlage
/// und die Zahl der Vorgänge und Beträge je Tatbestand.
/// </summary>
[Authorize(Policy = Berechtigungen.KostenersatzLesen)]
public class IndexModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
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

    /// <summary>Zahl der Vorgänge, festgesetzte und bezahlte Beträge je Tatbestand.</summary>
    public IReadOnlyList<(Kostentatbestand? Tatbestand, int Anzahl, decimal Festgesetzt, decimal Bezahlt)> JeTatbestand { get; private set; } = [];

    /// <summary>Eine Forderung aus einem Bescheid.</summary>
    /// <param name="Bescheid">Der Bescheid mit Schritten.</param>
    /// <param name="EinsatzId">Kennung des Einsatzes.</param>
    /// <param name="Einsatznummer">Einsatznummer.</param>
    /// <param name="Schuldner">Name des Schuldners oder null nach der Löschung.</param>
    /// <param name="VerjaehrtAm">Ende der Zahlungsverjährung.</param>
    public sealed record Forderung(Kostenbescheid Bescheid, Guid EinsatzId, string Einsatznummer, string? Schuldner, DateOnly? VerjaehrtAm);

    /// <summary>Forderungen aus den Bescheiden der Einsätze des Jahres.</summary>
    public IReadOnlyList<Forderung> Forderungen { get; private set; } = [];

    /// <summary>Heutiges Datum für Fälligkeit und Wiedervorlage.</summary>
    public DateOnly Heute { get; private set; }

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
        Heute = Anzeige.Heute(zeit);
        var jahre = await parameter.ZahlAsync(ParameterSchluessel.KostenersatzVerjaehrungJahre, Heute, 5, ct);
        var ids = vorgaenge.Select(v => v.Id).ToList();
        var bescheide = await db.Kostenbescheide.AsNoTracking().Include(b => b.Ereignisse).Include(b => b.Kostenschuldner)
            .Where(b => ids.Contains(b.KostenersatzvorgangId)).AsSplitQuery().ToListAsync(ct);
        var nachVorgang = vorgaenge.ToDictionary(v => v.Id);
        Forderungen = bescheide
            .Select(b => new Forderung(b, nachVorgang[b.KostenersatzvorgangId].EinsatzId, nachVorgang[b.KostenersatzvorgangId].Einsatz!.Einsatznummer,
                b.Kostenschuldner?.Name, b.VerjaehrtAm(jahre)))
            .OrderBy(f => f.Bescheid.Erledigt).ThenBy(f => f.Bescheid.FaelligAm ?? DateOnly.MaxValue).ThenBy(f => f.Einsatznummer)
            .ToList();
        JeTatbestand = vorgaenge.GroupBy(v => v.Tatbestand).OrderBy(g => g.Key is null).ThenBy(g => g.Key)
            .Select(g =>
            {
                var b = bescheide.Where(x => g.Any(v => v.Id == x.KostenersatzvorgangId)).ToList();
                return (g.Key, g.Count(), b.Sum(x => x.Betrag ?? 0), b.Sum(x => x.Bezahlt));
            })
            .ToList();
        OhneVorgang = await db.Einsaetze.AsNoTracking()
            .Where(e => e.Alarmiert >= von && e.Alarmiert < bis && (e.Rechtsgrundlage == Rechtsgrundlage.Para2Abs2 || e.KostenersatzOffen)
                && !db.Kostenersatzvorgaenge.Any(v => v.EinsatzId == e.Id))
            .OrderByDescending(e => e.Alarmiert)
            .Select(e => new Zeile(e.Id, e.Einsatznummer, e.Alarmiert, e.Stichwort, null))
            .ToListAsync(ct);
    }
}
