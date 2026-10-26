using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Eltern sehen im Elternkonto, was die Jugendfeuerwehr für den Notfall über ihr Kind weiß (V46): Heimweg, Abholberechtigte,
/// Erziehungsberechtigte, Notfallkontakt und ob die Notfallmappe vorliegt. Ändern lassen sie es beim Jugendwart.
/// </summary>
public class NotfallangabenModel(AppDbContext db, Dienstplanung planung, ParameterLeser parameter, IAktuellerBenutzer aktuell, TimeProvider zeit) : PageModel
{
    /// <summary>Die Angaben je Kind.</summary>
    public IReadOnlyList<NotfalllistePdf.Zeile> Kinder { get; private set; } = [];

    /// <summary>Nach so vielen Monaten ist die Mappe zu erneuern.</summary>
    public int ErneuernMonate { get; private set; }

    /// <summary>Zeigt die Angaben.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        ErneuernMonate = await parameter.ZahlAsync(ParameterSchluessel.NotfallmappeErneuernMonate, heute, 12, ct);
        var ids = (await planung.PersonenAsync(aktuell.Id, ct)).Where(p => !p.Selbst).Select(p => p.MitgliedId).ToList();
        var kinder = await db.Mitglieder.AsNoTracking().Where(m => ids.Contains(m.Id)).OrderBy(m => m.Vorname).ToListAsync(ct);
        var eltern = (await db.Erziehungsberechtigte.AsNoTracking().Where(e => ids.Contains(e.MitgliedId)).OrderBy(e => e.Nummer).ToListAsync(ct))
            .ToLookup(e => e.MitgliedId);
        Kinder = kinder.Select(m => new NotfalllistePdf.Zeile(
            $"{m.Vorname} {m.Nachname}",
            Anzeige.Datum(m.Geburtsdatum),
            null,
            m.Abholregelung is null ? "nicht angegeben" : Anzeige.Text(m.Abholregelung),
            m.Abholberechtigte,
            eltern[m.Id].Select(e => $"{e.Vorname} {e.Nachname}: {string.Join(" / ", new[] { e.Mobil, e.Telefon }.Where(t => !string.IsNullOrWhiteSpace(t)))}").ToList(),
            m.NotfallkontaktName is null ? null : $"{m.NotfallkontaktName} {m.NotfallkontaktTelefon}".Trim(),
            Jugend.NotfalllisteModel.Mappe(m, heute, ErneuernMonate))).ToList();
    }
}
