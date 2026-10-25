using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Dienstbetrieb;

/// <summary>Grund einer Abwesenheit, bewusst grob und ohne Diagnose.</summary>
public enum AbwesenheitArt
{
    /// <summary>Urlaub oder Reise.</summary>
    [Display(Name = "Urlaub")] Urlaub,
    /// <summary>Beruf, Schicht, Montage oder Studium.</summary>
    [Display(Name = "Beruf oder Ausbildung")] Beruf,
    /// <summary>Krankheit oder Verletzung; nur die Art, nie eine Diagnose.</summary>
    [Display(Name = "Krankheit")] Krankheit,
    /// <summary>Familiäre Gründe.</summary>
    [Display(Name = "Familie")] Familie,
    /// <summary>Sonstiger Grund.</summary>
    [Display(Name = "Sonstiges")] Sonstiges,
}

/// <summary>
/// Vom Mitglied gemeldete Abwesenheit (V37, Satzung § 5 Abs. 6). Alle Dienste im Zeitraum gelten als entschuldigt;
/// ab einer im Parameter festgelegten Dauer ist sie dem Kommandanten anzuzeigen und erscheint dort, bis er sie zur Kenntnis nimmt.
/// </summary>
public class Abwesenheit : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Erster Tag der Abwesenheit.</summary>
    public DateOnly Von { get; set; }
    /// <summary>Letzter Tag der Abwesenheit.</summary>
    public DateOnly Bis { get; set; }

    /// <summary>Grund ohne Diagnose; sehen nur das Mitglied und wer Abwesenheiten lesen darf.</summary>
    public AbwesenheitArt Art { get; set; }

    /// <summary>Freiwillige Bemerkung des Mitglieds, z. B. „per Handy erreichbar“. Keine Gesundheitsangaben.</summary>
    [NichtProtokollieren]
    public string? Bemerkung { get; set; }

    /// <summary>Zeitpunkt der Meldung in UTC.</summary>
    public DateTimeOffset GemeldetAm { get; set; }

    /// <summary>Zeitpunkt, zu dem der Kommandant die Anzeige zur Kenntnis genommen hat (UTC).</summary>
    public DateTimeOffset? KenntnisGenommenAm { get; set; }
    /// <summary>Konto, das die Anzeige zur Kenntnis genommen hat.</summary>
    public Guid? KenntnisGenommenVon { get; set; }

    /// <summary>Dauer in Kalendertagen, beide Grenzen eingeschlossen.</summary>
    public int Tage => Bis.DayNumber - Von.DayNumber + 1;

    /// <summary>Der Tag liegt im Zeitraum der Abwesenheit.</summary>
    /// <param name="tag">Kalendertag in Ortszeit.</param>
    /// <returns>true, wenn <paramref name="tag"/> zwischen <see cref="Von"/> und <see cref="Bis"/> liegt.</returns>
    public bool Umfasst(DateOnly tag) => tag >= Von && tag <= Bis;

    /// <summary>Die Abwesenheit ist dem Kommandanten anzuzeigen (Satzung § 5 Abs. 6).</summary>
    /// <param name="anzeigeAbTagen">Mindestdauer in Tagen aus dem Parameter <see cref="ParameterSchluessel.AbwesenheitAnzeigeAbTagen"/>.</param>
    /// <returns>true ab der Mindestdauer; 0 schaltet die Anzeige ab.</returns>
    public bool Anzeigepflichtig(int anzeigeAbTagen) => anzeigeAbTagen > 0 && Tage >= anzeigeAbTagen;

    /// <summary>Prüft die Eingaben einer Meldung. Rückwirkend geht es nur so weit, wie man sich nach der Satzung noch entschuldigen kann.</summary>
    /// <param name="von">Erster Tag.</param>
    /// <param name="bis">Letzter Tag.</param>
    /// <param name="heute">Heutiger Tag in Ortszeit.</param>
    /// <param name="nachfristTage">So viele Tage nach einem Dienst kann man sich noch entschuldigen (Parameter <see cref="ParameterSchluessel.DienstEntschuldigungNachfristTage"/>).</param>
    /// <returns>Feld und Meldung des ersten Fehlers oder <c>null</c>, wenn alles passt.</returns>
    public static (string Feld, string Meldung)? Pruefen(DateOnly von, DateOnly bis, DateOnly heute, int nachfristTage)
    {
        if (bis < von)
        {
            return (nameof(Bis), "Das Ende liegt vor dem Beginn.");
        }

        var fruehestens = heute.AddDays(-nachfristTage);
        if (von < fruehestens)
        {
            return (nameof(Von), $"Rückwirkend geht es nur ab dem {fruehestens:dd.MM.yyyy}.");
        }

        return null;
    }
}
