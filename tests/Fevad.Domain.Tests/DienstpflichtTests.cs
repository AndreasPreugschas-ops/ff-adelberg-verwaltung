using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

/// <summary>Dienstpflicht-Vorgänge in der Akte (P5: V57).</summary>
public class DienstpflichtTests
{
    [Fact]
    public void Beurlaubung_laeuft_im_Zeitraum()
    {
        var v = new Dienstpflichtvorgang { Art = DienstpflichtArt.Beurlaubung, Datum = new(2026, 3, 1), Von = new(2026, 4, 1), Bis = new(2026, 9, 30) };
        Assert.True(v.Laeuft(new(2026, 6, 1)));
        Assert.False(v.Laeuft(new(2026, 10, 1)));
        Assert.Equal(new DateOnly(2026, 9, 30), v.Bezugstag);
    }

    [Fact]
    public void Verweis_hat_keinen_Zeitraum()
    {
        var v = new Dienstpflichtvorgang { Art = DienstpflichtArt.Verweis, Datum = new(2026, 5, 5), Von = new(2026, 5, 5) };
        Assert.False(v.Laeuft(new(2026, 5, 5)));
        Assert.Equal(new DateOnly(2026, 5, 5), v.Bezugstag);
    }
}
