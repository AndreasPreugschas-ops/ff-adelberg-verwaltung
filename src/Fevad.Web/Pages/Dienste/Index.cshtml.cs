using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dienste;

/// <summary>Dienstplan: Übungen und Dienste planen (einmalig oder als Serie) und kommende sowie vergangene Dienste.</summary>
public class IndexModel(AppDbContext db, Dienstplanung planung, TimeProvider zeit, Datenbereich bereich, IAuthorizationService autorisierung) : PageModel
{
    /// <summary>Der Benutzer darf Dienste planen.</summary>
    public bool DarfPlanen { get; private set; }
    /// <summary>Der Benutzer darf die Einstellungen des Dienstplans ändern.</summary>
    public bool DarfEinstellen { get; private set; }

    /// <summary>Kommende Dienste, früheste zuerst.</summary>
    public IReadOnlyList<Zeile> Kommende { get; private set; } = [];
    /// <summary>Vergangene Dienste, neueste zuerst.</summary>
    public IReadOnlyList<Zeile> Vergangene { get; private set; } = [];
    /// <summary>Auswahllisten für Zielgruppe und Übungsleiter.</summary>
    public Zielgruppenlisten Listen { get; private set; } = null!;

    /// <summary>Formulardaten für einen neuen Dienst.</summary>
    [BindProperty]
    public Planungsformular Neu { get; set; } = new();

    /// <summary>Wiederholung für eine Serie; <c>null</c> plant einen Einzeltermin.</summary>
    [BindProperty]
    public Wiederholung? Wiederholung { get; set; }

    /// <summary>Letzter Tag der Serie.</summary>
    [BindProperty]
    public DateOnly? WiederholenBis { get; set; }

    /// <summary>Auch Termine zeigen, die nicht als Dienst geplant sind.</summary>
    [BindProperty(SupportsGet = true)]
    public bool AlleTermine { get; set; }

    /// <param name="Bearbeitbar">Ein Klick öffnet direkt das Bearbeiten statt der Terminansicht.</param>
    /// <param name="Dienst">Der Dienst.</param>
    /// <param name="Anwesend">Anzahl der Anwesenden.</param>
    /// <param name="Zusagen">Anzahl der Zusagen.</param>
    /// <param name="Absagen">Anzahl der Absagen.</param>
    public sealed record Zeile(Dienst Dienst, int Anwesend, int Zusagen, int Absagen, bool Bearbeitbar = false);

    /// <summary>Lädt kommende und vergangene Dienste.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        // Wer nur für einzelne Abteilungen plant (z. B. der Jugendwart), bekommt die erste davon als Zielgruppe vorbelegt.
        await ZielgruppeVorbelegenAsync(ct);

        await LadenAsync(ct);
    }

    /// <summary>Plant einen neuen Dienst oder eine Serie.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung, die Seite mit Fehlern oder 403.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await ZielgruppeVorbelegenAsync(ct);

        if (!await bereich.DarfDienstBearbeitenAsync(User, Neu.AbteilungIds, ct))
        {
            return Forbid();
        }

        Neu.ZeitenLesen(ModelState, nameof(Neu), out var beginn, out var ende);
        if (Wiederholung is not null)
        {
            if (WiederholenBis is null)
            {
                ModelState.AddModelError(nameof(WiederholenBis), "Bis wann soll wiederholt werden?");
            }
            else if (Neu.Datum is { } datum && WiederholenBis < datum)
            {
                ModelState.AddModelError(nameof(WiederholenBis), "Das Enddatum liegt vor dem ersten Termin.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var dienst = new Dienst { Titel = "", Status = DienstStatus.Geplant };
        await Neu.UebertragenAsync(db, dienst, beginn, ende, ct);

        if (Wiederholung is { } rhythmus)
        {
            var spaetestens = await planung.SerieSpaetestensAsync(Neu.Datum!.Value, ct);
            var termine = await planung.SerieAnlegenAsync(dienst, rhythmus, WiederholenBis!.Value, ct);
            TempData["Meldung"] = WiederholenBis > spaetestens
                ? $"{termine.Count} Termine angelegt, bis {Anzeige.Datum(spaetestens)} (längere Serien bitte später verlängern)."
                : $"{termine.Count} Termine angelegt.";
            return RedirectToPage();
        }

        db.Dienste.Add(dienst);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Dienst angelegt.";
        return RedirectToPage("/Termine/Termin", new { id = dienst.Id });
    }

    private async Task ZielgruppeVorbelegenAsync(CancellationToken ct)
    {
        if (await bereich.DienstabteilungenAsync(User) is { Count: > 0 } erlaubt
            && (Neu.AbteilungIds.Count == 0 || !Neu.AbteilungIds.All(erlaubt.Contains)))
        {
            Neu.AbteilungIds = [await bereich.ErsteAbteilungAsync(erlaubt, ct)];
        }
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        var freigabe = await bereich.FreigabeAsync(User, Berechtigungen.DiensteBearbeiten);
        DarfPlanen = freigabe.Irgendwo;
        DarfEinstellen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.DiensteEinstellungen)).Succeeded;
        Listen = await Zielgruppenlisten.LadenAsync(db, Anzeige.Heute(zeit), ct);
        var basis = db.Dienste.AsNoTracking().MitZielgruppe().Where(d => AlleTermine || d.AlsDienst);

        // Heute laufende Dienste zählen noch als kommend, damit die Anwesenheit leicht zu finden ist.
        var heute = Anzeige.AlsUtc(Anzeige.Heute(zeit), TimeOnly.MinValue);
        Kommende = await Zeilen(basis.Where(d => d.Beginn >= heute).OrderBy(d => d.Beginn).Take(50), freigabe, ct);
        Vergangene = await Zeilen(basis.Where(d => d.Beginn < heute).OrderByDescending(d => d.Beginn).Take(50), freigabe, ct);
    }

    private static async Task<List<Zeile>> Zeilen(IQueryable<Dienst> abfrage, Freigabe freigabe, CancellationToken ct) =>
        (await abfrage.Select(d => new Zeile(d,
                d.Anwesenheiten.Count(a => a.Status == AnwesenheitStatus.Anwesend),
                d.Rueckmeldungen.Count(r => r.Antwort == RueckmeldungAntwort.Zusage),
                d.Rueckmeldungen.Count(r => r.Antwort == RueckmeldungAntwort.Absage),
                false))
            .ToListAsync(ct))
            .Select(z => z with { Bearbeitbar = Datenbereich.Umfasst(freigabe, z.Dienst.AbteilungIds) })
            .ToList();
}
