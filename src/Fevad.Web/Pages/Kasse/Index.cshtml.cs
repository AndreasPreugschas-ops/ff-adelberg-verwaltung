using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Übersicht der Kassen (V01, V09): Kontostände, Einnahmen und Ausgaben des Jahres, Stand des Wirtschaftsplans und was ansteht: offene
/// Ausgaben, fehlende Belege, Plan nicht beschlossen, Jahresabschluss und Kassenprüfung des Vorjahres, Spenden ohne Annahmebeschluss,
/// Steuerampel und das Ende der Amtszeit der Rechnungsprüfer.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class IndexModel(
    AppDbContext db, Kassenzugriff zugriff, Kassenfuehrung kassen, Kassenabschluss abschluss, Steuerauswertung steuer, ParameterLeser parameter,
    IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
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
    /// <param name="Faellig">Fällige wiederkehrende Buchungen.</param>
    /// <param name="OffeneUmsaetze">Eingelesene Umsätze der Bank, die noch nicht abgeglichen sind.</param>
    /// <param name="AbschlussFehlt">Das Vorjahr hat Buchungen, ist aber nicht abgeschlossen.</param>
    /// <param name="PruefungFehlt">Das Vorjahr hat Buchungen, aber keine abgeschlossene Kassenprüfung bis zum 31.12.</param>
    /// <param name="OhneAnnahme">Spenden und Sponsoring ohne Annahmebeschluss des Gemeinderats.</param>
    /// <param name="OhneDank">Spenden ohne vermerktes Dankschreiben.</param>
    public sealed record Zeile(
        Domain.Kassen.Kasse Kasse, IReadOnlyList<(Zahlungsmittelkonto Konto, decimal Bestand)> Konten, decimal Einnahmen, decimal Ausgaben,
        WirtschaftsplanStatus? Plan, int Offen, int OhneBeleg, int Vorbereitet, bool DarfBuchen, IReadOnlyList<WiederkehrendeBuchung> Faellig,
        int OffeneUmsaetze, bool AbschlussFehlt, bool PruefungFehlt, int OhneAnnahme, int OhneDank);

    /// <summary>Rechnungsprüfer, deren Amtszeit bald endet.</summary>
    /// <param name="Name">Name des Prüfers.</param>
    /// <param name="Bis">Letzter Tag der Amtszeit.</param>
    public sealed record Amtsende(string Name, DateOnly Bis);

    /// <summary>Die Kassen, die der Benutzer sehen darf.</summary>
    public IReadOnlyList<Zeile> Kassen { get; private set; } = [];

    /// <summary>Laufendes Jahr.</summary>
    public int Jahr { get; private set; }

    /// <summary>Der Benutzer darf Kassen und Konten einrichten.</summary>
    public bool DarfEinrichten { get; private set; }

    /// <summary>Rechnungsprüfer, deren Amtszeit in den nächsten Monaten (Parameter) endet; nur mit Leserecht für alle Kassen.</summary>
    public IReadOnlyList<Amtsende> PrueferAmtsende { get; private set; } = [];

    /// <summary>Kein Rechnungsprüfer ist im Amt; nur mit Leserecht für alle Kassen.</summary>
    public bool KeinePruefer { get; private set; }

    /// <summary>Steuerstand des Jahres; nur mit Leserecht für alle Kassen.</summary>
    public Steuerauswertung.Stand? Steuer { get; private set; }

    /// <summary>Lädt die Übersicht.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Jahr = heute.Year;
        DarfEinrichten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.KasseEinrichten)).Succeeded;
        var vorjahr = Jahr - 1;
        var annahmeAb = await parameter.ZahlAsync(ParameterSchluessel.KasseZuwendungAnnahmeAbEuro, heute, 0, ct);
        var zeilen = new List<Zeile>();
        foreach (var kasse in await zugriff.KassenAsync(User, Berechtigungen.KasseLesen, ct: ct))
        {
            var jahr = await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasse.Id && b.Jahr == Jahr && b.Art != BuchungArt.Umbuchung)
                .Select(b => new { b.Art, b.Betrag, b.StornoVonId }).ToListAsync(ct);
            var vorjahrGebucht = await db.Buchungen.AnyAsync(b => b.KasseId == kasse.Id && b.Jahr == vorjahr, ct);
            var pruefungen = await db.Kassenpruefungen.AsNoTracking().Where(p => p.KasseId == kasse.Id && p.Bis >= new DateOnly(vorjahr, 12, 31)).ToListAsync(ct);
            var spenden = await db.Zuwendungen.AsNoTracking().Where(z => z.KasseId == kasse.Id && (z.AnnahmeAm == null || z.DankschreibenAm == null))
                .Select(z => new { z.AnnahmeAm, z.DankschreibenAm, z.Betrag, z.Sponsoring }).ToListAsync(ct);
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
                await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, kasse),
                await db.WiederkehrendeBuchungen.AsNoTracking()
                    .Where(w => w.KasseId == kasse.Id && w.Aktiv && w.NaechsteFaelligkeit <= heute).OrderBy(w => w.NaechsteFaelligkeit).ToListAsync(ct),
                await db.Kontoumsaetze.CountAsync(u => u.Konto!.KasseId == kasse.Id && u.Status == UmsatzStatus.Offen, ct),
                vorjahrGebucht && !await db.Jahresabschluesse.AnyAsync(j => j.KasseId == kasse.Id && j.Jahr == vorjahr, ct),
                vorjahrGebucht && !Pruefregeln.JahrGeprueft(pruefungen, vorjahr),
                spenden.Count(z => z.AnnahmeAm == null && z.Betrag >= annahmeAb),
                spenden.Count(z => z.DankschreibenAm == null && !z.Sponsoring)));
        }

        Kassen = zeilen;
        if (await zugriff.AlleAsync(User, Berechtigungen.KasseLesen))
        {
            Steuer = await steuer.AuswertenAsync(Jahr, ct);
            var monate = await parameter.ZahlAsync(ParameterSchluessel.KassePrueferHinweisMonate, heute, 3, ct);
            var grenze = heute.AddMonths(monate);
            KeinePruefer = (await abschluss.AmtierendePrueferAsync(heute, ct)).Count == 0;
            PrueferAmtsende = (await db.FunktionZuordnungen.AsNoTracking()
                    .Where(z => z.Funktion!.VorgeschlageneRolle == Rollen.Kassenpruefer && z.GueltigAb <= heute && z.GueltigBis >= heute && z.GueltigBis <= grenze)
                    .Select(z => new { z.Mitglied!.Nachname, z.Mitglied.Vorname, z.GueltigBis }).ToListAsync(ct))
                .OrderBy(z => z.GueltigBis).Select(z => new Amtsende($"{z.Nachname}, {z.Vorname}", z.GueltigBis!.Value)).ToList();
        }
    }
}
