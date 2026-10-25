using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Berichte der Kassen als PDF mit dem Briefkopf aus den Stammdaten: Jahresabschluss (V05), Prüfbericht der Rechnungsprüfer (V05) und
/// Dankschreiben an Spender (V08). Die Seiten der Kasse und die Mitgliedssicht (V09) nutzen dieselben Bausteine. Rechte prüft der Aufrufer.
/// </summary>
public class Kassenberichte(AppDbContext db, Kassenabschluss abschluss, Kassenfuehrung kassen, TimeProvider zeit)
{
    /// <summary>Eine erzeugte Datei.</summary>
    /// <param name="Inhalt">Das PDF.</param>
    /// <param name="Name">Dateiname zum Herunterladen.</param>
    public sealed record Datei(byte[] Inhalt, string Name);

    /// <summary>Briefkopf aus den Stammdaten der Organisation.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Name, Anschrift und Logo der Feuerwehr.</returns>
    public async Task<AntragPdf.Kopfdaten> KopfAsync(CancellationToken ct = default)
    {
        var organisation = await db.Organisationen.AsNoTracking().FirstAsync(ct);
        return new AntragPdf.Kopfdaten(organisation.FeuerwehrName, organisation.FeuerwehrAnschrift, null, null, organisation.Logo, organisation.LogoInhaltstyp);
    }

    /// <summary>Jahresabschluss einer Kasse als PDF; vor dem Abschluss als Entwurf.</summary>
    /// <param name="kasse">Die Kasse.</param>
    /// <param name="jahr">Das Haushaltsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei.</returns>
    public async Task<Datei> JahresabschlussAsync(Kasse kasse, int jahr, CancellationToken ct = default)
    {
        var rechnung = await abschluss.RechnungAsync(kasse, jahr, ct);
        var pdf = JahresabschlussPdf.Erzeugen(await KopfAsync(ct), rechnung, Anzeige.DatumZeit(zeit.GetUtcNow()),
            rechnung.Abschluss is { } a ? Anzeige.Datum(Anzeige.LokalesDatum(a.AbgeschlossenAm)) : null);
        return new Datei(pdf, JahresabschlussPdf.Dateiname(rechnung));
    }

    /// <summary>Prüfbericht als PDF mit den Zahlen der Kasse im geprüften Zeitraum.</summary>
    /// <param name="pruefung">Die Prüfung samt Prüfern und Beanstandungen (mit Buchung).</param>
    /// <param name="kasse">Die Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei.</returns>
    public async Task<Datei> PruefberichtAsync(Kassenpruefung pruefung, Kasse kasse, CancellationToken ct = default)
    {
        var buchungen = await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasse.Id && b.Datum >= pruefung.Von && b.Datum <= pruefung.Bis)
            .ToListAsync(ct);
        var konten = (await kassen.KontostaendeAsync(kasse.Id, pruefung.Bis, ct)).Where(k => k.Konto.Aktiv || k.Bestand != 0)
            .Select(k => (k.Konto.Bezeichnung, k.Bestand)).ToList();
        var daten = new PruefberichtPdf.Daten(await KopfAsync(ct), kasse.Name, pruefung,
            buchungen.Where(b => b.Art == BuchungArt.Einnahme).Sum(Kassenregeln.Ist), buchungen.Where(b => b.Art == BuchungArt.Ausgabe).Sum(Kassenregeln.Ist),
            buchungen.Count, konten, buchungen.Count(b => b.AusnahmeBegruendung is not null),
            pruefung.Pruefer1BestaetigtAm is { } b1 ? Anzeige.DatumZeit(b1) : null, pruefung.Pruefer2BestaetigtAm is { } b2 ? Anzeige.DatumZeit(b2) : null);
        return new Datei(PruefberichtPdf.Erzeugen(daten), PruefberichtPdf.Dateiname(daten));
    }

    /// <summary>Dankschreiben an den Geber mit dem Hinweis, dass die Kasse keine Zuwendungsbestätigung ausstellt.</summary>
    /// <param name="zuwendung">Die Zuwendung.</param>
    /// <param name="kasse">Die Kasse.</param>
    /// <param name="unterzeichner">Name unter dem Schreiben, meist der Kassenwart oder Kommandant.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei.</returns>
    public async Task<Datei> DankschreibenAsync(Zuwendung zuwendung, Kasse kasse, string? unterzeichner, CancellationToken ct = default)
    {
        var d = new DankschreibenPdf.Daten(await KopfAsync(ct), kasse.Name, zuwendung, Anzeige.Heute(zeit), unterzeichner);
        return new Datei(DankschreibenPdf.Erzeugen(d), DankschreibenPdf.Dateiname(d));
    }
}
