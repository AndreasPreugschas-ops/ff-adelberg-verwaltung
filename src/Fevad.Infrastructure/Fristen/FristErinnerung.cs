using System.Globalization;
using System.Text;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Fristen;

/// <summary>
/// Sammelt die fälligen Fristen aller Quellen und schickt jedem Empfänger eine Mail mit den Fristen,
/// die er sehen darf, und danach über <see cref="EigeneErinnerung"/> jedem Mitglied seine eigenen. Läuft höchstens alle <see cref="ParameterSchluessel.ErinnerungIntervallTage"/> Tage.
/// </summary>
public sealed class FristErinnerung(
    AppDbContext db,
    IEnumerable<IFristQuelle> quellen,
    UserManager<Benutzer> benutzer,
    IMailVersand mail,
    ParameterLeser parameter,
    IConfiguration konfiguration,
    Rechteverzeichnis rechte,
    EigeneErinnerung eigeneErinnerung,
    ILogger<FristErinnerung> log)
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Prüft, ob eine Erinnerung fällig ist, und verschickt sie. Gibt die Zahl der Empfänger zurück.</summary>
    public async Task<int> AusfuehrenAsync(DateTimeOffset jetzt, CancellationToken ct)
    {
        if (!mail.Aktiv)
        {
            log.LogDebug("Fristerinnerung übersprungen: Mailversand ist nicht eingerichtet.");
            return 0;
        }

        var lokal = TimeZoneInfo.ConvertTime(jetzt, Berlin);
        var heute = DateOnly.FromDateTime(lokal.DateTime);
        var intervall = await parameter.ZahlAsync(ParameterSchluessel.ErinnerungIntervallTage, heute, 7, ct);
        var abStunde = await parameter.ZahlAsync(ParameterSchluessel.ErinnerungAbStunde, heute, 7, ct);

        var letzter = await db.Erinnerungslaeufe.MaxAsync(e => (DateTimeOffset?)e.Zeitpunkt, ct);
        if (lokal.Hour < abStunde || (letzter is not null && jetzt - letzter.Value < TimeSpan.FromDays(intervall) - TimeSpan.FromHours(1)))
        {
            return 0;
        }

        var hinweise = new List<Fristhinweis>();
        foreach (var quelle in quellen)
        {
            hinweise.AddRange(await quelle.FaelligeAsync(heute, ct));
        }

        var empfaenger = await EmpfaengerAsync(hinweise.Select(h => h.Empfaenger).Distinct(), ct);
        var adresse = konfiguration["Anmeldung:OeffentlicheAdresse"]?.TrimEnd('/');
        var gesendet = 0;
        foreach (var (email, policies) in empfaenger)
        {
            var eigene = hinweise.Where(h => policies.Contains(h.Empfaenger)).ToList();
            if (eigene.Count == 0)
            {
                continue;
            }

            try
            {
                await mail.SendenAsync(email, Betreff(eigene), Text(eigene, heute, adresse), ct);
                gesendet++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Eine falsche Adresse darf die übrigen Empfänger nicht blockieren.
                log.LogWarning(ex, "Fristerinnerung an {Email} fehlgeschlagen.", email);
            }
        }

        // Danach jedes Mitglied selbst (V38): eigene Fristen, Schutzausrüstung, offene Rückmeldungen.
        gesendet += await eigeneErinnerung.SendenAsync(jetzt, heute, adresse, ct);

        db.Erinnerungslaeufe.Add(new Erinnerungslauf { Zeitpunkt = jetzt, Eintraege = hinweise.Count, Empfaenger = gesendet });
        await db.SaveChangesAsync(ct);
        log.LogInformation("Fristerinnerung: {Anzahl} Fristen an {Empfaenger} Empfänger.", hinweise.Count, gesendet);
        return gesendet;
    }

    /// <summary>E-Mail-Adresse → Policies, für die das Konto über seine Rollen Empfänger ist.</summary>
    private async Task<Dictionary<string, HashSet<string>>> EmpfaengerAsync(IEnumerable<string> policies, CancellationToken ct)
    {
        var ergebnis = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var stand = await rechte.StandAsync(ct);
        foreach (var policy in policies)
        {
            foreach (var rolle in stand.RollenMit(policy))
            {
                ct.ThrowIfCancellationRequested();
                foreach (var konto in await benutzer.GetUsersInRoleAsync(rolle))
                {
                    if (konto.IstDeaktiviert || string.IsNullOrWhiteSpace(konto.Email))
                    {
                        continue;
                    }

                    if (!ergebnis.TryGetValue(konto.Email, out var menge))
                    {
                        ergebnis[konto.Email] = menge = [];
                    }

                    menge.Add(policy);
                }
            }
        }

        return ergebnis;
    }

    private static string Betreff(IReadOnlyList<Fristhinweis> hinweise)
    {
        var abgelaufen = hinweise.Count(h => h.Ampel == Fristampel.Rot);
        return abgelaufen > 0
            ? $"Fristen: {abgelaufen} abgelaufen, {hinweise.Count - abgelaufen} laufen bald ab"
            : $"Fristen: {hinweise.Count} laufen bald ab";
    }

    private static string Text(IReadOnlyList<Fristhinweis> hinweise, DateOnly heute, string? adresse)
    {
        var text = new StringBuilder();
        text.AppendLine($"Stand {heute.ToString("dd.MM.yyyy", Deutsch)}. Diese Fristen sind abgelaufen oder laufen bald ab:");

        foreach (var bereich in hinweise.GroupBy(h => h.Bereich).OrderBy(g => g.Key))
        {
            text.AppendLine().AppendLine(bereich.Key.ToUpper(Deutsch));
            foreach (var h in bereich.OrderByDescending(h => h.Ampel).ThenBy(h => h.FaelligAm ?? DateOnly.MinValue).ThenBy(h => h.Gegenstand))
            {
                var stand = h.FaelligAm is { } datum
                    ? (h.Ampel == Fristampel.Rot ? "abgelaufen seit " : "fällig am ") + datum.ToString("dd.MM.yyyy", Deutsch)
                    : "kein Nachweis";
                text.AppendLine($"- {h.Gegenstand}: {h.Bezeichnung}, {stand}");
            }
        }

        if (!string.IsNullOrEmpty(adresse))
        {
            text.AppendLine().AppendLine($"Übersicht: {adresse}/Personal");
        }

        text.AppendLine().AppendLine("Diese Mail wird automatisch verschickt. Empfänger legt der Administrator über die Rollen fest.");
        return text.ToString();
    }
}
