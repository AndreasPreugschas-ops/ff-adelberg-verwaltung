using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Tests;

public class EntschaedigungTests
{
    [Theory]
    [InlineData(60, 61, 2.0, 30.0)]   // angefangene Stunde
    [InlineData(60, 60, 1.0, 15.0)]
    [InlineData(30, 70, 1.5, 22.5)]   // halbe Stunden
    [InlineData(1, 70, 1.17, 17.55)]  // minutengenau, zwei Stellen
    [InlineData(0, 45, 0.75, 11.25)]  // 0 wie minutengenau
    public void Stundensatz_rundet_angefangene_Einheiten_auf(int rundung, int minuten, double stunden, double betrag)
    {
        var regel = new Entschaedigungsregel(15m, rundung);

        var ergebnis = regel.Berechnen(TimeSpan.FromMinutes(minuten), 1);

        Assert.Equal((decimal)stunden, ergebnis.Menge);
        Assert.Equal(Entschaedigungseinheit.Stunde, ergebnis.Einheit);
        Assert.Equal((decimal)betrag, ergebnis.Betrag);
    }

    [Fact]
    public void Tagessatz_zaehlt_Kalendertage_und_kennt_den_halben_Tag()
    {
        var regel = new Entschaedigungsregel(0, 60, SatzTag: 42m, SatzHalberTag: 21m, HalberTagUnterStunden: 5m);

        Assert.Equal(new Entschaedigungsbetrag(1, Entschaedigungseinheit.HalberTag, 21m, 21m), regel.Berechnen(TimeSpan.FromHours(4.5), 1));
        Assert.Equal(new Entschaedigungsbetrag(1, Entschaedigungseinheit.Tag, 42m, 42m), regel.Berechnen(TimeSpan.FromHours(8), 1));
        Assert.Equal(new Entschaedigungsbetrag(3, Entschaedigungseinheit.Tag, 42m, 126m), regel.Berechnen(TimeSpan.FromHours(4), 3));
    }

    [Fact]
    public void Ohne_Dauer_gibt_es_nichts()
    {
        Assert.Equal(0m, new Entschaedigungsregel(15m, 60).Berechnen(TimeSpan.Zero, 1).Betrag);
        Assert.False(new Entschaedigungsregel(0, 60).Verguetet);
    }

    [Fact]
    public void Saetze_gelten_ab_Datum_und_akzeptieren_Komma()
    {
        Parameter P(string schluessel, string wert, DateOnly ab, DateOnly? bis = null) =>
            new() { Schluessel = schluessel, Wert = wert, GueltigAb = ab, GueltigBis = bis };

        var saetze = new Entschaedigungssaetze(
        [
            P("Entschaedigung.Einsatz.SatzStunde", "15", new(2020, 1, 1), new(2025, 12, 31)),
            P("Entschaedigung.Einsatz.SatzStunde", "16,50", new(2026, 1, 1)),
            P("Entschaedigung.Einsatz.RundungMinuten", "30", new(2020, 1, 1)),
            P("Entschaedigung.Pauschale.KDT", "700", new(2020, 1, 1)),
            P("Mitglied.Probezeit.Monate", "12", new(2020, 1, 1)),
        ]);

        Assert.Equal(15m, saetze.Regel("Einsatz", new(2025, 12, 31))!.SatzStunde);
        Assert.Equal(16.5m, saetze.Regel("Einsatz", new(2026, 1, 1))!.SatzStunde);
        Assert.Equal(30, saetze.Regel("Einsatz", new(2026, 1, 1))!.RundungMinuten);
        Assert.Null(saetze.Regel("Einsatz", new(2019, 12, 31)));
        Assert.Null(saetze.Regel(Entschaedigungssaetze.Vorgang(DienstArt.Uebung), new(2026, 1, 1)));
        Assert.Equal(700m, saetze.Pauschale("KDT", new(2026, 5, 1)));
        Assert.Equal(["KDT"], saetze.PauschaleFunktionen);
    }

    [Fact]
    public void Pauschale_ist_tagesgenau_anteilig()
    {
        // 2026 hat 365 Tage: 365 € je Jahr sind 1 € je Tag.
        var anteile = Funktionspauschale.Berechnen(
            new(2026, 1, 1), new(2026, 12, 31), new(2026, 3, 1), new(2026, 3, 31), _ => false, _ => 365m);

        var anteil = Assert.Single(anteile);
        Assert.Equal(new DateOnly(2026, 3, 1), anteil.Von);
        Assert.Equal(new DateOnly(2026, 3, 31), anteil.Bis);
        Assert.Equal(31, anteil.Tage);
        Assert.Equal(31m, anteil.Betrag);
    }

    [Fact]
    public void Pauschale_ganzes_Jahr_ergibt_Jahressatz_auch_im_Schaltjahr()
    {
        var anteil = Assert.Single(Funktionspauschale.Berechnen(new(2028, 1, 1), new(2028, 12, 31), new(2015, 1, 1), null, _ => false, _ => 700m));

        Assert.Equal(366, anteil.Tage);
        Assert.Equal(700m, anteil.Betrag);
    }

    [Fact]
    public void Pauschale_ueberspringt_abgerechnete_Tage_und_trennt_bei_Satzwechsel()
    {
        var anteile = Funktionspauschale.Berechnen(
            new(2026, 1, 1), new(2026, 12, 31), new(2020, 1, 1), null,
            tag => tag.Month <= 3,
            tag => tag.Month >= 7 ? 730m : 365m);

        Assert.Equal(2, anteile.Count);
        Assert.Equal((new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30), 91, 91m), (anteile[0].Von, anteile[0].Bis, anteile[0].Tage, anteile[0].Betrag));
        Assert.Equal((new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31), 184, 368m), (anteile[1].Von, anteile[1].Bis, anteile[1].Tage, anteile[1].Betrag));
    }

    [Fact]
    public void Abrechnung_laesst_sich_nur_mit_Posten_und_einmal_abschliessen()
    {
        var abrechnung = new Abrechnung { Bezeichnung = "2026", Von = new(2026, 1, 1), Bis = new(2026, 12, 31) };
        Assert.Throws<InvalidOperationException>(() => abrechnung.Abschliessen(Guid.CreateVersion7(), DateTimeOffset.UtcNow));

        abrechnung.Posten.Add(new AbrechnungsPosten { Vorgang = "Einsatz", Bezeichnung = "Einsatz 2026-001", Betrag = 15m });
        abrechnung.Abschliessen(Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        Assert.True(abrechnung.IstAbgeschlossen);
        Assert.Equal(15m, abrechnung.Summe);
        Assert.Throws<InvalidOperationException>(() => abrechnung.Abschliessen(Guid.CreateVersion7(), DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("2026-01-01", "2026-12-31", 0)]
    [InlineData("2026-02-01", "2026-01-31", 1)]
    [InlineData("2026-01-01", "2027-06-30", 1)]
    public void Zeitraum_hoechstens_ein_Jahr_und_nicht_rueckwaerts(string von, string bis, int fehler)
    {
        Assert.Equal(fehler, Abrechnung.ZeitraumPruefen(DateOnly.Parse(von), DateOnly.Parse(bis)).Count);
    }

    [Fact]
    public void Kalendertage_in_Ortszeit()
    {
        // 22:30–01:30 Ortszeit (Sommerzeit) berührt zwei Tage, Ende um 00:00 Ortszeit nur einen.
        Assert.Equal(2, Ortszeit.Kalendertage(new(2026, 7, 1, 20, 30, 0, TimeSpan.Zero), new(2026, 7, 1, 23, 30, 0, TimeSpan.Zero)));
        Assert.Equal(1, Ortszeit.Kalendertage(new(2026, 7, 1, 18, 0, 0, TimeSpan.Zero), new(2026, 7, 1, 22, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 7, 2), Ortszeit.Datum(new(2026, 7, 1, 22, 30, 0, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData(150, 3, 10.0)]   // 2,5 h → 3 h: nur die erste Stufe
    [InlineData(180, 3, 10.0)]
    [InlineData(181, 4, 15.0)]   // angefangene Stunde → 4 h: ein weiterer Block
    [InlineData(360, 6, 15.0)]
    [InlineData(480, 8, 20.0)]   // Lehrgangstag 8 h: 10 + 2 × 5
    public void Lehrgang_nach_Staffel_der_FwES_Adelberg(int minuten, double stunden, double betrag)
    {
        var regel = new Entschaedigungsregel(0, 60, Staffel: new Entschaedigungsstaffel(3, 10, 3, 5));

        var ergebnis = regel.Berechnen(TimeSpan.FromMinutes(minuten), 1);

        Assert.True(regel.Verguetet);
        Assert.Equal(Entschaedigungseinheit.Staffel, ergebnis.Einheit);
        Assert.Equal((decimal)stunden, ergebnis.Menge);
        Assert.Equal((decimal)betrag, ergebnis.Betrag);
    }

    [Fact]
    public void Einsatz_sechs_Euro_je_angefangene_halbe_Stunde()
    {
        var regel = new Entschaedigungsregel(12m, 30);

        Assert.Equal(18m, regel.Berechnen(TimeSpan.FromMinutes(61), 1).Betrag);
        Assert.Equal(6m, regel.Berechnen(TimeSpan.FromMinutes(5), 1).Betrag);
    }

    [Fact]
    public void Staffel_und_Leitung_werden_aus_den_Parametern_gelesen()
    {
        Parameter P(string schluessel, string wert) => new() { Schluessel = schluessel, Wert = wert, GueltigAb = new(2019, 1, 1) };
        var saetze = new Entschaedigungssaetze(
        [
            P("Entschaedigung.Lehrgang.StaffelErsteStunden", "3"),
            P("Entschaedigung.Lehrgang.StaffelErsterBetrag", "10"),
            P("Entschaedigung.Lehrgang.StaffelWeitereStunden", "3"),
            P("Entschaedigung.Lehrgang.StaffelWeitererBetrag", "5"),
            P("Entschaedigung.Uebung.SatzLeitung", "5"),
            P(Entschaedigungssaetze.PauschaleZwoelftel, "1"),
        ]);

        var lehrgang = saetze.Regel("Lehrgang", new(2026, 3, 1))!;
        Assert.Equal(new Entschaedigungsstaffel(3, 10, 3, 5), lehrgang.Staffel);
        Assert.Null(saetze.Regel("Uebung", new(2026, 3, 1)));
        Assert.Equal(5m, saetze.Leitung("Uebung", new(2026, 3, 1)));
        Assert.Equal(1m, saetze.Zahl(Entschaedigungssaetze.PauschaleZwoelftel, new(2026, 3, 1)));
        Assert.Empty(saetze.PauschaleFunktionen);
    }

    [Fact]
    public void Zwoelftel_je_angefangenen_Monat_ohne_doppelte_Monate()
    {
        // Kommandant ab 16.03.2026, Halbjahresabrechnung: März bis Juni = 4 Zwölftel von 500 €.
        var anteile = Funktionspauschale.BerechnenZwoelftel(
            new(2026, 1, 1), new(2026, 6, 30), new(2026, 3, 16), null, _ => false, _ => 500m);

        var anteil = Assert.Single(anteile);
        Assert.Equal(new DateOnly(2026, 3, 1), anteil.Von);
        Assert.Equal(new DateOnly(2026, 6, 30), anteil.Bis);
        Assert.Equal(4, anteil.Tage);
        Assert.Equal(166.67m, anteil.Betrag);

        // Ist der März schon abgerechnet, entfällt er ganz.
        var ohneMaerz = Funktionspauschale.BerechnenZwoelftel(
            new(2026, 1, 1), new(2026, 6, 30), new(2026, 3, 16), null, tag => tag.Month == 3, _ => 500m);
        Assert.Equal(3, Assert.Single(ohneMaerz).Tage);
    }

    [Fact]
    public void Zwoelftel_trennt_bei_Satzwechsel_und_Funktionsende()
    {
        var anteile = Funktionspauschale.BerechnenZwoelftel(
            new(2026, 1, 1), new(2026, 12, 31), new(2020, 1, 1), new(2026, 8, 10), _ => false,
            tag => tag.Month < 4 ? 250m : 300m);

        Assert.Equal(2, anteile.Count);
        Assert.Equal((3, 62.5m), (anteile[0].Tage, anteile[0].Betrag));
        Assert.Equal((5, 125m), (anteile[1].Tage, anteile[1].Betrag));
    }
}
