using Fevad.Domain.Entschaedigung;
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

public class AbrechnungModel(
    AppDbContext db, Abrechnungen abrechnungen, AbrechnungExport export, IAuthorizationService autorisierung, IAktuellerBenutzer aktuellerBenutzer,
    TimeProvider zeit)
    : PageModel
{
    public AbrechnungExport.Daten Daten { get; private set; } = null!;
    public Abrechnung Abrechnung => Daten.Abrechnung;
    public string? AbgeschlossenVon { get; private set; }
    public bool DarfAbrechnen { get; private set; }
    public bool DarfAbschliessen { get; private set; }
    public bool DarfBankdaten { get; private set; }
    public int OhneBankverbindung { get; private set; }

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

    public Task<IActionResult> OnPostNeuBerechnenAsync(Guid id, CancellationToken ct) =>
        AusfuehrenAsync(id, Berechtigungen.EntschaedigungAbrechnen, () => abrechnungen.NeuBerechnenAsync(id, ct), "Neu berechnet.");

    public Task<IActionResult> OnPostAbschliessenAsync(Guid id, CancellationToken ct) =>
        AusfuehrenAsync(id, Berechtigungen.EntschaedigungAbschliessen, () => abrechnungen.AbschliessenAsync(id, aktuellerBenutzer.Id ?? Guid.Empty, ct),
            "Abrechnung abgeschlossen. Sie kann jetzt an die Gemeindekasse gehen.");

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
