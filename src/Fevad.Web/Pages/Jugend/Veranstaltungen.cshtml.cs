using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Jugend;

/// <summary>
/// Veranstaltungen der Jugend- und Kinderfeuerwehr eines Jahres (V48): Fahrten, Zeltlager, Ausflüge, Elternabende und Kreisveranstaltungen
/// mit Zahl der Anmeldungen, Stand der Betreuer und Zuschuss. Angelegt werden sie als Dienst der passenden Art.
/// </summary>
[Authorize(Policy = Berechtigungen.JugendVeranstaltungenSehen)]
public class VeranstaltungenModel(AppDbContext db, Datenbereich datenbereich, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Veranstaltung in der Liste.</summary>
    /// <param name="Dienst">Der Dienst.</param>
    /// <param name="Anmeldungen">Zahl der Anmeldungen.</param>
    /// <param name="Betreuer">Zahl der eingeteilten Betreuer.</param>
    /// <param name="Uebernachtung">Mit Übernachtung.</param>
    /// <param name="Zuschussgeber">Gewählter Zuschussgeber.</param>
    /// <param name="ZuschussBeantragtAm">Tag des Zuschussantrags.</param>
    public sealed record Zeile(Dienst Dienst, int Anmeldungen, int Betreuer, bool Uebernachtung, string? Zuschussgeber, DateOnly? ZuschussBeantragtAm);

    /// <summary>Jahr der Liste.</summary>
    [BindProperty(SupportsGet = true)]
    public int? Jahr { get; set; }

    /// <summary>Die Veranstaltungen.</summary>
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    /// <summary>Zeigt die Liste.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Jahr ??= Anzeige.Heute(zeit).Year;
        var von = Fevad.Domain.Gemeinsam.Ortszeit.TagesbeginnUtc(new DateOnly(Jahr.Value, 1, 1));
        var bis = Fevad.Domain.Gemeinsam.Ortszeit.TagesbeginnUtc(new DateOnly(Jahr.Value + 1, 1, 1));
        var abteilungen = await JugendabteilungenAsync(db, datenbereich, User, ct);
        var arten = Jugenddienstarten.Alle.ToList();
        var dienste = await db.Dienste.AsNoTracking().Include(d => d.Abteilungen)
            .Where(d => arten.Contains(d.Art) && d.Beginn >= von && d.Beginn < bis && d.Abteilungen.Any(a => abteilungen.Contains(a.Id)))
            .OrderBy(d => d.Beginn)
            .ToListAsync(ct);
        var ids = dienste.Select(d => d.Id).ToList();
        var angaben = await db.Jugendveranstaltungen.AsNoTracking()
            .Where(v => ids.Contains(v.DienstId))
            .Select(v => new { v.DienstId, Anmeldungen = v.Anmeldungen.Count, Betreuer = v.Betreuer.Count, v.Uebernachtung, Geber = v.Zuschussgeber!.Name, v.ZuschussBeantragtAm })
            .ToDictionaryAsync(v => v.DienstId, ct);
        Zeilen = dienste.Select(d => angaben.TryGetValue(d.Id, out var a)
                ? new Zeile(d, a.Anmeldungen, a.Betreuer, a.Uebernachtung, a.Geber, a.ZuschussBeantragtAm)
                : new Zeile(d, 0, 0, false, null, null))
            .ToList();
    }

    /// <summary>Jugend- und Kinderfeuerwehren, für die der Benutzer Dienste planen darf.</summary>
    /// <param name="db">Datenbank.</param>
    /// <param name="datenbereich">Datenbereich des Benutzers.</param>
    /// <param name="benutzer">Der Benutzer.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Schlüssel der Abteilungen.</returns>
    public static async Task<List<Guid>> JugendabteilungenAsync(AppDbContext db, Datenbereich datenbereich, System.Security.Claims.ClaimsPrincipal benutzer, CancellationToken ct)
    {
        var freigabe = await datenbereich.FreigabeAsync(benutzer, Berechtigungen.DiensteBearbeiten);
        var freigegeben = freigabe.Abteilungen.ToList();
        return await db.Abteilungen.AsNoTracking()
            .Where(a => (a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder) && (freigabe.Alle || freigegeben.Contains(a.Id)))
            .Select(a => a.Id)
            .ToListAsync(ct);
    }
}
