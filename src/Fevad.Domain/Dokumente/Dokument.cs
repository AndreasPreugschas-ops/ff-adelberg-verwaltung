using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Dokumente;

/// <summary>Satzung, Dienstanweisung, Protokoll, Merkblatt, Formular, ...</summary>
public class Dokumentkategorie : Katalogeintrag
{
}

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
    public required string Titel { get; set; }
    public string? Beschreibung { get; set; }

    public Guid KategorieId { get; set; }
    public Dokumentkategorie? Kategorie { get; set; }

    public DokumentSichtbarkeit Sichtbarkeit { get; set; }

    /// <summary>Jede neue Version muss von allen Lesern bestätigt werden (z. B. Dienstanweisungen).</summary>
    public bool LesebestaetigungErforderlich { get; set; }

    /// <summary>Ab wann die Regelung gilt (Satzung, Dienstanweisung).</summary>
    public DateOnly? GueltigAb { get; set; }

    /// <summary>Aufgehobene Dokumente bleiben zum Nachlesen erhalten, erscheinen aber nicht mehr in der Übersicht.</summary>
    public DateOnly? AufgehobenAm { get; set; }

    public List<DokumentVersion> Versionen { get; set; } = [];

    public DokumentVersion? AktuelleVersion => Versionen.MaxBy(v => v.Nummer);

    public int NaechsteVersionsnummer => Versionen.Count == 0 ? 1 : Versionen.Max(v => v.Nummer) + 1;
}

/// <summary>Eine hochgeladene Fassung. Versionen werden nie überschrieben, nur ergänzt.</summary>
public class DokumentVersion : Entitaet
{
    public Guid DokumentId { get; set; }
    public Dokument? Dokument { get; set; }

    public int Nummer { get; set; }
    public required string Dateiname { get; set; }
    public required string Inhaltstyp { get; set; }
    public long Groesse { get; set; }

    /// <summary>SHA-256 des Inhalts (hex), belegt, welche Fassung bestätigt wurde.</summary>
    public required string Pruefsumme { get; set; }

    /// <summary>Verweis auf den Inhalt in der Dateiablage.</summary>
    public Guid DateiId { get; set; }

    public string? Aenderungshinweis { get; set; }

    public List<Lesebestaetigung> Lesebestaetigungen { get; set; } = [];
}

/// <summary>Inhalt einer Datei, getrennt von den Metadaten, damit Listen ihn nicht mitladen.</summary>
public class DokumentDatei : Entitaet
{
    [NichtProtokollieren]
    public required byte[] Inhalt { get; set; }
}

/// <summary>Ein Benutzer hat eine bestimmte Version gelesen und zur Kenntnis genommen.</summary>
public class Lesebestaetigung : Entitaet
{
    public Guid DokumentVersionId { get; set; }
    public DokumentVersion? DokumentVersion { get; set; }

    /// <summary>Konto (nicht Mitglied): bestätigen kann nur, wer sich anmeldet.</summary>
    public Guid BenutzerId { get; set; }

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

    public static IEnumerable<string> Endungen => Bekannte.Keys;

    /// <summary>Inhaltstyp nach Dateiendung, nie nach Angabe des Browsers. Null bei nicht erlaubter Endung.</summary>
    public static string? Inhaltstyp(string dateiname) =>
        Bekannte.GetValueOrDefault(Path.GetExtension(dateiname));

    /// <summary>Im Browser anzeigen statt herunterladen. Nur für Typen, die kein Skript ausführen können.</summary>
    public static bool Anzeigbar(string inhaltstyp) =>
        inhaltstyp is "application/pdf" or "image/png" or "image/jpeg" or "text/plain";

    /// <summary>Entfernt Pfadanteile und Steuerzeichen aus einem hochgeladenen Dateinamen.</summary>
    public static string Bereinigen(string dateiname)
    {
        var name = Path.GetFileName(dateiname.Replace('\\', '/'));
        name = new string(name.Where(c => !char.IsControl(c) && c is not ('"' or '/' or ':' or '*' or '?' or '<' or '>' or '|')).ToArray()).Trim();
        return name.Length > 200 ? name[^200..] : name;
    }
}
