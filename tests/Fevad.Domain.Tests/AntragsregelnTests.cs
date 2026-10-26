using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class AntragsregelnTests
{
    private static readonly DateOnly Stichtag = new(2026, 10, 23);

    [Theory]
    [InlineData(AntragArt.JugendEintritt, false, true)]
    [InlineData(AntragArt.UebertrittEinsatz, true, true)]
    [InlineData(AntragArt.EinsatzEintritt, true, false)]
    [InlineData(AntragArt.Volljaehrigkeit, true, false)]
    [InlineData(AntragArt.Altersabteilung, true, false)]
    [InlineData(AntragArt.Ausscheiden, true, false)]
    public void Unterschriften_je_nach_Art(AntragArt art, bool person, bool eltern)
    {
        Assert.Equal(person, Antragsregeln.UnterschriftPerson(art));
        Assert.Equal(eltern, Antragsregeln.UnterschriftEltern(art));
    }

    [Fact]
    public void Aufnahme_in_die_Einsatzabteilung_ohne_Uebertritt_erst_mit_der_Volljaehrigkeit()
    {
        Assert.True(Antragsregeln.ZuJungFuerAufnahme(AntragArt.EinsatzEintritt, Stichtag.AddYears(-17), Stichtag, 18));
        Assert.False(Antragsregeln.ZuJungFuerAufnahme(AntragArt.EinsatzEintritt, Stichtag.AddYears(-18), Stichtag, 18));
        Assert.False(Antragsregeln.ZuJungFuerAufnahme(AntragArt.UebertrittEinsatz, Stichtag.AddYears(-17), Stichtag, 18));
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
    public void Altersgrenzen_und_Vorlauf_machen_den_Antrag_unmoeglich()
    {
        var regeln = Laufbahnregeln.Satzung;
        IReadOnlyList<string> Fehler(AntragArt art, DateOnly geburt, DateOnly? ab = null) => Antragsregeln.Fehler(art, geburt, ab, Stichtag, regeln, 18, 6);

        Assert.Contains(Fehler(AntragArt.JugendEintritt, Stichtag.AddYears(-8)), f => f.Contains("erst ab 10"));
        // Mit 15 nicht in die Einsatzabteilung, weder direkt noch über den Übertritt.
        Assert.Contains(Fehler(AntragArt.EinsatzEintritt, Stichtag.AddYears(-15)), f => f.Contains("über die Jugendfeuerwehr und den Übertritt"));
        Assert.Contains(Fehler(AntragArt.UebertrittEinsatz, Stichtag.AddYears(-15)), f => f.Contains("frühestens ab dem"));
        // Ein halbes Jahr vor dem 17. Geburtstag geht der Übertritt zum Geburtstag.
        var geburt = Stichtag.AddYears(-17).AddMonths(5);
        Assert.Empty(Fehler(AntragArt.UebertrittEinsatz, geburt, Stichtag.AddMonths(5)));
        Assert.Contains(Fehler(AntragArt.UebertrittEinsatz, geburt), f => f.Contains("„Gewünscht ab“"));
        Assert.Contains(Fehler(AntragArt.EinsatzEintritt, Stichtag.AddYears(-30), Stichtag.AddMonths(7)), f => f.Contains("höchstens 6 Monate"));
        Assert.Contains(Fehler(AntragArt.Altersabteilung, Stichtag.AddYears(-50)), f => f.Contains("Altersabteilung"));
        Assert.Empty(Fehler(AntragArt.Altersabteilung, Stichtag.AddYears(-56)));
        Assert.Empty(Fehler(AntragArt.Ausscheiden, Stichtag.AddYears(-15)));
        Assert.Contains(Antragsregeln.Hinweise(AntragArt.UebertrittEinsatz, Stichtag.AddYears(-17), Stichtag, regeln), h => h.Contains("Einsatzdienst erst ab"));
    }

    [Fact]
    public void Ausscheiden_mit_Gruenden_der_Satzung_und_Altersabteilung()
    {
        Assert.Contains(Entlassungsgrund.AustrittProbezeit, Antragsregeln.Gruende(AbteilungArt.Einsatz, probezeit: true));
        Assert.DoesNotContain(Entlassungsgrund.AustrittProbezeit, Antragsregeln.Gruende(AbteilungArt.Einsatz, probezeit: false));
        Assert.Equal([Entlassungsgrund.JugendAustritt, Entlassungsgrund.ZustimmungZurueck], Antragsregeln.Gruende(AbteilungArt.Jugend, false));
        Assert.True(Antragsregeln.AltersabteilungMoeglich(AbteilungArt.Einsatz, Entlassungsgrund.Wohnung));
        Assert.False(Antragsregeln.AltersabteilungMoeglich(AbteilungArt.Einsatz, Entlassungsgrund.AustrittProbezeit));
        Assert.False(Antragsregeln.AltersabteilungMoeglich(AbteilungArt.Jugend, Entlassungsgrund.JugendAustritt));
        Assert.True(Antragsregeln.ElternUnterschreiben(AntragArt.Ausscheiden, minderjaehrig: true));
        Assert.False(Antragsregeln.ElternUnterschreiben(AntragArt.Ausscheiden, minderjaehrig: false));
    }

    [Fact]
    public void Kinderfeuerwehr_nimmt_von_6_bis_vor_10_auf()
    {
        IReadOnlyList<string> Fehler(DateOnly geburt) => Antragsregeln.Fehler(AntragArt.KinderEintritt, geburt, null, Stichtag, Laufbahnregeln.Satzung, 18, 6);

        Assert.Contains(Fehler(Stichtag.AddYears(-5)), f => f.Contains("erst ab 6"));
        Assert.Empty(Fehler(Stichtag.AddYears(-7)));
        Assert.Contains(Fehler(Stichtag.AddYears(-10)), f => f.Contains("Jugendfeuerwehr beantragen"));
        Assert.Equal(AbteilungArt.Kinder, Antragsregeln.Zielabteilung(AntragArt.KinderEintritt));
        Assert.True(Antragsregeln.UnterschriftEltern(AntragArt.KinderEintritt));
        Assert.False(Antragsregeln.UnterschriftPerson(AntragArt.KinderEintritt));
        Assert.False(Antragsregeln.MitMitglied(AntragArt.KinderEintritt));
    }
}
