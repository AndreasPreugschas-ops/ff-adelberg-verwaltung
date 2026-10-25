using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dienste;

/// <summary>
/// An- und Abmeldungen eines Dienstes ändern, auch nach dem Rückmeldeschluss: Mitglieder markieren (z. B. alle ohne
/// Rückmeldung) und ihnen gesammelt eine Zu- oder Absage oder gleich die Anwesenheit zuordnen. Für Bearbeiter des Dienstes
/// und den eingetragenen Übungsleiter.
/// </summary>
public class MeldungenModel(AppDbContext db, Dienstplanung planung, Datenbereich bereich) : PageModel
{
    /// <summary>Der Dienst, dessen Meldungen bearbeitet werden.</summary>
    public Dienst Dienst { get; private set; } = null!;
    /// <summary>Meldungen der Zielgruppe und aller, die sich gemeldet haben.</summary>
    public IReadOnlyList<Meldung> Eintraege { get; private set; } = [];
    /// <summary>Der Benutzer darf den Dienst selbst bearbeiten.</summary>
    public bool DarfDienstBearbeiten { get; private set; }

    /// <summary>Meldungen, die für die markierten Mitglieder gesetzt werden können.</summary>
    public static IReadOnlyList<(Dienstplanung.Sammelmeldung Wert, string Text)> Auswahl { get; } =
    [
        (Dienstplanung.Sammelmeldung.Zusage, "Zusage"),
        (Dienstplanung.Sammelmeldung.Absage, "Absage"),
        (Dienstplanung.Sammelmeldung.Anwesend, "anwesend"),
        (Dienstplanung.Sammelmeldung.Entschuldigt, "entschuldigt"),
        (Dienstplanung.Sammelmeldung.Unentschuldigt, "unentschuldigt"),
    ];

    /// <summary>Zeigt die Meldungen zum Dienst.</summary>
    /// <param name="id">Kennung des Dienstes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        return Page();
    }

    /// <summary>Setzt für die markierten Mitglieder eine Zu- oder Absage beziehungsweise einen Anwesenheitsstatus.</summary>
    /// <param name="id">Kennung des Dienstes.</param>
    /// <param name="markiert">Markierte Mitglieder; nur solche auf der Liste zählen.</param>
    /// <param name="meldung">Die zu setzende Meldung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostZuordnenAsync(Guid id, List<Guid> markiert, Dienstplanung.Sammelmeldung meldung, CancellationToken ct)
    {
        if (await LadenAsync(id, ct) is { } fehler)
        {
            return fehler;
        }

        // Nur Mitglieder, die auf der Liste stehen; alles andere wird stillschweigend übergangen.
        var erlaubt = Eintraege.Select(e => e.MitgliedId).ToHashSet();
        var auswahl = markiert.Where(erlaubt.Contains).Distinct().ToList();
        if (auswahl.Count == 0)
        {
            TempData["Fehler"] = "Bitte mindestens ein Mitglied markieren.";
            return RedirectToPage(new { id });
        }

        if (!Dienst.AlsDienst && meldung is not (Dienstplanung.Sammelmeldung.Zusage or Dienstplanung.Sammelmeldung.Absage))
        {
            TempData["Fehler"] = "Dieser Termin ist nicht als Dienst geplant und hat keine Anwesenheitsliste.";
            return RedirectToPage(new { id });
        }

        var anzahl = await planung.SammelnAsync(Dienst, auswahl, meldung, ct);
        var text = Auswahl.First(a => a.Wert == meldung).Text;
        TempData["Meldung"] = anzahl == 1 ? $"1 Mitglied auf „{text}“ gesetzt." : $"{anzahl} Mitglieder auf „{text}“ gesetzt.";
        return RedirectToPage(new { id });
    }

    private async Task<IActionResult?> LadenAsync(Guid id, CancellationToken ct)
    {
        var dienst = await db.Dienste.AsNoTracking().MitZielgruppe().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dienst is null)
        {
            return NotFound();
        }

        if (!await bereich.DarfMeldungenBearbeitenAsync(User, dienst, ct))
        {
            return Forbid();
        }

        Dienst = dienst;
        DarfDienstBearbeiten = await bereich.DarfDienstBearbeitenAsync(User, dienst.AbteilungIds, ct);
        Eintraege = await Meldungsliste.LadenAsync(db, planung, bereich, User, dienst, ct);
        return null;
    }
}
