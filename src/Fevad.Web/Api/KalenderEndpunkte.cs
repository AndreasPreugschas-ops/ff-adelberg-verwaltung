using System.Text;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Api;

/// <summary>
/// Kalender-Abo (iCal) für Handy und Kalender-Apps. Kalender-Apps können sich nicht anmelden,
/// deshalb steckt ein geheimer, jederzeit erneuerbarer Schlüssel im Link. Der Feed enthält nur die eigenen Termine.
/// </summary>
public static class KalenderEndpunkte
{
    public static IEndpointRouteBuilder MapKalenderEndpunkte(this IEndpointRouteBuilder app)
    {
        app.MapGet("/kalender/{token}.ics", KalenderLiefern).AllowAnonymous();
        return app;
    }

    private static async Task<IResult> KalenderLiefern(string token, AppDbContext db, Dienstplanung planung, ParameterLeser parameter, TimeProvider zeit, CancellationToken ct)
    {
        if (token.Length < 20)
        {
            return Results.NotFound();
        }

        var konto = await db.Users.AsNoTracking()
            .Where(b => b.KalenderToken == token && b.DeaktiviertAm == null)
            .Select(b => new { b.Id })
            .FirstOrDefaultAsync(ct);
        // Ohne verknüpftes Mitglied oder zugeordnete Kinder gibt es keine Zielgruppe; der Feed liefert dann nichts statt aller Termine.
        var personen = konto is null ? [] : await planung.PersonenAsync(konto.Id, ct);
        if (personen.Count == 0)
        {
            return Results.NotFound();
        }

        var jetzt = zeit.GetUtcNow();
        var heute = Anzeige.LokalesDatum(jetzt);
        var rueckblick = await parameter.ZahlAsync(ParameterSchluessel.KalenderRueckblickMonate, heute, 3, ct);
        var vorschau = await parameter.ZahlAsync(ParameterSchluessel.KalenderVorschauMonate, heute, 12, ct);
        var von = Anzeige.AlsUtc(heute.AddMonths(-rueckblick), TimeOnly.MinValue);
        var bis = Anzeige.AlsUtc(heute.AddMonths(vorschau), TimeOnly.MinValue);

        var profile = new List<Teilnehmerprofil>();
        foreach (var person in personen)
        {
            profile.Add(await planung.ProfilAsync(person.MitgliedId, ct));
        }

        var dienste = (await planung.TermineAsync(von, bis, null, ct))
            .Where(d => profile.Any(p => p.Betrifft(d, Dienstplanung.LokalesDatum(d.Beginn)))).ToList();
        var mitglieder = personen.Select(p => p.MitgliedId).ToList();
        var antworten = await db.Rueckmeldungen.Where(r => mitglieder.Contains(r.MitgliedId) && r.Dienst!.Beginn >= von && r.Dienst.Beginn < bis)
            .Select(r => new { r.DienstId, r.MitgliedId, r.Antwort }).ToListAsync(ct);

        // Die eigene Antwort zuerst; bei reinen Elternkonten die des (ersten) Kindes.
        RueckmeldungAntwort? Antwort(Guid dienstId) => personen
            .Select(p => antworten.FirstOrDefault(a => a.DienstId == dienstId && a.MitgliedId == p.MitgliedId)?.Antwort)
            .FirstOrDefault(a => a is not null);

        var inhalt = Kalenderdatei.Erzeugen("FEVAD – Termine FF Adelberg", dienste.Select(d => new Kalenderdatei.Termin(d, Antwort(d.Id))), jetzt);
        return Results.Text(inhalt, "text/calendar; charset=utf-8", Encoding.UTF8);
    }
}
