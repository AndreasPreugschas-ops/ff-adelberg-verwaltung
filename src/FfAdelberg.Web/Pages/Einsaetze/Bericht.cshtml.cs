using System.ComponentModel.DataAnnotations;
using FfAdelberg.Domain.Einsaetze;
using FfAdelberg.Infrastructure.Identitaet;
using FfAdelberg.Infrastructure.Persistenz;
using FfAdelberg.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FfAdelberg.Web.Pages.Einsaetze;

/// <summary>
/// Einsatzbericht ansehen, bearbeiten und freigeben. Lesen dürfen alle mit EinsaetzeLesen,
/// ändern alle mit EinsaetzeBearbeiten; nach der Freigabe nur noch, wer freigeben darf.
/// </summary>
public class BerichtModel(AppDbContext db, TimeProvider zeit, IAuthorizationService autorisierung, IAktuellerBenutzer aktuellerBenutzer) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public Einsatz? Einsatz { get; private set; }
    public bool DarfAendern { get; private set; }
    public bool DarfFreigeben { get; private set; }
    public bool AusAlarm { get; private set; }
    public string? FreigegebenVon { get; private set; }
    public IReadOnlyList<string> Hindernisse { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Kategorien { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Fahrzeugauswahl { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Mitgliederauswahl { get; private set; } = [];
    public IReadOnlyDictionary<Guid, string> Fahrzeugnamen { get; private set; } = new Dictionary<Guid, string>();
    public IReadOnlyDictionary<Guid, string> Mitgliedernamen { get; private set; } = new Dictionary<Guid, string>();

    public static IReadOnlyList<SelectListItem> Rechtsgrundlagen { get; } =
    [
        new("§ 2 Abs. 1 FwG (Pflichtaufgabe)", nameof(Rechtsgrundlage.Para2Abs1)),
        new("§ 2 Abs. 2 FwG (weitere Aufgabe, kostenersatzpflichtig)", nameof(Rechtsgrundlage.Para2Abs2)),
    ];

    public sealed class Eingabe
    {
        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }

        [StringLength(50), Display(Name = "Stichwort")] public string? Stichwort { get; set; }
        [Display(Name = "Stichwort-Beschreibung")] public string? StichwortBeschreibung { get; set; }
        [Display(Name = "Meldung")] public string? Meldung { get; set; }

        [Display(Name = "Straße")] public string? Strasse { get; set; }
        [Display(Name = "Hausnummer")] public string? Hausnummer { get; set; }
        [Display(Name = "PLZ")] public string? Plz { get; set; }
        [Display(Name = "Ort")] public string? Ort { get; set; }
        [Display(Name = "Ortsteil")] public string? Ortsteil { get; set; }
        [Display(Name = "Objekt")] public string? Objekt { get; set; }

        [Required(ErrorMessage = "Alarmzeit fehlt."), Display(Name = "Alarmiert")] public string Alarmiert { get; set; } = "";
        [Display(Name = "Ausgerückt")] public string? Ausgerueckt { get; set; }
        [Display(Name = "Eintreffen Einsatzort")] public string? EintreffenEinsatzort { get; set; }
        [Display(Name = "Einsatzende")] public string? Einsatzende { get; set; }
        [Display(Name = "Einsatzbereit")] public string? EinsatzbereitschaftHergestellt { get; set; }

        [Display(Name = "Statistik-Kategorie")] public Guid? StatistikKategorieId { get; set; }
        [Display(Name = "Rechtsgrundlage")] public Rechtsgrundlage Rechtsgrundlage { get; set; }
        [Display(Name = "Einsatzleiter")] public Guid? EinsatzleiterId { get; set; }

        [Display(Name = "Lagebericht")] public string? Lagebericht { get; set; }
        [Display(Name = "Maßnahmen")] public string? Massnahmen { get; set; }

        [Range(0, 999, ErrorMessage = "0 bis 999."), Display(Name = "Gerettete")] public int Gerettete { get; set; }
        [Range(0, 999, ErrorMessage = "0 bis 999."), Display(Name = "Verletzte")] public int Verletzte { get; set; }
        [Range(0, 999, ErrorMessage = "0 bis 999."), Display(Name = "Tote")] public int Tote { get; set; }
        [Range(0, 999, ErrorMessage = "0 bis 999."), Display(Name = "Verletzte Einsatzkräfte")] public int VerletzteFeuerwehr { get; set; }
        [Display(Name = "Überörtliche Hilfe")] public bool Ueberoertlich { get; set; }

        public List<FahrzeugEingabe> Fahrzeuge { get; set; } = [];
        public List<TeilnehmerEingabe> Teilnehmer { get; set; } = [];
    }

    public sealed class FahrzeugEingabe
    {
        public Guid FahrzeugId { get; set; }
        public bool Dabei { get; set; }
        [Range(0, 9999, ErrorMessage = "0 bis 9999 km.")] public int? Kilometer { get; set; }
    }

    public sealed class TeilnehmerEingabe
    {
        public Guid MitgliedId { get; set; }
        public bool Dabei { get; set; }
        public Guid? FahrzeugId { get; set; }
        [StringLength(50)] public string? Funktion { get; set; }
        public bool Atemschutz { get; set; }
        [Range(0, 600, ErrorMessage = "0 bis 600 Minuten.")] public int? AtemschutzMinuten { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        // Verfolgt laden, sonst fehlt die Schattenspalte xmin für die Versionsprüfung.
        var einsatz = await LadenAsync(id, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        Formular = AusEinsatz(einsatz);
        await AnzeigeVorbereitenAsync(einsatz, ct);
        return Page();
    }

    public Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct) => SpeichernAsync(id, freigeben: false, ct);

    public Task<IActionResult> OnPostFreigebenAsync(Guid id, CancellationToken ct) => SpeichernAsync(id, freigeben: true, ct);

    public async Task<IActionResult> OnPostZuruecknehmenAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.EinsaetzeFreigeben))
        {
            return Forbid();
        }

        var einsatz = await db.Einsaetze.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        if (einsatz.Status == EinsatzStatus.Freigegeben)
        {
            db.Entry(einsatz).Property<uint>("Version").OriginalValue = Formular.Version;
            einsatz.FreigabeZuruecknehmen();
            if (!await SicherSpeichernAsync(ct))
            {
                return await NeuAnzeigenAsync(id, ct);
            }

            TempData["Meldung"] = $"Freigabe von Einsatz {einsatz.Einsatznummer} zurückgenommen.";
        }

        return RedirectToPage(new { id });
    }

    private async Task<IActionResult> SpeichernAsync(Guid id, bool freigeben, CancellationToken ct)
    {
        var einsatz = await LadenAsync(id, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        if (!await DarfAendernAsync(einsatz) || (freigeben && !await DarfAsync(Berechtigungen.EinsaetzeFreigeben)))
        {
            return Forbid();
        }

        db.Entry(einsatz).Property<uint>("Version").OriginalValue = Formular.Version;

        var zeiten = ZeitenLesen(einsatz);
        await AuswahlPruefenAsync(ct);
        if (!ModelState.IsValid)
        {
            await AnzeigeVorbereitenAsync(einsatz, ct);
            return Page();
        }

        Uebernehmen(einsatz, zeiten);
        FahrzeugeUndTeilnehmerUebernehmen(einsatz);
        einsatz.InBearbeitungNehmen();

        // Neue Kinder hängen erst nach DetectChanges in den Navigationen; die Hindernisse zählen sie mit.
        db.ChangeTracker.DetectChanges();
        var hindernisse = einsatz.FreigabeHindernisse();
        if (einsatz.Status == EinsatzStatus.Freigegeben && hindernisse.Count > 0)
        {
            ModelState.AddModelError(string.Empty, $"Ein freigegebener Bericht muss vollständig bleiben: {string.Join(" ", hindernisse)} Sonst bitte erst die Freigabe zurücknehmen.");
            await AnzeigeVorbereitenAsync(einsatz, ct);
            return Page();
        }

        var freigegeben = freigeben && hindernisse.Count == 0;
        if (freigegeben)
        {
            // Im selben Speichervorgang: freigegeben wird genau der Stand aus diesem Formular.
            einsatz.Freigeben(aktuellerBenutzer.Id ?? Guid.Empty, zeit.GetUtcNow());
        }

        // Auch reine Teilnehmer- oder Fahrzeugänderungen sollen xmin des Einsatzes prüfen und erhöhen.
        db.Entry(einsatz).State = EntityState.Modified;

        if (!await SicherSpeichernAsync(ct))
        {
            return await NeuAnzeigenAsync(id, ct);
        }

        TempData["Meldung"] = (freigeben, freigegeben) switch
        {
            (false, _) => $"Einsatz {einsatz.Einsatznummer} gespeichert.",
            (true, true) => $"Einsatz {einsatz.Einsatznummer} freigegeben.",
            (true, false) => $"Gespeichert, aber noch nicht freigegeben: {string.Join(" ", hindernisse)}",
        };
        return RedirectToPage(new { id });
    }

    /// <summary>Nur bekannte Fahrzeuge, Mitglieder und Kategorien annehmen; manipulierte Formulare sollen nicht an Fremdschlüsseln scheitern.</summary>
    private async Task AuswahlPruefenAsync(CancellationToken ct)
    {
        var fahrzeuge = (await db.Fahrzeuge.Select(f => f.Id).ToListAsync(ct)).ToHashSet();
        var mitglieder = (await db.Mitglieder.Select(m => m.Id).ToListAsync(ct)).ToHashSet();
        Formular.Fahrzeuge.RemoveAll(f => !fahrzeuge.Contains(f.FahrzeugId));
        Formular.Teilnehmer.RemoveAll(t => !mitglieder.Contains(t.MitgliedId));
        foreach (var t in Formular.Teilnehmer.Where(t => t.FahrzeugId is not null && !fahrzeuge.Contains(t.FahrzeugId.Value)))
        {
            t.FahrzeugId = null;
        }

        if (Formular.EinsatzleiterId is not null && !mitglieder.Contains(Formular.EinsatzleiterId.Value))
        {
            ModelState.AddModelError("Formular.EinsatzleiterId", "Unbekanntes Mitglied.");
        }

        if (Formular.StatistikKategorieId is not null && !await db.StatistikKategorien.AnyAsync(k => k.Id == Formular.StatistikKategorieId, ct))
        {
            ModelState.AddModelError("Formular.StatistikKategorieId", "Unbekannte Kategorie.");
        }
    }

    /// <summary>
    /// Liest die Zeitfelder. Unveränderte Felder behalten ihren gespeicherten Wert, weil die Ortszeit
    /// beim Wechsel zur Winterzeit (02:00–03:00) doppelt vorkommt und sonst um eine Stunde rutschen könnte.
    /// </summary>
    private (DateTimeOffset Alarmiert, DateTimeOffset? Ausgerueckt, DateTimeOffset? Eintreffen, DateTimeOffset? Ende, DateTimeOffset? Einsatzbereit) ZeitenLesen(Einsatz bisher)
    {
        DateTimeOffset? Lesen(string feld, string? text, DateTimeOffset? bezug, DateTimeOffset? gespeichert)
        {
            if (gespeichert is not null && (text ?? "").Trim() == ZeitEingabe.Text(gespeichert))
            {
                return gespeichert;
            }

            if (ZeitEingabe.TryLesen(text, bezug, out var wert))
            {
                return wert;
            }

            ModelState.AddModelError($"Formular.{feld}", "Bitte als TT.MM.JJJJ HH:MM oder nur HH:MM eingeben.");
            return null;
        }

        var alarmiert = Lesen(nameof(Eingabe.Alarmiert), Formular.Alarmiert, null, bisher.Alarmiert);
        if (alarmiert is null && !string.IsNullOrWhiteSpace(Formular.Alarmiert))
        {
            ModelState.Remove($"Formular.{nameof(Eingabe.Alarmiert)}");
            ModelState.AddModelError($"Formular.{nameof(Eingabe.Alarmiert)}", "Alarmzeit bitte mit Datum: TT.MM.JJJJ HH:MM.");
        }

        return (
            alarmiert ?? default,
            Lesen(nameof(Eingabe.Ausgerueckt), Formular.Ausgerueckt, alarmiert, bisher.Ausgerueckt),
            Lesen(nameof(Eingabe.EintreffenEinsatzort), Formular.EintreffenEinsatzort, alarmiert, bisher.EintreffenEinsatzort),
            Lesen(nameof(Eingabe.Einsatzende), Formular.Einsatzende, alarmiert, bisher.Einsatzende),
            Lesen(nameof(Eingabe.EinsatzbereitschaftHergestellt), Formular.EinsatzbereitschaftHergestellt, alarmiert, bisher.EinsatzbereitschaftHergestellt));
    }

    private void Uebernehmen(Einsatz einsatz, (DateTimeOffset Alarmiert, DateTimeOffset? Ausgerueckt, DateTimeOffset? Eintreffen, DateTimeOffset? Ende, DateTimeOffset? Einsatzbereit) zeiten)
    {
        var f = Formular;
        einsatz.Stichwort = Leer(f.Stichwort);
        einsatz.StichwortBeschreibung = Leer(f.StichwortBeschreibung);
        einsatz.Meldung = Leer(f.Meldung);
        einsatz.Strasse = Leer(f.Strasse);
        einsatz.Hausnummer = Leer(f.Hausnummer);
        einsatz.Plz = Leer(f.Plz);
        einsatz.Ort = Leer(f.Ort);
        einsatz.Ortsteil = Leer(f.Ortsteil);
        einsatz.Objekt = Leer(f.Objekt);
        einsatz.Alarmiert = zeiten.Alarmiert;
        einsatz.Ausgerueckt = zeiten.Ausgerueckt;
        einsatz.EintreffenEinsatzort = zeiten.Eintreffen;
        einsatz.Einsatzende = zeiten.Ende;
        einsatz.EinsatzbereitschaftHergestellt = zeiten.Einsatzbereit;
        einsatz.StatistikKategorieId = f.StatistikKategorieId;
        einsatz.Rechtsgrundlage = f.Rechtsgrundlage;
        einsatz.EinsatzleiterId = f.EinsatzleiterId;
        einsatz.Lagebericht = Leer(f.Lagebericht);
        einsatz.Massnahmen = Leer(f.Massnahmen);
        einsatz.Gerettete = f.Gerettete;
        einsatz.Verletzte = f.Verletzte;
        einsatz.Tote = f.Tote;
        einsatz.VerletzteFeuerwehr = f.VerletzteFeuerwehr;
        einsatz.Ueberoertlich = f.Ueberoertlich;
    }

    /// <summary>Ein Fahrzeug, auf dem jemand saß, gehört zum Einsatz, auch wenn es nicht angehakt ist.</summary>
    private void FahrzeugeUndTeilnehmerUebernehmen(Einsatz einsatz)
    {
        var teilnehmer = Formular.Teilnehmer.Where(t => t.Dabei).ToList();
        var benoetigt = Formular.Fahrzeuge.Where(f => f.Dabei).Select(f => f.FahrzeugId)
            .Concat(teilnehmer.Where(t => t.FahrzeugId is not null).Select(t => t.FahrzeugId!.Value))
            .ToHashSet();

        foreach (var vorhanden in einsatz.Fahrzeuge.Where(f => !benoetigt.Contains(f.FahrzeugId)).ToList())
        {
            db.EinsatzFahrzeuge.Remove(vorhanden);
            einsatz.Fahrzeuge.Remove(vorhanden);
        }

        foreach (var fahrzeugId in benoetigt)
        {
            var eintrag = einsatz.Fahrzeuge.FirstOrDefault(f => f.FahrzeugId == fahrzeugId);
            if (eintrag is null)
            {
                // Ausdrücklich hinzufügen: die Id ist schon gesetzt, über die Navigation hielte EF den Eintrag für vorhanden.
                eintrag = new EinsatzFahrzeug { EinsatzId = einsatz.Id, FahrzeugId = fahrzeugId };
                db.EinsatzFahrzeuge.Add(eintrag);
            }

            eintrag.Kilometer = Formular.Fahrzeuge.FirstOrDefault(f => f.FahrzeugId == fahrzeugId)?.Kilometer;
        }

        var dabei = teilnehmer.Select(t => t.MitgliedId).ToHashSet();
        foreach (var vorhanden in einsatz.Teilnahmen.Where(t => !dabei.Contains(t.MitgliedId)).ToList())
        {
            db.EinsatzTeilnahmen.Remove(vorhanden);
            einsatz.Teilnahmen.Remove(vorhanden);
        }

        foreach (var t in teilnehmer)
        {
            var eintrag = einsatz.Teilnahmen.FirstOrDefault(x => x.MitgliedId == t.MitgliedId);
            if (eintrag is null)
            {
                eintrag = new EinsatzTeilnahme { EinsatzId = einsatz.Id, MitgliedId = t.MitgliedId, Quelle = TeilnahmeQuelle.Manuell };
                db.EinsatzTeilnahmen.Add(eintrag);
            }

            eintrag.EinsatzFahrzeugId = t.FahrzeugId is null ? null : einsatz.Fahrzeuge.First(f => f.FahrzeugId == t.FahrzeugId).Id;
            eintrag.Funktion = Leer(t.Funktion);
            eintrag.Atemschutz = t.Atemschutz;
            eintrag.AtemschutzMinuten = t.Atemschutz ? t.AtemschutzMinuten : null;
        }
    }

    private async Task<bool> SicherSpeichernAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Fehler"] = "Der Bericht wurde inzwischen von jemand anderem geändert. Unten steht der aktuelle Stand; bitte die Änderungen erneut eintragen.";
            return false;
        }
    }

    private async Task<IActionResult> NeuAnzeigenAsync(Guid id, CancellationToken ct)
    {
        // Sonst zeigen die Tag-Helper die geposteten Werte samt alter Version statt des aktuellen Stands.
        ModelState.Clear();
        db.ChangeTracker.Clear();
        return await OnGetAsync(id, ct);
    }

    private Task<Einsatz?> LadenAsync(Guid id, CancellationToken ct) =>
        db.Einsaetze.Include(e => e.Fahrzeuge).Include(e => e.Teilnahmen).AsSplitQuery().FirstOrDefaultAsync(e => e.Id == id, ct);

    private Eingabe AusEinsatz(Einsatz e) => new()
    {
        Version = db.Entry(e).Property<uint>("Version").CurrentValue,
        Stichwort = e.Stichwort,
        StichwortBeschreibung = e.StichwortBeschreibung,
        Meldung = e.Meldung,
        Strasse = e.Strasse,
        Hausnummer = e.Hausnummer,
        Plz = e.Plz,
        Ort = e.Ort,
        Ortsteil = e.Ortsteil,
        Objekt = e.Objekt,
        Alarmiert = ZeitEingabe.Text(e.Alarmiert),
        Ausgerueckt = ZeitEingabe.Text(e.Ausgerueckt),
        EintreffenEinsatzort = ZeitEingabe.Text(e.EintreffenEinsatzort),
        Einsatzende = ZeitEingabe.Text(e.Einsatzende),
        EinsatzbereitschaftHergestellt = ZeitEingabe.Text(e.EinsatzbereitschaftHergestellt),
        StatistikKategorieId = e.StatistikKategorieId,
        Rechtsgrundlage = e.Rechtsgrundlage,
        EinsatzleiterId = e.EinsatzleiterId,
        Lagebericht = e.Lagebericht,
        Massnahmen = e.Massnahmen,
        Gerettete = e.Gerettete,
        Verletzte = e.Verletzte,
        Tote = e.Tote,
        VerletzteFeuerwehr = e.VerletzteFeuerwehr,
        Ueberoertlich = e.Ueberoertlich,
        Fahrzeuge = e.Fahrzeuge.Select(f => new FahrzeugEingabe { FahrzeugId = f.FahrzeugId, Dabei = true, Kilometer = f.Kilometer }).ToList(),
        Teilnehmer = e.Teilnahmen.Select(t => new TeilnehmerEingabe
        {
            MitgliedId = t.MitgliedId,
            Dabei = true,
            FahrzeugId = e.Fahrzeuge.FirstOrDefault(f => f.Id == t.EinsatzFahrzeugId)?.FahrzeugId,
            Funktion = t.Funktion,
            Atemschutz = t.Atemschutz,
            AtemschutzMinuten = t.AtemschutzMinuten,
        }).ToList(),
    };

    /// <summary>Auswahllisten laden und Formularzeilen für alle wählbaren Fahrzeuge und Mitglieder ergänzen.</summary>
    private async Task AnzeigeVorbereitenAsync(Einsatz einsatz, CancellationToken ct)
    {
        Einsatz = einsatz;
        DarfFreigeben = await DarfAsync(Berechtigungen.EinsaetzeFreigeben);
        DarfAendern = await DarfAendernAsync(einsatz);
        Hindernisse = einsatz.Status == EinsatzStatus.Freigegeben ? [] : einsatz.FreigabeHindernisse();
        AusAlarm = await db.AlarmImporte.AnyAsync(a => a.EinsatzId == einsatz.Id, ct);
        FreigegebenVon = einsatz.FreigegebenVon is null
            ? null
            : await db.Users.Where(u => u.Id == einsatz.FreigegebenVon).Select(u => u.UserName).FirstOrDefaultAsync(ct);

        Kategorien = await db.StatistikKategorien
            .Where(k => k.Aktiv || k.Id == einsatz.StatistikKategorieId)
            .OrderBy(k => k.Sortierung)
            .Select(k => new SelectListItem(k.Bezeichnung, k.Id.ToString()))
            .ToListAsync(ct);

        var tag = Anzeige.LokalesDatum(einsatz.Alarmiert);
        var fahrzeugIds = einsatz.Fahrzeuge.Select(f => f.FahrzeugId).ToList();
        var fahrzeuge = await db.Fahrzeuge.AsNoTracking()
            .Where(f => (f.AusserDienstAm == null || f.AusserDienstAm >= tag) || fahrzeugIds.Contains(f.Id))
            .OrderBy(f => f.Funkrufname)
            .Select(f => new { f.Id, Name = f.Funkrufname + " (" + f.Typ + ")" })
            .ToListAsync(ct);
        Fahrzeugnamen = fahrzeuge.ToDictionary(f => f.Id, f => f.Name);
        Fahrzeugauswahl = fahrzeuge.Select(f => new SelectListItem(f.Name, f.Id.ToString())).ToList();

        // Wer nur liest (z. B. die Gemeinde), sieht nur die Beteiligten, nicht die ganze Mitgliederliste.
        var mitgliedIds = einsatz.Teilnahmen.Select(t => t.MitgliedId).ToList();
        var alleAktiven = DarfAendern;
        var mitglieder = await db.Mitglieder.AsNoTracking()
            .Where(m => (alleAktiven && m.Mitgliedschaften.Any(s => s.GueltigAb <= tag && (s.GueltigBis == null || s.GueltigBis >= tag)))
                || mitgliedIds.Contains(m.Id) || m.Id == einsatz.EinsatzleiterId)
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, Name = m.Nachname + ", " + m.Vorname })
            .ToListAsync(ct);
        Mitgliedernamen = mitglieder.ToDictionary(m => m.Id, m => m.Name);
        Mitgliederauswahl = mitglieder.Select(m => new SelectListItem(m.Name, m.Id.ToString())).ToList();

        // Formularzeilen in fester Reihenfolge: alle Fahrzeuge bzw. Mitglieder, angehakt oder nicht.
        Formular.Fahrzeuge = fahrzeuge
            .Select(f => Formular.Fahrzeuge.FirstOrDefault(x => x.FahrzeugId == f.Id) ?? new FahrzeugEingabe { FahrzeugId = f.Id })
            .ToList();
        Formular.Teilnehmer = mitglieder
            .Select(m => Formular.Teilnehmer.FirstOrDefault(x => x.MitgliedId == m.Id) ?? new TeilnehmerEingabe { MitgliedId = m.Id })
            .ToList();
    }

    private async Task<bool> DarfAendernAsync(Einsatz einsatz) =>
        await DarfAsync(einsatz.Status == EinsatzStatus.Freigegeben ? Berechtigungen.EinsaetzeFreigeben : Berechtigungen.EinsaetzeBearbeiten);

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
