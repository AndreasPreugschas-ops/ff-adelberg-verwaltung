using Fevad.Domain.Ausbildung;
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
/// Planungshilfe für eine Abnahme des Leistungsabzeichens: Wer erfüllt die Voraussetzungen der Stufe und
/// welche Funktion kann er am Abnahmetag übernehmen? Die Atemschutz-Spalte sieht nur, wer Atemschutz lesen darf.
/// </summary>
public class LeistungsabzeichenModel(AppDbContext db, Abzeichenabfrage abzeichen, TimeProvider zeit, IAuthorizationService autorisierung) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Stufe { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Abnahmetag { get; set; }
    [BindProperty(SupportsGet = true)] public bool NurMoegliche { get; set; }

    public IReadOnlyList<Qualifikation> Stufen { get; private set; } = [];
    public Qualifikation? Ziel { get; private set; }
    public IReadOnlyList<Abzeichenabfrage.Zeile> Zeilen { get; private set; } = [];
    public int Moegliche { get; private set; }
    public bool MitAtemschutz { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Abnahmetag ??= Anzeige.Heute(zeit);
        MitAtemschutz = (await autorisierung.AuthorizeAsync(User, Berechtigungen.AtemschutzLesen)).Succeeded;

        // Abzeichen der Einsatzabteilung: alles ohne Jugendbezug und ohne Altersgrenze.
        Stufen = await db.Qualifikationen.AsNoTracking().Include(q => q.Vorstufe)
            .Where(q => q.Aktiv && q.Art == QualifikationArt.Abzeichen && q.MindestJugendMonate == null && q.MindestalterJahre == null)
            .OrderBy(q => q.Sortierung)
            .ToListAsync(ct);
        Ziel = Stufen.FirstOrDefault(q => q.Id == Stufe) ?? Stufen.FirstOrDefault();
        if (Ziel is null)
        {
            return;
        }

        Stufe = Ziel.Id;
        var zeilen = await abzeichen.PlanenAsync(Ziel, Abnahmetag.Value, MitAtemschutz, ct);
        Moegliche = zeilen.Count(z => z.Erfuellt);
        Zeilen = NurMoegliche ? zeilen.Where(z => z.Erfuellt).ToList() : zeilen;
    }
}
