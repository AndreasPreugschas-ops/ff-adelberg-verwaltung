using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class MitgliedTests
{
    private static Mitglied Neu(DateOnly geburtsdatum) =>
        new() { Personalnummer = "1", Vorname = "Max", Nachname = "Muster", Geburtsdatum = geburtsdatum };

    [Theory]
    [InlineData("2008-10-22", "2026-10-22", 18)]
    [InlineData("2008-10-23", "2026-10-22", 17)]
    [InlineData("1961-10-22", "2026-10-22", 65)]
    [InlineData("2008-02-29", "2026-02-28", 17)]
    [InlineData("2008-02-29", "2026-03-01", 18)]
    public void Alter_wird_am_Stichtag_korrekt_berechnet(string geburt, string stichtag, int erwartet) =>
        Assert.Equal(erwartet, Neu(DateOnly.Parse(geburt)).AlterAm(DateOnly.Parse(stichtag)));

    [Fact]
    public void Aktuelle_Mitgliedschaft_ist_die_offene_juengste()
    {
        var mitglied = Neu(new DateOnly(1990, 1, 1));
        var jugend = new Mitgliedschaft { GueltigAb = new(2004, 1, 1), GueltigBis = new(2007, 12, 31), Status = MitgliedStatus.Aktiv };
        var einsatz = new Mitgliedschaft { GueltigAb = new(2008, 1, 1), Status = MitgliedStatus.Aktiv };
        mitglied.Mitgliedschaften.AddRange([jugend, einsatz]);

        Assert.Same(jugend, mitglied.AktuelleMitgliedschaft(new DateOnly(2006, 6, 1)));
        Assert.Same(einsatz, mitglied.AktuelleMitgliedschaft(new DateOnly(2026, 10, 22)));
        Assert.Null(mitglied.AktuelleMitgliedschaft(new DateOnly(2003, 1, 1)));
    }

    [Fact]
    public void Probezeit_endet_nach_zwoelf_Monaten()
    {
        var mitgliedschaft = new Mitgliedschaft { GueltigAb = new(2026, 3, 15) };
        Assert.Equal(new DateOnly(2027, 3, 15), mitgliedschaft.ProbezeitEnde(12));
    }

    [Fact]
    public void Aktueller_Dienstgrad_ist_der_letzte_bis_zum_Stichtag()
    {
        var mitglied = Neu(new DateOnly(1990, 1, 1));
        var fm = new DienstgradZuordnung { Ab = new(2010, 1, 1) };
        var ofm = new DienstgradZuordnung { Ab = new(2015, 1, 1) };
        mitglied.Dienstgrade.AddRange([ofm, fm]);

        Assert.Same(fm, mitglied.AktuellerDienstgrad(new DateOnly(2012, 1, 1)));
        Assert.Same(ofm, mitglied.AktuellerDienstgrad(new DateOnly(2026, 1, 1)));
    }
}
