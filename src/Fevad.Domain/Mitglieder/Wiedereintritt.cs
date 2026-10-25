namespace Fevad.Domain.Mitglieder;

/// <summary>Regeln für die Wiederaufnahme eines früheren Mitglieds über einen Aufnahmeantrag.</summary>
public static class Wiedereintritt
{
    /// <summary>
    /// Warum das Mitglied zum <paramref name="ab"/> nicht wieder aufgenommen werden kann, oder null. Erwartet geladene Mitgliedschaften.
    /// Anonymisierte Restdatensätze (Löschkonzept L7) taugen nicht mehr: dann ist es eine neue Aufnahme.
    /// </summary>
    /// <param name="mitglied">Das frühere Mitglied mit geladenen Mitgliedschaften.</param>
    /// <param name="ab">Gewünschter Tag des Wiedereintritts.</param>
    /// <returns>Die Begründung als Meldung für die Oberfläche oder null, wenn der Wiedereintritt möglich ist.</returns>
    public static string? Hindernis(Mitglied mitglied, DateOnly ab)
    {
        if (mitglied.AnonymisiertAm is not null)
        {
            return "Der frühere Datensatz ist anonymisiert; bitte als neue Aufnahme ohne Wiedereintritt erfassen.";
        }

        if (mitglied.Mitgliedschaften.Any(s => s.Austrittsgrund == Austrittsgrund.Tod))
        {
            return $"{mitglied.Anzeigename} ist als verstorben eingetragen.";
        }

        if (mitglied.Mitgliedschaften.FirstOrDefault(s => !s.Vordienstzeit && (s.GueltigBis is null || s.GueltigBis >= ab)) is { } offen)
        {
            return $"{mitglied.Anzeigename} ist am {ab:dd.MM.yyyy} noch Mitglied{(offen.Abteilung is { } a ? $" der {a.Bezeichnung}" : "")}; "
                + "bitte Übertritt oder Wechsel nutzen.";
        }

        return null;
    }
}
