using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Dokumente;

/// <summary>Satzung, Dienstanweisung, Protokoll, Merkblatt, Formular, ...</summary>
public class Dokumentkategorie : Katalogeintrag
{
}

/// <summary>Wer ein Dokument sehen darf.</summary>
public enum DokumentSichtbarkeit
{
    /// <summary>Alle angemeldeten Benutzer.</summary>
    [Display(Name = "alle Benutzer")] Alle,

    /// <summary>Nur Führung und Schriftführer, z. B. Ausschussprotokolle.</summary>
    [Display(Name = "nur Führung")] Fuehrung,
}

/// <summary>
/// Abgelegtes Dokument. Der Inhalt liegt versioniert in <see cref="DokumentVersion"/>;
/// angezeigt und heruntergeladen wird immer die jüngste Version.
/// </summary>
public class Dokument : Entitaet
{
    /// <summary>Titel des Dokuments.</summary>
    public required string Titel { get; set; }
    /// <summary>Kurze Beschreibung.</summary>
    public string? Beschreibung { get; set; }

    /// <summary>Schlüssel der Kategorie.</summary>
    public Guid KategorieId { get; set; }
    /// <summary>Die Kategorie.</summary>
    public Dokumentkategorie? Kategorie { get; set; }

    /// <summary>Wer das Dokument sehen darf.</summary>
    public DokumentSichtbarkeit Sichtbarkeit { get; set; }

    /// <summary>Jede neue Version muss von allen Lesern bestätigt werden (z. B. Dienstanweisungen).</summary>
    public bool LesebestaetigungErforderlich { get; set; }

    /// <summary>Ab wann die Regelung gilt (Satzung, Dienstanweisung).</summary>
    public DateOnly? GueltigAb { get; set; }

    /// <summary>Aufgehobene Dokumente bleiben zum Nachlesen erhalten, erscheinen aber nicht mehr in der Übersicht.</summary>
    public DateOnly? AufgehobenAm { get; set; }

    /// <summary>Nächste Überprüfung, z. B. der Alarm- und Ausrückeordnung oder einer Gefährdungsbeurteilung (V57).</summary>
    public DateOnly? RevisionAm { get; set; }

    /// <summary>Tag, an dem der Stand dem Bürgermeister mitgeteilt wurde (Satzung, § 9 FwG; V57).</summary>
    public DateOnly? BuergermeisterMitgeteiltAm { get; set; }

    /// <summary>Alle hochgeladenen Fassungen.</summary>
    public List<DokumentVersion> Versionen { get; set; } = [];

    /// <summary>Die jüngste Version; erwartet geladene <see cref="Versionen"/>.</summary>
    public DokumentVersion? AktuelleVersion => Versionen.MaxBy(v => v.Nummer);

    /// <summary>Nummer für die nächste hochgeladene Version.</summary>
    public int NaechsteVersionsnummer => Versionen.Count == 0 ? 1 : Versionen.Max(v => v.Nummer) + 1;
}

/// <summary>Eine hochgeladene Fassung. Versionen werden nie überschrieben, nur ergänzt.</summary>
public class DokumentVersion : Entitaet
{
    /// <summary>Schlüssel des Dokuments.</summary>
    public Guid DokumentId { get; set; }
    /// <summary>Das Dokument.</summary>
    public Dokument? Dokument { get; set; }

    /// <summary>Laufende Versionsnummer ab 1.</summary>
    public int Nummer { get; set; }
    /// <summary>Bereinigter Dateiname beim Hochladen.</summary>
    public required string Dateiname { get; set; }
    /// <summary>MIME-Typ aus der Dateiendung (<see cref="Dateitypen.Inhaltstyp"/>).</summary>
    public required string Inhaltstyp { get; set; }
    /// <summary>Dateigröße in Byte.</summary>
    public long Groesse { get; set; }

    /// <summary>SHA-256 des Inhalts (hex), belegt, welche Fassung bestätigt wurde.</summary>
    public required string Pruefsumme { get; set; }

    /// <summary>Verweis auf den Inhalt in der Dateiablage.</summary>
    public Guid DateiId { get; set; }

    /// <summary>Was sich gegenüber der vorigen Version geändert hat.</summary>
    public string? Aenderungshinweis { get; set; }

    /// <summary>Lesebestätigungen zu dieser Version.</summary>
    public List<Lesebestaetigung> Lesebestaetigungen { get; set; } = [];
}

/// <summary>Inhalt einer Datei, getrennt von den Metadaten, damit Listen ihn nicht mitladen.</summary>
public class DokumentDatei : Entitaet
{
    /// <summary>Inhalt der Datei als Bytes; nicht im Änderungsprotokoll.</summary>
    [NichtProtokollieren]
    public required byte[] Inhalt { get; set; }
}

/// <summary>Ein Benutzer hat eine bestimmte Version gelesen und zur Kenntnis genommen.</summary>
public class Lesebestaetigung : Entitaet
{
    /// <summary>Schlüssel der gelesenen Version.</summary>
    public Guid DokumentVersionId { get; set; }
    /// <summary>Die gelesene Version.</summary>
    public DokumentVersion? DokumentVersion { get; set; }

    /// <summary>Konto (nicht Mitglied): bestätigen kann nur, wer sich anmeldet.</summary>
    public Guid BenutzerId { get; set; }

    /// <summary>Zeitpunkt der Bestätigung in UTC.</summary>
    public DateTimeOffset BestaetigtAm { get; set; }
}

/// <summary>Welche Dateien hochgeladen werden dürfen und mit welchem Inhaltstyp sie ausgeliefert werden.</summary>
public static class Dateitypen
{
    private static readonly Dictionary<string, string> Bekannte = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        [".doc"] = "application/msword",
        [".xls"] = "application/vnd.ms-excel",
        [".txt"] = "text/plain",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
    };

    /// <summary>Erlaubte Dateiendungen mit Punkt, z. B. „.pdf“.</summary>
    public static IEnumerable<string> Endungen => Bekannte.Keys;

    /// <summary>Inhaltstyp nach Dateiendung, nie nach Angabe des Browsers. Null bei nicht erlaubter Endung.</summary>
    /// <param name="dateiname">Dateiname mit Endung.</param>
    /// <returns>Den Inhaltstyp oder null.</returns>
    public static string? Inhaltstyp(string dateiname) =>
        Bekannte.GetValueOrDefault(Path.GetExtension(dateiname));

    /// <summary>Im Browser anzeigen statt herunterladen. Nur für Typen, die kein Skript ausführen können.</summary>
    /// <param name="inhaltstyp">MIME-Typ der Datei.</param>
    /// <returns>true für PDF, PNG, JPEG und Text.</returns>
    public static bool Anzeigbar(string inhaltstyp) =>
        inhaltstyp is "application/pdf" or "image/png" or "image/jpeg" or "text/plain";

    /// <summary>Entfernt Pfadanteile und Steuerzeichen aus einem hochgeladenen Dateinamen.</summary>
    /// <param name="dateiname">Dateiname, wie ihn der Browser geschickt hat.</param>
    /// <returns>Den bereinigten Namen, höchstens 200 Zeichen.</returns>
    public static string Bereinigen(string dateiname)
    {
        var name = Path.GetFileName(dateiname.Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c) && c is not ('"' or '/' or ':' or '*' or '?' or '<' or '>' or '|')).ToArray()).Trim();
        return name.Length > 200 ? name[^200..] : name;
    }
}
