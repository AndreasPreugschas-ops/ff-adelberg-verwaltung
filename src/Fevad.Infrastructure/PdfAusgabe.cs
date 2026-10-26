using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Fevad.Infrastructure;

/// <summary>
/// Einheitliche Erzeugung aller PDF-Ausgaben (V66): als getaggtes PDF nach PDF/UA-1 mit deutscher Dokumentsprache, damit Screenreader
/// Überschriften, Absätze und Tabellen erkennen.
/// </summary>
public static class PdfAusgabe
{
    /// <summary>Erzeugt das PDF.</summary>
    /// <param name="dokument">Das QuestPDF-Dokument.</param>
    /// <param name="archiv">Zusätzlich PDF/A-3b für die Archivierung.</param>
    /// <returns>Die Bytes des PDF.</returns>
    public static byte[] AlsPdf(this Document dokument, bool archiv = false) =>
        dokument
            .WithMetadata(new DocumentMetadata { Language = "de-DE", Title = "FEVAD – Freiwillige Feuerwehr Adelberg", Author = "Freiwillige Feuerwehr Adelberg", Creator = "FEVAD" })
            .WithSettings(new DocumentSettings
            {
                PDFUA_Conformance = PDFUA_Conformance.PDFUA_1,
                PDFA_Conformance = archiv ? PDFA_Conformance.PDFA_3B : PDFA_Conformance.None,
            })
            .GeneratePdf();
}
