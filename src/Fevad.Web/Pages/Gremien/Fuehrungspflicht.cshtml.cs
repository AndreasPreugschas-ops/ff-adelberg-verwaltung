using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gremien;

/// <summary>Führungspflicht im Jahreskreis anlegen oder bearbeiten (V51): Stichtag, Turnus, Vorlauf, verantwortliche Funktion und Vorlage.</summary>
[Authorize(Policy = Berechtigungen.GremienBearbeiten)]
public class FuehrungspflichtModel(AppDbContext db) : PageModel
{
    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Es wird eine neue Pflicht angelegt.</summary>
    public bool IstNeu => Formular.Id is null;

    /// <summary>Formular einer Pflicht.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung; leer bei neuer Pflicht.</summary>
        public Guid? Id { get; set; }
        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }
        /// <summary>Kurzcode.</summary>
        [Required(ErrorMessage = "Code fehlt."), StringLength(20), Display(Name = "Code")] public string Code { get; set; } = "";
        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")] public string Bezeichnung { get; set; } = "";
        /// <summary>Turnus.</summary>
        [Display(Name = "Turnus")] public PflichtTurnus Turnus { get; set; }
        /// <summary>Monat des Stichtags.</summary>
        [Range(1, 12, ErrorMessage = "Monat 1 bis 12."), Display(Name = "Monat")] public int Monat { get; set; } = 1;
        /// <summary>Tag des Stichtags.</summary>
        [Range(1, 31, ErrorMessage = "Tag 1 bis 31."), Display(Name = "Tag")] public int Tag { get; set; } = 1;
        /// <summary>Abstand in Jahren bei mehrjährlichem Turnus.</summary>
        [Range(1, 20, ErrorMessage = "1 bis 20 Jahre."), Display(Name = "alle … Jahre")] public int IntervallJahre { get; set; } = 1;
        /// <summary>Ein Jahr, in dem die mehrjährliche Pflicht fällig ist.</summary>
        [Range(2000, 2100, ErrorMessage = "Jahr zwischen 2000 und 2100."), Display(Name = "fällig z. B. im Jahr")] public int? Bezugsjahr { get; set; }
        /// <summary>Vorlauf in Tagen.</summary>
        [Range(0, 365, ErrorMessage = "0 bis 365 Tage."), Display(Name = "Vorlauf in Tagen")] public int VorlaufTage { get; set; } = 30;
        /// <summary>Nachlauf in Tagen.</summary>
        [Range(0, 365, ErrorMessage = "0 bis 365 Tage."), Display(Name = "Nachlauf in Tagen")] public int NachlaufTage { get; set; } = 60;
        /// <summary>Verantwortliche Funktionen als Codes.</summary>
        [StringLength(100), Display(Name = "Verantwortlich (Funktionscodes, Alternativen mit |)")] public string? Verantwortlich { get; set; }
        /// <summary>Seite in FEVAD.</summary>
        [StringLength(200), Display(Name = "Seite in FEVAD")] public string? Vorlage { get; set; }
        /// <summary>Rechtsgrundlage.</summary>
        [StringLength(200), Display(Name = "Rechtsgrundlage")] public string? Rechtsgrundlage { get; set; }
        /// <summary>Hinweis für den Nachfolger.</summary>
        [StringLength(1000), Display(Name = "Hinweis")] public string? Hinweis { get; set; }
        /// <summary>Reihenfolge.</summary>
        [Display(Name = "Sortierung")] public int Sortierung { get; set; } = 100;
        /// <summary>Wird überwacht.</summary>
        [Display(Name = "Aktiv")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>Zeigt eine Pflicht oder ein leeres Formular.</summary>
    /// <param name="id">Kennung der Pflicht; leer für eine neue.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            var p = await db.Fuehrungspflichten.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (p is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = p.Id, Version = db.Entry(p).Property<uint>("Version").CurrentValue, Code = p.Code, Bezeichnung = p.Bezeichnung, Turnus = p.Turnus,
                Monat = p.Monat, Tag = p.Tag, IntervallJahre = p.IntervallJahre, Bezugsjahr = p.Bezugsjahr, VorlaufTage = p.VorlaufTage,
                NachlaufTage = p.NachlaufTage, Verantwortlich = p.Verantwortlich, Vorlage = p.Vorlage, Rechtsgrundlage = p.Rechtsgrundlage, Hinweis = p.Hinweis,
                Sortierung = p.Sortierung, Aktiv = p.Aktiv,
            };
        }

        return Page();
    }

    /// <summary>Speichert die Pflicht.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Jahreskreis, bei Fehlern die Seite oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var code = Formular.Code.Trim().ToUpperInvariant();
        if (await db.Fuehrungspflichten.IgnoreQueryFilters().AnyAsync(a => a.Code == code && a.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Code", "Dieser Code ist schon vergeben.");
        }

        var vorlage = Leer(Formular.Vorlage);
        if (vorlage is not null && (!vorlage.StartsWith('/') || vorlage.StartsWith("//", StringComparison.Ordinal)))
        {
            ModelState.AddModelError("Formular.Vorlage", "Bitte eine Seite in FEVAD angeben, z. B. /Auswertungen/Jahresstatistik.");
        }

        if (Formular.Turnus == PflichtTurnus.Mehrjaehrlich && Formular.Bezugsjahr is null)
        {
            ModelState.AddModelError("Formular.Bezugsjahr", "Bei mehrjährlichem Turnus ein Jahr angeben, in dem die Pflicht fällig ist.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Fuehrungspflicht pflicht;
        if (IstNeu)
        {
            pflicht = new Fuehrungspflicht { Code = code, Bezeichnung = "" };
            db.Fuehrungspflichten.Add(pflicht);
        }
        else
        {
            var vorhanden = await db.Fuehrungspflichten.FirstOrDefaultAsync(a => a.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            pflicht = vorhanden;
            db.Entry(pflicht).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        pflicht.Code = code;
        pflicht.Bezeichnung = Formular.Bezeichnung.Trim();
        pflicht.Turnus = Formular.Turnus;
        pflicht.Monat = Formular.Monat;
        pflicht.Tag = Formular.Tag;
        pflicht.IntervallJahre = Formular.Turnus == PflichtTurnus.Mehrjaehrlich ? Formular.IntervallJahre : 1;
        pflicht.Bezugsjahr = Formular.Turnus == PflichtTurnus.Mehrjaehrlich ? Formular.Bezugsjahr : null;
        pflicht.VorlaufTage = Formular.VorlaufTage;
        pflicht.NachlaufTage = Formular.NachlaufTage;
        pflicht.Verantwortlich = Leer(Formular.Verantwortlich) is { } v
            ? string.Join('|', v.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant()
            : null;
        pflicht.Vorlage = vorlage;
        pflicht.Rechtsgrundlage = Leer(Formular.Rechtsgrundlage);
        pflicht.Hinweis = Leer(Formular.Hinweis);
        pflicht.Sortierung = Formular.Sortierung;
        pflicht.Aktiv = Formular.Aktiv;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Der Eintrag wurde inzwischen geändert. Bitte neu laden.");
            return Page();
        }

        return RedirectToPage("Jahreskreis");
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
