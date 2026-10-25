using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Entschaedigung;

/// <summary>Stand einer Entschädigungsabrechnung.</summary>
public enum AbrechnungStatus
{
    /// <summary>Entwurf; wird bei jeder Berechnung neu aufgebaut.</summary>
    [Display(Name = "Entwurf")] Entwurf,
    /// <summary>Abgeschlossen und unveränderlich; die Posten gelten als abgerechnet.</summary>
    [Display(Name = "abgeschlossen")] Abgeschlossen,
}

/// <summary>
/// Entschädigungsabrechnung nach § 16 FwG für einen Zeitraum. Der Entwurf wird aus freigegebenen
/// Einsätzen, durchgeführten Diensten und Funktionen berechnet; abgeschlossen ist er unveränderlich
/// und geht als Export an die Gemeindekasse. Was in einer abgeschlossenen Abrechnung steht,
/// wird nie ein zweites Mal abgerechnet.
/// </summary>
public class Abrechnung : Entitaet
{
    /// <summary>Bezeichnung, z. B. „Entschädigung 1. Halbjahr 2027“.</summary>
    public required string Bezeichnung { get; set; }
    /// <summary>Erster Tag des Abrechnungszeitraums.</summary>
    public DateOnly Von { get; set; }
    /// <summary>Letzter Tag des Abrechnungszeitraums.</summary>
    public DateOnly Bis { get; set; }

    /// <summary>Entwurf oder abgeschlossen.</summary>
    public AbrechnungStatus Status { get; set; }
    /// <summary>Zeitpunkt des Abschlusses in UTC.</summary>
    public DateTimeOffset? AbgeschlossenAm { get; set; }
    /// <summary>Konto, das die Abrechnung abgeschlossen hat.</summary>
    public Guid? AbgeschlossenVon { get; set; }

    /// <summary>Hinweise der letzten Berechnung, je Zeile einer (z. B. fehlende Sätze, offene Berichte).</summary>
    public string? Hinweise { get; set; }

    /// <summary>Die berechneten Posten.</summary>
    public List<AbrechnungsPosten> Posten { get; set; } = [];

    /// <summary>Summe aller Posten in Euro.</summary>
    public decimal Summe => Posten.Sum(p => p.Betrag);

    /// <summary>Die Abrechnung ist abgeschlossen.</summary>
    public bool IstAbgeschlossen => Status == AbrechnungStatus.Abgeschlossen;

    /// <summary>Schließt die Abrechnung ab; danach ist sie unveränderlich.</summary>
    /// <param name="benutzerId">Konto, das abschließt.</param>
    /// <param name="zeitpunkt">Zeitpunkt des Abschlusses.</param>
    /// <exception cref="InvalidOperationException">Die Abrechnung ist schon abgeschlossen oder hat keine Posten.</exception>
    public void Abschliessen(Guid benutzerId, DateTimeOffset zeitpunkt)
    {
        if (IstAbgeschlossen)
        {
            throw new InvalidOperationException("Die Abrechnung ist bereits abgeschlossen.");
        }

        if (Posten.Count == 0)
        {
            throw new InvalidOperationException("Die Abrechnung enthält keine Posten.");
        }

        Status = AbrechnungStatus.Abgeschlossen;
        AbgeschlossenAm = zeitpunkt;
        AbgeschlossenVon = benutzerId;
    }

    /// <summary>Prüft den Abrechnungszeitraum.</summary>
    /// <param name="von">Erster Tag.</param>
    /// <param name="bis">Letzter Tag.</param>
    /// <returns>Fehlermeldungen; leer, wenn der Zeitraum höchstens ein Jahr umfasst und das Ende nicht vor dem Beginn liegt.</returns>
    public static IReadOnlyList<string> ZeitraumPruefen(DateOnly von, DateOnly bis)
    {
        var fehler = new List<string>();
        if (bis < von)
        {
            fehler.Add("Das Ende liegt vor dem Beginn.");
        }
        else if (bis.DayNumber - von.DayNumber > 366)
        {
            fehler.Add("Eine Abrechnung umfasst höchstens ein Jahr.");
        }

        return fehler;
    }
}

/// <summary>Herkunft eines Abrechnungspostens.</summary>
public enum PostenArt
{
    /// <summary>Teilnahme an einem freigegebenen Einsatz.</summary>
    [Display(Name = "Einsatz")] Einsatz,
    /// <summary>Anwesenheit bei einem durchgeführten Dienst.</summary>
    [Display(Name = "Dienst")] Dienst,
    /// <summary>Zeitanteil einer Jahrespauschale für eine Funktion.</summary>
    [Display(Name = "Funktionspauschale")] Funktionspauschale,
    /// <summary>Pauschale für die Leitung eines durchgeführten Dienstes, z. B. Übungsleiter (FwES Adelberg § 2 Abs. 3).</summary>
    [Display(Name = "Leitung")] Leitung,
    /// <summary>Bestandteil eines genehmigten Entschädigungsantrags (Lehrgang, mehrtägiger Einsatz, V32).</summary>
    [Display(Name = "Antrag")] Antrag,
}

/// <summary>Eine abgerechnete Teilnahme oder ein Pauschalenanteil. Satz und Betrag sind eingefroren.</summary>
public class AbrechnungsPosten : Entitaet
{
    /// <summary>Schlüssel der Abrechnung.</summary>
    public Guid AbrechnungId { get; set; }
    /// <summary>Die Abrechnung.</summary>
    public Abrechnung? Abrechnung { get; set; }

    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied, das die Entschädigung erhält.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Herkunft des Postens.</summary>
    public PostenArt Art { get; set; }

    /// <summary>"Einsatz" oder Name der <see cref="DienstArt"/>; bei Pauschalen der Funktionscode.</summary>
    public required string Vorgang { get; set; }

    /// <summary>Schlüssel der abgerechneten Einsatzteilnahme.</summary>
    public Guid? EinsatzTeilnahmeId { get; set; }
    /// <summary>Die abgerechnete Einsatzteilnahme.</summary>
    public EinsatzTeilnahme? EinsatzTeilnahme { get; set; }

    /// <summary>Schlüssel der abgerechneten Anwesenheit.</summary>
    public Guid? AnwesenheitId { get; set; }
    /// <summary>Die abgerechnete Anwesenheit.</summary>
    public Anwesenheit? Anwesenheit { get; set; }

    /// <summary>Schlüssel des geleiteten Dienstes bei Leitungspauschalen.</summary>
    public Guid? GeleiteterDienstId { get; set; }
    /// <summary>Der geleitete Dienst bei Leitungspauschalen.</summary>
    public Dienst? GeleiteterDienst { get; set; }

    /// <summary>Schlüssel des genehmigten Entschädigungsantrags.</summary>
    public Guid? EntschaedigungsantragId { get; set; }
    /// <summary>Der genehmigte Entschädigungsantrag.</summary>
    public Entschaedigungsantrag? Entschaedigungsantrag { get; set; }

    /// <summary>Steuerliche Einordnung des Postens (V34); bestimmt die Summen der Jahresbescheinigung.</summary>
    public Steuerart Steuerart { get; set; }

    /// <summary>Schlüssel der Funktionszuordnung bei Pauschalen.</summary>
    public Guid? FunktionZuordnungId { get; set; }
    /// <summary>Die Funktionszuordnung bei Pauschalen.</summary>
    public FunktionZuordnung? FunktionZuordnung { get; set; }

    /// <summary>Tag des Vorgangs (Ortszeit) bzw. erster Tag des Pauschalenanteils.</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Letzter Tag des Pauschalenanteils; sonst leer.</summary>
    public DateOnly? DatumBis { get; set; }

    /// <summary>Lesbarer Text für die Kämmerei, z. B. "Einsatz 2026-017 B 2".</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Tatsächliche Dauer in Minuten vor Rundung (nicht bei Pauschalen).</summary>
    public int? Minuten { get; set; }

    /// <summary>Abgerechnete Menge in <see cref="Einheit"/> nach Rundung.</summary>
    public decimal Menge { get; set; }
    /// <summary>Einheit der Menge.</summary>
    public Entschaedigungseinheit Einheit { get; set; }
    /// <summary>Satz je Einheit zum Zeitpunkt der Berechnung, bei Pauschalen der Jahressatz.</summary>
    public decimal Satz { get; set; }
    /// <summary>Betrag in Euro.</summary>
    public decimal Betrag { get; set; }
}
