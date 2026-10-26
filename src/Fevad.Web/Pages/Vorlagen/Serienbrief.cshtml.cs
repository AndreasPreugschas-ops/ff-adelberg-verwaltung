using System.Globalization;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Gremien;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Vorlagen;

/// <summary>
/// Serienbrief aus einer Vorlage (V56): Empfänger je Abteilung, Ausgabe als PDF, Word-Datei oder Serienmail. Die Empfänger kommen aus dem
/// Datenbereich des Benutzers, damit die Abschirmung der Jugendfeuerwehr auch hier gilt.
/// </summary>
public class SerienbriefModel(AppDbContext db, Datenbereich datenbereich, Serienbriefe serienbriefe, TimeProvider zeit) : PageModel
{
    /// <summary>Kennung der Vorlage.</summary>
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }

    /// <summary>Statt der Kennung der Code der Vorlage, z. B. „BEFU“ für den Link aus der Laufbahn.</summary>
    [BindProperty(SupportsGet = true)] public string? Code { get; set; }

    /// <summary>Nur dieses Mitglied, z. B. für eine einzelne Urkunde.</summary>
    [BindProperty(SupportsGet = true)] public Guid? MitgliedId { get; set; }

    /// <summary>Nur diese Abteilung; leer für alle.</summary>
    [BindProperty(SupportsGet = true)] public Guid? AbteilungId { get; set; }

    /// <summary>Datum des Schreibens; ohne Angabe heute.</summary>
    [BindProperty(SupportsGet = true)] public DateOnly? Datum { get; set; }

    /// <summary>Name unter dem Schreiben.</summary>
    [BindProperty(SupportsGet = true)] public string? Unterzeichner { get; set; }

    /// <summary>Die Vorlage.</summary>
    public Briefvorlage Vorlage { get; private set; } = null!;

    /// <summary>Die Empfänger.</summary>
    public IReadOnlyList<Serienbriefe.Empfaenger> Empfaenger { get; private set; } = [];

    /// <summary>Abteilungen zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Abteilungen { get; private set; } = [];

    /// <summary>Der Mailversand ist eingerichtet.</summary>
    public bool MailAktiv => serienbriefe.MailAktiv;

    /// <summary>Empfänger mit E-Mail-Adresse.</summary>
    public int MitMail => Empfaenger.Count(e => !string.IsNullOrWhiteSpace(e.Email));

    /// <summary>Datum für Links im ISO-Format, das die Datumsfelder ebenfalls lesen.</summary>
    public string DatumText => (Datum ?? Anzeige.Heute(zeit)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Zeigt Auswahl und Empfänger.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!await LadenAsync(ct))
        {
            return NotFound();
        }

        Abteilungen = await db.Abteilungen.AsNoTracking().Where(a => a.Aktiv).OrderBy(a => a.Sortierung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToListAsync(ct);
        return Page();
    }

    /// <summary>Serienbrief als PDF.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(CancellationToken ct)
    {
        if (!await LadenAsync(ct))
        {
            return NotFound();
        }

        var o = await db.Organisationen.AsNoTracking().FirstAsync(ct);
        var kopf = new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp);
        var pdf = SerienbriefPdf.Erzeugen(kopf, Vorlage.Form, Serienbriefe.Fuellen(Vorlage, Empfaenger, o.FeuerwehrName, Tag), Tag, UnterzeichnerText);
        return File(pdf, "application/pdf", $"{Dateiname} {Tag:yyyy-MM-dd}.pdf");
    }

    /// <summary>Serienbrief als Word-Datei.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei oder 404.</returns>
    public async Task<IActionResult> OnGetDocxAsync(CancellationToken ct)
    {
        if (!await LadenAsync(ct))
        {
            return NotFound();
        }

        var feuerwehr = await db.Organisationen.AsNoTracking().Select(o => o.FeuerwehrName).FirstAsync(ct);
        var docx = SerienbriefDocx.Erzeugen(feuerwehr, Vorlage.Form, Serienbriefe.Fuellen(Vorlage, Empfaenger, feuerwehr, Tag), Tag, UnterzeichnerText);
        return File(docx, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{Dateiname} {Tag:yyyy-MM-dd}.docx");
    }

    /// <summary>Sendet jedem Empfänger mit E-Mail-Adresse eine eigene Mail.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Ergebnis oder 404.</returns>
    public async Task<IActionResult> OnPostMailAsync(CancellationToken ct)
    {
        if (!await LadenAsync(ct))
        {
            return NotFound();
        }

        if (Vorlage.Form != VorlageForm.Brief || !serienbriefe.MailAktiv)
        {
            return BadRequest();
        }

        var feuerwehr = await db.Organisationen.AsNoTracking().Select(o => o.FeuerwehrName).FirstAsync(ct);
        var ergebnis = await serienbriefe.MailsSendenAsync(Serienbriefe.Fuellen(Vorlage, Empfaenger, feuerwehr, Tag), feuerwehr, ct);
        var text = $"{ergebnis.Gesendet} Mail(s) gesendet.";
        if (ergebnis.OhneMail.Count > 0)
        {
            text += $" Ohne E-Mail-Adresse, bitte auf Papier: {string.Join("; ", ergebnis.OhneMail)}.";
        }

        if (ergebnis.Fehler.Count > 0)
        {
            text += $" Nicht zustellbar: {string.Join(", ", ergebnis.Fehler)}.";
        }

        TempData["Meldung"] = text;
        return RedirectToPage(new { id = Id, mitgliedId = MitgliedId, abteilungId = AbteilungId, datum = DatumText });
    }

    private DateOnly Tag => Datum ?? Anzeige.Heute(zeit);

    private string? UnterzeichnerText => string.IsNullOrWhiteSpace(Unterzeichner) ? null : Unterzeichner.Trim();

    private string Dateiname => string.Concat(Vorlage.Bezeichnung.Select(z => char.IsLetterOrDigit(z) || z is ' ' or '-' ? z : '_')).Trim();

    private async Task<bool> LadenAsync(CancellationToken ct)
    {
        if (await db.Briefvorlagen.AsNoTracking().FirstOrDefaultAsync(v => (v.Id == Id || (Code != null && v.Code == Code)) && v.Aktiv, ct) is not { } vorlage)
        {
            return false;
        }

        Vorlage = vorlage;
        Id = vorlage.Id;
        // Empfänger nie zu einem vergangenen Stichtag, sonst ließen sich Ausgeschiedene anschreiben; das Datum des Schreibens bleibt frei.
        var heute = Anzeige.Heute(zeit);
        var stichtag = Tag < heute ? heute : Tag;
        var sichtbar = await datenbereich.Filtern(db.Mitglieder, User, stichtag);
        if (MitgliedId is { } mitglied)
        {
            sichtbar = sichtbar.Where(m => m.Id == mitglied);
        }

        Empfaenger = await serienbriefe.EmpfaengerAsync(sichtbar, vorlage.Empfaenger, AbteilungId, stichtag, ct);
        return true;
    }
}
