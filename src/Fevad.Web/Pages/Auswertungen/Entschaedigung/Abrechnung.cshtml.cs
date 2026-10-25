using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Entschaedigung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Auswertungen.Entschaedigung;

/// <summary>Eine Entschädigungsabrechnung mit Summen je Person, Neuberechnung, Abschluss und Ausgaben für die Gemeindekasse.</summary>
public class AbrechnungModel(
    AppDbContext db, Abrechnungen abrechnungen, AbrechnungExport export, IAuthorizationService autorisierung, IAktuellerBenutzer aktuellerBenutzer,
    TimeProvider zeit, ParameterLeser parameter)
    : PageModel
{
    /// <summary>Abrechnung mit Positionen und Summen.</summary>
    public AbrechnungExport.Daten Daten { get; private set; } = null!;
    /// <summary>Die Abrechnung selbst.</summary>
    public Abrechnung Abrechnung => Daten.Abrechnung;
    /// <summary>Name des Benutzers, der die Abrechnung abgeschlossen hat.</summary>
    public string? AbgeschlossenVon { get; private set; }
    /// <summary>Der Benutzer darf abrechnen (berechnen, löschen).</summary>
    public bool DarfAbrechnen { get; private set; }
    /// <summary>Der Benutzer darf die Abrechnung abschließen.</summary>
    public bool DarfAbschliessen { get; private set; }
    /// <summary>Der Benutzer darf Bankverbindungen sehen (Auszahlungsliste, SEPA-Datei).</summary>
    public bool DarfBankdaten { get; private set; }
    /// <summary>Anzahl der Empfänger ohne hinterlegte Bankverbindung.</summary>
    public int OhneBankverbindung { get; private set; }

    /// <summary>Zeigt die Abrechnung.</summary>
    /// <param name="id">Kennung der Abrechnung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Abrechnungen.AnyAsync(a => a.Id == id, ct))
        {
            return NotFound();
        }

        Daten = await export.LadenAsync(id, ct);
        DarfAbrechnen = await DarfAsync(Berechtigungen.EntschaedigungAbrechnen);
        DarfAbschliessen = await DarfAsync(Berechtigungen.EntschaedigungAbschliessen);
        DarfBankdaten = await DarfAsync(Berechtigungen.KasseBankdaten);
        if (DarfBankdaten)
        {
            OhneBankverbindung = (await EmpfaengerAsync(Daten, true, ct)).Count(e => e.Iban is null && e.Betrag > 0);
        }

        if (Abrechnung.AbgeschlossenVon is { } benutzer)
        {
            AbgeschlossenVon = await db.Users.Where(u => u.Id == benutzer).Select(u => u.UserName).SingleOrDefaultAsync(ct);
        }

        return Page();
    }

    /// <summary>Exporte: <c>summen</c> und <c>posten</c> als CSV, <c>excel</c> mit beiden Blättern.</summary>
    public async Task<IActionResult> OnGetExportAsync(Guid id, string format, CancellationToken ct)
    {
        if (!await db.Abrechnungen.AnyAsync(a => a.Id == id, ct))
        {
            return NotFound();
        }

        var daten = await export.LadenAsync(id, ct);
        return format switch
        {
            "summen" => File(AbrechnungExport.SummenCsv(daten), "text/csv; charset=utf-8", AbrechnungExport.Dateiname(daten.Abrechnung, "csv").Replace("Entschaedigung_", "Auszahlung_")),
            "posten" => File(AbrechnungExport.PostenCsv(daten), "text/csv; charset=utf-8", AbrechnungExport.Dateiname(daten.Abrechnung, "csv")),
            "excel" => File(AbrechnungExport.Excel(daten), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", AbrechnungExport.Dateiname(daten.Abrechnung, "xlsx")),
            _ => BadRequest(),
        };
    }

    /// <summary>Auszahlungsliste als PDF; mit IBAN nur für Kassier und Kommandant.</summary>
    public async Task<IActionResult> OnGetAuszahlungAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Abrechnungen.AnyAsync(a => a.Id == id, ct))
        {
            return NotFound();
        }

        var liste = await ListeAsync(await export.LadenAsync(id, ct), await DarfAsync(Berechtigungen.KasseBankdaten), ct);
        return File(Auszahlung.Pdf(liste), "application/pdf", Auszahlung.Dateiname(liste.Abrechnung, "pdf"));
    }

    /// <summary>Summenblatt als Anlage zur Kassenanordnung (V35): Summen je Art und Person, Felder für „sachlich richtig“ und „angeordnet“.</summary>
    /// <param name="id">Kennung der Abrechnung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetSummenblattAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Abrechnungen.AnyAsync(a => a.Id == id, ct))
        {
            return NotFound();
        }

        var daten = await export.LadenAsync(id, ct);
        var liste = await ListeAsync(daten, false, ct);
        var stichtag = daten.Abrechnung.Bis;
        var o = await db.OrganisationAsync(ct);
        var jeArt = daten.Zeilen
            .GroupBy(z => z.Art == PostenArt.Dienst ? z.Vorgang : AbrechnungExport.ArtText(z.Art))
            .Select(g => new Auszahlung.Artsumme(ArtText(g.Key), g.Count(), g.Sum(z => z.Betrag)))
            .OrderBy(a => a.Art)
            .ToList();
        var blatt = new Auszahlung.Summenblatt(liste, o.GemeindeName,
            await parameter.WertAsync(ParameterSchluessel.KassenanordnungEntschaedigungBuchungsstelle, stichtag, ct),
            await parameter.WertAsync(ParameterSchluessel.EntschaedigungssatzungBezeichnung, stichtag, ct) ?? "Feuerwehr-Entschädigungssatzung",
            jeArt);
        return File(Auszahlung.SummenblattPdf(blatt), "application/pdf", Auszahlung.SummenblattDateiname(daten.Abrechnung));
    }

    /// <summary>Lesbare Art: Dienstarten mit ihrem Anzeigenamen, sonst der Text der Postenart.</summary>
    private static string ArtText(string vorgang) =>
        Enum.TryParse<DienstArt>(vorgang, out var art) ? Anzeige.Text(art) : vorgang;

    /// <summary>SEPA-Überweisungsdatei; Auftraggeber und Datum werden nicht gespeichert.</summary>
    public async Task<IActionResult> OnPostUeberweisungAsync(Guid id, string? auftraggeber, string? auftraggeberIban, DateOnly? ausfuehrung, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.KasseBankdaten))
        {
            return Forbid();
        }

        if (!await db.Abrechnungen.AnyAsync(a => a.Id == id, ct))
        {
            return NotFound();
        }

        var daten = await export.LadenAsync(id, ct);
        if (!daten.Abrechnung.IstAbgeschlossen)
        {
            TempData["Fehler"] = "Überwiesen wird erst, wenn die Abrechnung abgeschlossen ist.";
            return RedirectToPage(new { id });
        }

        try
        {
            var liste = await ListeAsync(daten, true, ct);
            var tag = ausfuehrung ?? Anzeige.Heute(zeit);
            return File(Auszahlung.Sepa(liste, auftraggeber ?? "", auftraggeberIban ?? "", tag, zeit.GetUtcNow()), "application/xml",
                Auszahlung.Dateiname(liste.Abrechnung, "xml"));
        }
        catch (InvalidOperationException fehler)
        {
            TempData["Fehler"] = fehler.Message;
            return RedirectToPage(new { id });
        }
    }

    private async Task<Auszahlung.Liste> ListeAsync(AbrechnungExport.Daten daten, bool mitIban, CancellationToken ct)
    {
        var o = await db.OrganisationAsync(ct);
        return new(new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp), daten.Abrechnung,
            await EmpfaengerAsync(daten, mitIban, ct), mitIban);
    }

    private async Task<IReadOnlyList<Auszahlung.Empfaenger>> EmpfaengerAsync(AbrechnungExport.Daten daten, bool mitIban, CancellationToken ct)
    {
        var nummern = daten.Summen.Select(s => s.Personalnummer).ToList();
        var konten = mitIban
            ? await db.Bankverbindungen.AsNoTracking().Where(b => nummern.Contains(b.Mitglied!.Personalnummer))
                .Select(b => new { b.Mitglied!.Personalnummer, b.Kontoinhaber, b.Iban }).ToDictionaryAsync(b => b.Personalnummer, ct)
            : [];
        return daten.Summen.Select(s => new Auszahlung.Empfaenger(s.Personalnummer, $"{s.Nachname}, {s.Vorname}",
            konten.GetValueOrDefault(s.Personalnummer)?.Kontoinhaber, konten.GetValueOrDefault(s.Personalnummer)?.Iban, s.Betrag)).ToList();
    }

    /// <summary>Berechnet einen Entwurf aus den aktuellen Daten neu.</summary>
    /// <param name="id">Kennung der Abrechnung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder Fehler, 403 oder 404.</returns>
    public Task<IActionResult> OnPostNeuBerechnenAsync(Guid id, CancellationToken ct) =>
        AusfuehrenAsync(id, Berechtigungen.EntschaedigungAbrechnen, () => abrechnungen.NeuBerechnenAsync(id, ct), "Neu berechnet.");

    /// <summary>Schließt die Abrechnung ab; danach ist sie unveränderlich.</summary>
    /// <param name="id">Kennung der Abrechnung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder Fehler, 403 oder 404.</returns>
    public Task<IActionResult> OnPostAbschliessenAsync(Guid id, CancellationToken ct) =>
        AusfuehrenAsync(id, Berechtigungen.EntschaedigungAbschliessen, () => abrechnungen.AbschliessenAsync(id, aktuellerBenutzer.Id ?? Guid.Empty, ct),
            "Abrechnung abgeschlossen. Sie kann jetzt an die Gemeindekasse gehen.");

    /// <summary>Löscht einen Entwurf.</summary>
    /// <param name="id">Kennung der Abrechnung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Liste, bei Fehler zurück zur Abrechnung; 403 oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, CancellationToken ct)
    {
        var ergebnis = await AusfuehrenAsync(id, Berechtigungen.EntschaedigungAbrechnen, () => abrechnungen.LoeschenAsync(id, ct), "Entwurf gelöscht.");
        return ergebnis is RedirectToPageResult && TempData.Peek("Fehler") is null ? RedirectToPage("Index") : ergebnis;
    }

    private async Task<IActionResult> AusfuehrenAsync(Guid id, string policy, Func<Task> aktion, string meldung)
    {
        if (!await DarfAsync(policy))
        {
            return Forbid();
        }

        if (!await db.Abrechnungen.AnyAsync(a => a.Id == id))
        {
            return NotFound();
        }

        try
        {
            await aktion();
            TempData["Meldung"] = meldung;
        }
        catch (InvalidOperationException fehler)
        {
            TempData["Fehler"] = fehler.Message;
        }

        return RedirectToPage(new { id });
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;
}
