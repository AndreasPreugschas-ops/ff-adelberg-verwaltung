using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Abteilungen;

/// <summary>
/// Abzeichen der Jugendfeuerwehr (Kinderfunke, Jugendflamme, Leistungsspange) für den Jugendwart:
/// Übersicht und Erfassung, beschränkt auf die aktuellen Mitglieder der Jugend- und Kinderfeuerwehr. Die Jugendflamme wird nach
/// dem Regelwerk des Abnahmeorts abgenommen (V50a); die Vorschlagsliste zeigt, wer in einem Jahr eine Stufe ablegen kann (V49).
/// </summary>
[Authorize(Policy = Berechtigungen.JugendLesen)]
public class JugendabzeichenModel(
    AppDbContext db, Personalfristenabfrage fristen, Abzeichenabfrage abzeichen, ParameterLeser parameter, TimeProvider zeit, IAuthorizationService autorisierung) : PageModel
{
    private static readonly CultureInfo Deutsch = new("de-DE");

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
    /// <summary>Regelwerk des Abnahmeorts; gilt nur für Abzeichen, die das Regelwerk kennt.</summary>
    [BindProperty] public Guid? RegelwerkId { get; set; }

    /// <summary>Abzeichen der Vorschlagsliste.</summary>
    [BindProperty(SupportsGet = true)] public Guid? VorschlagId { get; set; }
    /// <summary>Jahr der Vorschlagsliste; das Alter zählt nach Jahrgang, Nachweise zum 31.12.</summary>
    [BindProperty(SupportsGet = true)] public int? VorschlagJahr { get; set; }

    /// <summary>Aktive Regelwerke mit ihren Stufen.</summary>
    public IReadOnlyList<Abzeichenregelwerk> Regelwerke { get; private set; } = [];
    /// <summary>Das gewählte Abzeichen der Vorschlagsliste.</summary>
    public Qualifikation? Vorschlag { get; private set; }
    /// <summary>Zeilen der Vorschlagsliste, erfüllte zuerst.</summary>
    public IReadOnlyList<Vorschlagszeile> Vorschlaege { get; private set; } = [];
    /// <summary>Bewerber je Gruppe für die Leistungsspange.</summary>
    public int Gruppenstaerke { get; private set; }

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

    /// <summary>Prüfergebnis nach einem Regelwerk oder nach dem Katalog.</summary>
    /// <param name="Regelwerk">Kurzname des Regelwerks oder „Katalog“.</param>
    /// <param name="Voraussetzungen">Die geprüften Bedingungen.</param>
    /// <param name="Hinweis">Hinweis des Regelwerks zur Stufe.</param>
    public sealed record Pruefergebnis(string Regelwerk, IReadOnlyList<Voraussetzung> Voraussetzungen, string? Hinweis)
    {
        /// <summary>Alle Bedingungen sind erfüllt.</summary>
        public bool Erfuellt => Voraussetzungen.All(v => v.Erfuellt);
    }

    /// <summary>Ein Mitglied in der Vorschlagsliste.</summary>
    /// <param name="Id">Kennung des Mitglieds.</param>
    /// <param name="Name">Anzeigename.</param>
    /// <param name="Jahrgangsalter">Alter nach Jahrgang im Jahr der Liste.</param>
    /// <param name="SchonErworben">Das Abzeichen ist schon erworben.</param>
    /// <param name="Ergebnisse">Je Regelwerk ein Ergebnis; ohne Regelwerk eines nach dem Katalog.</param>
    /// <param name="LetztesJahr">Im Folgejahr ist das Mitglied zu alt (Höchstalter).</param>
    public sealed record Vorschlagszeile(Guid Id, string Name, int Jahrgangsalter, bool SchonErworben, IReadOnlyList<Pruefergebnis> Ergebnisse, bool LetztesJahr)
    {
        /// <summary>Noch nicht erworben und nach mindestens einem Regelwerk erfüllt.</summary>
        public bool Moeglich => !SchonErworben && Ergebnisse.Any(e => e.Erfuellt);
    }

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

        // Das Regelwerk gilt nur, wenn es die Stufe kennt; sonst zählt der Katalog (z. B. Kinderfunke, Leistungsspange).
        var regel = await abzeichen.RegelAsync(RegelwerkId, ziel!.Id, ct);
        var offen = new List<string>();
        foreach (var id in neu)
        {
            var nicht = (await abzeichen.PruefenAsync(id, ziel!, datum, ct, regel)).Where(v => !v.Erfuellt).Select(v => v.Text).ToList();
            if (nicht.Count > 0)
            {
                var name = await db.Mitglieder.Where(m => m.Id == id).Select(m => m.Vorname + " " + m.Nachname).SingleAsync(ct);
                offen.Add($"{name}: {string.Join(", ", nicht)}");
            }

            db.QualifikationNachweise.Add(new QualifikationNachweis
            {
                MitgliedId = id, QualifikationId = ziel!.Id, ErworbenAm = datum, Lehrgangsort = Leer(Abnahmeort), Urkunde = Leer(Urkunde),
                AbzeichenregelwerkId = regel?.RegelwerkId,
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

    /// <summary>
    /// Anmeldeliste der Vorschlagsliste als CSV: alle, die das Abzeichen im Jahr ablegen können, mit Geburtsdatum und DJF-Ausweis
    /// (Meldung an die Kreisjugendfeuerwehr, Leistungsspange).
    /// </summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die CSV-Datei oder 404 ohne gewähltes Abzeichen.</returns>
    public async Task<IActionResult> OnGetAnmeldelisteAsync(CancellationToken ct)
    {
        await LadenAsync(ct);
        if (Vorschlag is null)
        {
            return NotFound();
        }

        var ids = Vorschlaege.Where(v => v.Moeglich).Select(v => v.Id).ToList();
        var personen = await db.Mitglieder.AsNoTracking().Where(m => ids.Contains(m.Id))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Nachname, m.Vorname, m.Geburtsdatum, m.Geschlecht, m.DjfAusweisnummer, m.DjfAusweisAm })
            .ToListAsync(ct);
        var csv = new StringBuilder("Nachname;Vorname;Geburtsdatum;Geschlecht;DJF-Ausweis;Ausweis vom\r\n");
        foreach (var p in personen)
        {
            csv.Append($"{Feld(p.Nachname)};{Feld(p.Vorname)};{p.Geburtsdatum.ToString("dd.MM.yyyy", Deutsch)};{(p.Geschlecht is { } g ? Anzeige.Text(g) : "")};"
                + $"{Feld(p.DjfAusweisnummer)};{p.DjfAusweisAm?.ToString("dd.MM.yyyy", Deutsch)}\r\n");
        }

        var datei = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(datei, "text/csv; charset=utf-8", $"Anmeldeliste_{Vorschlag.Code}_{VorschlagJahr}.csv");
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
        var nachweise = await db.QualifikationNachweise.AsNoTracking().Include(n => n.Qualifikation).Include(n => n.Abzeichenregelwerk)
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

        Regelwerke = await db.Abzeichenregelwerke.AsNoTracking().Include(r => r.Stufen)
            .Where(r => r.Aktiv).OrderBy(r => r.Sortierung).ThenBy(r => r.Bezeichnung).ToListAsync(ct);
        RegelwerkId ??= Regelwerke.FirstOrDefault(r => r.Standard)?.Id;
        Gruppenstaerke = await parameter.ZahlAsync(ParameterSchluessel.LeistungsspangeGruppenstaerke, Heute, 9, ct);

        VorschlagJahr ??= Heute.Year;
        Vorschlag = VorschlagId is { } vid ? Abzeichen.FirstOrDefault(a => a.Id == vid) : null;
        if (Vorschlag is null || VorschlagJahr is < 2000 or > 2100)
        {
            Vorschlag = null;
            return;
        }

        var ziel = Vorschlag;
        ziel.Vorstufe = ziel.VorstufeId is { } vs ? katalog.GetValueOrDefault(vs) : null;
        var stichtag = new DateOnly(VorschlagJahr.Value, 12, 31);
        var namen = katalog.Values.GroupBy(q => q.Code).ToDictionary(g => g.Key, g => g.First().Bezeichnung);
        var alleNachweise = (await db.QualifikationNachweise.AsNoTracking().Include(n => n.Qualifikation)
            .Where(n => ids.Contains(n.MitgliedId)).ToListAsync(ct)).ToLookup(n => n.MitgliedId);
        var jugendSeit = (await db.Mitgliedschaften.AsNoTracking()
                .Where(s => ids.Contains(s.MitgliedId) && s.Abteilung!.Art == AbteilungArt.Jugend)
                .GroupBy(s => s.MitgliedId).Select(g => new { g.Key, Seit = g.Min(s => s.GueltigAb) }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => (DateOnly?)x.Seit);
        var regeln = Regelwerke.SelectMany(r => r.Stufen.Where(st => st.QualifikationId == ziel.Id).Select(st => (Regelwerk: r, Stufe: st))).ToList();

        Vorschlaege = personen.Select(p =>
        {
            var vorhanden = alleNachweise[p.Id].ToList();
            IReadOnlyList<Pruefergebnis> ergebnisse = regeln.Count == 0
                ? [new Pruefergebnis("Katalog", Abzeichenpruefung.Pruefen(ziel, stichtag, p.Geburtsdatum, vorhanden, jugendSeit.GetValueOrDefault(p.Id), namen), null)]
                : regeln.Select(r => new Pruefergebnis(r.Regelwerk.Kurzbezeichnung ?? r.Regelwerk.Bezeichnung,
                    Abzeichenpruefung.Pruefen(ziel, stichtag, p.Geburtsdatum, vorhanden, jugendSeit.GetValueOrDefault(p.Id), namen, r.Stufe), r.Stufe.Hinweis)).ToList();
            var alter = stichtag.Year - p.Geburtsdatum.Year;
            return new Vorschlagszeile(p.Id, $"{p.Nachname}, {p.Vorname}", alter, vorhanden.Any(n => n.QualifikationId == ziel.Id), ergebnisse,
                ziel.HoechstalterJahre is { } hoechst && alter == hoechst);
        })
        .OrderByDescending(v => v.Moeglich).ThenBy(v => v.Name)
        .ToList();
    }

    private static string Feld(string? text) =>
        text is null ? "" : text.Contains(';') || text.Contains('"') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;

    private IQueryable<Mitglied> JugendQuery(DateOnly stichtag) =>
        db.Mitglieder.Where(m => m.Mitgliedschaften.Any(s => (s.Abteilung!.Art == AbteilungArt.Jugend || s.Abteilung.Art == AbteilungArt.Kinder)
            && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag)));

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
