using Fevad.Domain.Kleiderkammer;

namespace Fevad.Domain.Tests;

public class KleiderkammerTests
{
    [Theory]
    [InlineData(" xl ", "XL")]
    [InlineData("52  lang", "52 LANG")]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    public void Groesse_wird_einheitlich_geschrieben(string? eingabe, string erwartet) =>
        Assert.Equal(erwartet, Groesse.Normalisieren(eingabe));

    [Fact]
    public void Ohne_Ziel_kein_Bedarf()
    {
        var bedarf = new Nachbestellbedarf(Mindestmenge: 0, ImLager: 3, FehlendeGrundausstattung: 0, ErsatzFuerAusgegebene: 0, LagerBaldAuszusondern: 0, BereitsBeschafft: 0);
        Assert.Equal(0, bedarf.Bedarf);
        Assert.Empty(bedarf.Gruende());
    }

    [Fact]
    public void Mindestmenge_minus_Lager()
    {
        var bedarf = new Nachbestellbedarf(5, 2, 0, 0, 0, 0);
        Assert.Equal(3, bedarf.Bedarf);
        Assert.Equal(3, bedarf.Offen);
        Assert.Contains("Mindestbestand 5, im Lager 2", bedarf.Gruende());
    }

    [Fact]
    public void Lagerstuecke_mit_ablaufender_Lebensdauer_zaehlen_nicht_als_verfuegbar()
    {
        // 2 im Lager, davon 1 bald auszusondern; 3 Mitglieder ohne Helm, 1 ausgegebener Helm läuft ab.
        var bedarf = new Nachbestellbedarf(Mindestmenge: 1, ImLager: 2, FehlendeGrundausstattung: 3, ErsatzFuerAusgegebene: 1, LagerBaldAuszusondern: 1, BereitsBeschafft: 2);
        Assert.Equal(5, bedarf.Zielbestand);
        Assert.Equal(1, bedarf.Verfuegbar);
        Assert.Equal(4, bedarf.Bedarf);
        Assert.Equal(2, bedarf.Offen);
        Assert.Equal(4, bedarf.Gruende().Count);
    }

    [Fact]
    public void Ueberbestand_und_Beschaffung_ergeben_nie_negativen_Bedarf()
    {
        var bedarf = new Nachbestellbedarf(1, 10, 0, 0, 0, 4);
        Assert.Equal(0, bedarf.Bedarf);
        Assert.Equal(0, bedarf.Offen);
    }
}
