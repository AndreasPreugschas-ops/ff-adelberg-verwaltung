using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Jugend;

/// <summary>
/// Betreuer-Register der Jugend- und Kinderfeuerwehr (V47) mit dem Stand der erweiterten Führungszeugnisse: wer eingesetzt werden darf,
/// wessen Zeugnis bald oder schon wieder vorzulegen ist. Kommandant für alle, Jugendwart für seine Abteilungen.
/// </summary>
[Authorize(Policy = Berechtigungen.JugendBetreuerSehen)]
public class BetreuerModel(Datenbereich datenbereich, Betreuerregister register, Schutzkonzept schutzkonzept, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Auch Betreuer zeigen, deren Tätigkeit beendet ist.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Ehemalige { get; set; }

    /// <summary>Die Betreuer mit Stand.</summary>
    public IReadOnlyList<Betreuerregister.Zeile> Zeilen { get; private set; } = [];

    /// <summary>Fristen aus den Parametern.</summary>
    public Betreuerregister.Fristen Fristen { get; private set; } = null!;

    /// <summary>Stichtag.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Prüftermin des Schutzkonzepts (F26).</summary>
    public Schutzkonzept.Stand Schutz { get; private set; } = null!;

    /// <summary>Darf den Prüftermin ändern: Recht für die Betreuer aller Abteilungen (Kommandant) oder Jugendwart.</summary>
    public bool DarfSchutzkonzept { get; private set; }

    /// <summary>Zeigt das Register.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        var freigabe = await datenbereich.FreigabeAsync(User, Berechtigungen.JugendBetreuer);
        Fristen = await register.FristenAsync(Heute, ct);
        Zeilen = await register.ListeAsync(freigabe.Alle ? null : freigabe.Abteilungen, Heute, !Ehemalige, ct);
        Schutz = await schutzkonzept.StandAsync(Heute, ct);
        DarfSchutzkonzept = (await autorisierung.AuthorizeAsync(User, Berechtigungen.JugendBearbeiten)).Succeeded;
    }

    /// <summary>
    /// Vermerkt eine Prüfung des Schutzkonzepts oder trägt den ersten Prüftermin ein. Ohne Datum gilt: heute geprüft, nächster Termin nach
    /// dem Prüfintervall.
    /// </summary>
    /// <param name="naechste">Nächster Prüftermin; leer für „heute geprüft“.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung.</returns>
    public async Task<IActionResult> OnPostSchutzkonzeptAsync(DateOnly? naechste, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.JugendBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        Heute = Anzeige.Heute(zeit);
        var stand = await schutzkonzept.StandAsync(Heute, ct);
        var termin = naechste ?? Heute.AddMonths(stand.IntervallMonate);
        if (termin <= Heute)
        {
            TempData["Fehler"] = "Der nächste Prüftermin muss in der Zukunft liegen.";
            return RedirectToPage();
        }

        var wer = User.Identity?.Name ?? "unbekannt";
        await schutzkonzept.SetzenAsync(termin, naechste is null ? $"Schutzkonzept geprüft am {Anzeige.Datum(Heute)} ({wer})" : $"Prüftermin eingetragen ({wer})", Heute, ct);
        TempData["Meldung"] = $"Nächster Prüftermin des Schutzkonzepts: {Anzeige.Datum(termin)}.";
        return RedirectToPage();
    }
}
