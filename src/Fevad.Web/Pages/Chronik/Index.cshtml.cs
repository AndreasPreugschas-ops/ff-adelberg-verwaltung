using System.Text;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Chronik;

/// <summary>Chronik der Feuerwehr je Jahr (V56, F25): intern, einzelne Einträge zur Veröffentlichung freigegeben.</summary>
public class IndexModel(AppDbContext db, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Das Jahr; ohne Angabe das laufende.</summary>
    [BindProperty(SupportsGet = true)] public int? Jahr { get; set; }

    /// <summary>Einträge des Jahres.</summary>
    public IReadOnlyList<ChronikEintrag> Eintraege { get; private set; } = [];

    /// <summary>Jahre, in denen es Einträge gibt, und das laufende.</summary>
    public IReadOnlyList<int> Jahre { get; private set; } = [];

    /// <summary>Der Benutzer darf die Chronik führen.</summary>
    public bool DarfBearbeiten { get; private set; }

    /// <summary>Der Benutzer darf die Jubiläumsvorschau sehen.</summary>
    public bool DarfJubilaeen { get; private set; }

    /// <summary>Zeigt die Einträge eines Jahres.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        Jahr ??= heute.Year;
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.ChronikBearbeiten)).Succeeded;
        DarfJubilaeen = (await autorisierung.AuthorizeAsync(User, Berechtigungen.Jubilaeen)).Succeeded;
        Eintraege = await EintraegeAsync(Jahr.Value, false, ct);
        Jahre = (await db.ChronikEintraege.AsNoTracking().Select(c => c.Datum.Year).Distinct().ToListAsync(ct))
            .Append(heute.Year).Append(Jahr.Value).Distinct().OrderDescending().ToList();
        return Page();
    }

    /// <summary>Freigegebene Einträge eines Jahres als Text für Presse und Homepage.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Textdatei.</returns>
    public async Task<IActionResult> OnGetTextAsync(CancellationToken ct)
    {
        Jahr ??= Anzeige.Heute(zeit).Year;
        var sb = new StringBuilder();
        sb.AppendLine($"Chronik {Jahr}");
        foreach (var c in await EintraegeAsync(Jahr.Value, true, ct))
        {
            sb.AppendLine();
            sb.AppendLine($"{Anzeige.Datum(c.Datum)}: {c.Titel}");
            if (c.Text is { } text)
            {
                sb.AppendLine(text);
            }
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/plain; charset=utf-8", $"Chronik_{Jahr}_freigegeben.txt");
    }

    private async Task<List<ChronikEintrag>> EintraegeAsync(int jahr, bool nurFreigegeben, CancellationToken ct) =>
        await db.ChronikEintraege.AsNoTracking().Include(c => c.Dokument)
            .Where(c => c.Datum >= new DateOnly(jahr, 1, 1) && c.Datum <= new DateOnly(jahr, 12, 31) && (!nurFreigegeben || c.Veroeffentlichen))
            .OrderBy(c => c.Datum).ThenBy(c => c.Titel).ToListAsync(ct);
}
