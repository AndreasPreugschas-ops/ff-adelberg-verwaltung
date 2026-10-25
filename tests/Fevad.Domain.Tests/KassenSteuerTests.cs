using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kassen;

namespace Fevad.Domain.Tests;

/// <summary>Steuerbereiche, Steuerampel, Veranstaltungsabrechnung und Barkassenbericht (P2: V03).</summary>
public class KassenSteuerTests
{
    private static Buchung Buchung(BuchungArt art, decimal betrag) => new()
    {
        Art = art, Betrag = betrag, KontoId = Guid.NewGuid(), Datum = new(2026, 6, 20), Text = "Hocketse",
        Steuerbereich = Steuerbereich.Wirtschaftlich, Steuersatz = Umsatzsteuersatz.Regel, VeranstaltungId = Guid.NewGuid(),
    };

    [Theory]
    [InlineData(119, 19, 100)]
    [InlineData(107, 7, 100)]
    [InlineData(10, 19, 8.40)]
    [InlineData(250, 0, 250)]
    public void Netto_rechnet_die_Umsatzsteuer_heraus(decimal brutto, decimal satz, decimal netto) =>
        Assert.Equal(netto, Steuerregeln.Netto(brutto, satz));

    [Fact]
    public void Paragraph_2b_gilt_erst_ab_dem_Stichtag()
    {
        var ab = new DateOnly(2027, 1, 1);
        Assert.False(Steuerregeln.Paragraph2bGilt(new(2026, 12, 31), ab));
        Assert.True(Steuerregeln.Paragraph2bGilt(ab, ab));
        Assert.False(Steuerregeln.Paragraph2bGilt(new(2030, 1, 1), null));
    }

    [Theory]
    [InlineData(35_999, Fristampel.Gruen)]
    [InlineData(36_000, Fristampel.Gelb)]
    [InlineData(45_000, Fristampel.Gelb)]
    [InlineData(45_000.01, Fristampel.Rot)]
    public void Steuerampel_wird_ab_80_Prozent_gelb_und_ueber_der_Grenze_rot(decimal umsatz, Fristampel erwartet) =>
        Assert.Equal(erwartet, Steuerregeln.Ampel(umsatz, 45_000, 80));

    [Fact]
    public void Ohne_Grenze_bleibt_die_Ampel_gruen() => Assert.Equal(Fristampel.Gruen, Steuerregeln.Ampel(1_000_000m, 0, 80));

    [Fact]
    public void Vorschlag_kommt_aus_der_Position_und_Umbuchungen_sind_intern()
    {
        var fest = new Planposition { Nummer = "2", Bezeichnung = "Erträge aus Veranstaltungen", Steuerbereich = Steuerbereich.Wirtschaftlich };
        Assert.Equal(Steuerbereich.Wirtschaftlich, Steuerregeln.Vorschlag(BuchungArt.Einnahme, fest));
        Assert.Equal(Steuerbereich.Intern, Steuerregeln.Vorschlag(BuchungArt.Umbuchung, fest));
        Assert.Equal(Steuerbereich.Intern, Steuerregeln.Vorschlag(BuchungArt.Ausgabe, null));
    }

    [Fact]
    public void Storno_uebernimmt_Steuerangaben_und_Veranstaltung()
    {
        var original = Buchung(BuchungArt.Einnahme, 500m);
        var storno = Kassenregeln.Storno(original, new(2026, 6, 21), "doppelt");
        Assert.Equal(original.Steuerbereich, storno.Steuerbereich);
        Assert.Equal(original.Steuersatz, storno.Steuersatz);
        Assert.Equal(original.VeranstaltungId, storno.VeranstaltungId);
    }

    [Fact]
    public void Abrechnung_zieht_Stornos_ab_und_zeigt_den_Anteil_der_Feuerwehr()
    {
        var einnahme = Buchung(BuchungArt.Einnahme, 3200m);
        var falsch = Buchung(BuchungArt.Einnahme, 100m);
        var buchungen = new[]
        {
            einnahme,
            falsch,
            Kassenregeln.Storno(falsch, new(2026, 6, 22), "falsche Veranstaltung"),
            Buchung(BuchungArt.Ausgabe, 1200m),
        };

        var abrechnung = Veranstaltungsabrechnung.Berechnen(buchungen, 60m);
        Assert.Equal(3200m, abrechnung.Einnahmen);
        Assert.Equal(1200m, abrechnung.Ausgaben);
        Assert.Equal(2000m, abrechnung.Ergebnis);
        Assert.Equal(1200m, abrechnung.Anteil);
    }

    [Fact]
    public void Barkassenbericht_zaehlt_Bestand_und_zieht_das_Wechselgeld_ab()
    {
        var bericht = new Barkassenbericht
        {
            Stelle = "Getränke", GezaehltVon = "A", ZweitePerson = "B", Wechselgeld = 150m,
            Zaehlung =
            [
                new() { Wert = 50m, Anzahl = 12 },
                new() { Wert = 20m, Anzahl = 8 },
                new() { Wert = 2m, Anzahl = 15 },
                new() { Wert = 0.50m, Anzahl = 7 },
            ],
        };

        Assert.Equal(600m + 160m + 30m + 3.50m, bericht.Endbestand);
        Assert.Equal(793.50m - 150m, bericht.Einnahme);
        Assert.Equal(15, Zaehlposition.Stueckelung.Count);
    }
}
