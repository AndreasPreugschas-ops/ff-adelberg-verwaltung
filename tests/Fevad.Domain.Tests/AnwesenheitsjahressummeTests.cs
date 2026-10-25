using Fevad.Domain.Dienstbetrieb;

namespace Fevad.Domain.Tests;

public class AnwesenheitsjahressummeTests
{
    private static readonly Guid Anna = Guid.CreateVersion7();
    private static readonly Guid Ben = Guid.CreateVersion7();

    [Fact]
    public void Verdichten_zaehlt_je_Mitglied_und_Jahr_getrennt()
    {
        var werte = new[]
        {
            new Anwesenheitsjahressumme.Einzelwert(Anna, 2018, AnwesenheitStatus.Anwesend, TimeSpan.FromHours(2), true),
            new Anwesenheitsjahressumme.Einzelwert(Anna, 2018, AnwesenheitStatus.Anwesend, TimeSpan.FromMinutes(90), false),
            new Anwesenheitsjahressumme.Einzelwert(Anna, 2018, AnwesenheitStatus.Entschuldigt, TimeSpan.Zero, true),
            new Anwesenheitsjahressumme.Einzelwert(Anna, 2019, AnwesenheitStatus.Unentschuldigt, TimeSpan.Zero, true),
            new Anwesenheitsjahressumme.Einzelwert(Ben, 2018, AnwesenheitStatus.Anwesend, TimeSpan.FromHours(3), true),
        };

        var summen = Anwesenheitsjahressumme.Verdichten(werte, []);

        Assert.Equal(3, summen.Count);
        var anna2018 = summen.Single(s => s.MitgliedId == Anna && s.Jahr == 2018);
        Assert.Equal((2, 1, 0, 210, 120), (anna2018.Anwesend, anna2018.Entschuldigt, anna2018.Unentschuldigt, anna2018.Minuten, anna2018.FortbildungMinuten));
        Assert.Equal(1, summen.Single(s => s.MitgliedId == Anna && s.Jahr == 2019).Unentschuldigt);
        Assert.Equal(180, summen.Single(s => s.MitgliedId == Ben).FortbildungMinuten);
    }

    [Fact]
    public void Verdichten_schreibt_vorhandene_Summe_fort()
    {
        var vorhanden = new Anwesenheitsjahressumme { MitgliedId = Anna, Jahr = 2018, Anwesend = 5, Minuten = 600, FortbildungMinuten = 600 };

        var summen = Anwesenheitsjahressumme.Verdichten(
            [new Anwesenheitsjahressumme.Einzelwert(Anna, 2018, AnwesenheitStatus.Anwesend, TimeSpan.FromHours(1), true)], [vorhanden]);

        var summe = Assert.Single(summen);
        Assert.Same(vorhanden, summe);
        Assert.Equal((6, 660, 660), (summe.Anwesend, summe.Minuten, summe.FortbildungMinuten));
    }

    [Fact]
    public void Negative_Dauer_zaehlt_als_null_Minuten()
    {
        var summe = new Anwesenheitsjahressumme { MitgliedId = Anna, Jahr = 2018 };
        summe.Einrechnen([new Anwesenheitsjahressumme.Einzelwert(Anna, 2018, AnwesenheitStatus.Anwesend, TimeSpan.FromMinutes(-5), true)]);
        Assert.Equal((1, 0), (summe.Anwesend, summe.Minuten));
    }
}
