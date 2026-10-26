using Fevad.Domain.Technik;

namespace Fevad.Domain.Tests;

public class MangelWorkflowTests
{
    private static Geraet Neu(string nummer, Guid? fahrzeugId, string? lagerort) =>
        new() { Inventarnummer = nummer, Bezeichnung = "Motorsäge", FahrzeugId = fahrzeugId, Lagerort = lagerort };

    [Fact]
    public void Ersatz_kommt_ins_Fach_und_beim_Wiederverladen_tauscht_alles_zurueck()
    {
        var hlf = Guid.NewGuid();
        var defekt = Neu("MS-1", hlf, "G3");
        var ersatz = Neu("MS-9", null, "Magazin");
        var mangel = new Mangel { GeraetId = defekt.Id, Beschreibung = "Kettenbremse", Schwere = MangelSchwere.AusserBetrieb };

        mangel.ErsatzEinsetzen(defekt, ersatz, "Werkstatt");

        Assert.True(mangel.ErsatzEingesetzt);
        Assert.Equal((hlf, "G3"), (ersatz.FahrzeugId!.Value, ersatz.Lagerort));
        Assert.Equal(((Guid?)null, "Werkstatt"), (defekt.FahrzeugId, defekt.Lagerort));
        Assert.Throws<InvalidOperationException>(() => mangel.ErsatzEinsetzen(defekt, Neu("MS-8", null, null), "Werkstatt"));

        mangel.WiederVerladen(defekt, ersatz, new DateOnly(2026, 10, 26));

        Assert.False(mangel.ErsatzEingesetzt);
        Assert.Equal((hlf, "G3"), (defekt.FahrzeugId!.Value, defekt.Lagerort));
        Assert.Equal(((Guid?)null, "Magazin"), (ersatz.FahrzeugId, ersatz.Lagerort));
        Assert.Equal(new DateOnly(2026, 10, 26), mangel.WiederVerladenAm);
    }

    [Fact]
    public void Ersatz_nur_fuer_das_Geraet_des_Mangels()
    {
        var defekt = Neu("MS-1", Guid.NewGuid(), "G3");
        var mangel = new Mangel { GeraetId = Guid.NewGuid(), Beschreibung = "x" };

        Assert.Throws<InvalidOperationException>(() => mangel.ErsatzEinsetzen(defekt, Neu("MS-9", null, null), "Werkstatt"));
        Assert.Throws<InvalidOperationException>(() => mangel.WiederVerladen(defekt, null, new DateOnly(2026, 10, 26)));
    }

    [Theory]
    [InlineData(true, MangelSchwere.AusserBetrieb, MangelStatus.Offen, true)]
    [InlineData(true, MangelSchwere.Einschraenkung, MangelStatus.Offen, false)]
    [InlineData(true, MangelSchwere.AusserBetrieb, MangelStatus.Behoben, false)]
    [InlineData(false, MangelSchwere.AusserBetrieb, MangelStatus.Offen, false)]
    public void Einsatzrelevant_ist_nur_ein_offener_Fahrzeugausfall(bool fahrzeug, MangelSchwere schwere, MangelStatus status, bool erwartet)
    {
        var mangel = new Mangel
        {
            FahrzeugId = fahrzeug ? Guid.NewGuid() : null,
            GeraetId = fahrzeug ? null : Guid.NewGuid(),
            Beschreibung = "x",
            Schwere = schwere,
            Status = status,
        };

        Assert.Equal(erwartet, mangel.Einsatzrelevant);
    }
}

public class FahrtenbuchTests
{
    [Fact]
    public void Strecke_aus_den_Staenden_sonst_eingetragene_Kilometer()
    {
        Assert.Equal(37, new Fahrt { KmBeginn = 41_200, KmEnde = 41_237, Kilometer = 99 }.Strecke);
        Assert.Equal(12, new Fahrt { KmBeginn = 41_200, Kilometer = 12 }.Strecke);
        Assert.Null(new Fahrt().Strecke);
    }

    [Fact]
    public void Jahresverbrauch_ohne_AdBlue_je_100_km()
    {
        var werte = Fahrzeugverbrauch.JeJahr(
            [(2025, 400), (2025, 600), (2026, 50)],
            [
                new Betankung { Datum = new(2025, 5, 1), Kraftstoff = Kraftstoff.Diesel, Liter = 300, Betrag = 500 },
                new Betankung { Datum = new(2025, 9, 1), Kraftstoff = Kraftstoff.AdBlue, Liter = 10, Betrag = 10 },
            ]);

        Assert.Equal([2026, 2025], werte.Select(w => w.Jahr));
        var jahr = werte[1];
        Assert.Equal((2, 1000, 300m, 10m, 510m, 30.0m), (jahr.Fahrten, jahr.Kilometer, jahr.Liter, jahr.AdBlueLiter, jahr.Betrag, jahr.LiterJe100Km!.Value));
        Assert.Null(werte[0].LiterJe100Km);
    }
}

/// <summary>Pflegestand und Schlauchtyp (V63).</summary>
public class PflegeTests
{
    [Theory]
    [InlineData("B 75-20", "B", 75, 20)]
    [InlineData("C 42-15", "C", 42, 15)]
    [InlineData("C 52-20", "C 52", 52, 20)]
    [InlineData("a110-5m", "A", 110, 5)]
    [InlineData("D 25/15", "D", 25, 15)]
    public void Schlauchtyp_liest_Groesse_und_Laenge(string typ, string groesse, int durchmesser, int laenge) =>
        Assert.Equal(new Schlauchtyp(groesse, durchmesser, laenge), Schlauchtyp.Lesen(typ));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Druckschlauch")]
    [InlineData("E 75-20")]
    public void Schlauchtyp_ohne_Muster_ist_leer(string? typ) => Assert.Null(Schlauchtyp.Lesen(typ));

    [Fact]
    public void Pflegestand_zaehlt_nur_Waeschen_und_warnt_vor_der_Grenze()
    {
        var eintraege = Enumerable.Range(0, 23).Select(_ => new Pflegeeintrag { Art = Pflegeart.Waesche })
            .Append(new Pflegeeintrag { Art = Pflegeart.Impraegnierung }).ToList();
        var stand = Pflegestand.Aus(eintraege, 25);
        Assert.Equal(23, stand.Waeschen);
        Assert.True(stand.GrenzeNah);
        Assert.False(stand.GrenzeErreicht);
        Assert.True(new Pflegestand(25, 25).GrenzeErreicht);
        Assert.False(new Pflegestand(80, null).GrenzeErreicht);
    }
}
