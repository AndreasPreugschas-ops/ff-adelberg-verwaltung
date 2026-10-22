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
    public static string? Pruefen(DateOnly geburtsdatum, string zielAbteilungCode, DateOnly ab, Laufbahnregeln regeln)
    {
        var alter = Mitglied.Alter(geburtsdatum, ab);
        return zielAbteilungCode switch
        {
            AbteilungCode.Einsatz when alter < regeln.AufnahmeEinsatzAbAlter =>
                $"In die Einsatzabteilung erst ab {regeln.AufnahmeEinsatzAbAlter} Jahren (am {ab:dd.MM.yyyy}: {alter}).",
            AbteilungCode.Einsatz when alter >= regeln.AltersgrenzeEinsatzdienst =>
                $"Mit {regeln.AltersgrenzeEinsatzdienst} Jahren endet der Dienst in der Einsatzabteilung.",
            AbteilungCode.Jugend when alter >= regeln.JugendEndeAlter =>
                $"Die Jugendfeuerwehr endet mit {regeln.JugendEndeAlter} Jahren.",
            AbteilungCode.Alters when alter < regeln.AltersabteilungAbAlter =>
                $"In die Altersabteilung erst ab {regeln.AltersabteilungAbAlter} Jahren (am {ab:dd.MM.yyyy}: {alter}).",
            _ => null,
        };
    }

    public static Mitgliedschaft Uebertreten(Mitgliedschaft alt, Guid zielAbteilungId, MitgliedStatus status, DateOnly ab)
    {
        if (zielAbteilungId == alt.AbteilungId)
        {
            throw new InvalidOperationException("Das Mitglied ist schon in dieser Abteilung.");
        }

        Beenden(alt, ab.AddDays(-1), Austrittsgrund.Uebertritt);
        return Neu(alt.MitgliedId, zielAbteilungId, status, ab);
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
        return neu;
    }

    private static Mitgliedschaft Neu(Guid mitgliedId, Guid abteilungId, MitgliedStatus status, DateOnly ab) =>
        new() { MitgliedId = mitgliedId, AbteilungId = abteilungId, Status = status, GueltigAb = ab };
}
