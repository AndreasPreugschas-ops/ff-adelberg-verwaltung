using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class ZuschussrechnerTests
{
    private static readonly DateOnly Erster = new(2027, 7, 30);
    private static readonly DateOnly Letzter = new(2027, 8, 1);

    [Fact]
    public void Rechnet_Teilnehmertage_mit_Altersgrenzen_und_Frist()
    {
        var geber = new Zuschussgeber { Name = "KJR", SatzJeTeilnehmertag = 2.5m, MindestalterJahre = 6, HoechstalterJahre = 26, AntragsfristTage = 90 };
        var personen = new[]
        {
            new Zuschussrechner.Person(new DateOnly(2015, 1, 1), null, false),
            new Zuschussrechner.Person(new DateOnly(2014, 5, 1), 2, false),
            new Zuschussrechner.Person(new DateOnly(1990, 1, 1), null, false),
            new Zuschussrechner.Person(null, null, true),
        };

        var e = Zuschussrechner.Rechnen(geber, Erster, Letzter, personen);

        Assert.Equal((3, 2, 5), (e.Tage, e.Teilnehmer, e.Teilnehmertage));
        Assert.Equal(12.50m, e.Zuschuss);
        Assert.Equal(new DateOnly(2027, 10, 30), e.AntragBis);
        Assert.Empty(e.Hinweise);
    }

    [Fact]
    public void Ohne_Satz_oder_unter_der_Mindestdauer_kein_Betrag()
    {
        var leer = Zuschussrechner.Rechnen(new Zuschussgeber { Name = "Kreisjugendpflege" }, Erster, Letzter, [new(null, null, false)]);
        Assert.Null(leer.Zuschuss);
        Assert.Null(leer.AntragBis);
        Assert.Equal(2, leer.Hinweise.Count);

        var kurz = Zuschussrechner.Rechnen(new Zuschussgeber { Name = "X", SatzJeTeilnehmertag = 1, MindestTage = 4, AntragsfristTage = 30, BetreuerZaehlen = true },
            Erster, Letzter, [new(null, null, true)]);
        Assert.Equal(1, kurz.Teilnehmer);
        Assert.Null(kurz.Zuschuss);
        Assert.Contains(kurz.Hinweise, h => h.Contains("ab 4"));
    }

    [Fact]
    public void Bei_Uebernachtung_genuegt_der_Verzicht_aufs_Zeugnis_nicht()
    {
        Assert.True(Fuehrungszeugnisregeln.Einsetzbar(Zeugnisstatus.NichtNoetig));
        Assert.False(Fuehrungszeugnisregeln.EinsetzbarBeiUebernachtung(Zeugnisstatus.NichtNoetig));
        Assert.True(Fuehrungszeugnisregeln.EinsetzbarBeiUebernachtung(Zeugnisstatus.Bald));
        Assert.True(Jugenddienstarten.Ist(DienstArt.Zeltlager));
        Assert.False(Jugenddienstarten.Ist(DienstArt.Uebung));
    }
}

public class WartelisteTests
{
    [Fact]
    public void Wiedervorlage_beim_Mindestalter_und_Loeschung_ein_Jahr_danach()
    {
        var heute = new DateOnly(2026, 10, 26);
        Assert.Equal(new DateOnly(2028, 3, 1), Interessent.WiedervorlageAm(new DateOnly(2018, 3, 1), 10, heute));
        Assert.Equal(heute, Interessent.WiedervorlageAm(new DateOnly(2014, 3, 1), 10, heute));

        var i = new Interessent { Vorname = "A", Nachname = "B", Ansprechpartner = "C", Wiedervorlage = new DateOnly(2028, 3, 1) };
        Assert.Equal(new DateOnly(2029, 3, 1), i.LoeschenAb(12));
        i.ErledigtAm = new DateOnly(2027, 1, 10);
        Assert.Equal(new DateOnly(2028, 1, 10), i.LoeschenAb(12));
    }
}
