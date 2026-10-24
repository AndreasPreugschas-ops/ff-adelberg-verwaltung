using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Ausbildung;

public enum Fristbereich
{
    [Display(Name = "Qualifikation")] Qualifikation,
    [Display(Name = "Nachweis")] Nachweis,
    [Display(Name = "Atemschutz")] Atemschutz,
    [Display(Name = "Jugendfeuerwehr")] Jugend,
}

/// <summary>
/// Eine Frist eines Mitglieds. <see cref="FaelligAm"/> leer heißt: Nachweis fehlt ganz.
/// <see cref="Gesundheitsbezogen"/> markiert Einträge, die nur mit Atemschutz-Leserecht angezeigt werden dürfen.
/// <see cref="Schluessel"/> sagt, womit die Frist verlängert wird: ID der Qualifikation bzw. Nachweisart, beim Atemschutz
/// <see cref="Personalfristen.SchluesselVorsorge"/> oder die <see cref="AtemschutzNachweisArt"/>.
/// </summary>
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
public sealed record Fristdaten(
    Guid MitgliedId,
    string Mitglied,
    IReadOnlyList<QualifikationNachweis> Qualifikationen,
    IReadOnlyList<Nachweis> Nachweise,
    IReadOnlyList<AtemschutzVorsorge> Vorsorgen,
    IReadOnlyList<AtemschutzNachweis> AtemschutzNachweise);

/// <summary>Warnfristen und Intervalle aus den Parametern.</summary>
public sealed record Fristeinstellungen(
    int QualifikationWarnungTage,
    int NachweisWarnungTage,
    int AtemschutzWarnungTage,
    IReadOnlyDictionary<AtemschutzNachweisArt, int> AtemschutzIntervallMonate)
{
    public int Intervall(AtemschutzNachweisArt art) => AtemschutzIntervallMonate.GetValueOrDefault(art, 12);
}

/// <summary>Berechnet die Fristen der Personalverwaltung. Wird nie gespeichert.</summary>
public static class Personalfristen
{
    public const string VorsorgeBezeichnung = "Arbeitsmedizinische Vorsorge (G 26.3)";
    public const string SchluesselVorsorge = "Vorsorge";

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

        if (qualifikationen.Any(q => q.Qualifikation?.Code == Qualifikation.CodeAtemschutz))
        {
            var vorsorge = daten.Vorsorgen.Where(v => v.UntersuchtAm <= stichtag).MaxBy(v => v.UntersuchtAm);
            var vorsorgeFaellig = vorsorge is null || vorsorge.Ergebnis == VorsorgeErgebnis.NichtGeeignet ? (DateOnly?)null : vorsorge.NaechsteFaellig;
            eintraege.Add(Eintrag(Fristbereich.Atemschutz, VorsorgeBezeichnung, vorsorgeFaellig, einstellungen.AtemschutzWarnungTage, SchluesselVorsorge, gesundheit: true));

            foreach (var art in AtemschutzStatus.JaehrlichePflichten)
            {
                var letzte = daten.AtemschutzNachweise
                    .Where(n => n.Art == art && n.Datum <= stichtag)
                    .Select(n => (DateOnly?)n.Datum)
                    .Max();
                eintraege.Add(Eintrag(Fristbereich.Atemschutz, Bezeichnung(art), letzte?.AddMonths(einstellungen.Intervall(art)), einstellungen.AtemschutzWarnungTage, art.ToString()));
            }
        }

        return eintraege;
    }

    public static string Bezeichnung(AtemschutzNachweisArt art) => art switch
    {
        AtemschutzNachweisArt.Belastungsuebung => "Belastungsübung (Übungsanlage)",
        AtemschutzNachweisArt.Einsatzuebung => "Einsatzübung",
        AtemschutzNachweisArt.Unterweisung => "Atemschutz-Unterweisung",
        _ => "Atemschutzeinsatz",
    };
}
