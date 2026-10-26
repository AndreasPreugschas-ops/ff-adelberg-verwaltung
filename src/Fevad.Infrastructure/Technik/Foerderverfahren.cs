using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Infrastructure.Persistenz.Seed;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Technik;

/// <summary>Fristen eines Z-Feu-Antrags, aus den Parametern berechnet.</summary>
/// <param name="Antragsfrist">Letzter Tag für den Antrag beim Landratsamt.</param>
/// <param name="NachweisBis">Spätester Verwendungsnachweis; null, solange nicht geliefert.</param>
/// <param name="NachweisFuerAuszahlungBis">Stichtag, bis zu dem der Nachweis für eine Auszahlung im laufenden Jahr vorliegen muss.</param>
public sealed record Foerderfristen(DateOnly Antragsfrist, DateOnly? NachweisBis, DateOnly NachweisFuerAuszahlungBis);

/// <summary>
/// Z-Feu-Assistent (V30): Fristen aus Parametern, Fristhinweise für Antrag und Verwendungsnachweis und Übernahme
/// von Zuwendung und Zweckbindung in die Fahrzeugakte.
/// </summary>
public sealed class Foerderverfahren(AppDbContext db, ParameterLeser parameter) : IFristQuelle
{
    /// <summary>Berechnet die Fristen eines Antrags.</summary>
    /// <param name="antrag">Antrag.</param>
    /// <param name="geliefertAm">Lieferung laut Beschaffung.</param>
    /// <param name="stichtag">Tag, für den die Parameter gelten.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Fristen.</returns>
    public async Task<Foerderfristen> FristenAsync(Foerderantrag antrag, DateOnly? geliefertAm, DateOnly stichtag, CancellationToken ct)
    {
        var monat = Math.Clamp(await parameter.ZahlAsync(ParameterSchluessel.ZFeuAntragsfristMonat, stichtag, 2, ct), 1, 12);
        var tag = await parameter.ZahlAsync(ParameterSchluessel.ZFeuAntragsfristTag, stichtag, 15, ct);
        var nachweisMonate = await parameter.ZahlAsync(ParameterSchluessel.ZFeuVerwendungsnachweisMonate, stichtag, 12, ct);
        var auszahlungMonat = Math.Clamp(await parameter.ZahlAsync(ParameterSchluessel.ZFeuAuszahlungNachweisBisMonat, stichtag, 12, ct), 1, 12);
        var auszahlungTag = await parameter.ZahlAsync(ParameterSchluessel.ZFeuAuszahlungNachweisBisTag, stichtag, 1, ct);
        var jahr = (geliefertAm ?? stichtag).Year;
        return new Foerderfristen(
            antrag.Antragsfrist(monat, tag),
            geliefertAm?.AddMonths(nachweisMonate),
            new DateOnly(jahr, auszahlungMonat, Math.Clamp(auszahlungTag, 1, DateTime.DaysInMonth(jahr, auszahlungMonat))));
    }

    /// <summary>
    /// Trägt Zuwendung, Bescheid und Zweckbindung in die Akte des beschafften Fahrzeugs ein. Die Zweckbindung läuft ab
    /// Indienststellung, ersatzweise ab Lieferung, so viele Jahre wie der Fördergegenstand vorgibt. Speichert nicht.
    /// </summary>
    /// <param name="antrag">Antrag mit geladenem Fördergegenstand.</param>
    /// <param name="fahrzeug">Beschafftes Fahrzeug.</param>
    /// <param name="geliefertAm">Lieferung laut Beschaffung.</param>
    /// <returns>Fehlertext oder null.</returns>
    public static string? ZweckbindungUebernehmen(Foerderantrag antrag, Fahrzeug fahrzeug, DateOnly? geliefertAm)
    {
        if (antrag.Zuwendung is null || antrag.BescheidAm is null)
        {
            return "Erst Bescheid und bewilligte Zuwendung eintragen.";
        }

        if (antrag.Foerdergegenstand is null)
        {
            return "Fördergegenstand fehlt; er bestimmt die Dauer der Zweckbindung.";
        }

        if ((fahrzeug.IndienstAm ?? geliefertAm) is not { } beginn)
        {
            return "Am Fahrzeug fehlt die Indienststellung, an der Beschaffung die Lieferung.";
        }

        fahrzeug.Foerdermittel = antrag.Zuwendung;
        fahrzeug.Foerdergeber = Stammdaten.FoerdergeberZfeu;
        fahrzeug.ZuwendungsbescheidAm = antrag.BescheidAm;
        fahrzeug.ZuwendungsbescheidAktenzeichen = antrag.BescheidAktenzeichen;
        fahrzeug.ZweckbindungBis = beginn.AddYears(antrag.Foerdergegenstand.ZweckbindungJahre);
        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Fristhinweis>> FaelligeAsync(DateOnly stichtag, CancellationToken ct)
    {
        var warnungTage = await parameter.ZahlAsync(ParameterSchluessel.FristWarnungTage, stichtag, 90, ct);
        var antraege = await db.Foerderantraege.AsNoTracking().Include(a => a.Beschaffung)
            .Where(a => a.Status != Foerderstatus.Ausgezahlt && a.Status != Foerderstatus.Abgelehnt && a.Status != Foerderstatus.NachweisEingereicht)
            .ToListAsync(ct);
        var hinweise = new List<Fristhinweis>();
        foreach (var antrag in antraege)
        {
            var fristen = await FristenAsync(antrag, antrag.Beschaffung?.GeliefertAm, stichtag, ct);
            var gegenstand = $"Z-Feu: {antrag.Beschaffung?.Bezeichnung}";
            if (antrag.Status < Foerderstatus.Beantragt)
            {
                var ampel = Frist.Bewerten(fristen.Antragsfrist, stichtag, warnungTage);
                if (ampel != Fristampel.Gruen)
                {
                    hinweise.Add(new Fristhinweis("Technik", gegenstand, "Antrag beim Landratsamt (vorher KBM und Gemeinderatsbeschluss)", fristen.Antragsfrist, ampel, Berechtigungen.BeschaffungBearbeiten));
                }
            }
            else if (antrag.Status == Foerderstatus.Bewilligt && fristen.NachweisBis is { } bis)
            {
                var ampel = Frist.Bewerten(bis, stichtag, warnungTage);
                if (ampel != Fristampel.Gruen)
                {
                    hinweise.Add(new Fristhinweis("Technik", gegenstand, "Verwendungsnachweis", bis, ampel, Berechtigungen.BeschaffungBearbeiten));
                }
            }
        }

        return hinweise;
    }
}
