using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class GemeindeTests
{
    [Fact]
    public void Vorlagen_haben_Muster_fuer_jeden_Anlass()
    {
        foreach (var art in Enum.GetValues<GemeindevorgangArt>().Where(a => a != GemeindevorgangArt.Sonstiges))
        {
            var (zustaendig, rechtsgrundlage, vorschlag) = Gemeindevorlagen.Muster(art);
            Assert.False(string.IsNullOrWhiteSpace(zustaendig + rechtsgrundlage + vorschlag), art.ToString());
            Assert.False(string.IsNullOrWhiteSpace(vorschlag), art.ToString());
        }

        Assert.Equal(Gemeindevorlagen.Buergermeister, Gemeindevorlagen.Muster(GemeindevorgangArt.Wirtschaftsplan).Zustaendig);
    }

    [Fact]
    public void Stand_eines_Gemeindevorgangs()
    {
        var v = new Gemeindevorgang { Betreff = "x", Zustaendig = Gemeindevorlagen.Gemeinderat };
        Assert.Equal(GemeindevorgangStand.Entwurf, v.Stand);
        v.VorgelegtAm = new DateOnly(2030, 1, 1);
        Assert.Equal(GemeindevorgangStand.Vorgelegt, v.Stand);
        v.Ergebnis = GemeindeErgebnis.Zugestimmt;
        Assert.Equal(GemeindevorgangStand.Entschieden, v.Stand);
    }

    [Fact]
    public void Erlass_braucht_Anhoerung_Fristablauf_und_Hoechstbetrag()
    {
        var heute = new DateOnly(2030, 5, 10);
        var b = new Mitgliederbescheid { EmpfaengerName = "A", Art = MitgliederbescheidArt.Geldbusse, Tenor = "T", Begruendung = "B", Betrag = 1500 };
        Assert.Equal("Vor dem Erlass ist der Betroffene anzuhören.", b.ErlassPruefen(heute, 1000));

        b.AnhoerungAm = new DateOnly(2030, 5, 1);
        b.AnhoerungBis = new DateOnly(2030, 5, 15);
        Assert.Contains("läuft noch", b.ErlassPruefen(heute, 1000));

        b.Stellungnahme = "Ich bedauere.";
        Assert.Contains("1.000,00 €", b.ErlassPruefen(heute, 1000));
        b.Betrag = 200;
        Assert.Null(b.ErlassPruefen(heute, 1000));

        var ablehnung = new Mitgliederbescheid { EmpfaengerName = "B", Art = MitgliederbescheidArt.AblehnungAufnahme, Tenor = "T", Begruendung = "B" };
        Assert.Null(ablehnung.ErlassPruefen(heute, 1000));
        Assert.Equal("Entscheidungssatz und Begründung fehlen.", new Mitgliederbescheid { EmpfaengerName = "C" }.ErlassPruefen(heute, 1000));
    }

    [Fact]
    public void Ausweisnummer_zaehlt_hoch()
    {
        Assert.Equal("123-1", Dienstausweis.NaechsteNummer("123", 0));
        Assert.Equal("123-3", Dienstausweis.NaechsteNummer("123", 2));
    }
}
