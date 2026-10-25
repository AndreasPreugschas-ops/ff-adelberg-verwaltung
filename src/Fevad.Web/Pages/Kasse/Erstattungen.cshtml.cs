using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Auslagenerstattungen aller lesbaren Kassen (V07): offene Erstattungen mit Überweisungsdatei für Kassenwart und Kommandant, und die
/// Jahresliste je Person mit Personalnummer (Schlüssel der Kämmerei) und Hinweis über der Ehrenamtspauschale.
/// </summary>
[Authorize(Policy = Berechtigungen.KasseSehen)]
public class ErstattungenModel(AppDbContext db, Kassenzugriff zugriff, ParameterLeser parameter, IAuthorizationService autorisierung, TimeProvider zeit)
    : PageModel
{
    /// <summary>Eine offene Erstattung.</summary>
    /// <param name="Anordnung">Die vorbereitete Ausgabe mit Kasse und Mitglied.</param>
    /// <param name="NameSichtbar">Name und Personalnummer dürfen gezeigt werden.</param>
    /// <param name="MitIban">Für das Mitglied ist eine Bankverbindung hinterlegt.</param>
    /// <param name="DarfBuchen">Der Benutzer darf in dieser Kasse zahlen und buchen.</param>
    public sealed record Offen(Zahlungsanordnung Anordnung, bool NameSichtbar, bool MitIban, bool DarfBuchen);

    /// <summary>Summe einer Person im Jahr.</summary>
    /// <param name="Personalnummer">Personalnummer.</param>
    /// <param name="Name">Name.</param>
    /// <param name="Anzahl">Zahl der gezahlten Erstattungen.</param>
    /// <param name="Summe">Gezahlte Summe in Euro.</param>
    /// <param name="Kassen">Namen der zahlenden Kassen.</param>
    public sealed record Person(string Personalnummer, string Name, int Anzahl, decimal Summe, string Kassen);

    /// <summary>Gezeigtes Jahr der Jahresliste.</summary>
    [BindProperty(SupportsGet = true)] public int Jahr { get; set; }

    /// <summary>Offene Erstattungen, älteste zuerst.</summary>
    public IReadOnlyList<Offen> Offene { get; private set; } = [];

    /// <summary>Gezahlte Erstattungen des Jahres je Person, höchste Summe zuerst.</summary>
    public IReadOnlyList<Person> Jahresliste { get; private set; } = [];

    /// <summary>Ehrenamtspauschale des Jahres in Euro.</summary>
    public int Ehrenamtspauschale { get; private set; }

    /// <summary>Erstattungen aus Kassen der Jugendfeuerwehr fehlen in der Jahresliste, weil der Zugang fehlt.</summary>
    public bool JugendAusgeblendet { get; private set; }

    /// <summary>Der Benutzer darf Bankverbindungen nutzen und die Überweisungsdatei erzeugen.</summary>
    public bool DarfUeberweisen { get; private set; }

    /// <summary>Girokonten mit IBAN der Kassen, in denen der Benutzer bucht.</summary>
    public IReadOnlyList<SelectListItem> Konten { get; private set; } = [];

    /// <summary>Jahre zur Auswahl.</summary>
    public IReadOnlyList<int> Jahre { get; private set; } = [];

    /// <summary>Zeigt offene Erstattungen und Jahresliste.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        return Page();
    }

    /// <summary>Jahresliste als CSV mit Personalnummer für die Kämmerei.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei.</returns>
    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        var zeilen = new List<string> { "Personalnummer;Name;Anzahl;Summe;Kassen;über Ehrenamtspauschale" };
        zeilen.AddRange(Jahresliste.Select(p => string.Join(';', Kassenauswahl.Csv(p.Personalnummer), Kassenauswahl.Csv(p.Name), p.Anzahl,
            Anzeige.Zahl(p.Summe), Kassenauswahl.Csv(p.Kassen), Ehrenamtspauschale > 0 && p.Summe > Ehrenamtspauschale ? "ja" : "")));
        return File(Kassenauswahl.CsvDatei(zeilen), "text/csv", $"Auslagenerstattungen {Jahr}.csv");
    }

    /// <summary>
    /// SEPA-Überweisungsdatei für die gewählten offenen Erstattungen einer Kasse. Auftraggeber und Tag werden nicht gespeichert; gebucht
    /// wird jede Erstattung danach wie gewohnt, der Bankimport ordnet sie über die Kennung zu.
    /// </summary>
    /// <param name="auswahl">Gewählte Erstattungen.</param>
    /// <param name="kontoId">Belastetes Girokonto.</param>
    /// <param name="auftraggeber">Name des Kontoinhabers.</param>
    /// <param name="ausfuehrung">Ausführungstag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei, Weiterleitung mit Fehler oder 403.</returns>
    public async Task<IActionResult> OnPostUeberweisungAsync(List<Guid> auswahl, Guid? kontoId, string? auftraggeber, DateOnly? ausfuehrung, CancellationToken ct)
    {
        await LadenAsync(ct);
        if (!DarfUeberweisen)
        {
            return Forbid();
        }

        var konto = kontoId is { } kid && Konten.Any(k => k.Value == kid.ToString())
            ? await db.Zahlungsmittelkonten.AsNoTracking().FirstOrDefaultAsync(k => k.Id == kid, ct) : null;
        if (konto is null)
        {
            TempData["Fehler"] = "Bitte das Girokonto der Kasse wählen; es braucht eine IBAN (Kassen einrichten).";
            return RedirectToPage(new { Jahr });
        }

        var gewaehlt = Offene.Where(o => auswahl.Contains(o.Anordnung.Id) && o.Anordnung.Mitglied is not null).ToList();
        if (gewaehlt.Count == 0)
        {
            TempData["Fehler"] = "Bitte mindestens eine Erstattung wählen.";
            return RedirectToPage(new { Jahr });
        }

        if (gewaehlt.Any(o => o.Anordnung.KasseId != konto.KasseId))
        {
            TempData["Fehler"] = "Eine Überweisungsdatei gilt für ein Konto; bitte nur Erstattungen dieser Kasse wählen.";
            return RedirectToPage(new { Jahr });
        }

        var mitglieder = gewaehlt.Select(o => o.Anordnung.MitgliedId!.Value).ToList();
        var banken = await db.Bankverbindungen.AsNoTracking().Where(b => mitglieder.Contains(b.MitgliedId)).ToDictionaryAsync(b => b.MitgliedId, ct);
        var ohne = gewaehlt.Where(o => !banken.ContainsKey(o.Anordnung.MitgliedId!.Value)).ToList();
        if (ohne.Count > 0)
        {
            TempData["Fehler"] = $"Ohne Bankverbindung: {string.Join(", ", ohne.Select(o => o.Anordnung.Mitglied!.Anzeigename))}. Bitte bar zahlen oder die IBAN erfassen.";
            return RedirectToPage(new { Jahr });
        }

        var posten = gewaehlt.Select(o =>
        {
            var a = o.Anordnung;
            var bank = banken[a.MitgliedId!.Value];
            return new SepaUeberweisung.Posten(bank.Kontoinhaber ?? $"{a.Mitglied!.Vorname} {a.Mitglied.Nachname}", bank.Iban, a.Betrag,
                $"Auslagenerstattung {a.Zweck} PNr {a.Mitglied!.Personalnummer}", SepaUeberweisung.Referenz(a.Mitglied.Personalnummer, a.Id));
        }).ToList();
        try
        {
            var datei = SepaUeberweisung.Erzeugen(posten, auftraggeber ?? "", konto.Iban ?? "", ausfuehrung ?? Anzeige.Heute(zeit), zeit.GetUtcNow());
            return File(datei, "application/xml", $"Auslagenerstattung_{Anzeige.Heute(zeit):yyyy-MM-dd}.xml");
        }
        catch (InvalidOperationException fehler)
        {
            TempData["Fehler"] = fehler.Message;
            return RedirectToPage(new { Jahr });
        }
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        if (Jahr == 0)
        {
            Jahr = heute.Year;
        }

        var kassen = await zugriff.KassenAsync(User, Berechtigungen.KasseLesen, auchInaktive: true, ct: ct);
        var sichtbar = new Dictionary<Guid, bool>();
        var buchen = new Dictionary<Guid, bool>();
        foreach (var k in kassen)
        {
            sichtbar[k.Id] = await zugriff.PartnerSichtbarAsync(User, k);
            buchen[k.Id] = await zugriff.DarfAsync(User, Berechtigungen.KasseBuchen, k);
        }

        DarfUeberweisen = buchen.Values.Any(b => b) && (await autorisierung.AuthorizeAsync(User, Berechtigungen.KasseBankdaten)).Succeeded;
        var ids = kassen.Select(k => k.Id).ToList();
        var erstattungen = db.Zahlungsanordnungen.AsNoTracking().Include(a => a.Kasse).Include(a => a.Mitglied)
            .Where(a => a.Art == AnordnungArt.Auslagenerstattung && ids.Contains(a.KasseId));

        var offen = await erstattungen.Include(a => a.Belege).Where(a => a.Status == AnordnungStatus.Vorbereitet).OrderBy(a => a.ErstelltAm).ToListAsync(ct);
        var mitIban = DarfUeberweisen
            ? (await db.Bankverbindungen.AsNoTracking().Select(b => b.MitgliedId).ToListAsync(ct)).ToHashSet()
            : [];
        Offene = offen.Select(a => new Offen(a, sichtbar[a.KasseId], mitIban.Contains(a.MitgliedId ?? Guid.Empty), buchen[a.KasseId])).ToList();

        var gezahlt = await erstattungen.Where(a => a.Status == AnordnungStatus.Gezahlt)
            .Join(db.Buchungen, a => a.BuchungId, b => b.Id, (a, b) => new { a, b.Datum })
            .Where(x => x.Datum.Year == Jahr)
            .Select(x => x.a).ToListAsync(ct);
        JugendAusgeblendet = gezahlt.Any(a => !sichtbar[a.KasseId]) || offen.Any(a => !sichtbar[a.KasseId]);
        Jahresliste = gezahlt.Where(a => sichtbar[a.KasseId] && a.Mitglied is not null)
            .GroupBy(a => a.MitgliedId)
            .Select(g => new Person(g.First().Mitglied!.Personalnummer, g.First().Mitglied!.Anzeigename, g.Count(), g.Sum(a => a.Betrag),
                string.Join(", ", g.Select(a => a.Kasse!.Name).Distinct().Order())))
            .OrderByDescending(p => p.Summe).ThenBy(p => p.Name).ToList();
        Ehrenamtspauschale = await parameter.ZahlAsync(ParameterSchluessel.EhrenamtspauschaleEuro, new DateOnly(Jahr, 12, 31), 0, ct);

        var buchbar = buchen.Where(b => b.Value).Select(b => b.Key).ToList();
        Konten = DarfUeberweisen
            ? await db.Zahlungsmittelkonten.AsNoTracking().Include(k => k.Kasse)
                .Where(k => buchbar.Contains(k.KasseId) && k.Aktiv && k.Art == KontoArt.Girokonto && k.Iban != null)
                .OrderBy(k => k.Kasse!.Sortierung).ThenBy(k => k.Sortierung)
                .Select(k => new SelectListItem($"{k.Kasse!.Name}: {k.Bezeichnung}", k.Id.ToString())).ToListAsync(ct)
            : [];
        Jahre = Enumerable.Range(heute.Year - 9, 10).Append(Jahr).Distinct().OrderDescending().ToList();
    }
}
