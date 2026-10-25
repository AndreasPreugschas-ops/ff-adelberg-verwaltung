using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

public class WiedereintrittTests
{
    private static Mitglied Ehemaliger(DateOnly? bis, Austrittsgrund? grund = Austrittsgrund.Austritt)
    {
        var m = new Mitglied { Personalnummer = "1", Vorname = "Eva", Nachname = "Früher", Geburtsdatum = new(1980, 1, 1) };
        m.Mitgliedschaften.Add(new Mitgliedschaft { GueltigAb = new(2000, 1, 1), GueltigBis = bis, Austrittsgrund = bis is null ? null : grund });
        return m;
    }

    [Fact]
    public void Ausgetretene_koennen_wieder_eintreten()
    {
        Assert.Null(Wiedereintritt.Hindernis(Ehemaliger(new(2020, 6, 30)), new(2026, 11, 1)));
    }

    [Fact]
    public void Wer_noch_Mitglied_ist_braucht_Uebertritt()
    {
        Assert.Contains("noch Mitglied", Wiedereintritt.Hindernis(Ehemaliger(null), new(2026, 11, 1)));
        // Endet die Mitgliedschaft erst nach dem gewünschten Beginn, überschneiden sich beide.
        Assert.NotNull(Wiedereintritt.Hindernis(Ehemaliger(new(2026, 12, 31)), new(2026, 11, 1)));
    }

    [Fact]
    public void Anonymisierte_und_Verstorbene_nicht()
    {
        var anonym = Ehemaliger(new(2010, 1, 1));
        anonym.AnonymisiertAm = DateTimeOffset.UtcNow;
        Assert.Contains("anonymisiert", Wiedereintritt.Hindernis(anonym, new(2026, 11, 1)));
        Assert.Contains("verstorben", Wiedereintritt.Hindernis(Ehemaliger(new(2020, 1, 1), Austrittsgrund.Tod), new(2026, 11, 1)));
    }

    [Fact]
    public void Vordienstzeiten_zaehlen_nicht_als_offene_Mitgliedschaft()
    {
        var m = Ehemaliger(new(2020, 6, 30));
        m.Mitgliedschaften.Add(new Mitgliedschaft { GueltigAb = new(1995, 1, 1), GueltigBis = null, Vordienstzeit = true });
        Assert.Null(Wiedereintritt.Hindernis(m, new(2026, 11, 1)));
    }
}
