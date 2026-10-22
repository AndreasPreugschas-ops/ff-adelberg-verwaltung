using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Dienste;

/// <summary>Gemeinsame Felder für „Dienst planen“ und „Dienst bearbeiten“. Uhrzeiten als Text im 24-Stunden-Format.</summary>
public sealed class Planungsformular
{
    [Required(ErrorMessage = "Titel fehlt.")]
    [StringLength(200)]
    public string Titel { get; set; } = "";

    public DienstArt Art { get; set; } = DienstArt.Uebung;

    [Required(ErrorMessage = "Datum fehlt.")]
    public DateOnly? Datum { get; set; }

    [Required(ErrorMessage = "Beginn fehlt.")]
    public string Beginn { get; set; } = "19:30";

    [Required(ErrorMessage = "Ende fehlt.")]
    public string Ende { get; set; } = "21:30";

    [StringLength(200)]
    public string? Ort { get; set; } = "Gerätehaus";

    public string? Thema { get; set; }
    public Guid? AbteilungId { get; set; }
    public Guid? GruppeId { get; set; }
    public Guid? QualifikationId { get; set; }
    public bool RueckmeldungErbeten { get; set; } = true;
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

    public void Uebertragen(Dienst dienst, DateTimeOffset beginn, DateTimeOffset ende)
    {
        dienst.Titel = Titel.Trim();
        dienst.Art = Art;
        dienst.Beginn = beginn;
        dienst.Ende = ende;
        dienst.Ort = string.IsNullOrWhiteSpace(Ort) ? null : Ort.Trim();
        dienst.Thema = string.IsNullOrWhiteSpace(Thema) ? null : Thema.Trim();
        dienst.AbteilungId = AbteilungId;
        dienst.GruppeId = GruppeId;
        dienst.QualifikationId = QualifikationId;
        dienst.RueckmeldungErbeten = RueckmeldungErbeten;
        dienst.ZaehltFuerFortbildung = ZaehltFuerFortbildung;
    }

    public static Planungsformular Aus(Dienst dienst) => new()
    {
        Titel = dienst.Titel,
        Art = dienst.Art,
        Datum = Anzeige.LokalesDatum(dienst.Beginn),
        Beginn = Anzeige.Zeit(dienst.Beginn),
        Ende = Anzeige.Zeit(dienst.Ende),
        Ort = dienst.Ort,
        Thema = dienst.Thema,
        AbteilungId = dienst.AbteilungId,
        GruppeId = dienst.GruppeId,
        QualifikationId = dienst.QualifikationId,
        RueckmeldungErbeten = dienst.RueckmeldungErbeten,
        ZaehltFuerFortbildung = dienst.ZaehltFuerFortbildung,
    };
}

/// <summary>Auswahllisten für die Zielgruppe.</summary>
public sealed record Zielgruppenlisten(IReadOnlyList<SelectListItem> Abteilungen, IReadOnlyList<SelectListItem> Gruppen, IReadOnlyList<SelectListItem> Qualifikationen)
{
    /// <summary>Aktive Einträge plus die bereits gewählten, damit Speichern eine inaktive Auswahl nicht still entfernt.</summary>
    public static async Task<Zielgruppenlisten> LadenAsync(AppDbContext db, CancellationToken ct,
        Guid? abteilungId = null, Guid? gruppeId = null, Guid? qualifikationId = null) => new(
        await db.Abteilungen.Where(a => a.Aktiv || a.Id == abteilungId).OrderBy(a => a.Sortierung)
            .Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToListAsync(ct),
        await db.Gruppen.Where(g => g.Aktiv || g.Id == gruppeId).OrderBy(g => g.Sortierung).ThenBy(g => g.Bezeichnung)
            .Select(g => new SelectListItem(g.Bezeichnung, g.Id.ToString())).ToListAsync(ct),
        await db.Qualifikationen.Where(q => q.Aktiv || q.Id == qualifikationId).OrderBy(q => q.Sortierung)
            .Select(q => new SelectListItem(q.Bezeichnung, q.Id.ToString())).ToListAsync(ct));
}
