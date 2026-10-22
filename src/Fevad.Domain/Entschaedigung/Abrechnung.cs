using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Entschaedigung;

public enum AbrechnungStatus
{
    [Display(Name = "Entwurf")] Entwurf,
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
    public required string Bezeichnung { get; set; }
    public DateOnly Von { get; set; }
    public DateOnly Bis { get; set; }

    public AbrechnungStatus Status { get; set; }
    public DateTimeOffset? AbgeschlossenAm { get; set; }
    public Guid? AbgeschlossenVon { get; set; }

    /// <summary>Hinweise der letzten Berechnung, je Zeile einer (z. B. fehlende Sätze, offene Berichte).</summary>
    public string? Hinweise { get; set; }

    public List<AbrechnungsPosten> Posten { get; set; } = [];

    public decimal Summe => Posten.Sum(p => p.Betrag);

    public bool IstAbgeschlossen => Status == AbrechnungStatus.Abgeschlossen;

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

public enum PostenArt
{
    [Display(Name = "Einsatz")] Einsatz,
    [Display(Name = "Dienst")] Dienst,
    [Display(Name = "Funktionspauschale")] Funktionspauschale,
}

/// <summary>Eine abgerechnete Teilnahme oder ein Pauschalenanteil. Satz und Betrag sind eingefroren.</summary>
public class AbrechnungsPosten : Entitaet
{
    public Guid AbrechnungId { get; set; }
    public Abrechnung? Abrechnung { get; set; }

    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public PostenArt Art { get; set; }

    /// <summary>"Einsatz" oder Name der <see cref="DienstArt"/>; bei Pauschalen der Funktionscode.</summary>
    public required string Vorgang { get; set; }

    public Guid? EinsatzTeilnahmeId { get; set; }
    public EinsatzTeilnahme? EinsatzTeilnahme { get; set; }

    public Guid? AnwesenheitId { get; set; }
    public Anwesenheit? Anwesenheit { get; set; }

    public Guid? FunktionZuordnungId { get; set; }
    public FunktionZuordnung? FunktionZuordnung { get; set; }

    /// <summary>Tag des Vorgangs (Ortszeit) bzw. erster Tag des Pauschalenanteils.</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Letzter Tag des Pauschalenanteils; sonst leer.</summary>
    public DateOnly? DatumBis { get; set; }

    /// <summary>Lesbarer Text für die Kämmerei, z. B. "Einsatz 2026-017 B 2".</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Tatsächliche Dauer in Minuten vor Rundung (nicht bei Pauschalen).</summary>
    public int? Minuten { get; set; }

    public decimal Menge { get; set; }
    public Entschaedigungseinheit Einheit { get; set; }
    public decimal Satz { get; set; }
    public decimal Betrag { get; set; }
}
