using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Gemeinde;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gemeinde;

/// <summary>
/// Bescheid des Bürgermeisters an ein Mitglied oder einen Bewerber (V27): Mustertexte je Art, Anhörung mit Frist (Parameter), Erlass erst nach
/// Ablauf der Frist oder Stellungnahme, Bekanntgabe, PDF von Anhörung und Bescheid mit Rechtsbehelfsbelehrung.
/// </summary>
[Authorize(Policy = Berechtigungen.GemeindeMitgliederbescheide)]
public class BescheidModel(AppDbContext db, ParameterLeser parameter, TimeProvider zeit) : PageModel
{
    /// <summary>Eingabe des Bescheids.</summary>
    public sealed class BescheidEingabe
    {
        /// <summary>Art.</summary>
        [Display(Name = "Art")] public MitgliederbescheidArt Art { get; set; }
        /// <summary>Mitglied.</summary>
        [Display(Name = "Mitglied")] public Guid? MitgliedId { get; set; }
        /// <summary>Empfänger.</summary>
        [StringLength(200), Display(Name = "Empfänger (leer: aus dem Mitglied)")] public string? EmpfaengerName { get; set; }
        /// <summary>Anschrift.</summary>
        [StringLength(300), Display(Name = "Anschrift (leer: aus dem Mitglied)")] public string? EmpfaengerAnschrift { get; set; }
        /// <summary>Sachverhalt.</summary>
        [StringLength(4000), Display(Name = "Sachverhalt")] public string? Sachverhalt { get; set; }
        /// <summary>Tenor.</summary>
        [StringLength(1000), Display(Name = "Entscheidungssatz")] public string? Tenor { get; set; }
        /// <summary>Begründung.</summary>
        [StringLength(4000), Display(Name = "Begründung")] public string? Begruendung { get; set; }
        /// <summary>Betrag der Geldbuße.</summary>
        [Range(0, 100000, ErrorMessage = "Bitte einen Betrag angeben."), Display(Name = "Geldbuße (€)")] public decimal? Betrag { get; set; }
        /// <summary>Stellungnahme.</summary>
        [StringLength(2000), Display(Name = "Stellungnahme des Betroffenen")] public string? Stellungnahme { get; set; }
    }

    /// <summary>Der Bescheid; null beim Anlegen.</summary>
    public Mitgliederbescheid? Bescheid { get; private set; }

    /// <summary>Mitglieder zur Auswahl.</summary>
    public IReadOnlyList<SelectListItem> Mitgliederauswahl { get; private set; } = [];

    /// <summary>Arten zur Auswahl.</summary>
    public static IReadOnlyList<SelectListItem> Arten { get; } =
        Enum.GetValues<MitgliederbescheidArt>().Select(a => new SelectListItem(Anzeige.Text(a), a.ToString())).ToList();

    /// <summary>Formular.</summary>
    [BindProperty]
    public BescheidEingabe Formular { get; set; } = new();

    /// <summary>Tag der Bekanntgabe.</summary>
    [BindProperty, Display(Name = "Bekanntgegeben am")]
    public DateOnly? BekanntgegebenAm { get; set; }

    /// <summary>Zeigt den Bescheid oder ein neues Formular mit Mustertexten.</summary>
    /// <param name="id">Kennung oder null.</param>
    /// <param name="art">Art für die Mustertexte beim Anlegen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, MitgliederbescheidArt? art, CancellationToken ct)
    {
        if (id is { } bid)
        {
            Bescheid = await db.Mitgliederbescheide.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bid, ct);
            if (Bescheid is null)
            {
                return NotFound();
            }

            var b = Bescheid;
            Formular = new BescheidEingabe
            {
                Art = b.Art, MitgliedId = b.MitgliedId, EmpfaengerName = b.EmpfaengerName, EmpfaengerAnschrift = b.EmpfaengerAnschrift, Sachverhalt = b.Sachverhalt,
                Tenor = b.Tenor, Begruendung = b.Begruendung, Betrag = b.Betrag, Stellungnahme = b.Stellungnahme,
            };
            BekanntgegebenAm = Anzeige.Heute(zeit);
        }
        else
        {
            var a = art ?? MitgliederbescheidArt.Geldbusse;
            var (tenor, begruendung) = Mitgliederbescheid.Muster(a);
            Formular = new BescheidEingabe { Art = a, Tenor = tenor, Begruendung = begruendung };
        }

        await AnzeigeAsync(ct);
        return Page();
    }

    /// <summary>Legt den Bescheid an oder speichert ihn, solange er nicht erlassen ist; die Stellungnahme auch danach nicht mehr.</summary>
    /// <param name="id">Kennung oder null.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern oder 404.</returns>
    public async Task<IActionResult> OnPostSpeichernAsync(Guid? id, CancellationToken ct)
    {
        Mitgliederbescheid? b = null;
        if (id is { } bid)
        {
            b = await db.Mitgliederbescheide.FirstOrDefaultAsync(x => x.Id == bid, ct);
            if (b is null)
            {
                return NotFound();
            }

            if (b.ErlassenAm is not null || b.Eingestellt)
            {
                ModelState.AddModelError(string.Empty, "Ein erlassener oder eingestellter Bescheid wird nicht mehr geändert.");
            }
        }

        var heute = Anzeige.Heute(zeit);
        Mitglied? mitglied = null;
        if (Formular.MitgliedId is { } mid)
        {
            if (!await Auswahl.WaehlbarAsync(db, heute, mid, b?.MitgliedId, ct))
            {
                ModelState.AddModelError("Formular.MitgliedId", "Dieses Mitglied ist hier nicht wählbar.");
            }
            else
            {
                mitglied = await db.Mitglieder.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mid, ct);
            }
        }

        var name = Leer(Formular.EmpfaengerName) ?? (mitglied is null ? null : $"{mitglied.Vorname} {mitglied.Nachname}");
        if (name is null)
        {
            ModelState.AddModelError("Formular.EmpfaengerName", "Bitte ein Mitglied wählen oder den Empfänger angeben.");
        }

        if (!ModelState.IsValid)
        {
            Bescheid = b;
            await AnzeigeAsync(ct);
            return Page();
        }

        if (b is null)
        {
            b = new Mitgliederbescheid { EmpfaengerName = "" };
            db.Mitgliederbescheide.Add(b);
        }

        var f = Formular;
        b.Art = f.Art;
        b.MitgliedId = f.MitgliedId;
        b.EmpfaengerName = name!;
        b.EmpfaengerAnschrift = Leer(f.EmpfaengerAnschrift) ?? (mitglied is null ? null : Anschrift.Zeile(mitglied.Strasse, mitglied.Plz, mitglied.Ort));
        b.Sachverhalt = Leer(f.Sachverhalt);
        b.Tenor = Leer(f.Tenor);
        b.Begruendung = Leer(f.Begruendung);
        b.Betrag = f.Art == MitgliederbescheidArt.Geldbusse ? f.Betrag : null;
        b.Stellungnahme = Leer(f.Stellungnahme);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Bescheid gespeichert.";
        return RedirectToPage(new { id = b.Id });
    }

    /// <summary>Vermerkt den Versand der Anhörung heute und setzt die Frist aus dem Parameter.</summary>
    /// <param name="id">Kennung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostAnhoerungAsync(Guid id, CancellationToken ct)
    {
        var b = await db.Mitgliederbescheide.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null)
        {
            return NotFound();
        }

        if (b.Stand == MitgliederbescheidStand.Entwurf)
        {
            var heute = Anzeige.Heute(zeit);
            b.AnhoerungAm = heute;
            b.AnhoerungBis = heute.AddDays(await parameter.ZahlAsync(ParameterSchluessel.MitgliederbescheidAnhoerungFristTage, heute, 14, ct));
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Erlässt den Bescheid mit heutigem Datum, wenn Texte, Anhörung und Betrag passen.</summary>
    /// <param name="id">Kennung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostErlassenAsync(Guid id, CancellationToken ct)
    {
        var b = await db.Mitgliederbescheide.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null)
        {
            return NotFound();
        }

        var heute = Anzeige.Heute(zeit);
        if (b.Stand is MitgliederbescheidStand.Entwurf or MitgliederbescheidStand.Anhoerung)
        {
            var hoechst = await parameter.DezimalAsync(ParameterSchluessel.GeldbusseHoechstbetrag, heute, ct) ?? 0;
            if (b.ErlassPruefen(heute, hoechst) is { } fehler)
            {
                TempData["Fehler"] = fehler;
            }
            else
            {
                b.ErlassenAm = heute;
                await db.SaveChangesAsync(ct);
                TempData["Meldung"] = "Bescheid erlassen. Bitte ausdrucken, unterschreiben lassen und zustellen.";
            }
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Vermerkt die Bekanntgabe.</summary>
    /// <param name="id">Kennung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostBekanntgabeAsync(Guid id, CancellationToken ct)
    {
        var b = await db.Mitgliederbescheide.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null)
        {
            return NotFound();
        }

        if (b.Stand == MitgliederbescheidStand.Erlassen && BekanntgegebenAm is { } tag && tag >= b.ErlassenAm)
        {
            b.BekanntgegebenAm = tag;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            TempData["Fehler"] = "Die Bekanntgabe geht nur bei einem erlassenen Bescheid und nicht vor dem Erlass.";
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Stellt das Verfahren ein, solange nicht erlassen.</summary>
    /// <param name="id">Kennung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostEinstellenAsync(Guid id, CancellationToken ct)
    {
        var b = await db.Mitgliederbescheide.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null)
        {
            return NotFound();
        }

        if (b.ErlassenAm is null)
        {
            b.Eingestellt = true;
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Gibt Anhörung oder Bescheid als PDF aus.</summary>
    /// <param name="id">Kennung.</param>
    /// <param name="anhoerung">true: Anhörung, false: Bescheid.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(Guid id, bool anhoerung, CancellationToken ct)
    {
        var b = await db.Mitgliederbescheide.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null)
        {
            return NotFound();
        }

        var heute = Anzeige.Heute(zeit);
        var o = await db.OrganisationAsync(ct);
        var werte = new Dictionary<string, string?>
        {
            ["{Gemeinde}"] = o.GemeindeName,
            ["{Vorhaben}"] = b.Tenor,
            ["{AnhoerungBis}"] = b.AnhoerungBis is { } bis ? Anzeige.Datum(bis) : null,
        };
        var text = anhoerung
            ? Bescheidtexte.Einsetzen(Mitgliederbescheidtexte.Anhoerung, werte)
            : Bescheidtexte.Einsetzen(await parameter.WertAsync(ParameterSchluessel.MitgliederbescheidTextRechtsbehelf, heute, ct) ?? Mitgliederbescheidtexte.Rechtsbehelf, werte);
        var schreiben = new GemeindePdf.Schreiben(new AntragPdf.Kopfdaten(o.GemeindeName, o.GemeindeAnschrift, null, null, null),
            anhoerung, !anhoerung && b.ErlassenAm is null, anhoerung ? b.AnhoerungAm ?? heute : b.ErlassenAm ?? heute, b, text, o.GemeindeVertretung);
        return File(GemeindePdf.Mitgliederbescheid(schreiben), "application/pdf", $"{(anhoerung ? "Anhoerung" : "Bescheid")}-{b.Art}-{heute:yyyy-MM-dd}.pdf");
    }

    private async Task AnzeigeAsync(CancellationToken ct) =>
        Mitgliederauswahl = await Auswahl.MitgliederAsync(db, Anzeige.Heute(zeit), Bescheid?.MitgliedId, ct);

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
