using Fevad.Domain.Einsaetze;

namespace Fevad.Domain.Tests;

public class AaoTests
{
    private static readonly Guid Hlf = Guid.NewGuid();
    private static readonly Guid Tlf = Guid.NewGuid();

    private static string Name(Guid id) => id == Hlf ? "HLF 10" : "TLF 8/18";

    [Fact]
    public void Stichwort_ohne_Leerzeichen_und_Grossschreibung_vergleichen()
    {
        Assert.Equal(AaoRegel.Schluessel("B 2"), AaoRegel.Schluessel(" b2 "));
        Assert.NotEqual(AaoRegel.Schluessel("B 2"), AaoRegel.Schluessel("B 3"));
    }

    [Fact]
    public void Erfuellte_AAO_hat_keine_Abweichung_auch_mit_mehr_Kraeften()
    {
        var regel = new AaoRegel { Stichwort = "B 2", Fahrzeuge = [Hlf], Mindeststaerke = 9, MindestAtemschutz = 4 };
        Assert.Empty(regel.Abweichungen([Hlf, Tlf], 15, 6, Name));
    }

    [Fact]
    public void Fehlendes_Fahrzeug_und_zu_wenig_Kraefte_werden_benannt()
    {
        var regel = new AaoRegel { Stichwort = "B 2", Fahrzeuge = [Hlf, Tlf], Mindeststaerke = 9, MindestAtemschutz = 4 };
        var abweichungen = regel.Abweichungen([Hlf], 6, 2, Name);
        Assert.Equal(["Nicht ausgerückt: TLF 8/18", "Stärke 6 statt mindestens 9", "2 Atemschutzgeräteträger statt mindestens 4"], abweichungen);
    }
}
