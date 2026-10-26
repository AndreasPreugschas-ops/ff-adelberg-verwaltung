using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Mitglieder;

/// <summary>
/// Liest und schreibt das Einwilligungsregister (V39, V45). Erklärungen kommen auf Papier (Verwaltung), aus dem angenommenen
/// Antrag oder über das eigene Konto bzw. das Elternkonto. Gespeichert wird beim Aufrufer.
/// </summary>
public sealed class Einwilligungsregister(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Stand einer Art für ein Mitglied.</summary>
    /// <param name="Art">Die Einwilligungsart.</param>
    /// <param name="Status">Stand am Stichtag.</param>
    /// <param name="Aktuell">Die maßgebliche Erklärung, falls vorhanden.</param>
    public sealed record Stand(Einwilligungsart Art, Einwilligungsstatus Status, Einwilligung? Aktuell);

    /// <summary>Altersgrenzen für die Regeln: Volljährigkeit und Mitentscheidung des Jugendlichen.</summary>
    /// <param name="VolljaehrigAb">Alter der Volljährigkeit.</param>
    /// <param name="MitentscheidungAb">Ab diesem Alter stimmt der Jugendliche selbst zu.</param>
    public sealed record Grenzen(int VolljaehrigAb, int MitentscheidungAb);

    /// <summary>Lädt die Altersgrenzen aus den Parametern.</summary>
    /// <param name="heute">Stichtag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Grenzen.</returns>
    public async Task<Grenzen> GrenzenAsync(DateOnly heute, CancellationToken ct = default) =>
        new(await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, heute, 18, ct),
            await parameter.ZahlAsync(ParameterSchluessel.EinwilligungMitentscheidungAbAlter, heute, 16, ct));

    /// <summary>Aktive Arten; die nur für die Jugend gelten, nur wenn <paramref name="jugend"/>.</summary>
    /// <param name="jugend">Das Mitglied ist in der Jugend- oder Kinderfeuerwehr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Arten in ihrer Reihenfolge.</returns>
    public async Task<IReadOnlyList<Einwilligungsart>> ArtenAsync(bool jugend, CancellationToken ct = default) =>
        await db.Einwilligungsarten.AsNoTracking().Where(a => a.Aktiv && (jugend || !a.NurJugend))
            .OrderBy(a => a.Sortierung).ThenBy(a => a.Bezeichnung).ToListAsync(ct);

    /// <summary>Stand aller passenden Arten für ein Mitglied.</summary>
    /// <param name="mitglied">Das Mitglied (Geburtsdatum für die Altersregeln).</param>
    /// <param name="jugend">Das Mitglied ist in der Jugend- oder Kinderfeuerwehr.</param>
    /// <param name="heute">Stichtag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Je Art der Stand.</returns>
    public async Task<IReadOnlyList<Stand>> StandAsync(Mitglied mitglied, bool jugend, DateOnly heute, CancellationToken ct = default)
    {
        var arten = await ArtenAsync(jugend, ct);
        var eintraege = await db.Einwilligungen.AsNoTracking().Where(e => e.MitgliedId == mitglied.Id).ToListAsync(ct);
        var grenzen = await GrenzenAsync(heute, ct);
        var alter = mitglied.AlterAm(heute);
        return arten.Select(a =>
        {
            var eigene = eintraege.Where(e => e.EinwilligungsartId == a.Id).ToList();
            return new Stand(a, Einwilligungsregeln.Status(eigene, alter, grenzen.VolljaehrigAb, grenzen.MitentscheidungAb), Einwilligungsregeln.Aktuell(eigene));
        }).ToList();
    }

    /// <summary>Verlauf aller Erklärungen eines Mitglieds, neueste zuerst.</summary>
    /// <param name="mitgliedId">Das Mitglied.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Erklärungen mit Art.</returns>
    public async Task<IReadOnlyList<Einwilligung>> VerlaufAsync(Guid mitgliedId, CancellationToken ct = default) =>
        await db.Einwilligungen.AsNoTracking().Include(e => e.Einwilligungsart)
            .Where(e => e.MitgliedId == mitgliedId).OrderByDescending(e => e.Am).ThenByDescending(e => e.Id).ToListAsync(ct);

    /// <summary>
    /// Trägt eine neue Erklärung ein. Ein Nein zu einer wirksamen Einwilligung gilt zugleich als ihr Widerruf, damit die Folgeaufgabe
    /// (z. B. Fotos entfernen) entsteht.
    /// </summary>
    /// <param name="einwilligung">Die Erklärung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task EintragenAsync(Einwilligung einwilligung, CancellationToken ct = default)
    {
        if (!einwilligung.Erteilt)
        {
            await WiderrufenAsync(einwilligung.MitgliedId, einwilligung.EinwilligungsartId, einwilligung.Am, einwilligung.DurchName ?? "Erklärung „nein“", ct);
        }

        db.Einwilligungen.Add(einwilligung);
    }

    /// <summary>Offene Folgeaufgaben nach Widerrufen (WhatsApp-Gruppe, veröffentlichte Fotos) für die übergebenen Mitglieder.</summary>
    /// <param name="mitglieder">Mitglieder, die der Benutzer bearbeiten darf.</param>
    /// <returns>Abfrage der widerrufenen Erklärungen mit offener Folgeaufgabe.</returns>
    public IQueryable<Einwilligung> OffeneFolgeaufgaben(IQueryable<Mitglied> mitglieder) =>
        db.Einwilligungen.Where(e => e.WiderrufenAm != null && e.FolgeaufgabeErledigtAm == null && e.Einwilligungsart!.HinweisBeiWiderruf != null
            && mitglieder.Any(m => m.Id == e.MitgliedId));

    /// <summary>
    /// Widerruft die maßgebliche Erklärung einer Art, falls sie erteilt und nicht widerrufen ist. Der Widerruf eines Elternteils oder
    /// des Jugendlichen wirkt sofort.
    /// </summary>
    /// <param name="mitgliedId">Das Mitglied.</param>
    /// <param name="artId">Die Art.</param>
    /// <param name="am">Tag des Widerrufs.</param>
    /// <param name="durch">Wer widerruft.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns><c>true</c>, wenn etwas widerrufen wurde.</returns>
    public async Task<bool> WiderrufenAsync(Guid mitgliedId, Guid artId, DateOnly am, string durch, CancellationToken ct = default)
    {
        var aktuell = Einwilligungsregeln.Aktuell(await db.Einwilligungen.Where(e => e.MitgliedId == mitgliedId && e.EinwilligungsartId == artId).ToListAsync(ct));
        if (aktuell is not { Erteilt: true, WiderrufenAm: null })
        {
            return false;
        }

        aktuell.WiderrufenAm = am;
        aktuell.WiderrufenDurch = durch;
        return true;
    }

    /// <summary>Der Jugendliche stimmt der Erklärung seiner Eltern selbst zu.</summary>
    /// <param name="mitgliedId">Das Mitglied.</param>
    /// <param name="artId">Die Art.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns><c>true</c>, wenn eine wirksame Erklärung der Eltern bestätigt wurde.</returns>
    public async Task<bool> ZustimmenAsync(Guid mitgliedId, Guid artId, CancellationToken ct = default)
    {
        var aktuell = Einwilligungsregeln.Aktuell(await db.Einwilligungen.Where(e => e.MitgliedId == mitgliedId && e.EinwilligungsartId == artId).ToListAsync(ct));
        if (aktuell is not { Erteilt: true, WiderrufenAm: null, Durch: EinwilligungDurch.Erziehungsberechtigter })
        {
            return false;
        }

        aktuell.JugendlicherZugestimmt = true;
        return true;
    }

    /// <summary>
    /// Die Einwilligungen des angenommenen Antrags gehen je Art zum Tag der Unterschrift ins Register. Bei Minderjährigen erklären die
    /// Eltern; unterschreibt die Person selbst mit (Übertritt), hat sie zugestimmt.
    /// </summary>
    /// <param name="antrag">Der Antrag mit Erziehungsberechtigten und Einwilligungen.</param>
    /// <param name="mitglied">Das aufgenommene oder bestehende Mitglied.</param>
    /// <param name="unterschrieben">Tag der Unterschrift.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task AusAntragAsync(Antrag antrag, Mitglied mitglied, DateOnly unterschrieben, CancellationToken ct = default)
    {
        if (antrag.Einwilligungen.Count == 0)
        {
            return;
        }

        var grenzen = await GrenzenAsync(unterschrieben, ct);
        var minderjaehrig = Mitglied.Alter(antrag.Geburtsdatum, unterschrieben) < grenzen.VolljaehrigAb;
        var eltern = string.Join(", ", antrag.Erziehungsberechtigte.OrderBy(e => e.Nummer).Select(e => $"{e.Vorname} {e.Nachname}"));
        foreach (var antwort in antrag.Einwilligungen)
        {
            await EintragenAsync(new Einwilligung
            {
                Mitglied = mitglied,
                MitgliedId = mitglied.Id,
                EinwilligungsartId = antwort.EinwilligungsartId,
                Erteilt = antwort.Erteilt,
                Am = unterschrieben,
                Durch = minderjaehrig ? EinwilligungDurch.Erziehungsberechtigter : EinwilligungDurch.Mitglied,
                DurchName = minderjaehrig && eltern.Length > 0 ? eltern : $"{antrag.Vorname} {antrag.Nachname}",
                Weg = EinwilligungWeg.Antrag,
                Nachweis = $"Antrag vom {unterschrieben:dd.MM.yyyy}",
                JugendlicherZugestimmt = minderjaehrig && Antragsregeln.UnterschriftPerson(antrag.Art) ? true : null,
            }, ct);
        }
    }
}
