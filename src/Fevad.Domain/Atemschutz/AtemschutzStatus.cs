using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Atemschutz;

/// <summary>Berechneter Einsatzstatus eines Atemschutzgeräteträgers. Wird nie gespeichert.</summary>
public sealed record AtemschutzStatus(
    bool LehrgangVorhanden,
    bool Tauglich,
    DateOnly? VorsorgeFaellig,
    IReadOnlyDictionary<AtemschutzNachweisArt, DateOnly?> LetzteUebungen,
    Fristampel Ampel,
    bool Gesperrt = false)
{
    /// <summary>Einsatzbereit, solange die Ampel nicht rot ist.</summary>
    public bool Einsatzbereit => Ampel != Fristampel.Rot;

    /// <summary>Jährliche Pflichten nach FwDV 7: Unterweisung, Belastungsübung, Einsatzübung.</summary>
    public static readonly AtemschutzNachweisArt[] JaehrlichePflichten =
    [
        AtemschutzNachweisArt.Unterweisung,
        AtemschutzNachweisArt.Belastungsuebung,
        AtemschutzNachweisArt.Einsatzuebung,
    ];

    /// <summary>Parameter-Schlüssel für das Intervall einer jährlichen Pflicht.</summary>
    public static string IntervallSchluessel(AtemschutzNachweisArt art) => art switch
    {
        AtemschutzNachweisArt.Belastungsuebung => ParameterSchluessel.BelastungsuebungIntervallMonate,
        AtemschutzNachweisArt.Einsatzuebung => ParameterSchluessel.EinsatzuebungIntervallMonate,
        AtemschutzNachweisArt.Unterweisung => ParameterSchluessel.AtemschutzUnterweisungIntervallMonate,
        _ => ParameterSchluessel.AtemschutzUebungIntervallMonate,
    };

    /// <summary>Berechnet den Status mit demselben Intervall für alle jährlichen Pflichten.</summary>
    /// <param name="lehrgangVorhanden">Der Atemschutzlehrgang liegt vor.</param>
    /// <param name="vorsorgen">Vorsorgen des Trägers.</param>
    /// <param name="nachweise">Nachweise des Trägers.</param>
    /// <param name="stichtag">Stichtag der Bewertung.</param>
    /// <param name="uebungIntervallMonate">Intervall der jährlichen Pflichten in Monaten.</param>
    /// <param name="warnungTage">Vorwarnzeit für Gelb.</param>
    /// <returns>Der berechnete Status.</returns>
    public static AtemschutzStatus Berechnen(
        bool lehrgangVorhanden,
        IEnumerable<AtemschutzVorsorge> vorsorgen,
        IEnumerable<AtemschutzNachweis> nachweise,
        DateOnly stichtag,
        int uebungIntervallMonate,
        int warnungTage) =>
        Berechnen(lehrgangVorhanden, vorsorgen, nachweise, stichtag, _ => uebungIntervallMonate, warnungTage);

    /// <summary>Wie oben, mit eigenem Intervall je Übungsart.</summary>
    /// <param name="lehrgangVorhanden">Der Atemschutzlehrgang liegt vor.</param>
    /// <param name="vorsorgen">Vorsorgen des Trägers.</param>
    /// <param name="nachweise">Nachweise des Trägers.</param>
    /// <param name="stichtag">Stichtag der Bewertung.</param>
    /// <param name="intervallMonate">Intervall je jährlicher Pflicht in Monaten.</param>
    /// <param name="warnungTage">Vorwarnzeit für Gelb.</param>
    /// <param name="gesperrt">Der Träger ist vorübergehend abgemeldet.</param>
    /// <param name="kalenderjahrWarnungAbMonat">
    /// 1 bis 12: jährliche Pflichten zählen im Kalenderjahr und werden ab dem Ersten dieses Monats gelb (siehe <see cref="Jahrespflicht"/>);
    /// 0: gleitend ab dem letzten Nachweis mit <paramref name="warnungTage"/>.
    /// </param>
    /// <returns>Der berechnete Status.</returns>
    public static AtemschutzStatus Berechnen(
        bool lehrgangVorhanden,
        IEnumerable<AtemschutzVorsorge> vorsorgen,
        IEnumerable<AtemschutzNachweis> nachweise,
        DateOnly stichtag,
        Func<AtemschutzNachweisArt, int> intervallMonate,
        int warnungTage,
        bool gesperrt = false,
        int kalenderjahrWarnungAbMonat = 0)
    {
        // Nur was am Stichtag schon stattgefunden hat, zählt.
        var letzteVorsorge = vorsorgen.Where(v => v.UntersuchtAm <= stichtag).MaxBy(v => v.UntersuchtAm);
        var tauglich = letzteVorsorge is not null
            && letzteVorsorge.Ergebnis != VorsorgeErgebnis.NichtGeeignet
            && letzteVorsorge.NaechsteFaellig >= stichtag;

        var nachweisListe = nachweise.Where(n => n.Datum <= stichtag).ToList();
        var letzte = JaehrlichePflichten.ToDictionary(
            art => art,
            art => nachweisListe.Where(n => n.Erfuellt(art)).Select(n => (DateOnly?)n.Datum).Max());

        var ampeln = new List<Fristampel>
        {
            lehrgangVorhanden && tauglich && !gesperrt ? Fristampel.Gruen : Fristampel.Rot,
            Frist.Bewerten(letzteVorsorge?.NaechsteFaellig, stichtag, warnungTage),
        };
        ampeln.AddRange(letzte.Select(l =>
        {
            if (l.Value is null)
            {
                return Fristampel.Rot;
            }

            var (faellig, warnung) = Jahrespflicht(l.Value.Value, intervallMonate(l.Key), warnungTage, kalenderjahrWarnungAbMonat);
            return Frist.Bewerten(faellig, stichtag, warnung);
        }));

        return new AtemschutzStatus(lehrgangVorhanden, tauglich, letzteVorsorge?.NaechsteFaellig, letzte, ampeln.Max(), gesperrt);
    }

    /// <summary>
    /// Letzter gültiger Tag und Vorwarnzeit einer jährlichen Pflicht. Im Kalenderjahr (Andi, 25.10.2026) gilt ein Nachweis bis zum
    /// 31.12. des Folgejahres (bei längeren Intervallen entsprechend später): Wer 2025 geübt hat, muss 2026 wieder üben und wird ab dem
    /// Ersten des Warnmonats gelb, wenn es 2026 noch keinen Nachweis gibt.
    /// </summary>
    /// <param name="letzte">Datum des letzten Nachweises.</param>
    /// <param name="intervallMonate">Intervall der Pflicht in Monaten; im Kalenderjahr zählen volle Jahre, mindestens eines.</param>
    /// <param name="warnungTage">Vorwarnzeit bei gleitender Zählung.</param>
    /// <param name="kalenderjahrWarnungAbMonat">1 bis 12 für das Kalenderjahr mit Warnung ab diesem Monat, sonst gleitend.</param>
    /// <returns>Letzter gültiger Tag und Vorwarnzeit in Tagen.</returns>
    public static (DateOnly Faellig, int WarnungTage) Jahrespflicht(DateOnly letzte, int intervallMonate, int warnungTage, int kalenderjahrWarnungAbMonat)
    {
        if (kalenderjahrWarnungAbMonat is < 1 or > 12)
        {
            return (letzte.AddMonths(intervallMonate), warnungTage);
        }

        var faellig = new DateOnly(letzte.Year + Math.Max(1, intervallMonate / 12), 12, 31);
        return (faellig, faellig.DayNumber - new DateOnly(faellig.Year, kalenderjahrWarnungAbMonat, 1).DayNumber);
    }
}
