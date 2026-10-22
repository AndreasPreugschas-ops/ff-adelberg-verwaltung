using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>
/// Einen Nachweis für viele Mitglieder auf einmal eintragen, z. B. Führerscheinkontrolle am Dienstabend,
/// Belastungsübung der Atemschutzgruppe oder einen Kreislehrgang.
/// </summary>
public class SammelerfassungModel(AppDbContext db, TimeProvider zeit, IAuthorizationService autorisierung) : PageModel
{
    private const string PraefixQualifikation = "Q:";
    private const string PraefixNachweis = "N:";
    private const string PraefixAtemschutz = "A:";

    [BindProperty] public string? Typ { get; set; }
    [BindProperty] public DateOnly? Datum { get; set; }
    [BindProperty] public string? Durch { get; set; }
    [BindProperty] public string? Bemerkung { get; set; }
    [BindProperty] public int? Minuten { get; set; }
    [BindProperty] public List<Guid> Mitglieder { get; set; } = [];

    public IReadOnlyList<Auswahl> Typen { get; private set; } = [];
    public IReadOnlyList<Person> Personen { get; private set; } = [];

    public sealed record Auswahl(string Gruppe, string Wert, string Text);

    public sealed record Person(Guid Id, string Name, bool Atemschutz);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var (ausbildung, atemschutz) = await RechteAsync();
        if (!ausbildung && !atemschutz)
        {
            return Forbid();
        }

        Datum = Anzeige.Heute(zeit);
        await LadenAsync(ausbildung, atemschutz, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var (ausbildung, atemschutz) = await RechteAsync();
        var heute = Anzeige.Heute(zeit);
        var fehler = Typ is null ? "Bitte auswählen, was eingetragen wird."
            : Datum is null ? "Datum fehlt."
            : Datum > heute ? "Das Datum liegt in der Zukunft."
            : Mitglieder.Count == 0 ? "Bitte mindestens ein Mitglied ankreuzen."
            : null;
        if (fehler is not null)
        {
            ModelState.AddModelError(string.Empty, fehler);
            await LadenAsync(ausbildung, atemschutz, ct);
            return Page();
        }

        var datum = Datum!.Value;
        var ids = await db.Mitglieder.Where(m => Mitglieder.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct);
        int neu;
        string bezeichnung;

        if (Typ!.StartsWith(PraefixAtemschutz, StringComparison.Ordinal) && Enum.TryParse<AtemschutzNachweisArt>(Typ[PraefixAtemschutz.Length..], out var art))
        {
            if (!atemschutz)
            {
                return Forbid();
            }

            var vorhanden = await db.AtemschutzNachweise.Where(n => ids.Contains(n.MitgliedId) && n.Art == art && n.Datum == datum).Select(n => n.MitgliedId).ToListAsync(ct);
            var liste = ids.Except(vorhanden).Select(id => new AtemschutzNachweis
            {
                MitgliedId = id, Art = art, Datum = datum, Minuten = Minuten, Ort = Leer(Durch), Taetigkeit = Leer(Bemerkung),
            }).ToList();
            db.AtemschutzNachweise.AddRange(liste);
            (neu, bezeichnung) = (liste.Count, Anzeige.Text(art));
        }
        else if (Typ.StartsWith(PraefixNachweis, StringComparison.Ordinal) && Guid.TryParse(Typ[PraefixNachweis.Length..], out var artId)
            && await db.Nachweisarten.FirstOrDefaultAsync(a => a.Id == artId, ct) is { } nachweisart)
        {
            if (nachweisart.Gesundheitsbezogen ? !atemschutz : !ausbildung)
            {
                return Forbid();
            }

            var vorhanden = await db.Nachweise.Where(n => ids.Contains(n.MitgliedId) && n.NachweisartId == artId && n.Datum == datum).Select(n => n.MitgliedId).ToListAsync(ct);
            var liste = ids.Except(vorhanden).Select(id => new Nachweis
            {
                MitgliedId = id, NachweisartId = artId, Datum = datum, GueltigBis = Nachweis.GueltigkeitAus(datum, nachweisart),
                DurchgefuehrtVon = Leer(Durch), Bemerkung = Leer(Bemerkung),
            }).ToList();
            db.Nachweise.AddRange(liste);
            (neu, bezeichnung) = (liste.Count, nachweisart.Bezeichnung);
        }
        else if (Typ.StartsWith(PraefixQualifikation, StringComparison.Ordinal) && Guid.TryParse(Typ[PraefixQualifikation.Length..], out var qId)
            && await db.Qualifikationen.FirstOrDefaultAsync(q => q.Id == qId, ct) is { } qualifikation)
        {
            if (!ausbildung)
            {
                return Forbid();
            }

            var vorhanden = await db.QualifikationNachweise.Where(n => ids.Contains(n.MitgliedId) && n.QualifikationId == qId && n.ErworbenAm == datum).Select(n => n.MitgliedId).ToListAsync(ct);
            var liste = ids.Except(vorhanden).Select(id =>
            {
                var n = new QualifikationNachweis { MitgliedId = id, QualifikationId = qId, ErworbenAm = datum, Lehrgangsort = Leer(Durch), Bemerkung = Leer(Bemerkung) };
                n.GueltigkeitVorbelegen(qualifikation);
                return n;
            }).ToList();
            db.QualifikationNachweise.AddRange(liste);
            (neu, bezeichnung) = (liste.Count, qualifikation.Bezeichnung);
        }
        else
        {
            return BadRequest();
        }

        await db.SaveChangesAsync(ct);
        var doppelt = ids.Count - neu;
        TempData["Meldung"] = $"{bezeichnung} am {Anzeige.Datum(datum)} für {neu} Mitglieder eingetragen"
            + (doppelt > 0 ? $", {doppelt} waren schon eingetragen." : ".");
        return RedirectToPage();
    }

    private async Task<(bool Ausbildung, bool Atemschutz)> RechteAsync() => (
        (await autorisierung.AuthorizeAsync(User, Berechtigungen.AusbildungBearbeiten)).Succeeded,
        (await autorisierung.AuthorizeAsync(User, Berechtigungen.AtemschutzBearbeiten)).Succeeded);

    private async Task LadenAsync(bool ausbildung, bool atemschutz, CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var typen = new List<Auswahl>();
        typen.AddRange(await db.Nachweisarten.AsNoTracking()
            .Where(a => a.Aktiv && (a.Gesundheitsbezogen ? atemschutz : ausbildung))
            .OrderBy(a => a.Sortierung)
            .Select(a => new Auswahl("Wiederkehrende Nachweise und Untersuchungen", PraefixNachweis + a.Id, a.Bezeichnung)).ToListAsync(ct));
        if (ausbildung)
        {
            typen.AddRange(await db.Qualifikationen.AsNoTracking().Where(q => q.Aktiv).OrderBy(q => q.Art).ThenBy(q => q.Sortierung)
                .Select(q => new Auswahl("Lehrgänge und Qualifikationen", PraefixQualifikation + q.Id, q.Bezeichnung)).ToListAsync(ct));
        }

        if (atemschutz)
        {
            typen.AddRange(Enum.GetValues<AtemschutzNachweisArt>()
                .Select(a => new Auswahl("Atemschutz", PraefixAtemschutz + a, Anzeige.Text(a))));
        }

        Typen = typen;

        var agt = await db.QualifikationNachweise.AsNoTracking()
            .Where(q => q.Qualifikation!.Code == Qualifikation.CodeAtemschutz && q.ErworbenAm <= heute)
            .Select(q => q.MitgliedId).Distinct().ToListAsync(ct);

        Personen = (await db.Mitglieder.AsNoTracking()
            .Where(m => m.Mitgliedschaften.Any(s =>
                s.Abteilung!.Code == Personalfristenabfrage.AbteilungEinsatz
                && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)
                && (s.Status == MitgliedStatus.Aktiv || s.Status == MitgliedStatus.Probezeit)))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, m.Nachname, m.Vorname })
            .ToListAsync(ct))
            .Select(m => new Person(m.Id, $"{m.Nachname}, {m.Vorname}", agt.Contains(m.Id)))
            .ToList();
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
