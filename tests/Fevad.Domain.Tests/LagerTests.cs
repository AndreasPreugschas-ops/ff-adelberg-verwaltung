using Fevad.Domain.Technik;

namespace Fevad.Domain.Tests;

/// <summary>Verbrauchsmaterial (V16): Entnahme nach FEFO, verfallene Chargen, Mindestbestand.</summary>
public class LagerTests
{
    private static readonly DateOnly Heute = new(2026, 10, 26);

    private static Lagerartikel Artikel(params (decimal Menge, DateOnly? Verfall)[] chargen)
    {
        var artikel = new Lagerartikel { Bezeichnung = "Verbandpäckchen", Einheit = "Stück", Mindestmenge = 5 };
        foreach (var (menge, verfall) in chargen)
        {
            artikel.Chargen.Add(new Lagercharge { Menge = menge, VerfallAm = verfall, EingangAm = Heute.AddYears(-1) });
        }

        return artikel;
    }

    [Fact]
    public void Entnahme_nimmt_zuerst_was_am_fruehesten_verfaellt_und_Chargen_ohne_Verfall_zuletzt()
    {
        var artikel = Artikel((10, null), (4, Heute.AddMonths(6)), (3, Heute.AddMonths(2)));

        var teile = artikel.Entnehmen(5, Heute);

        Assert.NotNull(teile);
        Assert.Equal([(Heute.AddMonths(2), 3m), (Heute.AddMonths(6), 2m)], teile.Select(t => (t.Charge.VerfallAm!.Value, t.Menge)));
        Assert.Equal(12, artikel.Bestand);
        Assert.Equal(Heute.AddMonths(6), artikel.NaechsterVerfall);
    }

    [Fact]
    public void Verfallene_Chargen_bleiben_liegen_und_zu_wenig_Bestand_aendert_nichts()
    {
        var artikel = Artikel((6, Heute.AddDays(-1)), (2, Heute));

        Assert.Null(artikel.Entnehmen(3, Heute));
        Assert.Equal(8, artikel.Bestand);
        Assert.Null(artikel.Entnehmen(0, Heute));

        var teile = artikel.Entnehmen(2, Heute);
        Assert.Equal(2, Assert.Single(teile!).Menge);
        Assert.Equal(6, artikel.Bestand);
    }

    [Fact]
    public void Nachbestellen_erst_unter_Mindestmenge_und_Wert_aus_Preis()
    {
        Assert.False(Artikel((5, null)).Nachbestellen);
        Assert.True(Artikel((4.5m, null)).Nachbestellen);
        Assert.False(new Lagerartikel { Bezeichnung = "x", Einheit = "Stück" }.Nachbestellen);
        Assert.Equal(56.70m, new Lagerbewegung { Menge = 3, Stueckpreis = 18.90m }.Wert);
        Assert.Null(new Lagerbewegung { Menge = 3 }.Wert);
    }
}
