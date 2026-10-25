using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dienste;

/// <summary>Gemeinsame Felder für „Dienst planen“ und „Dienst bearbeiten“. Uhrzeiten als Text im 24-Stunden-Format.</summary>
public sealed class Planungsformular
{
    /// <summary>Titel des Dienstes.</summary>
    [Required(ErrorMessage = "Titel fehlt.")]
    [StringLength(200)]
    public string Titel { get; set; } = "";

    /// <summary>Art des Dienstes.</summary>
    public DienstArt Art { get; set; } = DienstArt.Uebung;

    /// <summary>Tag des Dienstes.</summary>
    [Required(ErrorMessage = "Datum fehlt.")]
    public DateOnly? Datum { get; set; }

    /// <summary>Beginn als „HH:MM“.</summary>
    [Required(ErrorMessage = "Beginn fehlt.")]
    public string Beginn { get; set; } = "19:30";

    /// <summary>Ende als „HH:MM“; vor dem Beginn bedeutet Folgetag.</summary>
    [Required(ErrorMessage = "Ende fehlt.")]
    public string Ende { get; set; } = "21:30";

    /// <summary>Ort des Dienstes.</summary>
    [StringLength(200)]
    public string? Ort { get; set; } = "Gerätehaus";

    /// <summary>Thema oder Inhalt des Dienstes.</summary>
    public string? Thema { get; set; }
    /// <summary>Zielabteilungen; keine bedeutet alle.</summary>
    public List<Guid> AbteilungIds { get; set; } = [];
    /// <summary>Zielgruppe zusätzlich auf eine Gruppe eingeschränkt.</summary>
    public Guid? GruppeId { get; set; }
    /// <summary>Zielgruppe zusätzlich auf Träger einer gültigen Qualifikation eingeschränkt.</summary>
    public Guid? QualifikationId { get; set; }

    /// <summary>Übungsleiter.</summary>
    public Guid? VerantwortlicherId { get; set; }
    /// <summary>Als Dienst mit Anwesenheitsliste planen; sonst nur ein Termin im Kalender.</summary>
    public bool AlsDienst { get; set; } = true;
    /// <summary>Mitglieder der Zielgruppe sollen zu- oder absagen.</summary>
    public bool RueckmeldungErbeten { get; set; } = true;
    /// <summary>Zählt für die jährliche Standortfortbildung.</summary>
    public bool ZaehltFuerFortbildung { get; set; } = true;

    /// <summary>Prüft die Uhrzeiten und liefert Beginn und Ende als UTC; ein Ende vor dem Beginn liegt am Folgetag.</summary>
    public bool ZeitenLesen(ModelStateDictionary modelState, string praefix, out DateTimeOffset beginn, out DateTimeOffset ende)
    {
        beginn = ende = default;
        var ok = true;
        if (!ZeitEingabe.TryUhrzeit(Beginn, out var von))
        {
            modelState.AddModelError($"{praefix}.Beginn", "Uhrzeit im Format 19:30 angeben.");
            ok = false;
        }

        if (!ZeitEingabe.TryUhrzeit(Ende, out var bis))
        {
            modelState.AddModelError($"{praefix}.Ende", "Uhrzeit im Format 21:30 angeben.");
            ok = false;
        }

        if (!ok || Datum is null)
        {
            return false;
        }

        if (bis == von)
        {
            modelState.AddModelError($"{praefix}.Ende", "Ende muss nach dem Beginn liegen.");
            return false;
        }

        beginn = Anzeige.AlsUtc(Datum.Value, von);
        ende = Anzeige.AlsUtc(bis < von ? Datum.Value.AddDays(1) : Datum.Value, bis);
        return true;
    }

    /// <summary>Schreibt die Felder in den Dienst; bei einem vorhandenen Dienst müssen seine Abteilungen mitgeladen sein.</summary>
    public async Task UebertragenAsync(AppDbContext db, Dienst dienst, DateTimeOffset beginn, DateTimeOffset ende, CancellationToken ct)
    {
        var ids = AbteilungIds.Distinct().ToList();
        var abteilungen = await db.Abteilungen.Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        dienst.Abteilungen.RemoveAll(a => !ids.Contains(a.Id));
        dienst.Abteilungen.AddRange(abteilungen.Where(a => dienst.Abteilungen.All(d => d.Id != a.Id)));
        dienst.Titel = Titel.Trim();
        dienst.Art = Art;
        dienst.Beginn = beginn;
        dienst.Ende = ende;
        dienst.Ort = string.IsNullOrWhiteSpace(Ort) ? null : Ort.Trim();
        dienst.Thema = string.IsNullOrWhiteSpace(Thema) ? null : Thema.Trim();
        dienst.GruppeId = GruppeId;
        dienst.QualifikationId = QualifikationId;
        dienst.VerantwortlicherId = VerantwortlicherId;
        dienst.AlsDienst = AlsDienst;
        dienst.RueckmeldungErbeten = RueckmeldungErbeten;
        dienst.ZaehltFuerFortbildung = ZaehltFuerFortbildung;
    }

    /// <summary>Füllt das Formular mit den Angaben eines Dienstes (Zeiten in Ortszeit).</summary>
    /// <param name="dienst">Der vorhandene Dienst.</param>
    /// <returns>Das vorbelegte Formular.</returns>
    public static Planungsformular Aus(Dienst dienst) => new()
    {
        Titel = dienst.Titel,
        Art = dienst.Art,
        Datum = Anzeige.LokalesDatum(dienst.Beginn),
        Beginn = Anzeige.Zeit(dienst.Beginn),
        Ende = Anzeige.Zeit(dienst.Ende),
        Ort = dienst.Ort,
        Thema = dienst.Thema,
        AbteilungIds = [.. dienst.AbteilungIds],
        GruppeId = dienst.GruppeId,
        QualifikationId = dienst.QualifikationId,
        VerantwortlicherId = dienst.VerantwortlicherId,
        AlsDienst = dienst.AlsDienst,
        RueckmeldungErbeten = dienst.RueckmeldungErbeten,
        ZaehltFuerFortbildung = dienst.ZaehltFuerFortbildung,
    };
}

/// <summary>Auswahllisten für die Zielgruppe und den Übungsleiter.</summary>
public sealed record Zielgruppenlisten(
    IReadOnlyList<SelectListItem> Abteilungen, IReadOnlyList<SelectListItem> Gruppen, IReadOnlyList<SelectListItem> Qualifikationen,
    IReadOnlyList<SelectListItem> Leiter)
{
    /// <summary>
    /// Aktive Einträge plus die bereits gewählten, damit Speichern eine inaktive Auswahl nicht still entfernt. Übungsleiter
    /// kommen aus dem aktiven Personal außerhalb der Jugend- und Kinderfeuerwehr.
    /// </summary>
    public static async Task<Zielgruppenlisten> LadenAsync(AppDbContext db, DateOnly heute, CancellationToken ct, Dienst? dienst = null)
    {
        var abteilungen = dienst?.AbteilungIds.ToList() ?? [];
        var gruppeId = dienst?.GruppeId;
        var qualifikationId = dienst?.QualifikationId;
        var leiterId = dienst?.VerantwortlicherId;
        var personal = Datenbereich.OhneJugendpersonal(db.Mitglieder.AsNoTracking(), heute)
            .Where(m => m.Mitgliedschaften.Any(s => s.Status != MitgliedStatus.Ausgeschieden && s.GueltigAb <= heute && (s.GueltigBis == null || s.GueltigBis >= heute)));
        return new(
        await db.Abteilungen.Where(a => a.Aktiv || abteilungen.Contains(a.Id)).OrderBy(a => a.Sortierung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToListAsync(ct),
        await db.Gruppen.Where(g => g.Aktiv || g.Id == gruppeId).OrderBy(g => g.Sortierung).ThenBy(g => g.Bezeichnung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString())).ToListAsync(ct),
        await db.Qualifikationen.Where(q => q.Aktiv || q.Id == qualifikationId).OrderBy(q => q.Sortierung)
            .Select(q => new SelectListItem(q.Bezeichnung, q.Id.ToString())).ToListAsync(ct),
        await db.Mitglieder.AsNoTracking().Where(m => m.Id == leiterId || personal.Any(p => p.Id == m.Id))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new SelectListItem(m.Nachname + ", " + m.Vorname, m.Id.ToString())).ToListAsync(ct));
    }
}
