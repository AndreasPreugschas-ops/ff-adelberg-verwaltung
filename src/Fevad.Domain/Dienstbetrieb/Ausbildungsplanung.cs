using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Dienstbetrieb;

/// <summary>
/// Thema der Standortausbildung für die Übungsplanung (V53), z. B. FwDV 3 Einheiten im Lösch- und Hilfeleistungseinsatz. Ein Pflichtthema
/// soll im Jahresausbildungsplan mindestens einmal im Jahr vorkommen; der Katalog ist änderbar.
/// </summary>
public class Ausbildungsthema : Katalogeintrag
{
    /// <summary>Grundlage, z. B. „FwDV 3“ oder „DGUV Information 205-035“.</summary>
    public string? Grundlage { get; set; }

    /// <summary>Soll jedes Jahr im Ausbildungsplan vorkommen.</summary>
    public bool Pflicht { get; set; }
}

/// <summary>Stand eines Pflichtthemas im Jahresausbildungsplan.</summary>
public enum PlanStand
{
    /// <summary>Im Jahr kein Dienst mit diesem Thema.</summary>
    Offen,
    /// <summary>Nur geplante Dienste mit diesem Thema.</summary>
    Geplant,
    /// <summary>Mindestens ein durchgeführter Dienst mit diesem Thema.</summary>
    Durchgefuehrt,
}

/// <summary>Regeln des Jahresausbildungsplans und der Stundenkonten (V53).</summary>
public static class Ausbildungsplanung
{
    /// <summary>Dienstarten, die zur Ausbildung gehören und eine Übungsplanung haben.</summary>
    public static readonly IReadOnlyList<DienstArt> Ausbildungsarten = [DienstArt.Uebung, DienstArt.Unterricht, DienstArt.Unterweisung, DienstArt.Dienstsport];

    /// <summary>Gehört die Art zur Ausbildung.</summary>
    /// <param name="art">Die Dienstart.</param>
    /// <returns><c>true</c> für Übung, Unterricht, Unterweisung und Dienstsport.</returns>
    public static bool IstAusbildung(DienstArt art) => Ausbildungsarten.Contains(art);

    /// <summary>Stand eines Themas aus den Diensten des Jahres.</summary>
    /// <param name="status">Status der nicht abgesagten Dienste mit diesem Thema.</param>
    /// <returns>Durchgeführt, geplant oder offen.</returns>
    public static PlanStand Stand(IEnumerable<DienstStatus> status)
    {
        var liste = status.Where(s => s != DienstStatus.Abgesagt).ToList();
        return liste.Contains(DienstStatus.Durchgefuehrt) ? PlanStand.Durchgefuehrt
            : liste.Count > 0 ? PlanStand.Geplant
            : PlanStand.Offen;
    }

    /// <summary>Ampel zum Stand: grün durchgeführt, gelb nur geplant, rot offen.</summary>
    /// <param name="stand">Der Stand.</param>
    /// <returns>Die Ampel.</returns>
    public static Fristampel Ampel(PlanStand stand) => stand switch
    {
        PlanStand.Durchgefuehrt => Fristampel.Gruen,
        PlanStand.Geplant => Fristampel.Gelb,
        _ => Fristampel.Rot,
    };

    /// <summary>Stundenkonto Truppmann Teil 2 (FwDV 2: Ausbildung am Standort in zwei Jahren nach Truppmann Teil 1).</summary>
    /// <param name="Stunden">Bisher geleistete Stunden im Zeitraum.</param>
    /// <param name="Soll">Sollstunden (Parameter, 80).</param>
    /// <param name="Ende">Ende des Zeitraums: Truppmann Teil 1 plus Monate (Parameter, 24).</param>
    public sealed record Stundenkonto(decimal Stunden, decimal Soll, DateOnly Ende)
    {
        /// <summary>Noch fehlende Stunden; nie negativ.</summary>
        public decimal Fehlend => Math.Max(0, Soll - Stunden);

        /// <summary>Ampel: grün erreicht, rot Zeitraum abgelaufen ohne Soll, gelb in den letzten Monaten vor dem Ende.</summary>
        /// <param name="heute">Heutiges Datum.</param>
        /// <param name="warnMonate">Monate vor dem Ende, ab denen gelb gilt.</param>
        /// <returns>Die Ampel.</returns>
        public Fristampel Ampel(DateOnly heute, int warnMonate) =>
            Fehlend == 0 ? Fristampel.Gruen
            : heute > Ende ? Fristampel.Rot
            : heute >= Ende.AddMonths(-warnMonate) ? Fristampel.Gelb
            : Fristampel.Gruen;
    }

    /// <summary>Berechnet das Stundenkonto Truppmann Teil 2.</summary>
    /// <param name="truppmann1">Datum Truppmann Teil 1.</param>
    /// <param name="dienste">Datum und Stunden der Anwesenheiten bei Diensten, die für die Fortbildung zählen.</param>
    /// <param name="soll">Sollstunden.</param>
    /// <param name="monate">Zeitraum in Monaten.</param>
    /// <returns>Das Konto; gezählt wird ab Truppmann Teil 1 bis zum Ende des Zeitraums.</returns>
    public static Stundenkonto Truppmann2(DateOnly truppmann1, IEnumerable<(DateOnly Datum, decimal Stunden)> dienste, decimal soll, int monate)
    {
        var ende = truppmann1.AddMonths(monate);
        var stunden = dienste.Where(d => d.Datum >= truppmann1 && d.Datum <= ende).Sum(d => d.Stunden);
        return new(Math.Round(stunden, 1), soll, ende);
    }
}
