using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class AntragsregelnTests
{
    private static readonly DateOnly Stichtag = new(2026, 10, 23);

    [Theory]
    [InlineData(AntragArt.JugendEintritt, 12, false, true)]
    [InlineData(AntragArt.UebertrittEinsatz, 17, true, true)]
    [InlineData(AntragArt.EinsatzEintritt, 17, true, true)]
    [InlineData(AntragArt.EinsatzEintritt, 30, true, false)]
    [InlineData(AntragArt.Volljaehrigkeit, 18, true, false)]
    public void Unterschriften_je_nach_Art_und_Alter(AntragArt art, int alter, bool person, bool eltern)
    {
        Assert.Equal(person, Antragsregeln.UnterschriftPerson(art));
        Assert.Equal(eltern, Antragsregeln.UnterschriftEltern(art, alter, 18));
    }

    [Fact]
    public void Blanko_Aufnahme_in_die_Einsatzabteilung_hat_Elternzeilen()
    {
        Assert.True(Antragsregeln.UnterschriftEltern(AntragArt.EinsatzEintritt, null, 18));
    }

    [Fact]
    public void Volljaehrigkeit_nur_wer_minderjaehrig_in_die_Einsatzabteilung_kam()
    {
        var geburtsdatum = new DateOnly(2008, 12, 1);
        // Vorlauf 3 Monate: der 18. Geburtstag am 01.12.2026 ist schon im Blick.
        Assert.Equal(new DateOnly(2026, 12, 1), Antragsregeln.VolljaehrigkeitFaellig(geburtsdatum, new DateOnly(2025, 12, 1), Stichtag, 18, 3, 12));
        // Erst nach dem 18. Geburtstag eingetreten: keine Erklärung nötig.
        Assert.Null(Antragsregeln.VolljaehrigkeitFaellig(new DateOnly(2007, 1, 1), new DateOnly(2025, 6, 1), Stichtag, 18, 3, 12));
        // Länger als die Erinnerungsfrist volljährig: kein Hinweis mehr.
        Assert.Null(Antragsregeln.VolljaehrigkeitFaellig(new DateOnly(2007, 9, 1), new DateOnly(2024, 9, 1), Stichtag, 18, 3, 12));
        // Noch zu weit hin.
        Assert.Null(Antragsregeln.VolljaehrigkeitFaellig(new DateOnly(2009, 6, 1), new DateOnly(2026, 6, 1), Stichtag, 18, 3, 12));
    }

    [Fact]
    public void Hinweise_zu_den_Altersgrenzen_der_Satzung()
    {
        var regeln = Laufbahnregeln.Satzung;
        Assert.Contains(Antragsregeln.Hinweise(AntragArt.JugendEintritt, Stichtag.AddYears(-8), Stichtag, regeln, 18), h => h.Contains("erst ab"));
        Assert.Contains(Antragsregeln.Hinweise(AntragArt.EinsatzEintritt, Stichtag.AddYears(-17), Stichtag, regeln, 18), h => h.Contains("Einsatzdienst erst ab"));
        Assert.Empty(Antragsregeln.Hinweise(AntragArt.EinsatzEintritt, Stichtag.AddYears(-30), Stichtag, regeln, 18));
    }
}
