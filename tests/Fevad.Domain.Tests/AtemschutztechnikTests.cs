using Fevad.Domain.Technik;

namespace Fevad.Domain.Tests;

public class AtemschutztechnikTests
{
    [Fact]
    public void Auftragsnummer_zaehlt_je_Jahr_weiter()
    {
        Assert.Equal("W-2026-001", Werkstattauftrag.NaechsteNummer(2026, []));
        Assert.Equal("W-2026-013", Werkstattauftrag.NaechsteNummer(2026, ["W-2026-012", "W-2025-040", "W-2026-003"]));
    }

    [Fact]
    public void Bauteil_ist_faellig_nach_Einbaudauer_oder_Gesamtalter()
    {
        var membran = new Bauteil { HergestelltAm = new(2018, 1, 1), EingebautAm = new(2020, 6, 1) };

        // Acht Jahre ab Herstellung (2026-01-01) kommen vor sechs Jahren Einbau (2026-06-01).
        Assert.Equal(new DateOnly(2026, 1, 1), membran.TauschFaellig(72, 96));
        Assert.Equal(new DateOnly(2026, 6, 1), membran.TauschFaellig(72, null));
        Assert.Null(new Bauteil { EingebautAm = new(2020, 6, 1) }.TauschFaellig(null, 96));
    }

    [Fact]
    public void Nutzungsende_laut_Praegung_begrenzt_die_Lebensdauer()
    {
        var flasche = new Geraet { Inventarnummer = "F1", Bezeichnung = "Atemluftflasche", Herstelldatum = new(2010, 5, 1), NutzbarBis = new(2030, 5, 1) };

        Assert.Equal(new DateOnly(2030, 5, 1), flasche.AussonderungFaellig(null));
        Assert.Equal(new DateOnly(2030, 5, 1), flasche.AussonderungFaellig(30));
        Assert.Equal(new DateOnly(2025, 5, 1), flasche.AussonderungFaellig(15));
    }

    [Fact]
    public void Zustand_kennt_Werkstatt_und_abgelaufene_Nutzung()
    {
        Assert.Equal(GeraetZustand.InWerkstatt, Geraet.ZustandBerechnen(null, [MangelSchwere.AusserBetrieb], inWerkstatt: true));
        Assert.Equal(GeraetZustand.AusserBetrieb, Geraet.ZustandBerechnen(null, [], nutzungAbgelaufen: true));
        Assert.Equal(GeraetZustand.Ausgesondert, Geraet.ZustandBerechnen(new DateOnly(2026, 1, 1), [], inWerkstatt: true));
        Assert.Equal(GeraetZustand.Einsatzbereit, Geraet.ZustandBerechnen(null, []));
    }

    [Fact]
    public void Vorschau_wiederholt_Termine_im_Intervall_und_zieht_Ueberfaelliges_vor()
    {
        var von = new DateOnly(2026, 10, 26);
        var bis = new DateOnly(2028, 12, 31);

        Assert.Equal([new DateOnly(2027, 3, 1)], Pruefplanung.Termine(new DateOnly(2027, 3, 1), 72, von, bis));
        Assert.Equal([von, von.AddMonths(12), von.AddMonths(24)], Pruefplanung.Termine(new DateOnly(2025, 1, 1), 12, von, bis));
        Assert.Equal([von], Pruefplanung.Termine(null, 60, von, bis));
        Assert.Empty(Pruefplanung.Termine(new DateOnly(2030, 1, 1), 60, von, bis));
        Assert.Empty(Pruefplanung.Termine(null, 0, von, bis));
    }
}
