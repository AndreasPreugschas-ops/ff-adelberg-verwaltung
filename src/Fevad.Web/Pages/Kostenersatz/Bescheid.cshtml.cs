using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Einsaetze;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Kostenersatz;

/// <summary>
/// Kostenersatzbescheid an einen Schuldner (V22): Statuskette von der Anhörung bis zur Klage mit Datum, Bearbeiter und Vermerk je Schritt,
/// Anhörung und Bescheid als PDF aus den Textbausteinen des Hauptamts. Lesen dürfen alle mit Kostenersatz-Recht, erfassen nur das Hauptamt.
/// Sind alle Bescheide eines Vorgangs erledigt, gilt der Kostenersatz des Einsatzes als abgeschlossen (Beginn der Löschfrist L3).
/// </summary>
[Authorize(Policy = Berechtigungen.KostenersatzLesen)]
public class BescheidModel(AppDbContext db, Kostenersatzrechner rechner, ParameterLeser parameter, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Der Bescheid.</summary>
    public Kostenbescheid Bescheid { get; private set; } = null!;

    /// <summary>Der Vorgang.</summary>
    public Kostenersatzvorgang Vorgang { get; private set; } = null!;

    /// <summary>Der Einsatz.</summary>
    public Einsatz Einsatz { get; private set; } = null!;

    /// <summary>Berechnung des Vorgangs.</summary>
    public Kostenersatzrechner.Ergebnis Berechnung { get; private set; } = null!;

    /// <summary>Die Aufstellung enthält vorläufige Sätze oder fehlt; Festsetzen ist gesperrt.</summary>
    public bool Vorlaeufig => Berechnung.Aufstellung is not { } a || a.Hinweise.Contains(Domain.Einsaetze.Kostenersatz.PlatzhalterHinweis);

    /// <summary>Betrag aus der Berechnung für diesen Schuldner (anteilig ohne Gesamtschuld).</summary>
    public decimal Vorschlag { get; private set; }

    /// <summary>Darf Schritte erfassen (Hauptamt).</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Jahre der Zahlungsverjährung.</summary>
    public int VerjaehrungJahre { get; private set; }

    /// <summary>Auswahl der Schritte.</summary>
    public static IReadOnlyList<SelectListItem> Schritte { get; } =
        Enum.GetValues<BescheidSchritt>().Select(s => new SelectListItem(Anzeige.Text(s), s.ToString())).ToList();

    /// <summary>Neuer Schritt.</summary>
    [BindProperty] public SchrittEingabe Schritt { get; set; } = new();

    /// <summary>Kassenzeichen.</summary>
    [BindProperty, StringLength(50), Display(Name = "Kassenzeichen")] public string? Kassenzeichen { get; set; }

    /// <summary>Eingaben eines Schritts.</summary>
    public sealed class SchrittEingabe
    {
        /// <summary>Schritt.</summary>
        [Display(Name = "Schritt")] public BescheidSchritt Schritt { get; set; }
        /// <summary>Datum; bei der Bekanntgabe der Tag der Aufgabe zur Post bzw. der Übergabe.</summary>
        [Required(ErrorMessage = "Datum fehlt."), Display(Name = "Datum (Bekanntgabe: Tag der Aufgabe zur Post)")] public DateOnly? Datum { get; set; }
        /// <summary>Betrag als Text.</summary>
        [Display(Name = "Betrag (€; Festsetzung leer = Berechnung)")] public string? Betrag { get; set; }
        /// <summary>Vermerk.</summary>
        [StringLength(1000), Display(Name = "Vermerk (Aktenzeichen, Zahlungsweg, Gründe; keine Gesundheitsangaben)")] public string? Vermerk { get; set; }
    }

    /// <summary>Zeigt den Bescheid.</summary>
    /// <param name="id">Kennung des Bescheids.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        Schritt.Datum = Anzeige.Heute(zeit);
        Kassenzeichen = Bescheid.Kassenzeichen;
        return Page();
    }

    /// <summary>Speichert das Kassenzeichen der Gemeindekasse.</summary>
    /// <param name="id">Kennung des Bescheids.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostKassenzeichenAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        Bescheid.Kassenzeichen = string.IsNullOrWhiteSpace(Kassenzeichen) ? null : Kassenzeichen.Trim()[..Math.Min(50, Kassenzeichen.Trim().Length)];
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Erfasst einen Schritt. Die Anhörung setzt die Frist, die Bekanntgabe den Zugangstag (Zugangsfiktion beim Brief) und die Fälligkeit.
    /// Die Festsetzung ist gesperrt, solange die Berechnung vorläufige Sätze enthält.
    /// </summary>
    /// <param name="id">Kennung des Bescheids.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, die Seite mit Fehlern, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostSchrittAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        ModelState.Clear();
        TryValidateModel(Schritt, nameof(Schritt));
        decimal? betrag = null;
        if (!string.IsNullOrWhiteSpace(Schritt.Betrag))
        {
            betrag = Entschaedigungssaetze.Lesen(Schritt.Betrag);
            if (betrag is null)
            {
                ModelState.AddModelError("Schritt.Betrag", "Bitte eine Zahl eingeben, z. B. 120 oder 12,50.");
            }
        }
        else if (Schritt.Schritt == BescheidSchritt.Festsetzung)
        {
            betrag = Vorschlag;
        }

        if (ModelState.IsValid && Bescheid.Pruefen(Schritt.Schritt, betrag, Vorlaeufig) is { } fehler)
        {
            ModelState.AddModelError(string.Empty, fehler);
        }

        if (!ModelState.IsValid)
        {
            Kassenzeichen = Bescheid.Kassenzeichen;
            return Page();
        }

        var datum = Schritt.Datum!.Value;
        switch (Schritt.Schritt)
        {
            case BescheidSchritt.Anhoerung:
                Bescheid.AnhoerungBis = datum.AddDays(await parameter.ZahlAsync(ParameterSchluessel.KostenersatzAnhoerungFristTage, datum, 14, ct));
                break;
            case BescheidSchritt.Bekanntgabe:
                var fiktion = await parameter.ZahlAsync(ParameterSchluessel.KostenersatzZugangsfiktionTage, datum, 4, ct);
                Bescheid.BekanntgegebenAm = Kostenbescheid.Bekanntgabe(datum, Bescheid.Kostenschuldner?.Zustellweg ?? Zustellweg.Brief, fiktion);
                Bescheid.FaelligAm = Bescheid.BekanntgegebenAm.Value.AddMonths(
                    await parameter.ZahlAsync(ParameterSchluessel.KostenersatzFaelligkeitMonate, datum, 1, ct));
                break;
        }

        var ereignis = new BescheidEreignis
        {
            KostenbescheidId = Bescheid.Id, Schritt = Schritt.Schritt, Datum = datum,
            Betrag = Schritt.Schritt is BescheidSchritt.Festsetzung or BescheidSchritt.Abhilfe or BescheidSchritt.KlageErledigt or BescheidSchritt.Zahlung ? betrag : null,
            Vermerk = string.IsNullOrWhiteSpace(Schritt.Vermerk) ? null : Schritt.Vermerk.Trim(), Bearbeiter = User.Identity?.Name, ErfasstAm = zeit.GetUtcNow(),
        };
        db.BescheidEreignisse.Add(ereignis);
        Bescheid.Ereignisse.Add(ereignis);
        AbschlussSetzen();
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"{Anzeige.Text(Schritt.Schritt)} erfasst.";
        return RedirectToPage(new { id });
    }

    /// <summary>Nimmt den zuletzt erfassten Schritt zurück, etwa nach einem Tippfehler.</summary>
    /// <param name="id">Kennung des Bescheids.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostZuruecknehmenAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        if (Bescheid.Ereignisse.MaxBy(e => e.ErfasstAm) is { } letzter)
        {
            Bescheid.Ereignisse.Remove(letzter);
            db.BescheidEreignisse.Remove(letzter);
            if (letzter.Schritt == BescheidSchritt.Anhoerung)
            {
                Bescheid.AnhoerungBis = null;
            }
            else if (letzter.Schritt == BescheidSchritt.Bekanntgabe)
            {
                Bescheid.BekanntgegebenAm = null;
                Bescheid.FaelligAm = null;
            }

            AbschlussSetzen();
            await db.SaveChangesAsync(ct);
        }

        return RedirectToPage(new { id });
    }

    /// <summary>Anhörung oder Bescheid als PDF; vor der Festsetzung trägt der Bescheid den Vermerk „Entwurf“.</summary>
    /// <param name="id">Kennung des Bescheids.</param>
    /// <param name="anhoerung">true: Anhörungsschreiben.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das PDF, eine Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(Guid id, bool anhoerung, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (Berechnung.Aufstellung is not { } kosten)
        {
            TempData["Fehler"] = "Für den Kostenersatz sind noch keine Kostensätze hinterlegt.";
            return RedirectToPage(new { id });
        }

        var heute = Anzeige.Heute(zeit);
        var betrag = Bescheid.Betrag ?? Vorschlag;
        var o = await db.OrganisationAsync(ct);
        var werte = new Dictionary<string, string?>
        {
            ["{Gemeinde}"] = o.GemeindeName,
            ["{Einsatznummer}"] = Einsatz.Einsatznummer,
            ["{Einsatzdatum}"] = Anzeige.Datum(Anzeige.LokalesDatum(Einsatz.Alarmiert)),
            ["{Tatbestand}"] = Vorgang.Tatbestand is { } t ? Anzeige.Text(t) : null,
            ["{Betrag}"] = Anzeige.Euro(betrag),
            ["{AnhoerungBis}"] = Bescheid.AnhoerungBis is { } bis ? Anzeige.Datum(bis) : null,
            ["{FaelligAm}"] = Bescheid.FaelligAm is { } f ? Anzeige.Datum(f) : "einen Monat nach Bekanntgabe",
            ["{Kassenzeichen}"] = Bescheid.Kassenzeichen,
        };
        async Task<string> Text(string schluessel, string muster) =>
            Bescheidtexte.Einsetzen(await parameter.WertAsync(schluessel, heute, ct) ?? muster, werte);

        var schuldner = Bescheid.Kostenschuldner;
        var daten = new KostenbescheidPdf.Daten(
            new AntragPdf.Kopfdaten(o.GemeindeName, o.GemeindeAnschrift, null, null, null),
            anhoerung, !anhoerung && !Bescheid.Ereignisse.Any(e => e.Schritt == BescheidSchritt.Festsetzung), heute,
            schuldner?.Name ?? "[Schuldner gelöscht]", schuldner?.Anschrift, Bescheid.Kassenzeichen,
            $"Kostenersatz für den Feuerwehreinsatz {Einsatz.Einsatznummer} am {werte["{Einsatzdatum}"]}", betrag,
            anhoerung
                ? await Text(ParameterSchluessel.KostenersatzTextAnhoerung, Bescheidtexte.Anhoerung)
                : await Text(ParameterSchluessel.KostenersatzTextBegruendung, Bescheidtexte.Begruendung),
            anhoerung ? null : await Text(ParameterSchluessel.KostenersatzTextZahlung, Bescheidtexte.Zahlung),
            anhoerung ? null : await Text(ParameterSchluessel.KostenersatzTextRechtsbehelf, Bescheidtexte.Rechtsbehelf),
            Vorgang.Gesamtschuld && Vorgang.Schuldner.Count > 1 ? "Mehrere Kostenschuldner haften als Gesamtschuldner (§ 34 Abs. 4 FwG); der Betrag ist insgesamt nur einmal zu zahlen." : null,
            kosten);
        return File(KostenbescheidPdf.Erzeugen(daten), "application/pdf",
            KostenbescheidPdf.Dateiname(anhoerung, Einsatz.Einsatznummer, schuldner?.Name ?? "Schuldner"));
    }

    // Der Kostenersatz des Einsatzes ist abgeschlossen, wenn jeder Bescheid erledigt ist.
    private void AbschlussSetzen()
    {
        var erledigt = Vorgang.Bescheide.All(b => b.Erledigt);
        Einsatz.KostenersatzOffen = !erledigt;
        Einsatz.KostenersatzAbgeschlossenAm = erledigt ? Anzeige.Heute(zeit) : null;
    }

    private async Task<bool> LadenAsync(Guid id, CancellationToken ct)
    {
        var bescheid = await db.Kostenbescheide.Include(b => b.Ereignisse).Include(b => b.Kostenschuldner).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bescheid is null)
        {
            return false;
        }

        Bescheid = bescheid;
        Vorgang = await db.Kostenersatzvorgaenge.Include(v => v.Schuldner).Include(v => v.Zusatzkosten).Include(v => v.Bescheide).ThenInclude(b => b.Ereignisse)
            .AsSplitQuery().SingleAsync(v => v.Id == bescheid.KostenersatzvorgangId, ct);
        Einsatz = await db.Einsaetze
            .Include(e => e.Fahrzeuge).Include(e => e.Teilnahmen).Include(e => e.Ausruestung).ThenInclude(a => a.Kostensatz)
            .AsSplitQuery().SingleAsync(e => e.Id == Vorgang.EinsatzId, ct);
        Berechnung = await rechner.BerechnenAsync(Einsatz, Vorgang, ct);
        var summe = Berechnung.Aufstellung?.Summe ?? 0;
        Vorschlag = !Vorgang.Gesamtschuld && bescheid.Kostenschuldner?.AnteilProzent is { } anteil ? Math.Round(summe * anteil / 100, 2) : summe;
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.KostenersatzEntscheiden)).Succeeded;
        VerjaehrungJahre = await parameter.ZahlAsync(ParameterSchluessel.KostenersatzVerjaehrungJahre, Anzeige.Heute(zeit), 5, ct);
        return true;
    }
}
