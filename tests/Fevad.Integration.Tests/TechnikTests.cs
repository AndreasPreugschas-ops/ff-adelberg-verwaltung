using System.Net;
using System.Text.RegularExpressions;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Technik;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

[Collection(AnwendungCollection.Name)]
public partial class TechnikTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    [GeneratedRegex("id=\"Formular_Version\" name=\"Formular.Version\" value=\"(\\d+)\"")]
    private static partial Regex VersionFeld();

    /// <summary>Ein paar Tage zurück, damit die Prüfung nicht wegen der Zeitzone „in der Zukunft“ liegt.</summary>
    private static readonly DateOnly Pruefdatum = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);

    [Fact]
    public async Task Geraet_anlegen_und_pruefen_mit_Mangel()
    {
        var (_, leiterArt, funktion, belastung) = await LeiterAsync();
        var factory = TestAnmeldung.Factory(fixture);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);
        var nummer = Nummer("L");

        // Materialstamm direkt mit erfassen; inventurpflichtig verlangt eine Inventurgruppe.
        var teilenummer = "TN-" + Guid.NewGuid().ToString("N")[..8];
        var formular = await geraetewart.GetStringAsync("/Technik/Geraete/Bearbeiten");
        var felderGeraet = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(formular),
            ["Formular.Material"] = "neu",
            ["Formular.Neu.Bezeichnung"] = "Steckleiter Teil " + teilenummer,
            ["Formular.Neu.Hersteller"] = "Günzburger",
            ["Formular.Neu.Herstellerteilenummer"] = teilenummer,
            ["Formular.Neu.GeraeteartId"] = leiterArt.ToString(),
            ["Formular.Neu.Stueckwert"] = "480,00",
            ["Formular.Neu.Inventurpflichtig"] = "true",
            ["Formular.Inventarnummer"] = nummer,
            ["Formular.IndienstAm"] = "2020-05-01",
        };
        var ohneGruppe = await PostAsync(geraetewart, "/Technik/Geraete/Bearbeiten", felderGeraet);
        Assert.Equal(HttpStatusCode.OK, ohneGruppe.StatusCode);
        Assert.Contains("Das Material ist inventurpflichtig", WebUtility.HtmlDecode(await ohneGruppe.Content.ReadAsStringAsync()));

        var gruppe = new Inventurgruppe { Bezeichnung = "Regal " + teilenummer, Art = InventurgruppenArt.Regal };
        await MitDbAsync(async db => { db.Inventurgruppen.Add(gruppe); return await db.SaveChangesAsync(); });
        felderGeraet["Formular.InventurgruppeId"] = gruppe.Id.ToString();
        var angelegt = await PostAsync(geraetewart, "/Technik/Geraete/Bearbeiten", felderGeraet);
        Assert.Equal(HttpStatusCode.Redirect, angelegt.StatusCode);
        var detailsPfad = angelegt.Headers.Location!.ToString();
        var geraetId = Guid.Parse(detailsPfad.Split('/').Last());

        var details = await geraetewart.GetStringAsync(detailsPfad);
        Assert.Contains("noch nie geprüft", details);
        Assert.Contains(teilenummer, details);

        // Bezeichnung, Geräteart und Anschaffungspreis kommen aus dem Materialstamm; die Teilenummer findet das Gerät.
        var neu = await MitDbAsync(db => db.Geraete.Include(g => g.Material).SingleAsync(g => g.Id == geraetId));
        Assert.Equal(leiterArt, neu.GeraeteartId);
        Assert.Equal(neu.Material!.Bezeichnung, neu.Bezeichnung);
        Assert.Equal(480m, neu.Anschaffungspreis);
        Assert.Equal(gruppe.Id, neu.InventurgruppeId);
        Assert.Contains(nummer, await geraetewart.GetStringAsync($"/Technik/Geraete?Suche={teilenummer}"));
        Assert.Contains(teilenummer, await geraetewart.GetStringAsync($"/Technik/Materialien?Suche={teilenummer}"));
        Assert.Contains("<svg", details);
        Assert.Contains($"/g/{nummer}", details);

        // Ein Prüfpunkt nicht in Ordnung: „bestanden“ passt nicht, „mit Mängeln“ verlangt eine Beschreibung.
        var pfad = $"/Technik/Pruefungen/Neu?geraetId={geraetId}&positionId={funktion}";
        var seite = await geraetewart.GetStringAsync(pfad);
        Assert.Contains("Holme und Sprossen ohne Risse oder Verformung", seite);
        var felder = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.GeraetId"] = geraetId.ToString(),
            ["Formular.PositionId"] = funktion.ToString(),
            ["Formular.Datum"] = Pruefdatum.ToString("yyyy-MM-dd"),
            ["Formular.PrueferExtern"] = "Gerätewart Test",
            ["Formular.Punkte[0].InOrdnung"] = "true",
            ["Formular.Punkte[1].InOrdnung"] = "false",
            ["Formular.Punkte[2].InOrdnung"] = "true",
            ["Formular.Ergebnis"] = nameof(PruefErgebnis.Bestanden),
        };
        var abgelehnt = await PostAsync(geraetewart, "/Technik/Pruefungen/Neu", felder);
        Assert.Equal(HttpStatusCode.OK, abgelehnt.StatusCode);
        Assert.Contains("Nicht alle Prüfpunkte sind in Ordnung", WebUtility.HtmlDecode(await abgelehnt.Content.ReadAsStringAsync()));

        felder["Formular.Ergebnis"] = nameof(PruefErgebnis.BestandenMitMaengeln);
        abgelehnt = await PostAsync(geraetewart, "/Technik/Pruefungen/Neu", felder);
        Assert.Contains("Bitte den Mangel beschreiben.", WebUtility.HtmlDecode(await abgelehnt.Content.ReadAsStringAsync()));

        felder["Formular.Mangel"] = "Gelenk schwergängig";
        felder["Formular.MangelSchwere"] = nameof(MangelSchwere.Einschraenkung);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, "/Technik/Pruefungen/Neu", felder)).StatusCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pruefung = await db.Pruefungen.SingleAsync(p => p.GeraetId == geraetId);
        Assert.True(pruefung.Abgeschlossen);
        Assert.Equal(Pruefdatum.AddMonths(12), pruefung.NaechsteFaellig);
        Assert.Equal([true, false, true], PruefpunktErgebnis.AusJson(pruefung.Ergebnisse).Select(e => e.InOrdnung));
        var mangel = await db.Maengel.SingleAsync(m => m.GeraetId == geraetId);
        Assert.Equal(pruefung.Id, mangel.PruefungId);

        var stand = (await scope.ServiceProvider.GetRequiredService<Pruefstaende>().GeraeteAsync(Pruefdatum.AddDays(3), geraetId: geraetId)).Single();
        Assert.Equal(GeraetZustand.Eingeschraenkt, stand.Zustand);
        Assert.Equal(Fristampel.Gruen, stand.Faelligkeiten.Single(f => f.PositionId == funktion).Ampel);
        Assert.True(stand.Faelligkeiten.Single(f => f.PositionId == belastung).NieGeprueft);
        Assert.Equal(Fristampel.Rot, stand.Ampel);

        // Das Protokoll erscheint im Prüfbuch und lässt sich ansehen.
        var protokoll = await geraetewart.GetStringAsync($"/Technik/Pruefungen/Details/{pruefung.Id}");
        Assert.Contains("Gelenk schwergängig", WebUtility.HtmlDecode(protokoll));

        // Nicht bestanden setzt das Gerät außer Betrieb, auch wenn der Mangel beschrieben und milder eingestuft ist.
        seite = await geraetewart.GetStringAsync(pfad);
        felder["__RequestVerificationToken"] = Token(seite);
        felder["Formular.Ergebnis"] = nameof(PruefErgebnis.NichtBestanden);
        felder["Formular.Mangel"] = "Sprosse gebrochen";
        felder["Formular.MangelSchwere"] = nameof(MangelSchwere.Info);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, "/Technik/Pruefungen/Neu", felder)).StatusCode);
        Assert.Equal(MangelSchwere.AusserBetrieb, await MitDbAsync(d => d.Maengel.Where(m => m.GeraetId == geraetId && m.Beschreibung == "Sprosse gebrochen").Select(m => m.Schwere).SingleAsync()));

        // Eine leere Inventarnummer zeigt die Validierung statt eines Serverfehlers.
        formular = await geraetewart.GetStringAsync("/Technik/Geraete/Bearbeiten");
        var leer = await PostAsync(geraetewart, "/Technik/Geraete/Bearbeiten", new()
        {
            ["__RequestVerificationToken"] = Token(formular),
            ["Formular.Inventarnummer"] = " ",
            ["Formular.Bezeichnung"] = "Ohne Nummer",
            ["Formular.Material"] = neu.MaterialId.ToString(),
        });
        Assert.Equal(HttpStatusCode.OK, leer.StatusCode);
    }

    [Fact]
    public async Task QR_Code_fuehrt_Mitglieder_zur_Maengelmeldung()
    {
        var (_, leiterArt, _, _) = await LeiterAsync();
        var nummer = Nummer("Q");
        Guid geraetId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var geraet = new Geraet { Inventarnummer = nummer, Bezeichnung = "Schiebleiter" };
            Materialstamm.Zuordnen(geraet, await Materialstamm.FindenOderAnlegenAsync(db, "Schiebleiter", leiterArt, null, null, true, default));
            db.Geraete.Add(geraet);
            await db.SaveChangesAsync();
            geraetId = geraet.Id;
        }

        var factory = TestAnmeldung.Factory(fixture);
        var mitglied = Angemeldet(factory, Rollen.Mitglied);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);

        var weiter = await mitglied.GetAsync($"/g/{nummer}");
        Assert.Equal(HttpStatusCode.Redirect, weiter.StatusCode);
        Assert.Equal($"/Melden?nummer={nummer}", weiter.Headers.Location!.ToString());
        Assert.Equal($"/Technik/Geraete/Details/{geraetId}", (await geraetewart.GetAsync($"/g/{nummer}")).Headers.Location!.ToString());

        // Ohne Technik-Recht keine Geräteseiten, aber melden geht.
        Assert.Equal(HttpStatusCode.Forbidden, (await mitglied.GetAsync("/Technik")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.GemeindeLesen).GetAsync("/Melden")).StatusCode);

        var seite = await mitglied.GetStringAsync($"/Melden?nummer={nummer}");
        Assert.Contains("Schiebleiter", seite);
        var gemeldet = await PostAsync(mitglied, "/Melden", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.Nummer"] = nummer,
            ["Formular.Beschreibung"] = "Sprosse gebrochen",
            ["Formular.Schwere"] = nameof(MangelSchwere.AusserBetrieb),
        });
        Assert.Equal(HttpStatusCode.Redirect, gemeldet.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var stand = (await scope.ServiceProvider.GetRequiredService<Pruefstaende>().GeraeteAsync(Pruefdatum, geraetId: geraetId)).Single();
            Assert.Equal(GeraetZustand.AusserBetrieb, stand.Zustand);
        }

        // Der Gerätewart behebt den Mangel; danach ist das Gerät wieder einsatzbereit.
        var mangelId = await MitDbAsync(db => db.Maengel.Where(m => m.GeraetId == geraetId).Select(m => m.Id).SingleAsync());
        var bearbeiten = await geraetewart.GetStringAsync($"/Technik/Maengel/Bearbeiten/{mangelId}");
        var fuehrung = Angemeldet(factory, Rollen.Fuehrungskraft);
        var nurLesen = await fuehrung.GetStringAsync($"/Technik/Maengel/Bearbeiten/{mangelId}");
        Assert.Contains("<fieldset disabled=\"disabled\"", nurLesen);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(fuehrung, $"/Technik/Maengel/Bearbeiten/{mangelId}", new()
        {
            ["__RequestVerificationToken"] = Token(nurLesen),
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, $"/Technik/Maengel/Bearbeiten/{mangelId}", new()
        {
            ["__RequestVerificationToken"] = Token(bearbeiten),
            ["Formular.Version"] = Version(bearbeiten).ToString(),
            ["Formular.Status"] = nameof(MangelStatus.Behoben),
            ["Formular.Schwere"] = nameof(MangelSchwere.AusserBetrieb),
            ["Formular.Massnahme"] = "Leiter ersetzt",
        })).StatusCode);
        Assert.NotNull(await MitDbAsync(db => db.Maengel.Where(m => m.Id == mangelId).Select(m => m.BehobenAm).SingleAsync()));
    }

    [Fact]
    public async Task Inventur_erfassen_und_abschliessen()
    {
        var (_, leiterArt, _, _) = await LeiterAsync();
        Guid fahrzeugId, vorhandenId, beschaedigtId;
        var nummerVorhanden = Nummer("I");
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fahrzeug = new Fahrzeug { Funkrufname = "Florian Test " + Guid.NewGuid().ToString("N")[..6], Typ = "TSF" };
            var gruppe = new Inventurgruppe { Bezeichnung = fahrzeug.Funkrufname, Art = InventurgruppenArt.Fahrzeug, Fahrzeug = fahrzeug };
            var material = await Materialstamm.FindenOderAnlegenAsync(db, "Steckleiter Inventur", leiterArt, null, null, true, default);
            var vorhanden = new Geraet { Inventarnummer = nummerVorhanden, Bezeichnung = "Leiter A", Fahrzeug = fahrzeug, Inventurgruppe = gruppe, Lagerort = "G1" };
            var beschaedigt = new Geraet { Inventarnummer = Nummer("I"), Bezeichnung = "Leiter B", Fahrzeug = fahrzeug, Inventurgruppe = gruppe, Lagerort = "G2" };
            // Nicht inventurpflichtiges Material (z. B. Verbrauchsmaterial) kommt nicht auf die Inventurliste.
            var verbrauch = new Geraet { Inventarnummer = Nummer("V"), Bezeichnung = "Leiter Verbrauch", Fahrzeug = fahrzeug, Inventurgruppe = gruppe };
            var ohneInventur = new Material { Bezeichnung = "Leiter ohne Inventur", GeraeteartId = leiterArt, Inventurpflichtig = false };
            Materialstamm.Zuordnen(vorhanden, material);
            Materialstamm.Zuordnen(beschaedigt, material);
            Materialstamm.Zuordnen(verbrauch, ohneInventur);
            db.AddRange(fahrzeug, gruppe, ohneInventur, vorhanden, beschaedigt, verbrauch);
            await db.SaveChangesAsync();
            (fahrzeugId, vorhandenId, beschaedigtId) = (gruppe.Id, vorhanden.Id, beschaedigt.Id);
        }

        var factory = TestAnmeldung.Factory(fixture);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);
        Assert.Equal(HttpStatusCode.Forbidden, (await Angemeldet(factory, Rollen.Fuehrungskraft).GetAsync("/Technik/Inventur")).StatusCode);

        var seite = await geraetewart.GetStringAsync("/Technik/Inventur");
        var begonnen = await PostAsync(geraetewart, "/Technik/Inventur", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.InventurgruppeId"] = fahrzeugId.ToString(),
            ["Formular.Stichtag"] = Pruefdatum.ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.Redirect, begonnen.StatusCode);
        var pfad = begonnen.Headers.Location!.ToString();
        var inventurId = Guid.Parse(pfad.Split('/').Last());

        seite = await geraetewart.GetStringAsync(pfad);
        Assert.Contains("Leiter A", seite);
        Assert.DoesNotContain("Leiter Verbrauch", seite);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, $"{pfad}?handler=Scannen", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["nummer"] = nummerVorhanden,
        })).StatusCode);

        var positionId = await MitDbAsync(db => db.InventurPositionen.Where(p => p.InventurId == inventurId && p.GeraetId == beschaedigtId).Select(p => p.Id).SingleAsync());
        var anfrage = new HttpRequestMessage(HttpMethod.Post, $"{pfad}?handler=Befund")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(seite),
                ["positionId"] = positionId.ToString(),
                ["befund"] = nameof(InventurBefund.Beschaedigt),
                ["bemerkung"] = "Holm verbogen",
            }),
        };
        anfrage.Headers.Add("HX-Request", "true");
        var zeile = await geraetewart.SendAsync(anfrage);
        Assert.Equal(HttpStatusCode.OK, zeile.StatusCode);
        Assert.StartsWith($"<tr id=\"pos-{positionId}\"", (await zeile.Content.ReadAsStringAsync()).Trim());

        // Ein geleertes Bemerkungsfeld löscht die Bemerkung.
        var vorhandenPosition = await MitDbAsync(db => db.InventurPositionen.Where(p => p.InventurId == inventurId && p.GeraetId == vorhandenId).Select(p => p.Id).SingleAsync());
        foreach (var bemerkung in new[] { "Etikett fehlt", "" })
        {
            Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, $"{pfad}?handler=Befund", new()
            {
                ["__RequestVerificationToken"] = Token(seite),
                ["positionId"] = vorhandenPosition.ToString(),
                ["befund"] = nameof(InventurBefund.Vorhanden),
                ["bemerkung"] = bemerkung,
            })).StatusCode);
        }

        Assert.Null(await MitDbAsync(db => db.InventurPositionen.Where(p => p.Id == vorhandenPosition).Select(p => p.Bemerkung).SingleAsync()));

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(geraetewart, $"{pfad}?handler=Abschliessen", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
        })).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var inventur = await db.Inventuren.Include(i => i.Positionen).SingleAsync(i => i.Id == inventurId);
            Assert.True(inventur.Abgeschlossen);
            Assert.Equal(InventurBefund.Vorhanden, inventur.Positionen.Single(p => p.GeraetId == vorhandenId).Befund);
            var mangel = await db.Maengel.SingleAsync(m => m.GeraetId == beschaedigtId);
            Assert.Contains("Holm verbogen", mangel.Beschreibung);
        }

        // Nach dem Abschluss ändert sich nichts mehr.
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(geraetewart, $"{pfad}?handler=Befund", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["positionId"] = positionId.ToString(),
            ["befund"] = nameof(InventurBefund.Vorhanden),
        })).StatusCode);
    }

    [Fact]
    public async Task Beschaffung_vom_Bedarf_bis_zum_Eingang()
    {
        var factory = TestAnmeldung.Factory(fixture);
        var geraetewart = Angemeldet(factory, Rollen.Geraetewart);
        var kommandant = Angemeldet(factory, Rollen.Kommandant);
        var gemeinde = Angemeldet(factory, Rollen.GemeindeLesen);
        var bezeichnung = "Wärmebildkamera " + Guid.NewGuid().ToString("N")[..6];

        var seite = await geraetewart.GetStringAsync("/Beschaffung/Bearbeiten");
        var angelegt = await PostAsync(geraetewart, "/Beschaffung/Bearbeiten", new()
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.Bezeichnung"] = bezeichnung,
            ["Formular.Menge"] = "1",
            ["Formular.Prioritaet"] = nameof(Prioritaet.Hoch),
            ["Formular.Haushaltsjahr"] = "2027",
            ["Formular.Kostenschaetzung"] = "6500,00",
        });
        Assert.Equal(HttpStatusCode.Redirect, angelegt.StatusCode);
        var pfad = angelegt.Headers.Location!.ToString();
        var id = Guid.Parse(pfad.Split('/').Last());
        Assert.Equal(6500m, await MitDbAsync(db => db.Beschaffungen.Where(b => b.Id == id).Select(b => b.Kostenschaetzung).SingleAsync()));

        // Die Gemeinde liest mit, ändert aber nichts.
        Assert.Contains(bezeichnung, WebUtility.HtmlDecode(await gemeinde.GetStringAsync("/Beschaffung")));
        Assert.Equal(HttpStatusCode.Forbidden, (await SchrittAsync(gemeinde, pfad, null)).StatusCode);

        // Genehmigen darf nur die Wehrführung; Bestellen vor der Genehmigung geht nicht.
        Assert.Equal(HttpStatusCode.Forbidden, (await SchrittAsync(geraetewart, pfad, BeschaffungStatus.Genehmigt)).StatusCode);
        var zuFrueh = await SchrittAsync(geraetewart, pfad, BeschaffungStatus.Bestellt);
        Assert.Equal(HttpStatusCode.OK, zuFrueh.StatusCode);
        Assert.Contains("geht es nicht nach", WebUtility.HtmlDecode(await zuFrueh.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await SchrittAsync(kommandant, pfad, BeschaffungStatus.Genehmigt)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await SchrittAsync(geraetewart, pfad, BeschaffungStatus.Bestellt)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await SchrittAsync(geraetewart, pfad, BeschaffungStatus.Geliefert)).StatusCode);

        var beschaffung = await MitDbAsync(db => db.Beschaffungen.SingleAsync(b => b.Id == id));
        Assert.Equal(BeschaffungStatus.Geliefert, beschaffung.Status);
        Assert.NotNull(beschaffung.GenehmigtAm);
        Assert.NotNull(beschaffung.BestelltAm);
        Assert.NotNull(beschaffung.GeliefertAm);

        // Eingang: Gerät mit übernommenen Angaben anlegen.
        Assert.Contains("Gerät anlegen", WebUtility.HtmlDecode(await geraetewart.GetStringAsync(pfad)));
        var geraet = await geraetewart.GetStringAsync($"/Technik/Geraete/Bearbeiten?beschaffungId={id}");
        Assert.Contains($"value=\"{bezeichnung}\"", WebUtility.HtmlDecode(geraet));
        Assert.Contains("value=\"6500,00\"", geraet);
    }

    [Fact]
    public async Task Alle_Technikseiten_laden()
    {
        var (fahrzeugId, artId) = await MitDbAsync(async db => (
            await db.Fahrzeuge.Where(f => f.Funkrufname == "Florian Adelberg 43").Select(f => f.Id).SingleAsync(),
            await db.Geraetearten.Where(g => g.Code == "HYDRAULIK").Select(g => g.Id).SingleAsync()));
        var kommandant = Angemeldet(TestAnmeldung.Factory(fixture), Rollen.Kommandant);

        foreach (var pfad in new[]
        {
            "/", "/Technik", "/Technik?tage=365", "/Technik/Fahrzeuge", $"/Technik/Fahrzeuge/Details/{fahrzeugId}", $"/Technik/Fahrzeuge/Bearbeiten/{fahrzeugId}",
            "/Technik/Geraete", "/Technik/Geraete?handler=Tabelle&NurFaellige=true", "/Technik/Geraete/Bearbeiten", "/Technik/Geraete/Etiketten",
            "/Technik/Maengel?alle=true", "/Technik/Inventur", "/Technik/Inventur/Gruppen", "/Technik/Materialien", "/Technik/Materialien/Bearbeiten", "/Technik/Geraetearten", $"/Technik/Geraetearten/Bearbeiten/{artId}",
            $"/Technik/Pruefungen/Neu?fahrzeugId={fahrzeugId}", "/Beschaffung", "/Beschaffung/Bearbeiten", "/Melden",
        })
        {
            var antwort = await kommandant.GetAsync(pfad);
            Assert.True(antwort.StatusCode == HttpStatusCode.OK, $"{pfad}: {antwort.StatusCode}");
        }

        // Das Stammfahrzeug hat über seine Prüfvorlage HU und UVV im Plan.
        var details = WebUtility.HtmlDecode(await kommandant.GetStringAsync($"/Technik/Fahrzeuge/Details/{fahrzeugId}"));
        Assert.Contains("Hauptuntersuchung", details);
        Assert.Contains("UVV-Prüfung", details);
    }

    /// <summary>Lädt das Formular neu und schickt es mit dem gewünschten nächsten Status ab.</summary>
    private static async Task<HttpResponseMessage> SchrittAsync(HttpClient client, string pfad, BeschaffungStatus? ziel)
    {
        var seite = await client.GetStringAsync(pfad);
        var felder = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(seite),
            ["Formular.Id"] = pfad.Split('/').Last(),
            ["Formular.Version"] = Version(seite).ToString(),
            ["Formular.Bezeichnung"] = WebUtility.HtmlDecode(Regex.Match(seite, "id=\"Formular_Bezeichnung\"[^>]*value=\"([^\"]*)\"").Groups[1].Value),
            ["Formular.Menge"] = "1",
            ["Formular.Kostenschaetzung"] = "6500,00",
            ["Formular.BestelltAm"] = Regex.Match(seite, "id=\"Formular_BestelltAm\"[^>]*value=\"([^\"]*)\"").Groups[1].Value,
            ["Formular.GeliefertAm"] = Regex.Match(seite, "id=\"Formular_GeliefertAm\"[^>]*value=\"([^\"]*)\"").Groups[1].Value,
        };
        if (ziel is not null)
        {
            felder["ziel"] = ziel.Value.ToString();
        }

        return await PostAsync(client, pfad, felder);
    }

    private async Task<(Guid Fahrzeug, Guid Art, Guid Funktion, Guid Belastung)> LeiterAsync() =>
        await MitDbAsync(async db =>
        {
            var art = await db.Geraetearten.Include(g => g.Pruefplan).SingleAsync(g => g.Code == "LEITER");
            return (Guid.Empty, art.Id,
                art.Pruefplan.Single(p => p.Pruefart == Pruefart.Funktionspruefung).Id,
                art.Pruefplan.Single(p => p.Pruefart == Pruefart.Belastungspruefung).Id);
        });

    private async Task<T> MitDbAsync<T>(Func<AppDbContext, Task<T>> abfrage)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static string Nummer(string praefix) => $"{praefix}-{Guid.NewGuid():N}"[..12];

    private static HttpClient Angemeldet(WebApplicationFactory<Program> factory, params string[] rollen)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, string.Join(',', rollen));
        return client;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string pfad, Dictionary<string, string> felder) =>
        client.PostAsync(pfad, new FormUrlEncodedContent(felder));

    private static string Token(string html) => WebUtility.HtmlDecode(TokenFeld().Match(html).Groups[1].Value);

    private static uint Version(string html) => uint.Parse(VersionFeld().Match(html).Groups[1].Value);
}
