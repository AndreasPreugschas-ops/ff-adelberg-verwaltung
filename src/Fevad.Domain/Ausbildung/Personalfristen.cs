using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Ausbildung;

/// <summary>Bereich einer Personalfrist, für Gruppierung und Rechte.</summary>
public enum Fristbereich
{
    /// <summary>Befristete Qualifikation, z. B. Fahrerlaubnis.</summary>
    [Display(Name = "Qualifikation")] Qualifikation,
    /// <summary>Wiederkehrender Nachweis, z. B. Unterweisung.</summary>
    [Display(Name = "Nachweis")] Nachweis,
    /// <summary>Atemschutz: Vorsorge, Übungen und Unterweisung.</summary>
    [Display(Name = "Atemschutz")] Atemschutz,
    /// <summary>Jugendfeuerwehr, z. B. fehlende Leistungsspange.</summary>
    [Display(Name = "Jugendfeuerwehr")] Jugend,
}

/// <summary>
/// Eine Frist eines Mitglieds. <see cref="FaelligAm"/> leer heißt: Nachweis fehlt ganz.
/// <see cref="Gesundheitsbezogen"/> markiert Einträge, die nur mit Atemschutz-Leserecht angezeigt werden dürfen.
/// <see cref="Schluessel"/> sagt, womit die Frist verlängert wird: ID der Qualifikation bzw. Nachweisart, beim Atemschutz
/// <see cref="Personalfristen.SchluesselVorsorge"/> oder die <see cref="AtemschutzNachweisArt"/>.
/// </summary>
/// <param name="MitgliedId">Schlüssel des Mitglieds.</param>
/// <param name="Mitglied">Anzeigename des Mitglieds.</param>
/// <param name="Bereich">Bereich der Frist.</param>
/// <param name="Bezeichnung">Was fällig ist, z. B. „Belastungsübung (Übungsanlage)“.</param>
/// <param name="FaelligAm">Tag, an dem die Frist abläuft; leer, wenn der Nachweis fehlt.</param>
/// <param name="Ampel">Bewertung zum Stichtag.</param>
/// <param name="Gesundheitsbezogen">Nur mit Atemschutz-Leserecht anzeigen.</param>
/// <param name="Schluessel">Womit die Frist verlängert wird.</param>
public sealed record Fristeintrag(
    Guid MitgliedId,
    string Mitglied,
    Fristbereich Bereich,
    string Bezeichnung,
    DateOnly? FaelligAm,
    Fristampel Ampel,
    bool Gesundheitsbezogen = false,
    string? Schluessel = null);

/// <summary>Alles, was für die Fristen eines Mitglieds gebraucht wird. Navigationen (Qualifikation) müssen geladen sein.</summary>
/// <param name="MitgliedId">Schlüssel des Mitglieds.</param>
/// <param name="Mitglied">Anzeigename des Mitglieds.</param>
/// <param name="Qualifikationen">Qualifikationsnachweise.</param>
/// <param name="Nachweise">Wiederkehrende Nachweise.</param>
/// <param name="Vorsorgen">Atemschutz-Vorsorgen (nur Status und Frist).</param>
/// <param name="AtemschutzNachweise">Atemschutz-Übungen, -Einsätze und -Unterweisungen.</param>
/// <param name="AtemschutzAktiv">Kennzeichen „aktiver Atemschutzgeräteträger“; nur dann werden Atemschutzfristen berechnet.</param>
public sealed record Fristdaten(
    Guid MitgliedId,
    string Mitglied,
    IReadOnlyList<QualifikationNachweis> Qualifikationen,
    IReadOnlyList<Nachweis> Nachweise,
    IReadOnlyList<AtemschutzVorsorge> Vorsorgen,
    IReadOnlyList<AtemschutzNachweis> AtemschutzNachweise,
    bool AtemschutzAktiv = false);

/// <summary>Warnfristen und Intervalle aus den Parametern.</summary>
/// <param name="QualifikationWarnungTage">Warnfrist für befristete Qualifikationen.</param>
/// <param name="NachweisWarnungTage">Warnfrist für wiederkehrende Nachweise.</param>
/// <param name="AtemschutzWarnungTage">Warnfrist für Atemschutzfristen.</param>
/// <param name="AtemschutzIntervallMonate">Intervall je jährlicher Atemschutzpflicht.</param>
public sealed record Fristeinstellungen(
    int QualifikationWarnungTage,
    int NachweisWarnungTage,
    int AtemschutzWarnungTage,
    IReadOnlyDictionary<AtemschutzNachweisArt, int> AtemschutzIntervallMonate)
{
    /// <summary>Intervall einer Atemschutzpflicht; ohne Eintrag 12 Monate.</summary>
    /// <param name="art">Art der Atemschutzpflicht.</param>
    /// <returns>Das Intervall in Monaten.</returns>
    public int Intervall(AtemschutzNachweisArt art) => AtemschutzIntervallMonate.GetValueOrDefault(art, 12);
}

/// <summary>Berechnet die Fristen der Personalverwaltung. Wird nie gespeichert.</summary>
public static class Personalfristen
{
    /// <summary>Bezeichnung der Vorsorgefrist in Listen.</summary>
    public const string VorsorgeBezeichnung = "Arbeitsmedizinische Vorsorge (G 26.3)";
    /// <summary>Schlüssel der Vorsorgefrist in <see cref="Fristeintrag.Schluessel"/>.</summary>
    public const string SchluesselVorsorge = "Vorsorge";

    /// <summary>
    /// Berechnet alle Fristen eines Mitglieds: befristete Qualifikationen (jüngster Nachweis je Qualifikation), Pflicht- und
    /// eingetragene Nachweise sowie bei aktiven Atemschutzgeräteträgern Vorsorge und jährliche Pflichten.
    /// </summary>
    /// <param name="daten">Daten des Mitglieds mit geladenen Navigationen.</param>
    /// <param name="nachweisarten">Alle Nachweisarten; nur aktive werden bewertet.</param>
    /// <param name="stichtag">Tag der Bewertung.</param>
    /// <param name="einstellungen">Warnfristen und Intervalle.</param>
    /// <returns>Die Fristeinträge; fehlende Pflichtnachweise erscheinen rot ohne Datum.</returns>
    public static IReadOnlyList<Fristeintrag> Berechnen(
        Fristdaten daten,
        IReadOnlyList<Nachweisart> nachweisarten,
        DateOnly stichtag,
        Fristeinstellungen einstellungen)
    {
        var eintraege = new List<Fristeintrag>();
        Fristeintrag Eintrag(Fristbereich bereich, string bezeichnung, DateOnly? faellig, int warnungTage, string schluessel, bool gesundheit = false) =>
            new(daten.MitgliedId, daten.Mitglied, bereich, bezeichnung, faellig,
                faellig is null ? Fristampel.Rot : Frist.Bewerten(faellig, stichtag, warnungTage), gesundheit, schluessel);

        // Je Qualifikation zählt nur der jüngste Nachweis; ältere sind durch Auffrischung erledigt.
        var qualifikationen = daten.Qualifikationen
            .Where(q => q.ErworbenAm <= stichtag)
            .GroupBy(q => q.QualifikationId)
            .Select(g => g.MaxBy(q => q.ErworbenAm)!)
            .ToList();

        foreach (var q in qualifikationen.Where(q => q.GueltigBis is not null))
        {
            eintraege.Add(Eintrag(Fristbereich.Qualifikation, q.Qualifikation?.Bezeichnung ?? "Qualifikation", q.GueltigBis, einstellungen.QualifikationWarnungTage, q.QualifikationId.ToString()));
        }

        var hatFahrerlaubnis = qualifikationen.Any(q => q.Qualifikation?.Art == QualifikationArt.Fahrerlaubnis);
        foreach (var art in nachweisarten.Where(a => a.Aktiv))
        {
            var letzter = daten.Nachweise
                .Where(n => n.NachweisartId == art.Id && n.Datum <= stichtag)
                .MaxBy(n => n.Datum);

            var pflicht = art.Pflicht switch
            {
                NachweisPflicht.AlleEinsatzkraefte => true,
                NachweisPflicht.MitFahrerlaubnis => hatFahrerlaubnis,
                NachweisPflicht.MitQualifikation => qualifikationen.Any(q => q.QualifikationId == art.QualifikationId),
                _ => false,
            };

            // Einmalige Nachweise laufen nicht ab: eingetragen ist erledigt, fehlend ist eine Frist ohne Datum.
            if (art.Einmalig && letzter is not null)
            {
                continue;
            }

            if (pflicht || letzter is not null)
            {
                eintraege.Add(Eintrag(Fristbereich.Nachweis, art.Bezeichnung, letzter?.GueltigBis, einstellungen.NachweisWarnungTage, art.Id.ToString(), art.Gesundheitsbezogen));
            }
        }

        // Überwacht wird, wer als aktiver Geräteträger gekennzeichnet ist, nicht schon jeder mit Lehrgang (Andi, 25.10.2026).
        if (daten.AtemschutzAktiv)
        {
            var vorsorge = daten.Vorsorgen.Where(v => v.UntersuchtAm <= stichtag).MaxBy(v => v.UntersuchtAm);
            var vorsorgeFaellig = vorsorge is null || vorsorge.Ergebnis == VorsorgeErgebnis.NichtGeeignet ? (DateOnly?)null : vorsorge.NaechsteFaellig;
            eintraege.Add(Eintrag(Fristbereich.Atemschutz, VorsorgeBezeichnung, vorsorgeFaellig, einstellungen.AtemschutzWarnungTage, SchluesselVorsorge, gesundheit: true));

            foreach (var art in AtemschutzStatus.JaehrlichePflichten)
            {
                var letzte = daten.AtemschutzNachweise
                    .Where(n => n.Erfuellt(art) && n.Datum <= stichtag)
                    .Select(n => (DateOnly?)n.Datum)
                    .Max();
                eintraege.Add(Eintrag(Fristbereich.Atemschutz, Bezeichnung(art), letzte?.AddMonths(einstellungen.Intervall(art)), einstellungen.AtemschutzWarnungTage, art.ToString()));
            }
        }

        return eintraege;
    }

    /// <summary>Bezeichnung einer Atemschutzpflicht für Listen.</summary>
    /// <param name="art">Art der Atemschutzpflicht.</param>
    /// <returns>Der Anzeigetext.</returns>
    public static string Bezeichnung(AtemschutzNachweisArt art) => art switch
    {
        AtemschutzNachweisArt.Belastungsuebung => "Belastungsübung (Übungsanlage)",
        AtemschutzNachweisArt.Einsatzuebung => "Einsatzübung",
        AtemschutzNachweisArt.Unterweisung => "Atemschutz-Unterweisung",
        _ => "Atemschutzeinsatz",
    };
}
