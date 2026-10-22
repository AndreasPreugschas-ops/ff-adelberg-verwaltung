using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FfAdelberg.Domain.Einsaetze;
using FfAdelberg.Domain.Gemeinsam;
using FfAdelberg.Infrastructure.Persistenz;
using FfAdelberg.Web.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FfAdelberg.Web.Api;

/// <summary>
/// Empfang von Alarmdaten aus alamos FE2 (HTTP-Ausgang). Speichert nur die Rohdaten;
/// daraus wird später ein Einsatzbericht-Entwurf. Keine Alarmierung, keine Einsatzführung.
/// </summary>
public static class Fe2Endpunkte
{
    public const string TokenHeader = "X-Fe2-Token";

    public static IEndpointRouteBuilder MapFe2Endpunkte(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/alarm/fe2", AlarmEmpfangen)
            .AllowAnonymous()
            .DisableAntiforgery();
        return app;
    }

    private static async Task<IResult> AlarmEmpfangen(
        HttpRequest request,
        AppDbContext db,
        IOptionsSnapshot<Fe2Optionen> optionen,
        TimeProvider zeit,
        CancellationToken ct)
    {
        if (!TokenGueltig(request.Headers[TokenHeader], optionen.Value.WebhookToken))
        {
            return Results.Unauthorized();
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var rohdaten = await reader.ReadToEndAsync(ct);

        string? externeId;
        try
        {
            externeId = ExterneIdLesen(rohdaten);
        }
        catch (JsonException)
        {
            return Results.BadRequest("Kein gültiges JSON.");
        }

        if (string.IsNullOrWhiteSpace(externeId))
        {
            return Results.BadRequest("externalId fehlt.");
        }

        // FE2 sendet bei Wiederholungen dieselbe externalId; doppelte Alarme werden ignoriert.
        if (await db.AlarmImporte.AnyAsync(a => a.Quelle == AlarmQuelle.Fe2Webhook && a.ExterneId == externeId, ct))
        {
            return Results.Ok(new { status = "bereits vorhanden" });
        }

        var aufbewahrung = await db.Parameter
            .Where(p => p.Schluessel == ParameterSchluessel.AlarmdatenAufbewahrungMonate)
            .OrderByDescending(p => p.GueltigAb)
            .Select(p => p.Wert)
            .FirstOrDefaultAsync(ct);

        db.AlarmImporte.Add(AlarmImport.Empfangen(
            AlarmQuelle.Fe2Webhook, externeId, rohdaten, zeit.GetUtcNow(), int.TryParse(aufbewahrung, out var monate) ? monate : 6));
        await db.SaveChangesAsync(ct);

        return Results.Accepted();
    }

    private static bool TokenGueltig(string? gesendet, string? erwartet)
    {
        if (string.IsNullOrEmpty(erwartet) || string.IsNullOrEmpty(gesendet))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(gesendet), Encoding.UTF8.GetBytes(erwartet));
    }

    /// <summary>Sucht externalId auf oberster Ebene oder unter data (Format der FE2-Schnittstelle v2).</summary>
    private static string? ExterneIdLesen(string json)
    {
        using var dokument = JsonDocument.Parse(json);
        var wurzel = dokument.RootElement;
        if (wurzel.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (wurzel.TryGetProperty("externalId", out var id) && id.ValueKind == JsonValueKind.String)
        {
            return id.GetString();
        }

        return wurzel.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("externalId", out var dataId)
            && dataId.ValueKind == JsonValueKind.String
                ? dataId.GetString()
                : null;
    }
}
