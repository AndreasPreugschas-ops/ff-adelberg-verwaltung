using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;

namespace Fevad.Domain.Tests;

/// <summary>Jahreskreis der Führungspflichten (P5: V51).</summary>
public class FuehrungspflichtTests
{
    private static Fuehrungspflicht ZFeu() => new() { Code = "ZFEU", Bezeichnung = "Z-Feu-Antrag", Monat = 2, Tag = 15, VorlaufTage = 60 };

    [Theory]
    [InlineData("2026-12-16", null)]
    [InlineData("2026-12-17", "2027-02-15")]
    [InlineData("2027-02-15", "2027-02-15")]
    [InlineData("2027-04-16", "2027-02-15")]
    [InlineData("2027-04-17", null)]
    [InlineData("2027-12-17", "2028-02-15")]
    public void Aktueller_Termin_vom_Vorlauf_bis_zum_Nachlauf(string heute, string? erwartet) =>
        Assert.Equal(erwartet is null ? null : DateOnly.Parse(erwartet), ZFeu().AktuellerTermin(DateOnly.Parse(heute)));

    [Fact]
    public void Ampel_gelb_im_Vorlauf_rot_danach_weg_wenn_erledigt()
    {
        var p = ZFeu();
        Assert.Null(p.Ampel(new DateOnly(2026, 12, 1)));
        Assert.Equal(Fristampel.Gelb, p.Ampel(new DateOnly(2027, 1, 10)));
        Assert.Equal(Fristampel.Rot, p.Ampel(new DateOnly(2027, 2, 16)));
        p.Erledigungen.Add(new PflichtErledigung { Stichtag = new DateOnly(2027, 2, 15), ErledigtAm = new DateOnly(2027, 2, 10) });
        Assert.Null(p.Ampel(new DateOnly(2027, 2, 16)));
        p.Aktiv = false;
        Assert.Null(p.Ampel(new DateOnly(2028, 2, 16)));
    }

    [Fact]
    public void Halbjaehrlich_zwei_Termine_und_Monatsende()
    {
        var p = new Fuehrungspflicht { Code = "FSK", Bezeichnung = "Führerscheinkontrolle", Turnus = PflichtTurnus.Halbjaehrlich, Monat = 8, Tag = 31 };
        Assert.Equal([new DateOnly(2027, 2, 28), new DateOnly(2027, 8, 31)], p.StichtageIm(2027));
        Assert.Equal(new DateOnly(2027, 8, 31), p.NaechsterTermin(new DateOnly(2027, 3, 1)));
    }

    [Fact]
    public void Mehrjaehrlich_nur_in_Jahren_im_Abstand_zum_Bezugsjahr()
    {
        var p = new Fuehrungspflicht
        {
            Code = "BPL", Bezeichnung = "Bedarfsplan", Turnus = PflichtTurnus.Mehrjaehrlich, IntervallJahre = 5, Bezugsjahr = 2031, Monat = 3, Tag = 31,
            VorlaufTage = 180,
        };
        Assert.Empty(p.StichtageIm(2030));
        Assert.Single(p.StichtageIm(2026));
        Assert.Equal(new DateOnly(2031, 3, 31), p.NaechsterTermin(new DateOnly(2027, 1, 1)));
        Assert.Null(p.AktuellerTermin(new DateOnly(2027, 1, 1)));
        Assert.Equal(new DateOnly(2031, 3, 31), p.AktuellerTermin(new DateOnly(2030, 10, 2)));
    }

    [Fact]
    public void Verantwortliche_Funktionen_aus_Codes() =>
        Assert.Equal(["KDT", "SF"], new Fuehrungspflicht { Code = "X", Bezeichnung = "x", Verantwortlich = " KDT | SF |" }.Funktionscodes);
}
