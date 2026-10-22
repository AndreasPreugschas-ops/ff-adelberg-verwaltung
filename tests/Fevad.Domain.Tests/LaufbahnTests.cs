using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class LaufbahnTests
{
    private static readonly Laufbahnregeln Regeln = Laufbahnregeln.Satzung;
    private static readonly Dienstzeitregeln Ehrenzeichen = new(14, 0);

    private static Abschnitt A(string code, string von, string? bis = null, MitgliedStatus status = MitgliedStatus.Aktiv, bool vordienst = false) =>
        new(Guid.NewGuid(), code, code, status, DateOnly.Parse(von), bis is null ? null : DateOnly.Parse(bis), vordienst);

    private static DateOnly D(string text) => DateOnly.Parse(text);

    [Fact]
    public void Jugendfeuerwehr_meldet_Uebertritt_ab_17_und_Pflicht_zum_18()
    {
        var geburt = D("2009-01-15");
        var jugend = A(AbteilungCode.Jugend, "2020-09-01");

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
        var probe = Laufbahnhinweise.Berechnen(D("1990-05-05"), [A(AbteilungCode.Einsatz, "2026-01-10", status: MitgliedStatus.Probezeit)], Regeln, D("2026-11-01"));
        Assert.Equal(D("2027-01-10"), Assert.Single(probe).FaelligAm);

        var alt = Laufbahnhinweise.Berechnen(D("1961-12-24"), [A(AbteilungCode.Einsatz, "1980-01-01")], Regeln, D("2026-10-22"));
        var grenze = Assert.Single(alt);
        Assert.Equal(HinweisArt.AltersgrenzeEinsatz, grenze.Art);
        Assert.Equal(D("2026-12-24"), grenze.FaelligAm);

        var mit55 = Laufbahnhinweise.Berechnen(D("1971-11-30"), [A(AbteilungCode.Einsatz, "1990-01-01")], Regeln, D("2026-10-22"));
        Assert.Equal(HinweisArt.AltersabteilungMoeglich, Assert.Single(mit55).Art);

        Assert.Empty(Laufbahnhinweise.Berechnen(D("1980-03-01"), [A(AbteilungCode.Einsatz, "2000-01-01")], Regeln, D("2026-10-22")));
    }

    [Fact]
    public void Altersgrenzen_beim_Uebertritt_werden_geprueft()
    {
        Assert.NotNull(Mitgliedschaftswechsel.Pruefen(D("2010-06-01"), AbteilungCode.Einsatz, D("2026-10-22"), Regeln));
        Assert.Null(Mitgliedschaftswechsel.Pruefen(D("2009-06-01"), AbteilungCode.Einsatz, D("2026-10-22"), Regeln));
        Assert.NotNull(Mitgliedschaftswechsel.Pruefen(D("1975-01-01"), AbteilungCode.Alters, D("2026-10-22"), Regeln));
        Assert.Null(Mitgliedschaftswechsel.Pruefen(D("1971-01-01"), AbteilungCode.Alters, D("2026-10-22"), Regeln));
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
            A(AbteilungCode.Jugend, "2002-01-01", "2007-12-31"),
            A(AbteilungCode.Einsatz, "2008-01-01", "2015-12-31"),
            A(AbteilungCode.Einsatz, "2014-01-01", "2016-12-31", vordienst: true),
            A(AbteilungCode.Einsatz, "2018-01-01", status: MitgliedStatus.Aktiv),
            A(AbteilungCode.Einsatz, "2017-01-01", "2017-12-31", status: MitgliedStatus.Befreit),
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
        var zeit = Dienstzeitrechnung.Einsatzdienst(D("1980-01-01"), [A(AbteilungCode.Einsatz, "2000-01-01")], Ehrenzeichen);
        Assert.Equal(D("2015-01-01"), zeit.ErreichtAm(15));
        Assert.Equal(D("2025-01-01"), zeit.ErreichtAm(25));
        Assert.Equal(14, zeit.VolleJahreBis(D("2014-12-31")));
        Assert.Equal(15, zeit.VolleJahreBis(D("2015-01-01")));
    }

    [Fact]
    public void Altersabteilung_zaehlt_nur_mit_Parameter()
    {
        var abschnitte = new[] { A(AbteilungCode.Einsatz, "1990-01-01", "2020-12-31"), A(AbteilungCode.Alters, "2021-01-01") };
        Assert.Null(Dienstzeitrechnung.Einsatzdienst(D("1960-06-01"), abschnitte, Ehrenzeichen).ErreichtAm(40));
        Assert.Equal(D("2030-01-01"), Dienstzeitrechnung.Einsatzdienst(D("1960-06-01"), abschnitte, new(14, 70)).ErreichtAm(40));
    }

    private static Ehrungsart Fez(int jahre) => new()
    {
        Code = $"FEZ{jahre}", Bezeichnung = $"FEZ {jahre}", Grundlage = EhrungGrundlage.Einsatzdienst, Jahre = jahre, Reihe = "FEZ", VorlaufMonate = 6,
    };

    [Fact]
    public void Ehrung_schlaegt_je_Reihe_nur_die_hoechste_offene_Stufe_vor()
    {
        var arten = new[] { Fez(15), Fez(25), Fez(40) };
        var zeit = Dienstzeitrechnung.Einsatzdienst(D("1970-01-01"), [A(AbteilungCode.Einsatz, "1999-01-01")], Ehrenzeichen);

        var vorschlag = Assert.Single(Ehrungsvorschlaege.Ermitteln(arten, [], zeit, zeit, D("2026-10-22")));
        Assert.Equal(25, vorschlag.Jahre);
        Assert.Equal(D("2024-01-01"), vorschlag.ErreichtAm);

        Assert.Empty(Ehrungsvorschlaege.Ermitteln(arten, [arten[1].Id], zeit, zeit, D("2026-10-22")));

        // Vorlauf: 40 Jahre am 01.01.2039, Vorschlag ab Juli 2038.
        Assert.Equal(40, Assert.Single(Ehrungsvorschlaege.Ermitteln(arten, [arten[1].Id], zeit, zeit, D("2038-07-01"))).Jahre);
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
