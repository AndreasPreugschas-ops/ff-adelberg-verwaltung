using Fevad.Domain.Einsaetze;

namespace Fevad.Domain.Tests;

public class KostenersatzTests
{
    private static readonly DateTimeOffset Alarm = new(2026, 7, 1, 18, 0, 0, TimeSpan.Zero);

    private static Einsatz EinsatzMit(Guid fahrzeugId) => new()
    {
        Einsatznummer = "2026-001",
        Alarmiert = Alarm,
        Einsatzende = Alarm.AddHours(2),
        EinsatzbereitschaftHergestellt = Alarm.AddHours(2).AddMinutes(20),
        Fahrzeuge = [new EinsatzFahrzeug { FahrzeugId = fahrzeugId, Ausgerueckt = Alarm.AddMinutes(5), Einsatzbereit = Alarm.AddMinutes(95) }],
        Teilnahmen =
        [
            // Eigene Zeiten: 18:00–19:10, 1:10 h → 1,5 h bei halbstündlicher Rundung.
            new EinsatzTeilnahme { MitgliedId = Guid.NewGuid(), Von = Alarm, Bis = Alarm.AddMinutes(70) },
            // Ohne eigene Zeiten: Alarm bis Einsatzbereitschaft, 2:20 h → 2,5 h.
            new EinsatzTeilnahme { MitgliedId = Guid.NewGuid() },
        ],
    };

    [Fact]
    public void Berechnet_Fahrzeug_Personal_und_Ausruestung_aus_eigenen_Zeiten()
    {
        var fahrzeugId = Guid.NewGuid();
        var oel = new Kostensatz { Art = KostensatzArt.Verbrauch, Bezeichnung = "Ölbindemittel", Einheit = Kosteneinheit.Stueck, Betrag = 18.5m, GueltigAb = new(2026, 1, 1) };
        var saetze = new List<Kostensatz>
        {
            new() { Art = KostensatzArt.Fahrzeug, FahrzeugId = fahrzeugId, Bezeichnung = "LF", Einheit = Kosteneinheit.Stunde, Betrag = 120m, GueltigAb = new(2026, 1, 1) },
            // Alte Fassung darf nicht greifen.
            new() { Art = KostensatzArt.Fahrzeug, FahrzeugId = fahrzeugId, Bezeichnung = "LF", Einheit = Kosteneinheit.Stunde, Betrag = 99m, GueltigAb = new(2020, 1, 1), GueltigBis = new(2025, 12, 31) },
            new() { Art = KostensatzArt.Personal, Bezeichnung = "Einsatzkräfte", Einheit = Kosteneinheit.Stunde, Betrag = 30m, GueltigAb = new(2026, 1, 1) },
            oel,
        };
        var einsatz = EinsatzMit(fahrzeugId);
        einsatz.Ausruestung.Add(new EinsatzAusruestung { Kostensatz = oel, Menge = 3 });

        var ergebnis = Kostenersatz.Berechnen(einsatz, saetze, 30, _ => "Florian 43");

        Assert.Empty(ergebnis.Hinweise);
        // Fahrzeug 18:05–19:35 = 1,5 h × 120.
        Assert.Equal(new Kostenposten(KostensatzArt.Fahrzeug, "Florian 43", 1.5m, Kosteneinheit.Stunde, 120m, 180m), ergebnis.Posten[0]);
        Assert.Equal(4m, ergebnis.Posten[1].Menge);
        Assert.Equal(120m, ergebnis.Posten[1].Betrag);
        Assert.Equal(55.5m, ergebnis.Posten[2].Betrag);
        Assert.Equal(355.5m, ergebnis.Summe);
    }

    [Fact]
    public void Fehlende_Saetze_werden_gemeldet_statt_mit_null_gerechnet()
    {
        var ergebnis = Kostenersatz.Berechnen(EinsatzMit(Guid.NewGuid()), [], 30, _ => "Florian 43");

        Assert.Empty(ergebnis.Posten);
        Assert.Equal(2, ergebnis.Hinweise.Count);
    }

    [Theory]
    [InlineData(30, 61, 1.5)]
    [InlineData(60, 61, 2.0)]
    [InlineData(1, 61, 1.02)]
    public void Stunden_runden_angefangene_Einheiten_auf(int rundung, int minuten, double stunden) =>
        Assert.Equal((decimal)stunden, Kostenersatz.Stunden(TimeSpan.FromMinutes(minuten), rundung));
}
