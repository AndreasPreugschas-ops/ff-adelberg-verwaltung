using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Fristen;

/// <summary>Lädt die Daten für <see cref="Abzeichenpruefung"/> und <see cref="Leistungsabzeichen"/>.</summary>
public sealed class Abzeichenabfrage(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Planungshilfe für eine Abnahme: je Mitglied Voraussetzungen der Zielstufe und mögliche Funktionen.</summary>
    public sealed record Zeile(
        Guid MitgliedId,
        string Mitglied,
        bool SchonErworben,
        IReadOnlyList<Voraussetzung> Voraussetzungen,
        IReadOnlyList<Funktionspruefung> Funktionen)
    {
        /// <summary>Das Abzeichen ist noch nicht erworben und alle Voraussetzungen sind erfüllt.</summary>
        public bool Erfuellt => !SchonErworben && Voraussetzungen.All(v => v.Erfuellt);
    }

    /// <summary>Codes mit Bezeichnung für Texte wie "Truppmann Teil 1 oder Sprechfunker".</summary>
    public async Task<IReadOnlyDictionary<string, string>> NamenAsync(CancellationToken ct) =>
        await db.Qualifikationen.AsNoTracking().ToDictionaryAsync(q => q.Code, q => q.Bezeichnung, ct);

    /// <summary>Voraussetzungen eines Abzeichens für ein Mitglied am Abnahmetag. <paramref name="ziel"/> braucht die geladene Vorstufe.</summary>
    /// <param name="mitgliedId">Kennung des Mitglieds.</param>
    /// <param name="ziel">Das angestrebte Abzeichen.</param>
    /// <param name="abnahmetag">Tag der Abnahme.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <param name="regel">Regeln der Stufe im gewählten Regelwerk (V50a); ohne Angabe gilt der Katalog.</param>
    /// <returns>Die geprüften Bedingungen.</returns>
    public async Task<IReadOnlyList<Voraussetzung>> PruefenAsync(Guid mitgliedId, Qualifikation ziel, DateOnly abnahmetag, CancellationToken ct, AbzeichenregelwerkStufe? regel = null)
    {
        var mitglied = await db.Mitglieder.AsNoTracking().Include(m => m.Mitgliedschaften).ThenInclude(s => s.Abteilung)
            .FirstAsync(m => m.Id == mitgliedId, ct);
        var vorhandene = await db.QualifikationNachweise.AsNoTracking().Include(n => n.Qualifikation)
            .Where(n => n.MitgliedId == mitgliedId).ToListAsync(ct);
        return Abzeichenpruefung.Pruefen(ziel, abnahmetag, mitglied.Geburtsdatum, vorhandene, JugendSeit(mitglied), await NamenAsync(ct), regel);
    }

    /// <summary>Alle aktiven Mitglieder der Einsatzabteilung für die Abnahme von <paramref name="ziel"/>.</summary>
    public async Task<IReadOnlyList<Zeile>> PlanenAsync(Qualifikation ziel, DateOnly abnahmetag, bool mitAtemschutz, CancellationToken ct)
    {
        var mitglieder = await db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s =>
                s.Abteilung!.Art == AbteilungArt.Einsatz
                && s.GueltigAb <= abnahmetag && (s.GueltigBis == null || s.GueltigBis >= abnahmetag)
                && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit)))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, m.Nachname, m.Vorname, m.Geburtsdatum })
            .ToListAsync(ct);
        var ids = mitglieder.Select(m => m.Id).ToList();

        var qualifikationen = (await db.QualifikationNachweise.AsNoTracking().Include(n => n.Qualifikation)
            .Where(n => ids.Contains(n.MitgliedId)).ToListAsync(ct)).ToLookup(n => n.MitgliedId);
        var vorsorgen = mitAtemschutz
            ? (await db.AtemschutzVorsorgen.AsNoTracking().Where(v => ids.Contains(v.MitgliedId)).ToListAsync(ct)).ToLookup(v => v.MitgliedId)
            : null;
        var atemschutz = mitAtemschutz
            ? (await db.AtemschutzNachweise.AsNoTracking().Where(n => ids.Contains(n.MitgliedId)).ToListAsync(ct)).ToLookup(n => n.MitgliedId)
            : null;
        var namen = await NamenAsync(ct);
        var monate = await parameter.ZahlAsync(ParameterSchluessel.LeistungsabzeichenBelastungsuebungMonate, abnahmetag, 12, ct);

        return mitglieder.Select(m =>
        {
            var vorhandene = qualifikationen[m.Id].ToList();
            return new Zeile(
                m.Id,
                $"{m.Nachname}, {m.Vorname}",
                vorhandene.Any(n => n.QualifikationId == ziel.Id),
                Abzeichenpruefung.Pruefen(ziel, abnahmetag, m.Geburtsdatum, vorhandene, null, namen),
                Leistungsabzeichen.Funktionen(abnahmetag, vorhandene, vorsorgen?[m.Id].ToList(), atemschutz?[m.Id].ToList(), monate));
        }).ToList();
    }

    /// <summary>Regeln einer Abzeichenstufe in einem Regelwerk; null, wenn das Regelwerk die Stufe nicht kennt oder keines gewählt ist.</summary>
    /// <param name="regelwerkId">Kennung des Regelwerks.</param>
    /// <param name="qualifikationId">Kennung der Abzeichenstufe.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Regeln der Stufe oder null.</returns>
    public async Task<AbzeichenregelwerkStufe?> RegelAsync(Guid? regelwerkId, Guid qualifikationId, CancellationToken ct) =>
        regelwerkId is { } id
            ? await db.AbzeichenregelwerkStufen.AsNoTracking().FirstOrDefaultAsync(s => s.RegelwerkId == id && s.QualifikationId == qualifikationId, ct)
            : null;

    /// <summary>Erster Eintritt in die Jugendfeuerwehr.</summary>
    public static DateOnly? JugendSeit(Mitglied mitglied) =>
        mitglied.Mitgliedschaften
            .Where(s => s.Abteilung?.Art == AbteilungArt.Jugend)
            .Min(s => (DateOnly?)s.GueltigAb);
}
