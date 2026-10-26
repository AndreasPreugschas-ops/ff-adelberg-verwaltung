using System.Net;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Einsatzbericht: Mittel, Schadenart, Tiere und Gefahrstoff-Block (P3: V15).</summary>
public partial class EinsatzberichtTests
{
    [Fact]
    public async Task Gefahrstoff_Block_wird_gespeichert_und_ohne_Haken_geleert()
    {
        Guid einsatzId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            einsatzId = (await scope.ServiceProvider.GetRequiredService<Einsatzberichte>()
                .NeuAnlegenAsync(new DateTimeOffset(2053, 3, 4, 12, 0, 0, TimeSpan.Zero))).Id;
        }

        var client = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);
        var pfad = $"/Einsaetze/Bericht/{einsatzId}";
        async Task<HttpResponseMessage> Speichern(Action<Dictionary<string, string>> anpassen)
        {
            var seite = await client.GetStringAsync(pfad);
            var felder = Felder(Version(seite), Token(seite));
            felder["Formular.Alarmiert"] = "04.03.2053 13:00";
            anpassen(felder);
            return await PostAsync(client, pfad, null, felder);
        }

        // Eine UN-Nummer hat vier Ziffern.
        var falsch = WebUtility.HtmlDecode(await (await Speichern(f => { f["Formular.Gefahrstoff"] = "true"; f["Formular.UnNummer"] = "12"; })).Content.ReadAsStringAsync());
        Assert.Contains("Vier Ziffern", falsch);

        Assert.Equal(HttpStatusCode.Redirect, (await Speichern(f =>
        {
            f["Formular.EingesetzteMittel"] = "3 Sack Bindemittel";
            f["Formular.Schadenart"] = "Gewässer / Umwelt";
            f["Formular.TiereGerettet"] = "2";
            f["Formular.Gefahrstoff"] = "true";
            f["Formular.GefahrstoffBezeichnung"] = "Dieselkraftstoff";
            f["Formular.UnNummer"] = "1202";
            f["Formular.GefahrstoffMenge"] = "ca. 40 l";
            f["Formular.FreisetzungWasser"] = "true";
            f["Formular.Behoerdenmeldung"] = "Umweltschutzamt, 13:40 Uhr";
        })).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var e = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Einsaetze.SingleAsync(x => x.Id == einsatzId);
            Assert.Equal(("3 Sack Bindemittel", "Gewässer / Umwelt", 2), (e.EingesetzteMittel, e.Schadenart, e.TiereGerettet));
            Assert.Equal(("Dieselkraftstoff", "1202", true, false), (e.GefahrstoffBezeichnung, e.UnNummer, e.FreisetzungWasser, e.FreisetzungBoden));
        }

        // Ohne Haken bleiben keine Reste des Blocks stehen.
        Assert.Equal(HttpStatusCode.Redirect, (await Speichern(f => f["Formular.GefahrstoffBezeichnung"] = "Dieselkraftstoff")).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var e = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Einsaetze.SingleAsync(x => x.Id == einsatzId);
            Assert.Equal((false, null, null, false), (e.Gefahrstoff, e.GefahrstoffBezeichnung, e.UnNummer, e.FreisetzungWasser));
        }
    }
}
