using Fevad.Domain.Dokumente;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;
using Fevad.Domain.Kassen;
using Fevad.Domain.Unfaelle;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>Auswahllisten und Belege für die Kassenseiten.</summary>
public static class Kassenauswahl
{
    /// <summary>Aktive Konten einer Kasse; das bisher gewählte bleibt in der Liste, auch wenn es inzwischen aufgelöst ist.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="kasseId">Die Kasse.</param>
    /// <param name="gewaehlt">Bisher gewählte Konten.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Einträge für ein <c>select</c>-Feld.</returns>
    public static async Task<IReadOnlyList<SelectListItem>> KontenAsync(AppDbContext db, Guid kasseId, IEnumerable<Guid?> gewaehlt, CancellationToken ct)
    {
        var ids = gewaehlt.Where(g => g is not null).Select(g => g!.Value).ToList();
        return await db.Zahlungsmittelkonten.AsNoTracking()
            .Where(k => k.KasseId == kasseId && (k.Aktiv || ids.Contains(k.Id)))
            .OrderBy(k => k.Sortierung).ThenBy(k => k.Bezeichnung)
            .Select(k => new SelectListItem(k.Bezeichnung, k.Id.ToString()))
            .ToListAsync(ct);
    }

    /// <summary>Aktive Positionen einer Kasse, nach Einnahmen und Ausgaben gruppiert.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="kasseId">Die Kasse.</param>
    /// <param name="gewaehlt">Bisher gewählte Position.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Einträge für ein <c>select</c>-Feld.</returns>
    public static async Task<IReadOnlyList<SelectListItem>> PositionenAsync(AppDbContext db, Guid kasseId, Guid? gewaehlt, CancellationToken ct)
    {
        var positionen = await db.Planpositionen.AsNoTracking()
            .Where(p => p.KasseId == kasseId && (p.Aktiv || p.Id == gewaehlt))
            .ToListAsync(ct);
        var gruppen = new Dictionary<Planrichtung, SelectListGroup>
        {
            [Planrichtung.Einnahme] = new() { Name = "Einnahmen" },
            [Planrichtung.Ausgabe] = new() { Name = "Ausgaben" },
        };
        return positionen.OrderBy(p => p.Richtung).ThenBy(p => p.Nummer, StringComparer.Ordinal)
            .Select(p => new SelectListItem($"{p.Nummer} {p.Bezeichnung}", p.Id.ToString()) { Group = gruppen[p.Richtung] })
            .ToList();
    }

    /// <summary>Angenommene Beschlüsse der letzten beiden Jahre und des laufenden Jahres für den Verweis bei Ausgaben über der Grenze.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <param name="gewaehlt">Bisher gewählter Beschluss.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Einträge für ein <c>select</c>-Feld, neueste zuerst.</returns>
    public static async Task<IReadOnlyList<SelectListItem>> BeschluesseAsync(AppDbContext db, DateOnly heute, Guid? gewaehlt, CancellationToken ct)
    {
        var ab = heute.Year - 2;
        var liste = await db.Beschluesse.AsNoTracking()
            .Where(b => (b.Jahr >= ab && b.Ergebnis == BeschlussErgebnis.Angenommen) || b.Id == gewaehlt)
            .OrderByDescending(b => b.Jahr).ThenByDescending(b => b.Nummer)
            .Select(b => new { b.Id, b.Jahr, b.Nummer, b.Gegenstand })
            .ToListAsync(ct);
        return liste.Select(b => new SelectListItem($"{b.Jahr}/{b.Nummer:00} {Kuerzen(b.Gegenstand, 80)}", b.Id.ToString())).ToList();
    }

    /// <summary>Veranstaltungen einer Kasse ab dem Vorjahr, die noch nicht abgerechnet sind; die bisher gewählte bleibt in der Liste.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="kasseId">Die Kasse.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <param name="gewaehlt">Bisher gewählte Veranstaltung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Einträge für ein <c>select</c>-Feld, neueste zuerst.</returns>
    public static async Task<IReadOnlyList<SelectListItem>> VeranstaltungenAsync(AppDbContext db, Guid kasseId, DateOnly heute, Guid? gewaehlt, CancellationToken ct)
    {
        var ab = new DateOnly(heute.Year - 1, 1, 1);
        var liste = await db.Veranstaltungen.AsNoTracking()
            .Where(v => v.KasseId == kasseId && ((v.Beginn >= ab && !v.Abgerechnet) || v.Id == gewaehlt))
            .OrderByDescending(v => v.Beginn)
            .Select(v => new { v.Id, v.Name, v.Beginn })
            .ToListAsync(ct);
        return liste.Select(v => new SelectListItem($"{v.Name} ({Anzeige.Datum(v.Beginn)})", v.Id.ToString())).ToList();
    }

    /// <summary>Liest hochgeladene Belege: PDF, PNG oder JPEG bis zur Größe aus dem Parameter; Bilder ohne Metadaten.</summary>
    /// <param name="dateien">Hochgeladene Dateien; leere werden übergangen.</param>
    /// <param name="parameter">Parameter.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <param name="modelState">Für Fehlermeldungen.</param>
    /// <param name="feld">Name des Felds für Fehlermeldungen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Belege ohne Bezug.</returns>
    public static async Task<List<Kassenbeleg>> BelegeLesenAsync(IReadOnlyList<IFormFile> dateien, ParameterLeser parameter, DateOnly heute,
        ModelStateDictionary modelState, string feld, CancellationToken ct)
    {
        var belege = new List<Kassenbeleg>();
        var megabyte = await parameter.ZahlAsync(ParameterSchluessel.KasseBelegMaxMegabyte, heute, 10, ct);
        foreach (var datei in dateien.Where(d => d.Length > 0))
        {
            var name = Dateitypen.Bereinigen(datei.FileName);
            var typ = Dateitypen.Inhaltstyp(name);
            if (typ is not ("application/pdf" or "image/png" or "image/jpeg"))
            {
                modelState.AddModelError(feld, $"„{name}“: nur PDF, JPG oder PNG.");
                continue;
            }

            if (datei.Length > megabyte * 1024L * 1024L)
            {
                modelState.AddModelError(feld, $"„{name}“ ist größer als {megabyte} MB.");
                continue;
            }

            using var speicher = new MemoryStream();
            await datei.CopyToAsync(speicher, ct);
            var inhalt = speicher.ToArray();
            if (typ != "application/pdf")
            {
                if (Fotobereinigung.Bereinigen(inhalt, typ) is not { } ohneMetadaten)
                {
                    modelState.AddModelError(feld, $"„{name}“ ist kein gültiges Bild.");
                    continue;
                }

                inhalt = ohneMetadaten;
            }
            else if (!inhalt.AsSpan().StartsWith("%PDF"u8))
            {
                modelState.AddModelError(feld, $"„{name}“ ist kein gültiges PDF.");
                continue;
            }

            belege.Add(Kassenfuehrung.Beleg(name, typ, inhalt));
        }

        return belege;
    }

    /// <summary>Betrag einer wiederkehrenden Buchung: der am Fälligkeitstag gültige Parameter, sonst der feste Betrag.</summary>
    /// <param name="w">Die wiederkehrende Buchung.</param>
    /// <param name="parameter">Parameterleser.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Betrag in Euro oder null, wenn keiner bekannt ist.</returns>
    public static async Task<decimal?> VorlageBetragAsync(WiederkehrendeBuchung w, ParameterLeser parameter, CancellationToken ct)
    {
        if (w.BetragParameter is { } schluessel
            && decimal.TryParse(await parameter.WertAsync(schluessel, w.NaechsteFaelligkeit, ct), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var wert) && wert > 0)
        {
            return wert;
        }

        return w.Betrag;
    }

    /// <summary>Feld einer CSV-Datei mit Semikolon als Trenner, bei Bedarf in Anführungszeichen.</summary>
    /// <param name="text">Inhalt des Felds.</param>
    /// <returns>Das Feld.</returns>
    public static string Csv(string? text) =>
        string.IsNullOrEmpty(text) ? "" : text.Contains(';') || text.Contains('"') || text.Contains('\n') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;

    /// <summary>CSV-Datei in UTF-8 mit BOM, damit Excel die Umlaute erkennt.</summary>
    /// <param name="zeilen">Zeilen samt Kopfzeile.</param>
    /// <returns>Inhalt der Datei.</returns>
    public static byte[] CsvDatei(IEnumerable<string> zeilen) =>
        System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", zeilen))).ToArray();

    private static string Kuerzen(string text, int laenge) => text.Length <= laenge ? text : text[..(laenge - 1)] + "…";
}
