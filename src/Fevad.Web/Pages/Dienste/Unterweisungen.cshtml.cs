using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Fevad.Web.Pages.Dienste;

/// <summary>
/// Deckungsprüfung der Unterweisung (V53): welche Pflichtthemen fehlen im Kalenderjahr noch, und bei wem. Zielgruppe ist die
/// Einsatzabteilung, bei Themen mit Qualifikation nur deren Inhaber (z. B. Atemschutzgeräteträger).
/// </summary>
[Authorize(Policy = Berechtigungen.AusbildungLesen)]
public class UnterweisungenModel(Unterweisungen unterweisungen, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Geprüftes Kalenderjahr.</summary>
    public int Jahr { get; private set; }
    /// <summary>Heutiges Datum.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Ab diesem Monat werden offene Pflichtthemen gelb.</summary>
    public int WarnenAbMonat { get; private set; }
    /// <summary>Je Pflichtthema die Deckung.</summary>
    public IReadOnlyList<Unterweisungen.Deckungszeile> Zeilen { get; private set; } = [];

    /// <summary>Lädt die Deckung eines Jahres.</summary>
    /// <param name="jahr">Kalenderjahr; leer = laufendes Jahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(int? jahr, CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        Jahr = jahr is >= 2000 and <= 2100 ? jahr.Value : Heute.Year;
        var stichtag = Jahr < Heute.Year ? new DateOnly(Jahr, 12, 31) : Heute;
        WarnenAbMonat = await parameter.ZahlAsync(ParameterSchluessel.UnterweisungWarnenAbMonat, Heute, 10, ct);
        Zeilen = await unterweisungen.DeckungAsync(Jahr, stichtag, ct);
    }
}
