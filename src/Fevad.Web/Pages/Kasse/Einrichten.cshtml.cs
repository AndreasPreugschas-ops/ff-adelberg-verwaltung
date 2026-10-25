using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kassen;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kasse;

/// <summary>
/// Kassen einrichten: Kassen anlegen und abschalten, Konten mit Anfangsbestand, Gliederung des Wirtschaftsplans und die Einstellungen
/// (Beschlussgrenze, Größe der Belege).
/// </summary>
[Authorize(Policy = Berechtigungen.KasseEinrichten)]
public class EinrichtenModel(AppDbContext db, Parametereinstellungen einstellungen, TimeProvider zeit) : PageModel
{
    /// <summary>Die einstellbaren Parameter der Kassen.</summary>
    public static IReadOnlyList<Einstellung> Alle { get; } =
    [
        new("Einstellungen der Kassen", ParameterSchluessel.KasseAusschussbeschlussUeberEuro, "Ausgaben beschließt der Ausschuss ab über", "Euro", 100, 0, 100_000,
            "Gilt für alle Kassen. FEVAD weist beim Buchen nur darauf hin und sperrt nichts (Andi, 25.10.2026). 0 = kein Hinweis."),
        new("Einstellungen der Kassen", ParameterSchluessel.KasseBelegMaxMegabyte, "Belegdateien höchstens", "MB", 10, 1, 50),
    ];

    /// <summary>Alle Kassen mit Konten und Positionen.</summary>
    public IReadOnlyList<Domain.Kassen.Kasse> Kassen { get; private set; } = [];

    /// <summary>Konten mit Buchungen: Beginn und Anfangsbestand sind dann fest.</summary>
    public IReadOnlySet<Guid> KontenMitBuchungen { get; private set; } = new HashSet<Guid>();

    /// <summary>Abteilungen für die Zuordnung einer Kasse.</summary>
    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];

    /// <summary>Werte der Einstellungen.</summary>
    [BindProperty] public Dictionary<string, int> Werte { get; set; } = [];

    /// <summary>Eingabe für eine Kasse.</summary>
    /// <param name="Name">Name.</param>
    /// <param name="AbteilungId">Abteilung, nur beim Anlegen.</param>
    /// <param name="Aktiv">Aktiv.</param>
    /// <param name="Bemerkung">Bemerkung.</param>
    public sealed record KasseEingabe(string? Name, Guid? AbteilungId, bool Aktiv, string? Bemerkung);

    /// <summary>Eingabe für ein Konto.</summary>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Art">Art.</param>
    /// <param name="Iban">IBAN des Kontos der Kasse.</param>
    /// <param name="Bank">Bank.</param>
    /// <param name="Eroeffnet">Beginn in FEVAD.</param>
    /// <param name="Anfangsbestand">Anfangsbestand.</param>
    /// <param name="Aktiv">Aktiv.</param>
    public sealed record KontoEingabe(string? Bezeichnung, KontoArt Art, string? Iban, string? Bank, DateOnly? Eroeffnet, decimal? Anfangsbestand, bool Aktiv);

    /// <summary>Eingabe für eine Position.</summary>
    /// <param name="Nummer">Nummer, nur beim Anlegen.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="Richtung">Einnahme oder Ausgabe, nur beim Anlegen.</param>
    /// <param name="Deckungsfaehig">Deckungsfähig in der Hauptgruppe.</param>
    /// <param name="Hinweis">Hinweis beim Buchen.</param>
    /// <param name="Aktiv">Aktiv.</param>
    public sealed record PositionEingabe(string? Nummer, string? Bezeichnung, Planrichtung Richtung, bool Deckungsfaehig, string? Hinweis, bool Aktiv);

    /// <summary>Zeigt die Einrichtung.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Werte = await einstellungen.LadenAsync(Alle, Anzeige.Heute(zeit), ct);
        await LadenAsync(ct);
    }

    /// <summary>Speichert die Einstellungen ab heute.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostEinstellungenAsync(CancellationToken ct)
    {
        if (await einstellungen.SpeichernAsync(Alle, Werte, ModelState, Anzeige.Heute(zeit), ct) is not { } geaendert)
        {
            await LadenAsync(ct);
            return Page();
        }

        TempData["Meldung"] = geaendert == 0 ? "Nichts geändert." : $"{geaendert} Einstellungen gespeichert, gültig ab heute.";
        return RedirectToPage();
    }

    /// <summary>Legt eine Kasse mit Girokonto, Barkasse und der Gliederung nach dem LFV-Muster an.</summary>
    /// <param name="eingabe">Name und Abteilung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung.</returns>
    public async Task<IActionResult> OnPostNeueKasseAsync([FromForm] KasseEingabe eingabe, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(eingabe.Name))
        {
            TempData["Meldung"] = "Bitte einen Namen angeben.";
            return RedirectToPage();
        }

        var abteilung = eingabe.AbteilungId is { } a && await db.Abteilungen.AnyAsync(x => x.Id == a, ct) ? a : (Guid?)null;
        var heute = Anzeige.Heute(zeit);
        var sortierung = (await db.Kassen.MaxAsync(k => (int?)k.Sortierung, ct) ?? 0) + 10;
        db.Kassen.Add(Kassenfuehrung.Neu(eingabe.Name.Trim(), abteilung, sortierung, new DateOnly(heute.Year, 1, 1), Kassenregeln.MusterKameradschaftskasse));
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Kasse „{eingabe.Name.Trim()}“ angelegt.";
        return RedirectToPage();
    }

    /// <summary>Ändert Name, Bemerkung und Aktiv einer Kasse.</summary>
    /// <param name="id">Kennung der Kasse.</param>
    /// <param name="eingabe">Die Felder.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostKasseAsync(Guid id, [FromForm] KasseEingabe eingabe, CancellationToken ct)
    {
        var kasse = await db.Kassen.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kasse is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(eingabe.Name))
        {
            kasse.Name = eingabe.Name.Trim();
        }

        kasse.Aktiv = eingabe.Aktiv;
        kasse.Bemerkung = string.IsNullOrWhiteSpace(eingabe.Bemerkung) ? null : eingabe.Bemerkung.Trim();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Kasse „{kasse.Name}“ gespeichert.";
        return RedirectToPage();
    }

    /// <summary>Legt ein Konto an oder ändert es. Beginn und Anfangsbestand sind fest, sobald es Buchungen gibt.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="konto">Kennung des Kontos; leer für ein neues.</param>
    /// <param name="eingabe">Die Felder.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder 404.</returns>
    public async Task<IActionResult> OnPostKontoAsync(Guid kasse, Guid? konto, [FromForm] KontoEingabe eingabe, CancellationToken ct)
    {
        if (!await db.Kassen.AnyAsync(k => k.Id == kasse, ct))
        {
            return NotFound();
        }

        string? iban = null;
        if (!string.IsNullOrWhiteSpace(eingabe.Iban) && (iban = Iban.Normalisieren(eingabe.Iban)) is null)
        {
            TempData["Meldung"] = "Die IBAN ist ungültig.";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(eingabe.Bezeichnung))
        {
            TempData["Meldung"] = "Bitte eine Bezeichnung angeben.";
            return RedirectToPage();
        }

        var eintrag = konto is { } kid ? await db.Zahlungsmittelkonten.FirstOrDefaultAsync(k => k.Id == kid && k.KasseId == kasse, ct) : null;
        if (konto is not null && eintrag is null)
        {
            return NotFound();
        }

        if (eintrag is null)
        {
            eintrag = new Zahlungsmittelkonto
            {
                KasseId = kasse, Bezeichnung = "",
                Sortierung = (await db.Zahlungsmittelkonten.Where(k => k.KasseId == kasse).MaxAsync(k => (int?)k.Sortierung, ct) ?? 0) + 10,
            };
            db.Zahlungsmittelkonten.Add(eintrag);
        }

        var gebucht = konto is { } id && await db.Buchungen.AnyAsync(b => b.KontoId == id || b.GegenkontoId == id, ct);
        eintrag.Bezeichnung = eingabe.Bezeichnung.Trim();
        eintrag.Art = eingabe.Art;
        eintrag.Iban = iban;
        eintrag.Bank = string.IsNullOrWhiteSpace(eingabe.Bank) ? null : eingabe.Bank.Trim();
        eintrag.Aktiv = konto is null || eingabe.Aktiv;
        if (!gebucht)
        {
            eintrag.Eroeffnet = eingabe.Eroeffnet ?? new DateOnly(Anzeige.Heute(zeit).Year, 1, 1);
            eintrag.Anfangsbestand = Math.Round(eingabe.Anfangsbestand ?? 0, 2);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Konto „{eintrag.Bezeichnung}“ gespeichert."
            + (gebucht ? " Beginn und Anfangsbestand bleiben unverändert, weil schon gebucht wurde." : "");
        return RedirectToPage();
    }

    /// <summary>Legt eine Position der Gliederung an oder ändert Bezeichnung, Deckungsfähigkeit, Hinweis und Aktiv.</summary>
    /// <param name="kasse">Kennung der Kasse.</param>
    /// <param name="position">Kennung der Position; leer für eine neue.</param>
    /// <param name="eingabe">Die Felder.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung oder 404.</returns>
    public async Task<IActionResult> OnPostPositionAsync(Guid kasse, Guid? position, [FromForm] PositionEingabe eingabe, CancellationToken ct)
    {
        if (!await db.Kassen.AnyAsync(k => k.Id == kasse, ct))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(eingabe.Bezeichnung))
        {
            TempData["Meldung"] = "Bitte eine Bezeichnung angeben.";
            return RedirectToPage();
        }

        Planposition? eintrag;
        if (position is { } pid)
        {
            eintrag = await db.Planpositionen.FirstOrDefaultAsync(p => p.Id == pid && p.KasseId == kasse, ct);
            if (eintrag is null)
            {
                return NotFound();
            }

            eintrag.Aktiv = eingabe.Aktiv;
        }
        else
        {
            var nummer = eingabe.Nummer?.Trim() ?? "";
            if (nummer.Length is 0 or > 10 || !char.IsAsciiDigit(nummer[0]))
            {
                TempData["Meldung"] = "Die Nummer beginnt mit der Ziffer der Hauptgruppe, z. B. „6.3“.";
                return RedirectToPage();
            }

            if (await db.Planpositionen.AnyAsync(p => p.KasseId == kasse && p.Nummer == nummer, ct))
            {
                TempData["Meldung"] = $"Position {nummer} gibt es schon.";
                return RedirectToPage();
            }

            eintrag = new Planposition { KasseId = kasse, Nummer = nummer, Bezeichnung = "", Richtung = eingabe.Richtung };
            db.Planpositionen.Add(eintrag);
        }

        eintrag.Bezeichnung = eingabe.Bezeichnung.Trim();
        eintrag.Deckungsfaehig = eingabe.Deckungsfaehig;
        eintrag.Hinweis = string.IsNullOrWhiteSpace(eingabe.Hinweis) ? null : eingabe.Hinweis.Trim();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Position {eintrag.Nummer} gespeichert.";
        return RedirectToPage();
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Kassen = await db.Kassen.AsNoTracking().Include(k => k.Abteilung).Include(k => k.Konten).Include(k => k.Positionen)
            .OrderBy(k => k.Sortierung).ThenBy(k => k.Name).ToListAsync(ct);
        KontenMitBuchungen = (await db.Buchungen.AsNoTracking().Select(b => b.KontoId)
            .Union(db.Buchungen.Where(b => b.GegenkontoId != null).Select(b => b.GegenkontoId!.Value)).ToListAsync(ct)).ToHashSet();
        Abteilungen = await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv).OrderBy(a => a.Sortierung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToListAsync(ct);
    }
}
