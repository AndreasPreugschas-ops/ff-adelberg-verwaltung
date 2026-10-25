using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

/// <summary>Art eines Laufbahnhinweises.</summary>
public enum HinweisArt
{
    /// <summary>Die Probezeit endet; über die Übernahme entscheiden.</summary>
    [Display(Name = "Probezeit")] ProbezeitEnde,
    /// <summary>Übertritt aus der Jugendfeuerwehr in die Einsatzabteilung steht an.</summary>
    [Display(Name = "Übertritt Jugendfeuerwehr")] UebertrittJugend,
    /// <summary>Übertritt aus der Kinderfeuerwehr in die Jugendfeuerwehr steht an.</summary>
    [Display(Name = "Übertritt Kinderfeuerwehr")] UebertrittKinder,
    /// <summary>Ab diesem Tag ist Einsatzdienst möglich.</summary>
    [Display(Name = "Einsatzdienst")] EinsatzdienstAb,
    /// <summary>Die Altersgrenze der Einsatzabteilung wird erreicht.</summary>
    [Display(Name = "Altersgrenze")] AltersgrenzeEinsatz,
    /// <summary>Der Wechsel in die Altersabteilung wird möglich.</summary>
    [Display(Name = "Altersabteilung")] AltersabteilungMoeglich,
}

/// <summary>
/// Anstehende Entscheidung zur Mitgliedschaft. <see cref="Pflicht"/> heißt: Die Satzung verlangt eine Entscheidung
/// bis <see cref="FaelligAm"/>; sonst ist es nur ein Hinweis auf eine neue Möglichkeit.
/// </summary>
/// <param name="Art">Art des Hinweises.</param>
/// <param name="FaelligAm">Tag des Ereignisses.</param>
/// <param name="Text">Hinweistext.</param>
/// <param name="Pflicht">Die Satzung verlangt eine Entscheidung.</param>
/// <param name="MitgliedschaftId">Die betroffene Mitgliedschaft.</param>
public sealed record Laufbahnhinweis(HinweisArt Art, DateOnly FaelligAm, string Text, bool Pflicht, Guid MitgliedschaftId)
{
    /// <summary>Ampel des Hinweises: grün für bloße Möglichkeiten, bei Pflicht gelb bis zur Fälligkeit, danach rot.</summary>
    /// <param name="stichtag">Tag der Bewertung.</param>
    /// <returns>Die Ampel.</returns>
    public Fristampel Ampel(DateOnly stichtag) =>
        !Pflicht ? Fristampel.Gruen : FaelligAm < stichtag ? Fristampel.Rot : Fristampel.Gelb;
}

/// <summary>Berechnet anstehende Entscheidungen zur Mitgliedschaft aus Alter, Abschnitten und Satzungsregeln.</summary>
public static class Laufbahnhinweise
{
    /// <summary>Hinweise zu Probezeit, Übertritten und Altersgrenzen der am Stichtag offenen Mitgliedschaften.</summary>
    /// <param name="geburtsdatum">Geburtsdatum des Mitglieds.</param>
    /// <param name="abschnitte">Mitgliedschaften des Mitglieds.</param>
    /// <param name="regeln">Altersgrenzen und Vorlauf.</param>
    /// <param name="stichtag">Heutiger Tag.</param>
    /// <returns>Die Hinweise, nach Fälligkeit sortiert.</returns>
    public static IReadOnlyList<Laufbahnhinweis> Berechnen(
        DateOnly geburtsdatum, IEnumerable<Abschnitt> abschnitte, Laufbahnregeln regeln, DateOnly stichtag)
    {
        var grenze = stichtag.AddMonths(regeln.VorlaufMonate);
        var rueckblick = stichtag.AddMonths(-regeln.VorlaufMonate);
        var hinweise = new List<Laufbahnhinweis>();

        foreach (var a in abschnitte.Where(a => !a.Vordienstzeit && a.IstOffenAm(stichtag)))
        {
            if (a.Status == MitgliedStatus.Probezeit)
            {
                var ende = a.ProbezeitBis ?? a.Von.AddMonths(regeln.ProbezeitMonate);
                if (ende <= grenze)
                {
                    hinweise.Add(new(HinweisArt.ProbezeitEnde, ende,
                        $"Probezeit ({a.Abteilung}) endet am {ende:dd.MM.yyyy}: über die Übernahme entscheiden.", true, a.Id));
                }
            }

            switch (a.Art)
            {
                case AbteilungArt.Kinder:
                {
                    var ende = regeln.KinderEndeAlter > 0 ? Geburtstag(geburtsdatum, regeln.KinderEndeAlter) : DateOnly.MaxValue;
                    var moeglich = Geburtstag(geburtsdatum, regeln.JugendAufnahmeAbAlter);
                    if (ende <= grenze)
                    {
                        hinweise.Add(new(HinweisArt.UebertrittKinder, ende,
                            $"Kinderfeuerwehr endet mit dem {regeln.KinderEndeAlter}. Geburtstag am {ende:dd.MM.yyyy}: Übertritt in die Jugendfeuerwehr oder Austritt.",
                            true, a.Id));
                    }
                    else if (moeglich <= grenze)
                    {
                        hinweise.Add(new(HinweisArt.UebertrittKinder, moeglich,
                            $"Übertritt in die Jugendfeuerwehr ab {moeglich:dd.MM.yyyy} möglich (Aufnahme ab {regeln.JugendAufnahmeAbAlter}).",
                            false, a.Id));
                    }

                    break;
                }

                case AbteilungArt.Jugend:
                {
                    var ende = Geburtstag(geburtsdatum, regeln.JugendEndeAlter);
                    var moeglich = Geburtstag(geburtsdatum, regeln.AufnahmeEinsatzAbAlter);
                    if (ende <= grenze)
                    {
                        hinweise.Add(new(HinweisArt.UebertrittJugend, ende,
                            $"Jugendfeuerwehr endet mit dem {regeln.JugendEndeAlter}. Geburtstag am {ende:dd.MM.yyyy}: Übertritt in die Einsatzabteilung oder Austritt.",
                            true, a.Id));
                    }
                    else if (moeglich <= grenze)
                    {
                        hinweise.Add(new(HinweisArt.UebertrittJugend, moeglich,
                            $"Übertritt in die Einsatzabteilung ab {moeglich:dd.MM.yyyy} möglich (Aufnahme ab {regeln.AufnahmeEinsatzAbAlter}).",
                            false, a.Id));
                    }

                    break;
                }

                case AbteilungArt.Einsatz:
                {
                    var einsatzdienst = Geburtstag(geburtsdatum, regeln.EinsatzdienstAbAlter);
                    var altersgrenze = Geburtstag(geburtsdatum, regeln.AltersgrenzeEinsatzdienst);
                    var alters = Geburtstag(geburtsdatum, regeln.AltersabteilungAbAlter);
                    if (stichtag < einsatzdienst)
                    {
                        hinweise.Add(new(HinweisArt.EinsatzdienstAb, einsatzdienst,
                            $"Einsatzdienst erst ab dem {regeln.EinsatzdienstAbAlter}. Geburtstag am {einsatzdienst:dd.MM.yyyy}. Bis dahin ist die schriftliche Zustimmung der Erziehungsberechtigten nötig.", false, a.Id));
                    }

                    if (altersgrenze <= grenze)
                    {
                        hinweise.Add(new(HinweisArt.AltersgrenzeEinsatz, altersgrenze,
                            $"Dienst in der Einsatzabteilung endet mit dem {regeln.AltersgrenzeEinsatzdienst}. Geburtstag am {altersgrenze:dd.MM.yyyy}: Übertritt in die Altersabteilung anbieten.",
                            true, a.Id));
                    }
                    else if (alters >= rueckblick && alters <= grenze)
                    {
                        hinweise.Add(new(HinweisArt.AltersabteilungMoeglich, alters,
                            $"Übertritt in die Altersabteilung ab {alters:dd.MM.yyyy} auf Antrag möglich.", false, a.Id));
                    }

                    break;
                }
            }
        }

        return hinweise.OrderBy(h => h.FaelligAm).ToList();
    }

    /// <summary>Tag, an dem das Lebensjahr vollendet ist; am 29.02. Geborene in Nicht-Schaltjahren am 01.03. (wie <see cref="Mitglied.Alter"/>).</summary>
    /// <param name="geburtsdatum">Geburtsdatum.</param>
    /// <param name="alter">Zu vollendendes Lebensjahr.</param>
    /// <returns>Der Tag, an dem das Lebensjahr vollendet ist.</returns>
    public static DateOnly Geburtstag(DateOnly geburtsdatum, int alter)
    {
        var jahr = geburtsdatum.Year + alter;
        return geburtsdatum is { Month: 2, Day: 29 } && !DateTime.IsLeapYear(jahr)
            ? new DateOnly(jahr, 3, 1)
            : geburtsdatum.AddYears(alter);
    }
}
