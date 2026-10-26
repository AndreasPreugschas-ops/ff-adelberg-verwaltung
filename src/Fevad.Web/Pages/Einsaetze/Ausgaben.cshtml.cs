using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Fevad.Domain.Einsaetze;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Einsaetze;

/// <summary>
/// Ausgaben des Einsatzberichts (V18): Bericht als PDF (wahlweise mit Teilnehmern, Beteiligte nur mit Recht), Anwesenheitsliste,
/// Pressefassung mit Freigabe und Mailversand des freigegebenen Berichts an die Standardempfänger aus den Stammdaten.
/// Pressefassung und Versand dürfen nur, wer Berichte freigeben darf; jeder Versand wird vermerkt.
/// </summary>
[Authorize(Policy = Berechtigungen.EinsaetzeLesen)]
public class AusgabenModel(AppDbContext db, IAuthorizationService autorisierung, IAktuellerBenutzer benutzer, IMailVersand mail, TimeProvider zeit, ILogger<AusgabenModel> log) : PageModel
{
    /// <summary>Der Einsatz.</summary>
    public Einsatz Einsatz { get; private set; } = null!;
    /// <summary>Darf Pressefassung freigeben und versenden.</summary>
    public bool DarfFreigeben { get; private set; }
    /// <summary>Darf Beteiligte sehen und mitschicken.</summary>
    public bool DarfBeteiligte { get; private set; }
    /// <summary>Mailversand ist eingerichtet.</summary>
    public bool MailAktiv => mail.Aktiv;
    /// <summary>Standardempfänger aus den Stammdaten.</summary>
    public IReadOnlyList<string> Standardempfaenger { get; private set; } = [];
    /// <summary>Bisherige Versände.</summary>
    public IReadOnlyList<EinsatzberichtVersand> Versaende { get; private set; } = [];
    /// <summary>Vorschlag für die Pressefassung.</summary>
    public string Vorschlag { get; private set; } = "";

    /// <summary>Text der Pressefassung im Formular.</summary>
    [BindProperty, StringLength(Einsatz.PressetextLaenge), Display(Name = "Pressefassung")]
    public string? Pressetext { get; set; }

    /// <summary>Zeigt die Seite.</summary>
    /// <param name="id">Der Einsatz.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Pressetext = Einsatz.Pressetext ?? Vorschlag;
        return Page();
    }

    /// <summary>Der Bericht als PDF.</summary>
    /// <param name="id">Der Einsatz.</param>
    /// <param name="teilnehmer">Teilnehmerliste aufnehmen.</param>
    /// <param name="beteiligte">Beteiligte aufnehmen (nur mit Recht).</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF, 403 oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(Guid id, bool teilnehmer, bool beteiligte, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (beteiligte && !DarfBeteiligte)
        {
            return Forbid();
        }

        return File(EinsatzberichtPdf.Bericht(await DatenAsync(teilnehmer, beteiligte, ct)), "application/pdf", EinsatzberichtPdf.Dateiname(Einsatz));
    }

    /// <summary>Die Anwesenheitsliste als PDF.</summary>
    /// <param name="id">Der Einsatz.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetAnwesenheitAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        return File(EinsatzberichtPdf.Anwesenheitsliste(await KopfAsync(ct), Einsatz), "application/pdf", EinsatzberichtPdf.DateinameAnwesenheit(Einsatz));
    }

    /// <summary>Speichert die Pressefassung; eine Änderung hebt die Freigabe auf.</summary>
    /// <param name="id">Der Einsatz.</param>
    /// <param name="freigeben">Zugleich freigeben.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Seite.</returns>
    public async Task<IActionResult> OnPostPresseAsync(Guid id, bool freigeben, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfFreigeben)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var text = string.IsNullOrWhiteSpace(Pressetext) ? null : Pressetext.Trim();
        var einsatz = await db.Einsaetze.FirstAsync(e => e.Id == id, ct);
        if (einsatz.Pressetext != text)
        {
            einsatz.Pressetext = text;
            einsatz.PresseFreigegebenAm = null;
            einsatz.PresseFreigegebenVon = null;
        }

        if (freigeben && text is not null)
        {
            einsatz.PresseFreigegebenAm = zeit.GetUtcNow();
            einsatz.PresseFreigegebenVon = await NameAsync(ct);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = einsatz.PresseFreigegebenAm is null ? "Pressefassung gespeichert, noch nicht freigegeben." : "Pressefassung freigegeben.";
        return RedirectToPage(new { id });
    }

    /// <summary>Schickt den freigegebenen Bericht als PDF an die gewählten Empfänger.</summary>
    /// <param name="id">Der Einsatz.</param>
    /// <param name="an">Gewählte Standardempfänger.</param>
    /// <param name="weitere">Weitere Adressen, durch Semikolon oder Komma getrennt.</param>
    /// <param name="teilnehmer">Teilnehmerliste mitschicken.</param>
    /// <param name="beteiligte">Beteiligte mitschicken (nur mit Recht).</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zurück zur Seite.</returns>
    public async Task<IActionResult> OnPostVersendenAsync(Guid id, string[] an, string? weitere, bool teilnehmer, bool beteiligte, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfFreigeben || (beteiligte && !DarfBeteiligte))
        {
            return Forbid();
        }

        if (Einsatz.Status != EinsatzStatus.Freigegeben || !mail.Aktiv)
        {
            TempData["Fehler"] = mail.Aktiv ? "Versenden lässt sich nur ein freigegebener Bericht." : "Der Mailversand ist nicht eingerichtet.";
            return RedirectToPage(new { id });
        }

        var adressen = an.Where(a => Standardempfaenger.Contains(a, StringComparer.OrdinalIgnoreCase))
            .Concat(Adressen(weitere)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (adressen.Count == 0 || adressen.Any(a => !MailAddress.TryCreate(a, out _)))
        {
            TempData["Fehler"] = adressen.Count == 0 ? "Bitte mindestens einen Empfänger wählen." : "Eine Adresse ist ungültig.";
            return RedirectToPage(new { id });
        }

        var o = await db.OrganisationAsync(ct);
        var pdf = new MailAnhang(EinsatzberichtPdf.Dateiname(Einsatz), "application/pdf", EinsatzberichtPdf.Bericht(await DatenAsync(teilnehmer, beteiligte, ct)));
        var text = $"Guten Tag,\n\nanbei der Einsatzbericht {Einsatz.Einsatznummer} ({Einsatz.Stichwort}, {Anzeige.DatumZeit(Einsatz.Alarmiert)} Uhr)."
            + (teilnehmer || beteiligte ? "\n\nDer Bericht enthält personenbezogene Daten. Bitte nur für den angegebenen Zweck verwenden und nicht weitergeben." : "")
            + $"\n\nMit freundlichen Grüßen\n{o.FeuerwehrName}";
        var gesendet = new List<string>();
        foreach (var adresse in adressen)
        {
            try
            {
                await mail.SendenAsync(adresse, $"Einsatzbericht {Einsatz.Einsatznummer}", text, [pdf], ct);
                gesendet.Add(adresse);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Einsatzbericht {Einsatz} an eine Adresse nicht versandt.", id);
            }
        }

        if (gesendet.Count > 0)
        {
            db.EinsatzberichtVersaende.Add(new EinsatzberichtVersand
            {
                EinsatzId = id, Empfaenger = string.Join("; ", gesendet), MitTeilnehmern = teilnehmer, MitBeteiligten = beteiligte,
                VersandtAm = zeit.GetUtcNow(), VersandtVon = await NameAsync(ct),
            });
            await db.SaveChangesAsync(ct);
        }

        TempData[gesendet.Count == adressen.Count ? "Meldung" : "Fehler"] = gesendet.Count == adressen.Count
            ? $"Bericht an {gesendet.Count} Empfänger verschickt."
            : $"Nur an {gesendet.Count} von {adressen.Count} Empfängern verschickt; bitte die übrigen Adressen prüfen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Zerlegt eine Adressliste.</summary>
    /// <param name="text">Adressen, durch Semikolon, Komma oder Zeilenumbruch getrennt.</param>
    /// <returns>Die Adressen.</returns>
    public static IReadOnlyList<string> Adressen(string? text) =>
        (text ?? "").Split([';', ',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var e = await db.Einsaetze.AsNoTracking()
            .Include(x => x.Fahrzeuge).ThenInclude(f => f.Fahrzeug)
            .Include(x => x.Teilnahmen).ThenInclude(t => t.Mitglied)
            .Include(x => x.Teilnahmen).ThenInclude(t => t.EinsatzFahrzeug).ThenInclude(f => f!.Fahrzeug)
            .Include(x => x.Organisationen)
            .Include(x => x.Beteiligte)
            .Include(x => x.Einsatzleiter)
            .Include(x => x.StatistikKategorie)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null)
        {
            return false;
        }

        Einsatz = e;
        DarfFreigeben = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeFreigeben)).Succeeded;
        DarfBeteiligte = (await autorisierung.AuthorizeAsync(User, Berechtigungen.EinsaetzeBeteiligte)).Succeeded;
        var o = await db.OrganisationAsync(ct);
        Standardempfaenger = Adressen(o.EinsatzberichtEmpfaenger);
        Vorschlag = EinsatzberichtPdf.Pressevorschlag(e, o.FeuerwehrName);
        Versaende = await db.EinsatzberichtVersaende.AsNoTracking().Where(v => v.EinsatzId == id).OrderByDescending(v => v.VersandtAm).ToListAsync(ct);
        return true;
    }

    private async Task<AntragPdf.Kopfdaten> KopfAsync(CancellationToken ct)
    {
        var o = await db.OrganisationAsync(ct);
        return new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp);
    }

    private async Task<EinsatzberichtPdf.Daten> DatenAsync(bool teilnehmer, bool beteiligte, CancellationToken ct)
    {
        var stand = Einsatz.Status == EinsatzStatus.Freigegeben
            ? $"freigegeben am {Anzeige.DatumZeit(Einsatz.FreigegebenAm)} Uhr"
            : $"Entwurf ({Anzeige.Text(Einsatz.Status)}), Ausdruck vom {Anzeige.DatumZeit(zeit.GetUtcNow())} Uhr";
        return new EinsatzberichtPdf.Daten(await KopfAsync(ct), Einsatz, teilnehmer, beteiligte, stand);
    }

    private async Task<string> NameAsync(CancellationToken ct)
    {
        if (benutzer.Id is not { } konto)
        {
            return "FEVAD";
        }

        return await db.Users.Where(u => u.Id == konto)
            .Select(u => db.Mitglieder.Where(x => x.Id == u.MitgliedId).Select(x => x.Vorname + " " + x.Nachname).FirstOrDefault() ?? u.UserName)
            .FirstOrDefaultAsync(ct) ?? "FEVAD";
    }
}
