using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Antraege;

/// <summary>
/// Aufnahme, Übertritt, Wechsel in die Altersabteilung, Ausscheiden und die Erklärung bei Volljährigkeit: in FEVAD ausfüllen, als PDF
/// zum Unterschreiben drucken oder blanko zum Ausfüllen von Hand. Beim Mitglied gibt es Knöpfe für die Anträge, die gerade gehen.
/// </summary>
[Authorize(Policy = Berechtigungen.AntraegeSehen)]
public class IndexModel(
    AppDbContext db, Antragsbereich bereich, Datenbereich datenbereich, Fevad.Infrastructure.Laufbahn.Laufbahnabfrage laufbahn, ParameterLeser parameter,
    TimeProvider zeit) : PageModel
{
    /// <summary>Ein Antrag in der Liste.</summary>
    /// <param name="Id">Kennung des Antrags.</param>
    /// <param name="Art">Art des Antrags.</param>
    /// <param name="Status">Bearbeitungsstand.</param>
    /// <param name="Name">Anzeigename „Nachname, Vorname“.</param>
    /// <param name="Abteilung">Beantragte Abteilung.</param>
    /// <param name="EingegangenAm">Eingangstag.</param>
    /// <param name="EntschiedenAm">Tag der Entscheidung.</param>
    public sealed record Zeile(Guid Id, AntragArt Art, AntragStatus Status, string Name, string Abteilung, DateOnly? EingegangenAm, DateOnly? EntschiedenAm);

    /// <summary>Filter auf die Anträge eines Mitglieds.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? Mitglied { get; set; }

    /// <summary>Auch abgeschlossene Anträge zeigen.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Abgeschlossene { get; set; }

    /// <summary>Die Anträge.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    /// <summary>Name des gefilterten Mitglieds.</summary>
    public string? MitgliedName { get; private set; }

    /// <summary>Darf neue Aufnahmeanträge anlegen (Freigabe für eine Jugend- bzw. Einsatzabteilung).</summary>
    public bool AufnahmeJugend { get; private set; }
    /// <summary>Der Benutzer darf Aufnahmen in eine aktive Kinderfeuerwehr erfassen (V50b).</summary>
    public bool AufnahmeKinder { get; private set; }
    /// <summary>Darf neue Aufnahmeanträge für eine Einsatzabteilung anlegen.</summary>
    public bool AufnahmeEinsatz { get; private set; }

    /// <summary>Mitglieder der Jugendfeuerwehr für den Übertrittsantrag, minderjährige Mitglieder der Einsatzabteilung für die Erklärung.</summary>
    public IReadOnlyList<SelectListItem> Jugendliche { get; private set; } = [];
    /// <summary>Minderjährige Mitglieder der Einsatzabteilung für die Erklärung bei Volljährigkeit.</summary>
    public IReadOnlyList<SelectListItem> Minderjaehrige { get; private set; } = [];

    /// <summary>Mitglieder der Einsatzabteilung, die innerhalb des Vorlaufs das Alter für die Altersabteilung erreichen.</summary>
    public IReadOnlyList<SelectListItem> Altersabteilung { get; private set; } = [];

    /// <summary>Beim Mitglied: Anträge, die jetzt gestellt werden können und noch nicht offen sind.</summary>
    public IReadOnlyList<AntragArt> NeueAntraege { get; private set; } = [];

    /// <summary>
    /// Ohne Mitglied die Übersicht aller sichtbaren Anträge; mit Mitglied nur dessen Anträge als Reiter der Mitgliederakte,
    /// samt abgeschlossenen und ohne die übrigen Bereiche.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var freigabe = await bereich.FreigabeAsync(User);
        var abfrage = Antragsbereich.Sichtbar(db.Antraege.AsNoTracking(), freigabe, heute);
        if (Mitglied is { } mitgliedId)
        {
            MitgliedName = await (await datenbereich.Filtern(db.Mitglieder.AsNoTracking(), User, heute))
                .Where(m => m.Id == mitgliedId).Select(m => m.Nachname + ", " + m.Vorname).FirstOrDefaultAsync(ct);
            if (MitgliedName is null)
            {
                return NotFound();
            }

            Zeilen = await ZeilenAsync(abfrage.Where(a => a.MitgliedId == mitgliedId), ct);
            NeueAntraege = await MoeglicheAntraegeAsync(mitgliedId, heute, ct);
            return Page();
        }

        if (!Abgeschlossene)
        {
            abfrage = abfrage.Where(a => a.Status == AntragStatus.Entwurf || a.Status == AntragStatus.Eingegangen);
        }

        Zeilen = await ZeilenAsync(abfrage, ct);

        var abteilungen = await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv).Select(a => new { a.Id, a.Art }).ToListAsync(ct);
        AufnahmeJugend = abteilungen.Any(a => a.Art == AbteilungArt.Jugend && freigabe.Umfasst(a.Id));
        AufnahmeKinder = abteilungen.Any(a => a.Art == AbteilungArt.Kinder && freigabe.Umfasst(a.Id));
        AufnahmeEinsatz = abteilungen.Any(a => a.Art == AbteilungArt.Einsatz && freigabe.Umfasst(a.Id));

        // Kandidaten: wer derzeit in einer freigegebenen Jugend- bzw. Einsatzabteilung ist.
        var volljaehrigAb = await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, heute, 18, ct);
        var regeln = await laufbahn.RegelnAsync(heute, ct);
        var vorlauf = heute.AddMonths(await parameter.ZahlAsync(ParameterSchluessel.AntraegeVorlaufMonate, heute, 6, ct));
        var ids = freigabe.Abteilungen.ToList();
        var kandidaten = await db.Mitgliedschaften.AsNoTracking()
            .Where(s => !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (s.Abteilung!.Art == AbteilungArt.Jugend || s.Abteilung.Art == AbteilungArt.Einsatz)
                && (freigabe.Alle || ids.Contains(s.AbteilungId)))
            .Select(s => new { s.MitgliedId, s.Abteilung!.Art, s.Mitglied!.Nachname, s.Mitglied.Vorname, s.Mitglied.Geburtsdatum })
            .ToListAsync(ct);
        // Nur wer innerhalb des Vorlaufs das Übertrittsalter erreicht; früher geht der Antrag nicht.
        Jugendliche = Auswahl(kandidaten
            .Where(k => k.Art == AbteilungArt.Jugend && Domain.Mitglieder.Mitglied.Alter(k.Geburtsdatum, vorlauf) >= regeln.AufnahmeEinsatzAbAlter)
            .Select(k => (k.MitgliedId, k.Nachname, k.Vorname, k.Geburtsdatum)), heute);
        Altersabteilung = Auswahl(kandidaten
            .Where(k => k.Art == AbteilungArt.Einsatz && Domain.Mitglieder.Mitglied.Alter(k.Geburtsdatum, vorlauf) >= regeln.AltersabteilungAbAlter)
            .Select(k => (k.MitgliedId, k.Nachname, k.Vorname, k.Geburtsdatum)), heute);
        // Bis ein Jahr nach der Volljährigkeit, falls die Erklärung noch fehlt.
        Minderjaehrige = Auswahl(kandidaten
            .Where(k => k.Art == AbteilungArt.Einsatz && Domain.Mitglieder.Mitglied.Alter(k.Geburtsdatum, heute) <= volljaehrigAb)
            .Select(k => (k.MitgliedId, k.Nachname, k.Vorname, k.Geburtsdatum)), heute);
        return Page();
    }

    /// <summary>Welche Anträge zum Mitglied gerade gehen: nach Abteilung und Alter (mit Vorlauf), ohne Arten, zu denen schon einer offen ist.</summary>
    private async Task<IReadOnlyList<AntragArt>> MoeglicheAntraegeAsync(Guid mitgliedId, DateOnly heute, CancellationToken ct)
    {
        var regeln = await laufbahn.RegelnAsync(heute, ct);
        var volljaehrigAb = await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, heute, 18, ct);
        var vorlauf = heute.AddMonths(await parameter.ZahlAsync(ParameterSchluessel.AntraegeVorlaufMonate, heute, 6, ct));
        var geburtsdatum = await db.Mitglieder.Where(m => m.Id == mitgliedId).Select(m => m.Geburtsdatum).SingleAsync(ct);
        var arten = await db.Mitgliedschaften.AsNoTracking()
            .Where(s => s.MitgliedId == mitgliedId && !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute))
            .Select(s => s.Abteilung!.Art).ToListAsync(ct);
        var offen = await db.Antraege.AsNoTracking()
            .Where(a => a.MitgliedId == mitgliedId && (a.Status == AntragStatus.Entwurf || a.Status == AntragStatus.Eingegangen))
            .Select(a => a.Art).ToListAsync(ct);
        var alterImVorlauf = Domain.Mitglieder.Mitglied.Alter(geburtsdatum, vorlauf);

        var moeglich = new List<AntragArt>();
        if (arten.Contains(AbteilungArt.Jugend) && alterImVorlauf >= regeln.AufnahmeEinsatzAbAlter)
        {
            moeglich.Add(AntragArt.UebertrittEinsatz);
        }

        if (arten.Contains(AbteilungArt.Einsatz) && Domain.Mitglieder.Mitglied.Alter(geburtsdatum, heute) <= volljaehrigAb && alterImVorlauf >= volljaehrigAb)
        {
            moeglich.Add(AntragArt.Volljaehrigkeit);
        }

        if (arten.Contains(AbteilungArt.Einsatz) && alterImVorlauf >= regeln.AltersabteilungAbAlter)
        {
            moeglich.Add(AntragArt.Altersabteilung);
        }

        if (arten.Count > 0)
        {
            moeglich.Add(AntragArt.Ausscheiden);
        }

        return moeglich.Except(offen).ToList();
    }

    private static async Task<IReadOnlyList<Zeile>> ZeilenAsync(IQueryable<Antrag> abfrage, CancellationToken ct) =>
        (await abfrage
            .OrderBy(a => a.Status).ThenByDescending(a => a.GeaendertAm ?? a.ErstelltAm)
            .Select(a => new { a.Id, a.Art, a.Status, a.Nachname, a.Vorname, Abteilung = a.Abteilung!.Bezeichnung, a.EingegangenAm, a.EntschiedenAm })
            .ToListAsync(ct))
        .Select(a => new Zeile(a.Id, a.Art, a.Status, $"{a.Nachname}, {a.Vorname}", a.Abteilung, a.EingegangenAm, a.EntschiedenAm))
        .ToList();

    /// <summary>Liefert ein leeres Antragsformular als PDF zum Ausfüllen von Hand.</summary>
    /// <param name="art">Art des Antrags.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die PDF-Datei.</returns>
    public async Task<IActionResult> OnGetBlankoAsync(AntragArt art, CancellationToken ct)
    {
        // Gibt es nur eine Jugendfeuerwehr, steht sie mit Anschrift im Kopf; bei mehreren Einsatzabteilungen bleibt es die Feuerwehr.
        var jugend = Antragsregeln.IstJugendaufnahme(art)
            ? await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv && a.Art == Antragsregeln.Zielabteilung(art)).Select(a => a.Id).Take(2).ToListAsync(ct)
            : [];
        var rahmen = await bereich.RahmenAsync(jugend.Count == 1 ? jugend[0] : null, Anzeige.Heute(zeit), ct);
        return File(AntragPdf.Erzeugen(art, null, rahmen), "application/pdf", AntragPdf.Dateiname(art, null));
    }

    private static List<SelectListItem> Auswahl(IEnumerable<(Guid Id, string Nachname, string Vorname, DateOnly Geburtsdatum)> personen, DateOnly heute) =>
        personen.DistinctBy(p => p.Id).OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .Select(p => new SelectListItem($"{p.Nachname}, {p.Vorname} ({Domain.Mitglieder.Mitglied.Alter(p.Geburtsdatum, heute)})", p.Id.ToString()))
            .ToList();
}
