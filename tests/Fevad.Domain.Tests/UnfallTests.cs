using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Unfaelle;

namespace Fevad.Domain.Tests;

public class UnfallTests
{
    private static Unfall Unfall(bool toedlich = false, bool mehrAlsDrei = false) => new()
    {
        Ort = "Gerätehaus", Hergang = "Beim Aussteigen gestürzt", Zeitpunkt = new DateTimeOffset(2026, 7, 10, 20, 30, 0, TimeSpan.Zero),
        Toedlich = toedlich, MehrAlsDreiTageArbeitsunfaehig = mehrAlsDrei,
    };

    [Fact]
    public void Nur_bei_Tod_oder_mehr_als_drei_Tagen_anzeigepflichtig()
    {
        Assert.Null(Unfall().AnzeigeBis(3));
        Assert.Equal(new DateOnly(2026, 7, 13), Unfall(mehrAlsDrei: true).AnzeigeBis(3));
        Assert.Equal(new DateOnly(2026, 7, 13), Unfall(toedlich: true).AnzeigeBis(3));
    }

    [Fact]
    public void Anzeigefrist_rechnet_vom_Unfalltag_in_Ortszeit()
    {
        var u = Unfall(mehrAlsDrei: true);
        u.Zeitpunkt = new DateTimeOffset(2026, 7, 10, 22, 30, 0, TimeSpan.Zero); // 11.07. 00:30 Uhr in Berlin
        Assert.Equal(new DateOnly(2026, 7, 14), u.AnzeigeBis(3));
    }

    [Fact]
    public void Ampel_rot_wenn_ueberfaellig_und_nicht_gemeldet()
    {
        var u = Unfall(mehrAlsDrei: true);
        Assert.Equal(Fristampel.Gelb, u.AnzeigeAmpel(new DateOnly(2026, 7, 11), 3));
        Assert.Equal(Fristampel.Rot, u.AnzeigeAmpel(new DateOnly(2026, 7, 14), 3));
        u.StatusSetzen(UnfallStatus.Gemeldet, new DateOnly(2026, 7, 14));
        Assert.Equal(Fristampel.Gruen, u.AnzeigeAmpel(new DateOnly(2026, 7, 20), 3));
    }

    [Fact]
    public void Status_setzt_und_entfernt_die_Daten()
    {
        var u = Unfall();
        u.StatusSetzen(UnfallStatus.Gemeldet, new DateOnly(2026, 7, 12));
        Assert.Equal((new DateOnly(2026, 7, 12), (DateOnly?)null), (u.GemeldetAm, u.AbgeschlossenAm));
        u.StatusSetzen(UnfallStatus.Abgeschlossen, new DateOnly(2026, 9, 1));
        Assert.Equal((new DateOnly(2026, 7, 12), new DateOnly(2026, 9, 1)), (u.GemeldetAm!.Value, u.AbgeschlossenAm!.Value));
        u.StatusSetzen(UnfallStatus.Erfasst, new DateOnly(2026, 9, 2));
        Assert.Equal(((DateOnly?)null, (DateOnly?)null), (u.GemeldetAm, u.AbgeschlossenAm));
    }
}
