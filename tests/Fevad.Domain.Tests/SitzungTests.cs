using Fevad.Domain.Dokumente;
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
