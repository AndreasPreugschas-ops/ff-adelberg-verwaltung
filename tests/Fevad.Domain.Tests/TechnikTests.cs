using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Tests;

public class TechnikTests
{
    private static readonly DateOnly Heute = new(2026, 10, 22);

    private static PruefplanPosition Position(Pruefart art, int monate) => new() { Pruefart = art, IntervallMonate = monate };

    [Fact]
    public void Checkliste_aus_Zeilen_erkennt_Messwert_und_Freitext()
    {
        var punkte = Pruefpunkt.AusZeilen("Holme ohne Risse\r\nPrüfdruck [bar]\n\nBemerkung [Text]\n");

        Assert.Equal(
            [new Pruefpunkt("Holme ohne Risse"), new Pruefpunkt("Prüfdruck", PruefpunktTyp.Messwert, "bar"), new Pruefpunkt("Bemerkung", PruefpunktTyp.Text)],
            punkte);
        Assert.Equal("Holme ohne Risse\nPrüfdruck [bar]\nBemerkung [Text]", Pruefpunkt.AlsZeilen(punkte));
        Assert.Equal(punkte, Pruefpunkt.AusJson(Pruefpunkt.AlsJson(punkte)));
    }

    [Fact]
    public void Ohne_Pruefung_sofort_faellig()
    {
        var f = Assert.Single(Pruefplanung.Berechnen([Position(Pruefart.Funktionspruefung, 12)], [], Heute, 30));

        Assert.True(f.NieGeprueft);
        Assert.Equal(Fristampel.Rot, f.Ampel);
    }

    [Fact]
    public void Juengste_bestandene_Pruefung_bestimmt_die_Frist()
    {
        var plan = new[] { Position(Pruefart.Funktionspruefung, 12), Position(Pruefart.Belastungspruefung, 24), Position(Pruefart.NachGebrauch, 0) };
        var pruefungen = new[]
        {
            new PruefungKurz(Pruefart.Funktionspruefung, new(2025, 1, 10), PruefErgebnis.Bestanden, null),
            new PruefungKurz(Pruefart.Funktionspruefung, new(2025, 11, 5), PruefErgebnis.BestandenMitMaengeln, null),
            // Nicht bestanden setzt die Frist nicht zurück.
            new PruefungKurz(Pruefart.Funktionspruefung, new(2026, 2, 1), PruefErgebnis.NichtBestanden, null),
            new PruefungKurz(Pruefart.Belastungspruefung, new(2024, 9, 1), PruefErgebnis.Bestanden, new(2026, 9, 1)),
        };

        var faelligkeiten = Pruefplanung.Berechnen(plan, pruefungen, Heute, 30);

        Assert.Equal(2, faelligkeiten.Count);
        var funktion = faelligkeiten.Single(f => f.Pruefart == Pruefart.Funktionspruefung);
        Assert.Equal(new DateOnly(2026, 11, 5), funktion.FaelligAm);
        Assert.Equal(Fristampel.Gelb, funktion.Ampel);
        var belastung = faelligkeiten.Single(f => f.Pruefart == Pruefart.Belastungspruefung);
        Assert.Equal(new DateOnly(2026, 9, 1), belastung.FaelligAm);
        Assert.Equal(Fristampel.Rot, belastung.Ampel);
        Assert.Equal(Fristampel.Rot, Pruefplanung.Ampel(faelligkeiten));
        Assert.Equal(new DateOnly(2026, 9, 1), Pruefplanung.NaechsterTermin(faelligkeiten));
    }

    [Fact]
    public void Korrektur_am_selben_Pruefdatum_gilt()
    {
        var erfasst = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);
        var pruefungen = new[]
        {
            new PruefungKurz(Pruefart.Belastungspruefung, new(2025, 3, 1), PruefErgebnis.Bestanden, new(2026, 3, 1), erfasst.AddHours(2)),
            new PruefungKurz(Pruefart.Belastungspruefung, new(2025, 3, 1), PruefErgebnis.Bestanden, new(2027, 3, 1), erfasst.AddHours(3)),
            new PruefungKurz(Pruefart.Belastungspruefung, new(2025, 3, 1), PruefErgebnis.Bestanden, new(2025, 9, 1), erfasst),
        };

        var f = Assert.Single(Pruefplanung.Berechnen([Position(Pruefart.Belastungspruefung, 12)], pruefungen, Heute, 30));

        Assert.Equal(new DateOnly(2027, 3, 1), f.FaelligAm);
    }

    [Fact]
    public void Aussonderung_ab_Herstelldatum_sonst_Baujahr_sonst_Indienststellung()
    {
        Assert.Equal(new DateOnly(2030, 3, 15), new Geraet { Inventarnummer = "1", Bezeichnung = "Gurt", Herstelldatum = new(2020, 3, 15), Baujahr = 2019 }.AussonderungFaellig(10));
        Assert.Equal(new DateOnly(2029, 1, 1), new Geraet { Inventarnummer = "1", Bezeichnung = "Gurt", Baujahr = 2019 }.AussonderungFaellig(10));
        Assert.Equal(new DateOnly(2031, 6, 1), new Geraet { Inventarnummer = "1", Bezeichnung = "Gurt", IndienstAm = new(2021, 6, 1) }.AussonderungFaellig(10));
        Assert.Null(new Geraet { Inventarnummer = "1", Bezeichnung = "Leiter", Baujahr = 2019 }.AussonderungFaellig(null));
    }

    [Fact]
    public void Zustand_aus_schwerstem_offenem_Mangel()
    {
        Assert.Equal(GeraetZustand.Einsatzbereit, Geraet.ZustandBerechnen(null, []));
        Assert.Equal(GeraetZustand.Einsatzbereit, Geraet.ZustandBerechnen(null, [MangelSchwere.Info]));
        Assert.Equal(GeraetZustand.Eingeschraenkt, Geraet.ZustandBerechnen(null, [MangelSchwere.Info, MangelSchwere.Einschraenkung]));
        Assert.Equal(GeraetZustand.AusserBetrieb, Geraet.ZustandBerechnen(null, [MangelSchwere.AusserBetrieb, MangelSchwere.Einschraenkung]));
        Assert.Equal(GeraetZustand.Ausgesondert, Geraet.ZustandBerechnen(Heute, [MangelSchwere.AusserBetrieb]));
    }

    [Fact]
    public void Beschaffung_geht_nur_schrittweise_weiter()
    {
        Assert.Contains(BeschaffungStatus.Genehmigt, Beschaffung.Folgestatus(BeschaffungStatus.Bedarf));
        Assert.DoesNotContain(BeschaffungStatus.Bestellt, Beschaffung.Folgestatus(BeschaffungStatus.Bedarf));
        Assert.Equal([BeschaffungStatus.Geliefert], Beschaffung.Folgestatus(BeschaffungStatus.Bestellt));
        Assert.Empty(Beschaffung.Folgestatus(BeschaffungStatus.Geliefert));
    }
}
