using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>
/// Anbietung an das Archiv vor dem Löschen (V65, Landesarchivgesetz BW § 4 Abs. 3): Der Löschlauf fragt hier, welche fälligen Unterlagen
/// er löschen darf. Neue fällige Unterlagen kommen ins Anbietungsverzeichnis und bleiben gesperrt, bis das Archiv entschieden hat,
/// das Archivpaket übergeben ist oder die Entscheidungsfrist nach dem Anbieten abgelaufen ist.
/// </summary>
public sealed class Archivierung(AppDbContext db, ParameterLeser parameter)
{
    /// <summary>Wert im Bewertungskatalog für Unterlagen, die das Archiv pauschal als nicht archivwürdig bewertet hat.</summary>
    public const string Kassabel = "kassabel";

    /// <summary>Eine Zeile des Bewertungskatalogs.</summary>
    /// <param name="Art">Art der Unterlage.</param>
    /// <param name="Regel">Regel im Löschkonzept.</param>
    /// <param name="FristSchluessel">Parameter der Aufbewahrungsfrist.</param>
    /// <param name="Frist">Wovon die Frist läuft.</param>
    /// <param name="Rechtsgrundlage">Rechtsgrundlage der Aufbewahrung und Anbietung.</param>
    public sealed record Bewertung(Archivgut Art, string Regel, string FristSchluessel, string Frist, string Rechtsgrundlage);

    /// <summary>Bewertungskatalog: welche Unterlagen der Löschlauf anbietet, mit Frist und Rechtsgrundlage.</summary>
    public static readonly IReadOnlyList<Bewertung> Katalog =
    [
        new(Archivgut.Mitgliederakte, "L7", ParameterSchluessel.RestdatensatzAufbewahrungJahre, "Jahre nach dem Austritt",
            "Feuerwehrgesetz BW (Personalunterlagen); Anbietung § 4 Abs. 1 und 3, § 12 Abs. 2 LArchG BW"),
        new(Archivgut.Kassenjahr, "L42", ParameterSchluessel.KassendatenAufbewahrungJahre, "Jahre nach dem Ende des Kassenjahrs",
            "Kassen- und Haushaltsrecht der Gemeinde; Anbietung § 4 Abs. 1 und 3, § 12 Abs. 2 LArchG BW"),
        new(Archivgut.Mitgliederbescheid, "L49", ParameterSchluessel.MitgliederbescheideLoeschenNachJahren, "Jahre nach der Bekanntgabe",
            "Verwaltungsakte der Gemeinde (LVwVfG); Anbietung § 4 Abs. 1 und 3, § 12 Abs. 2 LArchG BW"),
        new(Archivgut.Einsatzbericht, "L2", ParameterSchluessel.EinsatzberichteAufbewahrungJahre, "Jahre nach dem Ende des Einsatzjahrs",
            "Einsatzdokumentation und Kostenersatz (§ 34 FwG); Anbietung § 4 Abs. 1 und 3, § 12 Abs. 2 LArchG BW; danach auf die Statistik verdichtet"),
    ];

    /// <summary>Eine fällige Unterlage.</summary>
    /// <param name="Id">Schlüssel des Datensatzes.</param>
    /// <param name="Bezeichnung">Bezeichnung im Anbietungsverzeichnis.</param>
    /// <param name="Jahr">Jahr der Entstehung bzw. des Abschlusses.</param>
    public sealed record Kandidat(Guid Id, string Bezeichnung, int Jahr);

    /// <summary>Muss diese Art nach dem Bewertungskatalog angeboten werden?</summary>
    /// <param name="art">Art der Unterlage.</param>
    /// <param name="heute">Stichtag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>true, wenn sie nicht als kassabel bewertet ist.</returns>
    public async Task<bool> AnbietenAsync(Archivgut art, DateOnly heute, CancellationToken ct) =>
        !string.Equals(await parameter.WertAsync(ParameterSchluessel.ArchivBewertungPraefix + art, heute, ct), Kassabel, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Nimmt fällige Unterlagen ins Anbietungsverzeichnis auf und liefert die, die jetzt gelöscht werden dürfen. Was der Bewertungskatalog als
    /// kassabel führt, ist sofort frei.
    /// </summary>
    /// <param name="art">Art der Unterlagen.</param>
    /// <param name="faellige">Unterlagen, deren Aufbewahrungsfrist abgelaufen ist.</param>
    /// <param name="heute">Stichtag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Schlüssel der Unterlagen, die gelöscht werden dürfen.</returns>
    public async Task<IReadOnlySet<Guid>> FreigabeAsync(Archivgut art, IReadOnlyCollection<Kandidat> faellige, DateOnly heute, CancellationToken ct)
    {
        if (faellige.Count == 0)
        {
            return new HashSet<Guid>();
        }

        if (!await AnbietenAsync(art, heute, ct))
        {
            return faellige.Select(k => k.Id).ToHashSet();
        }

        var frist = await parameter.ZahlAsync(ParameterSchluessel.ArchivEntscheidungFristMonate, heute, 12, ct);
        var ids = faellige.Select(k => k.Id).ToList();
        var vorhanden = await db.Archivangebote.Where(a => a.Art == art && a.ErledigtAm == null && ids.Contains(a.DatensatzId)).ToListAsync(ct);
        foreach (var k in faellige.Where(k => vorhanden.All(a => a.DatensatzId != k.Id)))
        {
            db.Archivangebote.Add(new Archivangebot { Art = art, DatensatzId = k.Id, Bezeichnung = k.Bezeichnung, Jahr = k.Jahr, FaelligAm = heute });
        }

        await db.SaveChangesAsync(ct);
        return vorhanden.Where(a => a.DarfLoeschen(heute, frist)).Select(a => a.DatensatzId).ToHashSet();
    }

    /// <summary>Vermerkt die Löschung; die Bezeichnung verliert ihren Personenbezug, der Eintrag bleibt als Nachweis.</summary>
    /// <param name="art">Art der Unterlagen.</param>
    /// <param name="ids">Gelöschte Datensätze.</param>
    /// <param name="heute">Stichtag.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task ErledigtAsync(Archivgut art, IReadOnlyCollection<Guid> ids, DateOnly heute, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var angebote = await db.Archivangebote.Where(a => a.Art == art && a.ErledigtAm == null && ids.Contains(a.DatensatzId)).ToListAsync(ct);
        foreach (var a in angebote)
        {
            a.ErledigtAm = heute;
            a.Bezeichnung = $"{Anzeigename(art)} aus {a.Jahr} (gelöscht)";
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Anzeigename eines Enum-Werts aus dem Display-Attribut.</summary>
    /// <param name="wert">Der Wert.</param>
    /// <returns>Der Name.</returns>
    public static string Text(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
            .OfType<System.ComponentModel.DataAnnotations.DisplayAttribute>().FirstOrDefault()?.GetName() ?? wert.ToString();

    /// <summary>Anzeigename einer Art für Verzeichnis und Nachweis.</summary>
    /// <param name="art">Art der Unterlage.</param>
    /// <returns>Der Name, z. B. „Mitgliederakte“.</returns>
    public static string Anzeigename(Archivgut art) => art switch
    {
        Archivgut.Mitgliederakte => "Mitgliederakte",
        Archivgut.Kassenjahr => "Kassenjahr",
        Archivgut.Mitgliederbescheid => "Bescheid an Mitglied",
        _ => "Einsatzbericht",
    };
}
