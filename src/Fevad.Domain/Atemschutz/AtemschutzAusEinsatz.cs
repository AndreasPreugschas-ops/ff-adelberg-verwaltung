using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Atemschutz;

/// <summary>
/// Atemschutznachweise aus dem freigegebenen Einsatzbericht (V11, V12): je Geräteträger ein Eintrag „Einsatz“, der ab der Mindestdauer
/// als Einsatzübung zählt, und je exponiertem Teilnehmer ohne Atemschutzgerät ein Eintrag „Exposition“. Bei erneuter Freigabe werden
/// die Einträge abgeglichen; geänderte verlieren ihre Bestätigungen, nicht mehr passende werden entfernt.
/// </summary>
public static class AtemschutzAusEinsatz
{
    public sealed record Abgleich(IReadOnlyList<AtemschutzNachweis> Neu, IReadOnlyList<AtemschutzNachweis> Geaendert, IReadOnlyList<AtemschutzNachweis> Entfernt);

    public static Abgleich Abgleichen(Einsatz einsatz, IReadOnlyList<AtemschutzNachweis> vorhandene, int alsUebungAbMinuten)
    {
        var datum = Ortszeit.Datum(einsatz.Alarmiert);
        var ort = string.Join(", ", new[] { string.Join(" ", new[] { einsatz.Strasse, einsatz.Hausnummer }.Where(t => !string.IsNullOrWhiteSpace(t))), einsatz.Ort }
            .Where(t => !string.IsNullOrWhiteSpace(t)));
        var taetigkeit = string.Join(" – ", new[] { $"Einsatz {einsatz.Einsatznummer}", einsatz.Stichwort }.Where(t => !string.IsNullOrWhiteSpace(t)));

        var soll = new List<AtemschutzNachweis>();
        foreach (var t in einsatz.Teilnahmen)
        {
            var stoffe = t.Exposition == ExpositionStatus.Exponiert ? Kuerzen(t.Gefahrstoffe, 200) : null;
            if (t.Atemschutz)
            {
                soll.Add(new AtemschutzNachweis
                {
                    MitgliedId = t.MitgliedId, EinsatzId = einsatz.Id, Art = AtemschutzNachweisArt.Einsatz, Datum = datum,
                    Minuten = t.AtemschutzMinuten, PaGeraet = Kuerzen(t.PaGeraet, 50), Maske = Kuerzen(t.Maske, 50), Exposition = stoffe,
                    Ort = Kuerzen(ort, 500), Taetigkeit = Kuerzen(taetigkeit, 500),
                    GiltAlsEinsatzuebung = (t.AtemschutzMinuten ?? 0) >= alsUebungAbMinuten,
                });
            }
            else if (stoffe is not null)
            {
                soll.Add(new AtemschutzNachweis
                {
                    MitgliedId = t.MitgliedId, EinsatzId = einsatz.Id, Art = AtemschutzNachweisArt.Exposition, Datum = datum,
                    Exposition = stoffe, Ort = Kuerzen(ort, 500), Taetigkeit = Kuerzen(taetigkeit, 500),
                });
            }
        }

        var neu = new List<AtemschutzNachweis>();
        var geaendert = new List<AtemschutzNachweis>();
        var bleibt = new HashSet<AtemschutzNachweis>();
        foreach (var s in soll)
        {
            var alt = vorhandene.FirstOrDefault(v => v.MitgliedId == s.MitgliedId && v.Art == s.Art && !bleibt.Contains(v));
            if (alt is null)
            {
                neu.Add(s);
                continue;
            }

            bleibt.Add(alt);
            if (alt.Datum != s.Datum || alt.Minuten != s.Minuten || alt.PaGeraet != s.PaGeraet || alt.Maske != s.Maske || alt.Exposition != s.Exposition
                || alt.Ort != s.Ort || alt.Taetigkeit != s.Taetigkeit || alt.GiltAlsEinsatzuebung != s.GiltAlsEinsatzuebung)
            {
                (alt.Datum, alt.Minuten, alt.PaGeraet, alt.Maske, alt.Exposition, alt.Ort, alt.Taetigkeit, alt.GiltAlsEinsatzuebung) =
                    (s.Datum, s.Minuten, s.PaGeraet, s.Maske, s.Exposition, s.Ort, s.Taetigkeit, s.GiltAlsEinsatzuebung);
                alt.BestaetigungenZuruecksetzen();
                geaendert.Add(alt);
            }
        }

        return new Abgleich(neu, geaendert, vorhandene.Where(v => !bleibt.Contains(v)).ToList());
    }

    private static string? Kuerzen(string? text, int laenge) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim() is var t && t.Length > laenge ? t[..laenge] : text.Trim();
}
