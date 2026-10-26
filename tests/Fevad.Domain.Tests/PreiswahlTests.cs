using Fevad.Domain.Kleiderkammer;

namespace Fevad.Domain.Tests;

/// <summary>Welcher Lieferantenpreis für die Haushaltsplanung gilt.</summary>
public class PreiswahlTests
{
    private static ArtikelPreis Preis(decimal preis, bool bevorzugt = false, bool aktiv = true) =>
        new() { Preis = preis, Bevorzugt = bevorzugt, Lieferant = new Lieferant { Name = "L" + preis, Aktiv = aktiv } };

    [Fact]
    public void Ohne_Bevorzugung_gilt_der_guenstigste_aktive_Preis()
    {
        Assert.Equal(12m, Preiswahl.Massgeblich([Preis(20m), Preis(12m), Preis(5m, aktiv: false), Preis(0m)])!.Preis);
    }

    [Fact]
    public void Der_bevorzugte_Lieferant_schlaegt_den_guenstigsten()
    {
        Assert.Equal(20m, Preiswahl.Massgeblich([Preis(20m, bevorzugt: true), Preis(12m)])!.Preis);
        Assert.Equal(12m, Preiswahl.Massgeblich([Preis(20m, bevorzugt: true, aktiv: false), Preis(12m)])!.Preis);
        Assert.Null(Preiswahl.Massgeblich([]));
    }
}
