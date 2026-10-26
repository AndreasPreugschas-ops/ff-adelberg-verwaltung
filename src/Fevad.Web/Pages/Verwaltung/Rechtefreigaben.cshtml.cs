using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Verwaltung;

/// <summary>
/// Vier-Augen-Prinzip (F18): Rechte und Rollen, die Gesundheits- oder Jugenddaten öffnen, wirken erst, wenn eine zweite Person sie hier
/// freigibt. Wer eine Änderung beantragt hat, kann sie nicht selbst freigeben.
/// </summary>
[Authorize(Policy = Berechtigungen.RechteFreigeben)]
public class RechtefreigabenModel(
    AppDbContext db,
    UserManager<Fevad.Infrastructure.Identitaet.Benutzer> konten,
    RoleManager<IdentityRole<Guid>> rollen,
    Rechteverzeichnis rechte,
    IAktuellerBenutzer aktuell,
    TimeProvider zeit) : PageModel
{
    /// <summary>Eine Zeile der Liste.</summary>
    /// <param name="Freigabe">Der Antrag.</param>
    /// <param name="Was">Lesbare Beschreibung der Änderung.</param>
    /// <param name="Beantragt">E-Mail des Antragstellers.</param>
    /// <param name="Entschieden">E-Mail der zweiten Person.</param>
    /// <param name="Eigener">Der angemeldete Benutzer hat ihn selbst beantragt.</param>
    public sealed record Zeile(Rechtefreigabe Freigabe, string Was, string Beantragt, string? Entschieden, bool Eigener);

    /// <summary>Offene Anträge, älteste zuerst.</summary>
    public IReadOnlyList<Zeile> Offene { get; private set; } = [];

    /// <summary>Die zuletzt entschiedenen Anträge.</summary>
    public IReadOnlyList<Zeile> Entschiedene { get; private set; } = [];

    /// <summary>Zeigt offene und zuletzt entschiedene Anträge.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    /// <summary>Gibt einen Antrag frei und trägt das Recht bzw. die Rolle ein.</summary>
    /// <param name="id">Schlüssel des Antrags.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung.</returns>
    public async Task<IActionResult> OnPostFreigebenAsync(Guid id, CancellationToken ct) => await EntscheidenAsync(id, true, ct);

    /// <summary>Lehnt einen Antrag ab; das Recht bzw. die Rolle wird nicht eingetragen.</summary>
    /// <param name="id">Schlüssel des Antrags.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung.</returns>
    public async Task<IActionResult> OnPostAblehnenAsync(Guid id, CancellationToken ct) => await EntscheidenAsync(id, false, ct);

    private async Task<IActionResult> EntscheidenAsync(Guid id, bool freigeben, CancellationToken ct)
    {
        var ich = aktuell.Id;
        if (await db.Rechtefreigaben.FirstOrDefaultAsync(f => f.Id == id && f.Freigegeben == null, ct) is not { } antrag)
        {
            TempData["Fehler"] = "Den Antrag gibt es nicht mehr oder er ist schon entschieden.";
            return RedirectToPage();
        }

        if (ich is null || antrag.BeantragtVon == ich)
        {
            TempData["Fehler"] = "Eigene Anträge gibt eine zweite Person frei.";
            return RedirectToPage();
        }

        if (freigeben && !await rollen.RoleExistsAsync(antrag.Rolle))
        {
            TempData["Fehler"] = "Die Rolle gibt es nicht mehr; bitte ablehnen.";
            return RedirectToPage();
        }

        if (freigeben && antrag.Art == RechtefreigabeArt.Rollenrecht)
        {
            if (await db.RollenRechte.FirstOrDefaultAsync(r => r.Rolle == antrag.Rolle && r.Recht == antrag.Recht, ct) is { } vorhanden)
            {
                vorhanden.Abteilungen = antrag.Abteilungen;
            }
            else
            {
                db.RollenRechte.Add(new RolleRecht { Rolle = antrag.Rolle, Recht = antrag.Recht!, Abteilungen = antrag.Abteilungen });
            }
        }
        else if (freigeben)
        {
            if (await konten.FindByIdAsync(antrag.KontoId.ToString()!) is not { IstAnonymisiert: false } konto)
            {
                TempData["Fehler"] = "Das Konto gibt es nicht mehr; bitte ablehnen.";
                return RedirectToPage();
            }

            if (!await konten.IsInRoleAsync(konto, antrag.Rolle))
            {
                var ergebnis = await konten.AddToRoleAsync(konto, antrag.Rolle);
                if (!ergebnis.Succeeded)
                {
                    TempData["Fehler"] = string.Join(" ", ergebnis.Errors.Select(e => e.Description));
                    return RedirectToPage();
                }
            }
        }

        antrag.Freigegeben = freigeben;
        antrag.EntschiedenVon = ich;
        antrag.EntschiedenAm = zeit.GetUtcNow();
        await db.SaveChangesAsync(ct);
        rechte.Verwerfen();
        TempData["Meldung"] = freigeben ? "Freigegeben. Die Änderung gilt spätestens nach wenigen Minuten." : "Abgelehnt. Es bleibt bei den bisherigen Rechten.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        var offen = await db.Rechtefreigaben.AsNoTracking().Where(f => f.Freigegeben == null).OrderBy(f => f.ErstelltAm).ToListAsync(ct);
        var entschieden = await db.Rechtefreigaben.AsNoTracking().Where(f => f.Freigegeben != null)
            .OrderByDescending(f => f.EntschiedenAm).Take(20).ToListAsync(ct);
        var alle = offen.Concat(entschieden).ToList();
        var kontoIds = alle.SelectMany(f => new[] { f.BeantragtVon, f.EntschiedenVon ?? Guid.Empty, f.KontoId ?? Guid.Empty }).Distinct().ToList();
        var emails = await db.Users.AsNoTracking().Where(b => kontoIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Email ?? "(ohne E-Mail)", ct);
        var profile = await db.Rollenprofile.AsNoTracking().ToDictionaryAsync(p => p.Name, p => p.Bezeichnung, ct);
        var abteilungen = await db.Abteilungen.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Bezeichnung, ct);
        var ich = aktuell.Id;

        string Name(Guid? id) => id is { } k && emails.TryGetValue(k, out var e) ? e : "(gelöschtes Konto)";
        string Was(Rechtefreigabe f)
        {
            var rolle = profile.GetValueOrDefault(f.Rolle, f.Rolle);
            if (f.Art == RechtefreigabeArt.Rollenzuweisung)
            {
                return $"Konto {Name(f.KontoId)} bekommt die Rolle „{rolle}“";
            }

            var recht = Berechtigungen.Katalog.FirstOrDefault(r => r.Name == f.Recht)?.Bezeichnung ?? f.Recht;
            var umfang = f.Abteilungen.Count == 0 ? "alle Abteilungen" : string.Join(", ", f.Abteilungen.Select(a => abteilungen.GetValueOrDefault(a, "?")));
            return $"Rolle „{rolle}“ bekommt „{recht}“ ({umfang})";
        }

        Zeile Machen(Rechtefreigabe f) => new(f, Was(f), Name(f.BeantragtVon), f.EntschiedenVon is null ? null : Name(f.EntschiedenVon), f.BeantragtVon == ich);
        Offene = offen.Select(Machen).ToList();
        Entschiedene = entschieden.Select(Machen).ToList();
    }
}
