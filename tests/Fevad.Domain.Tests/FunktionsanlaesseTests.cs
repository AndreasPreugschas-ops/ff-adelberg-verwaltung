using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class FunktionsanlaesseTests
{
    private static readonly DateOnly Stichtag = new(2026, 10, 23);

    private static LaufendeFunktion F(string? voraussetzungen, FunktionArt art = FunktionArt.Bestellung, string ab = "2024-03-15", string? bis = null, int? amtszeit = null) =>
        new(Guid.NewGuid(), "Kommandant/in", voraussetzungen, art, DateOnly.Parse(ab), bis is null ? null : DateOnly.Parse(bis), amtszeit);

    [Fact]
    public void Fehlende_Voraussetzung_nennt_die_fehlenden_Gruppen()
    {
        var anlass = Assert.Single(Funktionsanlaesse.Pruefen([F("ZF|VF; LDF")], new HashSet<string> { "ZF" }, Stichtag, 3,
            new Dictionary<string, string> { ["LDF"] = "Leiter einer Feuerwehr" }));
        Assert.Equal(PruefhinweisArt.Funktion, anlass.Art);
        Assert.Equal("Kommandant/in: Voraussetzung fehlt (Leiter einer Feuerwehr).", anlass.Text);
        Assert.Empty(Funktionsanlaesse.Pruefen([F("ZF|VF; LDF")], new HashSet<string> { "VF", "LDF" }, Stichtag, 3));
    }

    [Fact]
    public void Gewaehlte_Funktion_meldet_das_Ende_der_Amtszeit_im_Vorlauf()
    {
        // Gewählt am 03.12.2021 für 5 Jahre: endet am 02.12.2026, also innerhalb von 3 Monaten.
        var anlass = Assert.Single(Funktionsanlaesse.Pruefen([F(null, FunktionArt.Wahl, "2021-12-03", amtszeit: 5)], new HashSet<string>(), Stichtag, 3));
        Assert.Equal(new DateOnly(2026, 12, 2), anlass.FaelligAm);
        Assert.Contains("Wahl vorbereiten", anlass.Text);

        Assert.Empty(Funktionsanlaesse.Pruefen([F(null, FunktionArt.Wahl, "2024-03-15", amtszeit: 5)], new HashSet<string>(), Stichtag, 3));
    }

    [Fact]
    public void Beendete_und_kuenftige_Funktionen_zaehlen_nicht()
    {
        Assert.Empty(Funktionsanlaesse.Pruefen([F("LDF", bis: "2023-12-31"), F("LDF", ab: "2027-01-01")], new HashSet<string>(), Stichtag, 3));
    }
}
