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
    Rechteverzeichnis rechte,
    Fevad.Infrastructure.Benachrichtigungen.Benachrichtigungsdienst benachrichtigungen,
    ILogger<BearbeitenModel> log) : PageModel
{
    /// <summary>Rollen, die auf die zweite Freigabe warten (F18); sie erscheinen angehakt mit Hinweis.</summary>
    public IReadOnlySet<string> Wartend { get; private set; } = new HashSet<string>();

    private const string LinkSchluessel = "Einrichtungslink";
    private const string LinkBisSchluessel = "EinrichtungslinkBis";

    /// <summary>Formulardaten des Kontos.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Die Seite legt ein neues Konto an.</summary>
    public bool IstNeu => Formular.Id is null;
    /// <summary>Das bearbeitete Konto; <c>null</c> beim Anlegen.</summary>
    public BenutzerKonto? Vorhanden { get; private set; }
    /// <summary>Mitglieder zur Verknüpfung mit dem Konto.</summary>
    public IReadOnlyList<SelectListItem> Mitglieder { get; private set; } = [];

    /// <summary>Alle Rollen, mitgelieferte zuerst; Wert ist der technische Name.</summary>
    public IReadOnlyList<SelectListItem> RollenAuswahl { get; private set; } = [];
    /// <summary>Der E-Mail-Versand ist eingerichtet.</summary>
    public bool MailAktiv => links.MailAktiv;
    /// <summary>Der Administrator bearbeitet sein eigenes Konto.</summary>
    public bool IstSelbst => Formular.Id is not null && Formular.Id == Guid.Parse(benutzer.GetUserId(User)!);
    /// <summary>Frisch erzeugter Einrichtungslink zum Weitergeben, wenn er nicht per E-Mail geht.</summary>
    public string? Einrichtungslink { get; private set; }
    /// <summary>Ablauf des Einrichtungslinks als Text in Ortszeit.</summary>
    public string? EinrichtungslinkBis { get; private set; }

    /// <summary>Kinder, deren Termine dieses Konto als Elternkonto sieht; zugeordnet wird in den Stammdaten des Kindes.</summary>
    public IReadOnlyList<string> Kinder { get; private set; } = [];

    /// <summary>Eingabefelder eines Kontos.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung des Kontos; <c>null</c> beim Anlegen.</summary>
        public Guid? Id { get; set; }

        /// <summary>E-Mail-Adresse, zugleich Benutzername.</summary>
        [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse."), Display(Name = "E-Mail-Adresse (Benutzername)")]
        public string? Email { get; set; }

        /// <summary>Verknüpftes Mitglied.</summary>
        [Display(Name = "Mitglied")]
        public Guid? MitgliedId { get; set; }

        /// <summary>Namen der zugewiesenen Rollen.</summary>
        public List<string> Rollen { get; set; } = [];

        /// <summary>Tag der schriftlichen Verpflichtung auf Vertraulichkeit; Pflicht für Rollen der Gemeindeverwaltung.</summary>
        [Display(Name = "Auf Vertraulichkeit verpflichtet am")]
        public DateOnly? VerpflichtetAm { get; set; }

        /// <summary>Beim Anlegen den Einrichtungslink per E-Mail schicken.</summary>
        [Display(Name = "Einrichtungslink per E-Mail schicken")]
        public bool LinkSenden { get; set; } = true;
    }

    /// <summary>Zeigt ein Konto oder ein leeres Formular.</summary>
    /// <param name="id">Kennung des Kontos; <c>null</c> legt neu an.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
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
                VerpflichtetAm = konto.VerpflichtetAm,
            };
            Wartend = (await db.Rechtefreigaben.AsNoTracking()
                .Where(f => f.Art == RechtefreigabeArt.Rollenzuweisung && f.KontoId == konto.Id && f.Freigegeben == null)
                .Select(f => f.Rolle).ToListAsync(ct)).ToHashSet();
            Formular.Rollen.AddRange(Wartend.Except(Formular.Rollen));
            Vorhanden = konto;
            Kinder = await db.Erziehungsberechtigte.Where(e => e.KontoId == konto.Id)
                .Select(e => e.Mitglied!.Vorname + " " + e.Mitglied.Nachname).OrderBy(n => n).ToListAsync(ct);
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

    /// <summary>Speichert das Konto oder legt es an; der Administrator behält dabei mindestens ein aktives Administratorkonto.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Konto, die Seite mit Fehlern oder 404.</returns>
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

        if (Formular.VerpflichtetAm is null && Formular.Rollen.Intersect(Rollen.VerpflichtungPflicht).Any())
        {
            // Speichern geht, die Rollen wirken aber erst mit der Verpflichtung (siehe BenutzerClaimsFactory).
            TempData["Fehler"] = "Rollen der Gemeindeverwaltung und die Rolle Betreuer Jugendfeuerwehr wirken erst, wenn die Verpflichtung auf Vertraulichkeit eingetragen ist.";
        }

        IdentityResult ergebnis;
        if (konto is null)
        {
            konto = new BenutzerKonto { UserName = email, Email = email, MitgliedId = Formular.MitgliedId, VerpflichtetAm = Formular.VerpflichtetAm };
            ergebnis = await benutzer.CreateAsync(konto);
        }
        else
        {
            konto.MitgliedId = Formular.MitgliedId;
            konto.VerpflichtetAm = Formular.VerpflichtetAm;
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

        var zurueck = new List<string>();
        if (ergebnis.Succeeded)
        {
            var bisher = await benutzer.GetRolesAsync(konto);
            var weg = bisher.Except(Formular.Rollen).ToList();
            var dazu = Formular.Rollen.Except(bisher).ToList();

            // Vier-Augen (F18): Rollen mit Rechten auf Gesundheits- oder Jugenddaten bekommt das Konto erst nach der zweiten Freigabe.
            zurueck = (await SchutzbeduerftigeRollenAsync(dazu, ct)).ToList();
            dazu = dazu.Except(zurueck).ToList();
            db.Rechtefreigaben.RemoveRange(await db.Rechtefreigaben
                .Where(f => f.Art == RechtefreigabeArt.Rollenzuweisung && f.KontoId == konto.Id && f.Freigegeben == null).ToListAsync(ct));
            var antragsteller = Guid.Parse(benutzer.GetUserId(User)!);
            foreach (var rolle in zurueck)
            {
                db.Rechtefreigaben.Add(new Rechtefreigabe { Art = RechtefreigabeArt.Rollenzuweisung, Rolle = rolle, KontoId = konto.Id, BeantragtVon = antragsteller });
            }

            await db.SaveChangesAsync(ct);
            if (zurueck.Count > 0)
            {
                await benachrichtigungen.AnRechtAsync(Berechtigungen.RechteFreigeben,
                    new(Fevad.Domain.Gemeinsam.Benachrichtigungsart.Aufgabe, "Rollenzuweisung wartet auf deine Freigabe", "/Verwaltung/Rechtefreigaben"), antragsteller, ct);
            }
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

        if (zurueck.Count > 0)
        {
            var namen = await db.Rollenprofile.AsNoTracking().Where(p => zurueck.Contains(p.Name)).Select(p => p.Bezeichnung).ToListAsync(ct);
            TempData["Meldung"] = $"{TempData["Meldung"]} Die {(zurueck.Count == 1 ? "Rolle" : "Rollen")} {string.Join(", ", namen)} "
                + $"{(zurueck.Count == 1 ? "öffnet" : "öffnen")} Gesundheits- oder Jugenddaten und {(zurueck.Count == 1 ? "wirkt" : "wirken")} erst, wenn eine zweite Person sie unter „Rechte freigeben“ freigibt.";
        }

        return RedirectToPage(new { id = konto.Id });
    }

    /// <summary>Welche dieser Rollen Rechte auf Gesundheits- oder Jugenddaten haben und deshalb eine zweite Freigabe brauchen (F18).</summary>
    /// <param name="rollen">Technische Rollennamen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die schutzbedürftigen Rollen.</returns>
    private async Task<IReadOnlySet<string>> SchutzbeduerftigeRollenAsync(IReadOnlyCollection<string> rollen, CancellationToken ct)
    {
        if (rollen.Count == 0)
        {
            return new HashSet<string>();
        }

        var jugend = (await rechte.StandAsync(ct)).Jugendabteilungen;
        var rechteDerRollen = await db.RollenRechte.AsNoTracking().Where(r => rollen.Contains(r.Rolle)).ToListAsync(ct);
        return rechteDerRollen.Where(r => Berechtigungen.Schutzbeduerftig(r.Recht, r.Abteilungen, jugend)).Select(r => r.Rolle).ToHashSet();
    }

    /// <summary>Erzeugt einen neuen Einrichtungslink und schickt ihn auf Wunsch per E-Mail.</summary>
    /// <param name="id">Kennung des Kontos.</param>
    /// <param name="senden">Den Link per E-Mail schicken statt nur anzeigen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Konto oder 404 bei deaktiviertem oder unbekanntem Konto.</returns>
    public async Task<IActionResult> OnPostLinkAsync(Guid id, bool senden, CancellationToken ct)
    {
        if (await benutzer.FindByIdAsync(id.ToString()) is not { IstDeaktiviert: false } konto)
        {
            return NotFound();
        }

        await LinkAusgebenAsync(konto, senden, ct);
        return RedirectToPage(new { id });
    }

    /// <summary>Deaktiviert das Konto; das letzte aktive Administratorkonto bleibt bestehen.</summary>
    /// <param name="id">Kennung des Kontos.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Konto mit Meldung oder Fehler, 404 bei unbekanntem Konto.</returns>
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

    /// <summary>Hebt eine vorübergehende Sperre nach zu vielen Fehlversuchen auf.</summary>
    /// <param name="id">Kennung des Kontos.</param>
    /// <returns>Weiterleitung zum Konto mit Meldung oder 404.</returns>
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
