using FfAdelberg.Domain.Atemschutz;
using FfAdelberg.Domain.Gemeinsam;

namespace FfAdelberg.Domain.Tests;

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
}
