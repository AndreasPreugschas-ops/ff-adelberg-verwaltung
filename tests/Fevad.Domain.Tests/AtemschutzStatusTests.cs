using Fevad.Domain.Atemschutz;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Tests;

public class AtemschutzStatusTests
{
    private static readonly DateOnly Heute = new(2026, 10, 22);

    private static AtemschutzVorsorge Vorsorge(VorsorgeErgebnis ergebnis, DateOnly faellig) =>
        new() { UntersuchtAm = faellig.AddYears(-3), Ergebnis = ergebnis, NaechsteFaellig = faellig };

    private static IEnumerable<AtemschutzNachweis> AlleUebungen(DateOnly datum) =>
        AtemschutzStatus.JaehrlichePflichten.Select(art => new AtemschutzNachweis { Art = art, Datum = datum });

    private static AtemschutzStatus Berechnen(bool lehrgang, IEnumerable<AtemschutzVorsorge> vorsorgen, IEnumerable<AtemschutzNachweis> nachweise) =>
        AtemschutzStatus.Berechnen(lehrgang, vorsorgen, nachweise, Heute, uebungIntervallMonate: 12, warnungTage: 30);

    [Fact]
    public void Alles_aktuell_ist_gruen()
    {
        var status = Berechnen(true, [Vorsorge(VorsorgeErgebnis.Geeignet, Heute.AddYears(2))], AlleUebungen(Heute.AddMonths(-2)));
        Assert.Equal(Fristampel.Gruen, status.Ampel);
        Assert.True(status.Einsatzbereit);
    }

    [Fact]
    public void Ohne_Lehrgang_gesperrt()
    {
        var status = Berechnen(false, [Vorsorge(VorsorgeErgebnis.Geeignet, Heute.AddYears(2))], AlleUebungen(Heute.AddMonths(-2)));
        Assert.Equal(Fristampel.Rot, status.Ampel);
    }

    [Fact]
    public void Abgelaufene_Vorsorge_sperrt()
    {
        var status = Berechnen(true, [Vorsorge(VorsorgeErgebnis.Geeignet, Heute.AddDays(-1))], AlleUebungen(Heute.AddMonths(-2)));
        Assert.False(status.Tauglich);
        Assert.False(status.Einsatzbereit);
    }

    [Fact]
    public void Nicht_geeignet_sperrt_auch_bei_gueltiger_Frist()
    {
        var status = Berechnen(true, [Vorsorge(VorsorgeErgebnis.NichtGeeignet, Heute.AddYears(2))], AlleUebungen(Heute.AddMonths(-2)));
        Assert.False(status.Einsatzbereit);
    }

    [Fact]
    public void Fehlende_Belastungsuebung_sperrt()
    {
        var nachweise = AlleUebungen(Heute.AddMonths(-2)).Where(n => n.Art != AtemschutzNachweisArt.Belastungsuebung);
        var status = Berechnen(true, [Vorsorge(VorsorgeErgebnis.Geeignet, Heute.AddYears(2))], nachweise);
        Assert.Equal(Fristampel.Rot, status.Ampel);
        Assert.Null(status.LetzteUebungen[AtemschutzNachweisArt.Belastungsuebung]);
    }

    [Fact]
    public void Uebung_aelter_als_zwoelf_Monate_sperrt()
    {
        var status = Berechnen(true, [Vorsorge(VorsorgeErgebnis.Geeignet, Heute.AddYears(2))], AlleUebungen(Heute.AddMonths(-13)));
        Assert.Equal(Fristampel.Rot, status.Ampel);
    }

    [Fact]
    public void Uebung_kurz_vor_Ablauf_ist_gelb()
    {
        var status = Berechnen(true, [Vorsorge(VorsorgeErgebnis.Geeignet, Heute.AddYears(2))], AlleUebungen(Heute.AddMonths(-12).AddDays(10)));
        Assert.Equal(Fristampel.Gelb, status.Ampel);
        Assert.True(status.Einsatzbereit);
    }

    [Fact]
    public void Massgeblich_ist_die_juengste_Vorsorge()
    {
        var alt = Vorsorge(VorsorgeErgebnis.NichtGeeignet, Heute.AddYears(-1));
        var neu = Vorsorge(VorsorgeErgebnis.Geeignet, Heute.AddYears(2));
        var status = Berechnen(true, [neu, alt], AlleUebungen(Heute.AddMonths(-1)));
        Assert.True(status.Tauglich);
    }

    [Fact]
    public void Eintraege_nach_dem_Stichtag_zaehlen_nicht()
    {
        var alt = Vorsorge(VorsorgeErgebnis.Geeignet, Heute.AddDays(-1));
        var spaeter = new AtemschutzVorsorge { UntersuchtAm = Heute.AddDays(10), Ergebnis = VorsorgeErgebnis.Geeignet, NaechsteFaellig = Heute.AddYears(3) };
        var status = Berechnen(true, [alt, spaeter], AlleUebungen(Heute.AddDays(5)));
        Assert.False(status.Tauglich);
        Assert.False(status.Einsatzbereit);
    }

    [Theory]
    [InlineData("2026-09-30", Fristampel.Gruen)]
    [InlineData("2026-10-01", Fristampel.Gelb)]
    [InlineData("2026-12-31", Fristampel.Gelb)]
    [InlineData("2027-01-01", Fristampel.Rot)]
    public void Im_Kalenderjahr_gilt_die_Uebung_aus_2025_bis_Jahresende_2026_mit_Warnung_ab_Oktober(string stichtag, Fristampel erwartet)
    {
        var tag = DateOnly.Parse(stichtag, System.Globalization.CultureInfo.InvariantCulture);
        var status = AtemschutzStatus.Berechnen(true, [Vorsorge(VorsorgeErgebnis.Geeignet, tag.AddYears(2))], AlleUebungen(new DateOnly(2025, 1, 15)),
            tag, _ => 12, 30, kalenderjahrWarnungAbMonat: 10);
        Assert.Equal(erwartet, status.Ampel);
    }

    [Fact]
    public void Im_Kalenderjahr_ist_eine_Uebung_vom_Dezember_ein_ganzes_Folgejahr_gueltig()
    {
        Assert.Equal((new DateOnly(2027, 12, 31), 91), AtemschutzStatus.Jahrespflicht(new DateOnly(2026, 12, 20), 12, 30, 10));
        Assert.Equal((new DateOnly(2027, 12, 20), 30), AtemschutzStatus.Jahrespflicht(new DateOnly(2026, 12, 20), 12, 30, 0));
    }
}
