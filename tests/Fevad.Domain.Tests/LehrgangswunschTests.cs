using Fevad.Domain.Ausbildung;

namespace Fevad.Domain.Tests;

/// <summary>Lehrgangswünsche (P5: V54).</summary>
public class LehrgangswunschTests
{
    private static readonly Qualifikation Tf = new() { Code = "TF", Bezeichnung = "Truppführer", Art = QualifikationArt.Lehrgang };
    private static readonly Qualifikation Gf = new() { Code = "GF", Bezeichnung = "Gruppenführer", Art = QualifikationArt.Lehrgang, Voraussetzungen = "TF" };
    private static readonly Qualifikation Agt = new() { Code = "AGT", Bezeichnung = "Atemschutzgeräteträger", Art = QualifikationArt.Lehrgang, MindestalterJahre = 18 };
    private static readonly DateOnly Heute = new(2026, 10, 26);

    private static QualifikationNachweis Nachweis(Qualifikation q, DateOnly am) => new() { QualifikationId = q.Id, Qualifikation = q, ErworbenAm = am };

    [Fact]
    public void Truppfuehrer_vor_Gruppenfuehrer()
    {
        var ohne = Lehrgangswunsch.Pruefen(Gf, Heute, new(1990, 1, 1), [], new Dictionary<string, string> { ["TF"] = "Truppführer" });
        Assert.Contains(ohne, v => !v.Erfuellt && v.Text.Contains("Truppführer"));
        var mit = Lehrgangswunsch.Pruefen(Gf, Heute, new(1990, 1, 1), [Nachweis(Tf, new(2020, 5, 1))]);
        Assert.All(mit, v => Assert.True(v.Erfuellt));
    }

    [Fact]
    public void Mindestalter_zum_Stichtag()
    {
        Assert.Contains(Lehrgangswunsch.Pruefen(Agt, Heute, new(2009, 1, 1), []), v => !v.Erfuellt);
        Assert.All(Lehrgangswunsch.Pruefen(Agt, Heute, new(2008, 1, 1), []), v => Assert.True(v.Erfuellt));
    }

    [Fact]
    public void Vorhanden_nur_mit_gueltigem_Nachweis()
    {
        Assert.True(Lehrgangswunsch.Vorhanden(Tf, Heute, [Nachweis(Tf, new(2020, 5, 1))]));
        Assert.False(Lehrgangswunsch.Vorhanden(Gf, Heute, [Nachweis(Tf, new(2020, 5, 1))]));
        var abgelaufen = Nachweis(Tf, new(2020, 5, 1));
        abgelaufen.GueltigBis = new(2025, 5, 1);
        Assert.False(Lehrgangswunsch.Vorhanden(Tf, Heute, [abgelaufen]));
    }

    [Theory]
    [InlineData(WunschStatus.Gewuenscht, true, true)]
    [InlineData(WunschStatus.Warteliste, true, true)]
    [InlineData(WunschStatus.Angemeldet, true, false)]
    [InlineData(WunschStatus.Bestaetigt, false, false)]
    [InlineData(WunschStatus.Abgelehnt, false, false)]
    [InlineData(WunschStatus.Zurueckgezogen, false, false)]
    public void Offen_und_zurueckziehbar(WunschStatus status, bool offen, bool zurueckziehbar)
    {
        var w = new Lehrgangswunsch { Status = status };
        Assert.Equal(offen, w.Offen);
        Assert.Equal(zurueckziehbar, w.Zurueckziehbar);
    }
}
