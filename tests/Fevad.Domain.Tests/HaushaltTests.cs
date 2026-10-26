using Fevad.Domain.Haushalt;

namespace Fevad.Domain.Tests;

/// <summary>Haushaltsanmeldung (V29): Planjahr, Wertgrenze, Pflichtangaben und Summen.</summary>
public class HaushaltTests
{
    private static readonly Haushaltsstelle Eh = new() { Code = "EH", Bezeichnung = "EH", Produkt = "12.60.01", Kostenstelle = "1", Sachkonto = "4221", Teil = Haushaltsteil.Ergebnishaushalt };
    private static readonly Haushaltsstelle Fh = new() { Code = "FH", Bezeichnung = "FH", Produkt = "12.60.01", Kostenstelle = "1", Sachkonto = "7832", Teil = Haushaltsteil.Finanzhaushalt };

    [Fact]
    public void Planjahr_ist_Gesamtbetrag_ohne_Raten_und_Investition_nach_Nettopreis()
    {
        var p = new Haushaltsposition { Bezeichnung = "TLF", Menge = 1, Einzelpreis = 420000m, RateFolgejahr1 = 300000m };
        Assert.Equal(120000m, p.BetragPlanjahr);
        Assert.True(p.Investiv(1000m, 19m));
        Assert.True(new Haushaltsposition { Bezeichnung = "x", Einzelpreis = 1190m }.Investiv(1000m, 19m));
        Assert.False(new Haushaltsposition { Bezeichnung = "x", Einzelpreis = 1189.99m }.Investiv(1000m, 19m));
    }

    [Fact]
    public void Ab_Wertgrenze_sind_Begruendung_und_Rechtsgrundlage_Pflicht()
    {
        Assert.Equal(["Begründung", "Rechtsgrundlage"], new Haushaltsposition { Bezeichnung = "Satz", Menge = 10, Einzelpreis = 150m }.Fehlt(1000m, 19m));
        Assert.Empty(new Haushaltsposition { Bezeichnung = "Klein", Menge = 2, Einzelpreis = 100m }.Fehlt(1000m, 19m));
        Assert.Equal(["Betrag"], new Haushaltsposition { Bezeichnung = "Satz fehlt", Menge = 3, Einzelpreis = 0m }.Fehlt(1000m, 19m));
        Assert.Contains("Raten größer als der Gesamtbetrag",
            new Haushaltsposition { Bezeichnung = "x", Einzelpreis = 500m, RateFolgejahr2 = 600m, Begruendung = "b", Rechtsgrundlage = "r" }.Fehlt(1000m, 19m));
    }

    [Fact]
    public void Summen_nur_aus_angemeldeten_Positionen_je_Haushaltsteil()
    {
        var a = new Haushaltsanmeldung { Jahr = 2027 };
        a.Positionen.AddRange([
            new() { Bezeichnung = "a", Einzelpreis = 100m, Haushaltsstelle = Eh, Status = PositionStatus.Angemeldet },
            new() { Bezeichnung = "b", Einzelpreis = 50m, Haushaltsstelle = Eh, Status = PositionStatus.Vorschlag },
            new() { Bezeichnung = "c", Einzelpreis = 80000m, RateFolgejahr1 = 30000m, Zuwendung = 25000m, Haushaltsstelle = Fh, Status = PositionStatus.Angemeldet },
            new() { Bezeichnung = "d", Einzelpreis = 9m, Haushaltsstelle = Eh, Status = PositionStatus.Gestrichen },
        ]);
        Assert.Equal(100m, a.Summe(Haushaltsteil.Ergebnishaushalt));
        Assert.Equal(50000m, a.Summe(Haushaltsteil.Finanzhaushalt));
        Assert.Equal(25000m, a.Zuwendungen);
        Assert.Equal([AnmeldungStatus.Eingereicht, AnmeldungStatus.Entwurf], Haushaltsanmeldung.Folgestatus(AnmeldungStatus.Beraten));
    }
}
