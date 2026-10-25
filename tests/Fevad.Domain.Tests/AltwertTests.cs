using Fevad.Domain.Import;

namespace Fevad.Domain.Tests;

/// <summary>Werte aus Altdaten (V68): Datum, Ja/Nein und Vergleichsschlüssel.</summary>
public class AltwertTests
{
    [Theory]
    [InlineData("24.10.2026", 2026, 10, 24)]
    [InlineData("1.2.1990", 1990, 2, 1)]
    [InlineData("2026-10-24", 2026, 10, 24)]
    [InlineData("24.10.2026 00:00", 2026, 10, 24)]
    [InlineData("45000", 2023, 3, 15)]
    public void Datum_liest_deutsche_ISO_und_Excel_Werte(string text, int jahr, int monat, int tag)
    {
        Assert.True(Altwert.Datum(text, out var datum));
        Assert.Equal(new DateOnly(jahr, monat, tag), datum);
    }

    [Theory]
    [InlineData("")]
    [InlineData("31.02.2026")]
    [InlineData("morgen")]
    public void Datum_lehnt_Unlesbares_ab(string text) => Assert.False(Altwert.Datum(text, out _));

    [Theory]
    [InlineData("ja", true)]
    [InlineData("X", true)]
    [InlineData("1", true)]
    [InlineData("nein", false)]
    [InlineData("", false)]
    public void JaNein_liest_uebliche_Schreibweisen(string text, bool erwartet)
    {
        Assert.True(Altwert.JaNein(text, out var wert));
        Assert.Equal(erwartet, wert);
    }

    [Fact]
    public void Schluessel_ignoriert_Umlaute_Gross_und_Satzzeichen() =>
        Assert.Equal(Altwert.Schluessel("Straße"), Altwert.Schluessel(" STRASSE. "));

    [Theory]
    [InlineData("24.10.2026 18:30", null, "2026-10-24T16:30:00Z")]
    [InlineData("24.01.2026", "18:30", "2026-01-24T17:30:00Z")]
    [InlineData("45000.75", null, "2023-03-15T17:00:00Z")]
    [InlineData("45000", "0.5", "2023-03-15T11:00:00Z")]
    [InlineData("29.03.2026", "02:30", "2026-03-29T01:30:00Z")]
    public void Zeitpunkt_rechnet_Ortszeit_in_UTC(string datum, string? uhrzeit, string utc)
    {
        Assert.True(Altwert.Zeitpunkt(datum, uhrzeit, out var zeitpunkt));
        Assert.Equal(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture), zeitpunkt);
    }

    [Fact]
    public void Zeitpunkt_lehnt_falsche_Uhrzeit_ab() => Assert.False(Altwert.Zeitpunkt("24.10.2026", "25:99", out _));

    [Theory]
    [InlineData("1.234,56 €", 1234.56)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("899,00", 899)]
    public void Betrag_liest_deutsche_und_technische_Schreibweise(string text, double erwartet)
    {
        Assert.True(Altwert.Betrag(text, out var betrag));
        Assert.Equal((decimal)erwartet, betrag);
    }
}
