using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Atemschutz;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Atemschutz;

/// <summary>
/// Atemschutz-Übersicht: aktive Geräteträger mit Einsatzstatus, Abmeldungen „vorübergehend nicht einsetzbar“ und die Arbeitsliste
/// unbestätigter Nachweise (V11, V62). Nur Status und Fristen, keine Befunde.
/// </summary>
[Authorize(Policy = Berechtigungen.AtemschutzLesen)]
public class IndexModel(AppDbContext db, Atemschutzabfrage abfrage, IAuthorizationService autorisierung, IAktuellerBenutzer benutzer, Leseprotokoll leseprotokoll, TimeProvider zeit) : PageModel
{
    /// <summary>Ein Nachweis, der noch eine Bestätigung braucht, mit Name und Einsatznummer.</summary>
    public sealed record OffenerNachweis(AtemschutzNachweis Nachweis, string Name, string? Einsatznummer);

    /// <summary>Eine Abmeldung mit Name und der Angabe, ob die Genehmigung noch fehlt.</summary>
    public sealed record SperreZeile(AtemschutzSperre Sperre, string Name, bool GenehmigungOffen);

    /// <summary>Alle überwachten Geräteträger mit Status.</summary>
    public IReadOnlyList<Atemschutzabfrage.Traeger> Traeger { get; private set; } = [];
    /// <summary>Mitglieder mit AGT-Lehrgang ohne Kennzeichen, zum Nachkennzeichnen.</summary>
    public IReadOnlyList<SelectListItem> OhneKennzeichen { get; private set; } = [];
    /// <summary>Wirksame und künftige Abmeldungen.</summary>
    public IReadOnlyList<SperreZeile> Sperren { get; private set; } = [];
    /// <summary>Nachweise, die der angemeldete Benutzer noch bestätigen kann.</summary>
    public IReadOnlyList<OffenerNachweis> Offen { get; private set; } = [];
    /// <summary>Höchstdauer einer Selbstmeldung ohne Genehmigung.</summary>
    public int HoechstTage { get; private set; }
    /// <summary>Heutiges Datum in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Darf Abmeldungen und Kennzeichen pflegen.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Darf als Kommando bestätigen.</summary>
    public bool AlsKommando { get; private set; }
    /// <summary>Darf als Atemschutzgerätewart bestätigen.</summary>
    public bool AlsWart { get; private set; }

    /// <summary>Formular für eine neue Abmeldung durch Kommando oder Atemschutzwart.</summary>
    public sealed class SperreEingabe
    {
        /// <summary>Abgemeldeter Geräteträger.</summary>
        [Required(ErrorMessage = "Geräteträger fehlt.")] public Guid? MitgliedId { get; set; }
        /// <summary>Erster Tag der Abmeldung.</summary>
        [Required(ErrorMessage = "Beginn fehlt.")] public DateOnly? Von { get; set; }
        /// <summary>Letzter Tag der Abmeldung.</summary>
        [Required(ErrorMessage = "Ende fehlt.")] public DateOnly? Bis { get; set; }
    }

    /// <summary>Zeigt Bestätigungen, Abmeldungen und Geräteträger.</summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        DarfBearbeiten = await DarfAsync(Berechtigungen.AtemschutzBearbeiten);
        AlsKommando = await DarfAsync(Berechtigungen.AtemschutzBestaetigenKommando);
        AlsWart = await DarfAsync(Berechtigungen.AtemschutzBestaetigenWart);
        HoechstTage = await abfrage.HoechstTageAsync(Heute, ct);
        Traeger = await abfrage.TraegerAsync(Heute, ct);
        await leseprotokoll.ProtokollierenAsync(Leseprotokoll.Atemschutz, "Atemschutz-Übersicht", Traeger.Select(t => t.MitgliedId), ct);

        OhneKennzeichen = await db.Mitglieder.AsNoTracking()
            .Where(m => !m.AtemschutzAktiv && m.AnonymisiertAm == null
                && db.QualifikationNachweise.Any(q => q.MitgliedId == m.Id && q.Qualifikation!.Code == Qualifikation.CodeAtemschutz)
                && m.Mitgliedschaften.Any(s => s.GueltigAb <= Heute && (s.GueltigBis == null || s.GueltigBis >= Heute)
                    && s.Abteilung!.Art == Fevad.Domain.Mitglieder.AbteilungArt.Einsatz))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new SelectListItem(m.Nachname + ", " + m.Vorname, m.Id.ToString()))
            .ToListAsync(ct);

        var namen = await db.Mitglieder.AsNoTracking().Select(m => new { m.Id, Name = m.Nachname + ", " + m.Vorname }).ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        Sperren = (await db.AtemschutzSperren.AsNoTracking().Where(s => s.AufgehobenAm == null && s.Bis >= Heute).OrderBy(s => s.Von).ToListAsync(ct))
            .Select(s => new SperreZeile(s, namen.GetValueOrDefault(s.MitgliedId, "?"), s.GenehmigungOffen(HoechstTage)))
            .ToList();

        if (AlsKommando || AlsWart)
        {
            var offen = await abfrage.Unbestaetigt(AlsKommando, AlsWart).AsNoTracking().OrderBy(n => n.Datum).Take(500).ToListAsync(ct);
            var einsatzIds = offen.Where(n => n.EinsatzId != null).Select(n => n.EinsatzId!.Value).Distinct().ToList();
            var nummern = await db.Einsaetze.AsNoTracking().Where(e => einsatzIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Einsatznummer, ct);
            Offen = offen.Select(n => new OffenerNachweis(n, namen.GetValueOrDefault(n.MitgliedId, "?"),
                n.EinsatzId is { } e ? nummern.GetValueOrDefault(e) : null)).ToList();
        }
    }

    /// <summary>Markierte Nachweise mit den eigenen Rechten bestätigen.</summary>
    public async Task<IActionResult> OnPostBestaetigenAsync(List<Guid> auswahl, CancellationToken ct)
    {
        var kommando = await DarfAsync(Berechtigungen.AtemschutzBestaetigenKommando);
        var wart = await DarfAsync(Berechtigungen.AtemschutzBestaetigenWart);
        if ((!kommando && !wart) || benutzer.Id is not { } id)
        {
            return Forbid();
        }

        var jetzt = zeit.GetUtcNow();
        var nachweise = await db.AtemschutzNachweise.Where(n => auswahl.Contains(n.Id)).ToListAsync(ct);
        var anzahl = nachweise.Count(n => n.Bestaetigen(id, kommando, wart, jetzt));
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = anzahl == 0
            ? "Nichts bestätigt. Dieselbe Person kann einen Nachweis nur einmal bestätigen."
            : $"{anzahl} Nachweise bestätigt.";
        return RedirectToPage();
    }

    /// <summary>Setzt oder entfernt das Kennzeichen „aktiver Atemschutzgeräteträger“.</summary>
    public async Task<IActionResult> OnPostKennzeichnenAsync(Guid mitgliedId, bool aktiv, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AtemschutzBearbeiten))
        {
            return Forbid();
        }

        var mitglied = await db.Mitglieder.FirstOrDefaultAsync(m => m.Id == mitgliedId, ct);
        if (mitglied is null)
        {
            return NotFound();
        }

        mitglied.AtemschutzAktiv = aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = aktiv
            ? $"{mitglied.Vorname} {mitglied.Nachname} ist als aktiver Atemschutzgeräteträger gekennzeichnet und wird überwacht."
            : $"{mitglied.Vorname} {mitglied.Nachname} wird nicht mehr als Atemschutzgeräteträger überwacht.";
        return RedirectToPage();
    }

    /// <summary>Abmeldung durch Kommando oder Atemschutzwart; braucht keine Genehmigung.</summary>
    public async Task<IActionResult> OnPostSperreAsync(SperreEingabe eingabe, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AtemschutzBearbeiten))
        {
            return Forbid();
        }

        if (!ModelState.IsValid || eingabe.Bis < eingabe.Von || !await db.Mitglieder.AnyAsync(m => m.Id == eingabe.MitgliedId && m.AtemschutzAktiv, ct))
        {
            TempData["Fehler"] = eingabe.Bis < eingabe.Von ? "Das Ende liegt vor dem Beginn." : "Bitte Geräteträger, Beginn und Ende angeben.";
            return RedirectToPage();
        }

        var jetzt = zeit.GetUtcNow();
        db.AtemschutzSperren.Add(new AtemschutzSperre
        {
            MitgliedId = eingabe.MitgliedId!.Value, Von = eingabe.Von!.Value, Bis = eingabe.Bis!.Value,
            GemeldetAm = jetzt, GemeldetVon = benutzer.Id, GenehmigtAm = jetzt, GenehmigtVon = benutzer.Id,
        });
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Nicht einsetzbar vom {Anzeige.Datum(eingabe.Von.Value)} bis {Anzeige.Datum(eingabe.Bis.Value)} eingetragen.";
        return RedirectToPage();
    }

    /// <summary>Genehmigt eine Selbstmeldung über die Höchstdauer hinaus.</summary>
    public async Task<IActionResult> OnPostGenehmigenAsync(Guid sperreId, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AtemschutzBearbeiten))
        {
            return Forbid();
        }

        var sperre = await db.AtemschutzSperren.FirstOrDefaultAsync(s => s.Id == sperreId, ct);
        if (sperre is null)
        {
            return NotFound();
        }

        sperre.GenehmigtAm ??= zeit.GetUtcNow();
        sperre.GenehmigtVon ??= benutzer.Id;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Abmeldung genehmigt.";
        return RedirectToPage();
    }

    /// <summary>Hebt eine Abmeldung auf; der Eintrag bleibt mit Zeitpunkt und Person im Protokoll.</summary>
    public async Task<IActionResult> OnPostAufhebenAsync(Guid sperreId, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AtemschutzBearbeiten))
        {
            return Forbid();
        }

        var sperre = await db.AtemschutzSperren.FirstOrDefaultAsync(s => s.Id == sperreId && s.AufgehobenAm == null, ct);
        if (sperre is null)
        {
            return NotFound();
        }

        sperre.AufgehobenAm = zeit.GetUtcNow();
        sperre.AufgehobenVon = benutzer.Id;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Abmeldung aufgehoben.";
        return RedirectToPage();
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;
}
