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
public class BetreuerModel(Datenbereich datenbereich, Betreuerregister register, TimeProvider zeit) : PageModel
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

    /// <summary>Zeigt das Register.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        var freigabe = await datenbereich.FreigabeAsync(User, Berechtigungen.JugendBetreuer);
        Fristen = await register.FristenAsync(Heute, ct);
        Zeilen = await register.ListeAsync(freigabe.Alle ? null : freigabe.Abteilungen, Heute, !Ehemalige, ct);
    }
}
