using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Tests;

public class PersonalfristenTests
{
    private static readonly DateOnly Heute = new(2026, 10, 22);
    private static readonly Fristeinstellungen Einstellungen = new(90, 30, 60,
        AtemschutzStatus.JaehrlichePflichten.ToDictionary(a => a, _ => 12));

    private static readonly Qualifikation Agt = new() { Code = Qualifikation.CodeAtemschutz, Bezeichnung = "Atemschutzgeräteträger", Art = QualifikationArt.Lehrgang };
    private static readonly Qualifikation KlasseC = new() { Code = "FS-C", Bezeichnung = "Fahrerlaubnis Klasse C", Art = QualifikationArt.Fahrerlaubnis };
    private static readonly Qualifikation ErsteHilfe = new() { Code = "EH", Bezeichnung = "Erste Hilfe", Art = QualifikationArt.Lehrgang, GueltigkeitMonate = 24 };

    private static readonly Nachweisart Fuehrerscheinkontrolle = new() { Code = "FSK", Bezeichnung = "Führerscheinkontrolle", IntervallMonate = 6, Pflicht = NachweisPflicht.MitFahrerlaubnis };
    private static readonly Nachweisart Unterweisung = new() { Code = "UVV", Bezeichnung = "Unterweisung", IntervallMonate = 12, Pflicht = NachweisPflicht.AlleEinsatzkraefte };
    private static readonly Nachweisart Freiwillig = new() { Code = "X", Bezeichnung = "Freiwillig", IntervallMonate = 12 };

    private static QualifikationNachweis Hat(Qualifikation q, DateOnly erworben, DateOnly? bis = null) =>
        new() { QualifikationId = q.Id, Qualifikation = q, ErworbenAm = erworben, GueltigBis = bis };

    private static Nachweis Nachweis(Nachweisart art, DateOnly datum) =>
        new() { NachweisartId = art.Id, Datum = datum, GueltigBis = Ausbildung.Nachweis.GueltigkeitAus(datum, art) };

    private static IReadOnlyList<Fristeintrag> Berechnen(
        IReadOnlyList<QualifikationNachweis>? q = null,
        IReadOnlyList<Nachweis>? n = null,
        IReadOnlyList<AtemschutzVorsorge>? v = null,
        IReadOnlyList<AtemschutzNachweis>? a = null) =>
        Personalfristen.Berechnen(
            new Fristdaten(Guid.NewGuid(), "Muster, Max", q ?? [], n ?? [], v ?? [], a ?? []),
            [Fuehrerscheinkontrolle, Unterweisung, Freiwillig], Heute, Einstellungen);

    [Fact]
    public void Pflichtnachweis_fuer_alle_fehlt_ist_rot_ohne_Datum()
    {
        var eintrag = Assert.Single(Berechnen());
        Assert.Equal("Unterweisung", eintrag.Bezeichnung);
        Assert.Null(eintrag.FaelligAm);
        Assert.Equal(Fristampel.Rot, eintrag.Ampel);
    }

    [Fact]
    public void Fuehrerscheinkontrolle_nur_mit_Fahrerlaubnis()
    {
        Assert.DoesNotContain(Berechnen(), e => e.Bezeichnung == "Führerscheinkontrolle");

        var mitC = Berechnen([Hat(KlasseC, new(2010, 5, 1))], [Nachweis(Fuehrerscheinkontrolle, Heute.AddMonths(-5))]);
        var kontrolle = Assert.Single(mitC, e => e.Bezeichnung == "Führerscheinkontrolle");
        Assert.Equal(Heute.AddMonths(1), kontrolle.FaelligAm);
        Assert.Equal(Fristampel.Gruen, kontrolle.Ampel);
    }

    [Fact]
    public void Juengster_Nachweis_zaehlt()
    {
        var eintraege = Berechnen(n: [Nachweis(Unterweisung, Heute.AddMonths(-13)), Nachweis(Unterweisung, Heute.AddMonths(-1))]);
        Assert.Equal(Fristampel.Gruen, Assert.Single(eintraege).Ampel);
    }

    [Fact]
    public void Freiwilliger_Nachweis_erscheint_erst_wenn_erfasst()
    {
        Assert.DoesNotContain(Berechnen(), e => e.Bezeichnung == "Freiwillig");
        var eintrag = Assert.Single(Berechnen(n: [Nachweis(Freiwillig, Heute.AddDays(-350))]), e => e.Bezeichnung == "Freiwillig");
        Assert.Equal(Fristampel.Gelb, eintrag.Ampel);
    }

    [Fact]
    public void Befristete_Qualifikation_mit_Auffrischung()
    {
        var eintraege = Berechnen([Hat(ErsteHilfe, new(2022, 1, 1), new(2024, 1, 1)), Hat(ErsteHilfe, new(2025, 1, 10), new(2026, 12, 1))]);
        var eh = Assert.Single(eintraege, e => e.Bereich == Fristbereich.Qualifikation);
        Assert.Equal(new DateOnly(2026, 12, 1), eh.FaelligAm);
        Assert.Equal(Fristampel.Gelb, eh.Ampel);
    }

    [Fact]
    public void Atemschutz_nur_fuer_Geraetetraeger_und_Vorsorge_ist_gesundheitsbezogen()
    {
        Assert.DoesNotContain(Berechnen(), e => e.Bereich == Fristbereich.Atemschutz);

        var vorsorge = new AtemschutzVorsorge { UntersuchtAm = Heute.AddYears(-1), Ergebnis = VorsorgeErgebnis.Geeignet, NaechsteFaellig = Heute.AddYears(2) };
        var uebungen = AtemschutzStatus.JaehrlichePflichten.Select(art => new AtemschutzNachweis { Art = art, Datum = Heute.AddMonths(-11) }).ToList();
        var eintraege = Berechnen([Hat(Agt, new(2015, 3, 1))], v: [vorsorge], a: uebungen).Where(e => e.Bereich == Fristbereich.Atemschutz).ToList();

        Assert.Equal(4, eintraege.Count);
        var g26 = Assert.Single(eintraege, e => e.Gesundheitsbezogen);
        Assert.Equal(Fristampel.Gruen, g26.Ampel);
        Assert.All(eintraege.Where(e => !e.Gesundheitsbezogen), e => Assert.Equal(Fristampel.Gelb, e.Ampel));
    }

    [Fact]
    public void Vorsorge_mit_Bedenken_zeigt_kein_Datum()
    {
        var vorsorge = new AtemschutzVorsorge { UntersuchtAm = Heute.AddDays(-5), Ergebnis = VorsorgeErgebnis.NichtGeeignet, NaechsteFaellig = Heute.AddYears(1) };
        var g26 = Assert.Single(Berechnen([Hat(Agt, new(2015, 3, 1))], v: [vorsorge]), e => e.Gesundheitsbezogen);
        Assert.Null(g26.FaelligAm);
        Assert.Equal(Fristampel.Rot, g26.Ampel);
    }

    [Fact]
    public void Inaktive_Nachweisart_wird_ignoriert()
    {
        var art = new Nachweisart { Code = "ALT", Bezeichnung = "Alt", Pflicht = NachweisPflicht.AlleEinsatzkraefte, Aktiv = false };
        var eintraege = Personalfristen.Berechnen(new Fristdaten(Guid.NewGuid(), "M", [], [], [], []), [art], Heute, Einstellungen);
        Assert.Empty(eintraege);
    }

    [Fact]
    public void Intervall_je_Atemschutzuebung_einstellbar()
    {
        var einstellungen = Einstellungen with
        {
            AtemschutzIntervallMonate = new Dictionary<AtemschutzNachweisArt, int>
            {
                [AtemschutzNachweisArt.Belastungsuebung] = 24,
                [AtemschutzNachweisArt.Einsatzuebung] = 12,
                [AtemschutzNachweisArt.Unterweisung] = 12,
            },
        };
        var uebungen = AtemschutzStatus.JaehrlichePflichten.Select(art => new AtemschutzNachweis { Art = art, Datum = Heute.AddMonths(-13) }).ToList();
        var eintraege = Personalfristen.Berechnen(
            new Fristdaten(Guid.NewGuid(), "M", [Hat(Agt, new(2015, 3, 1))], [], [], uebungen), [], Heute, einstellungen);

        Assert.Equal(Fristampel.Gruen, eintraege.Single(e => e.Bezeichnung.StartsWith("Belastungsübung")).Ampel);
        Assert.Equal(Fristampel.Rot, eintraege.Single(e => e.Bezeichnung == "Einsatzübung").Ampel);
    }

    [Fact]
    public void Untersuchung_mit_Gesundheitsbezug_wird_markiert()
    {
        var untersuchung = new Nachweisart { Code = "U", Bezeichnung = "Untersuchung", IntervallMonate = 24, Pflicht = NachweisPflicht.AlleEinsatzkraefte, Gesundheitsbezogen = true };
        var eintrag = Assert.Single(Personalfristen.Berechnen(new Fristdaten(Guid.NewGuid(), "M", [], [], [], []), [untersuchung], Heute, Einstellungen));
        Assert.True(eintrag.Gesundheitsbezogen);
    }

    [Theory]
    [InlineData(1980, "2028-03-01")] // 44 Jahre am Untersuchungstag: Standardintervall 36 Monate
    [InlineData(1970, "2026-03-01")] // 54 Jahre: kürzeres Intervall 12 Monate
    public void Vorsorge_Vorschlag_nach_Alter_am_Untersuchungstag(int geburtsjahr, string erwartet)
    {
        var vorschlag = AtemschutzVorsorge.NaechsteVorschlagen(new(2025, 3, 1), new(geburtsjahr, 6, 1), 36, 12, 50);
        Assert.Equal(DateOnly.Parse(erwartet, System.Globalization.CultureInfo.InvariantCulture), vorschlag);
    }
}
