using System.Text;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Tests;

public class FinanzexportTests
{
    [Fact]
    public void Spalten_Ueberschriften_feste_Texte_und_Formatierung()
    {
        var format = new Exportformat
        {
            Art = ExportArt.Entschaedigung, Kodierung = Exportkodierung.Utf8, DezimalKomma = false, Datumsformat = "yyyyMMdd",
            Spalten = "PNR=Personalnummer\nBetrag\nVon\n\"EH\"\nNachname", Buchungsstelle = "12.60",
        };
        var zeile = new Dictionary<string, object?> { ["Personalnummer"] = "0815", ["Betrag"] = 12.5m, ["Von"] = new DateOnly(2026, 1, 1), ["Nachname"] = "=Formel; mit Trenner" };
        var text = Encoding.UTF8.GetString(Finanzexport.Erzeugen(format, [zeile]));
        Assert.Equal("PNR;Betrag;Von;;Nachname\r\n0815;12.50;20260101;EH;\"'=Formel; mit Trenner\"\r\n", text);
    }

    [Fact]
    public void Unbekannte_Felder_werden_gemeldet()
    {
        Finanzexport.SpaltenLesen(new Exportformat { Art = ExportArt.Inventar, Spalten = "Inventarnummer\nIban" }, out var fehler);
        Assert.StartsWith("Unbekanntes Feld „Iban“", fehler);
    }

    [Fact]
    public void Rueckmeldung_liest_Anfuehrungszeichen_und_meldet_Fehler()
    {
        var format = Finanzexport.Standard(ExportArt.Rueckmeldung);
        var zeilen = Finanzexport.RueckmeldungLesen(format, "Kassenzeichen;Status;Bezahlt;Datum\n\"KZ;1\";Gemahnt;1.234,50;02.03.2030\nKZ2;offen;abc;\n", out var fehler);
        var z = Assert.Single(zeilen);
        Assert.Equal(("KZ;1", "gemahnt", 1234.50m, new DateOnly(2030, 3, 2)), (z.Kassenzeichen, z.Status, z.Bezahlt!.Value, z.Datum!.Value));
        Assert.Contains("nicht lesbar", Assert.Single(fehler));
    }
}
