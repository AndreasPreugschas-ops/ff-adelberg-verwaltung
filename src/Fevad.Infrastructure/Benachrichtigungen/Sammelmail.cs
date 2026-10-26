using System.Globalization;
using System.Text;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Benachrichtigungen;

/// <summary>
/// Tägliche Sammelmail der Benachrichtigungen (V72): je Konto höchstens eine Mail am Tag ab <see cref="ParameterSchluessel.SammelmailAbStunde"/>,
/// mit der Zahl neuer Benachrichtigungen je Art und dem Link. Titel und Texte stehen nicht in der Mail (datensparsam wie die Fristerinnerung, F13).
/// Arten, die das Konto abbestellt hat, zählen nicht.
/// </summary>
public sealed class Sammelmail(AppDbContext db, IMailVersand mail, ParameterLeser parameter, IConfiguration konfiguration, ILogger<Sammelmail> log)
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Verschickt die fälligen Sammelmails.</summary>
    /// <param name="jetzt">Zeitpunkt des Laufs in UTC.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der verschickten Mails.</returns>
    public async Task<int> AusfuehrenAsync(DateTimeOffset jetzt, CancellationToken ct)
    {
        if (!mail.Aktiv)
        {
            return 0;
        }

        var lokal = TimeZoneInfo.ConvertTime(jetzt, Ortszeit.Berlin);
        var heute = DateOnly.FromDateTime(lokal.DateTime);
        if (lokal.Hour < await parameter.ZahlAsync(ParameterSchluessel.SammelmailAbStunde, heute, 18, ct))
        {
            return 0;
        }

        var tagesbeginn = new DateTimeOffset(lokal.Date, lokal.Offset).ToUniversalTime();
        var offen = await db.Benachrichtigungen
            .Where(b => b.GelesenAm == null && b.GemailtAm == null)
            .Join(db.Users.Where(u => u.Email != null && u.DeaktiviertAm == null), b => b.EmpfaengerId, u => u.Id,
                (b, u) => new { Benachrichtigung = b, u.Email, u.SammelmailOhne })
            .ToListAsync(ct);
        var heuteGemailt = (await db.Benachrichtigungen.Where(b => b.GemailtAm >= tagesbeginn).Select(b => b.EmpfaengerId).Distinct().ToListAsync(ct)).ToHashSet();

        var adresse = konfiguration["Anmeldung:OeffentlicheAdresse"]?.TrimEnd('/');
        var gesendet = 0;
        foreach (var konto in offen.Where(o => (o.SammelmailOhne & o.Benachrichtigung.Art) == 0).GroupBy(o => o.Benachrichtigung.EmpfaengerId))
        {
            if (heuteGemailt.Contains(konto.Key))
            {
                continue;
            }

            var je = konto.GroupBy(o => o.Benachrichtigung.Art).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())).ToList();
            try
            {
                await mail.SendenAsync(konto.First().Email!, Betreff(je), Text(je, heute, adresse), ct);
                foreach (var o in konto)
                {
                    o.Benachrichtigung.GemailtAm = jetzt;
                }

                await db.SaveChangesAsync(ct);
                gesendet++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Eine falsche Adresse darf die übrigen Empfänger nicht blockieren.
                log.LogWarning(ex, "Sammelmail an Konto {Konto} fehlgeschlagen.", konto.Key);
            }
        }

        return gesendet;
    }

    /// <summary>Betreff der Sammelmail.</summary>
    /// <param name="je">Anzahl je Art.</param>
    /// <returns>Der Betreff.</returns>
    public static string Betreff(IReadOnlyList<(Benachrichtigungsart Art, int Anzahl)> je) =>
        $"FEVAD: {je.Sum(x => x.Anzahl)} neue Benachrichtigung(en)";

    /// <summary>Text der Sammelmail: nur Anzahl je Art und Link, keine Inhalte.</summary>
    /// <param name="je">Anzahl je Art.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <param name="adresse">Öffentliche Adresse von FEVAD, leer ohne.</param>
    /// <returns>Der Text.</returns>
    public static string Text(IReadOnlyList<(Benachrichtigungsart Art, int Anzahl)> je, DateOnly heute, string? adresse)
    {
        var text = new StringBuilder().AppendLine($"Stand {heute.ToString("dd.MM.yyyy", Deutsch)}. In FEVAD wartet Neues auf dich:").AppendLine();
        foreach (var (art, anzahl) in je)
        {
            text.AppendLine($"- {Bezeichnung(art)}: {anzahl}");
        }

        if (!string.IsNullOrEmpty(adresse))
        {
            text.AppendLine().AppendLine($"Ansehen: {adresse}/Benachrichtigungen");
        }

        text.AppendLine().AppendLine("Diese Mail kommt höchstens einmal am Tag. Welche Arten darin stehen, stellst du unter „Mein Konto“ ein.");
        return text.ToString();
    }

    /// <summary>Bezeichnung einer Art für Mail und Oberfläche.</summary>
    /// <param name="art">Die Art.</param>
    /// <returns>Die Bezeichnung.</returns>
    public static string Bezeichnung(Benachrichtigungsart art) => art switch
    {
        Benachrichtigungsart.Mitteilung => "Mitteilungen",
        Benachrichtigungsart.Rundfrage => "Rundfragen und Umfragen",
        Benachrichtigungsart.Termin => "Termine",
        Benachrichtigungsart.Erinnerung => "Erinnerungen",
        Benachrichtigungsart.Aufgabe => "Aufgaben",
        Benachrichtigungsart.Hinweis => "Hinweise",
        _ => art.ToString(),
    };
}

/// <summary>Prüft stündlich, ob Sammelmails fällig sind (<see cref="Sammelmail"/>).</summary>
public sealed class SammelmailDienst(IServiceScopeFactory scopes, TimeProvider zeit, ILogger<SammelmailDienst> log) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start abwarten, damit Migrationen und Seed durch sind.
        await Task.Delay(TimeSpan.FromMinutes(2), zeit, stoppingToken);

        using var takt = new PeriodicTimer(TimeSpan.FromHours(1), zeit);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<Sammelmail>().AusfuehrenAsync(zeit.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Sammelmail fehlgeschlagen.");
            }
        }
        while (await takt.WaitForNextTickAsync(stoppingToken));
    }
}
