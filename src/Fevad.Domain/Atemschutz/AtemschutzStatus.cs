using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Atemschutz;

/// <summary>Berechneter Einsatzstatus eines Atemschutzgeräteträgers. Wird nie gespeichert.</summary>
public sealed record AtemschutzStatus(
    bool LehrgangVorhanden,
    bool Tauglich,
    DateOnly? VorsorgeFaellig,
    IReadOnlyDictionary<AtemschutzNachweisArt, DateOnly?> LetzteUebungen,
    Fristampel Ampel)
{
    public bool Einsatzbereit => Ampel != Fristampel.Rot;

    /// <summary>Jährliche Pflichten nach FwDV 7: Unterweisung, Belastungsübung, Einsatzübung.</summary>
    public static readonly AtemschutzNachweisArt[] JaehrlichePflichten =
    [
        AtemschutzNachweisArt.Unterweisung,
        AtemschutzNachweisArt.Belastungsuebung,
        AtemschutzNachweisArt.Einsatzuebung,
    ];

    public static AtemschutzStatus Berechnen(
        bool lehrgangVorhanden,
        IEnumerable<AtemschutzVorsorge> vorsorgen,
        IEnumerable<AtemschutzNachweis> nachweise,
        DateOnly stichtag,
        int uebungIntervallMonate,
        int warnungTage)
    {
        // Nur was am Stichtag schon stattgefunden hat, zählt.
        var letzteVorsorge = vorsorgen.Where(v => v.UntersuchtAm <= stichtag).MaxBy(v => v.UntersuchtAm);
        var tauglich = letzteVorsorge is not null
            && letzteVorsorge.Ergebnis != VorsorgeErgebnis.NichtGeeignet
            && letzteVorsorge.NaechsteFaellig >= stichtag;

        var nachweisListe = nachweise.Where(n => n.Datum <= stichtag).ToList();
        var letzte = JaehrlichePflichten.ToDictionary(
            art => art,
            art => nachweisListe.Where(n => n.Art == art).Select(n => (DateOnly?)n.Datum).Max());

        var ampeln = new List<Fristampel>
        {
            lehrgangVorhanden && tauglich ? Fristampel.Gruen : Fristampel.Rot,
            Frist.Bewerten(letzteVorsorge?.NaechsteFaellig, stichtag, warnungTage),
        };
        ampeln.AddRange(letzte.Values.Select(datum => datum is null
            ? Fristampel.Rot
            : Frist.Bewerten(datum.Value.AddMonths(uebungIntervallMonate), stichtag, warnungTage)));

        return new AtemschutzStatus(lehrgangVorhanden, tauglich, letzteVorsorge?.NaechsteFaellig, letzte, ampeln.Max());
    }
}
