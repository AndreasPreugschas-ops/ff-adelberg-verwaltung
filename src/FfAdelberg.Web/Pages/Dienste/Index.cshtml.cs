using System.ComponentModel.DataAnnotations;
using FfAdelberg.Domain.Dienstbetrieb;
using FfAdelberg.Infrastructure.Identitaet;
using FfAdelberg.Infrastructure.Persistenz;
using FfAdelberg.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FfAdelberg.Web.Pages.Dienste;

public class IndexModel(AppDbContext db, IAuthorizationService autorisierung) : PageModel
{
    public IReadOnlyList<Zeile> Zeilen { get; private set; } = [];

    [BindProperty]
    public NeuerDienst Neu { get; set; } = new();

    public sealed record Zeile(Guid Id, DateTimeOffset Beginn, DateTimeOffset Ende, DienstArt Art, string Titel, DienstStatus Status, int Anwesend);

    public sealed class NeuerDienst
    {
        [Required(ErrorMessage = "Titel fehlt.")] public string Titel { get; set; } = "";
        public DienstArt Art { get; set; } = DienstArt.Uebung;
        [Required(ErrorMessage = "Datum fehlt.")] public DateOnly? Datum { get; set; }
        public TimeOnly Beginn { get; set; } = new(19, 30);
        public TimeOnly Ende { get; set; } = new(21, 30);
    }

    public async Task OnGetAsync(CancellationToken ct) => await LadenAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!(await autorisierung.AuthorizeAsync(User, Berechtigungen.DiensteBearbeiten)).Succeeded)
        {
            return Forbid();
        }

        if (Neu.Ende <= Neu.Beginn)
        {
            ModelState.AddModelError("Neu.Ende", "Ende muss nach dem Beginn liegen.");
        }

        if (!ModelState.IsValid)
        {
            await LadenAsync(ct);
            return Page();
        }

        var dienst = new Dienst
        {
            Titel = Neu.Titel,
            Art = Neu.Art,
            Beginn = Anzeige.AlsUtc(Neu.Datum!.Value, Neu.Beginn),
            Ende = Anzeige.AlsUtc(Neu.Datum!.Value, Neu.Ende),
            Ort = "Gerätehaus",
            Status = DienstStatus.Geplant,
        };
        db.Dienste.Add(dienst);
        await db.SaveChangesAsync(ct);
        return RedirectToPage("Anwesenheit", new { id = dienst.Id });
    }

    private async Task LadenAsync(CancellationToken ct)
    {
        Zeilen = await db.Dienste.AsNoTracking()
            .OrderByDescending(d => d.Beginn)
            .Take(50)
            .Select(d => new Zeile(d.Id, d.Beginn, d.Ende, d.Art, d.Titel, d.Status,
                d.Anwesenheiten.Count(a => a.Status == AnwesenheitStatus.Anwesend)))
            .ToListAsync(ct);
    }
}
