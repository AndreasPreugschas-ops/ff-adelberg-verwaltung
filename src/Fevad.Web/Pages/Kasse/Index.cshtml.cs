using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>Übersicht der Kassen (V01): Kontostände, Einnahmen und Ausgaben des Jahres, Stand des Wirtschaftsplans, Offenes.</summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class IndexModel(
    AppDbContext db, Kassenzugriff zugriff, Kassenfuehrung kassen, Steuerauswertung steuer, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Kasse in der Übersicht.</summary>
    /// <param name="Kasse">Die Kasse.</param>
    /// <param name="Konten">Konten mit Bestand heute.</param>
    /// <param name="Einnahmen">Einnahmen des Jahres.</param>
    /// <param name="Ausgaben">Ausgaben des Jahres.</param>
    /// <param name="Plan">Stand des Wirtschaftsplans im Jahr; null, wenn es keinen gibt.</param>
    /// <param name="Offen">Nicht festgeschriebene Buchungen.</param>
    /// <param name="OhneBeleg">Buchungen ohne Beleg.</param>
    /// <param name="Vorbereitet">Vorbereitete, noch nicht gezahlte Ausgaben.</param>
    /// <param name="DarfBuchen">Der Benutzer darf in dieser Kasse buchen.</param>
    public sealed record Zeile(
        Domain.Kassen.Kasse Kasse, IReadOnlyList<(Zahlungsmittelkonto Konto, decimal Bestand)> Konten, decimal Einnahmen, decimal Ausgaben,
        WirtschaftsplanStatus? Plan, int Offen, int OhneBeleg, int Vorbereitet, bool DarfBuchen);

    /// <summary>Die Kassen, die der Benutzer sehen darf.</summary>
    public IReadOnlyList<Zeile> Kassen { get; private set; } = [];

    /// <summary>Laufendes Jahr.</summary>
    public int Jahr { get; private set; }

    /// <summary>Der Benutzer darf Kassen und Konten einrichten.</summary>
    public bool DarfEinrichten { get; private set; }

    /// <summary>Steuerstand des Jahres; nur mit Leserecht für alle Kassen.</summary>
    public Steuerauswertung.Stand? Steuer { get; private set; }

    /// <summary>Lädt die Übersicht.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Jahr = heute.Year;
        DarfEinrichten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.KasseEinrichten)).Succeeded;
        var zeilen = new List<Zeile>();
        foreach (var kasse in await zugriff.KassenAsync(User, Berechtigungen.KasseLesen, ct: ct))
        {
            var jahr = await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasse.Id && b.Jahr == Jahr && b.Art != BuchungArt.Umbuchung)
                .Select(b => new { b.Art, b.Betrag, b.StornoVonId }).ToListAsync(ct);
            var offen = await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasse.Id && b.FestgeschriebenAm == null)
                .Select(b => new { b.Belegart, b.EigenbelegBegruendung, Dateien = b.Belege.Count }).ToListAsync(ct);
            zeilen.Add(new Zeile(
                kasse,
                await kassen.KontostaendeAsync(kasse.Id, heute, ct),
                jahr.Where(b => b.Art == BuchungArt.Einnahme).Sum(b => b.StornoVonId is null ? b.Betrag : -b.Betrag),
                jahr.Where(b => b.Art == BuchungArt.Ausgabe).Sum(b => b.StornoVonId is null ? b.Betrag : -b.Betrag),
                await db.Wirtschaftsplaene.Where(w => w.KasseId == kasse.Id && w.Jahr == Jahr).Select(w => (WirtschaftsplanStatus?)w.Status).FirstOrDefaultAsync(ct),
                offen.Count,
                offen.Count(o => !Kassenregeln.BelegVollstaendig(o.Belegart, o.EigenbelegBegruendung, o.Dateien)),
                await db.Zahlungsanordnungen.CountAsync(a => a.KasseId == kasse.Id && a.Status == AnordnungStatus.Vorbereitet, ct),
                await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse)));
        }

        Kassen = zeilen;
        if (await zugriff.AlleAsync(User, Berechtigungen.KasseLesen))
        {
            Steuer = await steuer.AuswertenAsync(Jahr, ct);
        }
    }
}
