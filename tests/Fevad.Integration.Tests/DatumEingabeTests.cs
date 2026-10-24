using Fevad.Web.Infrastruktur;

namespace Fevad.Integration.Tests;

public class DatumEingabeTests
{
    private static readonly DateOnly Heute = new(2026, 10, 24);

    [Theory]
    [InlineData("24.10.2026", "2026-10-24")]
    [InlineData("4.3.26", "2026-03-04")]
    [InlineData("24102026", "2026-10-24")]
    [InlineData("240326", "2026-03-24")]
    [InlineData("1.3.", "2026-03-01")]
    [InlineData("2026-10-24", "2026-10-24")]
    [InlineData("h", "2026-10-24")]
    [InlineData("Heute", "2026-10-24")]
    [InlineData(" 24.10.2026 ", "2026-10-24")]
    public void Liest_alle_Schreibweisen(string text, string erwartet)
    {
        Assert.True(DatumEingabe.TryLesen(text, Heute, out var datum));
        Assert.Equal(DateOnly.Parse(erwartet), datum);
    }

    [Theory]
    [InlineData("31.02.2026")]
    [InlineData("morgen")]
    [InlineData("24.10")]
    [InlineData("2026")]
    public void Lehnt_Unlesbares_ab(string text) => Assert.False(DatumEingabe.TryLesen(text, Heute, out _));

    [Fact]
    public void Leer_ergibt_null_und_Anzeige_ist_deutsch()
    {
        Assert.True(DatumEingabe.TryLesen("  ", Heute, out var datum));
        Assert.Null(datum);
        Assert.Equal("24.10.2026", DatumEingabe.Anzeige("2026-10-24"));
        Assert.Equal("24.10.2026", DatumEingabe.Anzeige("24.10.2026"));
        Assert.Equal("Unsinn", DatumEingabe.Anzeige("Unsinn"));
        Assert.Equal("", DatumEingabe.Text(null));
    }
}
