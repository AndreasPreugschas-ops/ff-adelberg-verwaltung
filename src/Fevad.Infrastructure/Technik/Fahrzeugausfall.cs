using System.Text;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Technik;

/// <summary>
/// Hinweis an den Kommandanten, wenn ein Fahrzeug als außer Betrieb gemeldet wird (V63): Die Ausrückeordnung ist betroffen,
/// vielleicht muss die Leitstelle informiert werden. Empfänger sind alle Konten mit dem Recht
/// <see cref="Berechtigungen.FahrzeugausfallHinweis"/>. Die Mail nennt keine Personen, nur Fahrzeug und Beschreibung; in der Anwendung
/// erscheint zusätzlich eine Benachrichtigung (V72).
/// </summary>
public sealed class Fahrzeugausfall(
    AppDbContext db,
    IMailVersand mail,
    Rechteverzeichnis rechte,
    UserManager<Benutzer> benutzer,
    IConfiguration konfiguration,
    TimeProvider zeit,
    Benachrichtigungen.Benachrichtigungsdienst benachrichtigungen,
    ILogger<Fahrzeugausfall> log)
{
    /// <summary>
    /// Schickt für jeden einsatzrelevanten Fahrzeugmangel, über den noch niemand informiert wurde, den Hinweis und vermerkt den Zeitpunkt
    /// am Mangel. Ohne eingerichteten Mailversand passiert nichts; die Mängel stehen dann ganz oben in der Mängelliste.
    /// Aufrufen, nachdem ein Mangel gespeichert wurde (Meldung, Bearbeitung, Prüfung).
    /// </summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der verschickten Mails.</returns>
    public async Task<int> OffeneMeldenAsync(CancellationToken ct)
    {
        var maengel = await db.Maengel.Include(m => m.Fahrzeug)
            .Where(m => m.FahrzeugId != null && m.Schwere == MangelSchwere.AusserBetrieb && m.Status != MangelStatus.Behoben && m.KommandantInformiertAm == null)
            .ToListAsync(ct);
        if (maengel.Count == 0)
        {
            return 0;
        }

        // In der Anwendung immer (V72), auch ohne Mailversand; je Mangel nur einmal.
        var ids = maengel.Select(m => m.Id).ToList();
        var schon = (await db.Benachrichtigungen.Where(b => b.BezugId != null && ids.Contains(b.BezugId.Value)).Select(b => b.BezugId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
        foreach (var mangel in maengel.Where(m => !schon.Contains(m.Id)))
        {
            await benachrichtigungen.AnRechtAsync(Berechtigungen.FahrzeugausfallHinweis,
                new(Fevad.Domain.Gemeinsam.Benachrichtigungsart.Hinweis, $"Fahrzeug außer Betrieb: {mangel.Fahrzeug!.Funkrufname}",
                    $"/Technik/Maengel/Bearbeiten/{mangel.Id}", mangel.Id), ct: ct);
        }

        if (!mail.Aktiv)
        {
            return 0;
        }

        var stand = await rechte.StandAsync(ct);
        var adressen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rolle in stand.RollenMit(Berechtigungen.FahrzeugausfallHinweis))
        {
            foreach (var konto in await benutzer.GetUsersInRoleAsync(rolle))
            {
                if (!konto.IstDeaktiviert && !string.IsNullOrWhiteSpace(konto.Email))
                {
                    adressen.Add(konto.Email);
                }
            }
        }

        var adresse = konfiguration["Anmeldung:OeffentlicheAdresse"]?.TrimEnd('/');
        var gesendet = 0;
        foreach (var mangel in maengel)
        {
            var text = new StringBuilder()
                .AppendLine($"{mangel.Fahrzeug!.Funkrufname} ({mangel.Fahrzeug.Typ}) wurde als außer Betrieb gemeldet:")
                .AppendLine()
                .AppendLine(mangel.Beschreibung)
                .AppendLine()
                .AppendLine("Bitte prüfen, ob die Ausrückeordnung angepasst und die Leitstelle informiert werden muss.");
            if (!string.IsNullOrEmpty(adresse))
            {
                text.AppendLine().AppendLine($"{adresse}/Technik/Maengel/Bearbeiten/{mangel.Id}");
            }

            var erreicht = false;
            foreach (var empfaenger in adressen)
            {
                try
                {
                    await mail.SendenAsync(empfaenger, $"Fahrzeug außer Betrieb: {mangel.Fahrzeug.Funkrufname}", text.ToString(), ct);
                    gesendet++;
                    erreicht = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Eine falsche Adresse darf die übrigen Empfänger nicht blockieren.
                    log.LogWarning(ex, "Hinweis Fahrzeugausfall an {Email} fehlgeschlagen.", empfaenger);
                }
            }

            if (erreicht)
            {
                mangel.KommandantInformiertAm = zeit.GetUtcNow();
            }
        }

        await db.SaveChangesAsync(ct);
        return gesendet;
    }
}
