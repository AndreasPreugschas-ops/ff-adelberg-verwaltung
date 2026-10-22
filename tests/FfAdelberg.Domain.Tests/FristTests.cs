using FfAdelberg.Domain.Gemeinsam;

namespace FfAdelberg.Domain.Tests;

public class FristTests
{
    private static readonly DateOnly Heute = new(2026, 10, 22);

    [Fact]
    public void Ohne_Ablaufdatum_ist_gruen() =>
        Assert.Equal(Fristampel.Gruen, Frist.Bewerten(null, Heute, 90));

    [Fact]
    public void Abgelaufen_ist_rot() =>
        Assert.Equal(Fristampel.Rot, Frist.Bewerten(Heute.AddDays(-1), Heute, 90));

    [Fact]
    public void Am_Ablauftag_noch_gelb() =>
        Assert.Equal(Fristampel.Gelb, Frist.Bewerten(Heute, Heute, 90));

    [Fact]
    public void Innerhalb_der_Warnfrist_gelb() =>
        Assert.Equal(Fristampel.Gelb, Frist.Bewerten(Heute.AddDays(90), Heute, 90));

    [Fact]
    public void Ausserhalb_der_Warnfrist_gruen() =>
        Assert.Equal(Fristampel.Gruen, Frist.Bewerten(Heute.AddDays(91), Heute, 90));
}
