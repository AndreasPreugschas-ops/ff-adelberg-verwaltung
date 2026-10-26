using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Technik.Geraetearten;

/// <summary>Geräteart mit Prüfplan anlegen oder bearbeiten.</summary>
[Authorize(Policy = Berechtigungen.TechnikBearbeiten)]
public class BearbeitenModel(AppDbContext db) : PageModel
{
    /// <summary>Leere Zeilen für neue Prüfplanpositionen.</summary>
    private const int LeereZeilen = 2;

    /// <summary>Eingaben des Formulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Es wird eine neue Geräteart angelegt.</summary>
    public bool IstNeu => Formular.Id is null;

    /// <summary>Formular einer Geräteart.</summary>
    public sealed class Eingabe
    {
        /// <summary>Kennung; leer bei neuer Geräteart.</summary>
        public Guid? Id { get; set; }
        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }

        /// <summary>Kurzcode.</summary>
        [Required(ErrorMessage = "Code fehlt."), StringLength(20), Display(Name = "Code")]
        public string Code { get; set; } = "";

        /// <summary>Bezeichnung.</summary>
        [Required(ErrorMessage = "Bezeichnung fehlt."), StringLength(100), Display(Name = "Bezeichnung")]
        public string Bezeichnung { get; set; } = "";

        /// <summary>Rechtsgrundlage oder Norm der Prüfung.</summary>
        [StringLength(200), Display(Name = "Prüfgrundlage")] public string? Pruefgrundlage { get; set; }
        /// <summary>Lebensdauer ab Herstellung in Jahren.</summary>
        [Range(1, 100), Display(Name = "Lebensdauer in Jahren ab Herstellung")] public int? AussonderungNachJahren { get; set; }
        /// <summary>Höchstzahl der Wäschen nach Herstellerangabe.</summary>
        [Range(1, 500), Display(Name = "Höchstzahl Wäschen (Hersteller)")] public int? HoechstWaeschen { get; set; }
        /// <summary>Prüfvorlage für Fahrzeuge statt für Geräte.</summary>
        [Display(Name = "Prüfvorlage für Fahrzeuge")] public bool FuerFahrzeuge { get; set; }

        /// <summary>Werkstattliste nach dem Einsatz (V13).</summary>
        [Display(Name = "Nach dem Einsatz an")] public Ruecklauf Ruecklauf { get; set; }
        /// <summary>In Auswahllisten angeboten.</summary>
        [Display(Name = "aktiv")] public bool Aktiv { get; set; } = true;
        /// <summary>Reihenfolge in Listen.</summary>
        [Display(Name = "Sortierung")] public int Sortierung { get; set; }

        /// <summary>Positionen des Prüfplans.</summary>
        public List<PositionEingabe> Positionen { get; set; } = [];
    }

    /// <summary>Eingabezeile einer Prüfplanposition.</summary>
    public sealed class PositionEingabe
    {
        /// <summary>Kennung der gespeicherten Position; leer bei neuen.</summary>
        public Guid? Id { get; set; }
        /// <summary>Art der Prüfung.</summary>
        public Pruefart Pruefart { get; set; }

        /// <summary>Leer = Zeile ignorieren bzw. Position entfernen.</summary>
        [Range(0, 240, ErrorMessage = "Intervall zwischen 0 und 240 Monaten.")]
        public int? IntervallMonate { get; set; }

        /// <summary>Wer prüfen darf.</summary>
        [StringLength(200)] public string? Pruefer { get; set; }
        /// <summary>Checkliste, ein Prüfpunkt je Zeile.</summary>
        public string? Checkliste { get; set; }
    }

    /// <summary>Zeigt eine Geräteart oder ein leeres Formular.</summary>
    /// <param name="id">Kennung der Geräteart; leer für eine neue.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not null)
        {
            var art = await db.Geraetearten.Include(g => g.Pruefplan).FirstOrDefaultAsync(g => g.Id == id, ct);
            if (art is null)
            {
                return NotFound();
            }

            Formular = new Eingabe
            {
                Id = art.Id,
                Version = db.Entry(art).Property<uint>("Version").CurrentValue,
                Code = art.Code,
                Bezeichnung = art.Bezeichnung,
                Pruefgrundlage = art.Pruefgrundlage,
                AussonderungNachJahren = art.AussonderungNachJahren,
                HoechstWaeschen = art.HoechstWaeschen,
                FuerFahrzeuge = art.FuerFahrzeuge,
                Ruecklauf = art.Ruecklauf,
                Aktiv = art.Aktiv,
                Sortierung = art.Sortierung,
                Positionen = art.Pruefplan.OrderBy(p => p.Pruefart).Select(p => new PositionEingabe
                {
                    Id = p.Id,
                    Pruefart = p.Pruefart,
                    IntervallMonate = p.IntervallMonate,
                    Pruefer = p.Pruefer,
                    Checkliste = Pruefpunkt.AlsZeilen(Pruefpunkt.AusJson(p.Checkliste)),
                }).ToList(),
            };
        }

        LeereZeilenErgaenzen();
        return Page();
    }

    /// <summary>Speichert die Geräteart mit Prüfplan.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Liste, bei Fehlern die Seite oder 404.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var code = (Formular.Code ?? "").Trim().ToUpperInvariant();
        if (await db.Geraetearten.IgnoreQueryFilters().AnyAsync(g => g.Code == code && g.Id != Formular.Id, ct))
        {
            ModelState.AddModelError("Formular.Code", "Diesen Code gibt es schon.");
        }

        var genutzt = Formular.Positionen.Where(p => p.IntervallMonate is not null).ToList();
        if (genutzt.GroupBy(p => p.Pruefart).Any(g => g.Count() > 1))
        {
            ModelState.AddModelError(string.Empty, "Jede Prüfart darf im Prüfplan nur einmal vorkommen.");
        }

        if (!ModelState.IsValid)
        {
            LeereZeilenErgaenzen();
            return Page();
        }

        Geraeteart art;
        if (IstNeu)
        {
            art = new Geraeteart { Code = code, Bezeichnung = Formular.Bezeichnung };
            db.Geraetearten.Add(art);
        }
        else
        {
            var vorhanden = await db.Geraetearten.Include(g => g.Pruefplan).FirstOrDefaultAsync(g => g.Id == Formular.Id, ct);
            if (vorhanden is null)
            {
                return NotFound();
            }

            art = vorhanden;
            db.Entry(art).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        art.Code = code;
        art.Bezeichnung = Formular.Bezeichnung.Trim();
        art.Pruefgrundlage = Formular.Pruefgrundlage;
        art.AussonderungNachJahren = Formular.AussonderungNachJahren;
        art.HoechstWaeschen = Formular.HoechstWaeschen;
        art.FuerFahrzeuge = Formular.FuerFahrzeuge;
        art.Ruecklauf = Formular.Ruecklauf;
        art.Aktiv = Formular.Aktiv;
        art.Sortierung = Formular.Sortierung;

        // Entfernte Positionen werden weich gelöscht; alte Protokolle behalten ihren Verweis.
        foreach (var position in art.Pruefplan.Where(p => !genutzt.Any(g => g.Id == p.Id)).ToList())
        {
            db.PruefplanPositionen.Remove(position);
        }

        foreach (var eingabe in genutzt)
        {
            var position = art.Pruefplan.FirstOrDefault(p => p.Id == eingabe.Id);
            if (position is null)
            {
                position = new PruefplanPosition();
                art.Pruefplan.Add(position);
            }

            position.Pruefart = eingabe.Pruefart;
            position.IntervallMonate = eingabe.IntervallMonate!.Value;
            position.Pruefer = string.IsNullOrWhiteSpace(eingabe.Pruefer) ? null : eingabe.Pruefer.Trim();
            position.Checkliste = Pruefpunkt.AlsJson(Pruefpunkt.AusZeilen(eingabe.Checkliste));
        }

        if (!IstNeu)
        {
            // Auch reine Prüfplanänderungen sollen xmin der Geräteart prüfen und erhöhen.
            db.Entry(art).State = EntityState.Modified;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "Die Geräteart wurde inzwischen von jemand anderem geändert. Bitte neu laden.");
            LeereZeilenErgaenzen();
            return Page();
        }

        TempData["Meldung"] = $"{art.Bezeichnung} gespeichert.";
        return RedirectToPage("Index");
    }

    private void LeereZeilenErgaenzen()
    {
        Formular.Positionen.RemoveAll(p => p.Id is null && p.IntervallMonate is null);
        for (var i = 0; i < LeereZeilen; i++)
        {
            Formular.Positionen.Add(new PositionEingabe());
        }
    }
}
