using Fevad.Domain.Dokumente;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;

namespace Fevad.Domain.Tests;

public class SitzungTests
{
    [Theory]
    [InlineData(2026, 3, 1, true)]
    [InlineData(2026, 3, 2, false)]
    public void Einladungsfrist_14_Tage(int jahr, int monat, int tag, bool eingehalten) =>
        Assert.Equal(eingehalten, Sitzung.EinladungsfristEingehalten(new DateOnly(jahr, monat, tag), new DateOnly(2026, 3, 15), 14));

    [Fact]
    public void Ohne_Einladungsdatum_keine_Aussage() =>
        Assert.Null(Sitzung.EinladungsfristEingehalten(null, new DateOnly(2026, 3, 15), 14));

    [Theory]
    [InlineData(2, 5, false)]
    [InlineData(3, 5, true)]
    [InlineData(2, 4, true)]
    [InlineData(0, 0, false)]
    public void Beschlussfaehig_ab_der_Haelfte(int anwesend, int stimmberechtigt, bool erwartet) =>
        Assert.Equal(erwartet, Sitzung.Beschlussfaehig(anwesend, stimmberechtigt));

    [Theory]
    [InlineData(3, 2, BeschlussErgebnis.Angenommen)]
    [InlineData(2, 2, BeschlussErgebnis.Abgelehnt)]
    [InlineData(1, 4, BeschlussErgebnis.Abgelehnt)]
    public void Stimmengleichheit_ist_abgelehnt(int ja, int nein, BeschlussErgebnis erwartet) =>
        Assert.Equal(erwartet, Beschluss.AusStimmen(ja, nein));

    [Fact]
    public void Beschlusskennung_mit_Jahr_und_zweistelliger_Nummer() =>
        Assert.Equal("2026/07", new Beschluss { Jahr = 2026, Nummer = 7, Gegenstand = "x" }.Kennung);

    [Theory]
    [InlineData(5, Fristampel.Gelb)]
    [InlineData(7, Fristampel.Gelb)]
    [InlineData(8, Fristampel.Rot)]
    public void Wahlniederschrift_binnen_einer_Woche(int tageNachSitzung, Fristampel erwartet)
    {
        var sitzung = new Sitzung { Titel = "HV", Status = SitzungStatus.Durchgefuehrt, Wahlen = [new Wahl()] };
        var tag = new DateOnly(2026, 3, 15);
        Assert.Equal(erwartet, sitzung.WahlniederschriftAmpel(tag.AddDays(tageNachSitzung), tag, 7));
    }

    [Fact]
    public void Wahlniederschrift_ohne_Wahl_oder_vorgelegt_ohne_Ampel()
    {
        var tag = new DateOnly(2026, 3, 15);
        Assert.Null(new Sitzung { Titel = "HV", Status = SitzungStatus.Durchgefuehrt }.WahlniederschriftAmpel(tag.AddDays(10), tag, 7));
        var vorgelegt = new Sitzung { Titel = "HV", Status = SitzungStatus.Durchgefuehrt, Wahlen = [new Wahl()], DemBuergermeisterVorgelegtAm = tag.AddDays(2) };
        Assert.Null(vorgelegt.WahlniederschriftAmpel(tag.AddDays(10), tag, 7));
        Assert.Null(new Sitzung { Titel = "HV", Status = SitzungStatus.Geplant, Wahlen = [new Wahl()] }.WahlniederschriftAmpel(tag.AddDays(10), tag, 7));
    }

    [Fact]
    public void Beschlussstand_aus_Ergebnis_Erledigung_und_Aufhebung()
    {
        var b = new Beschluss { Jahr = 2026, Nummer = 1, Gegenstand = "x", Ergebnis = BeschlussErgebnis.Angenommen };
        Assert.Equal(BeschlussStand.Gefasst, b.Stand);
        b.ErledigtAm = new DateOnly(2026, 4, 1);
        Assert.Equal(BeschlussStand.Umgesetzt, b.Stand);
        b.AufgehobenAm = new DateOnly(2026, 9, 1);
        Assert.Equal(BeschlussStand.Aufgehoben, b.Stand);
        Assert.Equal(BeschlussStand.NichtGefasst, new Beschluss { Jahr = 2026, Nummer = 2, Gegenstand = "y", Ergebnis = BeschlussErgebnis.Abgelehnt }.Stand);
    }

    [Theory]
    [InlineData(@"C:\Users\x\Dienstanweisung Atemschutz.pdf", "Dienstanweisung Atemschutz.pdf")]
    [InlineData("../../etc/passwd.txt", "passwd.txt")]
    [InlineData("Protokoll \"HV\" 2026.docx", "Protokoll HV 2026.docx")]
    public void Dateinamen_werden_bereinigt(string eingabe, string erwartet) =>
        Assert.Equal(erwartet, Dateitypen.Bereinigen(eingabe));

    [Theory]
    [InlineData("satzung.PDF", "application/pdf")]
    [InlineData("seite.html", null)]
    [InlineData("bild.svg", null)]
    [InlineData("makro.docm", null)]
    public void Nur_erlaubte_Dateitypen(string name, string? inhaltstyp) =>
        Assert.Equal(inhaltstyp, Dateitypen.Inhaltstyp(name));

    [Fact]
    public void Neue_Version_zaehlt_hoch()
    {
        var dokument = new Dokument { Titel = "Satzung" };
        Assert.Equal(1, dokument.NaechsteVersionsnummer);
        dokument.Versionen.Add(new DokumentVersion { Nummer = 1, Dateiname = "a.pdf", Inhaltstyp = "application/pdf", Pruefsumme = "" });
        dokument.Versionen.Add(new DokumentVersion { Nummer = 2, Dateiname = "b.pdf", Inhaltstyp = "application/pdf", Pruefsumme = "" });
        Assert.Equal(3, dokument.NaechsteVersionsnummer);
        Assert.Equal("b.pdf", dokument.AktuelleVersion!.Dateiname);
    }
}
