using Fevad.Domain.Haushalt;

namespace Fevad.Domain.Tests;

/// <summary>Haushaltsanmeldung (V29): Planjahr, Wertgrenze, Pflichtangaben und Summen.</summary>
public class HaushaltTests
{
    private static readonly Haushaltsstelle Eh = new() { Code = "EH", Bezeichnung = "EH", Produkt = "12.60.01", Kostenstelle = "1", Sachkonto = "4221", Teil = Haushaltsteil.Ergebnishaushalt };
    private static readonly Haushaltsstelle Fh = new() { Code = "FH", Bezeichnung = "FH", Produkt = "12.60.01", Kostenstelle = "1", Sachkonto = "7832", Teil = Haushaltsteil.Finanzhaushalt };

    [Fact]
    public void Planjahr_ist_Gesamtbetrag_ohne_Raten_und_Investition_nach_Nettopreis()
    {
        var p = new Haushaltsposition { Bezeichnung = "TLF", Menge = 1, Einzelpreis = 420000m, RateFolgejahr1 = 300000m };
        Assert.Equal(120000m, p.BetragPlanjahr);
        Assert.True(p.Investiv(1000m, 19m));
        Assert.True(new Haushaltsposition { Bezeichnung = "x", Einzelpreis = 1190m }.Investiv(1000m, 19m));
        Assert.False(new Haushaltsposition { Bezeichnung = "x", Einzelpreis = 1189.99m }.Investiv(1000m, 19m));
    }

    [Fact]
    public void Ab_Wertgrenze_sind_Begruendung_und_Rechtsgrundlage_Pflicht()
    {
        Assert.Equal(["Begründung", "Rechtsgrundlage"], new Haushaltsposition { Bezeichnung = "Satz", Menge = 10, Einzelpreis = 150m }.Fehlt(1000m, 19m));
        Assert.Empty(new Haushaltsposition { Bezeichnung = "Klein", Menge = 2, Einzelpreis = 100m }.Fehlt(1000m, 19m));
        Assert.Equal(["Betrag"], new Haushaltsposition { Bezeichnung = "Satz fehlt", Menge = 3, Einzelpreis = 0m }.Fehlt(1000m, 19m));
        Assert.Contains("Raten größer als der Gesamtbetrag",
            new Haushaltsposition { Bezeichnung = "x", Einzelpreis = 500m, RateFolgejahr2 = 600m, Begruendung = "b", Rechtsgrundlage = "r" }.Fehlt(1000m, 19m));
    }

    [Fact]
    public void Summen_nur_aus_angemeldeten_Positionen_je_Haushaltsteil()
    {
        var a = new Haushaltsanmeldung { Jahr = 2027 };
        a.Positionen.AddRange([
            new() { Bezeichnung = "a", Einzelpreis = 100m, Haushaltsstelle = Eh, Status = PositionStatus.Angemeldet },
            new() { Bezeichnung = "b", Einzelpreis = 50m, Haushaltsstelle = Eh, Status = PositionStatus.Vorschlag },
            new() { Bezeichnung = "c", Einzelpreis = 80000m, RateFolgejahr1 = 30000m, Zuwendung = 25000m, Haushaltsstelle = Fh, Status = PositionStatus.Angemeldet },
            new() { Bezeichnung = "d", Einzelpreis = 9m, Haushaltsstelle = Eh, Status = PositionStatus.Gestrichen },
        ]);
        Assert.Equal(100m, a.Summe(Haushaltsteil.Ergebnishaushalt));
        Assert.Equal(50000m, a.Summe(Haushaltsteil.Finanzhaushalt));
        Assert.Equal(25000m, a.Zuwendungen);
        Assert.Equal([AnmeldungStatus.Eingereicht, AnmeldungStatus.Entwurf], Haushaltsanmeldung.Folgestatus(AnmeldungStatus.Beraten));
    }

    [Fact]
    public void Ueberwachung_ordnet_Ist_ueber_Sachkonto_und_Innenauftrag_zu()
    {
        var allgemein = new Haushaltsstelle { Code = "EH-4221", Bezeichnung = "Unterhaltung", Produkt = "p", Kostenstelle = "126000", Sachkonto = "4221", Teil = Haushaltsteil.Ergebnishaushalt, Sortierung = 1 };
        var atemschutz = new Haushaltsstelle { Code = "EH-4221-AS", Bezeichnung = "Atemschutz", Produkt = "p", Kostenstelle = "126000", Innenauftrag = "IA 126010 Atemschutz", Sachkonto = "4221", Teil = Haushaltsteil.Ergebnishaushalt, Sortierung = 2 };
        var a = new Haushaltsanmeldung { Jahr = 2027, Status = AnmeldungStatus.Beschlossen };
        a.Positionen.AddRange([
            new() { Bezeichnung = "Reparaturen", Einzelpreis = 5000m, HaushaltsstelleId = allgemein.Id, Status = PositionStatus.Angemeldet, Ergebnis = Haushaltsergebnis.Gekuerzt, BeschlossenerBetrag = 4000m },
            new() { Bezeichnung = "Prüfung PA", Einzelpreis = 2000m, HaushaltsstelleId = atemschutz.Id, Status = PositionStatus.Angemeldet, Ergebnis = Haushaltsergebnis.Beschlossen },
            new() { Bezeichnung = "Masken", Einzelpreis = 900m, HaushaltsstelleId = atemschutz.Id, Status = PositionStatus.Angemeldet, Ergebnis = Haushaltsergebnis.Verschoben },
        ]);
        HaushaltIst Ist(string konto, string? ia, decimal betrag, decimal obligo = 0) => new() { Jahr = 2027, Sachkonto = konto, Kostenstelle = "126000", Innenauftrag = ia, Ist = betrag, Obligo = obligo };
        var ist = new[] { Ist("4221", null, 3500m, 800m), Ist("4221", "126010", 1500m), Ist("4999", null, 1m) };

        var staende = Haushaltsueberwachung.Berechnen([atemschutz, allgemein], a, new Dictionary<Guid, decimal> { [allgemein.Id] = 500m }, ist);

        var s1 = staende[0];
        Assert.Equal((4000m, 800m, 3500m, -300m, true, false), (s1.Ansatz, s1.Gebunden, s1.Ist, s1.Frei, s1.Ueberschritten, s1.Vorlaeufig));
        var s2 = staende[1];
        Assert.Equal((2000m, 1500m, 500m, 75m), (s2.Ansatz, s2.Ist, s2.Frei, s2.Auslastung!.Value));
        Assert.Single(Haushaltsueberwachung.NichtZugeordnet([allgemein, atemschutz], ist));
    }

    [Fact]
    public void Ist_Import_und_Haushaltsexport_nach_Spaltenzuordnung()
    {
        var format = Fevad.Domain.Gemeinsam.Finanzexport.Standard(Fevad.Domain.Gemeinsam.ExportArt.HaushaltIst);
        var zeilen = Fevad.Domain.Gemeinsam.Finanzexport.HaushaltIstLesen(format,
            "Sachkonto;Kostenstelle;Innenauftrag;Ist;Obligo;Stand\n4221;126000;;1.234,56;;30.06.2027\n;1;;1;;\n4271;126000;;x;0;\n", out var fehler);
        var z = Assert.Single(zeilen);
        Assert.Equal(("4221", 1234.56m, 0m, new DateOnly(2027, 6, 30)), (z.Sachkonto, z.Ist, z.Obligo, z.Stand!.Value));
        Assert.Equal(2, fehler.Count);

        var export = Fevad.Domain.Gemeinsam.Finanzexport.Standard(Fevad.Domain.Gemeinsam.ExportArt.Haushalt);
        export.Sachkonto = "falsch";
        var datei = System.Text.Encoding.UTF8.GetString(Fevad.Domain.Gemeinsam.Finanzexport.Erzeugen(export,
            [new Dictionary<string, object?> { ["Sachkonto"] = "7832", ["BetragPlanjahr"] = 120000m }]));
        Assert.Contains("7832", datei);
        Assert.DoesNotContain("falsch", datei);
    }
}
