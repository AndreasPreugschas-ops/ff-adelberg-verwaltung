using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

public enum HinweisArt
{
    [Display(Name = "Probezeit")] ProbezeitEnde,
    [Display(Name = "Übertritt Jugendfeuerwehr")] UebertrittJugend,
    [Display(Name = "Einsatzdienst")] EinsatzdienstAb,
    [Display(Name = "Altersgrenze")] AltersgrenzeEinsatz,
    [Display(Name = "Altersabteilung")] AltersabteilungMoeglich,
}

/// <summary>
/// Anstehende Entscheidung zur Mitgliedschaft. <see cref="Pflicht"/> heißt: Die Satzung verlangt eine Entscheidung
/// bis <see cref="FaelligAm"/>; sonst ist es nur ein Hinweis auf eine neue Möglichkeit.
/// </summary>
public sealed record Laufbahnhinweis(HinweisArt Art, DateOnly FaelligAm, string Text, bool Pflicht, Guid MitgliedschaftId)
{
    public Fristampel Ampel(DateOnly stichtag) =>
        !Pflicht ? Fristampel.Gruen : FaelligAm < stichtag ? Fristampel.Rot : Fristampel.Gelb;
}

public static class Laufbahnhinweise
{
    /// <summary>Hinweise zu Probezeit, Übertritten und Altersgrenzen der am Stichtag offenen Mitgliedschaften.</summary>
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
                var ende = a.Von.AddMonths(regeln.ProbezeitMonate);
                if (ende <= grenze)
                {
                    hinweise.Add(new(HinweisArt.ProbezeitEnde, ende,
                        $"Probezeit ({a.Abteilung}) endet am {ende:dd.MM.yyyy}: über die Übernahme entscheiden.", true, a.Id));
                }
            }

            switch (a.AbteilungCode)
            {
                case AbteilungCode.Jugend:
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

                case AbteilungCode.Einsatz:
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
    public static DateOnly Geburtstag(DateOnly geburtsdatum, int alter)
    {
        var jahr = geburtsdatum.Year + alter;
        return geburtsdatum is { Month: 2, Day: 29 } && !DateTime.IsLeapYear(jahr)
            ? new DateOnly(jahr, 3, 1)
            : geburtsdatum.AddYears(alter);
    }
}
