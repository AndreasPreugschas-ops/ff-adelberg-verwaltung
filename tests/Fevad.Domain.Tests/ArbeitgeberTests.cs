using Fevad.Domain.Entschaedigung;

namespace Fevad.Domain.Tests;

public class ArbeitgeberTests
{
    [Fact]
    public void Pruefcode_hat_drei_Vierergruppen_ohne_verwechselbare_Zeichen()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => Arbeitgeberbescheinigung.NeuerPruefcode()).ToList();

        Assert.All(codes, c => Assert.Matches("^[2-9A-HJKMNP-Z]{4}-[2-9A-HJKMNP-Z]{4}-[2-9A-HJKMNP-Z]{4}$", c));
        Assert.True(codes.Distinct().Count() > 195);
    }

    [Theory]
    [InlineData("k7qm 4xht pz9c", "K7QM-4XHT-PZ9C")]
    [InlineData("K7QM-4XHT-PZ9C", "K7QM-4XHT-PZ9C")]
    [InlineData("K7QM-4XHT-PZ9", null)]
    [InlineData("K7QM-4XHT-PZ0C", null)]
    [InlineData(null, null)]
    public void Pruefcode_wird_normalisiert(string? eingabe, string? erwartet) =>
        Assert.Equal(erwartet, Arbeitgeberbescheinigung.PruefcodeNormalisieren(eingabe));

    [Fact]
    public void Erstattung_sperrt_Verdienstausfall_ausser_abgelehnt()
    {
        var e = new Arbeitgebererstattung { Arbeitgeber = "Muster GmbH", Entgelt = 180, Sozialversicherung = 37.5m };
        Assert.Equal(217.5m, e.Summe);
        Assert.True(e.SperrtVerdienstausfall);
        e.Stand = ErstattungStand.Abgelehnt;
        Assert.False(e.SperrtVerdienstausfall);
    }
}
