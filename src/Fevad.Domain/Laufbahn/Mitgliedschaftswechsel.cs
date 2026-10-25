using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

/// <summary>
/// Übertritt, Ende der Probezeit und Ausscheiden. Eine Mitgliedschaft wird nie überschrieben: Der alte Abschnitt
/// endet am Vortag, der neue beginnt am Stichtag. So bleiben Dienstjahre und Personalstand nachvollziehbar.
/// Der neue Abschnitt wird nur zurückgegeben: Erst den alten speichern, dann den neuen, sonst greift der eindeutige
/// Index auf offene Mitgliedschaften je Abteilung.
/// </summary>
public static class Mitgliedschaftswechsel
{
    /// <summary>Prüft die Altersgrenzen der Zielabteilung; liefert eine Fehlermeldung oder null.</summary>
    /// <param name="geburtsdatum">Geburtsdatum des Mitglieds.</param>
    /// <param name="zielArt">Art der Zielabteilung.</param>
    /// <param name="ab">Tag des Wechsels.</param>
    /// <param name="regeln">Altersgrenzen.</param>
    /// <returns>Die Fehlermeldung oder null, wenn der Wechsel zulässig ist.</returns>
    public static string? Pruefen(DateOnly geburtsdatum, AbteilungArt zielArt, DateOnly ab, Laufbahnregeln regeln)
    {
        var alter = Mitglied.Alter(geburtsdatum, ab);
        return zielArt switch
        {
            AbteilungArt.Einsatz when alter < regeln.AufnahmeEinsatzAbAlter =>
                $"In die Einsatzabteilung erst ab {regeln.AufnahmeEinsatzAbAlter} Jahren (am {ab:dd.MM.yyyy}: {alter}).",
            AbteilungArt.Einsatz when alter >= regeln.AltersgrenzeEinsatzdienst =>
                $"Mit {regeln.AltersgrenzeEinsatzdienst} Jahren endet der Dienst in der Einsatzabteilung.",
            AbteilungArt.Jugend when alter >= regeln.JugendAufnahmeBisAlter =>
                $"In die Jugendfeuerwehr nur bis zum vollendeten {regeln.JugendAufnahmeBisAlter}. Lebensjahr (am {ab:dd.MM.yyyy}: {alter}).",
            AbteilungArt.Jugend when alter < regeln.JugendAufnahmeAbAlter =>
                $"In die Jugendfeuerwehr erst ab {regeln.JugendAufnahmeAbAlter} Jahren (am {ab:dd.MM.yyyy}: {alter}).",
            AbteilungArt.Jugend when alter >= regeln.JugendEndeAlter =>
                $"Die Jugendfeuerwehr endet mit {regeln.JugendEndeAlter} Jahren.",
            AbteilungArt.Kinder when alter < regeln.KinderAufnahmeAbAlter =>
                $"In die Kinderfeuerwehr erst ab {regeln.KinderAufnahmeAbAlter} Jahren (am {ab:dd.MM.yyyy}: {alter}).",
            AbteilungArt.Kinder when regeln.KinderEndeAlter > 0 && alter >= regeln.KinderEndeAlter =>
                $"Die Kinderfeuerwehr endet mit {regeln.KinderEndeAlter} Jahren.",
            AbteilungArt.Alters when alter < regeln.AltersabteilungAbAlter =>
                $"In die Altersabteilung erst ab {regeln.AltersabteilungAbAlter} Jahren (am {ab:dd.MM.yyyy}: {alter}).",
            _ => null,
        };
    }

    /// <summary>Beendet die bisherige Mitgliedschaft am Vortag mit Grund Übertritt und erzeugt die neue in der Zielabteilung.</summary>
    /// <param name="alt">Die bisherige Mitgliedschaft.</param>
    /// <param name="zielAbteilungId">Schlüssel der Zielabteilung.</param>
    /// <param name="status">Status in der neuen Abteilung, z. B. Probezeit.</param>
    /// <param name="ab">Erster Tag in der neuen Abteilung.</param>
    /// <returns>Die neue, noch nicht gespeicherte Mitgliedschaft.</returns>
    /// <exception cref="InvalidOperationException">Das Mitglied ist schon in der Zielabteilung.</exception>
    public static Mitgliedschaft Uebertreten(Mitgliedschaft alt, Guid zielAbteilungId, MitgliedStatus status, DateOnly ab)
    {
        if (zielAbteilungId == alt.AbteilungId)
        {
            throw new InvalidOperationException("Das Mitglied ist schon in dieser Abteilung.");
        }

        Beenden(alt, ab.AddDays(-1), Austrittsgrund.Uebertritt);
        return Neu(alt.MitgliedId, zielAbteilungId, status, ab);
    }

    /// <summary>
    /// Probezeit eines Abschnitts festlegen oder ändern (Verkürzung, Verlängerung). Ohne Datum gilt die Probezeit der Satzung.
    /// </summary>
    public static void ProbezeitAendern(Mitgliedschaft m, DateOnly? bis, string? bemerkung)
    {
        if (m.Status != MitgliedStatus.Probezeit && !(m.Status == MitgliedStatus.Befreit && m.ProbezeitBis is not null))
        {
            throw new InvalidOperationException("Die Mitgliedschaft ist nicht in der Probezeit.");
        }

        if (bis < m.GueltigAb)
        {
            throw new InvalidOperationException($"Das Ende der Probezeit liegt vor dem Beginn des Abschnitts ({m.GueltigAb:dd.MM.yyyy}).");
        }

        m.ProbezeitBis = bis;
        m.ProbezeitBemerkung = string.IsNullOrWhiteSpace(bemerkung) ? null : bemerkung.Trim();
    }

    /// <summary>Probezeit bestanden: gleiche Abteilung, neuer Abschnitt mit Status aktiv.</summary>
    public static Mitgliedschaft ProbezeitBestanden(Mitgliedschaft alt, DateOnly ab)
    {
        if (alt.Status != MitgliedStatus.Probezeit)
        {
            throw new InvalidOperationException("Die Mitgliedschaft ist nicht in der Probezeit.");
        }

        Beenden(alt, ab.AddDays(-1), null);
        return Weiter(alt, MitgliedStatus.Aktiv, ab);
    }

    /// <summary>
    /// Dienstbefreiung in derselben Abteilung ab <paramref name="von"/>. Mit <paramref name="bis"/> geht es danach mit dem
    /// bisherigen Status weiter. Liefert die neuen Abschnitte (Befreiung und gegebenenfalls Fortsetzung).
    /// </summary>
    public static IReadOnlyList<Mitgliedschaft> Befreien(Mitgliedschaft alt, DateOnly von, DateOnly? bis, Befreiungsgrund grund)
    {
        if (alt.Status == MitgliedStatus.Befreit)
        {
            throw new InvalidOperationException("Das Mitglied ist schon befreit.");
        }

        if (bis < von)
        {
            throw new InvalidOperationException("Das Ende der Befreiung liegt vor dem Beginn.");
        }

        Beenden(alt, von.AddDays(-1), null);
        var befreiung = Weiter(alt, MitgliedStatus.Befreit, von);
        befreiung.Befreiungsgrund = grund;
        if (bis is null)
        {
            return [befreiung];
        }

        befreiung.GueltigBis = bis;
        return [befreiung, Weiter(alt, alt.Status, bis.Value.AddDays(1))];
    }

    /// <summary>
    /// Befreiung endet: ab <paramref name="ab"/> weiter in derselben Abteilung mit dem Status vor der Befreiung
    /// (<paramref name="vorher"/>; wer in der Probezeit befreit wurde, bleibt in der Probezeit).
    /// </summary>
    public static Mitgliedschaft BefreiungBeenden(Mitgliedschaft alt, DateOnly ab, MitgliedStatus vorher)
    {
        if (alt.Status != MitgliedStatus.Befreit)
        {
            throw new InvalidOperationException("Die Mitgliedschaft ist nicht befreit.");
        }

        Beenden(alt, ab.AddDays(-1), null);
        return Weiter(alt, vorher is MitgliedStatus.Probezeit ? MitgliedStatus.Probezeit : MitgliedStatus.Aktiv, ab);
    }

    /// <summary>Beendet einen offenen Abschnitt. <paramref name="bis"/> ist der letzte Tag der Mitgliedschaft.</summary>
    public static void Beenden(Mitgliedschaft alt, DateOnly bis, Austrittsgrund? grund)
    {
        if (alt.GueltigBis is not null)
        {
            throw new InvalidOperationException("Die Mitgliedschaft ist bereits beendet.");
        }

        if (bis < alt.GueltigAb)
        {
            throw new InvalidOperationException($"Das Ende liegt vor dem Beginn der Mitgliedschaft ({alt.GueltigAb:dd.MM.yyyy}).");
        }

        alt.GueltigBis = bis;
        alt.Austrittsgrund = grund;
    }

    /// <summary>Neuer Abschnitt in derselben Abteilung; die Bestätigung des Kommandanten gilt weiter.</summary>
    private static Mitgliedschaft Weiter(Mitgliedschaft alt, MitgliedStatus status, DateOnly ab)
    {
        var neu = Neu(alt.MitgliedId, alt.AbteilungId, status, ab);
        neu.EinsatzdienstBestaetigt = alt.EinsatzdienstBestaetigt;

        // Eine Befreiung in der Probezeit verschiebt das eingetragene Ende nicht von selbst; das entscheidet der Ausschuss.
        if (status is MitgliedStatus.Probezeit or MitgliedStatus.Befreit)
        {
            neu.ProbezeitBis = alt.ProbezeitBis;
            neu.ProbezeitBemerkung = alt.ProbezeitBemerkung;
        }

        return neu;
    }

    private static Mitgliedschaft Neu(Guid mitgliedId, Guid abteilungId, MitgliedStatus status, DateOnly ab) =>
        new() { MitgliedId = mitgliedId, AbteilungId = abteilungId, Status = status, GueltigAb = ab };
}
