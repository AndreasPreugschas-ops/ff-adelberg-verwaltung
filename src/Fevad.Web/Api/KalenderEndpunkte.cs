using System.Text;
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
            .Select(b => new { b.MitgliedId })
            .FirstOrDefaultAsync(ct);
        // Ohne verknüpftes Mitglied gibt es keine Zielgruppe; der Feed liefert dann nichts statt aller Termine.
        if (konto?.MitgliedId is not { } mitgliedId)
        {
            return Results.NotFound();
        }

        var jetzt = zeit.GetUtcNow();
        var heute = Anzeige.LokalesDatum(jetzt);
        var rueckblick = await parameter.ZahlAsync(ParameterSchluessel.KalenderRueckblickMonate, heute, 3, ct);
        var vorschau = await parameter.ZahlAsync(ParameterSchluessel.KalenderVorschauMonate, heute, 12, ct);
        var von = Anzeige.AlsUtc(heute.AddMonths(-rueckblick), TimeOnly.MinValue);
        var bis = Anzeige.AlsUtc(heute.AddMonths(vorschau), TimeOnly.MinValue);

        var dienste = await planung.TermineAsync(von, bis, await planung.ProfilAsync(mitgliedId, ct), ct);
        var antworten = await db.Rueckmeldungen.Where(r => r.MitgliedId == mitgliedId && r.Dienst!.Beginn >= von && r.Dienst.Beginn < bis)
            .ToDictionaryAsync(r => r.DienstId, r => r.Antwort, ct);

        var inhalt = Kalenderdatei.Erzeugen("FEVAD – Termine FF Adelberg",
            dienste.Select(d => new Kalenderdatei.Termin(d, antworten.TryGetValue(d.Id, out var a) ? a : null)), jetzt);
        return Results.Text(inhalt, "text/calendar; charset=utf-8", Encoding.UTF8);
    }
}
