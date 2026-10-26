using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Ausbildung;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Abteilungen;

/// <summary>
/// Regelwerke der Jugendabzeichen (V50a): je Regelwerk die Altersgrenzen und Nachweise der Stufen. Lesen mit Zugang zur Jugend,
/// pflegen mit Jugend.Bearbeiten (Jugendwart) oder Ausbildung.Kataloge.
/// </summary>
[Authorize(Policy = Berechtigungen.JugendLesen)]
public class AbzeichenregelwerkeModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Regelwerk im Formular.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung; leer bei einem neuen Regelwerk.</summary>
        public Guid? Id { get; set; }
        /// <summary>Kurzschlüssel, z. B. „JFL-C“.</summary>
        [Required(ErrorMessage = "Code fehlt."), StringLength(20)] public string Code { get; set; } = "";
        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100)] public string Bezeichnung { get; set; } = "";
        /// <summary>Kurzname für Listen.</summary>
        [StringLength(20)] public string? Kurzbezeichnung { get; set; }
        /// <summary>Wo das Regelwerk gilt.</summary>
        [StringLength(200)] public string? Geltungsbereich { get; set; }
        /// <summary>Fundstelle.</summary>
        [StringLength(300)] public string? Quelle { get; set; }
        /// <summary>Vorbelegung bei neuen Abnahmen.</summary>
        public bool Standard { get; set; }
        /// <summary>Zur Auswahl angeboten.</summary>
        public bool Aktiv { get; set; } = true;
        /// <summary>Regeln je Stufe.</summary>
        public List<StufeEingabe> Stufen { get; set; } = [];
    }

    /// <summary>Regeln einer Stufe im Formular; ohne Abzeichen wird die Zeile übergangen.</summary>
    public sealed class StufeEingabe
    {
        /// <summary>Abzeichenstufe.</summary>
        public Guid? QualifikationId { get; set; }
        /// <summary>Mindestalter nach Jahrgang; leer heißt wie im Katalog.</summary>
        [Range(0, 30)] public int? MindestalterJahre { get; set; }
        /// <summary>Nötige Nachweise als Codes; leer heißt wie im Katalog.</summary>
        [StringLength(100)] public string? Voraussetzungen { get; set; }
        /// <summary>Höchstalter der Nachweise in Monaten.</summary>
        [Range(1, 240)] public int? NachweisHoechstensMonate { get; set; }
        /// <summary>Hinweis ohne Prüfung.</summary>
        [StringLength(300)] public string? Hinweis { get; set; }
    }

    /// <summary>Das gespeicherte Regelwerk.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Alle Regelwerke mit Stufen.</summary>
    public IReadOnlyList<Abzeichenregelwerk> Regelwerke { get; private set; } = [];
    /// <summary>Jugendabzeichen (Alter nach Jahrgang) zur Auswahl in den Stufen.</summary>
    public IReadOnlyList<Qualifikation> Abzeichen { get; private set; } = [];
    /// <summary>Der Benutzer darf Regelwerke pflegen.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Zeigt die Regelwerke.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    /// <summary>Legt ein Regelwerk an oder ändert es samt Stufen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await DarfAsync())
        {
            return Forbid();
        }

        var abzeichen = await db.Qualifikationen.Where(q => q.Art == QualifikationArt.Abzeichen && q.AlterNachJahrgang).Select(q => q.Id).ToListAsync(ct);
        var stufen = Formular.Stufen.Where(s => s.QualifikationId is not null).ToList();
        var code = Formular.Code.Trim().ToUpperInvariant();
        if (stufen.Any(s => !abzeichen.Contains(s.QualifikationId!.Value)))
        {
            ModelState.AddModelError(string.Empty, "Nur Jugendabzeichen können Stufen eines Regelwerks sein.");
        }
        else if (stufen.GroupBy(s => s.QualifikationId).Any(g => g.Count() > 1))
        {
            ModelState.AddModelError(string.Empty, "Jede Stufe nur einmal je Regelwerk.");
        }
        else if (await db.Abzeichenregelwerke.IgnoreQueryFilters().AnyAsync(r => r.Code == code && r.Id != Formular.Id, ct))
        {
            ModelState.AddModelError(string.Empty, $"Der Code {code} ist schon vergeben.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        Abzeichenregelwerk? regelwerk;
        if (Formular.Id is { } id)
        {
            regelwerk = await db.Abzeichenregelwerke.Include(r => r.Stufen).FirstOrDefaultAsync(r => r.Id == id, ct);
            if (regelwerk is null)
            {
                return NotFound();
            }
        }
        else
        {
            regelwerk = new Abzeichenregelwerk { Code = code, Bezeichnung = Formular.Bezeichnung.Trim() };
            db.Abzeichenregelwerke.Add(regelwerk);
        }

        regelwerk.Code = code;
        regelwerk.Bezeichnung = Formular.Bezeichnung.Trim();
        regelwerk.Kurzbezeichnung = Leer(Formular.Kurzbezeichnung);
        regelwerk.Geltungsbereich = Leer(Formular.Geltungsbereich);
        regelwerk.Quelle = Leer(Formular.Quelle);
        regelwerk.Aktiv = Formular.Aktiv;
        regelwerk.Standard = Formular.Standard && Formular.Aktiv;
        if (regelwerk.Standard)
        {
            // Höchstens ein Regelwerk ist Vorbelegung.
            await db.Abzeichenregelwerke.Where(r => r.Standard && r.Id != regelwerk.Id).ForEachAsync(r => r.Standard = false, ct);
        }

        foreach (var alt in regelwerk.Stufen.Where(a => stufen.All(s => s.QualifikationId != a.QualifikationId)).ToList())
        {
            db.Remove(alt);
        }

        foreach (var s in stufen)
        {
            var stufe = regelwerk.Stufen.FirstOrDefault(a => a.QualifikationId == s.QualifikationId);
            if (stufe is null)
            {
                stufe = new AbzeichenregelwerkStufe { QualifikationId = s.QualifikationId!.Value };
                regelwerk.Stufen.Add(stufe);
            }

            stufe.MindestalterJahre = s.MindestalterJahre;
            stufe.Voraussetzungen = Leer(s.Voraussetzungen)?.ToUpperInvariant();
            stufe.NachweisHoechstensMonate = s.NachweisHoechstensMonate;
            stufe.Hinweis = Leer(s.Hinweis);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Regelwerk {regelwerk.Bezeichnung} gespeichert.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        DarfBearbeiten = await DarfAsync();
        Regelwerke = await db.Abzeichenregelwerke.AsNoTracking().Include(r => r.Stufen).ThenInclude(s => s.Qualifikation)
            .OrderBy(r => r.Sortierung).ThenBy(r => r.Bezeichnung).ToListAsync(ct);
        Abzeichen = await db.Qualifikationen.AsNoTracking().Where(q => q.Aktiv && q.Art == QualifikationArt.Abzeichen && q.AlterNachJahrgang)
            .OrderBy(q => q.Sortierung).ToListAsync(ct);
    }

    private async Task<bool> DarfAsync() =>
        (await autorisierung.AuthorizeAsync(User, Berechtigungen.JugendBearbeiten)).Succeeded
        || (await autorisierung.AuthorizeAsync(User, Berechtigungen.AusbildungKataloge)).Succeeded;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
