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
        wahl.Stimmberechtigte = 30;

        var ergebnis = wahl.ErgebnisFeststellen();

        Assert.Equal(16, Assert.Single(ergebnis.Gewaehlte).Stimmen);
        Assert.Equal(0, ergebnis.OffeneSitze);
        Assert.Empty(ergebnis.Hinweise);
    }

    [Fact]
    public void Genau_die_Haelfte_reicht_nicht_Stichwahl()
    {
        var wahl = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 15, 10, 5);
        wahl.Stimmberechtigte = 30;

        var ergebnis = wahl.ErgebnisFeststellen();

        Assert.Empty(ergebnis.Gewaehlte);
        Assert.Contains("Stichwahl", Assert.Single(ergebnis.Hinweise));
    }

    [Fact]
    public void Ohne_Zahl_der_Anwesenden_wird_keine_absolute_Mehrheit_festgestellt()
    {
        // 15 von 25 gültigen Stimmen wären die Mehrheit, bei 31 Anwesenden aber nicht.
        var wahl = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 15, 10);
        wahl.GueltigeStimmen = 25;

        var ergebnis = wahl.ErgebnisFeststellen();

        Assert.Empty(ergebnis.Gewaehlte);
        Assert.Contains("anwesenden Wahlberechtigten", Assert.Single(ergebnis.Hinweise));
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
        wahl.Stimmberechtigte = 35;
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

    [Fact]
    public void Mehrheit_bezieht_sich_auf_die_anwesenden_Wahlberechtigten()
    {
        // 31 anwesend, 16 Ja-Stimmen reichen, 15 nicht; Enthaltungen zählen wie Nein (Satzung § 15 Abs. 3).
        var knapp = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 16, 9);
        knapp.Stimmberechtigte = 31;
        Assert.Single(knapp.ErgebnisFeststellen().Gewaehlte);

        var zuWenig = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 15, 10);
        zuWenig.Stimmberechtigte = 31;
        zuWenig.GueltigeStimmen = 25;
        Assert.Empty(zuWenig.ErgebnisFeststellen().Gewaehlte);
    }

    [Fact]
    public void Stichwahl_entscheidet_die_einfache_Mehrheit()
    {
        var wahl = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 14, 12);
        wahl.Wahlgang = 2;
        wahl.Stimmberechtigte = 31;

        Assert.Equal(14, Assert.Single(wahl.ErgebnisFeststellen().Gewaehlte).Stimmen);
    }

    [Fact]
    public void Stichwahl_nur_zwischen_zwei_Bewerbern()
    {
        var wahl = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 14, 12, 5);
        wahl.Wahlgang = 2;
        wahl.Stimmberechtigte = 31;

        var ergebnis = wahl.ErgebnisFeststellen();

        Assert.Empty(ergebnis.Gewaehlte);
        Assert.Contains("beiden Bewerber", Assert.Single(ergebnis.Hinweise));
    }

    [Fact]
    public void Gleichstand_in_der_Stichwahl_entscheidet_das_Los()
    {
        var wahl = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 13, 13);
        wahl.Wahlgang = 2;

        var ergebnis = wahl.ErgebnisFeststellen();

        Assert.Empty(ergebnis.Gewaehlte);
        Assert.Contains("Los", Assert.Single(ergebnis.Hinweise));
    }

    [Fact]
    public void Einziger_Bewerber_braucht_auch_im_zweiten_Wahlgang_die_absolute_Mehrheit()
    {
        var wahl = Wahl(Wahlverfahren.AbsoluteMehrheit, 1, 15);
        wahl.Wahlgang = 2;
        wahl.Stimmberechtigte = 31;

        var ergebnis = wahl.ErgebnisFeststellen();

        Assert.Empty(ergebnis.Gewaehlte);
        Assert.Contains("zweiter Wahlgang", Assert.Single(ergebnis.Hinweise));
    }
}
