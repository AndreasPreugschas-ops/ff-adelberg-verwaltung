using System.Text.RegularExpressions;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Laufbahn;

/// <summary>Laufbahn eines Mitglieds: Mitgliedschaften und Übertritte, Dienstgrade, Ehrungen.</summary>
public partial class MitgliedModel(AppDbContext db, Laufbahnabfrage laufbahn, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    public DateOnly Heute { get; private set; }
    public bool DarfBearbeiten { get; private set; }
    public bool DarfBefreiungsgrundSehen { get; private set; }

    /// <summary>Warnungen zur Anrechnung; ohne Recht auf den Befreiungsgrund ohne den Grund.</summary>
    public IReadOnlyList<string> NichtAngerechnet => DarfBefreiungsgrundSehen
        ? Stand.NichtAngerechnet
        : Stand.NichtAngerechnet.Select(t => GrundImHinweis().Replace(t, "Befreiung ab $1: nur teilweise angerechnet.")).ToList();

    [GeneratedRegex(@"^Kindererziehung ab (\S+):.*$")]
    private static partial Regex GrundImHinweis();
    public Laufbahnstand Stand { get; private set; } = null!;
    public IReadOnlyList<Mitgliedschaft> Mitgliedschaften { get; private set; } = [];
    public IReadOnlyList<DienstgradZuordnung> Dienstgrade { get; private set; } = [];
    public IReadOnlyList<Ehrung> Ehrungen { get; private set; } = [];
    public IReadOnlyList<Befoerderungsweg> WegeZumVorschlag { get; private set; } = [];

    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];
    public IReadOnlyList<SelectListItem> DienstgradAuswahl { get; private set; } = [];
    public IReadOnlyList<SelectListItem> EhrungsartAuswahl { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Heute = Anzeige.Heute(zeit);
        var stand = (await laufbahn.LadenAsync(Heute, [id], ct)).SingleOrDefault();
        if (stand is null)
        {
            return NotFound();
        }

        Stand = stand;
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.LaufbahnBearbeiten)).Succeeded;
        DarfBefreiungsgrundSehen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.LaufbahnBefreiungsgrund)).Succeeded;

        Mitgliedschaften = await db.Mitgliedschaften.AsNoTracking().Include(m => m.Abteilung)
            .Where(m => m.MitgliedId == id).OrderByDescending(m => m.GueltigAb).ToListAsync(ct);
        Dienstgrade = await db.DienstgradZuordnungen.AsNoTracking().Include(d => d.Dienstgrad)
            .Where(d => d.MitgliedId == id).OrderByDescending(d => d.Ab).ToListAsync(ct);
        Ehrungen = await db.Ehrungen.AsNoTracking().Include(e => e.Ehrungsart)
            .Where(e => e.MitgliedId == id).OrderByDescending(e => e.VerliehenAm ?? e.VoraussetzungAm).ToListAsync(ct);
        if (stand.Befoerderung is { } b)
        {
            WegeZumVorschlag = await db.Befoerderungswege.AsNoTracking().Where(w => w.DienstgradId == b.Dienstgrad.Id).ToListAsync(ct);
        }

        Abteilungen = await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv).OrderBy(a => a.Sortierung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToListAsync(ct);
        DienstgradAuswahl = await db.Dienstgrade.AsNoTracking().Where(d => d.Aktiv).OrderBy(d => d.Sortierung)
            .Select(d => new SelectListItem(d.Bezeichnung, d.Id.ToString(), stand.Befoerderung != null && stand.Befoerderung.Dienstgrad.Id == d.Id))
            .ToListAsync(ct);
        var vorschlag = stand.Ehrungsvorschlaege.FirstOrDefault()?.EhrungsartId;
        EhrungsartAuswahl = await db.Ehrungsarten.AsNoTracking().Where(e => e.Aktiv).OrderBy(e => e.Sortierung)
            .Select(e => new SelectListItem(e.Bezeichnung, e.Id.ToString(), e.Id == vorschlag))
            .ToListAsync(ct);
        return Page();
    }

    public Task<IActionResult> OnPostUebertrittAsync(Guid id, Guid mitgliedschaftId, Guid zielAbteilungId, MitgliedStatus status, DateOnly? ab, CancellationToken ct) =>
        AendernAsync(id, async () =>
        {
            var alt = await OffeneMitgliedschaftAsync(id, mitgliedschaftId, ct);
            var ziel = await db.Abteilungen.SingleAsync(a => a.Id == zielAbteilungId, ct);
            var mitglied = await db.Mitglieder.SingleAsync(m => m.Id == id, ct);
            var datum = ab ?? Heute;
            if (Mitgliedschaftswechsel.Pruefen(mitglied.Geburtsdatum, ziel.Code, datum, await laufbahn.RegelnAsync(datum, ct)) is { } fehler)
            {
                throw new InvalidOperationException(fehler);
            }

            var neu = Mitgliedschaftswechsel.Uebertreten(alt, ziel.Id, status, datum);
            await db.SaveChangesAsync(ct);
            db.Mitgliedschaften.Add(neu);
            return $"Übertritt in die {ziel.Bezeichnung} zum {Anzeige.Datum(datum)} gespeichert.";
        }, ct);

    public Task<IActionResult> OnPostProbezeitAsync(Guid id, Guid mitgliedschaftId, DateOnly? ab, CancellationToken ct) =>
        AendernAsync(id, async () =>
        {
            var alt = await OffeneMitgliedschaftAsync(id, mitgliedschaftId, ct);
            var neu = Mitgliedschaftswechsel.ProbezeitBestanden(alt, ab ?? Heute);
            await db.SaveChangesAsync(ct);
            db.Mitgliedschaften.Add(neu);
            return "Probezeit als bestanden eingetragen.";
        }, ct);

    public Task<IActionResult> OnPostAusscheidenAsync(Guid id, Guid mitgliedschaftId, DateOnly? bis, Austrittsgrund grund, CancellationToken ct) =>
        AendernAsync(id, async () =>
        {
            var alt = await OffeneMitgliedschaftAsync(id, mitgliedschaftId, ct);
            Mitgliedschaftswechsel.Beenden(alt, bis ?? Heute, grund);
            return $"Mitgliedschaft zum {Anzeige.Datum(bis ?? Heute)} beendet.";
        }, ct);

    public Task<IActionResult> OnPostBefreiungAsync(Guid id, Guid mitgliedschaftId, DateOnly? von, DateOnly? bis, Befreiungsgrund grund, CancellationToken ct) =>
        AendernAsync(id, async () =>
        {
            // Eine laufende Befreiung bleibt offen und wird später beendet; sonst wäre der offene Abschnitt einer aus der Zukunft.
            if (bis >= Heute)
            {
                throw new InvalidOperationException("Für eine laufende Befreiung das Ende leer lassen und später „Befreiung beenden“ nutzen.");
            }

            var alt = await OffeneMitgliedschaftAsync(id, mitgliedschaftId, ct);
            var neue = Mitgliedschaftswechsel.Befreien(alt, von ?? Heute, bis, grund);
            await db.SaveChangesAsync(ct);
            db.Mitgliedschaften.AddRange(neue);
            return $"Befreiung ({Anzeige.Text(grund)}) ab {Anzeige.Datum(von ?? Heute)} gespeichert.";
        }, ct);

    public Task<IActionResult> OnPostBefreiungBeendenAsync(Guid id, Guid mitgliedschaftId, DateOnly? ab, CancellationToken ct) =>
        AendernAsync(id, async () =>
        {
            var alt = await OffeneMitgliedschaftAsync(id, mitgliedschaftId, ct);
            var vorher = await db.Mitgliedschaften
                .Where(m => m.MitgliedId == id && m.AbteilungId == alt.AbteilungId && m.GueltigBis == alt.GueltigAb.AddDays(-1) && !m.Vordienstzeit)
                .Select(m => (MitgliedStatus?)m.Status).FirstOrDefaultAsync(ct);
            var neu = Mitgliedschaftswechsel.BefreiungBeenden(alt, ab ?? Heute, vorher ?? MitgliedStatus.Aktiv);
            await db.SaveChangesAsync(ct);
            db.Mitgliedschaften.Add(neu);
            return $"Befreiung zum {Anzeige.Datum((ab ?? Heute).AddDays(-1))} beendet, {Anzeige.Text(neu.Status)} ab {Anzeige.Datum(ab ?? Heute)}.";
        }, ct);

    /// <summary>Kommandant bestätigt Aufgaben nach Konzeption 65plus bzw. Übungsdienst der Musikabteilung (oder nimmt es zurück).</summary>
    public Task<IActionResult> OnPostBestaetigenAsync(Guid id, Guid mitgliedschaftId, bool bestaetigt, CancellationToken ct) =>
        AendernAsync(id, async () =>
        {
            var m = await db.Mitgliedschaften.Include(x => x.Abteilung).SingleOrDefaultAsync(x => x.Id == mitgliedschaftId && x.MitgliedId == id, ct)
                ?? throw new InvalidOperationException("Mitgliedschaft nicht gefunden.");
            if (m.Abteilung!.Code is not (AbteilungCode.Alters or AbteilungCode.Musik))
            {
                throw new InvalidOperationException("Eine Bestätigung gibt es nur für die Alters- und die Musikabteilung.");
            }

            m.EinsatzdienstBestaetigt = bestaetigt;
            return bestaetigt
                ? $"Bestätigt: Die Zeit in der {m.Abteilung.Bezeichnung} ab {Anzeige.Datum(m.GueltigAb)} zählt für das Feuerwehr-Ehrenzeichen."
                : $"Bestätigung für die {m.Abteilung.Bezeichnung} ab {Anzeige.Datum(m.GueltigAb)} zurückgenommen.";
        }, ct);

    public Task<IActionResult> OnPostVordienstzeitAsync(
        Guid id, Guid abteilungId, Vordienstart art, DateOnly von, DateOnly bis, string? bemerkung, CancellationToken ct) =>
        AendernAsync(id, () =>
        {
            if (bis < von)
            {
                throw new InvalidOperationException("Das Ende liegt vor dem Beginn.");
            }

            db.Mitgliedschaften.Add(new Mitgliedschaft
            {
                MitgliedId = id, AbteilungId = abteilungId, Status = MitgliedStatus.Aktiv,
                GueltigAb = von, GueltigBis = bis, Vordienstzeit = true, Vordienstart = art, Bemerkung = bemerkung,
            });
            return Task.FromResult("Vordienstzeit gespeichert.");
        }, ct);

    public Task<IActionResult> OnPostBefoerdernAsync(Guid id, Guid dienstgradId, DateOnly? ab, string? bemerkung, CancellationToken ct) =>
        AendernAsync(id, async () =>
        {
            var dienstgrad = await db.Dienstgrade.SingleAsync(d => d.Id == dienstgradId, ct);
            var datum = ab ?? Heute;
            if (await db.DienstgradZuordnungen.AnyAsync(d => d.MitgliedId == id && d.Ab >= datum, ct))
            {
                throw new InvalidOperationException("Es gibt schon einen Dienstgrad ab diesem Tag oder später.");
            }

            db.DienstgradZuordnungen.Add(new DienstgradZuordnung { MitgliedId = id, DienstgradId = dienstgrad.Id, Ab = datum, Bemerkung = bemerkung });
            return $"Beförderung zum {dienstgrad.Bezeichnung} zum {Anzeige.Datum(datum)} gespeichert.";
        }, ct);

    public Task<IActionResult> OnPostEhrungAsync(
        Guid id, Guid ehrungsartId, EhrungStatus status, DateOnly? voraussetzungAm, DateOnly? beantragtAm, DateOnly? verliehenAm,
        string? urkundennummer, string? bemerkung, CancellationToken ct) =>
        AendernAsync(id, async () =>
        {
            var art = await db.Ehrungsarten.Include(e => e.Vorstufe).SingleAsync(e => e.Id == ehrungsartId, ct);
            var ehrung = new Ehrung { MitgliedId = id, EhrungsartId = art.Id };
            Uebernehmen(ehrung, status, voraussetzungAm, beantragtAm, verliehenAm, urkundennummer, bemerkung);
            var erfasst = await db.Ehrungen.Where(e => e.MitgliedId == id)
                .Select(e => new ErfassteEhrung(e.EhrungsartId, e.Status, e.VerliehenAm)).ToListAsync(ct);
            db.Ehrungen.Add(ehrung);
            var hinweis = Ehrungsvorschlaege.VorstufeHinweis(art, art.Vorstufe, erfasst, ehrung.VerliehenAm ?? Heute);
            return hinweis is null ? $"{art.Bezeichnung} eingetragen." : $"{art.Bezeichnung} eingetragen. Hinweis: {hinweis}";
        }, ct);

    public Task<IActionResult> OnPostEhrungAendernAsync(
        Guid id, Guid ehrungId, EhrungStatus status, DateOnly? voraussetzungAm, DateOnly? beantragtAm, DateOnly? verliehenAm,
        string? urkundennummer, string? bemerkung, CancellationToken ct) =>
        AendernAsync(id, async () =>
        {
            var ehrung = await db.Ehrungen.SingleOrDefaultAsync(e => e.Id == ehrungId && e.MitgliedId == id, ct)
                ?? throw new InvalidOperationException("Ehrung nicht gefunden.");
            Uebernehmen(ehrung, status, voraussetzungAm, beantragtAm, verliehenAm, urkundennummer, bemerkung);
            return "Ehrung gespeichert.";
        }, ct);

    private void Uebernehmen(Ehrung ehrung, EhrungStatus status, DateOnly? voraussetzungAm, DateOnly? beantragtAm, DateOnly? verliehenAm,
        string? urkundennummer, string? bemerkung)
    {
        if (status == EhrungStatus.Verliehen && verliehenAm is null)
        {
            verliehenAm = Heute;
        }

        if (status == EhrungStatus.Beantragt && beantragtAm is null)
        {
            beantragtAm = Heute;
        }

        ehrung.Status = status;
        ehrung.VoraussetzungAm = voraussetzungAm;
        ehrung.BeantragtAm = beantragtAm;
        ehrung.VerliehenAm = verliehenAm;
        ehrung.Urkundennummer = string.IsNullOrWhiteSpace(urkundennummer) ? null : urkundennummer.Trim();
        ehrung.Bemerkung = string.IsNullOrWhiteSpace(bemerkung) ? null : bemerkung.Trim();
    }

    private async Task<Mitgliedschaft> OffeneMitgliedschaftAsync(Guid mitgliedId, Guid mitgliedschaftId, CancellationToken ct) =>
        await db.Mitgliedschaften.SingleOrDefaultAsync(m => m.Id == mitgliedschaftId && m.MitgliedId == mitgliedId && m.GueltigBis == null, ct)
            ?? throw new InvalidOperationException("Die Mitgliedschaft ist nicht mehr offen. Bitte die Seite neu laden.");

    /// <summary>Rechte prüfen, Änderung in einer Transaktion ausführen, Ergebnis als Meldung zurück auf die Seite.</summary>
    private async Task<IActionResult> AendernAsync(Guid id, Func<Task<string>> aenderung, CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.LaufbahnBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        Heute = Anzeige.Heute(zeit);
        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        try
        {
            TempData["Meldung"] = await aenderung();
            await db.SaveChangesAsync(ct);
            await transaktion.CommitAsync(ct);
        }
        catch (InvalidOperationException fehler)
        {
            TempData.Remove("Meldung");
            TempData["Fehler"] = fehler.Message;
        }
        catch (DbUpdateException)
        {
            TempData.Remove("Meldung");
            TempData["Fehler"] = "Speichern nicht möglich. Vermutlich hat jemand anderes das Mitglied gerade geändert; bitte neu laden.";
        }

        return RedirectToPage(new { id });
    }
}
