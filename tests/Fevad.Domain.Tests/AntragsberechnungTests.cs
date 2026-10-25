using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Tests;

public class AntragsberechnungTests
{
    private static Parameter P(string schluessel, string wert) => new() { Schluessel = schluessel, Wert = wert, GueltigAb = new(2019, 1, 1) };

    private static readonly Entschaedigungssaetze Saetze = new(
    [
        P("Entschaedigung.Lehrgang.StaffelErsteStunden", "3"),
        P("Entschaedigung.Lehrgang.StaffelErsterBetrag", "10"),
        P("Entschaedigung.Lehrgang.StaffelWeitereStunden", "3"),
        P("Entschaedigung.Lehrgang.StaffelWeitererBetrag", "5"),
        P("Entschaedigung.Lehrgang.RundungMinuten", "60"),
        P(Antragsberechnung.ZuschlagSchluessel, "7"),
        P(Antragsberechnung.HaushaltSatzSchluessel, "7.5"),
        P(Antragsberechnung.HaushaltMaxStundenSchluessel, "8"),
        P(Antragsberechnung.KilometerSchluessel, "0.35"),
        P(Entschaedigungssaetze.TatsaechlicheHoeheUeberTage, "2"),
    ]);

    private static AntragTag Tag(int tag, int vonStunde, int vonMinute, int bisStunde, int bisMinute) => new()
    {
        Von = Ortszeit.TagesbeginnUtc(new DateOnly(2026, 3, tag)).AddHours(vonStunde).AddMinutes(vonMinute),
        Bis = Ortszeit.TagesbeginnUtc(new DateOnly(2026, 3, tag)).AddHours(bisStunde).AddMinutes(bisMinute),
    };

    private static Entschaedigungsantrag Antrag(Erwerbslage lage, bool verdienstausfall, params AntragTag[] tage) => new()
    {
        Bezeichnung = "Truppführer",
        Anlass = AntragAnlass.Lehrgang,
        Erwerbslage = lage,
        Verdienstausfall = verdienstausfall,
        Tage = [.. tage],
    };

    [Fact]
    public void Lehrgang_zwei_Tage_nach_Staffel_mit_Aufschlag_fuer_Verdienstausfall_und_Kilometern()
    {
        // Mo/Di 08:00–16:30 = 8,5 h, aufgerundet 9 h: 10 € + 2 × 5 € = 20 € je Tag; Aufschlag 9 h × 7 € = 63 € je Tag.
        var antrag = Antrag(Erwerbslage.Arbeitnehmer, true, Tag(2, 8, 0, 16, 30), Tag(3, 8, 0, 16, 30));
        antrag.Fahrtart = Fahrtart.Pkw;
        antrag.Kilometer = 120;

        var ergebnis = Antragsberechnung.Berechnen(antrag, Saetze);

        Assert.Empty(ergebnis.Hinweise);
        Assert.Equal(40m, ergebnis.Posten.Where(p => p.Steuerart == Steuerart.Aufwandsentschaedigung).Sum(p => p.Betrag));
        Assert.Equal(126m, ergebnis.Posten.Where(p => p.Steuerart == Steuerart.Verdienstausfall).Sum(p => p.Betrag));
        Assert.Equal(42m, ergebnis.Posten.Single(p => p.Steuerart == Steuerart.Auslagenersatz).Betrag);
        Assert.Equal(208m, ergebnis.Summe);
    }

    [Fact]
    public void Ohne_Verdienstausfall_kein_Aufschlag_haushaltsfuehrend_immer()
    {
        var ohne = Antragsberechnung.Berechnen(Antrag(Erwerbslage.Arbeitnehmer, false, Tag(2, 9, 0, 12, 0)), Saetze);
        Assert.Equal(10m, ohne.Summe);

        var schueler = Antragsberechnung.Berechnen(Antrag(Erwerbslage.OhneVerdienst, true, Tag(2, 9, 0, 12, 0)), Saetze);
        Assert.Equal(10m, schueler.Summe);

        // FwES § 4: Zeitversäumnis gilt als Verdienstausfall.
        var haushalt = Antragsberechnung.Berechnen(Antrag(Erwerbslage.Haushaltsfuehrend, false, Tag(2, 9, 0, 12, 0)), Saetze);
        Assert.Equal(10m + 21m, haushalt.Summe);
    }

    [Fact]
    public void Mehr_als_zwei_Tage_in_tatsaechlicher_Hoehe()
    {
        var antrag = Antrag(Erwerbslage.Arbeitnehmer, true, Tag(2, 8, 0, 17, 0), Tag(3, 8, 0, 17, 0), Tag(4, 8, 0, 17, 0));
        antrag.VerdienstausfallBetrag = 480m;
        antrag.SozialversicherungBetrag = 95.20m;
        antrag.AuslagenBetrag = 30m;

        Assert.True(antrag.InTatsaechlicherHoehe(2));
        var ergebnis = Antragsberechnung.Berechnen(antrag, Saetze);

        Assert.DoesNotContain(ergebnis.Posten, p => p.Steuerart == Steuerart.Aufwandsentschaedigung);
        Assert.Equal(480m, ergebnis.Posten.Single(p => p.Steuerart == Steuerart.Verdienstausfall).Betrag);
        Assert.Equal(125.20m, ergebnis.Posten.Where(p => p.Steuerart == Steuerart.Auslagenersatz).Sum(p => p.Betrag));
    }

    [Fact]
    public void Haushaltsfuehrend_ueber_zwei_Tage_hoechstens_acht_Stunden_je_Werktag()
    {
        // Fr bis So (6.–8. März 2026), je 10 h: Sonntag zählt nicht, je Werktag höchstens 8 h → 16 h × 7,50 € = 120 €.
        var antrag = Antrag(Erwerbslage.Haushaltsfuehrend, false, Tag(6, 8, 0, 18, 0), Tag(7, 8, 0, 18, 0), Tag(8, 8, 0, 18, 0));

        var ergebnis = Antragsberechnung.Berechnen(antrag, Saetze);

        var posten = Assert.Single(ergebnis.Posten);
        Assert.Equal((16m, 120m, Steuerart.Verdienstausfall), (posten.Menge, posten.Betrag, posten.Steuerart));
    }

    [Fact]
    public void Pruefen_meldet_fehlende_Angaben_und_kurze_Einsaetze()
    {
        var antrag = Antrag(Erwerbslage.Arbeitnehmer, false, Tag(2, 10, 0, 9, 0));
        antrag.Fahrtart = Fahrtart.Pkw;
        antrag.Anlass = AntragAnlass.Einsatz;

        var fehler = antrag.Pruefen(2);

        Assert.Contains(fehler, f => f.Contains("Ende"));
        Assert.Contains(fehler, f => f.Contains("Kilometer"));
        Assert.Contains(fehler, f => f.Contains("Einsatzbericht"));
        Assert.Contains("Bitte mindestens einen Tag", Antrag(Erwerbslage.Arbeitnehmer, false).Pruefen(2)[0]);
    }
}
