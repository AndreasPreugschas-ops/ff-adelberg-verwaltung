using System.ComponentModel.DataAnnotations;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using BenutzerKonto = Fevad.Infrastructure.Identitaet.Benutzer;

namespace Fevad.Web.Pages.Verwaltung.Benutzer;

/// <summary>Konto anlegen oder ändern: E-Mail, verknüpftes Mitglied, Rollen. Dazu Einrichtungslink, Deaktivieren, 2FA zurücksetzen.</summary>
public class BearbeitenModel(
    UserManager<BenutzerKonto> benutzer,
    AppDbContext db,
    Kontolinks links,
    TimeProvider zeit,
    ILogger<BearbeitenModel> log) : PageModel
{
    private const string LinkSchluessel = "Einrichtungslink";
    private const string LinkBisSchluessel = "EinrichtungslinkBis";

    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public bool IstNeu => Formular.Id is null;
    public BenutzerKonto? Vorhanden { get; private set; }
    public IReadOnlyList<SelectListItem> Mitglieder { get; private set; } = [];

    /// <summary>Alle Rollen, mitgelieferte zuerst; Wert ist der technische Name.</summary>
    public IReadOnlyList<SelectListItem> RollenAuswahl { get; private set; } = [];
    public bool MailAktiv => links.MailAktiv;
    public bool IstSelbst => Formular.Id is not null && Formular.Id == Guid.Parse(benutzer.GetUserId(User)!);
    public string? Einrichtungslink { get; private set; }
    public string? EinrichtungslinkBis { get; private set; }

    public sealed class Eingabe
    {
        public Guid? Id { get; set; }

        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), Display(Name = "E-Mail-Adresse (Benutzername)")]
        public string? Email { get; set; }

        [Display(Name = "Mitglied")]
        public Guid? MitgliedId { get; set; }

        public List<string> Rollen { get; set; } = [];

        [Display(Name = "Einrichtungslink per E-Mail schicken")]
        public bool LinkSenden { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            if (await benutzer.FindByIdAsync(id.Value.ToString()) is not { IstAnonymisiert: false } konto)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = konto.Id,
                Email = konto.Email,
                MitgliedId = konto.MitgliedId,
                Rollen = [.. await benutzer.GetRolesAsync(konto)],
            };
            Vorhanden = konto;
        }
        else
        {
            Formular.Rollen = [Rollen.Mitglied];
        }

        Einrichtungslink = TempData[LinkSchluessel] as string;
        EinrichtungslinkBis = TempData[LinkBisSchluessel] as string;
        await AuswahlLadenAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await using var transaktion = await AdministratorSperreAsync(ct);
        var konto = IstNeu ? null : await benutzer.FindByIdAsync(Formular.Id!.Value.ToString());
        // Vom Löschlauf geleerte Konten bleiben nur als ID stehen und lassen sich nicht wieder beleben.
        if (!IstNeu && konto is not { IstAnonymisiert: false })
        {
            return NotFound();
        }

        Vorhanden = konto;
        var vorhandeneRollen = await db.Roles.Select(r => r.Name!).ToListAsync(ct);
        Formular.Rollen = Formular.Rollen.Where(vorhandeneRollen.Contains).Distinct().ToList();

        var email = Formular.Email?.Trim();
        if (string.IsNullOrEmpty(email) && Formular.MitgliedId is { } mid)
        {
            // Ohne Eingabe die E-Mail-Adresse aus den Mitgliederdaten übernehmen.
            email = await db.Mitglieder.Where(m => m.Id == mid).Select(m => m.Email).FirstOrDefaultAsync(ct);
        }

        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError("Formular.Email", "E-Mail-Adresse fehlt (im Mitglied ist auch keine hinterlegt).");
        }

        if (Formular.MitgliedId is { } mitgliedId
            && await db.Users.AnyAsync(b => b.MitgliedId == mitgliedId && b.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.MitgliedId", "Dieses Mitglied hat schon ein Konto.");
        }

        if (konto is not null)
        {
            await AdministratorSchuetzenAsync(konto, Formular.Rollen.Contains(Rollen.Administrator), deaktivieren: false);
        }

        if (!ModelState.IsValid)
        {
            await AuswahlLadenAsync(ct);
            return Page();
        }

        IdentityResult ergebnis;
        if (konto is null)
        {
            konto = new BenutzerKonto { UserName = email, Email = email, MitgliedId = Formular.MitgliedId };
            ergebnis = await benutzer.CreateAsync(konto);
        }
        else
        {
            konto.MitgliedId = Formular.MitgliedId;
            ergebnis = await benutzer.UpdateAsync(konto);
            if (ergebnis.Succeeded && !string.Equals(konto.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                ergebnis = await benutzer.SetEmailAsync(konto, email);
                if (ergebnis.Succeeded)
                {
                    ergebnis = await benutzer.SetUserNameAsync(konto, email);
                }
            }
        }

        if (ergebnis.Succeeded)
        {
            var bisher = await benutzer.GetRolesAsync(konto);
            var weg = bisher.Except(Formular.Rollen).ToList();
            var dazu = Formular.Rollen.Except(bisher).ToList();
            if (weg.Count > 0)
            {
                ergebnis = await benutzer.RemoveFromRolesAsync(konto, weg);
            }

            if (ergebnis.Succeeded && dazu.Count > 0)
            {
                ergebnis = await benutzer.AddToRolesAsync(konto, dazu);
            }
        }

        if (!ergebnis.Succeeded)
        {
            foreach (var fehler in ergebnis.Errors)
            {
                ModelState.AddModelError(fehler.Code.Contains("Email") || fehler.Code.Contains("UserName") ? "Formular.Email" : string.Empty, fehler.Description);
            }

            await AuswahlLadenAsync(ct);
            return Page();
        }

        await transaktion.CommitAsync(ct);
        if (IstNeu)
        {
            // Neues Konto hat noch kein Passwort: gleich den Einrichtungslink erzeugen.
            await LinkAusgebenAsync(konto, Formular.LinkSenden, ct);
        }
        else
        {
            TempData["Meldung"] = $"Konto {konto.Email} gespeichert.";
        }

        return RedirectToPage(new { id = konto.Id });
    }

    public async Task<IActionResult> OnPostLinkAsync(Guid id, bool senden, CancellationToken ct)
    {
        if (await benutzer.FindByIdAsync(id.ToString()) is not { IstDeaktiviert: false } konto)
        {
            return NotFound();
        }

        await LinkAusgebenAsync(konto, senden, ct);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeaktivierenAsync(Guid id, CancellationToken ct)
    {
        await using var transaktion = await AdministratorSperreAsync(ct);
        if (await benutzer.FindByIdAsync(id.ToString()) is not { IstAnonymisiert: false } konto)
        {
            return NotFound();
        }

        Formular.Id = id;
        await AdministratorSchuetzenAsync(konto, await benutzer.IsInRoleAsync(konto, Rollen.Administrator), deaktivieren: true);
        if (!ModelState.IsValid)
        {
            TempData["Fehler"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToPage(new { id });
        }

        konto.DeaktiviertAm = zeit.GetUtcNow();
        await benutzer.UpdateAsync(konto);
        // Neuer Sicherheitsstempel: laufende Sitzungen enden bei der nächsten Prüfung, offene Links verfallen.
        await benutzer.UpdateSecurityStampAsync(konto);
        await transaktion.CommitAsync(ct);
        TempData["Meldung"] = $"Konto {konto.Email} deaktiviert.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAktivierenAsync(Guid id)
    {
        if (await benutzer.FindByIdAsync(id.ToString()) is not { IstAnonymisiert: false } konto)
        {
            return NotFound();
        }

        konto.DeaktiviertAm = null;
        await benutzer.UpdateAsync(konto);
        await benutzer.SetLockoutEndDateAsync(konto, null);
        TempData["Meldung"] = $"Konto {konto.Email} wieder aktiviert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Handy verloren: Authenticator und Wiederherstellungscodes verwerfen. Bei Pflichtrollen muss die App bei der nächsten Anmeldung neu eingerichtet werden.</summary>
    public async Task<IActionResult> OnPostZweiFaktorZuruecksetzenAsync(Guid id)
    {
        if (await benutzer.FindByIdAsync(id.ToString()) is not { IstAnonymisiert: false } konto)
        {
            return NotFound();
        }

        await benutzer.SetTwoFactorEnabledAsync(konto, false);
        await benutzer.ResetAuthenticatorKeyAsync(konto);
        await benutzer.GenerateNewTwoFactorRecoveryCodesAsync(konto, 0);
        log.LogWarning("Zwei-Faktor-Anmeldung für {Email} durch Administrator zurückgesetzt", konto.Email);
        TempData["Meldung"] = $"Zwei-Faktor-Anmeldung für {konto.Email} zurückgesetzt.";
        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Serialisiert alle Änderungen, die Administratoren betreffen können: Zwei Administratoren, die sich gleichzeitig
    /// gegenseitig deaktivieren, würden sonst beide noch einen weiteren zählen.
    /// </summary>
    private async Task<IDbContextTransaction> AdministratorSperreAsync(CancellationToken ct)
    {
        var transaktion = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('fevad.administratoren'))", ct);
        return transaktion;
    }

    /// <summary>Der eigene Administratorzugang und der letzte aktive Administrator bleiben erhalten.</summary>
    private async Task AdministratorSchuetzenAsync(BenutzerKonto konto, bool bleibtAdministrator, bool deaktivieren)
    {
        var istAdministrator = await benutzer.IsInRoleAsync(konto, Rollen.Administrator);
        if (!istAdministrator || (bleibtAdministrator && !deaktivieren))
        {
            if (deaktivieren && IstSelbst)
            {
                ModelState.AddModelError(string.Empty, "Das eigene Konto kann nicht deaktiviert werden.");
            }

            return;
        }

        if (IstSelbst)
        {
            ModelState.AddModelError(string.Empty, deaktivieren
                ? "Das eigene Konto kann nicht deaktiviert werden."
                : "Die eigene Rolle Administrator kann nicht entfernt werden.");
            return;
        }

        var weitereAdministratoren = (await benutzer.GetUsersInRoleAsync(Rollen.Administrator))
            .Count(a => a.Id != konto.Id && !a.IstDeaktiviert);
        if (weitereAdministratoren == 0)
        {
            ModelState.AddModelError(string.Empty, "Es muss mindestens ein aktiver Administrator bleiben.");
        }
    }

    private async Task LinkAusgebenAsync(BenutzerKonto konto, bool senden, CancellationToken ct)
    {
        var link = await links.PasswortLinkAsync(HttpContext, konto);
        TempData[LinkSchluessel] = link.Adresse;
        TempData[LinkBisSchluessel] = Anzeige.DatumZeit(link.GueltigBis);

        if (!senden || !links.MailAktiv)
        {
            TempData["Meldung"] = $"Link für {konto.Email} erzeugt. Bitte persönlich weitergeben.";
            return;
        }

        try
        {
            await links.EinrichtungSendenAsync(konto, link, ct);
            TempData["Meldung"] = $"Link an {konto.Email} geschickt.";
        }
        catch (Exception fehler) when (fehler is not OperationCanceledException)
        {
            log.LogError(fehler, "Einrichtungslink an {Email} nicht versendet", konto.Email);
            TempData["Fehler"] = "Die E-Mail konnte nicht verschickt werden. Bitte den Link unten persönlich weitergeben.";
        }
    }

    private async Task AuswahlLadenAsync(CancellationToken ct)
    {
        var rollen = await db.Roles.Select(r => r.Name!).ToListAsync(ct);
        RollenAuswahl = (await db.Rollenprofile.AsNoTracking().ToListAsync(ct))
            .Where(p => rollen.Contains(p.Name))
            .OrderBy(p => p.Systemrolle ? Array.IndexOf(Rollen.Alle, p.Name) : int.MaxValue).ThenBy(p => p.Bezeichnung)
            .Select(p => new SelectListItem(p.Bezeichnung, p.Name))
            .ToList();
        var vergeben = await db.Users
            .Where(b => b.MitgliedId != null && b.Id != Formular.Id)
            .Select(b => b.MitgliedId!.Value)
            .ToListAsync(ct);

        Mitglieder = await db.Mitglieder
            .Where(m => !vergeben.Contains(m.Id))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new SelectListItem(m.Nachname + ", " + m.Vorname + " (" + m.Personalnummer + ")", m.Id.ToString()))
            .ToListAsync(ct);
    }
}
