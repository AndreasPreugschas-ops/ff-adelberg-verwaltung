using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Haushalt;
using Fevad.Infrastructure.Finanzen;
using Fevad.Infrastructure.Haushalt;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Haushalt;

/// <summary>Haushaltsanmeldung eines Jahres (V29): Positionen, Vorschlagszeilen, Ablauf bis zur Rückmeldung der Gemeinde.</summary>
[Authorize(Policy = Berechtigungen.HaushaltLesen)]
public class AnmeldungModel(AppDbContext db, Haushaltsplanung planung, IAuthorizationService autorisierung, UserManager<Benutzer> benutzer, TimeProvider zeit,
    Finanzaustausch austausch, Kassenberichte berichte) : PageModel
{
    /// <summary>Die Anmeldung.</summary>
    public Haushaltsanmeldung Anmeldung { get; private set; } = null!;

    /// <summary>Wertgrenze und Steuersatz.</summary>
    public Haushaltsgrenzen Grenzen { get; private set; } = new(1000, 19);

    /// <summary>Ob der Nutzer zumelden darf.</summary>
    public bool DarfZumelden { get; private set; }

    /// <summary>Ob der Nutzer alle Bereiche bearbeiten darf.</summary>
    public bool DarfAlleBereiche { get; private set; }

    /// <summary>Ob der Nutzer freigeben darf.</summary>
    public bool DarfFreigeben { get; private set; }

    /// <summary>Was vor der Übergabe an die Kämmerei noch fehlt.</summary>
    public IReadOnlyList<string> VorUebergabe { get; private set; } = [];

    /// <summary>Zeigt die Anmeldung.</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(int jahr, CancellationToken ct) => await LadenAsync(jahr, ct) ? Page() : NotFound();

    /// <summary>Exportiert die angemeldeten Positionen für die Kämmerei nach der eingestellten Spaltenzuordnung.</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>CSV-Datei, Weiterleitung mit Fehler oder 404.</returns>
    public async Task<IActionResult> OnGetExportAsync(int jahr, CancellationToken ct)
    {
        if (!await LadenAsync(jahr, ct))
        {
            return NotFound();
        }

        try
        {
            var datei = Finanzexport.Erzeugen(await austausch.FormatAsync(ExportArt.Haushalt, ct), Haushaltsplanung.ExportZeilen(Anmeldung));
            return File(datei, "text/csv", $"Haushaltsanmeldung-Feuerwehr-{jahr}.csv");
        }
        catch (InvalidOperationException fehler)
        {
            return Fehler(fehler.Message);
        }
    }

    /// <summary>Erzeugt die Haushaltsanmeldung als PDF mit Vorjahresvergleich, Investitionsprogramm und Begründungen.</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>PDF oder 404.</returns>
    public async Task<IActionResult> OnGetPdfAsync(int jahr, CancellationToken ct)
    {
        if (!await LadenAsync(jahr, ct))
        {
            return NotFound();
        }

        var vorjahr = await db.Haushaltsanmeldungen.AsNoTracking().Include(a => a.Positionen).ThenInclude(p => p.Haushaltsstelle)
            .FirstOrDefaultAsync(a => a.Jahr == jahr - 1, ct);
        var pdf = HaushaltPdf.Erzeugen(new HaushaltPdf.Daten(await berichte.KopfAsync(ct), Anmeldung, vorjahr, Anzeige.DatumZeit(zeit.GetUtcNow())));
        return File(pdf, "application/pdf", $"Haushaltsanmeldung-Feuerwehr-{jahr}.pdf");
    }

    /// <summary>Ergänzt fehlende Vorschlagszeilen aus den Daten von FEVAD.</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostVorschlaegeAsync(int jahr, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.HaushaltAlleBereiche))
        {
            return Forbid();
        }

        if (!await LadenAsync(jahr, ct, verfolgen: true))
        {
            return NotFound();
        }

        if (!Anmeldung.Offen)
        {
            return Fehler("Die Anmeldung ist schon bei der Kämmerei.");
        }

        var neu = await planung.VorschlaegeAsync(Anmeldung, ct);
        foreach (var position in neu)
        {
            position.Haushaltsstelle = null;
            db.Haushaltspositionen.Add(position);
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = neu.Count == 0 ? "Keine neuen Vorschläge." : $"{neu.Count} Vorschläge ergänzt; bitte bestätigen oder streichen.";
        return RedirectToPage(new { jahr });
    }

    /// <summary>Bestätigt, streicht oder löscht eine Position.</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="positionId">Position.</param>
    /// <param name="aktion">„bestaetigen“, „streichen“ oder „loeschen“.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostPositionAsync(int jahr, Guid positionId, string aktion, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.HaushaltZumelden))
        {
            return Forbid();
        }

        if (!await LadenAsync(jahr, ct, verfolgen: true) || Anmeldung.Positionen.FirstOrDefault(p => p.Id == positionId) is not { } position)
        {
            return NotFound();
        }

        if (!DarfAlleBereiche && position.Bereich != Haushaltsbereich.Jugend)
        {
            return Forbid();
        }

        if (!Anmeldung.Offen)
        {
            return Fehler("Die Anmeldung ist schon bei der Kämmerei.");
        }

        switch (aktion)
        {
            case "bestaetigen":
                position.Status = PositionStatus.Angemeldet;
                break;
            case "streichen":
                position.Status = PositionStatus.Gestrichen;
                break;
            case "loeschen" when position.Herkunft is null:
                db.Haushaltspositionen.Remove(position);
                break;
            default:
                return BadRequest();
        }

        await db.SaveChangesAsync(ct);
        return RedirectToPage(null, null, new { jahr }, $"p-{positionId}");
    }

    /// <summary>Nächster Schritt im Ablauf (Kommandant oder Stellvertreter).</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="ziel">Neuer Stand.</param>
    /// <param name="datum">Datum des Schritts.</param>
    /// <param name="beschluss">Verweis auf den Ausschussbeschluss (beim Schritt „beraten“).</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, Seite mit Fehler, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostStatusAsync(int jahr, AnmeldungStatus ziel, DateOnly? datum, string? beschluss, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.HaushaltFreigeben))
        {
            return Forbid();
        }

        if (!await LadenAsync(jahr, ct, verfolgen: true))
        {
            return NotFound();
        }

        if (!Haushaltsanmeldung.Folgestatus(Anmeldung.Status).Contains(ziel))
        {
            return Fehler($"Von „{Anzeige.Text(Anmeldung.Status)}“ geht es nicht nach „{Anzeige.Text(ziel)}“.");
        }

        var tag = datum ?? Anzeige.Heute(zeit);
        switch (ziel)
        {
            case AnmeldungStatus.Beraten:
                if (string.IsNullOrWhiteSpace(beschluss))
                {
                    return Fehler("Bitte den Ausschussbeschluss angeben (Sitzung, TOP).");
                }

                Anmeldung.BeratenAm = tag;
                Anmeldung.Ausschussbeschluss = beschluss.Trim()[..Math.Min(beschluss.Trim().Length, 200)];
                break;
            case AnmeldungStatus.Eingereicht:
                if (VorUebergabe.Count > 0)
                {
                    return Fehler("Vor der Übergabe fehlt noch: " + string.Join("; ", VorUebergabe));
                }

                Anmeldung.FreigegebenAm = Anzeige.Heute(zeit);
                Anmeldung.FreigegebenVon = (await benutzer.GetUserAsync(User))?.Id;
                Anmeldung.EingereichtAm = tag;
                break;
            case AnmeldungStatus.Beschlossen:
                Anmeldung.HaushaltBeschlossenAm = tag;
                break;
        }

        Anmeldung.Status = ziel;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Haushaltsanmeldung {jahr}: {Anzeige.Text(ziel)}.";
        return RedirectToPage(new { jahr });
    }

    /// <summary>Trägt die Rückmeldung der Gemeinde zu einer Position ein.</summary>
    /// <param name="jahr">Haushaltsjahr.</param>
    /// <param name="positionId">Position.</param>
    /// <param name="ergebnis">Ergebnis.</param>
    /// <param name="betrag">Beschlossener Betrag (Text mit Komma); ohne Angabe der angemeldete bzw. 0.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostRueckmeldungAsync(int jahr, Guid positionId, Haushaltsergebnis ergebnis, string? betrag, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.HaushaltFreigeben))
        {
            return Forbid();
        }

        if (!await LadenAsync(jahr, ct, verfolgen: true) || Anmeldung.Positionen.FirstOrDefault(p => p.Id == positionId && p.Status == PositionStatus.Angemeldet) is not { } position)
        {
            return NotFound();
        }

        if (Anmeldung.Offen)
        {
            return Fehler("Rückmeldungen gibt es erst nach der Übergabe an die Kämmerei.");
        }

        var wert = Fevad.Domain.Entschaedigung.Entschaedigungssaetze.Lesen(betrag);
        position.Ergebnis = ergebnis;
        position.BeschlossenerBetrag = ergebnis switch
        {
            Haushaltsergebnis.Beschlossen => wert ?? position.BetragPlanjahr,
            Haushaltsergebnis.Gekuerzt => wert,
            _ => 0,
        };
        if (ergebnis == Haushaltsergebnis.Gekuerzt && (wert is null || wert >= position.BetragPlanjahr))
        {
            return Fehler("Bei „gekürzt“ bitte den beschlossenen, niedrigeren Betrag angeben.");
        }

        position.RueckmeldungAm = Anzeige.Heute(zeit);
        await db.SaveChangesAsync(ct);
        return RedirectToPage(null, null, new { jahr }, $"p-{positionId}");
    }

    private IActionResult Fehler(string meldung)
    {
        TempData["Fehler"] = meldung;
        return RedirectToPage(new { jahr = Anmeldung.Jahr });
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private async Task<bool> LadenAsync(int jahr, CancellationToken ct, bool verfolgen = false)
    {
        var abfrage = db.Haushaltsanmeldungen.Include(a => a.Positionen).ThenInclude(p => p.Haushaltsstelle);
        var anmeldung = await (verfolgen ? abfrage : abfrage.AsNoTracking()).FirstOrDefaultAsync(a => a.Jahr == jahr, ct);
        if (anmeldung is null)
        {
            return false;
        }

        Anmeldung = anmeldung;
        Grenzen = await planung.GrenzenAsync(new DateOnly(jahr - 1, 9, 1), ct);
        DarfZumelden = await DarfAsync(Berechtigungen.HaushaltZumelden);
        DarfAlleBereiche = await DarfAsync(Berechtigungen.HaushaltAlleBereiche);
        DarfFreigeben = await DarfAsync(Berechtigungen.HaushaltFreigeben);
        var fehlt = new List<string>();
        var vorschlaege = anmeldung.Positionen.Count(p => p.Status == PositionStatus.Vorschlag);
        if (vorschlaege > 0)
        {
            fehlt.Add($"{vorschlaege} Vorschläge bestätigen oder streichen");
        }

        foreach (var p in anmeldung.Angemeldet)
        {
            if (p.Fehlt(Grenzen.GrenzeNetto, Grenzen.SteuersatzProzent) is { Count: > 0 } liste)
            {
                fehlt.Add($"„{p.Bezeichnung}“: {string.Join(", ", liste)}");
            }
        }

        if (anmeldung.BeratenAm is null)
        {
            fehlt.Add("Beratung im Feuerwehrausschuss");
        }

        VorUebergabe = fehlt;
        return true;
    }
}
