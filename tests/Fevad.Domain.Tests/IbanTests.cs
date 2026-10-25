using Fevad.Domain.Entschaedigung;

namespace Fevad.Domain.Tests;

public class IbanTests
{
    [Theory]
    [InlineData("DE89 3704 0044 0532 0130 00", "DE89370400440532013000")]
    [InlineData("de89370400440532013000", "DE89370400440532013000")]
    [InlineData("AT61 1904 3002 3457 3201", "AT611904300234573201")]
    public void Gueltige_IBAN_wird_normalisiert(string eingabe, string erwartet) =>
        Assert.Equal(erwartet, Iban.Normalisieren(eingabe));

    [Theory]
    [InlineData("DE89 3704 0044 0532 0130 01")]
    [InlineData("DE8937040044053201300")]
    [InlineData("1234")]
    [InlineData("")]
    [InlineData(null)]
    public void Falsche_IBAN_wird_abgelehnt(string? eingabe) => Assert.Null(Iban.Normalisieren(eingabe));

    [Fact]
    public void Maskiert_bleiben_Land_und_die_letzten_vier_Stellen()
    {
        var maskiert = Iban.Maskieren("DE89370400440532013000");
        Assert.StartsWith("DE", maskiert);
        Assert.EndsWith("3000", maskiert.Replace(" ", ""));
        Assert.DoesNotContain("3704", maskiert.Replace(" ", ""));
    }
}
