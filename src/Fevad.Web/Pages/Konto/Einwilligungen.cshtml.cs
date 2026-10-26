using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Konto;

/// <summary>
/// Einwilligungen selbst erteilen und widerrufen (V39, V45): Volljährige für sich, Eltern über das Elternkonto für ihr Kind.
/// Minderjährige können selbst widerrufen und ab dem Mitentscheidungsalter der Erklärung ihrer Eltern zustimmen.
/// </summary>
public class EinwilligungenModel(
    AppDbContext db, Dienstplanung planung, Einwilligungsregister register, UserManager<Benutzer> benutzer, IAktuellerBenutzer aktuell, TimeProvider zeit) : PageModel
{
    /// <summary>Eine Person mit dem Stand ihrer Einwilligungen.</summary>
    /// <param name="MitgliedId">Kennung des Mitglieds.</param>
    /// <param name="Vorname">Vorname.</param>
    /// <param name="Selbst">Das eigene Mitglied, nicht ein Kind.</param>
    /// <param name="DarfErteilen">Darf im Konto erteilen; sonst nur widerrufen bzw. zustimmen.</param>
    /// <param name="Mitentscheidung">Minderjährig im Mitentscheidungsalter.</param>
    /// <param name="Stand">Stand je Art.</param>
    public sealed record Person(Guid MitgliedId, string Vorname, bool Selbst, bool DarfErteilen, bool Mitentscheidung, IReadOnlyList<Einwilligungsregister.Stand> Stand);

    /// <summary>Personen des Kontos: das eigene Mitglied und die Kinder.</summary>
    public IReadOnlyList<Person> Personen { get; private set; } = [];

    /// <summary>Zeigt die Einwilligungen.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Personen = await LadenAsync(ct);
        return Page();
    }

    /// <summary>Erteilt eine Einwilligung oder lehnt sie ab.</summary>
    /// <param name="mitgliedId">Für wen.</param>
    /// <param name="artId">Welche Art.</param>
    /// <param name="erteilt">Eingewilligt oder abgelehnt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
    public async Task<IActionResult> OnPostErklaerenAsync(Guid mitgliedId, Guid artId, bool erteilt, CancellationToken ct)
    {
        var person = (await LadenAsync(ct)).FirstOrDefault(p => p.MitgliedId == mitgliedId);
        if (person?.Stand.FirstOrDefault(s => s.Art.Id == artId) is not { } stand)
        {
            return NotFound();
        }

        if (!person.DarfErteilen)
        {
            return Forbid();
        }

        var heute = Anzeige.Heute(zeit);
        await register.EintragenAsync(new Einwilligung
        {
            MitgliedId = mitgliedId,
            EinwilligungsartId = artId,
            Erteilt = erteilt,
            Am = heute,
            Durch = person.Selbst ? EinwilligungDurch.Mitglied : EinwilligungDurch.Erziehungsberechtigter,
            DurchName = await NameAsync(ct),
            Weg = EinwilligungWeg.Konto,
            KontoId = aktuell.Id,
        }, ct);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = erteilt
            ? $"„{stand.Art.Bezeichnung}“ für {person.Vorname} erteilt.{(person.Mitentscheidung ? $" Sie wirkt, sobald auch {person.Vorname} zustimmt." : "")}"
            : $"„{stand.Art.Bezeichnung}“ für {person.Vorname} abgelehnt.";
        return RedirectToPage();
    }

    /// <summary>Widerruft eine Einwilligung ab heute; das dürfen Eltern und das Mitglied selbst, auch minderjährig.</summary>
    /// <param name="mitgliedId">Für wen.</param>
    /// <param name="artId">Welche Art.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostWiderrufenAsync(Guid mitgliedId, Guid artId, CancellationToken ct)
    {
        if ((await LadenAsync(ct)).FirstOrDefault(p => p.MitgliedId == mitgliedId) is not { } person)
        {
            return NotFound();
        }

        var ok = await register.WiderrufenAsync(mitgliedId, artId, Anzeige.Heute(zeit), await NameAsync(ct) ?? "über das Konto", ct);
        await db.SaveChangesAsync(ct);
        TempData[ok ? "Meldung" : "Fehler"] = ok ? $"Widerruf für {person.Vorname} gespeichert; er gilt ab sofort." : "Es gab keine wirksame Einwilligung.";
        return RedirectToPage();
    }

    /// <summary>Der Jugendliche stimmt der Erklärung seiner Eltern zu.</summary>
    /// <param name="artId">Welche Art.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung oder 404.</returns>
    public async Task<IActionResult> OnPostZustimmenAsync(Guid artId, CancellationToken ct)
    {
        if ((await LadenAsync(ct)).FirstOrDefault(p => p.Selbst && p.Mitentscheidung) is not { } person)
        {
            return NotFound();
        }

        var ok = await register.ZustimmenAsync(person.MitgliedId, artId, ct);
        await db.SaveChangesAsync(ct);
        TempData[ok ? "Meldung" : "Fehler"] = ok ? "Zustimmung gespeichert." : "Es gibt keine Erklärung deiner Eltern, der du zustimmen kannst.";
        return RedirectToPage();
    }

    private async Task<string?> NameAsync(CancellationToken ct)
    {
        if (aktuell.Id is not { } id)
        {
            return null;
        }

        var mitgliedId = await planung.MitgliedIdAsync(id, ct);
        if (mitgliedId is { } m)
        {
            return await db.Mitglieder.Where(x => x.Id == m).Select(x => x.Vorname + " " + x.Nachname).FirstOrDefaultAsync(ct);
        }

        // Elternkonto: der Name steht beim Erziehungsberechtigten des Kindes.
        return await db.Erziehungsberechtigte.Where(e => e.KontoId == id).Select(e => e.Vorname + " " + e.Nachname).FirstOrDefaultAsync(ct)
            ?? (await benutzer.FindByIdAsync(id.ToString()))?.Email;
    }

    private async Task<IReadOnlyList<Person>> LadenAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var grenzen = await register.GrenzenAsync(heute, ct);
        var personen = new List<Person>();
        foreach (var p in await planung.PersonenAsync(aktuell.Id, ct))
        {
            var mitglied = await db.Mitglieder.AsNoTracking().Include(m => m.Mitgliedschaften).ThenInclude(s => s.Abteilung)
                .FirstAsync(m => m.Id == p.MitgliedId, ct);
            var alter = mitglied.AlterAm(heute);
            var minderjaehrig = alter < grenzen.VolljaehrigAb;
            var jugend = minderjaehrig || mitglied.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.Abteilung!.Art is AbteilungArt.Jugend or AbteilungArt.Kinder
                && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute));
            personen.Add(new Person(p.MitgliedId, p.Vorname, p.Selbst, Einwilligungsregeln.DarfErteilen(p.Selbst, alter, grenzen.VolljaehrigAb),
                minderjaehrig && alter >= grenzen.MitentscheidungAb, await register.StandAsync(mitglied, jugend, heute, ct)));
        }

        return personen;
    }
}
