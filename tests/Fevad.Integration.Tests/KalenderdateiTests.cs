using Fevad.Domain.Dienstbetrieb;
using Fevad.Web.Infrastruktur;

namespace Fevad.Integration.Tests;

public class KalenderdateiTests
{
    private static readonly DateTimeOffset Beginn = new(2026, 11, 5, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Termin_wird_mit_UTC_Zeiten_und_maskierten_Texten_geschrieben()
    {
        var dienst = new Dienst { Titel = "Übung TH; Pkw, Lkw", Beginn = Beginn, Ende = Beginn.AddHours(2), Ort = "Gerätehaus", Status = DienstStatus.Geplant };

        var ics = Kalenderdatei.Erzeugen("Test", [new(dienst, RueckmeldungAntwort.Zusage)], Beginn);

        Assert.Contains("DTSTART:20261105T183000Z\r\n", ics);
        Assert.Contains("DTEND:20261105T203000Z\r\n", ics);
        Assert.Contains(@"SUMMARY:Übung TH\; Pkw\, Lkw", ics);
        Assert.Contains("STATUS:CONFIRMED", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
    }

    [Fact]
    public void Abgesagter_Dienst_wird_storniert()
    {
        var dienst = new Dienst { Titel = "Übung", Beginn = Beginn, Ende = Beginn.AddHours(2), Status = DienstStatus.Abgesagt };
        var ics = Kalenderdatei.Erzeugen("Test", [new(dienst, null)], Beginn);
        Assert.Contains("STATUS:CANCELLED", ics);
        Assert.Contains("SUMMARY:Abgesagt: Übung", ics);
    }

    [Fact]
    public void Lange_Zeilen_werden_nach_75_Byte_gefaltet()
    {
        var dienst = new Dienst { Titel = new string('Ü', 60), Beginn = Beginn, Ende = Beginn.AddHours(2) };
        var ics = Kalenderdatei.Erzeugen("Test", [new(dienst, null)], Beginn);
        Assert.All(ics.Split("\r\n"), z => Assert.True(System.Text.Encoding.UTF8.GetByteCount(z) <= 75, z));
        Assert.Contains(new string('Ü', 60), ics.Replace("\r\n ", ""));
    }
}
