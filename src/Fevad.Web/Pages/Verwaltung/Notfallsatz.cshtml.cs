using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Atemschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Notbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Verwaltung;

/// <summary>
/// Papier-Notfallsatz (R18): monatlicher Sammeldruck für das Gerätehaus mit Erreichbarkeit der Einsatzabteilung, Atemschutz-Einsetzbarkeit
/// (nur ja/nein), Prüfstand von Fahrzeugen und Geräten und leerer Anwesenheitsliste. Jeder Druck wird vermerkt; die Erinnerung kommt über
/// „Was liegt an“, wenn der letzte älter als der Parameter ist.
/// </summary>
[Authorize(Policy = Berechtigungen.Notfallsatz)]
public class NotfallsatzModel(
    AppDbContext db, Notfallsatzfrist frist, Atemschutzabfrage atemschutz, Pruefstaende pruefstaende, Kassenberichte berichte,
    IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Letzter Druck in UTC; leer, wenn noch nie gedruckt.</summary>
    public DateTimeOffset? Letzter { get; private set; }

    /// <summary>Bis wann der nächste Druck fällig ist.</summary>
    public DateOnly Faellig { get; private set; }

    /// <summary>Wer zuletzt gedruckt hat.</summary>
    public string? LetzterDurch { get; private set; }

    /// <summary>Zeigt den Stand des letzten Drucks.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        (Letzter, Faellig) = await frist.StandAsync(Anzeige.Heute(zeit), ct);
        LetzterDurch = await db.Betriebsmeldungen.AsNoTracking().Where(m => m.Art == BetriebsmeldungArt.Notfallsatz)
            .OrderByDescending(m => m.Zeitpunkt).Select(m => m.Text).FirstOrDefaultAsync(ct);
    }

    /// <summary>Erzeugt den Notfallsatz als PDF und vermerkt den Druck.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF.</returns>
    public async Task<IActionResult> OnPostPdfAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var jetzt = zeit.GetUtcNow();
        var daten = await DatenAsync(heute, jetzt, ct);
        var pdf = NotfallsatzPdf.Erzeugen(daten);
        await frist.GedrucktAsync(jetzt, await NameAsync(ct), ct);
        return File(pdf, "application/pdf", $"notfallsatz-{heute:yyyy-MM-dd}.pdf");
    }

    private async Task<NotfallsatzPdf.Daten> DatenAsync(DateOnly heute, DateTimeOffset jetzt, CancellationToken ct)
    {
        var mitglieder = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s => s.Abteilung!.Art == AbteilungArt.Einsatz && !s.Vordienstzeit
                && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit)))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, m.Nachname, m.Vorname, m.TelefonMobil, m.TelefonFestnetz, m.NotfallkontaktName, m.NotfallkontaktTelefon })
            .ToListAsync(ct);
        var traeger = (await atemschutz.TraegerAsync(heute, ct)).ToDictionary(t => t.MitgliedId, t => t.Status.Einsatzbereit);
        var personen = mitglieder.Select(m => new NotfallsatzPdf.Person(
            $"{m.Nachname}, {m.Vorname}",
            m.TelefonMobil ?? m.TelefonFestnetz,
            m.NotfallkontaktName is null ? null : $"{m.NotfallkontaktName} {m.NotfallkontaktTelefon}".Trim(),
            traeger.TryGetValue(m.Id, out var bereit) ? (bereit ? "ja" : "nein") : null)).ToList();

        var fahrzeuge = (await pruefstaende.FahrzeugeAsync(heute, ct: ct)).Select(Zeile).ToList();
        var geraete = await pruefstaende.GeraeteAsync(heute, ct: ct);
        var auffaellig = geraete.Where(g => g.Ampel != Fristampel.Gruen || g.Zustand != Domain.Technik.GeraetZustand.Einsatzbereit).Select(Zeile).ToList();

        return new NotfallsatzPdf.Daten(await berichte.KopfAsync(ct), Anzeige.DatumZeit(jetzt) + " Uhr", personen, fahrzeuge, auffaellig,
            geraete.Count - auffaellig.Count);
    }

    private static NotfallsatzPdf.Technik Zeile(Pruefstand p)
    {
        var offen = p.Faelligkeiten.Where(f => f.Ampel != Fristampel.Gruen)
            .Select(f => $"{Anzeige.Text(f.Pruefart)} {(f.Ampel == Fristampel.Rot ? "überfällig" : "bald fällig")}");
        return new NotfallsatzPdf.Technik(p.Nummer, p.Bezeichnung, Anzeige.Text(p.Zustand),
            p.NaechsterTermin is { } t ? Anzeige.Datum(t) : "–", string.Join(", ", offen) is { Length: > 0 } h ? h : null);
    }

    private async Task<string> NameAsync(CancellationToken ct)
    {
        if (benutzer.Id is not { } konto)
        {
            return "FEVAD";
        }

        return await db.Users.Where(u => u.Id == konto)
            .Select(u => db.Mitglieder.Where(x => x.Id == u.MitgliedId).Select(x => x.Vorname + " " + x.Nachname).FirstOrDefault() ?? u.UserName)
            .FirstOrDefaultAsync(ct) ?? "FEVAD";
    }
}
