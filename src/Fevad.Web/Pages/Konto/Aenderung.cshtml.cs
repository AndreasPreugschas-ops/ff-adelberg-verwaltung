using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Änderung der eigenen Daten beantragen (V36): Anschrift, Telefon, E-Mail, Notfallkontakt, Beruf und Arbeitgeber. Das Mitglied
/// ändert nichts direkt; die Personalverwaltung übernimmt oder lehnt ab (Andi, 25.10.2026). Je Mitglied ist ein Antrag offen.
/// Eltern melden so auch Änderungen für ihr Kind (V50), ohne Beruf und Arbeitgeber.
/// </summary>
public class AenderungModel(AppDbContext db, Dienstplanung planung, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    /// <summary>Feldgruppen des Formulars mit Überschrift.</summary>
    public static readonly IReadOnlyList<(string Titel, AenderungFeld[] Felder)> Gruppen =
    [
        ("Anschrift", [AenderungFeld.Strasse, AenderungFeld.Plz, AenderungFeld.Ort]),
        ("Telefon und E-Mail", [AenderungFeld.TelefonMobil, AenderungFeld.TelefonFestnetz, AenderungFeld.Email]),
        ("Notfallkontakt", [AenderungFeld.NotfallkontaktName, AenderungFeld.NotfallkontaktTelefon]),
        ("Beruf und Arbeitgeber", [AenderungFeld.Beruf, AenderungFeld.Arbeitgeber, AenderungFeld.ArbeitgeberStrasse, AenderungFeld.ArbeitgeberPlz,
            AenderungFeld.Arbeitsort, AenderungFeld.ArbeitgeberTelefon, AenderungFeld.ArbeitgeberMobil, AenderungFeld.ArbeitgeberEmail]),
    ];

    /// <summary>Elternkonto (V50): Antrag für dieses Kind statt für sich selbst.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? Kind { get; set; }

    /// <summary>Feldgruppen für dieses Formular; für Kinder ohne Beruf und Arbeitgeber.</summary>
    public IEnumerable<(string Titel, AenderungFeld[] Felder)> Feldgruppen => Kind is null ? Gruppen : Gruppen.Where(g => !g.Felder.Contains(AenderungFeld.Beruf));

    /// <summary>Das eigene Mitglied mit den gespeicherten Werten.</summary>
    public Mitglied? Mitglied { get; private set; }
    /// <summary>Der offene Antrag, falls es einen gibt.</summary>
    public Aenderungsantrag? Offener { get; private set; }
    /// <summary>Entschiedene oder zurückgezogene Anträge, neueste zuerst.</summary>
    public IReadOnlyList<Aenderungsantrag> Erledigte { get; private set; } = [];

    /// <summary>Gewünschte Werte je Feld (Name des Feldes als Schlüssel).</summary>
    [BindProperty]
    public Dictionary<string, string?> Werte { get; set; } = [];

    /// <summary>Freiwillige Bemerkung zum Antrag.</summary>
    [BindProperty, Display(Name = "Bemerkung (freiwillig)"), StringLength(500)]
    public string? Bemerkung { get; set; }

    /// <summary>Gespeicherter Wert eines Feldes.</summary>
    /// <param name="feld">Das Feld.</param>
    /// <returns>Der Wert oder null.</returns>
    public string? Gespeichert(AenderungFeld feld) => Mitglied is null ? null : Aenderungsfelder.Lesen(Mitglied, feld);

    /// <summary>Zeigt Formular, offenen Antrag und Verlauf.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404 ohne verknüpftes Mitglied.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!await LadenAsync(ct))
        {
            return NotFound();
        }

        Werte = Feldgruppen.SelectMany(g => g.Felder).ToDictionary(f => f.ToString(), Gespeichert);
        return Page();
    }

    /// <summary>Stellt den Antrag mit allen Feldern, die sich ändern.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await LadenAsync(ct))
        {
            return NotFound();
        }

        if (Offener is not null)
        {
            ModelState.AddModelError("", "Es ist schon ein Antrag offen. Zieh ihn zurück, wenn du ihn ändern möchtest.");
            return Page();
        }

        var gewuenscht = new Dictionary<AenderungFeld, string?>();
        foreach (var feld in Feldgruppen.SelectMany(g => g.Felder))
        {
            if (!Werte.TryGetValue(feld.ToString(), out var roh))
            {
                continue;
            }

            var wert = Aenderungsfelder.Bereinigen(roh);
            var schluessel = $"{nameof(Werte)}[{feld}]";
            if (wert?.Length > Aenderungsfelder.Laenge(feld))
            {
                ModelState.AddModelError(schluessel, $"Höchstens {Aenderungsfelder.Laenge(feld)} Zeichen.");
            }
            else if (wert is not null && feld is AenderungFeld.Email or AenderungFeld.ArbeitgeberEmail && !new EmailAddressAttribute().IsValid(wert))
            {
                ModelState.AddModelError(schluessel, "Das ist keine gültige E-Mail-Adresse.");
            }

            gewuenscht[feld] = wert;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Aenderungsantrag.Erstellen(Mitglied!, gewuenscht, Bemerkung) is not { } antrag)
        {
            ModelState.AddModelError("", "Du hast nichts geändert.");
            return Page();
        }

        db.Aenderungsantraege.Add(antrag);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Antrag gestellt ({antrag.Felder.Count} Angabe(n)). Die Personalverwaltung prüft ihn; hier siehst du, wie es steht.";
        return RedirectToPage(new { kind = Kind });
    }

    /// <summary>Zieht den offenen Antrag zurück.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder 404.</returns>
    public async Task<IActionResult> OnPostZurueckziehenAsync(CancellationToken ct)
    {
        if (!await LadenAsync(ct) || Offener is null)
        {
            return NotFound();
        }

        var antrag = await db.Aenderungsantraege.FirstAsync(a => a.Id == Offener.Id, ct);
        antrag.Zurueckziehen(zeit.GetUtcNow());
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Antrag zurückgezogen.";
        return RedirectToPage(new { kind = Kind });
    }

    private async Task<bool> LadenAsync(CancellationToken ct)
    {
        if (await AkteModel.MitgliedAsync(planung, benutzer, Kind, ct) is not { } mitgliedId)
        {
            return false;
        }

        Mitglied = await db.Mitglieder.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mitgliedId, ct);
        if (Mitglied is null)
        {
            return false;
        }

        var antraege = await db.Aenderungsantraege.AsNoTracking().Include(a => a.Felder)
            .Where(a => a.MitgliedId == mitgliedId)
            .OrderByDescending(a => a.ErstelltAm)
            .ToListAsync(ct);
        Offener = antraege.FirstOrDefault(a => a.Offen);
        Erledigte = antraege.Where(a => !a.Offen).ToList();
        return true;
    }
}
