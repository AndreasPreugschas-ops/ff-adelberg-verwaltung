using System.Globalization;
using System.Text;

namespace Fevad.Infrastructure.Auswertungen;

/// <summary>
/// Kennzahlen der Jahresstatistik als CSV (Bereich;Kennzahl;Wert) zum Übertragen in das Erhebungsformular
/// des Kreisbrandmeisters. Das amtliche Formular ist nicht öffentlich; die Zeilen folgen den Landeskategorien.
/// </summary>
public static class JahresmeldungCsv
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Erzeugt die Jahresmeldung als CSV-Datei.</summary>
    /// <param name="b">Die berechnete Jahresstatistik.</param>
    /// <returns>Die CSV-Datei als Bytes.</returns>
    public static byte[] Erzeugen(Jahresstatistik.Bericht b)
    {
        var csv = new StringBuilder();
        void Zeile(string bereich, string kennzahl, string wert) => csv.Append($"{bereich};{kennzahl};{wert}\r\n");

        Zeile("Bereich", "Kennzahl", "Wert");
        Zeile("Allgemein", "Jahr", b.Jahr.ToString(Deutsch));
        foreach (var k in b.Einsaetze.NachKategorie)
        {
            Zeile("Einsätze", k.Bezeichnung, k.Anzahl.ToString(Deutsch));
        }

        Zeile("Einsätze", "Gesamt (freigegeben)", b.Einsaetze.Freigegeben.ToString(Deutsch));
        Zeile("Einsätze", "Gerettete Personen", b.Einsaetze.Gerettete.ToString(Deutsch));
        Zeile("Einsätze", "Verletzte Personen", b.Einsaetze.Verletzte.ToString(Deutsch));
        Zeile("Einsätze", "Tote", b.Einsaetze.Tote.ToString(Deutsch));
        Zeile("Einsätze", "Verletzte Feuerwehrangehörige", b.Einsaetze.VerletzteFeuerwehr.ToString(Deutsch));
        Zeile("Einsätze", "Überörtliche Einsätze", b.Einsaetze.Ueberoertlich.ToString(Deutsch));
        Zeile("Einsätze", "Personenstunden", b.Einsaetze.Personenstunden.ToString("0.0", Deutsch));

        foreach (var d in b.Dienste.NachArt)
        {
            Zeile("Dienste", $"{JahresberichtPdf.DienstArtText(d.Art)}: Anzahl", d.Anzahl.ToString(Deutsch));
            Zeile("Dienste", $"{JahresberichtPdf.DienstArtText(d.Art)}: Personenstunden", d.Personenstunden.ToString("0.0", Deutsch));
        }

        foreach (var a in b.Personal.Abteilungen)
        {
            Zeile("Personal 31.12.", a.Bezeichnung, a.Gesamt.ToString(Deutsch));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }
}
