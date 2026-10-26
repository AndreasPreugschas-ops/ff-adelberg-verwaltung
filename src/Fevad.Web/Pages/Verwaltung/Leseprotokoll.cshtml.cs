using System.Text.Json;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Lesen = Fevad.Infrastructure.Datenschutz.Leseprotokoll;

namespace Fevad.Web.Pages.Verwaltung;

/// <summary>
/// Leseprotokoll der Gesundheitsdaten (F17): wer wann wessen Atemschutz-Eignung oder Befreiungsgründe angezeigt bekam. Für Auskünfte nach
/// Art. 15 DSGVO und Stichproben des Datenschutzbeauftragten; aufbewahrt wie das Änderungsprotokoll (L19).
/// </summary>
[Authorize(Policy = Berechtigungen.DatenschutzLeseprotokoll)]
public class LeseprotokollModel(AppDbContext db, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Zeile des Protokolls.</summary>
    /// <param name="Zeitpunkt">Zeitpunkt der Anzeige (UTC).</param>
    /// <param name="Wer">E-Mail des Kontos.</param>
    /// <param name="Bereich">Atemschutz oder Befreiungsgrund.</param>
    /// <param name="Ansicht">Seite, auf der die Daten erschienen.</param>
    /// <param name="Person">Betroffenes Mitglied.</param>
    public sealed record Zeile(DateTimeOffset Zeitpunkt, string Wer, string Bereich, string Ansicht, string Person);

    /// <summary>Nur Einträge zu diesem Mitglied.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? MitgliedId { get; set; }

    /// <summary>Nur Einträge dieses Kontos.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? KontoId { get; set; }

    /// <summary>Erster Tag des Zeitraums; ohne Angabe drei Monate zurück.</summary>
    [BindProperty(SupportsGet = true)]
    public DateOnly? Von { get; set; }

    /// <summary>Letzter Tag des Zeitraums; ohne Angabe heute.</summary>
    [BindProperty(SupportsGet = true)]
    public DateOnly? Bis { get; set; }

    /// <summary>Angezeigte Seite.</summary>
    [BindProperty(SupportsGet = true, Name = "Seite")]
    public int Seitennummer { get; set; } = 1;

    /// <summary>Mitglieder, zu denen es Einträge gibt.</summary>
    public IReadOnlyList<SelectListItem> Mitglieder { get; private set; } = [];

    /// <summary>Konten, die Einträge erzeugt haben.</summary>
    public IReadOnlyList<SelectListItem> Konten { get; private set; } = [];

    /// <summary>Die Einträge der Seite, neueste zuerst.</summary>
    public Seite<Zeile> Eintraege { get; private set; } = Seite<Zeile>.Leer;

    /// <summary>Zeigt das Protokoll mit Filter.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Von ??= heute.AddMonths(-3);
        Bis ??= heute;
        var ab = Ortszeit.TagesbeginnUtc(Von.Value);
        var bis = Ortszeit.TagesbeginnUtc(Bis.Value.AddDays(1));
        var basis = db.AuditEintraege.AsNoTracking()
            .Where(a => a.Aktion == AuditAktion.Gelesen && (a.Tabelle == Lesen.Atemschutz || a.Tabelle == Lesen.Befreiungsgrund));

        var mitgliedIds = await basis.Select(a => a.DatensatzId).Distinct().ToListAsync(ct);
        var kontoIds = await basis.Where(a => a.BenutzerId != null).Select(a => a.BenutzerId!.Value).Distinct().ToListAsync(ct);
        var namen = await db.Mitglieder.IgnoreQueryFilters().AsNoTracking().Where(m => mitgliedIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.AnonymisiertAm == null ? m.Nachname + ", " + m.Vorname : "(anonymisiert)", ct);
        var emails = await db.Users.AsNoTracking().Where(b => kontoIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Email ?? "(gelöschtes Konto)", ct);
        Mitglieder = namen.OrderBy(n => n.Value).Select(n => new SelectListItem(n.Value, n.Key.ToString(), n.Key == MitgliedId)).ToList();
        Konten = emails.OrderBy(e => e.Value).Select(e => new SelectListItem(e.Value, e.Key.ToString(), e.Key == KontoId)).ToList();

        var abfrage = basis.Where(a => a.Zeitpunkt >= ab && a.Zeitpunkt < bis);
        if (MitgliedId is { } m)
        {
            abfrage = abfrage.Where(a => a.DatensatzId == m);
        }

        if (KontoId is { } k)
        {
            abfrage = abfrage.Where(a => a.BenutzerId == k);
        }

        var eintraege = await abfrage.OrderByDescending(a => a.Zeitpunkt).ThenByDescending(a => a.Id).ToListAsync(ct);
        Eintraege = eintraege.Select(a => new Zeile(
                a.Zeitpunkt,
                a.BenutzerId is { } b ? emails.GetValueOrDefault(b, "(gelöschtes Konto)") : "(ohne Anmeldung)",
                a.Tabelle == Lesen.Befreiungsgrund ? "Befreiungsgrund" : "Atemschutz",
                Ansicht(a.Aenderungen),
                namen.GetValueOrDefault(a.DatensatzId, "(gelöscht)")))
            .ToList()
            .Seite(Seitennummer);
    }

    private static string Ansicht(string? angaben)
    {
        if (angaben is null)
        {
            return "–";
        }

        using var json = JsonDocument.Parse(angaben);
        return json.RootElement.TryGetProperty("ansicht", out var a) ? a.GetString() ?? "–" : "–";
    }
}
