using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Vorlagen;

/// <summary>Briefvorlage anlegen oder ändern (Schriftführung, V56).</summary>
[Authorize(Policy = Berechtigungen.VorlagenBearbeiten)]
public class BearbeitenModel(AppDbContext db) : PageModel
{
    /// <summary>Formularfelder.</summary>
    public sealed class Eingabe
    {
        /// <summary>Name der Vorlage.</summary>
        [Required(ErrorMessage = "Bitte einen Namen angeben.")]
        [StringLength(100)]
        [Display(Name = "Name der Vorlage")]
        public string Bezeichnung { get; set; } = "";

        /// <summary>Betreff oder Überschrift.</summary>
        [Required(ErrorMessage = "Bitte einen Betreff angeben.")]
        [StringLength(200)]
        [Display(Name = "Betreff oder Überschrift")]
        public string Betreff { get; set; } = "";

        /// <summary>Brief oder Urkunde.</summary>
        [Display(Name = "Form")]
        public VorlageForm Form { get; set; }

        /// <summary>Empfänger.</summary>
        [Display(Name = "Empfänger")]
        public VorlageEmpfaenger Empfaenger { get; set; }

        /// <summary>Text mit Platzhaltern.</summary>
        [Required(ErrorMessage = "Bitte einen Text angeben.")]
        [StringLength(8000)]
        [Display(Name = "Text")]
        public string Text { get; set; } = "";

        /// <summary>Zur Auswahl angeboten.</summary>
        [Display(Name = "aktiv (zur Auswahl angeboten)")]
        public bool Aktiv { get; set; } = true;
    }

    /// <summary>Kennung beim Bearbeiten.</summary>
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }

    /// <summary>Formular.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();

    /// <summary>Zeigt das Formular.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id is { } id)
        {
            if (await db.Briefvorlagen.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct) is not { } v)
            {
                return NotFound();
            }

            Formular = new Eingabe { Bezeichnung = v.Bezeichnung, Betreff = v.Betreff, Form = v.Form, Empfaenger = v.Empfaenger, Text = v.Text, Aktiv = v.Aktiv };
        }

        return Page();
    }

    /// <summary>Speichert die Vorlage.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Übersicht oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var unbekannt = Platzhalter.Unbekannte(Formular.Betreff).Concat(Platzhalter.Unbekannte(Formular.Text)).Distinct().ToList();
        if (unbekannt.Count > 0)
        {
            ModelState.AddModelError("Formular.Text", $"Unbekannte Platzhalter: {string.Join(", ", unbekannt.Select(n => "{" + n + "}"))}.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Briefvorlage v;
        if (Id is { } id)
        {
            if (await db.Briefvorlagen.FirstOrDefaultAsync(x => x.Id == id, ct) is not { } vorhanden)
            {
                return NotFound();
            }

            v = vorhanden;
        }
        else
        {
            // Eigene Vorlagen bekommen einen eigenen Code, damit der Seed sie nie überschreibt.
            v = new Briefvorlage { Code = "EIG-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Bezeichnung = "", Betreff = "", Text = "", Sortierung = 900 };
            db.Briefvorlagen.Add(v);
        }

        v.Bezeichnung = Formular.Bezeichnung.Trim();
        v.Betreff = Formular.Betreff.Trim();
        v.Form = Formular.Form;
        v.Empfaenger = Formular.Empfaenger;
        v.Text = Formular.Text.Replace("\r\n", "\n").Trim();
        v.Aktiv = Formular.Aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Vorlage „{v.Bezeichnung}“ gespeichert.";
        return RedirectToPage("Index");
    }
}
