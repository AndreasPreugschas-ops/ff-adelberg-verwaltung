using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Fevad.Domain.Gemeinsam;
using static Fevad.Infrastructure.Auswertungen.JahresberichtPdf;

namespace Fevad.Infrastructure.Auswertungen;

/// <summary>
/// Jahresbericht als Word-Datei zum Weiterschreiben (F23): dieselben Zahlen wie das PDF, mit Überschriften als Formatvorlagen und Platz für den
/// Bericht des Kommandanten. Das fertige PDF bleibt die Fassung für Gemeinde und Jahresmeldung.
/// </summary>
public static class JahresberichtDocx
{
    private const string Rot = "A50D26";

    /// <summary>Erzeugt den Jahresbericht als DOCX.</summary>
    /// <param name="bericht">Die berechnete Jahresstatistik.</param>
    /// <param name="organisation">Name der Feuerwehr.</param>
    /// <param name="erstellt">Zeitpunkt der Erstellung.</param>
    /// <returns>Die Datei als Bytes.</returns>
    public static byte[] Erzeugen(Jahresstatistik.Bericht bericht, string organisation, DateTimeOffset erstellt)
    {
        using var strom = new MemoryStream();
        using (var dokument = WordprocessingDocument.Create(strom, WordprocessingDocumentType.Document))
        {
            var haupt = dokument.AddMainDocumentPart();
            Formatvorlagen(haupt);
            var body = new Body();
            haupt.Document = new Document(body);

            body.Append(Absatz(organisation, grau: true));
            body.Append(Ueberschrift($"Jahresbericht {bericht.Jahr}", "Title"));
            body.Append(Absatz($"Einsätze und Dienste im Kalenderjahr {bericht.Jahr}, Personalstand zum {Datum(bericht.Stichtag)}", grau: true));
            foreach (var hinweis in bericht.Hinweise)
            {
                body.Append(Absatz("Hinweis: " + hinweis, grau: true));
            }

            body.Append(Ueberschrift("Bericht des Kommandanten", "Heading1"));
            body.Append(Absatz("[Hier den Bericht für die Hauptversammlung schreiben.]", grau: true));

            var e = bericht.Einsaetze;
            body.Append(Ueberschrift("Einsätze", "Heading1"));
            body.Append(Tabelle(["Kategorie", "Einsätze", "Personenstunden"],
                [.. e.NachKategorie.Select(k => new[] { k.Bezeichnung, Ganz(k.Anzahl), Zahl(k.Personenstunden) }), ["Gesamt", Ganz(e.Freigegeben), Zahl(e.Personenstunden)]]));
            body.Append(Tabelle(["Kennzahl", "Wert"],
            [
                ["Gerettete Personen", Ganz(e.Gerettete)],
                ["Verletzte Personen", Ganz(e.Verletzte)],
                ["Tote", Ganz(e.Tote)],
                ["Verletzte Feuerwehrangehörige", Ganz(e.VerletzteFeuerwehr)],
                ["Überörtliche Einsätze", Ganz(e.Ueberoertlich)],
                ["Teilnehmer je Einsatz (Durchschnitt)", Zahl(e.DurchschnittTeilnehmer)],
                ["Ausrückezeit (Median)", e.AusrueckzeitMedianMinuten is { } m ? $"{m:0.0} min".Replace('.', ',') : "–"],
                ["Alarme werktags 06:00–18:00 Uhr", Ganz(e.WerktagsTagsueber)],
            ]));

            var d = bericht.Dienste;
            body.Append(Ueberschrift("Übungs- und Dienstbetrieb", "Heading1"));
            body.Append(Tabelle(["Art", "Anzahl", "Stunden", "Teilnahmen", "Personenstunden"],
                [.. d.NachArt.Select(a => new[] { DienstArtText(a.Art), Ganz(a.Anzahl), Zahl(a.Stunden), Ganz(a.Teilnahmen), Zahl(a.Personenstunden) })]));
            body.Append(Absatz($"Standortfortbildung: {d.FortbildungZielErreicht} von {d.FortbildungBasis} Angehörigen der Einsatzabteilung haben mindestens {d.FortbildungZielStunden:0.#} Stunden erreicht."));

            var p = bericht.Personal;
            body.Append(Ueberschrift($"Personalstand zum {Datum(bericht.Stichtag)}", "Heading1"));
            body.Append(Tabelle(["Abteilung", "Gesamt", "weiblich", "aktiv", "Probezeit", "befreit", "Zugänge", "Abgänge"],
                [.. p.Abteilungen.Select(a => new[] { a.Bezeichnung, Ganz(a.Gesamt), Ganz(a.Weiblich), Ganz(a.Aktiv), Ganz(a.Probezeit), Ganz(a.Befreit), Ganz(a.Zugaenge), Ganz(a.Abgaenge) })]));
            body.Append(Absatz($"In den nächsten fünf Jahren erreichen {p.ErreichenAltersgrenzeInFuenfJahren} Angehörige der Einsatzabteilung die Altersgrenze von {p.Altersgrenze} Jahren."));

            Liste(body, "Beförderungen", ["Datum", "Name", "Dienstgrad"], bericht.Befoerderungen.Select(b => new[] { Datum(b.Datum), b.Name, b.Dienstgrad }));
            Liste(body, "Ehrungen", ["Datum", "Name", "Ehrung"], bericht.Ehrungen.Select(x => new[] { Datum(x.Datum), x.Name, x.Ehrung }),
                "Mit Namen; für die Hauptversammlung und die Gemeinde, nicht zur Veröffentlichung ohne Rückfrage.");
            Liste(body, "Aus- und Fortbildung", ["Lehrgang / Qualifikation", "Abschlüsse"], bericht.Qualifikationen.Select(q => new[] { q.Text, Ganz(q.Anzahl) }));
            Liste(body, "Fahrzeuge im Einsatz", ["Fahrzeug", "Typ", "Einsätze", "Kilometer"], bericht.Fahrzeuge.Select(f => new[] { f.Funkrufname, f.Typ, Ganz(f.Einsaetze), Ganz(f.Kilometer) }));

            if (bericht.Chronik.Count > 0)
            {
                body.Append(Ueberschrift("Aus der Chronik", "Heading1"));
                foreach (var c in bericht.Chronik)
                {
                    body.Append(new Paragraph(Lauf($"{Datum(c.Datum)} {c.Titel}", fett: true), Lauf(c.Text is null ? "" : " " + c.Text)));
                }
            }

            var lokal = TimeZoneInfo.ConvertTime(erstellt, Ortszeit.Berlin);
            body.Append(Absatz($"Erstellt am {lokal:dd.MM.yyyy HH:mm} Uhr mit FEVAD", grau: true));
            body.Append(new SectionProperties(new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134, Header = 709, Footer = 709 }));
            haupt.Document.Save();
        }

        return strom.ToArray();
    }

    private static void Liste(Body body, string titel, IReadOnlyList<string> kopf, IEnumerable<string[]> zeilen, string? hinweis = null)
    {
        var liste = zeilen.ToList();
        if (liste.Count == 0)
        {
            return;
        }

        body.Append(Ueberschrift(titel, "Heading1"));
        if (hinweis is not null)
        {
            body.Append(Absatz(hinweis, grau: true));
        }

        body.Append(Tabelle(kopf, liste));
    }

    private static Paragraph Ueberschrift(string text, string vorlage) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = vorlage }), Lauf(text));

    private static Paragraph Absatz(string text, bool grau = false) => new(Lauf(text, grau: grau));

    private static Run Lauf(string text, bool fett = false, bool grau = false)
    {
        var eigenschaften = new RunProperties();
        if (fett)
        {
            eigenschaften.Append(new Bold());
        }

        if (grau)
        {
            eigenschaften.Append(new Color { Val = "666666" });
        }

        return new Run(eigenschaften, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static OpenXmlElement[] Tabelle(IReadOnlyList<string> kopf, IReadOnlyList<string[]> zeilen)
    {
        static BorderType Linie<T>() where T : BorderType, new() => new T { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" };
        var tabelle = new Table(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(Linie<TopBorder>(), Linie<BottomBorder>(), Linie<LeftBorder>(), Linie<RightBorder>(), Linie<InsideHorizontalBorder>(), Linie<InsideVerticalBorder>())));
        tabelle.Append(Zeile(kopf, kopfzeile: true));
        foreach (var z in zeilen)
        {
            tabelle.Append(Zeile(z, kopfzeile: false));
        }

        // Ein leerer Absatz danach, sonst verschmilzt Word zwei aufeinanderfolgende Tabellen.
        return [tabelle, new Paragraph()];

        static TableRow Zeile(IReadOnlyList<string> zellen, bool kopfzeile)
        {
            var zeile = new TableRow();
            if (kopfzeile)
            {
                zeile.Append(new TableRowProperties(new TableHeader()));
            }

            for (var i = 0; i < zellen.Count; i++)
            {
                var absatz = new Paragraph(Lauf(zellen[i], fett: kopfzeile));
                if (i > 0)
                {
                    absatz.PrependChild(new ParagraphProperties(new Justification { Val = JustificationValues.Right }));
                }

                var zelle = new TableCell(absatz);
                if (kopfzeile)
                {
                    zelle.PrependChild(new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = "EEEEEE" }));
                }

                zeile.Append(zelle);
            }

            return zeile;
        }
    }

    private static void Formatvorlagen(MainDocumentPart haupt)
    {
        var teil = haupt.AddNewPart<StyleDefinitionsPart>();
        Style Vorlage(string id, string name, int halbpunkte, bool fett, string? farbe, int? gliederung)
        {
            var lauf = new StyleRunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" }, new FontSize { Val = halbpunkte.ToString() });
            if (fett)
            {
                lauf.Append(new Bold());
            }

            if (farbe is not null)
            {
                lauf.Append(new Color { Val = farbe });
            }

            var absatz = new StyleParagraphProperties(new SpacingBetweenLines { Before = gliederung is null ? "0" : "240", After = "120" });
            if (gliederung is { } ebene)
            {
                absatz.Append(new KeepNext(), new OutlineLevel { Val = ebene });
            }

            return new Style(new StyleName { Val = name }, new PrimaryStyle(), absatz, lauf) { Type = StyleValues.Paragraph, StyleId = id, Default = id == "Normal" ? true : null };
        }

        teil.Styles = new Styles(
            Vorlage("Normal", "Normal", 21, false, null, null),
            Vorlage("Title", "Title", 40, true, Rot, null),
            Vorlage("Heading1", "heading 1", 28, true, Rot, 0));
        teil.Styles.Save();
    }
}
