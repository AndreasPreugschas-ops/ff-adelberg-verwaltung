using Fevad.Domain.Einsaetze;

namespace Fevad.Domain.Tests;

public class BmaKalkulationTests
{
    [Fact]
    public void Serienvorgang_traegt_Tatbestand_Betreiber_und_Sachverhalt()
    {
        var objekt = new BmaObjekt { Bezeichnung = "Schule", Aufschaltnummer = "123", BetreiberName = "Gemeinde X", BetreiberAnschrift = "Rathaus 1" };
        var v = objekt.SerienVorgang(Guid.NewGuid(), new DateOnly(2030, 3, 4), 3);
        Assert.Equal(Kostentatbestand.Abs1Nr6Brandmeldeanlage, v.Tatbestand);
        var s = Assert.Single(v.Schuldner);
        Assert.Equal((SchuldnerArt.Betreiber, "Gemeinde X", "Rathaus 1"), (s.Art, s.Name, s.Anschrift));
        Assert.Equal("Fehlalarm der Brandmeldeanlage Schule (Aufschaltnummer 123) am 04.03.2030, 3. Fehlalarm des Objekts im Jahr 2030.", v.Sachverhalt);
        Assert.Empty(v.Pruefen(KostenersatzErgebnis.Kostenpflichtig, null, null));
    }

    [Fact]
    public void Stundensatz_nach_Paragraf_34_Abs_5()
    {
        var k = new Satzkalkulation
        {
            KalkuliertAm = new DateOnly(2030, 5, 1), Kosten1 = 10000, Kosten2 = 11000, Kosten3 = 12000, Kosten4 = 13000,
            Angehoerige = 30, StundenJeAngehoerigem = 80, Entschaedigungssatz = 9,
        };
        // 11.500 € / 2.400 h = 4,79 € (gerundet) + 9 €.
        Assert.Equal((11500m, 4.79m, 13.79m), (k.Mittelwert, k.KostenJeStunde, k.Stundensatz));
        Assert.Equal(new DateOnly(2034, 5, 1), k.WiedervorlageAm(4));
        Assert.Equal(9m, new Satzkalkulation { Kosten1 = 100, Entschaedigungssatz = 9 }.Stundensatz);
    }
}
