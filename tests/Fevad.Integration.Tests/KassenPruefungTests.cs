using System.Net;
using Fevad.Domain.Kassen;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Kassenprüfung durch zwei Rechnungsprüfer mit Rolle aus der Funktion (P2: V05).</summary>
public partial class KasseTests
{
    /// <summary>Mitglied mit Konto und Kassenprüfer-Funktion bis zum angegebenen Tag; der Client trägt die Rolle wie nach der Anmeldung.</summary>
    private async Task<(Guid MitgliedId, Guid KontoId, HttpClient Client)> PrueferAsync(string nachname, DateOnly bis)
    {
        var (mitgliedId, _, _) = await MitgliedMitKontoAsync(nachname);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var kp = await db.Funktionen.SingleAsync(f => f.Code == "KP");
        db.FunktionZuordnungen.Add(new FunktionZuordnung
        {
            MitgliedId = mitgliedId, FunktionId = kp.Id, Art = FunktionArt.Wahl, GueltigAb = Heute.AddYears(-1), GueltigBis = bis,
        });
        await db.SaveChangesAsync();
        var kontoId = await db.Users.Where(u => u.MitgliedId == mitgliedId).Select(u => u.Id).SingleAsync();
        var client = Client(Rollen.Mitglied, Rollen.Kassenpruefer);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, kontoId.ToString());
        return (mitgliedId, kontoId, client);
    }

    [Fact]
    public async Task Funktion_Kassenpruefer_bringt_die_Rolle_nur_fuer_die_Amtszeit()
    {
        var (_, aktiv, _) = await PrueferAsync("Amtierend" + Guid.NewGuid().ToString("N")[..6], Heute.AddYears(4));
        var (_, ehemalig, _) = await PrueferAsync("Ehemalig" + Guid.NewGuid().ToString("N")[..6], Heute.AddDays(-1));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var benutzer = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var fabrik = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<Benutzer>>();

        Assert.True((await fabrik.CreateAsync((await benutzer.FindByIdAsync(aktiv.ToString()))!)).IsInRole(Rollen.Kassenpruefer));
        Assert.False((await fabrik.CreateAsync((await benutzer.FindByIdAsync(ehemalig.ToString()))!)).IsInRole(Rollen.Kassenpruefer));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(Rollen.Kassenpruefer, (await db.Funktionen.SingleAsync(f => f.Code == "KP")).VorgeschlageneRolle);
        Assert.True(await db.RollenRechte.AnyAsync(r => r.Rolle == Rollen.Kassenpruefer && r.Recht == Berechtigungen.KasseLesen));
    }

    [Fact]
    public async Task Zwei_Pruefer_pruefen_die_Kasse_und_der_Kassier_beantwortet_Beanstandungen()
    {
        var vorjahr = Heute.Year - 1;
        var (kasse, giro, einnahme, ausgabe) = await EigeneKasseAsync(new DateOnly(vorjahr, 1, 1));
        await BuchenAsync(kasse.Id, giro, einnahme, BuchungArt.Einnahme, 900m, new DateOnly(vorjahr, 2, 1), "Zuwendung");
        var beleg = await BuchenAsync(kasse.Id, giro, ausgabe, BuchungArt.Ausgabe, 120m, new DateOnly(vorjahr, 7, 1), "Grillgut");
        var kennung = Guid.NewGuid().ToString("N")[..6];
        var (erster, _, pruefer1) = await PrueferAsync("Erster" + kennung, Heute.AddYears(4));
        var (zweiter, _, pruefer2) = await PrueferAsync("Zweiter" + kennung, Heute.AddYears(4));
        var (ehemalig, _, _) = await PrueferAsync("Ehemalig" + kennung, Heute.AddDays(-1));
        var kassier = Client(Rollen.Kassier);

        var liste = $"/Kasse/Pruefungen?kasse={kasse.Id}";
        var html = WebUtility.HtmlDecode(await pruefer1.GetStringAsync(liste));
        Assert.Contains("Zweiter" + kennung, html);
        Assert.DoesNotContain("Ehemalig" + kennung, html);
        Assert.DoesNotContain("Neue Prüfung", WebUtility.HtmlDecode(await kassier.GetStringAsync(liste)));

        var felder = new Dictionary<string, string>
        {
            ["Formular.Von"] = $"01.01.{vorjahr}", ["Formular.Bis"] = $"31.12.{vorjahr}", ["Formular.Pruefer2Id"] = ehemalig.ToString(),
        };
        var antwort = await AbschickenAsync(pruefer1, liste, liste, felder);
        Assert.Contains("im Amt", WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync()));
        felder["Formular.Pruefer2Id"] = zweiter.ToString();
        antwort = await AbschickenAsync(pruefer1, liste, liste, felder);
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        var seite = antwort.Headers.Location!.ToString();
        Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var p = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Kassenpruefungen.SingleAsync(x => x.KasseId == kasse.Id);
            Assert.Equal((erster, zweiter), (p.Pruefer1Id, p.Pruefer2Id));
            id = p.Id;
        }

        // Prüfer lesen Kassenbuch und Kontoauszüge, buchen aber nicht.
        Assert.Contains("Grillgut", await pruefer1.GetStringAsync($"/Kasse/Buch?kasse={kasse.Id}&jahr={vorjahr}"));
        Assert.DoesNotContain("Einlesen", await pruefer1.GetStringAsync($"/Kasse/Kontoauszug?kasse={kasse.Id}"));
        Assert.Equal(HttpStatusCode.Forbidden, (await pruefer1.GetAsync($"/Kasse/Buchung?kasse={kasse.Id}")).StatusCode);

        // Beanstandung zu einem Beleg; der Kassier darf keine erfassen, aber antworten.
        antwort = await AbschickenAsync(pruefer1, seite, $"{seite}&handler=Beanstandung",
            new() { ["NeueBeanstandung"] = "Quittung ohne Datum", ["BuchungId"] = beleg.Id.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(kassier, seite, $"{seite}&handler=Beanstandung",
            new() { ["NeueBeanstandung"] = "selbst" })).StatusCode);
        Guid beanstandung;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var b = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Beanstandungen.SingleAsync(x => x.KassenpruefungId == id);
            Assert.Equal(beleg.Id, b.BuchungId);
            beanstandung = b.Id;
        }

        antwort = await AbschickenAsync(kassier, seite, $"{seite}&handler=Antwort&beanstandungId={beanstandung}", new() { ["Antwort"] = "Datum nachgetragen" });
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(pruefer2, seite, $"{seite}&handler=Erledigt&beanstandungId={beanstandung}&erledigt=true", new())).StatusCode);

        // Bestätigen geht erst mit Ergebnis; ändert der andere danach etwas, gilt die Bestätigung nicht mehr.
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(pruefer1, seite, $"{seite}&handler=Bestaetigen", new())).StatusCode);
        var ergebnis = new Dictionary<string, string>
        {
            ["Ergebnis.GeprueftAm"] = $"15.01.{Heute.Year}", ["Ergebnis.Text"] = "Kasse ordnungsgemäß geführt", ["Ergebnis.EntlastungEmpfohlen"] = "true",
        };
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(pruefer1, seite, $"{seite}&handler=Ergebnis", ergebnis)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(pruefer1, seite, $"{seite}&handler=Bestaetigen", new())).StatusCode);
        ergebnis["Ergebnis.Text"] = "Kasse ordnungsgemäß geführt, Belege vollständig";
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(pruefer2, seite, $"{seite}&handler=Ergebnis", ergebnis)).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            Assert.Null((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Kassenpruefungen.SingleAsync(x => x.Id == id)).Pruefer1BestaetigtAm);
        }

        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(pruefer1, seite, $"{seite}&handler=Bestaetigen", new())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(pruefer2, seite, $"{seite}&handler=Bestaetigen", new())).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var p = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Kassenpruefungen.SingleAsync(x => x.Id == id);
            Assert.True(p.Abgeschlossen);
            Assert.True(p.EntlastungEmpfohlen);
            Assert.Equal(new DateOnly(Heute.Year, 1, 15), p.GeprueftAm);
        }

        // Abgeschlossen: nichts mehr änderbar, der Bericht ist fertig und im Jahresabschluss verlinkt.
        Assert.Equal(HttpStatusCode.Forbidden, (await AbschickenAsync(pruefer1, seite, $"{seite}&handler=Beanstandung",
            new() { ["NeueBeanstandung"] = "zu spät" })).StatusCode);
        var pdf = await kassier.GetAsync($"{seite}&handler=Pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Entlastung empfohlen", WebUtility.HtmlDecode(await kassier.GetStringAsync($"/Kasse/Abschluss?kasse={kasse.Id}&Jahr={vorjahr}")));
    }
}
