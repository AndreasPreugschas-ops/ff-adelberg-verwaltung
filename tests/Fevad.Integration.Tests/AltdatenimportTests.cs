using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Fevad.Domain.Import;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Import;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fevad.Integration.Tests;

/// <summary>Altdatenimport (V68): Datei lesen, zuordnen, Probelauf ohne Speichern, Übernahme mit Freigabe.</summary>
[Collection(AnwendungCollection.Name)]
public partial class AltdatenimportTests(AnwendungFixture fixture)
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenFeld();

    private WebApplicationFactory<Program>? _factory;

    private HttpClient Client(string rolle)
    {
        _factory ??= TestAnmeldung.Factory(fixture);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAnmeldung.RollenHeader, rolle);
        return client;
    }

    private static async Task<string> TokenAsync(HttpClient client, string pfad) =>
        WebUtility.HtmlDecode(TokenFeld().Match(await client.GetStringAsync(pfad)).Groups[1].Value);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string pfad, string handler, Dictionary<string, string> felder)
    {
        felder["__RequestVerificationToken"] = await TokenAsync(client, pfad);
        return await client.PostAsync($"{pfad}?handler={handler}", new FormUrlEncodedContent(felder));
    }

    private static async Task<string> HochladenAsync(HttpClient client, Importziel ziel, string dateiname, byte[] inhalt)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(await TokenAsync(client, "/Verwaltung/Import")), "__RequestVerificationToken" },
            { new StringContent(((int)ziel).ToString()), "Ziel" },
            { new ByteArrayContent(inhalt), "Datei", dateiname },
        };
        var antwort = await client.PostAsync("/Verwaltung/Import", form);
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        return antwort.Headers.Location!.OriginalString;
    }

    [Fact]
    public void Csv_mit_Semikolon_Anfuehrungszeichen_und_Windows_Zeichensatz()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = "Name;Ort;Bemerkung\r\nMüller;Göppingen;\"Satz; mit \"\"Zitat\"\"\"\r\n;;\r\nBäuerle;Adelberg;\r\n";
        var tabelle = Tabellenleser.Lesen(Encoding.GetEncoding(1252).GetBytes(text), "export.csv");
        Assert.Equal(["Name", "Ort", "Bemerkung"], tabelle.Spalten);
        Assert.Equal(2, tabelle.Zeilen.Count);
        Assert.Equal("Satz; mit \"Zitat\"", tabelle.Zeilen[0][2]);
        Assert.Equal("Bäuerle", tabelle.Zeilen[1][0]);
    }

    [Fact]
    public void Xlsx_erstes_Blatt_mit_gemeinsamen_Texten_und_Luecken()
    {
        var tabelle = Tabellenleser.Lesen(Xlsx(), "export.xlsx");
        Assert.Equal(["Personalnummer", "Spalte 2", "Eintritt"], tabelle.Spalten);
        Assert.Equal(["4711", "", "45000"], tabelle.Zeilen.Single());
    }

    [Fact]
    public async Task Personen_und_Lehrgaenge_erst_Probelauf_dann_Uebernahme_mit_Freigabe()
    {
        var pn = "I" + Guid.NewGuid().ToString("N")[..8];
        var client = Client(Rollen.Administrator);
        var csv = $"Pers.-Nr.;Name;Vorname;Geburtsdatum;Abteilung;Eintritt;Mobil\n{pn};Imported;Ina;03.04.1990;EA 1;01.01.2010;01701234567\n{pn}x;Fehler;Fritz;kein Datum;EA 1;01.01.2010;\n";
        var lauf = await HochladenAsync(client, Importziel.Personen, "personen.csv", Encoding.UTF8.GetBytes(csv));

        var laufId = Guid.Parse(lauf.Split('/')[^1]);
        var seite = WebUtility.HtmlDecode(await client.GetStringAsync(lauf));
        Assert.Contains("<option value=\"Pers.-Nr.\" selected=\"selected\">", seite);

        var zuordnung = new Dictionary<string, string>
        {
            ["Zuordnung[Personalnummer]"] = "Pers.-Nr.", ["Zuordnung[Nachname]"] = "Name", ["Zuordnung[Vorname]"] = "Vorname",
            ["Zuordnung[Geburtsdatum]"] = "Geburtsdatum", ["Zuordnung[Abteilung]"] = "Abteilung", ["Zuordnung[Eintritt]"] = "Eintritt",
            ["Zuordnung[TelefonMobil]"] = "Mobil", ["VorlageName"] = "Test " + pn,
        };
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, lauf, "Probelauf", zuordnung)).StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Mitglieder.AnyAsync(m => m.Personalnummer == pn));
            var gespeichert = await db.Importlaeufe.SingleAsync(l => l.Id == laufId);
            Assert.Equal((ImportlaufStatus.Vorbereitet, 2, 1, 1), (gespeichert.Status, gespeichert.Zeilen, gespeichert.Angelegt, gespeichert.Fehlerhaft));
            Assert.Contains("kein Datum", gespeichert.Meldungen);
            Assert.NotNull(gespeichert.ProbelaufAm);
            Assert.True(await db.Importvorlagen.AnyAsync(v => v.Name == "Test " + pn));
        }

        seite = WebUtility.HtmlDecode(await (await PostAsync(client, lauf, "Uebernehmen", new() { ["Freigabe"] = "" })).Content.ReadAsStringAsync()
            + await client.GetStringAsync(lauf));
        Assert.Contains("Bitte die Freigabe vermerken", seite);

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, lauf, "Uebernehmen", new() { ["Freigabe"] = "BM und DSB per Mail" })).StatusCode);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = await db.Mitglieder.Include(x => x.Mitgliedschaften).SingleAsync(x => x.Personalnummer == pn);
            Assert.Equal(("Ina", "Imported", new DateOnly(1990, 4, 3), "01701234567"), (m.Vorname, m.Nachname, m.Geburtsdatum, m.TelefonMobil));
            Assert.Equal(new DateOnly(2010, 1, 1), Assert.Single(m.Mitgliedschaften).GueltigAb);
            Assert.False(await db.Mitglieder.AnyAsync(x => x.Personalnummer == pn + "x"));
            var abgeschlossen = await db.Importlaeufe.SingleAsync(l => l.Id == laufId);
            Assert.Equal((ImportlaufStatus.Uebernommen, "BM und DSB per Mail"), (abgeschlossen.Status, abgeschlossen.Freigabe));
            Assert.Empty(abgeschlossen.Inhalt);
        }

        // Lehrgänge zur eben angelegten Person; ohne „gültig bis“ gilt die Gültigkeit aus dem Katalog.
        var lehrgaenge = await HochladenAsync(client, Importziel.Qualifikationen, "lehrgaenge.csv",
            Encoding.UTF8.GetBytes($"Personalnummer,Lehrgang,Datum\n{pn},Fahrerlaubnis Klasse C,15.03.2020\n{pn},Gibt es nicht,15.03.2020\n"));
        await PostAsync(client, lehrgaenge, "Probelauf", new()
        {
            ["Zuordnung[Personalnummer]"] = "Personalnummer", ["Zuordnung[Lehrgang]"] = "Lehrgang", ["Zuordnung[ErworbenAm]"] = "Datum",
        });
        await PostAsync(client, lehrgaenge, "Uebernehmen", new() { ["Freigabe"] = "wie oben" });
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var nachweis = await db.QualifikationNachweise.Include(q => q.Qualifikation).SingleAsync(q => q.Mitglied!.Personalnummer == pn);
            Assert.Equal(("FS-C", new DateOnly(2025, 3, 15)), (nachweis.Qualifikation!.Code, nachweis.GueltigBis));
        }
    }

    [Fact]
    public async Task Ohne_Recht_kein_Zugang()
    {
        var antwort = await Client(Rollen.Mitglied).GetAsync("/Verwaltung/Import");
        Assert.Equal(HttpStatusCode.Forbidden, antwort.StatusCode);
    }

    [Fact]
    public async Task Loeschlauf_verwirft_liegengebliebene_Dateien()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alt = new Importlauf { Dateiname = "alt.csv", Inhalt = [1, 2, 3] };
        db.Importlaeufe.Add(alt);
        await db.SaveChangesAsync();
        await db.Importlaeufe.Where(l => l.Id == alt.Id).ExecuteUpdateAsync(u => u.SetProperty(l => l.ErstelltAm, DateTimeOffset.UtcNow.AddDays(-30)));

        await scope.ServiceProvider.GetRequiredService<Fevad.Infrastructure.Datenschutz.Loeschlauf>().AusfuehrenAsync(DateTimeOffset.UtcNow, null, default);
        var nachher = await db.Importlaeufe.AsNoTracking().SingleAsync(l => l.Id == alt.Id);
        Assert.Equal(ImportlaufStatus.Verworfen, nachher.Status);
        Assert.Empty(nachher.Inhalt);
    }

    private async Task<Importergebnis> ImportierenAsync(Importziel ziel, string csv, bool probelauf = false)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tabelle = Tabellenleser.Lesen(Encoding.UTF8.GetBytes(csv), "x.csv");
        var lauf = new Importlauf
        {
            Ziel = ziel, Dateiname = "x.csv", Inhalt = Encoding.UTF8.GetBytes(csv),
            Zuordnung = Importdienst.ZuordnungSchreiben(Importdienst.Vorschlag(ziel, tabelle.Spalten).ToDictionary(z => z.Key, z => (string?)z.Value)),
        };
        db.Importlaeufe.Add(lauf);
        await db.SaveChangesAsync();
        return await scope.ServiceProvider.GetRequiredService<Importdienst>().AusfuehrenAsync(lauf.Id, probelauf, "test", default);
    }

    [Fact]
    public async Task Geraete_Atemschutz_Einsaetze_und_Teilnahmen()
    {
        var kennung = Guid.NewGuid().ToString("N")[..6];
        var pn = "P" + kennung;
        await ImportierenAsync(Importziel.Personen, $"Personalnummer;Nachname;Vorname;Geburtsdatum\n{pn};Gerät;Gerd;01.01.1980\n");

        var geraete = await ImportierenAsync(Importziel.Geraete,
            $"Inv.-Nr.;Gerät;Art;Hersteller;Baujahr;Preis;Prüfdatum\nL-{kennung};Steckleiter {kennung};Tragbare Leiter;Hailo;2015;1.234,50 €;10.05.2026\nX-{kennung};Unbekannt;Gibt es nicht;;;;\n");
        Assert.Equal((1, 1), (geraete.Angelegt, geraete.Fehlerhaft));

        var atemschutz = await ImportierenAsync(Importziel.Atemschutz,
            $"Personalnummer;Vorsorge am;Vorsorge gültig bis;Belastungsübung;Unterweisung\n{pn};01.02.2025;01.02.2028;15.03.2026;20.01.2026\n{pn};01.02.2025;;;\n");
        Assert.Equal((3, 1), (atemschutz.Angelegt, atemschutz.Fehlerhaft));

        var nummer = $"2019-{kennung}";
        var einsaetze = await ImportierenAsync(Importziel.Einsaetze,
            $"Einsatz-Nr.;Datum;Uhrzeit;Endzeit;Stichwort;Einsatzart;Einsatzleiter\n{nummer};24.12.2019;23:30;01:15;B2;Brand;{pn}\n");
        Assert.Equal(1, einsaetze.Angelegt);
        var teilnahmen = await ImportierenAsync(Importziel.Einsatzteilnahmen, $"Einsatz;PersNr;Funktion\n{nummer};{pn};Maschinist\n{nummer};unbekannt;\n");
        Assert.Equal((1, 1), (teilnahmen.Angelegt, teilnahmen.Fehlerhaft));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var geraet = await db.Geraete.Include(g => g.Pruefungen).Include(g => g.Material).SingleAsync(g => g.Inventarnummer == $"L-{kennung}");
        Assert.Equal((2015, 1234.50m, "Hailo"), (geraet.Baujahr, geraet.Anschaffungspreis, geraet.Material!.Hersteller));
        var pruefung = Assert.Single(geraet.Pruefungen);
        Assert.Equal((Fevad.Domain.Technik.Pruefart.Funktionspruefung, new DateOnly(2027, 5, 10)), (pruefung.Pruefart, pruefung.NaechsteFaellig));

        var mitglied = await db.Mitglieder.SingleAsync(m => m.Personalnummer == pn);
        var vorsorge = await db.AtemschutzVorsorgen.SingleAsync(v => v.MitgliedId == mitglied.Id);
        Assert.Equal(new DateOnly(2028, 2, 1), vorsorge.NaechsteFaellig);
        Assert.Equal(2, await db.AtemschutzNachweise.CountAsync(n => n.MitgliedId == mitglied.Id && n.BestaetigtWartAm == null));

        var einsatz = await db.Einsaetze.Include(e => e.Teilnahmen).Include(e => e.StatistikKategorie).SingleAsync(e => e.Einsatznummer == nummer);
        Assert.Equal(Fevad.Domain.Einsaetze.EinsatzStatus.InBearbeitung, einsatz.Status);
        Assert.Equal(new DateTimeOffset(2019, 12, 24, 22, 30, 0, TimeSpan.Zero), einsatz.Alarmiert);
        Assert.Equal(new DateTimeOffset(2019, 12, 25, 0, 15, 0, TimeSpan.Zero), einsatz.Einsatzende);
        Assert.Equal(("BR", mitglied.Id), (einsatz.StatistikKategorie!.Code, einsatz.EinsatzleiterId));
        Assert.Equal("Maschinist", Assert.Single(einsatz.Teilnahmen).Funktion);

        // Nach der Übernahme stehen in den Meldungen keine Werte mehr.
        var lauf = await db.Importlaeufe.Where(l => l.Ziel == Importziel.Einsatzteilnahmen && l.Meldungen.Contains("Personalnummer")).OrderByDescending(l => l.ErstelltAm).FirstAsync();
        Assert.Contains("Personalnummer „…“ gibt es in FEVAD nicht.", lauf.Meldungen);
        Assert.DoesNotContain("unbekannt", lauf.Meldungen);
    }

    /// <summary>Kleinste XLSX-Datei: eine Überschriftenzeile mit Lücke in B1 und eine Datenzeile mit Zahl und Datum als Seriennummer.</summary>
    private static byte[] Xlsx()
    {
        using var speicher = new MemoryStream();
        using (var zip = new ZipArchive(speicher, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Datei(string pfad, string inhalt)
            {
                using var w = new StreamWriter(zip.CreateEntry(pfad).Open(), new UTF8Encoding(false));
                w.Write(inhalt);
            }

            const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            Datei("xl/workbook.xml", $"<workbook xmlns=\"{ns}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"A\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Datei("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"x\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Datei("xl/sharedStrings.xml", $"<sst xmlns=\"{ns}\"><si><t>Personalnummer</t></si><si><r><t>Ein</t></r><r><t>tritt</t></r></si></sst>");
            Datei("xl/worksheets/sheet1.xml", $"<worksheet xmlns=\"{ns}\"><sheetData>"
                + "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"C1\" t=\"s\"><v>1</v></c></row>"
                + "<row r=\"2\"><c r=\"A2\"><v>4711</v></c><c r=\"C2\"><v>45000</v></c></row>"
                + "</sheetData></worksheet>");
        }

        return speicher.ToArray();
    }
}
