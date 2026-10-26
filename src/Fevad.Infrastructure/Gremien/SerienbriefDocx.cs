using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Auswertungen;

namespace Fevad.Infrastructure.Gremien;

/// <summary>Serienbrief als Word-Datei zum Nachbearbeiten (V56): je Empfänger ein Abschnitt, getrennt durch Seitenumbrüche.</summary>
public static class SerienbriefDocx
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Erzeugt die Word-Datei.</summary>
    /// <param name="feuerwehr">Name der Feuerwehr.</param>
    /// <param name="form">Brief oder Urkunde.</param>
    /// <param name="schreiben">Die gefüllten Schreiben.</param>
    /// <param name="datum">Datum des Schreibens.</param>
    /// <param name="unterzeichner">Name unter dem Schreiben.</param>
    /// <returns>Die Datei als Bytes.</returns>
    public static byte[] Erzeugen(string feuerwehr, VorlageForm form, IReadOnlyList<Serienbriefe.Schreiben> schreiben, DateOnly datum, string? unterzeichner)
    {
        using var strom = new MemoryStream();
        using (var dokument = WordprocessingDocument.Create(strom, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var haupt = dokument.AddMainDocumentPart();
            JahresberichtDocx.Formatvorlagen(haupt);
            var body = new Body();
            haupt.Document = new Document(body);
            var tag = datum.ToString("dd.MM.yyyy", Deutsch);
            for (var i = 0; i < schreiben.Count; i++)
            {
                var s = schreiben[i];
                if (i > 0)
                {
                    body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                }

                var zentriert = form == VorlageForm.Urkunde;
                body.Append(Ausrichten(JahresberichtDocx.Absatz(feuerwehr, grau: true), zentriert));
                if (!zentriert)
                {
                    foreach (var zeile in s.An.Anschrift)
                    {
                        body.Append(JahresberichtDocx.Absatz(zeile));
                    }

                    body.Append(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Right }), JahresberichtDocx.Lauf(tag)));
                }

                body.Append(Ausrichten(JahresberichtDocx.Ueberschrift(s.Betreff, zentriert ? "Title" : "Heading1"), zentriert));
                foreach (var absatz in SerienbriefPdf.Absaetze(s.Text))
                {
                    var p = new Paragraph();
                    var zeilen = absatz.Split('\n');
                    for (var z = 0; z < zeilen.Length; z++)
                    {
                        if (z > 0)
                        {
                            p.Append(new Run(new Break()));
                        }

                        p.Append(JahresberichtDocx.Lauf(zeilen[z]));
                    }

                    body.Append(Ausrichten(p, zentriert));
                }

                if (zentriert)
                {
                    body.Append(JahresberichtDocx.Absatz(tag));
                }

                body.Append(JahresberichtDocx.Absatz(""));
                body.Append(JahresberichtDocx.Absatz("______________________________"));
                body.Append(JahresberichtDocx.Absatz(unterzeichner ?? (zentriert ? "Kommandant" : feuerwehr)));
            }

            haupt.Document.Save();
        }

        return strom.ToArray();
    }

    private static Paragraph Ausrichten(Paragraph p, bool zentriert)
    {
        if (zentriert)
        {
            var eigenschaften = p.ParagraphProperties ?? p.PrependChild(new ParagraphProperties());
            eigenschaften.Append(new Justification { Val = JustificationValues.Center });
        }

        return p;
    }
}
