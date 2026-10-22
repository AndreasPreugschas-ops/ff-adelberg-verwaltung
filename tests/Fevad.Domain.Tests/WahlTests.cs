using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class WahlTests
{
    private static Wahl Wahl(Wahlverfahren verfahren, int sitze, params int[] stimmen)
    {
        var wahl = new Wahl { Verfahren = verfahren, Sitze = sitze, FunktionId = Guid.CreateVersion7() };
        wahl.Bewerber.AddRange(stimmen.Select(s => new WahlBewerber { MitgliedId = Guid.CreateVersion7(), Stimmen = s }));
        return wahl;
    }

    [Fact]
    public void Absolute_Mehrheit_braucht_mehr_als_die_Haelfte()
    {
        var wahl = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 16, 10, 4);

        var ergebnis = wahl.ErgebnisFeststellen();

        Assert.Equal(16, Assert.Single(ergebnis.Gewaehlte).Stimmen);
        Assert.Equal(0, ergebnis.OffeneSitze);
        Assert.Empty(ergebnis.Hinweise);
    }

    [Fact]
    public void Genau_die_Haelfte_reicht_nicht_Stichwahl()
    {
        var wahl = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 15, 10, 5);

        var ergebnis = wahl.ErgebnisFeststellen();

        Assert.Empty(ergebnis.Gewaehlte);
        Assert.Contains("Stichwahl", Assert.Single(ergebnis.Hinweise));
    }

    [Fact]
    public void Gueltige_Stimmen_aus_dem_Protokoll_zaehlen_statt_der_Summe()
    {
        // 20 Stimmen für den einzigen Bewerber, aber 41 gültige Stimmzettel (21 Nein).
        var wahl = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 20);
        wahl.GueltigeStimmen = 41;

        Assert.Empty(wahl.ErgebnisFeststellen().Gewaehlte);
    }

    [Fact]
    public void Mehrheitswahl_besetzt_die_Sitze_nach_Stimmen_Rest_sind_Ersatzmitglieder()
    {
        var wahl = Wahl(Wahlverfahren.Mehrheitswahl, 4, 30, 25, 22, 18, 12, 0);

        var ergebnis = wahl.ErgebnisFeststellen();

        Assert.Equal([30, 25, 22, 18], ergebnis.Gewaehlte.Select(b => b.Stimmen).OrderDescending());
        Assert.True(wahl.Bewerber.Single(b => b.Stimmen == 12).Ersatzmitglied);
        Assert.False(wahl.Bewerber.Single(b => b.Stimmen == 0).Ersatzmitglied);
        Assert.Equal(5, wahl.Bewerber.Single(b => b.Stimmen == 12).Platz);
    }

    [Fact]
    public void Gleichstand_um_den_letzten_Sitz_bleibt_offen()
    {
        var wahl = Wahl(Wahlverfahren.Mehrheitswahl, 4, 30, 25, 22, 18, 18);

        var ergebnis = wahl.ErgebnisFeststellen();

        Assert.Equal(3, ergebnis.Gewaehlte.Count);
        Assert.Equal(1, ergebnis.OffeneSitze);
        Assert.Contains("Los", Assert.Single(ergebnis.Hinweise));
        Assert.Equal([4, 4], wahl.Bewerber.Where(b => b.Stimmen == 18).Select(b => b.Platz!.Value));
    }

    [Fact]
    public void Amtszeit_endet_am_Tag_vor_dem_Jahrestag()
    {
        var wahl = new Wahl { Amtsbeginn = new(2026, 4, 1), AmtszeitJahre = 5 };

        Assert.Equal(new DateOnly(2031, 3, 31), wahl.Amtsende);
    }

    [Fact]
    public void Uebernahme_nur_mit_Zustimmung_des_Gemeinderats_und_angenommener_Wahl()
    {
        var wahl = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 30, 5);
        wahl.Amtsbeginn = new(2026, 4, 1);
        wahl.AmtszeitJahre = 5;
        wahl.ZustimmungGemeinderatErforderlich = true;
        wahl.ErgebnisFeststellen();

        Assert.Throws<InvalidOperationException>(() => wahl.FunktionenErzeugen(new(2026, 3, 13)));

        wahl.ZustimmungGemeinderatAm = new(2026, 3, 26);
        Assert.Empty(wahl.FunktionenErzeugen(new(2026, 3, 13)));

        wahl.Bewerber.Single(b => b.Gewaehlt).AngenommenAm = new(2026, 3, 13);
        var funktion = Assert.Single(wahl.FunktionenErzeugen(new(2026, 3, 13)));
        Assert.Equal(FunktionArt.Wahl, funktion.Art);
        Assert.Equal(new DateOnly(2026, 4, 1), funktion.GueltigAb);
        Assert.Equal(new DateOnly(2031, 3, 31), funktion.GueltigBis);
        Assert.Equal(new DateOnly(2026, 3, 13), funktion.Beschlussdatum);
    }
}
