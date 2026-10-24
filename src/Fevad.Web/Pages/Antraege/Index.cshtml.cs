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
/// Aufnahme- und Übertrittsanträge und die Erklärung bei Volljährigkeit: in FEVAD ausfüllen, als PDF zum Unterschreiben
/// drucken oder blanko zum Ausfüllen von Hand.
/// </summary>
[Authorize(Policy = Berechtigungen.AntraegeSehen)]
public class IndexModel(AppDbContext db, Antragsbereich bereich, Datenbereich datenbereich, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    public sealed record Zeile(Guid Id, AntragArt Art, AntragStatus Status, string Name, string Abteilung, DateOnly? EingegangenAm, DateOnly? EntschiedenAm);

    [BindProperty(SupportsGet = true)]
    public Guid? Mitglied { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Abgeschlossene { get; set; }

    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];
    public string? MitgliedName { get; private set; }

    /// <summary>Darf neue Aufnahmeanträge anlegen (Freigabe für eine Jugend- bzw. Einsatzabteilung).</summary>
    public bool AufnahmeJugend { get; private set; }
    public bool AufnahmeEinsatz { get; private set; }

    /// <summary>Mitglieder der Jugendfeuerwehr für den Übertrittsantrag, minderjährige Mitglieder der Einsatzabteilung für die Erklärung.</summary>
    public IReadOnlyList<SelectListItem> Jugendliche { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Minderjaehrige { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var freigabe = await bereich.FreigabeAsync(User);
        var abfrage = Antragsbereich.Sichtbar(db.Antraege.AsNoTracking(), freigabe, heute);
        if (Mitglied is { } mitgliedId)
        {
            abfrage = abfrage.Where(a => a.MitgliedId == mitgliedId);
            MitgliedName = await (await datenbereich.Filtern(db.Mitglieder.AsNoTracking(), User, heute))
                .Where(m => m.Id == mitgliedId).Select(m => m.Nachname + ", " + m.Vorname).FirstOrDefaultAsync(ct);
        }

        if (!Abgeschlossene)
        {
            abfrage = abfrage.Where(a => a.Status == AntragStatus.Entwurf || a.Status == AntragStatus.Eingegangen);
        }

        Zeilen = (await abfrage
                .OrderBy(a => a.Status).ThenByDescending(a => a.GeaendertAm ?? a.ErstelltAm)
                .Select(a => new { a.Id, a.Art, a.Status, a.Nachname, a.Vorname, Abteilung = a.Abteilung!.Bezeichnung, a.EingegangenAm, a.EntschiedenAm })
                .ToListAsync(ct))
            .Select(a => new Zeile(a.Id, a.Art, a.Status, $"{a.Nachname}, {a.Vorname}", a.Abteilung, a.EingegangenAm, a.EntschiedenAm))
            .ToList();

        var abteilungen = await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv).Select(a => new { a.Id, a.Art }).ToListAsync(ct);
        AufnahmeJugend = abteilungen.Any(a => a.Art == AbteilungArt.Jugend && freigabe.Umfasst(a.Id));
        AufnahmeEinsatz = abteilungen.Any(a => a.Art == AbteilungArt.Einsatz && freigabe.Umfasst(a.Id));

        // Kandidaten: wer derzeit in einer freigegebenen Jugend- bzw. Einsatzabteilung ist.
        var volljaehrigAb = await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, heute, 18, ct);
        var ids = freigabe.Abteilungen.ToList();
        var kandidaten = await db.Mitgliedschaften.AsNoTracking()
            .Where(s => !s.Vordienstzeit && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (s.Abteilung!.Art == AbteilungArt.Jugend || s.Abteilung.Art == AbteilungArt.Einsatz)
                && (freigabe.Alle || ids.Contains(s.AbteilungId)))
            .Select(s => new { s.MitgliedId, s.Abteilung!.Art, s.Mitglied!.Nachname, s.Mitglied.Vorname, s.Mitglied.Geburtsdatum })
            .ToListAsync(ct);
        Jugendliche = Auswahl(kandidaten.Where(k => k.Art == AbteilungArt.Jugend).Select(k => (k.MitgliedId, k.Nachname, k.Vorname, k.Geburtsdatum)), heute);
        // Bis ein Jahr nach der Volljährigkeit, falls die Erklärung noch fehlt.
        Minderjaehrige = Auswahl(kandidaten
            .Where(k => k.Art == AbteilungArt.Einsatz && Domain.Mitglieder.Mitglied.Alter(k.Geburtsdatum, heute) <= volljaehrigAb)
            .Select(k => (k.MitgliedId, k.Nachname, k.Vorname, k.Geburtsdatum)), heute);
    }

    public async Task<IActionResult> OnGetBlankoAsync(AntragArt art, CancellationToken ct)
    {
        // Gibt es nur eine Jugendfeuerwehr, steht sie mit Anschrift im Kopf; bei mehreren Einsatzabteilungen bleibt es die Feuerwehr.
        var jugend = art == AntragArt.JugendEintritt
            ? await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv && a.Art == AbteilungArt.Jugend).Select(a => a.Id).Take(2).ToListAsync(ct)
            : [];
        var rahmen = await bereich.RahmenAsync(jugend.Count == 1 ? jugend[0] : null, Anzeige.Heute(zeit), ct);
        return File(AntragPdf.Erzeugen(art, null, rahmen), "application/pdf", AntragPdf.Dateiname(art, null));
    }

    private static List<SelectListItem> Auswahl(IEnumerable<(Guid Id, string Nachname, string Vorname, DateOnly Geburtsdatum)> personen, DateOnly heute) =>
        personen.DistinctBy(p => p.Id).OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .Select(p => new SelectListItem($"{p.Nachname}, {p.Vorname} ({Domain.Mitglieder.Mitglied.Alter(p.Geburtsdatum, heute)})", p.Id.ToString()))
            .ToList();
}
