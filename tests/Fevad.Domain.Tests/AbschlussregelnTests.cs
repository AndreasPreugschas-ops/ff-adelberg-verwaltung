using Fevad.Domain.Kassen;

namespace Fevad.Domain.Tests;

/// <summary>Regeln für Bestandsverzeichnis und Jahresabschluss (P2: V04, V05).</summary>
public class AbschlussregelnTests
{
    private static Buchung Ausgabe(decimal betrag, DateOnly datum, Guid? stornoVon = null) => new()
    {
        Art = BuchungArt.Ausgabe, Betrag = betrag, Datum = datum, Jahr = datum.Year, Text = "Test", StornoVonId = stornoVon,
    };

    [Fact]
    public void Vorschlaege_nur_Ausgaben_ab_der_Grenze_ohne_Storno_Verknuepfung_und_Verbrauch()
    {
        var zelt = Ausgabe(750m, new(2026, 5, 1));
        var genau = Ausgabe(500m, new(2026, 6, 1));
        var klein = Ausgabe(499.99m, new(2026, 6, 1));
        var storniert = Ausgabe(900m, new(2026, 7, 1));
        var storno = Ausgabe(900m, new(2026, 7, 2), storniert.Id);
        var verknuepft = Ausgabe(1200m, new(2026, 8, 1));
        var fest = Ausgabe(800m, new(2026, 9, 1));
        fest.KeinGegenstand = true;
        var einnahme = new Buchung { Art = BuchungArt.Einnahme, Betrag = 5000m, Datum = new(2026, 9, 1), Text = "Zuwendung" };
        var alle = new[] { zelt, genau, klein, storniert, storno, verknuepft, fest, einnahme };

        var vorschlaege = Abschlussregeln.Vorschlaege(alle, 500, new HashSet<Guid> { verknuepft.Id });
        Assert.Equal([genau.Id, zelt.Id], vorschlaege.Select(b => b.Id));
        Assert.Empty(Abschlussregeln.Vorschlaege(alle, 0, new HashSet<Guid>()));
    }

    [Fact]
    public void Gesperrt_und_Hindernisse_beim_Abschluss()
    {
        Assert.True(Abschlussregeln.Gesperrt(new HashSet<int> { 2025 }, new(2025, 12, 31)));
        Assert.False(Abschlussregeln.Gesperrt(new HashSet<int> { 2025 }, new(2026, 1, 1)));

        var heute = new DateOnly(2026, 10, 25);
        Assert.Empty(Abschlussregeln.Hindernisse(2025, 0, false, heute));
        var gruende = Abschlussregeln.Hindernisse(2026, 3, true, heute);
        Assert.Equal(3, gruende.Count);
        Assert.Contains(gruende, g => g.Contains("noch nicht vorbei"));
        Assert.Contains(gruende, g => g.Contains("3 Buchungen"));
        Assert.Contains(gruende, g => g.Contains("2025 abschließen"));
    }

    [Fact]
    public void Gegenstand_ist_bis_zum_Abgang_im_Bestand()
    {
        var g = new Vermoegensgegenstand { Bezeichnung = "Zelt", Anschaffung = new(2024, 5, 1), AbgangAm = new(2026, 3, 1) };
        Assert.False(g.ImBestand(new(2024, 4, 30)));
        Assert.True(g.ImBestand(new(2025, 12, 31)));
        Assert.False(g.ImBestand(new(2026, 3, 1)));
    }
}

/// <summary>Regeln der Kassenprüfung (P2: V05).</summary>
public class PruefregelnTests
{
    [Fact]
    public void Zwei_verschiedene_amtierende_Pruefer_und_ein_gueltiger_Zeitraum()
    {
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var amtierend = new HashSet<Guid> { a, b };
        Assert.Empty(Pruefregeln.Fehler(new(2025, 1, 1), new(2025, 12, 31), a, b, amtierend));
        Assert.Contains(Pruefregeln.Fehler(new(2025, 1, 1), new(2025, 12, 31), a, a, amtierend), f => f.Contains("zwei verschiedene"));
        Assert.Contains(Pruefregeln.Fehler(new(2025, 1, 1), new(2025, 12, 31), a, c, amtierend), f => f.Contains("im Amt"));
        Assert.Contains(Pruefregeln.Fehler(new(2025, 12, 31), new(2025, 1, 1), a, b, amtierend), f => f.Contains("vor seinem Beginn"));

        var p = new Kassenpruefung { Pruefer1Id = a, Pruefer2Id = b, Pruefer1BestaetigtAm = DateTimeOffset.UtcNow };
        Assert.False(p.Abgeschlossen);
        Assert.True(p.IstPruefer(b));
        Assert.False(p.IstPruefer(c));
        p.Pruefer2BestaetigtAm = DateTimeOffset.UtcNow;
        Assert.True(p.Abgeschlossen);
    }

    [Fact]
    public void Jahr_gilt_als_geprueft_wenn_eine_abgeschlossene_Pruefung_bis_zum_Jahresende_reicht()
    {
        var jetzt = DateTimeOffset.UtcNow;
        Kassenpruefung Pruefung(DateOnly von, DateOnly bis, bool fertig) => new()
        {
            Von = von, Bis = bis, Pruefer1BestaetigtAm = jetzt, Pruefer2BestaetigtAm = fertig ? jetzt : null,
        };

        Assert.True(Pruefregeln.JahrGeprueft([Pruefung(new(2025, 1, 1), new(2025, 12, 31), true)], 2025));
        Assert.False(Pruefregeln.JahrGeprueft([Pruefung(new(2025, 1, 1), new(2025, 12, 31), false)], 2025));
        Assert.False(Pruefregeln.JahrGeprueft([Pruefung(new(2025, 1, 1), new(2025, 6, 30), true)], 2025));
        Assert.True(Pruefregeln.JahrGeprueft([Pruefung(new(2024, 7, 1), new(2026, 6, 30), true)], 2025));
        Assert.False(Pruefregeln.JahrGeprueft([], 2025));
    }
}

/// <summary>Regeln des Spenden- und Sponsoringregisters (P2: V08).</summary>
public class ZuwendungTests
{
    [Fact]
    public void Annahmebeschluss_ab_der_Wertgrenze_und_Sponsoring_im_wirtschaftlichen_Bereich()
    {
        var z = new Zuwendung { Geber = "Muster", Betrag = 80m };
        Assert.True(z.AnnahmeFehlt(0));
        Assert.False(z.AnnahmeFehlt(100));
        z.AnnahmeAm = new DateOnly(2027, 3, 14);
        Assert.False(z.AnnahmeFehlt(0));

        z.Sponsoring = true;
        Assert.False(z.SteuerbereichFalsch);
        z.Buchung = new Buchung { Text = "Plakat", Steuerbereich = Steuerbereich.Intern };
        Assert.True(z.SteuerbereichFalsch);
        z.Buchung.Steuerbereich = Steuerbereich.Wirtschaftlich;
        Assert.False(z.SteuerbereichFalsch);
    }
}
