using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Tests;

/// <summary>Deckungsprüfung der Unterweisung (V53) und Leistungsnachweis der Brandsicherheitswache (V25).</summary>
public class UnterweisungTests
{
    [Fact]
    public void Deckung_zaehlt_nur_die_Zielgruppe_und_meldet_die_Offenen()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var fremd = Guid.NewGuid();

        var ergebnis = Unterweisungsdeckung.Berechnen(Guid.NewGuid(), [a, b, c, a], [a, fremd, a]);

        Assert.Equal(3, ergebnis.Zielgruppe);
        Assert.Equal(1, ergebnis.Unterwiesen);
        Assert.Equal([b, c], ergebnis.Offen);
        Assert.False(ergebnis.Vollstaendig);
    }

    [Fact]
    public void Ampel_gelb_ab_dem_Warnmonat_rot_nach_dem_Jahr_gruen_wenn_vollstaendig()
    {
        var offen = Unterweisungsdeckung.Berechnen(Guid.NewGuid(), [Guid.NewGuid()], []);
        var fertig = Unterweisungsdeckung.Berechnen(Guid.NewGuid(), [], []);

        Assert.Null(offen.Ampel(new(2026, 9, 30), 2026, 10));
        Assert.Equal(Fristampel.Gelb, offen.Ampel(new(2026, 10, 1), 2026, 10));
        Assert.Equal(Fristampel.Rot, offen.Ampel(new(2027, 1, 2), 2026, 10));
        Assert.Equal(Fristampel.Gruen, fertig.Ampel(new(2027, 1, 2), 2026, 10));
    }

    [Fact]
    public void Wachleistung_rundet_je_Person_auf_angefangene_Stunden()
    {
        // 4:10 h wird zu 5 h, 3:00 h bleibt 3 h; 8 h × 16,50 € = 132 €.
        var ergebnis = Wachleistung.Berechnen([TimeSpan.FromMinutes(250), TimeSpan.FromHours(3), TimeSpan.Zero], 60, 16.50m, 0);

        Assert.Equal(2, ergebnis.Personen);
        Assert.Equal(8m, ergebnis.Personenstunden);
        Assert.Equal(132m, ergebnis.Betrag);
        Assert.Null(ergebnis.AnteilKasse);
    }

    [Fact]
    public void Wachleistung_ohne_Satz_ohne_Betrag_und_Anteil_nur_mit_Prozent()
    {
        Assert.Null(Wachleistung.Berechnen([TimeSpan.FromHours(2)], 60, null, 10).Betrag);
        Assert.Equal(3.30m, Wachleistung.Berechnen([TimeSpan.FromHours(2)], 60, 16.50m, 10).AnteilKasse);
    }
}
