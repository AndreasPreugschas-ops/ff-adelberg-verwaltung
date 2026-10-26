using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Tests;

public class AbzeichenTests
{
    private static readonly Qualifikation Tm1 = new() { Code = "TM1", Bezeichnung = "Truppmann Teil 1", Art = QualifikationArt.Lehrgang };
    private static readonly Qualifikation Sprechfunker = new() { Code = "SPRF", Bezeichnung = "Sprechfunker", Art = QualifikationArt.Lehrgang };
    private static readonly Qualifikation Agt = new() { Code = Qualifikation.CodeAtemschutz, Bezeichnung = "Atemschutzgeräteträger", Art = QualifikationArt.Lehrgang };
    private static readonly Qualifikation Maschinist = new() { Code = Qualifikation.CodeMaschinist, Bezeichnung = "Maschinist", Art = QualifikationArt.Lehrgang };
    private static readonly Qualifikation KlasseC = new() { Code = "FS-C", Bezeichnung = "Fahrerlaubnis Klasse C", Kurzbezeichnung = "C", Art = QualifikationArt.Fahrerlaubnis, GueltigkeitMonate = 60 };
    private static readonly Qualifikation ErsteHilfe = new() { Code = "EH", Bezeichnung = "Erste Hilfe", Art = QualifikationArt.Lehrgang, GueltigkeitMonate = 24 };

    private static readonly Qualifikation Bronze = new() { Code = "LA-B", Bezeichnung = "Leistungsabzeichen Bronze", Art = QualifikationArt.Abzeichen, Voraussetzungen = "TM1" };
    private static readonly Qualifikation Silber = new() { Code = "LA-S", Bezeichnung = "Leistungsabzeichen Silber", Art = QualifikationArt.Abzeichen, VorstufeId = Bronze.Id, Vorstufe = Bronze, VorstufeAbstandJahre = 1, Voraussetzungen = "TM1; AGT|SPRF" };
    private static readonly Qualifikation Gold = new() { Code = "LA-G", Bezeichnung = "Leistungsabzeichen Gold", Art = QualifikationArt.Abzeichen, VorstufeId = Silber.Id, Vorstufe = Silber, VorstufeAbstandJahre = 1 };

    private static readonly Qualifikation Flamme1 = new() { Code = "JFL1", Bezeichnung = "Jugendflamme Stufe I", Art = QualifikationArt.Abzeichen, MindestalterJahre = 10, AlterNachJahrgang = true };
    private static readonly Qualifikation Flamme2 = new() { Code = "JFL2", Bezeichnung = "Jugendflamme Stufe II", Art = QualifikationArt.Abzeichen, VorstufeId = Flamme1.Id, Vorstufe = Flamme1, MindestalterJahre = 13, AlterNachJahrgang = true };
    private static readonly Qualifikation Flamme3 = new() { Code = "JFL3", Bezeichnung = "Jugendflamme Stufe III", Art = QualifikationArt.Abzeichen, VorstufeId = Flamme2.Id, Vorstufe = Flamme2, MindestalterJahre = 15, AlterNachJahrgang = true, Voraussetzungen = "EH" };
    private static readonly Qualifikation Spange = new() { Code = Qualifikation.CodeLeistungsspange, Bezeichnung = "Leistungsspange DJF", Art = QualifikationArt.Abzeichen, MindestalterJahre = 15, AlterNachJahrgang = true, MindestJugendMonate = 12 };

    private static QualifikationNachweis Hat(Qualifikation q, DateOnly erworben)
    {
        var n = new QualifikationNachweis { QualifikationId = q.Id, Qualifikation = q, ErworbenAm = erworben };
        n.GueltigkeitVorbelegen(q);
        return n;
    }

    private static readonly DateOnly Erwachsen = new(1990, 5, 1);

    [Fact]
    public void Silber_braucht_Bronze_aus_einem_frueheren_Jahr()
    {
        var abnahme = new DateOnly(2026, 7, 4);
        var gleichesJahr = Abzeichenpruefung.Pruefen(Silber, abnahme, Erwachsen, [Hat(Tm1, new(2020, 3, 1)), Hat(Agt, new(2021, 3, 1)), Hat(Bronze, new(2026, 5, 2))], null);
        var vorjahr = Abzeichenpruefung.Pruefen(Silber, abnahme, Erwachsen, [Hat(Tm1, new(2020, 3, 1)), Hat(Agt, new(2021, 3, 1)), Hat(Bronze, new(2025, 5, 2))], null);
        var ohne = Abzeichenpruefung.Pruefen(Silber, abnahme, Erwachsen, [Hat(Tm1, new(2020, 3, 1)), Hat(Agt, new(2021, 3, 1))], null);

        Assert.Contains(gleichesJahr, v => !v.Erfuellt && v.Text.Contains("frühestens 2027"));
        Assert.All(vorjahr, v => Assert.True(v.Erfuellt, v.Text));
        Assert.Contains(ohne, v => !v.Erfuellt && v.Text == "Leistungsabzeichen Bronze fehlt");
    }

    [Fact]
    public void Silber_reicht_Sprechfunker_statt_Atemschutz_aber_TM1_ist_Pflicht()
    {
        var abnahme = new DateOnly(2026, 7, 4);
        var namen = new Dictionary<string, string> { ["TM1"] = "Truppmann Teil 1", ["AGT"] = "Atemschutzgeräteträger", ["SPRF"] = "Sprechfunker" };

        var mitFunk = Abzeichenpruefung.Pruefen(Silber, abnahme, Erwachsen, [Hat(Tm1, new(2020, 3, 1)), Hat(Sprechfunker, new(2021, 3, 1)), Hat(Bronze, new(2024, 5, 2))], null, namen);
        var ohneTm1 = Abzeichenpruefung.Pruefen(Silber, abnahme, Erwachsen, [Hat(Sprechfunker, new(2021, 3, 1)), Hat(Bronze, new(2024, 5, 2))], null, namen);

        Assert.All(mitFunk, v => Assert.True(v.Erfuellt, v.Text));
        Assert.Contains(mitFunk, v => v.Text == "Atemschutzgeräteträger oder Sprechfunker");
        Assert.Contains(ohneTm1, v => !v.Erfuellt && v.Text == "Truppmann Teil 1");
    }

    [Fact]
    public void Jugendflamme_zaehlt_das_Alter_nach_Jahrgang()
    {
        // Geboren im Dezember 2013: im Jahr 2026 nach Jahrgang 13, nach Geburtstag erst 12.
        var geburt = new DateOnly(2013, 12, 20);
        var ergebnis = Abzeichenpruefung.Pruefen(Flamme2, new DateOnly(2026, 6, 1), geburt, [Hat(Flamme1, new(2024, 6, 1))], new DateOnly(2023, 1, 1));

        Assert.All(ergebnis, v => Assert.True(v.Erfuellt, v.Text));
        Assert.Contains(ergebnis, v => v.Text == "Mindestalter 13 (nach Jahrgang), ist 13");
    }

    [Fact]
    public void Jugendflamme_III_braucht_gueltige_Erste_Hilfe()
    {
        var geburt = new DateOnly(2011, 3, 1);
        var abnahme = new DateOnly(2026, 6, 1);
        var abgelaufen = Abzeichenpruefung.Pruefen(Flamme3, abnahme, geburt, [Hat(Flamme2, new(2024, 6, 1)), Hat(ErsteHilfe, new(2023, 1, 1))], null);
        var gueltig = Abzeichenpruefung.Pruefen(Flamme3, abnahme, geburt, [Hat(Flamme2, new(2024, 6, 1)), Hat(ErsteHilfe, new(2025, 1, 1))], null);

        Assert.Contains(abgelaufen, v => !v.Erfuellt && v.Text == "EH");
        Assert.All(gueltig, v => Assert.True(v.Erfuellt, v.Text));
    }

    [Fact]
    public void Leistungsspange_braucht_ein_Jahr_Jugendfeuerwehr()
    {
        var geburt = new DateOnly(2011, 3, 1);
        var abnahme = new DateOnly(2026, 6, 1);

        Assert.Contains(Abzeichenpruefung.Pruefen(Spange, abnahme, geburt, [], new DateOnly(2025, 9, 1)), v => !v.Erfuellt);
        Assert.All(Abzeichenpruefung.Pruefen(Spange, abnahme, geburt, [], new DateOnly(2025, 6, 1)), v => Assert.True(v.Erfuellt, v.Text));
        Assert.Contains(Abzeichenpruefung.Pruefen(Spange, abnahme, geburt, [], null), v => !v.Erfuellt);
    }

    [Fact]
    public void Nur_die_hoechste_Stufe_zaehlt_auch_ueber_mehrere_Stufen()
    {
        var katalog = new[] { Bronze, Silber, Gold, Flamme1, Flamme2, Flamme3 }.ToDictionary(q => q.Id);
        var bronze = Hat(Bronze, new(2022, 6, 1));
        var gold = Hat(Gold, new(2024, 6, 1));
        var flamme1 = Hat(Flamme1, new(2020, 6, 1));

        var abgeloest = Abzeichenpruefung.Abgeloest([bronze, gold, flamme1], katalog);

        Assert.Equal([bronze.Id], abgeloest);
    }

    [Fact]
    public void Funktionen_am_Abnahmetag_pruefen_Fahrerlaubnis_Eignung_und_Belastungsuebung()
    {
        var abnahme = new DateOnly(2026, 7, 4);
        var vorhandene = new[] { Hat(Maschinist, new(2018, 1, 1)), Hat(KlasseC, new(2022, 1, 1)), Hat(Agt, new(2019, 1, 1)) };
        var vorsorge = new AtemschutzVorsorge { UntersuchtAm = new(2025, 1, 10), Ergebnis = VorsorgeErgebnis.Geeignet, NaechsteFaellig = new(2028, 1, 10) };
        var alteUebung = new AtemschutzNachweis { Art = AtemschutzNachweisArt.Belastungsuebung, Datum = new(2025, 6, 1) };
        var neueUebung = new AtemschutzNachweis { Art = AtemschutzNachweisArt.Belastungsuebung, Datum = new(2025, 9, 1) };

        var zuAlt = Leistungsabzeichen.Funktionen(abnahme, vorhandene, [vorsorge], [alteUebung], 12);
        var passt = Leistungsabzeichen.Funktionen(abnahme, vorhandene, [vorsorge], [alteUebung, neueUebung], 12);
        var ohneAtemschutzrecht = Leistungsabzeichen.Funktionen(abnahme, vorhandene, null, null, 12);

        Assert.True(zuAlt.Single(f => f.Funktion == "Maschinist").Erfuellt);
        Assert.Equal("Fahrerlaubnis C", zuAlt.Single(f => f.Funktion == "Maschinist").Hinweis);
        Assert.False(zuAlt.Single(f => f.Funktion == "Atemschutz").Erfuellt);
        Assert.True(passt.Single(f => f.Funktion == "Atemschutz").Erfuellt);
        Assert.DoesNotContain(ohneAtemschutzrecht, f => f.Funktion == "Atemschutz");
        Assert.False(passt.Single(f => f.Funktion == "Gruppenführer").Erfuellt);
    }

    [Fact]
    public void Abgelaufene_Fahrerlaubnis_reicht_nicht_fuer_den_Maschinisten()
    {
        var abnahme = new DateOnly(2026, 7, 4);
        var maschinist = Leistungsabzeichen.Funktionen(abnahme, [Hat(Maschinist, new(2018, 1, 1)), Hat(KlasseC, new(2020, 1, 1))], null, null, 12)
            .Single(f => f.Funktion == "Maschinist");

        Assert.False(maschinist.Erfuellt);
        Assert.Equal("keine gültige Fahrerlaubnis", maschinist.Hinweis);
    }

    private static readonly Leistungsspange.Regeln Spangenregeln = new(new DateOnly(2018, 1, 1), 14, 18, 365);

    [Theory]
    [InlineData("2012-03-01", "2022-01-01", false, "2026-10-22", "Gelb")]   // 14 Jahre, ohne Spange
    [InlineData("2009-03-01", "2019-01-01", false, "2026-10-22", "Rot")]    // 17 Jahre: weniger als ein Jahr bis zum Übertritt
    [InlineData("2013-03-01", "2022-01-01", false, "2026-10-22", null)]     // erst 13
    [InlineData("2012-03-01", "2022-01-01", true, "2026-10-22", null)]      // Spange vorhanden
    [InlineData("2010-03-01", "2017-06-01", false, "2026-10-22", null)]     // Eintritt vor dem Stichtag
    public void Leistungsspange_fehlt_ab_14_bis_zum_Uebertritt(string geburt, string eintritt, bool hatSpange, string stichtag, string? ampel)
    {
        var hinweis = Leistungsspange.Hinweis(Guid.NewGuid(), "Muster, Max", DateOnly.Parse(geburt), DateOnly.Parse(eintritt), hatSpange, DateOnly.Parse(stichtag), Spangenregeln);

        Assert.Equal(ampel, hinweis?.Ampel.ToString());
        if (hinweis is not null)
        {
            Assert.Equal(Fristbereich.Jugend, hinweis.Bereich);
            Assert.Equal(DateOnly.Parse(geburt).AddYears(18), hinweis.FaelligAm);
        }
    }

    [Fact]
    public void Voraussetzungen_werden_als_Gruppen_gelesen()
    {
        var gruppen = Abzeichenpruefung.Gruppen(" tm1 ;AGT | sprf;; ");

        Assert.Equal(2, gruppen.Count);
        Assert.Equal(["TM1"], gruppen[0]);
        Assert.Equal(["AGT", "SPRF"], gruppen[1]);
        Assert.Empty(Abzeichenpruefung.Gruppen(null));
    }

    [Fact]
    public void Regelwerk_ersetzt_Mindestalter_und_begrenzt_das_Alter_der_Erste_Hilfe()
    {
        var abnahme = new DateOnly(2026, 6, 13);
        var geboren = new DateOnly(2011, 9, 1); // Jahrgangsalter 15
        var regelA = new AbzeichenregelwerkStufe { QualifikationId = Flamme3.Id, MindestalterJahre = 15, Voraussetzungen = "EH" };
        var regelB = new AbzeichenregelwerkStufe { QualifikationId = Flamme3.Id, MindestalterJahre = 16, Voraussetzungen = "EH", NachweisHoechstensMonate = 12 };
        var vorhanden = new[] { Hat(Flamme2, new(2024, 6, 1)), new QualifikationNachweis { QualifikationId = ErsteHilfe.Id, Qualifikation = ErsteHilfe, ErworbenAm = new(2024, 9, 1) } };

        var nachA = Abzeichenpruefung.Pruefen(Flamme3, abnahme, geboren, vorhanden, null, null, regelA);
        var nachB = Abzeichenpruefung.Pruefen(Flamme3, abnahme, geboren, vorhanden, null, null, regelB);
        var katalog = Abzeichenpruefung.Pruefen(Flamme3, abnahme, geboren, vorhanden, null);

        Assert.All(nachA, v => Assert.True(v.Erfuellt, v.Text));
        Assert.All(katalog, v => Assert.True(v.Erfuellt, v.Text));
        Assert.Contains(nachB, v => !v.Erfuellt && v.Text.StartsWith("Mindestalter 16"));
        Assert.Contains(nachB, v => !v.Erfuellt && v.Text == "EH (höchstens 12 Monate alt)");
    }
}
