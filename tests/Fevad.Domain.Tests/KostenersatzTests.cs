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

    [Fact]
    public void Fahrzeug_ohne_eigenen_Satz_rechnet_mit_der_VOKeFw_Klasse()
    {
        var fahrzeugId = Guid.NewGuid();
        var saetze = new List<Kostensatz>
        {
            new() { Art = KostensatzArt.Fahrzeugklasse, Bezeichnung = "TLF 2000", Einheit = Kosteneinheit.Stunde, Betrag = 155m, GueltigAb = new(2024, 3, 11) },
        };

        var ergebnis = Kostenersatz.Berechnen(EinsatzMit(fahrzeugId), saetze, 30, _ => "Florian 21", _ => "tlf 2000");

        // 18:05–19:35 = 1,5 h × 155.
        Assert.Equal(new Kostenposten(KostensatzArt.Fahrzeugklasse, "Florian 21 (TLF 2000)", 1.5m, Kosteneinheit.Stunde, 155m, 232.5m), ergebnis.Posten[0]);

        // Ein eigener Satz am Fahrzeug geht vor.
        saetze.Add(new() { Art = KostensatzArt.Fahrzeug, FahrzeugId = fahrzeugId, Bezeichnung = "Florian 21", Einheit = Kosteneinheit.Stunde, Betrag = 100m, GueltigAb = new(2024, 1, 1) });
        Assert.Equal(150m, Kostenersatz.Berechnen(EinsatzMit(fahrzeugId), saetze, 30, _ => "Florian 21", _ => "TLF 2000").Posten[0].Betrag);
    }

    [Fact]
    public void Ergaenzen_rechnet_Vorhaltezuschlag_nur_auf_Verbrauch_und_kennzeichnet_vorlaeufige_Gebuehr()
    {
        var basis = new Kostenaufstellung([], []);
        var gebuehr = new Kostensatz { Art = KostensatzArt.Gebuehr, Bezeichnung = "Verwaltungsgebühr", Einheit = Kosteneinheit.Pauschale, Betrag = 50m, Quelle = "Platzhalter: Entwurf" };
        var zusatz = new[]
        {
            new Zusatzkosten { Art = ZusatzkostenArt.Verbrauch, Bezeichnung = "Bindemittel", Betrag = 100m },
            new Zusatzkosten { Art = ZusatzkostenArt.Ueberlandhilfe, Bezeichnung = "FF Göppingen", Betrag = 300m },
        };

        var ergebnis = Kostenersatz.Ergaenzen(basis, zusatz, 10m, gebuehr);

        Assert.Equal([300m, 110m, 50m], ergebnis.Posten.Select(p => p.Betrag));
        Assert.Contains("Vorhaltezuschlag", ergebnis.Posten[1].Bezeichnung);
        Assert.Equal(Kostenersatz.PlatzhalterHinweis, Assert.Single(ergebnis.Hinweise));
        Assert.Equal(460m, ergebnis.Summe);
    }

    [Fact]
    public void Vorgang_prueft_Pflichtangaben_je_Tatbestand()
    {
        var vorgang = new Kostenersatzvorgang();
        Assert.Contains(vorgang.Pruefen(KostenersatzErgebnis.Kostenpflichtig, null, null), f => f.Contains("Tatbestand"));
        Assert.Empty(vorgang.Pruefen(KostenersatzErgebnis.Unentgeltlich, null, null));

        vorgang.Tatbestand = Kostentatbestand.Abs1Nr2Fahrzeug;
        vorgang.Schuldner.Add(new Kostenschuldner { Name = "Muster", Art = SchuldnerArt.Halter });
        Assert.Contains(vorgang.Pruefen(KostenersatzErgebnis.Kostenpflichtig, null, null), f => f.Contains("Kennzeichen"));
        vorgang.Schuldner[0].Kennzeichen = "GP-AB 123";
        Assert.Empty(vorgang.Pruefen(KostenersatzErgebnis.Kostenpflichtig, null, null));

        Assert.Contains(vorgang.Pruefen(KostenersatzErgebnis.Verzicht, Verzichtsgrund.UnbilligeHaerte, " "), f => f.Contains("Begründung"));
        Assert.Empty(vorgang.Pruefen(KostenersatzErgebnis.Verzicht, Verzichtsgrund.UnbilligeHaerte, "Rentnerin, geringes Einkommen"));

        vorgang.Tatbestand = Kostentatbestand.Abs2Leistung;
        Assert.Contains(vorgang.Pruefen(KostenersatzErgebnis.Unentgeltlich, null, null), f => f.Contains("§ 2 Abs. 2"));

        vorgang.Tatbestand = Kostentatbestand.Abs1Nr6Brandmeldeanlage;
        Assert.Contains(vorgang.Pruefen(KostenersatzErgebnis.Kostenpflichtig, null, null), f => f.Contains("Betreiber"));
    }

    [Fact]
    public void Stand_folgt_Empfehlung_und_Entscheidung()
    {
        var vorgang = new Kostenersatzvorgang();
        Assert.Equal((KostenersatzStand.InBearbeitung, true), (vorgang.Stand, vorgang.FeuerwehrDarfAendern));
        vorgang.Empfehlung = KostenersatzErgebnis.Kostenpflichtig;
        Assert.Equal((KostenersatzStand.Empfohlen, false), (vorgang.Stand, vorgang.FeuerwehrDarfAendern));
        vorgang.Entscheidung = KostenersatzErgebnis.Verzicht;
        Assert.Equal(KostenersatzStand.Entschieden, vorgang.Stand);
    }
}
