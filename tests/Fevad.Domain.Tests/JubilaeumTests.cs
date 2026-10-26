using Fevad.Domain.Gremien;

namespace Fevad.Domain.Tests;

/// <summary>Jubiläumsvorschau (P5: V56).</summary>
public class JubilaeumTests
{
    [Fact]
    public void Stufen_aus_dem_Parameter()
    {
        Assert.Equal([25, 40, 50], Jubilaeum.Stufen("50| 25 |40|x|25"));
        Assert.Empty(Jubilaeum.Stufen(null));
    }

    [Fact]
    public void Jubilaeum_im_Jahr()
    {
        IReadOnlyList<int> stufen = [25, 40];
        Assert.Equal((25, new DateOnly(2026, 3, 1)), Jubilaeum.Im(new DateOnly(2001, 3, 1), 2026, stufen));
        Assert.Null(Jubilaeum.Im(new DateOnly(2002, 3, 1), 2026, stufen));
        // Am 29.02. in einem Nichtschaltjahr gilt der 28.02.
        Assert.Equal((40, new DateOnly(2027, 2, 28)), Jubilaeum.Im(new DateOnly(1987, 2, 28), 2027, stufen));
        Assert.Equal((25, new DateOnly(2025, 2, 28)), Jubilaeum.Im(new DateOnly(2000, 2, 29), 2025, stufen));
    }
}
