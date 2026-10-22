using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Fevad.Web.Api;

/// <summary>
/// Empfang von Alarmdaten aus alamos FE2 (HTTP-Ausgang). Speichert die Rohdaten
/// und legt daraus einen Einsatzbericht-Entwurf an. Keine Alarmierung, keine Einsatzführung.
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
        ParameterLeser parameter,
        IOptionsSnapshot<Fe2Optionen> optionen,
        TimeProvider zeit,
        Einsatzberichte berichte,
        ILoggerFactory logger,
        CancellationToken ct)
    {
        if (!TokenGueltig(request.Headers[TokenHeader], optionen.Value.WebhookToken))
        {
            return Results.Unauthorized();
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var rohdaten = await reader.ReadToEndAsync(ct);

        Fe2Alarm alarm;
        try
        {
            alarm = Fe2Alarm.Lesen(rohdaten);
        }
        catch (JsonException)
        {
            return Results.BadRequest("Kein gültiges JSON.");
        }

        var externeId = alarm.ExterneId;
        if (string.IsNullOrWhiteSpace(externeId))
        {
            return Results.BadRequest("externalId fehlt.");
        }

        // Statusmeldungen, Abschluss usw. gehören zur Einsatzführung in FE2, nicht in den Bericht.
        if (!alarm.IstAlarm)
        {
            return Results.Ok(new { status = "ignoriert" });
        }

        // FE2 sendet bei Wiederholungen dieselbe externalId; doppelte Alarme werden ignoriert.
        var bereitsVorhanden = Results.Ok(new { status = "bereits vorhanden" });
        if (await db.AlarmImporte.IgnoreQueryFilters().AnyAsync(a => a.Quelle == AlarmQuelle.Fe2Webhook && a.ExterneId == externeId, ct))
        {
            return bereitsVorhanden;
        }

        var jetzt = zeit.GetUtcNow();
        var aufbewahrungMonate = await parameter.ZahlAsync(
            ParameterSchluessel.AlarmdatenAufbewahrungMonate, DateOnly.FromDateTime(jetzt.UtcDateTime), 6, ct);

        var import = AlarmImport.Empfangen(AlarmQuelle.Fe2Webhook, externeId, rohdaten, jetzt, aufbewahrungMonate);
        db.AlarmImporte.Add(import);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Zwei gleichzeitige Zustellungen desselben Alarms: die zweite scheitert am eindeutigen Index.
            return bereitsVorhanden;
        }

        // Der Alarm ist gesichert. Scheitert der Entwurf, holt ihn die Einsatzliste später nach;
        // FE2 bekommt trotzdem eine Bestätigung, sonst sendet es den Alarm erneut.
        try
        {
            await berichte.EntwurfAusAlarmAsync(import, ct);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or FormatException)
        {
            logger.CreateLogger(typeof(Fe2Endpunkte)).LogError(ex, "Entwurf aus FE2-Alarm {ExterneId} konnte nicht angelegt werden.", externeId);
        }

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
}
