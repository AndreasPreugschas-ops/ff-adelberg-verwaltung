using Fevad.Domain.Technik;

namespace Fevad.Domain.Tests;

public class BeladeplanTests
{
    private static readonly DateOnly Heute = new(2026, 10, 26);
    private static readonly Guid Schere = Guid.NewGuid();
    private static readonly Guid Schlauch = Guid.NewGuid();
    private static readonly Guid SchlauchArt = Guid.NewGuid();
    private static readonly Guid HydraulikArt = Guid.NewGuid();

    private static Beladeabgleich.Geraet Geraet(string nummer, Guid material, Guid art, string fach) =>
        new(Guid.NewGuid(), nummer, nummer, material, art, fach);

    [Fact]
    public void SollIst_zaehlt_Geraete_je_Fach_und_Material()
    {
        var positionen = new List<Beladeposition>
        {
            new() { Fach = "G3", Bezeichnung = "Rettungsschere", MaterialId = Schere, Menge = 1 },
            new() { Fach = "G2", Bezeichnung = "Druckschlauch C", GeraeteartId = SchlauchArt, Menge = 4 },
            new() { Fach = "G6", Bezeichnung = "Ölbindemittel", Menge = 2 },
        };
        var geraete = new List<Beladeabgleich.Geraet>
        {
            Geraet("S1", Schere, HydraulikArt, "g3 "),
            Geraet("C1", Schlauch, SchlauchArt, "G2"),
            Geraet("C2", Schlauch, SchlauchArt, "G2"),
            Geraet("C3", Schlauch, SchlauchArt, "G4"),
        };

        var zeilen = Beladeabgleich.SollIst(positionen, geraete);

        Assert.Equal(1, zeilen.Single(z => z.Bezeichnung == "Rettungsschere").Ist);
        Assert.Equal(2, zeilen.Single(z => z.Bezeichnung == "Druckschlauch C").Fehlt);
        Assert.True(zeilen.Single(z => z.Bezeichnung == "Ölbindemittel").Stimmt);
        var ohneSoll = Assert.Single(zeilen, z => z.PositionId is null);
        Assert.Equal("G4", ohneSoll.Fach);
    }

    [Fact]
    public void Kontrolle_meldet_nicht_gefundene_Geraete_und_zu_wenig_gezaehlte_Mengen()
    {
        var menge = new Beladeposition { Fach = "G6", Bezeichnung = "Ölbindemittel", Menge = 2 };
        var positionen = new List<Beladeposition>
        {
            new() { Fach = "G2", Bezeichnung = "Druckschlauch C", MaterialId = Schlauch, Menge = 2 },
            menge,
        };
        var c1 = Geraet("C1", Schlauch, SchlauchArt, "G2");
        var c2 = Geraet("C2", Schlauch, SchlauchArt, "G2");

        var zeilen = Beladeabgleich.Kontrolle(positionen, [c1, c2], new HashSet<Guid> { c1.Id }, new Dictionary<Guid, int> { [menge.Id] = 1 });

        Assert.Equal(2, Beladeabgleich.FehlendGesamt(zeilen));
        Assert.Equal(["C2"], zeilen.Single(z => z.Fach == "G2").Fehlende);
        var liste = Beladeabgleich.Fehlliste(zeilen);
        Assert.Contains("G2: Druckschlauch C, fehlt 1 von 2; nicht gefunden: Nr. C2", liste);
        Assert.Contains("G6: Ölbindemittel, fehlt 1 von 2", liste);
    }

    [Fact]
    public void Kontrolle_vollstaendig_auch_mit_Ueberschuss()
    {
        var positionen = new List<Beladeposition> { new() { Fach = "G2", Bezeichnung = "Druckschlauch C", MaterialId = Schlauch, Menge = 1 } };
        var c1 = Geraet("C1", Schlauch, SchlauchArt, "G2");
        var c2 = Geraet("C2", Schlauch, SchlauchArt, "G2");

        var zeilen = Beladeabgleich.Kontrolle(positionen, [c1, c2], new HashSet<Guid> { c1.Id, c2.Id }, new Dictionary<Guid, int>());

        Assert.Equal(0, Beladeabgleich.FehlendGesamt(zeilen));
        Assert.Equal(1, Assert.Single(zeilen).Zuviel);
        Assert.Equal("", Beladeabgleich.Fehlliste(zeilen));
    }

    [Fact]
    public void Ergebnis_der_Kontrolle_uebersteht_das_Speichern()
    {
        var zeile = new Beladeabgleich.Zeile(Guid.NewGuid(), "G1", "Leine", 2, 1, ["L1"]);
        var kontrolle = new Beladekontrolle { Ergebnis = System.Text.Json.JsonSerializer.Serialize(new[] { zeile }, Beladeabgleich.Json) };

        var gelesen = Assert.Single(kontrolle.Zeilen());

        Assert.Equal(zeile.PositionId, gelesen.PositionId);
        Assert.Equal(["L1"], gelesen.Fehlende);
        Assert.Equal(1, gelesen.Fehlt);
    }

    [Fact]
    public void Fahrberechtigung_reicht_nur_bis_zur_Gesamtmasse()
    {
        var klasseB = new Fahrberechtigung.Erlaubnis(3500, new(2010, 5, 1), null);

        Assert.True(Fahrberechtigung.Pruefen(3500, [klasseB], null, Heute).Ausreichend);
        Assert.False(Fahrberechtigung.Pruefen(7490, [klasseB], null, Heute).Ausreichend);
        Assert.Null(Fahrberechtigung.Pruefen(3500, [klasseB], null, Heute).Bis);
    }

    [Fact]
    public void Fahrberechtigung_endet_mit_Befristung_oder_Fuehrerscheinkontrolle()
    {
        var klasseC = new Fahrberechtigung.Erlaubnis(null, new(2015, 1, 1), new(2028, 3, 31));

        Assert.Equal(new DateOnly(2028, 3, 31), Fahrberechtigung.Pruefen(16000, [klasseC], null, Heute).Bis);
        Assert.Equal(new DateOnly(2027, 2, 1), Fahrberechtigung.Pruefen(16000, [klasseC], new DateOnly(2027, 2, 1), Heute).Bis);
        Assert.False(Fahrberechtigung.Pruefen(16000, [klasseC], DateOnly.MinValue, Heute).Ausreichend);
        Assert.False(Fahrberechtigung.Pruefen(16000, [klasseC with { GueltigBis = new(2026, 1, 1) }], null, Heute).Ausreichend);
    }

    [Fact]
    public void Zweckbindung_warnt_bei_Ausserdienststellung_ohne_Mitteilung()
    {
        var hlf = new Fahrzeug { Funkrufname = "Florian Adelberg 43", Typ = "HLF 10", Foerdermittel = 150000m, ZweckbindungBis = new(2043, 11, 30) };

        Assert.True(hlf.ZweckbindungWarnung(new DateOnly(2040, 1, 1)));
        Assert.False(hlf.ZweckbindungWarnung(new DateOnly(2044, 1, 1)));
        Assert.False(hlf.ZweckbindungWarnung(null));
        hlf.FoerdergeberMitgeteiltAm = new(2039, 12, 1);
        Assert.False(hlf.ZweckbindungWarnung(new DateOnly(2040, 1, 1)));
    }
}
