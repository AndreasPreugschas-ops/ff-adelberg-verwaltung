using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Datenschutz im eigenen Konto (V39): Datenschutzinformation nach Art. 13 DSGVO als PDF, Vorschau „was passiert mit meinen Daten“
/// beim Austritt und beim Wechsel in die Altersabteilung (Fristen aus den Parametern des Löschkonzepts) und die Löschanfrage nach Art. 17.
/// </summary>
public class DatenschutzModel(AppDbContext db, Dienstplanung planung, Antragsbereich antragsbereich, ParameterLeser parameter, IAktuellerBenutzer aktuell, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Zeile der Vorschau.</summary>
    /// <param name="Was">Welche Daten.</param>
    /// <param name="Wann">Was wann geschieht.</param>
    public sealed record Vorschauzeile(string Was, string Wann);

    /// <summary>Vorschau für den Austritt.</summary>
    public IReadOnlyList<Vorschauzeile> Austritt { get; private set; } = [];
    /// <summary>Eigene Löschanfragen, neueste zuerst.</summary>
    public IReadOnlyList<Loeschanfrage> Anfragen { get; private set; } = [];
    /// <summary>Mit dem Konto ist ein Mitglied verknüpft.</summary>
    public bool MitMitglied { get; private set; }

    /// <summary>Text der neuen Löschanfrage.</summary>
    [BindProperty, Required(ErrorMessage = "Bitte beschreiben, was gelöscht werden soll."), StringLength(4000), Display(Name = "Was soll gelöscht werden?")]
    public string? Text { get; set; }

    /// <summary>Zeigt die Seite.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        return Page();
    }

    /// <summary>Datenschutzinformation als PDF; mit der Ergänzung für die Jugend, wenn das Konto Kinder hat.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF.</returns>
    public async Task<IActionResult> OnGetInformationAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var jugend = (await planung.PersonenAsync(aktuell.Id, ct)).Any(p => !p.Selbst);
        var pdf = AntragPdf.NurDatenschutzinformation(await antragsbereich.RahmenAsync(null, heute, ct), jugend);
        return File(pdf, "application/pdf", "Datenschutzinformation.pdf");
    }

    /// <summary>Stellt eine Löschanfrage.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAnfrageAsync(CancellationToken ct)
    {
        if (aktuell.Id is not { } konto)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var mitgliedId = await planung.MitgliedIdAsync(konto, ct);
        var name = mitgliedId is { } m
            ? await db.Mitglieder.Where(x => x.Id == m).Select(x => x.Vorname + " " + x.Nachname).FirstAsync(ct)
            : await db.Erziehungsberechtigte.Where(e => e.KontoId == konto).Select(e => e.Vorname + " " + e.Nachname).FirstOrDefaultAsync(ct)
                ?? await db.Users.Where(b => b.Id == konto).Select(b => b.Email).FirstOrDefaultAsync(ct) ?? "unbekannt";
        db.Loeschanfragen.Add(new Loeschanfrage { KontoId = konto, MitgliedId = mitgliedId, Name = name, Text = Text!.Trim(), GestelltAm = zeit.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Löschanfrage gestellt. Du bekommst hier eine Antwort, was gelöscht wurde und was wegen Aufbewahrungspflichten bleibt.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        async Task<int> Zahl(string schluessel, int ersatz) => await parameter.ZahlAsync(schluessel, heute, ersatz, ct);

        MitMitglied = await planung.MitgliedIdAsync(aktuell.Id, ct) is not null;
        Anfragen = await db.Loeschanfragen.AsNoTracking().Where(l => l.KontoId == aktuell.Id).OrderByDescending(l => l.GestelltAm).ToListAsync(ct);
        Austritt =
        [
            new("Anschrift, Telefon, E-Mail, Arbeitgeber, Notfallkontakt, Notizen, Größen",
                $"{await Zahl(ParameterSchluessel.KontaktdatenLoeschenNachAustrittMonate, 3)} Monate nach dem Austritt gelöscht (L6), frühere Anschriften ebenso"),
            new("Bankverbindung", $"{await Zahl(ParameterSchluessel.BankverbindungLoeschenNachMonate, 12)} Monate nach dem Austritt gelöscht (L37)"),
            new("Einwilligungen", $"{await Zahl(ParameterSchluessel.EinwilligungenAufbewahrungMonate, 36)} Monate nach dem Austritt gelöscht (L51)"),
            new("Benutzerkonto", $"beim Austritt deaktiviert, {await Zahl(ParameterSchluessel.KontoLoeschenNachDeaktivierungMonate, 6)} Monate danach geleert (L18)"),
            new("Anwesenheit bei Diensten", $"nach {await Zahl(ParameterSchluessel.AnwesenheitenVerdichtenNachJahren, 6)} Jahren zu Jahressummen verdichtet, die Einzelnachweise gelöscht (L5)"),
            new("Atemschutz: Übungen und Einsätze (falls vorhanden)",
                $"{await Zahl(ParameterSchluessel.AtemschutznachweiseAufbewahrungJahre, 40)} Jahre nach dem letzten Nachweis gelöscht; Pflicht nach § 10a Gefahrstoffverordnung (L8)"),
            new("Name, Mitgliedszeiten, Dienstgrade, Lehrgänge, Ehrungen",
                $"{await Zahl(ParameterSchluessel.RestdatensatzAufbewahrungJahre, 10)} Jahre nach dem Austritt anonymisiert (L7), danach nur noch für die Statistik"),
            new("Einsatzberichte, Kassenbelege", $"bleiben nach den gesetzlichen Fristen ({await Zahl(ParameterSchluessel.EinsatzberichteAufbewahrungJahre, 10)} bzw. {await Zahl(ParameterSchluessel.KassendatenAufbewahrungJahre, 10)} Jahre)"),
        ];
    }
}
