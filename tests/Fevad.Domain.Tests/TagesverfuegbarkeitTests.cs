using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Tests;

/// <summary>Wochenmuster der freiwilligen Tagesverfügbarkeit (V43).</summary>
public class TagesverfuegbarkeitTests
{
    [Fact]
    public void Summen_zaehlen_je_Werktag()
    {
        var summen = Tagesverfuegbarkeit.Summen([Werktage.Montag | Werktage.Freitag, Werktage.Montag, Werktage.Keine]);
        Assert.Equal([2, 0, 0, 0, 1], summen);
    }

    [Fact]
    public void Tage_in_Wochenfolge_mit_Kurznamen()
    {
        Assert.Equal("Mo Di Mi Do Fr", string.Join(' ', Tagesverfuegbarkeit.Tage.Select(Tagesverfuegbarkeit.Kurz)));
    }
}
