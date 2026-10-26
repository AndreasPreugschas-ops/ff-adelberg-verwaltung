using System.Text.Json;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>
/// Leseprotokoll für Gesundheitsdaten (F17, Art. 9 und 32 DSGVO): Wer Atemschutz-Eignung oder Befreiungsgründe einer Person angezeigt
/// bekommt, steht mit Zeitpunkt und Ansicht im Änderungsprotokoll (Aktion <see cref="AuditAktion.Gelesen"/>). Aufbewahrt wird es wie das
/// Änderungsprotokoll (L19). Die eigenen Daten zu sehen wird nicht protokolliert.
/// </summary>
public sealed class Leseprotokoll(AppDbContext db, IAktuellerBenutzer benutzer, TimeProvider zeit)
{
    /// <summary>Tabellenname im Protokoll für Atemschutz-Eignung (Vorsorge, Status, Sperren, Expositionen).</summary>
    public const string Atemschutz = "lesen.atemschutz";

    /// <summary>Tabellenname im Protokoll für Gründe einer Befreiung vom Dienst.</summary>
    public const string Befreiungsgrund = "lesen.befreiungsgrund";

    /// <summary>Hält fest, dass der angemeldete Benutzer diese Daten dieser Personen angezeigt bekommt.</summary>
    /// <param name="bereich"><see cref="Atemschutz"/> oder <see cref="Befreiungsgrund"/>.</param>
    /// <param name="ansicht">Wo die Daten erschienen, z. B. „Personalakte“.</param>
    /// <param name="mitglieder">Die betroffenen Mitglieder.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task ProtokollierenAsync(string bereich, string ansicht, IEnumerable<Guid> mitglieder, CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var eigenesMitglied = benutzer.Id is { } id ? await db.Users.Where(b => b.Id == id).Select(b => b.MitgliedId).FirstOrDefaultAsync(ct) : null;
        var angaben = JsonSerializer.Serialize(new { ansicht });
        var eintraege = mitglieder.Distinct().Where(m => m != eigenesMitglied)
            .Select(m => new AuditEintrag { Zeitpunkt = jetzt, BenutzerId = benutzer.Id, Tabelle = bereich, DatensatzId = m, Aktion = AuditAktion.Gelesen, Aenderungen = angaben })
            .ToList();
        if (eintraege.Count == 0)
        {
            return;
        }

        db.AuditEintraege.AddRange(eintraege);
        await db.SaveChangesAsync(ct);
    }
}
