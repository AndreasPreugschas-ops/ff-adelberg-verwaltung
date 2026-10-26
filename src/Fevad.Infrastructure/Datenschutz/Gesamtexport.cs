using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>
/// Gesamtexport (V70): alle Tabellen als JSON (eine Datei je Tabelle, Spaltennamen wie in der Datenbank) und alle gespeicherten Dateien
/// (Dokumente, Fotos, Belege) als eigene Einträge in einem ZIP. Ausstiegsweg in einem offenen Format neben dem Datenbank-Dump, damit die
/// Gemeinde ihre Daten auch ohne FEVAD lesen kann. Ausgelassen werden Anmeldegeheimnisse: Passwort-Hashes, Sicherheitsstempel, Anmelde-Token,
/// Passkeys und die Schlüssel der Datenverschlüsselung. Jeder Export steht im Änderungsprotokoll.
/// </summary>
/// <param name="db">Datenbankkontext.</param>
/// <param name="benutzer">Angemeldetes Konto, für das Protokoll.</param>
/// <param name="zeit">Uhr.</param>
public sealed class Gesamtexport(AppDbContext db, IAktuellerBenutzer benutzer, TimeProvider zeit)
{
    /// <summary>Tabellenname der Protokolleinträge im Änderungsprotokoll.</summary>
    public const string Protokolltabelle = "export.gesamt";

    private static readonly Type[] Ausgelassen =
    [
        typeof(DataProtectionKey), typeof(IdentityUserToken<Guid>), typeof(IdentityUserLogin<Guid>), typeof(IdentityUserPasskey<Guid>),
        typeof(IdentityUserClaim<Guid>), typeof(IdentityRoleClaim<Guid>),
    ];

    private static readonly string[] GeheimeKontofelder =
        [nameof(Benutzer.PasswordHash), nameof(Benutzer.SecurityStamp), nameof(Benutzer.ConcurrencyStamp), nameof(Benutzer.KalenderToken)];

    /// <summary>Ergebnis eines Exports.</summary>
    /// <param name="Tabellen">Exportierte Tabellen.</param>
    /// <param name="Zeilen">Zeilen insgesamt.</param>
    /// <param name="Dateien">Dateien insgesamt.</param>
    public sealed record Umfang(int Tabellen, long Zeilen, int Dateien);

    /// <summary>Schreibt den Export als ZIP in den Datenstrom und protokolliert ihn.</summary>
    /// <param name="ziel">Beschreibbarer Datenstrom, z. B. eine temporäre Datei.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Umfang des Exports.</returns>
    public async Task<Umfang> SchreibenAsync(Stream ziel, CancellationToken ct = default)
    {
        var jetzt = zeit.GetUtcNow();
        var tabellen = Tabellen();
        long zeilen = 0;
        var dateien = 0;
        var manifest = new List<object>();
        await using (var zip = new ZipArchive(ziel, ZipArchiveMode.Create, leaveOpen: true))
        {
            var verbindung = db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync(ct);
            try
            {
                foreach (var (name, schema, ohne) in tabellen)
                {
                    await using var befehl = verbindung.CreateCommand();
                    befehl.CommandText = schema is null ? $"SELECT * FROM \"{name}\"" : $"SELECT * FROM \"{schema}\".\"{name}\"";
                    await using var leser = await befehl.ExecuteReaderAsync(ct);
                    var spalten = Enumerable.Range(0, leser.FieldCount).Select(leser.GetName).ToList();
                    using var puffer = new MemoryStream();
                    var anzahl = 0L;
                    await using (var json = new Utf8JsonWriter(puffer, new JsonWriterOptions { Indented = true }))
                    {
                        json.WriteStartArray();
                        while (await leser.ReadAsync(ct))
                        {
                            anzahl++;
                            var id = spalten.IndexOf("id") is var i and >= 0 && !leser.IsDBNull(i) ? Convert.ToString(leser.GetValue(i)) : anzahl.ToString();
                            json.WriteStartObject();
                            for (var s = 0; s < spalten.Count; s++)
                            {
                                if (ohne.Contains(spalten[s]))
                                {
                                    continue;
                                }

                                json.WritePropertyName(spalten[s]);
                                var wert = leser.IsDBNull(s) ? null : leser.GetValue(s);
                                if (wert is byte[] bytes)
                                {
                                    var pfad = $"dateien/{name}/{id}_{spalten[s]}{Endung(bytes)}";
                                    var eintrag = zip.CreateEntry(pfad, CompressionLevel.Optimal);
                                    await using (var datei = eintrag.Open())
                                    {
                                        await datei.WriteAsync(bytes, ct);
                                    }

                                    dateien++;
                                    json.WriteStringValue(pfad);
                                }
                                else if (wert is null)
                                {
                                    json.WriteNullValue();
                                }
                                else
                                {
                                    JsonSerializer.Serialize(json, wert, wert.GetType());
                                }
                            }

                            json.WriteEndObject();
                        }

                        json.WriteEndArray();
                    }

                    var tabellenEintrag = zip.CreateEntry($"tabellen/{name}.json", CompressionLevel.Optimal);
                    await using (var datei = tabellenEintrag.Open())
                    {
                        puffer.Position = 0;
                        await puffer.CopyToAsync(datei, ct);
                    }

                    zeilen += anzahl;
                    manifest.Add(new { tabelle = name, zeilen = anzahl, ausgelassen = ohne });
                }
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }

            var info = zip.CreateEntry("manifest.json");
            await using (var datei = info.Open())
            {
                await JsonSerializer.SerializeAsync(datei, new { erstellt = jetzt, programm = "FEVAD", tabellen = manifest }, new JsonSerializerOptions { WriteIndented = true }, ct);
            }

            var liesmich = zip.CreateEntry("LIESMICH.txt");
            await using (var datei = liesmich.Open())
            {
                await datei.WriteAsync(Encoding.UTF8.GetBytes(Liesmich), ct);
            }
        }

        var umfang = new Umfang(tabellen.Count, zeilen, dateien);
        db.AuditEintraege.Add(new AuditEintrag
        {
            Zeitpunkt = jetzt, BenutzerId = benutzer.Id, Tabelle = Protokolltabelle, DatensatzId = Guid.CreateVersion7(), Aktion = AuditAktion.Gelesen,
            Aenderungen = JsonSerializer.Serialize(new { tabellen = umfang.Tabellen, zeilen = umfang.Zeilen, dateien = umfang.Dateien }),
        });
        await db.SaveChangesAsync(ct);
        return umfang;
    }

    /// <summary>Bisherige Exporte aus dem Änderungsprotokoll, neueste zuerst.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zeitpunkt und Konto (E-Mail oder leer).</returns>
    public async Task<IReadOnlyList<(DateTimeOffset Zeitpunkt, string? Konto)>> BisherigeAsync(CancellationToken ct = default) =>
        (await db.AuditEintraege.AsNoTracking().Where(a => a.Tabelle == Protokolltabelle).OrderByDescending(a => a.Zeitpunkt).Take(20)
            .Select(a => new { a.Zeitpunkt, Konto = db.Users.Where(b => b.Id == a.BenutzerId).Select(b => b.Email).FirstOrDefault() })
            .ToListAsync(ct))
        .Select(a => (a.Zeitpunkt, a.Konto)).ToList();

    private List<(string Name, string? Schema, string[] Ohne)> Tabellen()
    {
        var konto = db.Model.FindEntityType(typeof(Benutzer))!;
        var geheim = GeheimeKontofelder.Select(f => konto.FindProperty(f)!.GetColumnName()).ToArray();
        return db.Model.GetEntityTypes()
            .Where(t => !t.IsOwned() && t.GetTableName() is not null && t.GetViewName() is null && !Ausgelassen.Contains(t.ClrType))
            .Where(t => t.BaseType is null)
            .Select(t => (Name: t.GetTableName()!, Schema: t.GetSchema(), Ohne: t.ClrType == typeof(Benutzer) ? geheim : Array.Empty<string>()))
            .DistinctBy(t => (t.Schema, t.Name))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static string Endung(byte[] d) =>
        d.Length >= 4 && d[0] == 0x25 && d[1] == 0x50 && d[2] == 0x44 && d[3] == 0x46 ? ".pdf"
        : d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF ? ".jpg"
        : d.Length >= 4 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47 ? ".png"
        : d.Length >= 2 && d[0] == 0x50 && d[1] == 0x4B ? ".zip"
        : ".bin";

    private const string Liesmich = """
        FEVAD – Gesamtexport

        tabellen/<name>.json   Alle Zeilen einer Datenbanktabelle als JSON-Liste; Spaltennamen wie in der Datenbank (snake_case).
                               Zeiten sind UTC (ISO 8601), Datumswerte ohne Uhrzeit, Aufzählungen als Text.
        dateien/<tabelle>/...  Gespeicherte Dateien (Dokumente, Fotos, Belege). In der Tabelle steht an ihrer Stelle der Pfad im ZIP.
                               Dokumentversionen verweisen über datei_id auf die Tabelle dokument_dateien.
        manifest.json          Erstellungszeitpunkt, Tabellen und Zeilenzahlen, ausgelassene Spalten.

        Nicht enthalten: Passwort-Hashes, Sicherheitsstempel, Anmelde-Token, Passkeys und die Schlüssel der Datenverschlüsselung.
        Der Export enthält personenbezogene Daten einschließlich Gesundheitsdaten (Atemschutz-Fristen) und ist entsprechend zu schützen.
        """;
}
