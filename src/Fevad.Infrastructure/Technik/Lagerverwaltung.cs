using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Technik;

/// <summary>
/// Verbrauchsmaterial (V16): Entnahme nach FEFO mit Lagerbewegung, Übernahme der Entnahmen eines Einsatzes in den Kostenersatz
/// und Verfall bzw. Mindestmenge als Frist für die Erinnerung.
/// </summary>
public sealed class Lagerverwaltung(AppDbContext db, ParameterLeser parameter) : IFristQuelle
{
    /// <summary>Entnimmt Material und schreibt je Charge eine Bewegung; speichert nicht.</summary>
    /// <param name="artikel">Artikel mit geladenen Chargen.</param>
    /// <param name="menge">Menge in der Einheit des Artikels.</param>
    /// <param name="datum">Tag der Entnahme.</param>
    /// <param name="einsatzId">Einsatz, falls im Einsatz verbraucht.</param>
    /// <param name="bemerkung">Bemerkung.</param>
    /// <returns>Fehlertext oder null.</returns>
    public string? Entnehmen(Lagerartikel artikel, decimal menge, DateOnly datum, Guid? einsatzId, string? bemerkung)
    {
        var teile = artikel.Entnehmen(menge, datum);
        if (teile is null)
        {
            return menge <= 0
                ? "Bitte eine Menge über 0 angeben."
                : $"Nicht genug verwendbarer Bestand: {artikel.Bezeichnung} hat {artikel.Chargen.Where(c => c.VerfallAm is null || c.VerfallAm >= datum).Sum(c => c.Menge):0.##} {artikel.Einheit}.";
        }

        foreach (var (charge, teil) in teile)
        {
            db.Lagerbewegungen.Add(new Lagerbewegung
            {
                LagerartikelId = artikel.Id,
                LagerchargeId = charge.Id,
                Art = LagerbewegungArt.Entnahme,
                Datum = datum,
                Menge = teil,
                Stueckpreis = artikel.Stueckpreis,
                EinsatzId = einsatzId,
                Bemerkung = bemerkung,
            });
        }

        return null;
    }

    /// <summary>
    /// Übernimmt die noch nicht übernommenen Entnahmen eines Einsatzes mit Preis als Verbrauch in den Kostenersatz-Vorgang;
    /// je Artikel ein Posten. Speichert.
    /// </summary>
    /// <param name="vorgang">Der Kostenersatz-Vorgang.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der neuen Posten.</returns>
    public async Task<int> InKostenersatzUebernehmenAsync(Kostenersatzvorgang vorgang, CancellationToken ct)
    {
        var offen = await db.Lagerbewegungen.Include(b => b.Lagerartikel)
            .Where(b => b.EinsatzId == vorgang.EinsatzId && b.Art == LagerbewegungArt.Entnahme && b.ZusatzkostenId == null && b.Stueckpreis != null)
            .ToListAsync(ct);
        var neu = 0;
        foreach (var gruppe in offen.GroupBy(b => (b.LagerartikelId, b.Stueckpreis)))
        {
            var artikel = gruppe.First().Lagerartikel!;
            var menge = gruppe.Sum(b => b.Menge);
            var posten = new Zusatzkosten
            {
                KostenersatzvorgangId = vorgang.Id,
                Art = ZusatzkostenArt.Verbrauch,
                Bezeichnung = $"Lager: {menge:0.##} {artikel.Einheit} {artikel.Bezeichnung} à {gruppe.Key.Stueckpreis:N2} €",
                Betrag = Math.Round(menge * gruppe.Key.Stueckpreis!.Value, 2),
            };
            db.Zusatzkosten.Add(posten);
            foreach (var bewegung in gruppe)
            {
                bewegung.ZusatzkostenId = posten.Id;
            }

            neu++;
        }

        await db.SaveChangesAsync(ct);
        return neu;
    }

    /// <summary>Zahl der Entnahmen eines Einsatzes, die noch nicht im Kostenersatz stehen.</summary>
    /// <param name="einsatzId">Der Einsatz.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Anzahl.</returns>
    public Task<int> OffeneEntnahmenAsync(Guid einsatzId, CancellationToken ct) =>
        db.Lagerbewegungen.CountAsync(b => b.EinsatzId == einsatzId && b.Art == LagerbewegungArt.Entnahme && b.ZusatzkostenId == null && b.Stueckpreis != null, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Fristhinweis>> FaelligeAsync(DateOnly stichtag, CancellationToken ct)
    {
        var warnungTage = await parameter.ZahlAsync(ParameterSchluessel.FristWarnungTage, stichtag, 90, ct);
        var artikel = await db.Lagerartikel.AsNoTracking().Include(a => a.Chargen).Where(a => a.Aktiv).ToListAsync(ct);
        var hinweise = new List<Fristhinweis>();
        foreach (var a in artikel)
        {
            foreach (var charge in a.Chargen.Where(c => c.Menge > 0 && c.VerfallAm != null))
            {
                var ampel = Frist.Bewerten(charge.VerfallAm, stichtag, warnungTage);
                if (ampel != Fristampel.Gruen)
                {
                    hinweise.Add(new Fristhinweis("Technik", $"Lager: {a.Bezeichnung}", $"Verfall ({charge.Menge:0.##} {a.Einheit})", charge.VerfallAm, ampel, Berechtigungen.TechnikPruefen));
                }
            }

            if (a.Nachbestellen)
            {
                hinweise.Add(new Fristhinweis("Technik", $"Lager: {a.Bezeichnung}", $"Mindestmenge unterschritten ({a.Bestand:0.##} von {a.Mindestmenge:0.##} {a.Einheit})", null, Fristampel.Gelb, Berechtigungen.TechnikPruefen));
            }
        }

        return hinweise;
    }
}
