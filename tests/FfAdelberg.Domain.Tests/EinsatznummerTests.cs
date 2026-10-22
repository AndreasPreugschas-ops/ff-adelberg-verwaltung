using FfAdelberg.Domain.Einsaetze;

namespace FfAdelberg.Domain.Tests;

public class EinsatznummerTests
{
    [Theory]
    [InlineData("{JJJJ}-{NNN}", 2026, 17, "2026-017")]
    [InlineData("{JJJJ}-{NNN}", 2026, 1234, "2026-1234")]
    [InlineData("E{JJ}/{NN}", 2026, 5, "E26/05")]
    [InlineData("", 2027, 1, "2027-001")]
    public void Bilden(string format, int jahr, int nummer, string erwartet)
    {
        Assert.Equal(erwartet, Einsatznummer.Bilden(format, jahr, nummer));
    }

    [Theory]
    [InlineData("2026-017", 17)]
    [InlineData("2026-1234", 1234)]
    [InlineData("2025-017", null)]
    [InlineData("2026-", null)]
    [InlineData("2026-01a", null)]
    [InlineData("N-2026-001", null)]
    public void Laufende_Nummer_lesen(string nummer, int? erwartet)
    {
        Assert.Equal(erwartet, Einsatznummer.LaufendeNummer(Einsatznummer.StandardFormat, 2026, nummer));
    }

    [Fact]
    public void Format_ohne_laufende_Nummer_ist_ungueltig()
    {
        Assert.Throws<FormatException>(() => Einsatznummer.Bilden("{JJJJ}", 2026, 1));
    }
}
