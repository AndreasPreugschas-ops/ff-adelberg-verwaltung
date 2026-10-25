using System.Net;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Auswertungen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Fevad.Web.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Lieferung wird Gerät, Aussonderung, Anlagennachweis und Inventurprotokoll (V60), Stichtagsliste der Einsatzabteilung (V28).</summary>
public partial class TechnikTests
{
    [Fact]
    public async Task Lieferung_wird_Geraet_und_bleibt_bis_zur_Eingangspruefung_gesperrt()
    {
        var (_, leiterArt, _, _) = await LeiterAsync();
        var fahrzeug = new Fahrzeug { Funkrufname = "Florian Eingang " + Guid.NewGuid().ToString("N")[..6], Typ = "HLF 10" };
        var beschaffung = new Beschaffung
        {
            Bezeichnung = "Schiebleiter Eingang", Menge = 3, GeraeteartId = leiterArt, Status = BeschaffungStatus.Geliefert,
            Istkosten = 1500m, GeliefertAm = Pruefdatum, FahrzeugId = fahrzeug.Id,
        };
        await MitDbAsync(async db => { db.AddRange(fahrzeug, beschaffung); return await db.SaveChangesAsync(); });

        var factory = TestAnmeldung.Factory(fixture);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);
        var seite = await geraetewart.GetStringAsync($"/Beschaffung/Eingang/{beschaffung.Id}");
        Assert.Contains("Schiebleiter Eingang", seite);

        var praefix = "E" + Guid.NewGuid().ToString("N")[..6] + "-";
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, $"/Beschaffung/Eingang/{beschaffung.Id}", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.Material"] = "neu",
            ["Formular.NeuBezeichnung"] = "Schiebleiter Eingang",
            ["Formular.NeuGeraeteartId"] = leiterArt.ToString(),
            ["Formular.Anzahl"] = "3",
            ["Formular.ErsteNummer"] = praefix + "08",
            ["Formular.Seriennummern"] = "SN-1\nSN-2",
            ["Formular.Stueckpreis"] = "500",
            ["Formular.BeschafftAm"] = Pruefdatum.ToString("yyyy-MM-dd"),
            ["Formular.FahrzeugId"] = fahrzeug.Id.ToString(),
            ["Formular.Anlage.Eigentuemer"] = nameof(Eigentuemer.Gemeinde),
            ["Formular.Anlage.Anlagennummer"] = "A-4711",
            ["Formular.Eingangspruefung"] = "true",
        })).StatusCode);

        var geraete = await MitDbAsync(db => db.Geraete.Where(g => g.BeschaffungId == beschaffung.Id).OrderBy(g => g.Inventarnummer).ToListAsync());
        Assert.Equal([praefix + "08", praefix + "09", praefix + "10"], geraete.Select(g => g.Inventarnummer));
        Assert.Equal(["SN-1", "SN-2", null], geraete.Select(g => g.Seriennummer));
        Assert.All(geraete, g => Assert.Equal(500m, g.Anschaffungspreis));
        Assert.All(geraete, g => Assert.Equal("A-4711", g.Anlagennummer));
        Assert.All(geraete, g => Assert.NotNull(g.InventurgruppeId));
        var heute = Anzeige.LokalesDatum(DateTimeOffset.UtcNow);
        Assert.Equal(GeraetZustand.AusserBetrieb, await ZustandAsync(geraete[0].Id, heute));

        // Eine zweite Buchung mit denselben Nummern scheitert, ohne etwas anzulegen.
        var zweite = await PostAsync(geraetewart, $"/Beschaffung/Eingang/{beschaffung.Id}", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.Material"] = geraete[0].MaterialId.ToString(),
            ["Formular.Anzahl"] = "1",
            ["Formular.ErsteNummer"] = praefix + "10",
            ["Formular.FahrzeugId"] = fahrzeug.Id.ToString(),
        });
        Assert.Equal(HttpStatusCode.OK, zweite.StatusCode);
        Assert.Contains("Schon vergeben", WebUtility.HtmlDecode(await zweite.Content.ReadAsStringAsync()));

        // Die Geräteakte bietet die Eingangsprüfung an; eine bestandene Prüfung gibt das Gerät frei.
        Assert.Contains("Eingangsprüfung erfassen", WebUtility.HtmlDecode(await geraetewart.GetStringAsync($"/Technik/Geraete/Details/{geraete[0].Id}")));
        var formular = await geraetewart.GetStringAsync($"/Technik/Pruefungen/Neu?geraetId={geraete[0].Id}&pruefart={nameof(Pruefart.Eingang)}");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, "/Technik/Pruefungen/Neu", new()
        {
            ["__RequestVerificationToken"] = Token(formular),
            ["Formular.GeraetId"] = geraete[0].Id.ToString(),
            ["Formular.Pruefart"] = nameof(Pruefart.Eingang),
            ["Formular.Datum"] = Pruefdatum.ToString("yyyy-MM-dd"),
            ["Formular.PrueferExtern"] = "Gerätewart Test",
            ["Formular.Ergebnis"] = nameof(PruefErgebnis.Bestanden),
        })).StatusCode);
        Assert.Equal(GeraetZustand.Einsatzbereit, await ZustandAsync(geraete[0].Id, heute));
        Assert.Equal(GeraetZustand.AusserBetrieb, await ZustandAsync(geraete[1].Id, heute));
    }

    [Fact]
    public async Task Aussonderung_mit_Verwertung_erscheint_im_Anlagennachweis()
    {
        var (geraetId, nummer) = await GeraetAsync("Hydraulikpumpe Verkauf");
        await MitDbAsync(async db =>
        {
            var g = await db.Geraete.SingleAsync(x => x.Id == geraetId);
            g.Anschaffungspreis = 2400m;
            g.Foerdermittel = 600m;
            g.Foerdergeber = "Land BW (Z-Feu)";
            g.ZweckbindungBis = DateOnly.FromDateTime(DateTime.Today).AddYears(3);
            g.Anlagennummer = "A-" + nummer;
            return await db.SaveChangesAsync();
        });

        var factory = TestAnmeldung.Factory(fixture);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);
        var seite = WebUtility.HtmlDecode(await geraetewart.GetStringAsync($"/Technik/Geraete/Details/{geraetId}"));
        Assert.Contains("braucht einen Beschluss", seite);
        Assert.Contains("Fördergeber vor der Verwertung informieren", seite);

        // Ein Erlös gibt es nur beim Verkauf.
        var felder = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Aussonderung.Grund"] = "Ersatz durch neues Gerät",
            ["Aussonderung.Verwertung"] = nameof(Verwertung.Abgabe),
            ["Aussonderung.Erloes"] = "150",
            ["Aussonderung.Beschluss"] = "Gemeinderat 12.03.2027, TOP 4",
        };
        var abgelehnt = await PostAsync(geraetewart, $"/Technik/Geraete/Details/{geraetId}?handler=Aussondern", felder);
        Assert.Equal(HttpStatusCode.OK, abgelehnt.StatusCode);
        Assert.Contains("nur bei einem Verkauf", WebUtility.HtmlDecode(await abgelehnt.Content.ReadAsStringAsync()));

        felder["Aussonderung.Verwertung"] = nameof(Verwertung.Verkauf);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, $"/Technik/Geraete/Details/{geraetId}?handler=Aussondern", felder)).StatusCode);
        var geraet = await MitDbAsync(db => db.Geraete.SingleAsync(g => g.Id == geraetId));
        Assert.NotNull(geraet.AusgesondertAm);
        Assert.Equal(Verwertung.Verkauf, geraet.Verwertung);
        Assert.Equal(150m, geraet.Erloes);

        // Die Gemeinde sieht den Abgang im Anlagennachweis und als CSV.
        var gemeinde = Angemeldet(factory, Rollen.GemeindeLesen);
        Assert.Contains(nummer, await gemeinde.GetStringAsync("/Beschaffung/Anlagen"));
        var csv = await gemeinde.GetAsync($"/Beschaffung/Anlagen?handler=Csv&jahr={geraet.AusgesondertAm!.Value.Year}");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        var zeile = (await csv.Content.ReadAsStringAsync()).Split("\r\n").Single(z => z.Contains(nummer));
        Assert.Contains("2400,00", zeile);
        Assert.Contains("verkauft", zeile);
        Assert.Contains("150,00", zeile);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Fuehrungskraft).GetAsync("/Beschaffung/Anlagen")).StatusCode);
    }

    [Fact]
    public async Task Inventurprotokoll_mit_Zaehler_und_Pruefer()
    {
        var (geraetId, _) = await GeraetAsync("Leiter Protokoll");
        var inventur = new Inventur { Bezeichnung = "Inventur Protokoll", Stichtag = Pruefdatum };
        inventur.Positionen.Add(new InventurPosition { GeraetId = geraetId, SollStandort = "Lager", Befund = InventurBefund.Fehlt });
        await MitDbAsync(async db => { db.Inventuren.Add(inventur); return await db.SaveChangesAsync(); });

        var geraetewart = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Geraetewart);
        var seite = await geraetewart.GetStringAsync($"/Technik/Inventur/Erfassen/{inventur.Id}");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, $"/Technik/Inventur/Erfassen/{inventur.Id}?handler=Beteiligte", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["zaehler"] = "Müller, Schmid",
            ["pruefer"] = "Kommandant Meier",
        })).StatusCode);
        var gespeichert = await MitDbAsync(db => db.Inventuren.SingleAsync(i => i.Id == inventur.Id));
        Assert.Equal("Müller, Schmid", gespeichert.Zaehler);
        Assert.Equal("Kommandant Meier", gespeichert.Pruefer);

        var pdf = await geraetewart.GetAsync($"/Technik/Inventur/Erfassen/{inventur.Id}?handler=Protokoll");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync())[..4]);
    }

    [Fact]
    public async Task Stichtagsliste_zaehlt_die_Einsatzabteilung_am_31_12()
    {
        var jahr = Anzeige.LokalesDatum(DateTimeOffset.UtcNow).Year - 1;
        var personalnummer = "S-" + Guid.NewGuid().ToString("N")[..8];
        var ausgeschieden = "S-" + Guid.NewGuid().ToString("N")[..8];
        await MitDbAsync(async db =>
        {
            var abteilung = await db.Abteilungen.FirstAsync(a => a.Art == AbteilungArt.Einsatz);
            var mitglied = new Mitglied { Personalnummer = personalnummer, Vorname = "Stefan", Nachname = "Stichtag", Geburtsdatum = new(1990, 5, 5) };
            // Zwei lückenlose Zeiten zählen als eine Zugehörigkeit seit 01.03.2019.
            mitglied.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Probezeit, GueltigAb = new(2019, 3, 1), GueltigBis = new(2020, 2, 29) });
            mitglied.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2020, 3, 1) });
            var weg = new Mitglied { Personalnummer = ausgeschieden, Vorname = "Anna", Nachname = "Ausgetreten", Geburtsdatum = new(1980, 1, 1) };
            weg.Mitgliedschaften.Add(new Mitgliedschaft { AbteilungId = abteilung.Id, Status = MitgliedStatus.Aktiv, GueltigAb = new(2010, 1, 1), GueltigBis = new(jahr, 6, 30) });
            db.Mitglieder.AddRange(mitglied, weg);
            return await db.SaveChangesAsync();
        });

        var factory = TestAnmeldung.Factory(fixture);
        var gemeinde = Angemeldet(factory, Rollen.GemeindeLesen);
        var seite = WebUtility.HtmlDecode(await gemeinde.GetStringAsync("/Auswertungen/Stichtagsliste"));
        Assert.Contains($"31.12.{jahr}", seite);
        Assert.Contains(personalnummer, seite);
        Assert.DoesNotContain(ausgeschieden, seite);

        var csv = await (await gemeinde.GetAsync($"/Auswertungen/Stichtagsliste?handler=Csv&jahr={jahr}")).Content.ReadAsStringAsync();
        Assert.Contains($"{personalnummer};Stichtag;Stefan;", csv);
        Assert.Contains(";aktiv;01.03.2019", csv);
        Assert.Contains("Entschädigungssatzung § 5 Abs. 1", csv);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var nachweis = await scope.ServiceProvider.GetRequiredService<Stichtagsliste>().ErstellenAsync(jahr);
        Assert.Equal(nachweis.Angehoerige.Count * 90m, nachweis.Betraege[0].Euro);
        Assert.Equal(1500m, nachweis.Betraege[1].Euro);
        Assert.Equal(600m, nachweis.Betraege[2].Euro);

        var pdf = await gemeinde.GetAsync($"/Auswertungen/Stichtagsliste?handler=Pdf&jahr={jahr}");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var anzahlen = await gemeinde.GetAsync($"/Auswertungen/Stichtagsliste?handler=Pdf&jahr={jahr}&nurAnzahlen=true");
        Assert.Equal("application/pdf", anzahlen.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Anzahlen", anzahlen.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Geraetewart).GetAsync("/Auswertungen/Stichtagsliste")).StatusCode);
    }
}
