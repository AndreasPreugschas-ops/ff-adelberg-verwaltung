using Fevad.Domain.Einsaetze;

namespace Fevad.Domain.Tests;

/// <summary>Rechenregeln der Führungskennzahlen (P5: V52).</summary>
public class KennzahlenTests
{
    [Fact]
    public void Median_bei_ungerader_und_gerader_Anzahl()
    {
        Assert.Equal(5, Kennzahlen.Median([9, 1, 5]));
        Assert.Equal(4, Kennzahlen.Median([1, 3, 5, 9]));
        Assert.Null(Kennzahlen.Median([]));
    }

    [Fact]
    public void Perzentil_nach_naechstem_Rang()
    {
        var werte = Enumerable.Range(1, 10).Select(i => (double)i).ToList();
        Assert.Equal(9, Kennzahlen.Perzentil(werte, 0.9));
        Assert.Equal(10, Kennzahlen.Perzentil(werte, 1));
        Assert.Equal(1, Kennzahlen.Perzentil(werte, 0));
        Assert.Equal(7, Kennzahlen.Perzentil([7], 0.9));
        Assert.Null(Kennzahlen.Perzentil([], 0.9));
    }

    [Theory]
    [InlineData("2026-10-26T05:00:00Z", Tageszeit.WerktagsTag)] // Montag 06:00 Uhr
    [InlineData("2026-10-26T04:59:00Z", Tageszeit.Uebrige)] // Montag 05:59 Uhr
    [InlineData("2026-10-26T16:59:00Z", Tageszeit.WerktagsTag)] // Montag 17:59 Uhr
    [InlineData("2026-10-26T17:00:00Z", Tageszeit.Uebrige)] // Montag 18:00 Uhr
    [InlineData("2026-10-24T10:00:00Z", Tageszeit.Uebrige)] // Samstag 12:00 Uhr
    [InlineData("2026-07-01T04:30:00Z", Tageszeit.WerktagsTag)] // Mittwoch 06:30 Uhr Sommerzeit
    public void Tageszeit_in_Ortszeit(string alarmiert, Tageszeit erwartet) =>
        Assert.Equal(erwartet, Kennzahlen.TageszeitVon(DateTimeOffset.Parse(alarmiert)));

    [Fact]
    public void Funktionen_bis_zur_Frist_zaehlen_nur_eingetroffene_Fahrzeuge()
    {
        var alarm = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        (DateTimeOffset?, int)[] fahrzeuge = [(alarm.AddMinutes(8), 9), (alarm.AddMinutes(10), 3), (alarm.AddMinutes(13), 6), (null, 2)];
        Assert.Equal(12, Kennzahlen.FunktionenBis(alarm, fahrzeuge, 10));
        Assert.Equal(18, Kennzahlen.FunktionenBis(alarm, fahrzeuge, 15));
        Assert.Equal(0, Kennzahlen.FunktionenBis(alarm, fahrzeuge, 5));
    }

    [Fact]
    public void Minuten_nur_bei_passender_Reihenfolge()
    {
        var von = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(4.5, Kennzahlen.Minuten(von, von.AddSeconds(270)));
        Assert.Null(Kennzahlen.Minuten(von, null));
        Assert.Null(Kennzahlen.Minuten(von, von.AddMinutes(-1)));
    }
}
