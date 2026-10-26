using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Finanzen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Finanzen;

/// <summary>
/// Datenaustausch mit der Kämmerei (V23, V26): Exporte je Art nach der eingestellten Spaltenzuordnung, Zahlungsstatus der Kostenersatz-Forderungen
/// ohne Schuldnernamen und Einspielen der Rückmeldung der Gemeindekasse. Jede Exportart verlangt das Leserecht ihres Bereichs.
/// </summary>
[Authorize]
public class IndexModel(AppDbContext db, Finanzaustausch austausch, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Recht, das eine Exportart verlangt.</summary>
    public static readonly IReadOnlyDictionary<ExportArt, string> Rechte = new Dictionary<ExportArt, string>
    {
        [ExportArt.KostenersatzSollstellung] = Berechtigungen.KostenersatzForderungen,
        [ExportArt.Entschaedigung] = Berechtigungen.EntschaedigungLesen,
        [ExportArt.Kameradschaftskasse] = Berechtigungen.KasseLesen,
        [ExportArt.Inventar] = Berechtigungen.TechnikLesen,
        [ExportArt.Rueckmeldung] = Berechtigungen.KostenersatzForderungen,
    };

    /// <summary>Eine Forderung ohne Schuldnernamen.</summary>
    /// <param name="Bescheid">Der Bescheid mit Schritten.</param>
    /// <param name="Einsatznummer">Einsatznummer.</param>
    public sealed record Forderung(Kostenbescheid Bescheid, string Einsatznummer);

    /// <summary>Gewähltes Jahr.</summary>
    public int Jahr { get; private set; }

    /// <summary>Erlaubte Exportarten.</summary>
    public IReadOnlyList<ExportArt> Erlaubt { get; private set; } = [];

    /// <summary>Darf Spaltenzuordnungen einrichten.</summary>
    public bool DarfEinrichten { get; private set; }

    /// <summary>Abgeschlossene Abrechnungen der Entschädigung.</summary>
    public IReadOnlyList<Abrechnung> Abrechnungen { get; private set; } = [];

    /// <summary>Forderungen zu Einsätzen des Jahres und alle noch offenen.</summary>
    public IReadOnlyList<Forderung> Forderungen { get; private set; } = [];

    /// <summary>Datei der Rückmeldung.</summary>
    [BindProperty] public IFormFile? Datei { get; set; }

    /// <summary>Zeigt die Seite.</summary>
    /// <param name="jahr">Jahr; ohne Angabe das laufende.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 403.</returns>
    public async Task<IActionResult> OnGetAsync(int? jahr, CancellationToken ct)
    {
        await RechteLadenAsync();
        if (Erlaubt.Count == 0 && !DarfEinrichten)
        {
            return Forbid();
        }

        Jahr = jahr ?? Anzeige.Heute(zeit).Year;
        if (Erlaubt.Contains(ExportArt.Entschaedigung))
        {
            Abrechnungen = await db.Abrechnungen.AsNoTracking().Where(a => a.Status == AbrechnungStatus.Abgeschlossen).OrderByDescending(a => a.Bis).Take(20).ToListAsync(ct);
        }

        if (Erlaubt.Contains(ExportArt.KostenersatzSollstellung))
        {
            var (von, bis) = Anzeige.Jahr(Jahr);
            var bescheide = await db.Kostenbescheide.AsNoTracking().Include(b => b.Ereignisse).Include(b => b.Kostenersatzvorgang).ThenInclude(v => v!.Einsatz)
                .AsSplitQuery().ToListAsync(ct);
            Forderungen = bescheide
                .Where(b => b.Betrag is not null && ((b.Kostenersatzvorgang!.Einsatz!.Alarmiert >= von && b.Kostenersatzvorgang.Einsatz.Alarmiert < bis) || !b.Erledigt))
                .Select(b => new Forderung(b, b.Kostenersatzvorgang!.Einsatz!.Einsatznummer))
                .OrderBy(f => f.Bescheid.Erledigt).ThenBy(f => f.Bescheid.FaelligAm ?? DateOnly.MaxValue).ToList();
        }

        return Page();
    }

    /// <summary>Erzeugt einen Export.</summary>
    /// <param name="art">Exportart.</param>
    /// <param name="jahr">Jahr für Sollstellungen und Kassen.</param>
    /// <param name="abrechnungId">Abrechnung für die Entschädigung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Datei, eine Weiterleitung mit Fehler oder 403.</returns>
    public async Task<IActionResult> OnGetExportAsync(ExportArt art, int? jahr, Guid? abrechnungId, CancellationToken ct)
    {
        if (art == ExportArt.Rueckmeldung || !await DarfAsync(Rechte[art]))
        {
            return Forbid();
        }

        var j = jahr ?? Anzeige.Heute(zeit).Year;
        if (art == ExportArt.Entschaedigung && (abrechnungId is null
            || !await db.Abrechnungen.AnyAsync(a => a.Id == abrechnungId && a.Status == AbrechnungStatus.Abgeschlossen, ct)))
        {
            TempData["Fehler"] = "Bitte eine abgeschlossene Abrechnung wählen.";
            return RedirectToPage(new { jahr = j });
        }

        var zeilen = art switch
        {
            ExportArt.KostenersatzSollstellung => await austausch.SollstellungenAsync(j, ct),
            ExportArt.Entschaedigung => await austausch.EntschaedigungAsync(abrechnungId!.Value, ct),
            ExportArt.Kameradschaftskasse => await austausch.KassenAsync(j, await DarfAsync(Berechtigungen.JugendZugang), ct),
            _ => await austausch.InventarAsync(ct),
        };
        try
        {
            var datei = Finanzexport.Erzeugen(await austausch.FormatAsync(art, ct), zeilen);
            var name = art switch
            {
                ExportArt.KostenersatzSollstellung => $"Sollstellungen-Kostenersatz-{j}",
                ExportArt.Entschaedigung => "Entschaedigung-Kaemmerei",
                ExportArt.Kameradschaftskasse => $"Kassen-Steuergrundlagen-{j}",
                _ => $"Inventar-{Anzeige.Heute(zeit):yyyy-MM-dd}",
            };
            return File(datei, "text/csv", name + ".csv");
        }
        catch (InvalidOperationException fehler)
        {
            TempData["Fehler"] = fehler.Message;
            return RedirectToPage(new { jahr = j });
        }
    }

    /// <summary>Spielt die Rückmeldung der Gemeindekasse ein.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Ergebnis oder 403.</returns>
    public async Task<IActionResult> OnPostRueckmeldungAsync(CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.KostenersatzForderungen))
        {
            return Forbid();
        }

        if (Datei is not { Length: > 0 and < 2_000_000 })
        {
            TempData["Fehler"] = "Bitte eine Datei bis 2 MB wählen.";
            return RedirectToPage();
        }

        string inhalt;
        await using (var strom = Datei.OpenReadStream())
        {
            using var leser = new StreamReader(strom, detectEncodingFromByteOrderMarks: true);
            inhalt = await leser.ReadToEndAsync(ct);
        }

        var e = await austausch.RueckmeldungEinspielenAsync(inhalt, User.Identity?.Name, ct);
        TempData["Meldung"] = $"Rückmeldung eingespielt: {e.Zahlungen} Zahlungen, {e.Mahnungen} Mahnungen, {e.Unveraendert} unverändert.";
        if (e.Fehler.Count > 0)
        {
            TempData["Fehler"] = string.Join(" ", e.Fehler.Take(10)) + (e.Fehler.Count > 10 ? $" (und {e.Fehler.Count - 10} weitere)" : "");
        }

        return RedirectToPage();
    }

    private async Task RechteLadenAsync()
    {
        var erlaubt = new List<ExportArt>();
        foreach (var (art, recht) in Rechte)
        {
            if (art != ExportArt.Rueckmeldung && await DarfAsync(recht))
            {
                erlaubt.Add(art);
            }
        }

        Erlaubt = erlaubt;
        DarfEinrichten = await DarfAsync(Berechtigungen.FinanzexportEinrichten);
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;
}
