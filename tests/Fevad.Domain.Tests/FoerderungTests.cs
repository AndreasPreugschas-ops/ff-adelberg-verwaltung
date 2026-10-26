using Fevad.Domain.Technik;

namespace Fevad.Domain.Tests;

/// <summary>Z-Feu-Assistent (V30): Statuskette, Voraussetzungen je Schritt, Checkliste und erwarteter Betrag.</summary>
public class FoerderungTests
{
    private static readonly Foerdergegenstand Mtw = new() { Code = "ZF-MTW", Bezeichnung = "MTW", Festbetrag = 25000m, FestbetragRahmenvertrag = 28000m, ZweckbindungJahre = 10, GueltigBis = new(2031, 12, 31) };

    [Fact]
    public void Statuskette_laesst_keinen_Schritt_aus_und_Abgelehnt_nur_bis_zur_Bewilligung()
    {
        Assert.Equal([Foerderstatus.KbmAbgestimmt, Foerderstatus.Abgelehnt], Foerderantrag.Folgestatus(Foerderstatus.Vorbereitung));
        Assert.Equal([Foerderstatus.NachweisEingereicht], Foerderantrag.Folgestatus(Foerderstatus.Bewilligt));
        Assert.Empty(Foerderantrag.Folgestatus(Foerderstatus.Ausgezahlt));
        Assert.True(new Foerderantrag { Status = Foerderstatus.Beantragt }.BeginnGesperrt);
        Assert.False(new Foerderantrag { Status = Foerderstatus.Bewilligt }.BeginnGesperrt);
        Assert.False(new Foerderantrag { Status = Foerderstatus.Abgelehnt }.BeginnGesperrt);
    }

    [Fact]
    public void Jeder_Schritt_nennt_was_fehlt()
    {
        var antrag = new Foerderantrag { Antragsjahr = 2027 };
        Assert.Equal(["Datum des Vorgesprächs mit dem KBM"], antrag.Fehlt(Foerderstatus.KbmAbgestimmt, null));
        Assert.Equal(["Antragsdatum", "Fördergegenstand"], antrag.Fehlt(Foerderstatus.Beantragt, null));
        Assert.Equal(["Datum des Zuwendungsbescheids", "bewilligte Zuwendung"], antrag.Fehlt(Foerderstatus.Bewilligt, null));

        antrag.RechnungLiegtVor = antrag.RichtigkeitsbescheinigungLiegtVor = antrag.AbnahmeberichtLiegtVor = antrag.BeladungserklaerungLiegtVor = true;
        antrag.NachweisEingereichtAm = new(2028, 3, 1);
        Assert.Empty(antrag.Fehlt(Foerderstatus.NachweisEingereicht, null));
        Assert.Equal(["Altfahrzeug außer Dienst gestellt"], antrag.Fehlt(Foerderstatus.NachweisEingereicht, false));
        Assert.Equal(5, antrag.Checkliste(true).Count);
    }

    [Fact]
    public void Erwarteter_Betrag_aus_Bescheid_sonst_Festbetrag_mit_Rahmenvertrag()
    {
        Assert.Equal(25000m, new Foerderantrag { Foerdergegenstand = Mtw }.ErwarteterBetrag);
        Assert.Equal(28000m, new Foerderantrag { Foerdergegenstand = Mtw, Rahmenvertrag = true }.ErwarteterBetrag);
        Assert.Equal(24500m, new Foerderantrag { Foerdergegenstand = Mtw, Zuwendung = 24500m }.ErwarteterBetrag);
        Assert.Null(new Foerderantrag().ErwarteterBetrag);
        Assert.Equal(new DateOnly(2027, 2, 15), new Foerderantrag { Antragsjahr = 2027 }.Antragsfrist(2, 15));
        Assert.Equal(new DateOnly(2027, 2, 28), new Foerderantrag { Antragsjahr = 2027 }.Antragsfrist(2, 31));
    }
}
