using Fevad.Domain.Kassen;

namespace Fevad.Domain.Tests;

/// <summary>Rechenregeln des Kassenbuchs (P2: V01, V02).</summary>
public class KassenregelnTests
{
    private static readonly Zahlungsmittelkonto Giro = new() { Bezeichnung = "Girokonto", Art = KontoArt.Girokonto, Eroeffnet = new(2026, 1, 1), Anfangsbestand = 1000m };
    private static readonly Zahlungsmittelkonto Bar = new() { Bezeichnung = "Barkasse", Art = KontoArt.Barkasse, Eroeffnet = new(2026, 1, 1), Anfangsbestand = 50m };

    private static Buchung Buchung(BuchungArt art, decimal betrag, Guid konto, Guid? gegenkonto = null, Guid? position = null, DateOnly? datum = null) => new()
    {
        Art = art, Betrag = betrag, KontoId = konto, GegenkontoId = gegenkonto, PlanpositionId = position, Datum = datum ?? new(2026, 3, 1), Text = "Test",
    };

    [Fact]
    public void Kontostand_aus_Einnahmen_Ausgaben_Umbuchung_und_Storno()
    {
        var ausgabe = Buchung(BuchungArt.Ausgabe, 120m, Giro.Id);
        var buchungen = new[]
        {
            Buchung(BuchungArt.Einnahme, 1500m, Giro.Id),
            ausgabe,
            Buchung(BuchungArt.Umbuchung, 200m, Giro.Id, Bar.Id),
            Kassenregeln.Storno(ausgabe, new(2026, 3, 5), "doppelt gebucht"),
            Buchung(BuchungArt.Einnahme, 999m, Giro.Id, datum: new(2026, 12, 31)),
        };

        Assert.Equal(1000m + 1500m - 120m - 200m + 120m, Kassenregeln.Kontostand(Giro, buchungen, new(2026, 6, 30)));
        Assert.Equal(250m, Kassenregeln.Kontostand(Bar, buchungen, new(2026, 6, 30)));
    }

    [Fact]
    public void Storno_hebt_das_Ist_der_Position_auf()
    {
        var position = Guid.NewGuid();
        var ausgabe = Buchung(BuchungArt.Ausgabe, 80m, Giro.Id, position: position);
        var storno = Kassenregeln.Storno(ausgabe, new(2026, 4, 1), "falsche Position");

        Assert.Equal(ausgabe.Id, storno.StornoVonId);
        Assert.Equal(Belegart.Eigenbeleg, storno.Belegart);
        Assert.Equal(0m, Kassenregeln.IstJePosition([ausgabe, storno])[position]);
    }

    [Fact]
    public void Deckungsfaehige_Positionen_helfen_sich_innerhalb_der_Hauptgruppe()
    {
        var p61 = new Planposition { Nummer = "6.1", Bezeichnung = "Kameradschaftspflege", Richtung = Planrichtung.Ausgabe, Deckungsfaehig = true };
        var p62 = new Planposition { Nummer = "6.2", Bezeichnung = "Ehrungen", Richtung = Planrichtung.Ausgabe, Deckungsfaehig = true };
        var p7 = new Planposition { Nummer = "7", Bezeichnung = "Anschaffungen", Richtung = Planrichtung.Ausgabe };
        var alle = new[] { p61, p62, p7 };
        var ansaetze = new Dictionary<Guid, decimal> { [p61.Id] = 500m, [p62.Id] = 300m, [p7.Id] = 1000m };
        var ist = new Dictionary<Guid, decimal> { [p61.Id] = 650m, [p62.Id] = 100m, [p7.Id] = 0m };

        Assert.Equal(-150m + 200m, Kassenregeln.Verfuegbar(p61, alle, ansaetze, ist));
        Assert.Equal(200m, Kassenregeln.Verfuegbar(p62, alle, ansaetze, ist));
        Assert.Equal(1000m, Kassenregeln.Verfuegbar(p7, alle, ansaetze, ist));
        Assert.Equal(6, p61.Hauptgruppe);
    }

    [Theory]
    [InlineData(BuchungArt.Ausgabe, 100, 100, false)]
    [InlineData(BuchungArt.Ausgabe, 100.01, 100, true)]
    [InlineData(BuchungArt.Einnahme, 500, 100, false)]
    [InlineData(BuchungArt.Ausgabe, 500, 0, false)]
    public void Beschlusshinweis_erst_ueber_der_Grenze_und_nur_bei_Ausgaben(BuchungArt art, decimal betrag, int grenze, bool erwartet) =>
        Assert.Equal(erwartet, Kassenregeln.BeschlussHinweis(art, betrag, grenze));

    [Theory]
    [InlineData(Belegart.Rechnung, null, 0, false)]
    [InlineData(Belegart.Rechnung, null, 1, true)]
    [InlineData(Belegart.Eigenbeleg, " ", 0, false)]
    [InlineData(Belegart.Eigenbeleg, "Parkautomat ohne Quittung", 0, true)]
    public void Festschreiben_nur_mit_Beleg(Belegart art, string? begruendung, int dateien, bool erwartet) =>
        Assert.Equal(erwartet, Kassenregeln.BelegVollstaendig(art, begruendung, dateien));

    [Fact]
    public void Pruefen_meldet_falsche_Seite_fehlende_Position_und_Umbuchung_ohne_Ziel()
    {
        var einnahme = new Planposition { Nummer = "1.1", Bezeichnung = "Zuwendung", Richtung = Planrichtung.Einnahme };
        var konten = new[] { Giro, Bar };

        Assert.Contains(Kassenregeln.Pruefen(Buchung(BuchungArt.Ausgabe, 10m, Giro.Id, position: einnahme.Id), konten, [einnahme]),
            f => f.Contains("ist eine Einnahme"));
        Assert.Contains(Kassenregeln.Pruefen(Buchung(BuchungArt.Ausgabe, 10m, Giro.Id), konten, [einnahme]), f => f.Contains("Position"));
        Assert.Contains(Kassenregeln.Pruefen(Buchung(BuchungArt.Umbuchung, 10m, Giro.Id, Giro.Id), konten, [einnahme]), f => f.Contains("anderes Konto"));
        Assert.Contains(Kassenregeln.Pruefen(Buchung(BuchungArt.Einnahme, 10m, Giro.Id, position: einnahme.Id, datum: new(2025, 12, 31)), konten, [einnahme]),
            f => f.Contains("erst ab 01.01.2026"));
        Assert.Empty(Kassenregeln.Pruefen(Buchung(BuchungArt.Einnahme, 10m, Giro.Id, position: einnahme.Id), konten, [einnahme]));
        Assert.Empty(Kassenregeln.Pruefen(Buchung(BuchungArt.Umbuchung, 10m, Giro.Id, Bar.Id), konten, [einnahme]));
    }

    [Fact]
    public void Muster_haben_eindeutige_Nummern_und_beide_Seiten()
    {
        foreach (var muster in new[] { Kassenregeln.MusterKameradschaftskasse, Kassenregeln.MusterJugendkasse })
        {
            Assert.Equal(muster.Count, muster.Select(m => m.Nummer).Distinct().Count());
            Assert.Contains(muster, m => m.Richtung == Planrichtung.Einnahme && m.Nummer == "1.1");
            Assert.Contains(muster, m => m.Richtung == Planrichtung.Ausgabe && m.Nummer == "9");
        }
    }
}
