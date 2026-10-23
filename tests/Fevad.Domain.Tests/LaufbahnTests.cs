using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class LaufbahnTests
{
    private static readonly Laufbahnregeln Regeln = Laufbahnregeln.Satzung;
    private static readonly Dienstzeitregeln Ehrenzeichen = new(14, 0);

    private static Abschnitt A(
        AbteilungArt art, string von, string? bis = null, MitgliedStatus status = MitgliedStatus.Aktiv, bool vordienst = false,
        Vordienstart? vordienstart = null, Befreiungsgrund? grund = null, bool bestaetigt = false) =>
        new(Guid.NewGuid(), art, art.ToString(), status, DateOnly.Parse(von), bis is null ? null : DateOnly.Parse(bis), vordienst || vordienstart is not null, vordienstart, grund, bestaetigt);

    private static DateOnly D(string text) => DateOnly.Parse(text);

    [Fact]
    public void Jugendfeuerwehr_meldet_Uebertritt_ab_17_und_Pflicht_zum_18()
    {
        var geburt = D("2009-01-15");
        var jugend = A(AbteilungArt.Jugend, "2020-09-01");

        var mit17 = Laufbahnhinweise.Berechnen(geburt, [jugend], Regeln, D("2025-12-01"));
        var h = Assert.Single(mit17);
        Assert.False(h.Pflicht);
        Assert.Equal(D("2026-01-15"), h.FaelligAm);

        var kurzVor18 = Laufbahnhinweise.Berechnen(geburt, [jugend], Regeln, D("2026-11-01"));
        var pflicht = Assert.Single(kurzVor18);
        Assert.True(pflicht.Pflicht);
        Assert.Equal(D("2027-01-15"), pflicht.FaelligAm);
        Assert.Equal(Fristampel.Gelb, pflicht.Ampel(D("2026-11-01")));
        Assert.Equal(Fristampel.Rot, pflicht.Ampel(D("2027-02-01")));
    }

    [Fact]
    public void Einsatzabteilung_meldet_Probezeit_Altersgrenze_und_Altersabteilung()
    {
        var probe = Laufbahnhinweise.Berechnen(D("1990-05-05"), [A(AbteilungArt.Einsatz, "2026-01-10", status: MitgliedStatus.Probezeit)], Regeln, D("2026-11-01"));
        Assert.Equal(D("2027-01-10"), Assert.Single(probe).FaelligAm);

        var alt = Laufbahnhinweise.Berechnen(D("1961-12-24"), [A(AbteilungArt.Einsatz, "1980-01-01")], Regeln, D("2026-10-22"));
        var grenze = Assert.Single(alt);
        Assert.Equal(HinweisArt.AltersgrenzeEinsatz, grenze.Art);
        Assert.Equal(D("2026-12-24"), grenze.FaelligAm);

        var mit55 = Laufbahnhinweise.Berechnen(D("1971-11-30"), [A(AbteilungArt.Einsatz, "1990-01-01")], Regeln, D("2026-10-22"));
        Assert.Equal(HinweisArt.AltersabteilungMoeglich, Assert.Single(mit55).Art);

        Assert.Empty(Laufbahnhinweise.Berechnen(D("1980-03-01"), [A(AbteilungArt.Einsatz, "2000-01-01")], Regeln, D("2026-10-22")));
    }

    [Fact]
    public void Eingetragenes_Probezeitende_geht_vor()
    {
        var verkuerzt = A(AbteilungArt.Einsatz, "2026-07-01", status: MitgliedStatus.Probezeit) with { ProbezeitBis = D("2026-12-31") };
        var hinweis = Assert.Single(Laufbahnhinweise.Berechnen(D("2008-05-05"), [verkuerzt], Regeln, D("2026-11-01")));
        Assert.Equal(HinweisArt.ProbezeitEnde, hinweis.Art);
        Assert.Equal(D("2026-12-31"), hinweis.FaelligAm);

        // Verlängert: Ende liegt außerhalb des Vorlaufs, also noch kein Hinweis.
        var verlaengert = A(AbteilungArt.Einsatz, "2025-12-01", status: MitgliedStatus.Probezeit) with { ProbezeitBis = D("2027-06-30") };
        Assert.Empty(Laufbahnhinweise.Berechnen(D("1990-05-05"), [verlaengert], Regeln, D("2026-11-01")));
    }

    [Fact]
    public void Probezeit_aendern_und_bei_Befreiung_mitnehmen()
    {
        var m = new Mitgliedschaft { MitgliedId = Guid.NewGuid(), AbteilungId = Guid.NewGuid(), Status = MitgliedStatus.Probezeit, GueltigAb = D("2026-03-01") };
        Assert.Equal(D("2027-03-01"), m.ProbezeitEnde(12));

        Mitgliedschaftswechsel.ProbezeitAendern(m, D("2027-09-01"), " verlängert, Ausschuss 01.02.2027 ");
        Assert.Equal(D("2027-09-01"), m.ProbezeitEnde(12));
        Assert.Equal("verlängert, Ausschuss 01.02.2027", m.ProbezeitBemerkung);
        Assert.Throws<InvalidOperationException>(() => Mitgliedschaftswechsel.ProbezeitAendern(m, D("2026-01-01"), null));

        var neue = Mitgliedschaftswechsel.Befreien(m, D("2026-06-01"), D("2026-08-31"), Befreiungsgrund.Krankheit);
        Assert.All(neue, n => Assert.Equal(D("2027-09-01"), n.ProbezeitBis));
        Assert.Equal(MitgliedStatus.Probezeit, neue[1].Status);

        var aktiv = new Mitgliedschaft { MitgliedId = Guid.NewGuid(), AbteilungId = Guid.NewGuid(), Status = MitgliedStatus.Aktiv, GueltigAb = D("2020-01-01") };
        Assert.Throws<InvalidOperationException>(() => Mitgliedschaftswechsel.ProbezeitAendern(aktiv, D("2027-01-01"), null));
    }

    [Fact]
    public void Kinderfeuerwehr_hat_eigene_Altersgrenzen_und_meldet_Uebertritt_in_die_Jugend()
    {
        Assert.NotNull(Mitgliedschaftswechsel.Pruefen(D("2021-01-01"), AbteilungArt.Kinder, D("2026-10-22"), Regeln));
        Assert.Null(Mitgliedschaftswechsel.Pruefen(D("2019-01-01"), AbteilungArt.Kinder, D("2026-10-22"), Regeln));
        Assert.NotNull(Mitgliedschaftswechsel.Pruefen(D("2016-01-01"), AbteilungArt.Kinder, D("2026-10-22"), Regeln));

        var kinder = A(AbteilungArt.Kinder, "2023-03-01");
        var hinweis = Assert.Single(Laufbahnhinweise.Berechnen(D("2016-12-10"), [kinder], Regeln, D("2026-11-01")));
        Assert.Equal(HinweisArt.UebertrittKinder, hinweis.Art);
        Assert.True(hinweis.Pflicht);
        Assert.Equal(D("2026-12-10"), hinweis.FaelligAm);
    }

    [Fact]
    public void Kinderfeuerwehr_zaehlt_nicht_fuer_das_Ehrenzeichen()
    {
        var zeit = Dienstzeitrechnung.Anrechnen(D("2010-01-01"), [A(AbteilungArt.Kinder, "2016-01-01", "2019-12-31")], Ehrenzeichen, false).Zeit;
        Assert.Equal(0, zeit.VolleJahreBis(D("2026-10-22")));
    }

    [Fact]
    public void Altersgrenzen_beim_Uebertritt_werden_geprueft()
    {
        Assert.NotNull(Mitgliedschaftswechsel.Pruefen(D("2010-06-01"), AbteilungArt.Einsatz, D("2026-10-22"), Regeln));
        Assert.Null(Mitgliedschaftswechsel.Pruefen(D("2009-06-01"), AbteilungArt.Einsatz, D("2026-10-22"), Regeln));
        Assert.NotNull(Mitgliedschaftswechsel.Pruefen(D("1975-01-01"), AbteilungArt.Alters, D("2026-10-22"), Regeln));
        Assert.Null(Mitgliedschaftswechsel.Pruefen(D("1971-01-01"), AbteilungArt.Alters, D("2026-10-22"), Regeln));
    }

    [Fact]
    public void Jugendfeuerwehr_nimmt_nur_bis_zum_vollendeten_17_Lebensjahr_auf()
    {
        // Satzung § 7 Abs. 2: Aufnahme bis 17, Mindestalter legt der Ausschuss fest.
        Assert.Null(Mitgliedschaftswechsel.Pruefen(D("2009-10-23"), AbteilungArt.Jugend, D("2026-10-22"), Regeln));
        Assert.NotNull(Mitgliedschaftswechsel.Pruefen(D("2009-10-22"), AbteilungArt.Jugend, D("2026-10-22"), Regeln));
        Assert.Null(Mitgliedschaftswechsel.Pruefen(D("2016-10-22"), AbteilungArt.Jugend, D("2026-10-22"), Regeln));
        Assert.NotNull(Mitgliedschaftswechsel.Pruefen(D("2016-10-23"), AbteilungArt.Jugend, D("2026-10-22"), Regeln));
        Assert.Null(Mitgliedschaftswechsel.Pruefen(D("2018-01-01"), AbteilungArt.Jugend, D("2026-10-22"), Regeln with { JugendAufnahmeAbAlter = 0 }));
    }

    [Fact]
    public void Uebertritt_beendet_alten_Abschnitt_am_Vortag()
    {
        var alt = new Mitgliedschaft { MitgliedId = Guid.NewGuid(), AbteilungId = Guid.NewGuid(), GueltigAb = D("2020-01-01"), Status = MitgliedStatus.Aktiv };
        var ziel = Guid.NewGuid();

        var neu = Mitgliedschaftswechsel.Uebertreten(alt, ziel, MitgliedStatus.Probezeit, D("2026-10-22"));

        Assert.Equal(D("2026-10-21"), alt.GueltigBis);
        Assert.Equal(Austrittsgrund.Uebertritt, alt.Austrittsgrund);
        Assert.Equal((alt.MitgliedId, ziel, D("2026-10-22"), MitgliedStatus.Probezeit), (neu.MitgliedId, neu.AbteilungId, neu.GueltigAb, neu.Status));
        Assert.Throws<InvalidOperationException>(() => Mitgliedschaftswechsel.Beenden(alt, D("2026-12-31"), null));
    }

    [Fact]
    public void Dienstzeit_zaehlt_Jugend_ab_14_und_Doppelzeiten_einfach()
    {
        var geburt = D("1990-03-10");
        var abschnitte = new[]
        {
            A(AbteilungArt.Jugend, "2002-01-01", "2007-12-31"),
            A(AbteilungArt.Einsatz, "2008-01-01", "2015-12-31"),
            A(AbteilungArt.Einsatz, "2014-01-01", "2016-12-31", vordienst: true),
            A(AbteilungArt.Einsatz, "2018-01-01", status: MitgliedStatus.Aktiv),
            A(AbteilungArt.Einsatz, "2017-01-01", "2017-12-31", status: MitgliedStatus.Befreit),
        };

        var zeit = Dienstzeitrechnung.Einsatzdienst(geburt, abschnitte, Ehrenzeichen);

        // 10.03.2004 bis 31.12.2016 durchgehend, 2017 befreit, ab 2018 wieder.
        Assert.Equal([(D("2004-03-10"), (DateOnly?)D("2016-12-31")), (D("2018-01-01"), null)], zeit.Zeitraeume);
        Assert.Equal(12, zeit.VolleJahreBis(D("2017-06-01")));
        Assert.Equal(D("2020-03-10"), zeit.ErreichtAm(15));
    }

    [Fact]
    public void Funfzehn_Jahre_ohne_Unterbrechung_sind_am_Jahrestag_erreicht()
    {
        var zeit = Dienstzeitrechnung.Einsatzdienst(D("1980-01-01"), [A(AbteilungArt.Einsatz, "2000-01-01")], Ehrenzeichen);
        Assert.Equal(D("2015-01-01"), zeit.ErreichtAm(15));
        Assert.Equal(D("2025-01-01"), zeit.ErreichtAm(25));
        Assert.Equal(14, zeit.VolleJahreBis(D("2014-12-31")));
        Assert.Equal(15, zeit.VolleJahreBis(D("2015-01-01")));
    }

    private static readonly Dienstzeitregeln Vwv2025 = new(14, 70, 12, D("2018-01-01"));

    [Fact]
    public void Altersabteilung_zaehlt_nur_mit_Bestaetigung_65plus_bis_70()
    {
        var unbestaetigt = new[] { A(AbteilungArt.Einsatz, "1990-01-01", "2020-12-31"), A(AbteilungArt.Alters, "2021-01-01") };
        var ohne = Dienstzeitrechnung.Anrechnen(D("1960-06-01"), unbestaetigt, Vwv2025, true);
        Assert.Null(ohne.Zeit.ErreichtAm(40));
        Assert.Contains(ohne.NichtAngerechnet, t => t.Contains("65plus"));

        var bestaetigt = new[] { A(AbteilungArt.Einsatz, "1990-01-01", "2020-12-31"), A(AbteilungArt.Alters, "2021-01-01", bestaetigt: true) };
        var zeit = Dienstzeitrechnung.Einsatzdienst(D("1960-06-01"), bestaetigt, Vwv2025);
        Assert.Equal(D("2030-01-01"), zeit.ErreichtAm(40));

        // Mit 70 ist Schluss: 31.05.2030 ist der letzte angerechnete Tag.
        Assert.Equal(D("2030-05-31"), zeit.Zeitraeume[^1].Bis);
    }

    [Fact]
    public void Jugendzeit_zaehlt_bei_Eintritt_ab_2018_nur_mit_Leistungsspange()
    {
        var abschnitte = new[] { A(AbteilungArt.Jugend, "2018-03-01", "2022-12-31"), A(AbteilungArt.Einsatz, "2023-01-01") };
        var geburt = D("2006-01-10");

        var ohne = Dienstzeitrechnung.Anrechnen(geburt, abschnitte, Vwv2025, false);
        Assert.Equal(D("2023-01-01"), ohne.Zeit.Zeitraeume[0].Von);
        Assert.Contains(ohne.NichtAngerechnet, t => t.Contains("Leistungsspange"));

        var mit = Dienstzeitrechnung.Anrechnen(geburt, abschnitte, Vwv2025, true);
        Assert.Equal(D("2020-01-10"), mit.Zeit.Zeitraeume[0].Von);
        Assert.Empty(mit.NichtAngerechnet);

        // Eintritt vor 2018: keine Pflicht.
        var frueher = new[] { A(AbteilungArt.Jugend, "2017-09-01", "2022-12-31"), A(AbteilungArt.Einsatz, "2023-01-01") };
        Assert.Equal(D("2020-01-10"), Dienstzeitrechnung.Einsatzdienst(geburt, frueher, Vwv2025, false).Zeitraeume[0].Von);
    }

    [Fact]
    public void Befreiung_zaehlt_nur_bei_Krankheit_Mutterschutz_und_Kindererziehung_bis_ein_Jahr()
    {
        var geburt = D("1990-01-01");
        Abschnitt[] Mit(Befreiungsgrund grund) =>
        [
            A(AbteilungArt.Einsatz, "2010-01-01", "2014-12-31"),
            A(AbteilungArt.Einsatz, "2015-01-01", "2016-12-31", MitgliedStatus.Befreit, grund: grund),
            A(AbteilungArt.Einsatz, "2017-01-01"),
        ];

        Assert.Equal(D("2025-01-01"), Dienstzeitrechnung.Einsatzdienst(geburt, Mit(Befreiungsgrund.Krankheit), Vwv2025).ErreichtAm(15));
        Assert.Equal(D("2025-01-01"), Dienstzeitrechnung.Einsatzdienst(geburt, Mit(Befreiungsgrund.Mutterschutz), Vwv2025).ErreichtAm(15));

        // Kindererziehung: 2015 zählt, 2016 (366 Tage) nicht; der Tag verschiebt sich um die Lücke.
        var kind = Dienstzeitrechnung.Anrechnen(geburt, Mit(Befreiungsgrund.Kindererziehung), Vwv2025, true);
        Assert.Equal(D("2026-01-02"), kind.Zeit.ErreichtAm(15));
        Assert.Single(kind.NichtAngerechnet);

        Assert.Equal(D("2027-01-02"), Dienstzeitrechnung.Einsatzdienst(geburt, Mit(Befreiungsgrund.Sonstiges), Vwv2025).ErreichtAm(15));
    }

    [Fact]
    public void Wehrdienst_zaehlt_nur_nach_vorherigem_Feuerwehrdienst_und_Doppelzeiten_einfach()
    {
        var geburt = D("1985-01-01");
        var davor = new[]
        {
            A(AbteilungArt.Einsatz, "2003-01-01", "2004-06-30"),
            A(AbteilungArt.Einsatz, "2004-07-01", "2005-03-31", vordienstart: Vordienstart.Wehrdienst),
            A(AbteilungArt.Einsatz, "2005-04-01"),
            A(AbteilungArt.Einsatz, "2006-01-01", "2010-12-31", vordienstart: Vordienstart.BerufsOderWerkfeuerwehr),
        };
        var zeit = Dienstzeitrechnung.Einsatzdienst(geburt, davor, Vwv2025);
        Assert.Equal([(D("2003-01-01"), (DateOnly?)null)], zeit.Zeitraeume);

        var ohneVorher = new[]
        {
            A(AbteilungArt.Einsatz, "2003-01-01", "2003-12-31", vordienstart: Vordienstart.Freiwilligendienst),
            A(AbteilungArt.Einsatz, "2004-01-01"),
        };
        var anrechnung = Dienstzeitrechnung.Anrechnen(geburt, ohneVorher, Vwv2025, true);
        Assert.Equal(D("2004-01-01"), anrechnung.Zeit.Zeitraeume[0].Von);
        Assert.Contains(anrechnung.NichtAngerechnet, t => t.Contains("Freiwilligendienst"));
    }

    [Fact]
    public void Befreiung_hebt_die_Regeln_der_Abteilung_nicht_auf()
    {
        // Krank in der unbestätigten Altersabteilung zählt nicht; in der bestätigten nur bis 70.
        var geburt = D("1955-06-01");
        var krank = A(AbteilungArt.Alters, "2020-01-01", "2026-12-31", MitgliedStatus.Befreit, grund: Befreiungsgrund.Krankheit);
        Assert.Empty(Dienstzeitrechnung.Einsatzdienst(geburt, [krank], Vwv2025).Zeitraeume);
        var bestaetigt = krank with { Bestaetigt = true };
        Assert.Equal(D("2025-05-31"), Dienstzeitrechnung.Einsatzdienst(geburt, [bestaetigt], Vwv2025).Zeitraeume[0].Bis);

        // Krank in der Jugendfeuerwehr vor 14 zählt erst ab 14.
        var jung = A(AbteilungArt.Jugend, "2018-01-01", "2020-12-31", MitgliedStatus.Befreit, grund: Befreiungsgrund.Krankheit);
        Assert.Equal(D("2020-03-01"), Dienstzeitrechnung.Einsatzdienst(D("2006-03-01"), [jung], new Dienstzeitregeln(14, 70)).Zeitraeume[0].Von);
    }

    [Fact]
    public void Leistungsspange_richtet_sich_nach_dem_ersten_Eintritt_auch_in_einer_anderen_Wehr()
    {
        var geburt = D("2004-01-01");
        var vorher = A(AbteilungArt.Jugend, "2017-06-01", "2019-12-31", vordienstart: Vordienstart.AndereFeuerwehr);
        var hier = A(AbteilungArt.Jugend, "2020-01-01", "2021-12-31");
        Assert.Empty(Dienstzeitrechnung.Anrechnen(geburt, [vorher, hier], Vwv2025, false).NichtAngerechnet);

        var nurAndere = A(AbteilungArt.Jugend, "2019-01-01", "2021-12-31", vordienstart: Vordienstart.AndereFeuerwehr);
        Assert.Empty(Dienstzeitrechnung.Einsatzdienst(geburt, [nurAndere], Vwv2025, false).Zeitraeume);
    }

    [Fact]
    public void Befreiung_in_der_Probezeit_endet_wieder_in_der_Probezeit()
    {
        var alt = new Mitgliedschaft { MitgliedId = Guid.NewGuid(), AbteilungId = Guid.NewGuid(), GueltigAb = D("2026-01-01"), Status = MitgliedStatus.Probezeit };
        var befreit = Assert.Single(Mitgliedschaftswechsel.Befreien(alt, D("2026-03-01"), null, Befreiungsgrund.Krankheit));
        Assert.Equal(MitgliedStatus.Probezeit, Mitgliedschaftswechsel.BefreiungBeenden(befreit, D("2026-05-01"), alt.Status).Status);
        Assert.Equal(MitgliedStatus.Aktiv, Mitgliedschaftswechsel.BefreiungBeenden(new Mitgliedschaft { Status = MitgliedStatus.Befreit, GueltigAb = D("2026-01-01") }, D("2026-05-01"), MitgliedStatus.Aktiv).Status);
    }

    [Fact]
    public void Musikabteilung_zaehlt_nur_mit_Bestaetigung()
    {
        var geburt = D("1980-01-01");
        Assert.Empty(Dienstzeitrechnung.Einsatzdienst(geburt, [A(AbteilungArt.Musik, "2000-01-01")], Vwv2025).Zeitraeume);
        Assert.Equal(D("2015-01-01"), Dienstzeitrechnung.Einsatzdienst(geburt, [A(AbteilungArt.Musik, "2000-01-01", bestaetigt: true)], Vwv2025).ErreichtAm(15));
    }

    private static Ehrungsart Fez(int jahre) => new()
    {
        Code = $"FEZ{jahre}", Bezeichnung = $"FEZ {jahre}", Grundlage = EhrungGrundlage.Einsatzdienst, Jahre = jahre, Reihe = "FEZ", VorlaufWochen = 26,
    };

    private static Ehrungsgrundlagen Grundlagen(Dienstzeit einsatz, Dienstzeit? jugend = null, Dictionary<string, Dienstzeit>? funktionen = null, params string[] qualifikationen) =>
        new(einsatz, einsatz, jugend ?? new Dienstzeit([]), funktionen ?? [], qualifikationen.ToHashSet());

    private static ErfassteEhrung Erfasst(Ehrungsart art, EhrungStatus status = EhrungStatus.Vorgeschlagen, string? verliehen = null) =>
        new(art.Id, status, verliehen is null ? null : D(verliehen));

    [Fact]
    public void Ehrung_mit_Vorstufe_kommt_erst_nach_Verleihung_und_Abstand()
    {
        var silber = new Ehrungsart { Code = "JF-ENS", Bezeichnung = "Silber", Grundlage = EhrungGrundlage.Funktion, FunktionCode = "JW", Jahre = 5, VorlaufWochen = 6 };
        var gold = new Ehrungsart
        {
            Code = "JF-ENG", Bezeichnung = "Gold", Grundlage = EhrungGrundlage.Funktion, FunktionCode = "JW", Jahre = 5, VorlaufWochen = 6,
            VorstufeId = silber.Id, MindestabstandJahre = 2,
        };
        var jw = new Dictionary<string, Dienstzeit> { ["JW"] = new([(D("2015-01-01"), null)]) };
        var g = Grundlagen(new Dienstzeit([]), funktionen: jw);

        Assert.Equal("Silber", Assert.Single(Ehrungsvorschlaege.Ermitteln([silber, gold], [], g, D("2026-10-22"))).Ehrungsart);

        var verliehen = new[] { Erfasst(silber, EhrungStatus.Verliehen, "2025-03-15") };
        Assert.Empty(Ehrungsvorschlaege.Ermitteln([silber, gold], verliehen, g, D("2026-10-22")));
        var vorschlag = Assert.Single(Ehrungsvorschlaege.Ermitteln([silber, gold], verliehen, g, D("2027-03-01")));
        Assert.Equal(("Gold", D("2027-03-15")), (vorschlag.Ehrungsart, vorschlag.ErreichtAm));

        Assert.NotNull(Ehrungsvorschlaege.VorstufeHinweis(gold, silber, [], D("2026-10-22")));
        Assert.NotNull(Ehrungsvorschlaege.VorstufeHinweis(gold, silber, verliehen, D("2026-10-22")));
        Assert.Null(Ehrungsvorschlaege.VorstufeHinweis(gold, silber, verliehen, D("2027-03-15")));

        // Ohne Mindestabstand muss die Vorstufe trotzdem vorher verliehen sein.
        var ohneAbstand = new Ehrungsart { Code = "X", Bezeichnung = "X", VorstufeId = silber.Id };
        Assert.NotNull(Ehrungsvorschlaege.VorstufeHinweis(ohneAbstand, silber, verliehen, D("2020-01-01")));
        Assert.Null(Ehrungsvorschlaege.VorstufeHinweis(ohneAbstand, silber, verliehen, D("2025-03-15")));
    }

    [Fact]
    public void Traditionsnadel_nur_ohne_Jugendflamme_und_Leistungsspange()
    {
        var nadel = new Ehrungsart
        {
            Code = "JF-TN", Bezeichnung = "Traditionsnadel", Grundlage = EhrungGrundlage.Jugendfeuerwehr, Jahre = 2, VorlaufWochen = 6,
            NichtMitQualifikationen = [Qualifikation.CodeJugendflamme1, Qualifikation.CodeLeistungsspange],
        };
        var jugend = new Dienstzeit([(D("2022-09-01"), null)]);
        Assert.Single(Ehrungsvorschlaege.Ermitteln([nadel], [], Grundlagen(new Dienstzeit([]), jugend), D("2026-10-22")));
        Assert.Empty(Ehrungsvorschlaege.Ermitteln([nadel], [], Grundlagen(new Dienstzeit([]), jugend, null, Qualifikation.CodeJugendflamme1), D("2026-10-22")));
    }

    [Fact]
    public void Antragsschluss_ist_der_naechste_Termin()
    {
        Assert.Equal(D("2026-11-30"), Antragsschluss.Naechster("30.11.", D("2026-10-22")));
        Assert.Equal(D("2026-11-30"), Antragsschluss.Naechster("30.11.", D("2026-11-30")));
        Assert.Equal(D("2027-11-30"), Antragsschluss.Naechster("30.11", D("2026-12-01")));
        Assert.Null(Antragsschluss.Naechster("bald", D("2026-10-22")));
    }

    [Fact]
    public void Ehrung_schlaegt_je_Reihe_nur_die_hoechste_offene_Stufe_vor()
    {
        var arten = new[] { Fez(15), Fez(25), Fez(40) };
        var zeit = Dienstzeitrechnung.Einsatzdienst(D("1970-01-01"), [A(AbteilungArt.Einsatz, "1999-01-01")], Ehrenzeichen);

        var vorschlag = Assert.Single(Ehrungsvorschlaege.Ermitteln(arten, [], Grundlagen(zeit), D("2026-10-22")));
        Assert.Equal(25, vorschlag.Jahre);
        Assert.Equal(D("2024-01-01"), vorschlag.ErreichtAm);

        Assert.Empty(Ehrungsvorschlaege.Ermitteln(arten, [Erfasst(arten[1])], Grundlagen(zeit), D("2026-10-22")));

        // Vorlauf 26 Wochen: 40 Jahre am 01.01.2039, Vorschlag ab Anfang Juli 2038.
        Assert.Equal(40, Assert.Single(Ehrungsvorschlaege.Ermitteln(arten, [Erfasst(arten[1])], Grundlagen(zeit), D("2038-07-15"))).Jahre);
    }

    private static readonly DienstgradStufe Fmp = new(Guid.NewGuid(), "FMP", "FM(P)", 10, 12);
    private static readonly DienstgradStufe Fm = new(Guid.NewGuid(), "FM", "FM", 20, 12);
    private static readonly DienstgradStufe Ofm = new(Guid.NewGuid(), "OFM", "OFM", 30, 12);
    private static readonly DienstgradStufe Hfm = new(Guid.NewGuid(), "HFM", "HFM", 40, 12);
    private static readonly DienstgradStufe Lm = new(Guid.NewGuid(), "LM", "LM", 50, 12);
    private static readonly DienstgradStufe[] Stufen = [Fmp, Fm, Ofm, Hfm, Lm];

    private static readonly Befoerderungsweg[] Wege =
    [
        new() { DienstgradId = Fm.Id, ProbezeitBeendet = true, AlleQualifikationen = ["TM1"] },
        new() { DienstgradId = Ofm.Id, Bezeichnung = "Weg A", MindestjahreImDienstgrad = 10 },
        new() { DienstgradId = Ofm.Id, Bezeichnung = "Weg B", MindestjahreImDienstgrad = 3, AlleQualifikationen = ["TM2"], EineDerQualifikationen = ["MA", "AGT", "SPRF"] },
        new() { DienstgradId = Hfm.Id, MindestjahreImDienstgrad = 5, AlleQualifikationen = ["TF"] },
        new() { DienstgradId = Lm.Id, AlleQualifikationen = ["GF"], UeberspringenErlaubt = true },
    ];

    private static Befoerderungsstand Stand(DienstgradStufe? dg, string seit, bool probezeit, params string[] q) =>
        new(dg, D(seit), probezeit, q.ToHashSet());

    [Fact]
    public void Feuerwehrmann_nach_Probezeit_und_Truppmann_Teil_1()
    {
        Assert.Null(Befoerderungspruefung.Pruefen(Stand(Fmp, "2025-01-01", false, "TM1"), Stufen, Wege, D("2026-10-22")));
        Assert.Null(Befoerderungspruefung.Pruefen(Stand(Fmp, "2025-01-01", true), Stufen, Wege, D("2026-10-22")));

        var b = Befoerderungspruefung.Pruefen(Stand(Fmp, "2025-01-01", true, "TM1"), Stufen, Wege, D("2026-10-22"));
        Assert.Equal(Fm, b!.Dienstgrad);
        Assert.Equal(D("2026-01-01"), b.MoeglichAb);
    }

    [Fact]
    public void Oberfeuerwehrmann_frueher_mit_Lehrgaengen()
    {
        var ohne = Befoerderungspruefung.Pruefen(Stand(Fm, "2015-04-01", true, "TM1"), Stufen, Wege, D("2026-10-22"));
        Assert.Equal(("Weg A", D("2025-04-01")), (ohne!.Weg.Bezeichnung, ohne.MoeglichAb));

        var mit = Befoerderungspruefung.Pruefen(Stand(Fm, "2022-04-01", true, "TM1", "TM2", "AGT"), Stufen, Wege, D("2026-10-22"));
        Assert.Equal(("Weg B", D("2025-04-01")), (mit!.Weg.Bezeichnung, mit.MoeglichAb));

        Assert.Null(Befoerderungspruefung.Pruefen(Stand(Fm, "2022-04-01", true, "TM1", "TM2"), Stufen, Wege, D("2026-10-22")));
    }

    [Fact]
    public void Gruppenfuehrer_darf_zum_Loeschmeister_springen_aber_nicht_vor_Ablauf_der_Wartezeit()
    {
        var stand = Stand(Ofm, "2026-03-01", true, "TM1", "TM2", "TF", "GF");
        Assert.Null(Befoerderungspruefung.Pruefen(stand, Stufen, Wege, D("2026-10-22")));

        var b = Befoerderungspruefung.Pruefen(stand, Stufen, Wege, D("2027-03-01"));
        Assert.Equal(Lm, b!.Dienstgrad);
    }
}
