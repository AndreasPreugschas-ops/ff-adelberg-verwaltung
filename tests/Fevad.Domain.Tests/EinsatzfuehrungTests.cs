using Fevad.Domain.Einsaetze;

namespace Fevad.Domain.Tests;

public class EinsatzfuehrungTests
{
    private static readonly Guid Eigener = Guid.NewGuid();
    private static readonly Guid Zweiter = Guid.NewGuid();
    private static readonly DateTimeOffset Alarm = new(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

    private static List<Einsatzleitung> Verlauf() =>
    [
        new() { Ab = Alarm.AddMinutes(30), MitgliedId = Zweiter },
        new() { Ab = Alarm, Extern = "Kreisbrandmeister" },
        new() { Ab = Alarm.AddMinutes(10), MitgliedId = Eigener },
    ];

    [Fact]
    public void Zum_liefert_den_letzten_Leiter_vor_dem_Zeitpunkt()
    {
        Assert.Equal("Kreisbrandmeister", Einsatzfuehrung.Zum(Verlauf(), Alarm.AddMinutes(5))!.Extern);
        Assert.Equal(Eigener, Einsatzfuehrung.Zum(Verlauf(), Alarm.AddMinutes(29))!.MitgliedId);
        Assert.Equal(Zweiter, Einsatzfuehrung.Zum(Verlauf(), Alarm.AddHours(2))!.MitgliedId);
        Assert.Null(Einsatzfuehrung.Zum(Verlauf(), Alarm.AddMinutes(-1)));
    }

    [Fact]
    public void ErsterEigener_ueberspringt_externe_Leiter()
    {
        Assert.Equal(Eigener, Einsatzfuehrung.ErsterEigener(Verlauf()));
        Assert.Null(Einsatzfuehrung.ErsterEigener([new Einsatzleitung { Ab = Alarm, Extern = "Nachbarwehr" }]));
    }

    [Fact]
    public void Warnung_nur_wenn_keine_der_Qualifikationen_vorhanden()
    {
        var gf = new Einsatzfunktion { Code = "GF", Bezeichnung = "Gruppenführer", Qualifikationen = "GF|ZF|VF" };
        Assert.Null(gf.Warnung(["ZF"]));
        Assert.Equal("Gruppenführer: Qualifikation GF oder ZF oder VF fehlt.", gf.Warnung(["MA"]));
        Assert.Null(new Einsatzfunktion { Code = "ME", Bezeichnung = "Melder" }.Warnung([]));
    }

    [Theory]
    [InlineData("at", true)]
    [InlineData(" Angriffstrupp ", true)]
    [InlineData("WT", false)]
    [InlineData(null, false)]
    public void Passt_nach_Code_oder_Bezeichnung(string? text, bool erwartet) =>
        Assert.Equal(erwartet, new Einsatzfunktion { Code = "AT", Bezeichnung = "Angriffstrupp" }.Passt(text));
}
