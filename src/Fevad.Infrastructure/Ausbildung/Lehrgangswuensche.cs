using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Ausbildung;

/// <summary>
/// Lehrgangswünsche (V54): Mitglieder tragen Wünsche ein, die Voraussetzungen werden geprüft, der Kommandant setzt Priorität und Stand und
/// gibt den Bedarf im Herbst an den Kreisbrandmeister. Mit der Anmeldung entsteht die Lehrgangsteilnahme.
/// </summary>
/// <param name="db">Datenbankkontext.</param>
public sealed class Lehrgangswuensche(AppDbContext db)
{
    /// <summary>Wie viele Jahre im Voraus ein Wunsch eingetragen werden kann.</summary>
    public const int JahreVoraus = 2;

    /// <summary>Ein Lehrgang mit dem Prüfergebnis für ein Mitglied.</summary>
    /// <param name="Lehrgang">Der Lehrgang.</param>
    /// <param name="Vorhanden">Das Mitglied hat ihn schon mit gültigem Nachweis.</param>
    /// <param name="Voraussetzungen">Geprüfte Voraussetzungen.</param>
    public sealed record Angebot(Qualifikation Lehrgang, bool Vorhanden, IReadOnlyList<Voraussetzung> Voraussetzungen)
    {
        /// <summary>Alle Voraussetzungen sind heute erfüllt.</summary>
        public bool Erfuellt => Voraussetzungen.All(v => v.Erfuellt);
    }

    /// <summary>Ein Wunsch mit Mitglied und Prüfergebnis.</summary>
    /// <param name="Wunsch">Der Wunsch mit Qualifikation.</param>
    /// <param name="Nachname">Nachname.</param>
    /// <param name="Vorname">Vorname.</param>
    /// <param name="Geburtsdatum">Geburtsdatum, für die Anmeldung im Bildungsportal.</param>
    /// <param name="Voraussetzungen">Geprüfte Voraussetzungen.</param>
    public sealed record Zeile(Lehrgangswunsch Wunsch, string Nachname, string Vorname, DateOnly Geburtsdatum, IReadOnlyList<Voraussetzung> Voraussetzungen)
    {
        /// <summary>Alle Voraussetzungen sind heute erfüllt.</summary>
        public bool Erfuellt => Voraussetzungen.All(v => v.Erfuellt);

        /// <summary>Nicht erfüllte Voraussetzungen als Text.</summary>
        public string Fehlend => string.Join("; ", Voraussetzungen.Where(v => !v.Erfuellt).Select(v => v.Text));
    }

    /// <summary>Mitglieder, die Lehrgänge wünschen können: heute aktiv oder in Probezeit in der Einsatzabteilung.</summary>
    /// <param name="heute">Heutiges Datum.</param>
    /// <returns>Abfrage der Mitglieder.</returns>
    public IQueryable<Mitglied> Berechtigte(DateOnly heute) =>
        db.Mitglieder.Where(m => m.Mitgliedschaften.Any(s => s.Abteilung!.Art == AbteilungArt.Einsatz && s.GueltigAb <= heute
            && (s.GueltigBis == null || s.GueltigBis >= heute) && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit)));

    /// <summary>Alle aktiven Lehrgänge mit Prüfergebnis für ein Mitglied.</summary>
    /// <param name="mitgliedId">Kennung des Mitglieds.</param>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Angebote in Katalogreihenfolge.</returns>
    public async Task<IReadOnlyList<Angebot>> AngeboteAsync(Guid mitgliedId, DateOnly heute, CancellationToken ct = default)
    {
        var mitglied = await db.Mitglieder.AsNoTracking().SingleAsync(m => m.Id == mitgliedId, ct);
        var (lehrgaenge, namen) = await KatalogAsync(ct);
        var nachweise = await NachweiseAsync([mitgliedId], ct);
        var eigene = nachweise[mitgliedId].ToList();
        return lehrgaenge.Select(l => new Angebot(l, Lehrgangswunsch.Vorhanden(l, heute, eigene),
            Lehrgangswunsch.Pruefen(l, heute, mitglied.Geburtsdatum, eigene, namen))).ToList();
    }

    /// <summary>Trägt einen Wunsch ein, selbst oder durch den Kommandanten für ein Mitglied ohne eigenen Zugang.</summary>
    /// <param name="mitgliedId">Kennung des Mitglieds.</param>
    /// <param name="qualifikationId">Kennung des Lehrgangs.</param>
    /// <param name="jahr">Wunschjahr.</param>
    /// <param name="begruendung">Begründung des Mitglieds.</param>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Fehlermeldung oder <c>null</c>, wenn eingetragen.</returns>
    public async Task<string?> EintragenAsync(Guid mitgliedId, Guid qualifikationId, int jahr, string? begruendung, DateOnly heute, CancellationToken ct = default)
    {
        if (jahr < heute.Year || jahr > heute.Year + JahreVoraus)
        {
            return $"Das Wunschjahr muss zwischen {heute.Year} und {heute.Year + JahreVoraus} liegen.";
        }

        if (!await Berechtigte(heute).AnyAsync(m => m.Id == mitgliedId, ct))
        {
            return "Lehrgänge können nur Angehörige der Einsatzabteilung wünschen.";
        }

        var angebot = (await AngeboteAsync(mitgliedId, heute, ct)).FirstOrDefault(a => a.Lehrgang.Id == qualifikationId);
        if (angebot is null)
        {
            return "Diesen Lehrgang gibt es nicht.";
        }

        if (angebot.Vorhanden)
        {
            return $"{angebot.Lehrgang.Bezeichnung} ist schon mit gültigem Nachweis eingetragen.";
        }

        var offen = await db.Lehrgangswuensche.AnyAsync(w => w.MitgliedId == mitgliedId && w.QualifikationId == qualifikationId
            && (w.Status == WunschStatus.Gewuenscht || w.Status == WunschStatus.Warteliste || w.Status == WunschStatus.Angemeldet), ct);
        if (offen)
        {
            return $"Für {angebot.Lehrgang.Bezeichnung} gibt es schon einen offenen Wunsch.";
        }

        var text = string.IsNullOrWhiteSpace(begruendung) ? null : begruendung.Trim();
        db.Lehrgangswuensche.Add(new Lehrgangswunsch
        {
            MitgliedId = mitgliedId, QualifikationId = qualifikationId, Jahr = jahr, EingetragenAm = heute, Status = WunschStatus.Gewuenscht,
            Begruendung = text is { Length: > 500 } ? text[..500] : text,
        });
        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Alle Wünsche eines Jahres mit Prüfergebnis, nach Priorität, Lehrgang und Name.</summary>
    /// <param name="jahr">Wunschjahr.</param>
    /// <param name="heute">Heutiges Datum (Stichtag der Prüfung).</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zeilen.</returns>
    public async Task<IReadOnlyList<Zeile>> JahrAsync(int jahr, DateOnly heute, CancellationToken ct = default)
    {
        var wuensche = await db.Lehrgangswuensche.AsNoTracking().Include(w => w.Qualifikation).Include(w => w.Mitglied)
            .Where(w => w.Jahr == jahr).ToListAsync(ct);
        var (_, namen) = await KatalogAsync(ct);
        var nachweise = await NachweiseAsync(wuensche.Select(w => w.MitgliedId).Distinct().ToList(), ct);
        return wuensche
            .Select(w => new Zeile(w, w.Mitglied!.Nachname, w.Mitglied.Vorname, w.Mitglied.Geburtsdatum,
                Lehrgangswunsch.Pruefen(w.Qualifikation!, heute, w.Mitglied.Geburtsdatum, nachweise[w.MitgliedId], namen)))
            .OrderBy(z => z.Wunsch.Offen ? 0 : 1).ThenBy(z => z.Wunsch.Prioritaet ?? int.MaxValue).ThenBy(z => z.Wunsch.Qualifikation!.Sortierung)
            .ThenBy(z => z.Nachname).ThenBy(z => z.Vorname).ToList();
    }

    /// <summary>Setzt Stand, Priorität und Bemerkung. Mit „angemeldet“ oder „bestätigt“ entsteht die Lehrgangsteilnahme, falls noch keine da ist.</summary>
    /// <param name="wunsch">Der Wunsch (verfolgt, mit Teilnahme).</param>
    /// <param name="status">Neuer Stand.</param>
    /// <param name="prioritaet">Priorität, 1 ist die höchste.</param>
    /// <param name="bemerkung">Bemerkung des Kommandanten.</param>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Aufgabe.</returns>
    public async Task StandSetzenAsync(Lehrgangswunsch wunsch, WunschStatus status, int? prioritaet, string? bemerkung, DateOnly heute, CancellationToken ct = default)
    {
        wunsch.Status = status;
        wunsch.Prioritaet = prioritaet is > 0 ? prioritaet : null;
        var text = string.IsNullOrWhiteSpace(bemerkung) ? null : bemerkung.Trim();
        wunsch.Bemerkung = text is { Length: > 500 } ? text[..500] : text;
        if (status is WunschStatus.Angemeldet or WunschStatus.Bestaetigt)
        {
            if (wunsch.Teilnahme is null)
            {
                // Über das DbSet anlegen: die Id steht schon fest, über die Navigation hielte EF die Teilnahme für vorhanden.
                var teilnahme = new Lehrgangsteilnahme { MitgliedId = wunsch.MitgliedId, QualifikationId = wunsch.QualifikationId, AngemeldetAm = heute };
                db.Lehrgangsteilnahmen.Add(teilnahme);
                wunsch.TeilnahmeId = teilnahme.Id;
                wunsch.Teilnahme = teilnahme;
            }

            if (status == WunschStatus.Bestaetigt && wunsch.Teilnahme.ZugewiesenAm is null)
            {
                wunsch.Teilnahme.ZugewiesenAm = heute;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Vermerkt alle offenen, noch nicht gemeldeten Wünsche eines Jahres für Kreis- und Landeslehrgänge als an den Kreisbrandmeister gemeldet.</summary>
    /// <param name="jahr">Wunschjahr.</param>
    /// <param name="heute">Tag der Meldung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Anzahl der vermerkten Wünsche.</returns>
    public async Task<int> GemeldetAsync(int jahr, DateOnly heute, CancellationToken ct = default) =>
        await Meldbar(db.Lehrgangswuensche, jahr).Where(w => w.GemeldetAm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.GemeldetAm, heute), ct);

    /// <summary>
    /// Herbstmeldung an den Kreisbrandmeister als CSV: offene Wünsche für Kreis- und Landeslehrgänge mit Priorität und den Angaben, die das
    /// Bildungsportal der Landesfeuerwehrschule zur Anmeldung braucht. Begründungen und Bemerkungen bleiben intern.
    /// </summary>
    /// <param name="feuerwehr">Name der Feuerwehr.</param>
    /// <param name="jahr">Wunschjahr.</param>
    /// <param name="zeilen">Die Zeilen des Jahres.</param>
    /// <returns>UTF-8 mit BOM, Semikolon getrennt.</returns>
    public static byte[] Herbstmeldung(string feuerwehr, int jahr, IEnumerable<Zeile> zeilen)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Lehrgangsbedarf {jahr};{Feld(feuerwehr)}");
        sb.AppendLine("Priorität;Lehrgang;Ebene;Nachname;Vorname;Geburtsdatum;Voraussetzungen erfüllt;Stand");
        foreach (var z in zeilen.Where(z => z.Wunsch.Offen && z.Wunsch.Qualifikation!.Ebene is AusbildungsEbene.Kreis or AusbildungsEbene.Land))
        {
            var w = z.Wunsch;
            sb.AppendLine(string.Join(';', w.Prioritaet?.ToString() ?? "", Feld(w.Qualifikation!.Bezeichnung), Text(w.Qualifikation.Ebene), Feld(z.Nachname),
                Feld(z.Vorname), z.Geburtsdatum.ToString("dd.MM.yyyy"), z.Erfuellt ? "ja" : "nein: " + Feld(z.Fehlend), Text(w.Status)));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static IQueryable<Lehrgangswunsch> Meldbar(IQueryable<Lehrgangswunsch> menge, int jahr) =>
        menge.Where(w => w.Jahr == jahr && (w.Status == WunschStatus.Gewuenscht || w.Status == WunschStatus.Warteliste || w.Status == WunschStatus.Angemeldet)
            && (w.Qualifikation!.Ebene == AusbildungsEbene.Kreis || w.Qualifikation.Ebene == AusbildungsEbene.Land));

    private async Task<(List<Qualifikation> Lehrgaenge, Dictionary<string, string> Namen)> KatalogAsync(CancellationToken ct)
    {
        var alle = await db.Qualifikationen.AsNoTracking().Include(q => q.Vorstufe).OrderBy(q => q.Sortierung).ToListAsync(ct);
        return (alle.Where(q => q.Aktiv && q.Art == QualifikationArt.Lehrgang).ToList(), alle.ToDictionary(q => q.Code.ToUpperInvariant(), q => q.Bezeichnung));
    }

    private async Task<ILookup<Guid, QualifikationNachweis>> NachweiseAsync(List<Guid> ids, CancellationToken ct) =>
        (await db.QualifikationNachweise.AsNoTracking().Include(n => n.Qualifikation).Where(n => ids.Contains(n.MitgliedId)).ToListAsync(ct))
        .ToLookup(n => n.MitgliedId);

    // Semikolon und Zeilenumbruch würden die Spalten verschieben.
    private static string Feld(string? text) => (text ?? "").Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ');

    private static string Text(Enum? wert) =>
        wert is null ? "" : wert.GetType().GetField(wert.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? wert.ToString();
}
