using System.Net;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

public partial class RechteTests
{
    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static HttpClient AlsPerson(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, Guid benutzerId, params string[] rollen)
    {
        var client = Angemeldet(factory, rollen);
        client.DefaultRequestHeaders.Add(TestAnmeldung.BenutzerHeader, benutzerId.ToString());
        return client;
    }

    [Fact]
    public void Gesundheits_und_Jugendrechte_brauchen_eine_zweite_Freigabe()
    {
        var jugend = new HashSet<Guid> { Guid.NewGuid() };
        var andere = Guid.NewGuid();
        Assert.True(Berechtigungen.BrauchtFreigabe(Berechtigungen.AtemschutzLesen, null, [], jugend));
        Assert.True(Berechtigungen.BrauchtFreigabe(Berechtigungen.JugendZugang, null, [], jugend));
        Assert.True(Berechtigungen.BrauchtFreigabe(Berechtigungen.RechteFreigeben, null, [], jugend));
        Assert.True(Berechtigungen.BrauchtFreigabe(Berechtigungen.MitgliederLesen, null, [jugend.First()], jugend));
        Assert.True(Berechtigungen.BrauchtFreigabe(Berechtigungen.MitgliederLesen, [], [jugend.First()], jugend));
        Assert.False(Berechtigungen.BrauchtFreigabe(Berechtigungen.MitgliederLesen, null, [], jugend));
        Assert.False(Berechtigungen.BrauchtFreigabe(Berechtigungen.MitgliederLesen, null, [andere], jugend));
        Assert.False(Berechtigungen.BrauchtFreigabe(Berechtigungen.MitgliederLesen, [jugend.First()], [jugend.First(), andere], jugend));
        Assert.False(Berechtigungen.BrauchtFreigabe(Berechtigungen.AtemschutzLesen, [], [], jugend));
        Assert.False(Berechtigungen.BrauchtFreigabe(Berechtigungen.TechnikLesen, null, [], jugend));
    }

    [Fact]
    public async Task Rolle_mit_Atemschutzrecht_wirkt_erst_nach_Freigabe_durch_zweite_Person()
    {
        var factory = TestAnmeldung.Factory(fixture);
        var admin = Angemeldet(factory, Rollen.Administrator);
        var name = "Vg" + Guid.NewGuid().ToString("N")[..6];
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(admin, "/Verwaltung/Rechte", [new("Bezeichnung", "Prüfer " + name), new("Beschreibung", ""), new("Vorlage", "")])).StatusCode);
        var rolle = "Pruefer" + name;
        var pfad = $"/Verwaltung/Rechte/Bearbeiten?name={rolle}";
        var gespeichert = await AbschickenAsync(admin, pfad,
            [new("Bezeichnung", "Prüfer " + name), new("recht", Berechtigungen.AtemschutzLesen), new("recht", Berechtigungen.TechnikLesen), new("GesundheitBestaetigt", "true")]);
        Assert.Equal(HttpStatusCode.Redirect, gespeichert.StatusCode);

        // Technik gilt sofort, Atemschutz wartet.
        var rechte = await MitDbAsync(db => db.RollenRechte.Where(r => r.Rolle == rolle).Select(r => r.Recht).ToListAsync());
        Assert.Equal([Berechtigungen.TechnikLesen], rechte);
        var antrag = await MitDbAsync(db => db.Rechtefreigaben.AsNoTracking().SingleAsync(f => f.Rolle == rolle));
        Assert.Equal((RechtefreigabeArt.Rollenrecht, Berechtigungen.AtemschutzLesen, TestAnmeldung.BenutzerId, (bool?)null),
            (antrag.Art, antrag.Recht, antrag.BeantragtVon, antrag.Freigegeben));
        Assert.Contains("wartet auf Freigabe", WebUtility.HtmlDecode(await admin.GetStringAsync(pfad)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, rolle).GetAsync("/Atemschutz")).StatusCode);

        // Wer beantragt hat, gibt nicht selbst frei; das Administratorkonto hat das Recht ohnehin nicht.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/Verwaltung/Rechtefreigaben")).StatusCode);
        var selbst = AlsPerson(factory, TestAnmeldung.BenutzerId, Rollen.Kommandant);
        Assert.Contains("eigener Antrag", WebUtility.HtmlDecode(await selbst.GetStringAsync("/Verwaltung/Rechtefreigaben")));
        await AbschickenAsync(selbst, $"/Verwaltung/Rechtefreigaben?handler=Freigeben&id={antrag.Id}", []);
        Assert.Null(await MitDbAsync(db => db.Rechtefreigaben.Where(f => f.Id == antrag.Id).Select(f => f.Freigegeben).SingleAsync()));

        var zweite = Guid.NewGuid();
        var kommandant = AlsPerson(factory, zweite, Rollen.Kommandant);
        Assert.Contains("Rechte freigeben", WebUtility.HtmlDecode(await kommandant.GetStringAsync("/")));
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, $"/Verwaltung/Rechtefreigaben?handler=Freigeben&id={antrag.Id}", [])).StatusCode);
        var entschieden = await MitDbAsync(db => db.Rechtefreigaben.AsNoTracking().SingleAsync(f => f.Id == antrag.Id));
        Assert.Equal((true, zweite), (entschieden.Freigegeben, entschieden.EntschiedenVon));
        Assert.True(await MitDbAsync(db => db.RollenRechte.AnyAsync(r => r.Rolle == rolle && r.Recht == Berechtigungen.AtemschutzLesen)));
        Assert.Equal(HttpStatusCode.OK, (await Angemeldet(factory, rolle).GetAsync("/Atemschutz")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(admin, pfad.Replace("?", "?handler=Loeschen&"), [])).StatusCode);
    }

    [Fact]
    public async Task Konto_bekommt_Rolle_mit_Gesundheitsdaten_erst_nach_Freigabe()
    {
        var factory = TestAnmeldung.Factory(fixture);
        Guid kontoId;
        string email = $"vg-{Guid.NewGuid():N}"[..12] + "@ff-adelberg.test";
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var konten = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            var konto = new Benutzer { UserName = email, Email = email };
            Assert.True((await konten.CreateAsync(konto)).Succeeded);
            Assert.True((await konten.AddToRoleAsync(konto, Rollen.Mitglied)).Succeeded);
            kontoId = konto.Id;
        }

        var admin = Angemeldet(factory, Rollen.Administrator);
        var antwort = await AbschickenAsync(admin, $"/Verwaltung/Benutzer/Bearbeiten/{kontoId}",
        [
            new("Formular.Id", kontoId.ToString()), new("Formular.Email", email),
            new("Formular.Rollen", Rollen.Mitglied), new("Formular.Rollen", Rollen.Geraetewart), new("Formular.Rollen", Rollen.Atemschutzgeraetewart),
        ]);
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);

        async Task<IList<string>> RollenAsync()
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            var konten = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            return await konten.GetRolesAsync((await konten.FindByIdAsync(kontoId.ToString()))!);
        }

        Assert.Equal([Rollen.Geraetewart, Rollen.Mitglied], (await RollenAsync()).Order());
        var antrag = await MitDbAsync(db => db.Rechtefreigaben.AsNoTracking().SingleAsync(f => f.KontoId == kontoId));
        Assert.Equal((RechtefreigabeArt.Rollenzuweisung, Rollen.Atemschutzgeraetewart), (antrag.Art, antrag.Rolle));
        Assert.Contains("wartet auf Freigabe", WebUtility.HtmlDecode(await admin.GetStringAsync($"/Verwaltung/Benutzer/Bearbeiten/{kontoId}")));

        var kommandant = AlsPerson(factory, Guid.NewGuid(), Rollen.Kommandant);
        Assert.Contains(email, WebUtility.HtmlDecode(await kommandant.GetStringAsync("/Verwaltung/Rechtefreigaben")));
        Assert.Equal(HttpStatusCode.Redirect, (await AbschickenAsync(kommandant, $"/Verwaltung/Rechtefreigaben?handler=Freigeben&id={antrag.Id}", [])).StatusCode);
        Assert.Contains(Rollen.Atemschutzgeraetewart, await RollenAsync());
    }

    [Fact]
    public async Task Entschiedene_Rechtefreigaben_loescht_der_Loeschlauf_nach_der_Protokollfrist()
    {
        var (alt, offen, neu) = await MitDbAsync(async db =>
        {
            var lange = DateTimeOffset.UtcNow.AddYears(-10);
            var a = new Rechtefreigabe { Art = RechtefreigabeArt.Rollenrecht, Rolle = Rollen.Mitglied, Recht = Berechtigungen.AtemschutzLesen, Freigegeben = false, EntschiedenAm = lange };
            var o = new Rechtefreigabe { Art = RechtefreigabeArt.Rollenrecht, Rolle = Rollen.Mitglied, Recht = Berechtigungen.JugendZugang };
            var n = new Rechtefreigabe { Art = RechtefreigabeArt.Rollenrecht, Rolle = Rollen.Mitglied, Recht = Berechtigungen.UnfaelleLesen, Freigegeben = false, EntschiedenAm = DateTimeOffset.UtcNow };
            db.Rechtefreigaben.AddRange(a, o, n);
            await db.SaveChangesAsync();
            return (a.Id, o.Id, n.Id);
        });
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        }

        var uebrig = await MitDbAsync(db => db.Rechtefreigaben.IgnoreQueryFilters().Where(f => f.Id == alt || f.Id == offen || f.Id == neu).Select(f => f.Id).ToListAsync());
        Assert.Equal(new[] { offen, neu }.Order(), uebrig.Order());
        await MitDbAsync(db => db.Rechtefreigaben.Where(f => f.Id == offen || f.Id == neu).ExecuteDeleteAsync());
    }

    [Fact]
    public async Task Leseprotokoll_haelt_fest_wer_Atemschutzdaten_ansieht()
    {
        var factory = TestAnmeldung.Factory(fixture);
        var name = "Lp" + Guid.NewGuid().ToString("N")[..6];
        var mitgliedId = await MitDbAsync(async db =>
        {
            var m = new Mitglied { Personalnummer = "L-" + Guid.NewGuid().ToString("N")[..8], Vorname = "Lena", Nachname = name, Geburtsdatum = new(1990, 1, 1), AtemschutzAktiv = true };
            m.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = (await db.Abteilungen.SingleAsync(a => a.Code == AbteilungCode.Einsatz)).Id, Status = MitgliedStatus.Aktiv, GueltigAb = Heute.AddYears(-1) });
            db.Mitglieder.Add(m);
            await db.SaveChangesAsync();
            return m.Id;
        });

        var leser = Guid.NewGuid();
        var wart = AlsPerson(factory, leser, Rollen.Atemschutzgeraetewart);
        Assert.Equal(HttpStatusCode.OK, (await wart.GetAsync($"/Personal/Akte/{mitgliedId}")).StatusCode);
        var eintraege = await MitDbAsync(db => db.AuditEintraege.AsNoTracking()
            .Where(a => a.Aktion == AuditAktion.Gelesen && a.DatensatzId == mitgliedId).ToListAsync());
        var eintrag = Assert.Single(eintraege);
        Assert.Equal((Leseprotokoll.Atemschutz, (Guid?)leser), (eintrag.Tabelle, eintrag.BenutzerId));

        // Ohne Atemschutzrecht wird nichts protokolliert.
        Assert.Equal(HttpStatusCode.OK, (await AlsPerson(factory, Guid.NewGuid(), Rollen.Fuehrungskraft).GetAsync($"/Personal/Akte/{mitgliedId}")).StatusCode);
        Assert.Equal(1, await MitDbAsync(db => db.AuditEintraege.CountAsync(a => a.Aktion == AuditAktion.Gelesen && a.DatensatzId == mitgliedId)));

        var protokoll = WebUtility.HtmlDecode(await Angemeldet(factory, Rollen.Kommandant).GetStringAsync($"/Verwaltung/Leseprotokoll?MitgliedId={mitgliedId}"));
        Assert.Contains($"{name}, Lena", protokoll);
        Assert.Contains("Personalakte", protokoll);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Atemschutzgeraetewart).GetAsync("/Verwaltung/Leseprotokoll")).StatusCode);
    }
}
