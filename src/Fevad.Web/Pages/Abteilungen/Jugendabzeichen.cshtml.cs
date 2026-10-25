using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Abteilungen;

/// <summary>
/// Abzeichen der Jugendfeuerwehr (Kinderfunke, Jugendflamme, Leistungsspange) für den Jugendwart:
/// Übersicht und Erfassung, beschränkt auf die aktuellen Mitglieder der Jugend- und Kinderfeuerwehr.
/// </summary>
[Authorize(Policy = Berechtigungen.JugendLesen)]
public class JugendabzeichenModel(
    AppDbContext db, Personalfristenabfrage fristen, Abzeichenabfrage abzeichen, TimeProvider zeit, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Abzeichen, das erfasst wird.</summary>
    [BindProperty] public Guid? AbzeichenId { get; set; }
    /// <summary>Tag der Abnahme.</summary>
    [BindProperty] public DateOnly? Datum { get; set; }
    /// <summary>Ort der Abnahme.</summary>
    [BindProperty] public string? Abnahmeort { get; set; }
    /// <summary>Urkundennummer oder Vermerk zur Urkunde.</summary>
    [BindProperty] public string? Urkunde { get; set; }
    /// <summary>Mitglieder, die das Abzeichen erworben haben.</summary>
    [BindProperty] public List<Guid> Mitglieder { get; set; } = [];

    /// <summary>Heutiges Datum in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }
    /// <summary>Der Benutzer darf Abzeichen erfassen und löschen.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Aktive Abzeichen der Jugendfeuerwehr, deren Alter nach Jahrgang zählt.</summary>
    public IReadOnlyList<Qualifikation> Abzeichen { get; private set; } = [];
    /// <summary>Je Mitglied der Jugend- und Kinderfeuerwehr eine Zeile.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Die 15 zuletzt erfassten Abzeichen.</summary>
    public IReadOnlyList<QualifikationNachweis> Zuletzt { get; private set; } = [];

    /// <summary>Abzeichenstand eines Mitglieds.</summary>
    /// <param name="Id">Kennung des Mitglieds.</param>
    /// <param name="Name">Anzeigename „Nachname, Vorname“.</param>
    /// <param name="Jahrgangsalter">Alter nach Jahrgang (aktuelles Jahr minus Geburtsjahr).</param>
    /// <param name="Hoechste">Erworbene Abzeichen ohne die durch höhere Stufen abgelösten, ohne Leistungsspange.</param>
    /// <param name="Spange">Erste erworbene Leistungsspange, falls vorhanden.</param>
    /// <param name="Spangenhinweis">Fristhinweis aus dem Bereich Jugend, z. B. zur Leistungsspange.</param>
    public sealed record Zeile(Guid Id, string Name, int Jahrgangsalter, IReadOnlyList<QualifikationNachweis> Hoechste, QualifikationNachweis? Spange, Fristeintrag? Spangenhinweis);

    /// <summary>Lädt Abzeichen und Mitglieder.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    /// <summary>Erfasst das gewählte Abzeichen für die markierten Mitglieder.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, die Seite mit Fehlern oder 403.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.JugendBearbeiten))
        {
            return Forbid();
        }

        Heute = Anzeige.Heute(zeit);
        var ziel = AbzeichenId is { } aid
            ? await db.Qualifikationen.Include(q => q.Vorstufe).FirstOrDefaultAsync(q => q.Id == aid && q.Art == QualifikationArt.Abzeichen && q.AlterNachJahrgang, ct)
            : null;
        var fehler = ziel is null ? "Bitte ein Abzeichen auswählen."
            : Datum is null ? "Datum fehlt."
            : Datum > Heute ? "Das Datum liegt in der Zukunft."
            : Mitglieder.Count == 0 ? "Bitte mindestens ein Mitglied ankreuzen."
            : Abnahmeort?.Length > 200 || Urkunde?.Length > 100 ? "Abnahmeort höchstens 200, Urkunde höchstens 100 Zeichen."
            : null;
        if (fehler is not null)
        {
            ModelState.AddModelError(string.Empty, fehler);
            await LadenAsync(ct);
            return Page();
        }

        // Nur wer heute in der Jugendfeuerwehr ist; andere Mitglieder trägt der Kommandant in der Personalakte ein.
        var ids = await JugendQuery(Heute).Where(m => Mitglieder.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct);
        var datum = Datum!.Value;
        var vorhanden = await db.QualifikationNachweise.Where(n => ids.Contains(n.MitgliedId) && n.QualifikationId == ziel!.Id && n.ErworbenAm == datum)
            .Select(n => n.MitgliedId).ToListAsync(ct);
        var neu = ids.Except(vorhanden).ToList();

        var offen = new List<string>();
        foreach (var id in neu)
        {
            var nicht = (await abzeichen.PruefenAsync(id, ziel!, datum, ct)).Where(v => !v.Erfuellt).Select(v => v.Text).ToList();
            if (nicht.Count > 0)
            {
                var name = await db.Mitglieder.Where(m => m.Id == id).Select(m => m.Vorname + " " + m.Nachname).SingleAsync(ct);
                offen.Add($"{name}: {string.Join(", ", nicht)}");
            }

            db.QualifikationNachweise.Add(new QualifikationNachweis
            {
                MitgliedId = id, QualifikationId = ziel!.Id, ErworbenAm = datum, Lehrgangsort = Leer(Abnahmeort), Urkunde = Leer(Urkunde),
            });
        }

        await db.SaveChangesAsync(ct);
        var doppelt = ids.Count - neu.Count;
        TempData["Meldung"] = $"{ziel!.Bezeichnung} am {Anzeige.Datum(datum)} für {neu.Count} Mitglieder eingetragen"
            + (doppelt > 0 ? $", {doppelt} waren schon eingetragen." : ".")
            + (offen.Count > 0 ? " Bitte prüfen, nicht erfüllt: " + string.Join("; ", offen) + "." : "");
        return RedirectToPage();
    }

    /// <summary>Löscht einen erfassten Abzeichennachweis eines Jugendmitglieds.</summary>
    /// <param name="eintragId">Kennung des Nachweises.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid eintragId, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.JugendBearbeiten))
        {
            return Forbid();
        }

        Heute = Anzeige.Heute(zeit);
        var jugend = JugendQuery(Heute).Select(m => m.Id);
        var eintrag = await db.QualifikationNachweise
            .FirstOrDefaultAsync(n => n.Id == eintragId && jugend.Contains(n.MitgliedId)
                && n.Qualifikation!.Art == QualifikationArt.Abzeichen && n.Qualifikation.AlterNachJahrgang, ct);
        if (eintrag is null)
        {
            return NotFound();
        }

        db.Remove(eintrag);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Eintrag gelöscht.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        Datum ??= Heute;
        DarfBearbeiten = await DarfAsync(Berechtigungen.JugendBearbeiten);

        Abzeichen = await db.Qualifikationen.AsNoTracking()
            .Where(q => q.Aktiv && q.Art == QualifikationArt.Abzeichen && q.AlterNachJahrgang)
            .OrderBy(q => q.Sortierung)
            .ToListAsync(ct);
        var katalog = await db.Qualifikationen.AsNoTracking().ToDictionaryAsync(q => q.Id, ct);

        var personen = await JugendQuery(Heute).AsNoTracking()
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, m.Nachname, m.Vorname, m.Geburtsdatum })
            .ToListAsync(ct);
        var ids = personen.Select(p => p.Id).ToList();
        var nachweise = await db.QualifikationNachweise.AsNoTracking().Include(n => n.Qualifikation)
            .Where(n => ids.Contains(n.MitgliedId) && n.Qualifikation!.Art == QualifikationArt.Abzeichen && n.Qualifikation.AlterNachJahrgang)
            .ToListAsync(ct);
        var hinweise = (await fristen.LadenAsync(Heute, ct: ct)).Where(f => f.Bereich == Fristbereich.Jugend).ToDictionary(f => f.MitgliedId);
        var je = nachweise.ToLookup(n => n.MitgliedId);

        Zeilen = personen.Select(p =>
        {
            var eigene = je[p.Id].ToList();
            var abgeloest = Abzeichenpruefung.Abgeloest(eigene, katalog);
            var hoechste = eigene.Where(n => !abgeloest.Contains(n.Id) && n.Qualifikation!.Code != Qualifikation.CodeLeistungsspange)
                .GroupBy(n => n.QualifikationId).Select(g => g.MinBy(n => n.ErworbenAm)!)
                .OrderBy(n => n.Qualifikation!.Sortierung).ToList();
            var spange = eigene.Where(n => n.Qualifikation!.Code == Qualifikation.CodeLeistungsspange).MinBy(n => n.ErworbenAm);
            return new Zeile(p.Id, $"{p.Nachname}, {p.Vorname}", Heute.Year - p.Geburtsdatum.Year, hoechste, spange, hinweise.GetValueOrDefault(p.Id));
        }).ToList();

        Zuletzt = nachweise.OrderByDescending(n => n.ErstelltAm).Take(15).ToList();
    }

    private IQueryable<Mitglied> JugendQuery(DateOnly stichtag) =>
        db.Mitglieder.Where(m => m.Mitgliedschaften.Any(s => (s.Abteilung!.Art == AbteilungArt.Jugend || s.Abteilung.Art == AbteilungArt.Kinder)
            && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag)));

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
