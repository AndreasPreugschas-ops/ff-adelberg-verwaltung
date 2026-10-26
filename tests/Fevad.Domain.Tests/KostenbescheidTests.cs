using Fevad.Domain.Einsaetze;

namespace Fevad.Domain.Tests;

public class KostenbescheidTests
{
    private static Kostenbescheid Mit(params (BescheidSchritt Schritt, int Tag, decimal? Betrag)[] schritte)
    {
        var b = new Kostenbescheid();
        var i = 0;
        foreach (var (schritt, tag, betrag) in schritte)
        {
            b.Ereignisse.Add(new BescheidEreignis { Schritt = schritt, Datum = new DateOnly(2030, 1, 1).AddDays(tag), Betrag = betrag, ErfasstAm = DateTimeOffset.UnixEpoch.AddMinutes(i++) });
        }

        return b;
    }

    [Fact]
    public void Festsetzung_ist_bei_vorlaeufigen_Saetzen_gesperrt()
    {
        var b = Mit();
        Assert.Contains("vorläufige Sätze", b.Pruefen(BescheidSchritt.Festsetzung, 100, vorlaeufig: true));
        Assert.Null(b.Pruefen(BescheidSchritt.Festsetzung, 100, vorlaeufig: false));
        Assert.NotNull(b.Pruefen(BescheidSchritt.Zahlung, 100, false));
    }

    [Fact]
    public void Widerspruch_mit_Abhilfe_ersetzt_den_Betrag()
    {
        var b = Mit((BescheidSchritt.Festsetzung, 0, 300), (BescheidSchritt.Bekanntgabe, 1, null), (BescheidSchritt.Widerspruch, 10, null));
        Assert.Equal(BescheidStand.Widerspruch, b.Stand);
        Assert.NotNull(b.Pruefen(BescheidSchritt.Klage, null, false));

        b.Ereignisse.Add(new BescheidEreignis { Schritt = BescheidSchritt.Abhilfe, Datum = new DateOnly(2030, 2, 1), Betrag = 200, ErfasstAm = DateTimeOffset.UnixEpoch.AddHours(1) });
        Assert.Equal((BescheidStand.Bekanntgegeben, 200m, 200m), (b.Stand, b.Betrag!.Value, b.Offen));
        Assert.Equal("Es ist kein Widerspruch offen.", b.Pruefen(BescheidSchritt.Abhilfe, 100, false));
    }

    [Fact]
    public void Zahlung_Niederschlagung_und_volle_Abhilfe_erledigen()
    {
        var bezahlt = Mit((BescheidSchritt.Festsetzung, 0, 100), (BescheidSchritt.Bekanntgabe, 1, null), (BescheidSchritt.Zahlung, 20, 60), (BescheidSchritt.Zahlung, 30, 40));
        Assert.Equal((BescheidStand.Bezahlt, true), (bezahlt.Stand, bezahlt.Erledigt));
        Assert.NotNull(bezahlt.Pruefen(BescheidSchritt.Mahnung, null, false));

        var niedergeschlagen = Mit((BescheidSchritt.Festsetzung, 0, 100), (BescheidSchritt.Niederschlagung, 50, null));
        Assert.Equal((BescheidStand.Niedergeschlagen, 0m), (niedergeschlagen.Stand, niedergeschlagen.Offen));

        var abgeholfen = Mit((BescheidSchritt.Festsetzung, 0, 100), (BescheidSchritt.Bekanntgabe, 1, null), (BescheidSchritt.Widerspruch, 5, null), (BescheidSchritt.Abhilfe, 9, 0));
        Assert.Equal(BescheidStand.Aufgehoben, abgeholfen.Stand);
    }

    [Fact]
    public void Bekanntgabe_und_Verjaehrung()
    {
        var aufgabe = new DateOnly(2030, 3, 2);
        Assert.Equal(new DateOnly(2030, 3, 6), Kostenbescheid.Bekanntgabe(aufgabe, Zustellweg.Brief, 4));
        Assert.Equal(aufgabe, Kostenbescheid.Bekanntgabe(aufgabe, Zustellweg.Zustellungsurkunde, 4));
        Assert.Equal(new DateOnly(2035, 12, 31), new Kostenbescheid { FaelligAm = new DateOnly(2030, 4, 6) }.VerjaehrtAm(5));
    }

    [Fact]
    public void Textbausteine_setzen_Platzhalter_ein()
    {
        var text = Bescheidtexte.Einsetzen("{Betrag} bis {FaelligAm}, Kassenzeichen {Kassenzeichen}", new Dictionary<string, string?> { ["{Betrag}"] = "10,00 €", ["{FaelligAm}"] = "01.02.2030" });
        Assert.Equal("10,00 € bis 01.02.2030, Kassenzeichen [fehlt]", text);
    }
}
