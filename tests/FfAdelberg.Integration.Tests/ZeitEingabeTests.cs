using FfAdelberg.Web.Infrastruktur;

namespace FfAdelberg.Integration.Tests;

public class ZeitEingabeTests
{
    // 22.10.2026 23:30 Uhr Ortszeit (Sommerzeit, UTC+2).
    private static readonly DateTimeOffset Alarm = new(2026, 10, 22, 21, 30, 25, TimeSpan.Zero);

    [Fact]
    public void Datum_und_Uhrzeit_in_Ortszeit()
    {
        Assert.True(ZeitEingabe.TryLesen("22.10.2026 23:30", null, out var wert));
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 21, 30, 0, TimeSpan.Zero), wert);
        Assert.Equal("22.10.2026 23:30", ZeitEingabe.Text(wert));
    }

    [Fact]
    public void Winterzeit_wird_beachtet()
    {
        Assert.True(ZeitEingabe.TryLesen("1.12.2026 7:05", null, out var wert));
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 6, 5, 0, TimeSpan.Zero), wert);
    }

    [Fact]
    public void Nur_Uhrzeit_gilt_am_Alarmtag_und_nach_Mitternacht_am_Folgetag()
    {
        Assert.True(ZeitEingabe.TryLesen("23:30", Alarm, out var gleich));
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 21, 30, 0, TimeSpan.Zero), gleich);

        Assert.True(ZeitEingabe.TryLesen("01:15", Alarm, out var danach));
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 23, 15, 0, TimeSpan.Zero), danach);
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("gestern")]
    [InlineData("10:00 PM")]
    public void Unsinn_wird_abgelehnt(string text)
    {
        Assert.False(ZeitEingabe.TryLesen(text, Alarm, out _));
    }

    [Fact]
    public void Leer_ist_keine_Zeit_aber_gueltig()
    {
        Assert.True(ZeitEingabe.TryLesen("  ", Alarm, out var wert));
        Assert.Null(wert);
    }
}
