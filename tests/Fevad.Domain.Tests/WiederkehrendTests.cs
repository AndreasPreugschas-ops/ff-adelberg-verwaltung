using Fevad.Domain.Kassen;

namespace Fevad.Domain.Tests;

/// <summary>Fälligkeiten wiederkehrender Buchungen (P2: V07).</summary>
public class WiederkehrendTests
{
    [Theory]
    [InlineData("2026-01-31", Buchungsrhythmus.Monatlich, 31, "2026-02-28")]
    [InlineData("2026-02-28", Buchungsrhythmus.Monatlich, 31, "2026-03-31")]
    [InlineData("2028-02-29", Buchungsrhythmus.Jaehrlich, 29, "2029-02-28")]
    [InlineData("2026-11-15", Buchungsrhythmus.Vierteljaehrlich, 15, "2027-02-15")]
    [InlineData("2026-08-01", Buchungsrhythmus.Halbjaehrlich, 1, "2027-02-01")]
    public void Naechste_Faelligkeit_haelt_den_Tag_im_Monat(string faellig, Buchungsrhythmus rhythmus, int tag, string erwartet) =>
        Assert.Equal(DateOnly.Parse(erwartet), Wiederkehrend.Naechste(DateOnly.Parse(faellig), rhythmus, tag));

    [Fact]
    public void Faellig_ist_nur_eine_aktive_Buchung_ab_dem_Tag()
    {
        var w = new WiederkehrendeBuchung { Bezeichnung = "Zuwendung", Text = "Zuwendung der Gemeinde {Jahr}", NaechsteFaelligkeit = new(2027, 1, 1) };
        Assert.False(Wiederkehrend.Faellig(w, new(2026, 12, 31)));
        Assert.True(Wiederkehrend.Faellig(w, new(2027, 1, 1)));
        w.Aktiv = false;
        Assert.False(Wiederkehrend.Faellig(w, new(2027, 3, 1)));
        Assert.Equal("Zuwendung der Gemeinde 2027", Wiederkehrend.Text(w));
    }
}
