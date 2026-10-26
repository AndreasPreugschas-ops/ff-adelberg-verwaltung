using System.Net;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Microsoft.EntityFrameworkCore;
using static System.Net.WebUtility;

namespace Fevad.Integration.Tests;

public partial class JugendbetreuerTests
{
    [Fact]
    public async Task Prueftermin_des_Schutzkonzepts_erinnert_und_wird_fortgeschrieben()
    {
        var jugendwart = Angemeldet(Rollen.Jugendwart);
        const string seite = "/Jugend/Betreuer?handler=Schutzkonzept";

        // Termin in der Vergangenheit wird abgelehnt.
        await AbschickenAsync(jugendwart, seite, new() { ["naechste"] = Heute.AddDays(-1).ToString("yyyy-MM-dd") });
        Assert.DoesNotContain("Schutzkonzept prüfen", HtmlDecode(await jugendwart.GetStringAsync("/?handler=Anliegen")));

        // Erster Prüftermin in zehn Tagen: „Was liegt an“ erinnert.
        Assert.Equal(HttpStatusCode.Redirect,
            (await AbschickenAsync(jugendwart, seite, new() { ["naechste"] = Heute.AddDays(10).ToString("yyyy-MM-dd") })).StatusCode);
        Assert.Contains($"Schutzkonzept prüfen (bis {Heute.AddDays(10):dd.MM.yyyy})", HtmlDecode(await jugendwart.GetStringAsync("/?handler=Anliegen")));
        Assert.Contains("Nächster Prüftermin: ", HtmlDecode(await jugendwart.GetStringAsync("/Jugend/Betreuer")));

        // Heute geprüft: nächster Termin nach dem Intervall (24 Monate), keine Erinnerung mehr.
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(jugendwart, seite, [])).StatusCode);
        var wert = await MitDbAsync(db => db.Parameter.Where(p => p.Schluessel == ParameterSchluessel.SchutzkonzeptPruefungAm && p.GueltigAb == Heute)
            .Select(p => p.Wert).SingleAsync());
        Assert.Equal(Heute.AddMonths(24).ToString("yyyy-MM-dd"), wert);
        Assert.DoesNotContain("Schutzkonzept prüfen", HtmlDecode(await jugendwart.GetStringAsync("/?handler=Anliegen")));

        // Wer die Betreuer nur sehen darf, ändert den Termin nicht; ohne Jugendrecht kein Zugriff.
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(Rollen.Geraetewart).GetAsync("/Jugend/Betreuer")).StatusCode);
    }
}
