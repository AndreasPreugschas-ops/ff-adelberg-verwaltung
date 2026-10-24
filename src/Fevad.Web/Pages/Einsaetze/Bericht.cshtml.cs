using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Einsaetze;

/// <summary>
/// Einsatzbericht ansehen, bearbeiten und freigeben. Lesen dürfen alle mit EinsaetzeLesen,
/// ändern alle mit EinsaetzeBearbeiten; nach der Freigabe nur noch, wer freigeben darf.
/// Fahrzeuge, Teilnehmer, Organisationen, Beteiligte und Ausrüstung sind Zeilen, die über
/// „Hinzufügen“ und „Entfernen“ ohne Speichern umgebaut werden; gespeichert wird erst mit „Speichern“.
/// </summary>
public class BerichtModel(AppDbContext db, TimeProvider zeit, IAuthorizationService autorisierung, IAktuellerBenutzer aktuellerBenutzer, ParameterLeser parameter) : PageModel
{
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    public Einsatz? Einsatz { get; private set; }
    public bool DarfAendern { get; private set; }
    public bool DarfFreigeben { get; private set; }

    /// <summary>Anschrift, Telefon, Kennzeichen und Versicherung der Beteiligten; ohne das Recht nur Rolle und Name.</summary>
    public bool DarfBeteiligteDetails { get; private set; }
    public bool AusAlarm { get; private set; }
    public string? FreigegebenVon { get; private set; }
    public IReadOnlyList<string> Hindernisse { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Kategorien { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Fahrzeugauswahl { get; private set; } = [];
    public IReadOnlyList<SelectListItem> FahrzeugeZumHinzufuegen { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Mitgliederauswahl { get; private set; } = [];
    public IReadOnlyList<SelectListItem> MitgliederZumHinzufuegen { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Kostensatzauswahl { get; private set; } = [];
    public IReadOnlyDictionary<Guid, string> Fahrzeugnamen { get; private set; } = new Dictionary<Guid, string>();
    public IReadOnlyDictionary<Guid, string> Mitgliedernamen { get; private set; } = new Dictionary<Guid, string>();
    public IReadOnlyDictionary<Guid, Kostensatz> Kostensaetze { get; private set; } = new Dictionary<Guid, Kostensatz>();

    /// <summary>Kostenersatz aus dem gespeicherten Stand; null, wenn noch keine Sätze hinterlegt sind.</summary>
    public Kostenaufstellung? Kosten { get; private set; }
    public int RundungMinuten { get; private set; }
    public IReadOnlyList<EntschaedigungZeile> Entschaedigungen { get; private set; } = [];
    public bool EntschaedigungOhneSatz { get; private set; }

    public sealed record EntschaedigungZeile(string Name, DateTimeOffset Von, DateTimeOffset? Bis, Entschaedigungsbetrag? Betrag);

    public static IReadOnlyList<SelectListItem> Rechtsgrundlagen { get; } =
    [
        new("§ 2 Abs. 1 FwG (Pflichtaufgabe)", nameof(Rechtsgrundlage.Para2Abs1)),
        new("§ 2 Abs. 2 FwG (weitere Aufgabe, kostenersatzpflichtig)", nameof(Rechtsgrundlage.Para2Abs2)),
    ];

    public static IReadOnlyList<SelectListItem> Organisationsarten { get; } =
        Enum.GetValues<OrganisationArt>().Select(a => new SelectListItem(Anzeige.Text(a), a.ToString())).ToList();

    public static IReadOnlyList<SelectListItem> Beteiligtenrollen { get; } =
        Enum.GetValues<BeteiligtenRolle>().Select(r => new SelectListItem(Anzeige.Text(r), r.ToString())).ToList();

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
        [Display(Name = "Kostenersatz offen")] public bool KostenersatzOffen { get; set; }
        [Display(Name = "Kostenersatz abgeschlossen am")] public DateOnly? KostenersatzAbgeschlossenAm { get; set; }

        public List<FahrzeugEingabe> Fahrzeuge { get; set; } = [];
        public List<TeilnehmerEingabe> Teilnehmer { get; set; } = [];
        public List<OrganisationEingabe> Organisationen { get; set; } = [];
        public List<BeteiligterEingabe> Beteiligte { get; set; } = [];
        public List<AusruestungEingabe> Ausruestung { get; set; } = [];
    }

    public sealed class FahrzeugEingabe
    {
        public Guid FahrzeugId { get; set; }
        [Display(Name = "Ausgerückt")] public string? Ausgerueckt { get; set; }
        [Display(Name = "Eingetroffen")] public string? Eingetroffen { get; set; }
        [Display(Name = "Zurück")] public string? Zurueck { get; set; }
        [Display(Name = "Einsatzbereit")] public string? Einsatzbereit { get; set; }
        [Range(0, 9999, ErrorMessage = "0 bis 9999 km.")] public int? Kilometer { get; set; }
    }

    public sealed class TeilnehmerEingabe
    {
        public Guid MitgliedId { get; set; }
        public Guid? FahrzeugId { get; set; }
        [StringLength(50)] public string? Funktion { get; set; }
        [Display(Name = "Von")] public string? Von { get; set; }
        [Display(Name = "Bis")] public string? Bis { get; set; }
        public bool Atemschutz { get; set; }
        [Range(0, 600, ErrorMessage = "0 bis 600 Minuten.")] public int? AtemschutzMinuten { get; set; }
    }

    public sealed class OrganisationEingabe
    {
        public Guid? Id { get; set; }
        [Display(Name = "Organisation")] public OrganisationArt Art { get; set; }
        [StringLength(200), Display(Name = "Dienststelle / Einheit")] public string? Bezeichnung { get; set; }
        [StringLength(200), Display(Name = "Ansprechpartner")] public string? Ansprechpartner { get; set; }
        [StringLength(50), Display(Name = "Telefon")] public string? Telefon { get; set; }
        [StringLength(100), Display(Name = "Aktenzeichen")] public string? Aktenzeichen { get; set; }
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    public sealed class BeteiligterEingabe
    {
        public Guid? Id { get; set; }
        [Display(Name = "Rolle")] public BeteiligtenRolle Rolle { get; set; }
        [Required(ErrorMessage = "Name fehlt."), StringLength(200), Display(Name = "Name / Firma")] public string Name { get; set; } = "";
        [StringLength(300), Display(Name = "Anschrift")] public string? Anschrift { get; set; }
        [StringLength(50), Display(Name = "Telefon")] public string? Telefon { get; set; }
        [StringLength(20), Display(Name = "Kennzeichen")] public string? Kennzeichen { get; set; }
        [StringLength(200), Display(Name = "Fahrzeug")] public string? Fahrzeugbeschreibung { get; set; }
        [StringLength(200), Display(Name = "Versicherung")] public string? Versicherung { get; set; }
        [StringLength(100), Display(Name = "Versicherungsnummer")] public string? Versicherungsnummer { get; set; }
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    public sealed class AusruestungEingabe
    {
        public Guid? Id { get; set; }
        public Guid KostensatzId { get; set; }
        [Range(0.01, 99999, ErrorMessage = "Menge größer 0."), Display(Name = "Menge")] public decimal Menge { get; set; } = 1;
        [StringLength(500), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        // Verfolgt laden, sonst fehlt die Schattenspalte xmin für die Versionsprüfung.
        var einsatz = await LadenAsync(id, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        DarfBeteiligteDetails = await DarfAsync(Berechtigungen.EinsaetzeBeteiligte);
        Formular = AusEinsatz(einsatz);
        await AnzeigeVorbereitenAsync(einsatz, ct);
        return Page();
    }

    public Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct) => SpeichernAsync(id, freigeben: false, ct);

    public Task<IActionResult> OnPostFreigebenAsync(Guid id, CancellationToken ct) => SpeichernAsync(id, freigeben: true, ct);

    /// <summary>Fügt eine Zeile hinzu, ohne zu speichern; die übrigen Eingaben bleiben im Formular.</summary>
    public async Task<IActionResult> OnPostHinzufuegenAsync(Guid id, string was, Guid? neuFahrzeugId, Guid? neuMitgliedId, Guid? neuKostensatzId, CancellationToken ct)
    {
        var einsatz = await LadenAsync(id, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        if (!await DarfAendernAsync(einsatz))
        {
            return Forbid();
        }

        switch (was)
        {
            case "fahrzeug" when neuFahrzeugId is { } fid && Formular.Fahrzeuge.All(f => f.FahrzeugId != fid):
                Formular.Fahrzeuge.Add(new FahrzeugEingabe { FahrzeugId = fid });
                break;
            case "teilnehmer" when neuMitgliedId is { } mid && Formular.Teilnehmer.All(t => t.MitgliedId != mid):
                Formular.Teilnehmer.Add(new TeilnehmerEingabe { MitgliedId = mid });
                break;
            case "organisation":
                Formular.Organisationen.Add(new OrganisationEingabe());
                break;
            case "beteiligter":
                Formular.Beteiligte.Add(new BeteiligterEingabe());
                break;
            case "ausruestung" when neuKostensatzId is { } kid:
                Formular.Ausruestung.Add(new AusruestungEingabe { KostensatzId = kid });
                break;
        }

        return await ZeilenNeuAnzeigenAsync(einsatz, ct);
    }

    public async Task<IActionResult> OnPostEntfernenAsync(Guid id, string was, int index, CancellationToken ct)
    {
        var einsatz = await LadenAsync(id, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        if (!await DarfAendernAsync(einsatz))
        {
            return Forbid();
        }

        static void Entfernen<T>(List<T> liste, int i)
        {
            if (i >= 0 && i < liste.Count)
            {
                liste.RemoveAt(i);
            }
        }

        switch (was)
        {
            case "fahrzeug":
                if (index >= 0 && index < Formular.Fahrzeuge.Count)
                {
                    // Wer auf dem Fahrzeug saß, bleibt Teilnehmer, nur ohne Fahrzeug.
                    var fahrzeugId = Formular.Fahrzeuge[index].FahrzeugId;
                    foreach (var t in Formular.Teilnehmer.Where(t => t.FahrzeugId == fahrzeugId))
                    {
                        t.FahrzeugId = null;
                    }
                }

                Entfernen(Formular.Fahrzeuge, index);
                break;
            case "teilnehmer": Entfernen(Formular.Teilnehmer, index); break;
            case "organisation": Entfernen(Formular.Organisationen, index); break;
            case "beteiligter": Entfernen(Formular.Beteiligte, index); break;
            case "ausruestung": Entfernen(Formular.Ausruestung, index); break;
        }

        return await ZeilenNeuAnzeigenAsync(einsatz, ct);
    }

    private async Task<IActionResult> ZeilenNeuAnzeigenAsync(Einsatz einsatz, CancellationToken ct)
    {
        // Sonst zeigen die Tag-Helper die alten Zeilenwerte aus dem ModelState statt der umgebauten Liste.
        var formular = Formular;
        ModelState.Clear();
        Formular = formular;
        await AnzeigeVorbereitenAsync(einsatz, ct);
        return Page();
    }

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
        var fahrzeugzeiten = FahrzeugzeitenLesen(einsatz, zeiten.Alarmiert);
        var teilnehmerzeiten = TeilnehmerzeitenLesen(einsatz, zeiten.Alarmiert);
        if (!ModelState.IsValid)
        {
            await AnzeigeVorbereitenAsync(einsatz, ct);
            return Page();
        }

        DarfBeteiligteDetails = await DarfAsync(Berechtigungen.EinsaetzeBeteiligte);
        Uebernehmen(einsatz, zeiten);
        FahrzeugeUndTeilnehmerUebernehmen(einsatz, fahrzeugzeiten, teilnehmerzeiten);
        OrganisationenUebernehmen(einsatz);
        BeteiligteUebernehmen(einsatz);
        AusruestungUebernehmen(einsatz);
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

    /// <summary>Nur bekannte Fahrzeuge, Mitglieder, Kostensätze und Kategorien annehmen; manipulierte Formulare sollen nicht an Fremdschlüsseln scheitern.</summary>
    private async Task AuswahlPruefenAsync(CancellationToken ct)
    {
        var fahrzeuge = (await db.Fahrzeuge.Select(f => f.Id).ToListAsync(ct)).ToHashSet();
        var mitglieder = (await db.Mitglieder.Select(m => m.Id).ToListAsync(ct)).ToHashSet();
        var kostensaetze = (await db.Kostensaetze.Select(k => k.Id).ToListAsync(ct)).ToHashSet();
        Formular.Fahrzeuge.RemoveAll(f => !fahrzeuge.Contains(f.FahrzeugId));
        Formular.Teilnehmer.RemoveAll(t => !mitglieder.Contains(t.MitgliedId));
        Formular.Ausruestung.RemoveAll(a => !kostensaetze.Contains(a.KostensatzId));
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
    /// Liest ein Zeitfeld. Ein unverändertes Feld behält seinen gespeicherten Wert, weil die Ortszeit
    /// beim Wechsel zur Winterzeit (02:00–03:00) doppelt vorkommt und sonst um eine Stunde rutschen könnte.
    /// </summary>
    private DateTimeOffset? ZeitLesen(string feld, string? text, DateTimeOffset? bezug, DateTimeOffset? gespeichert)
    {
        var eingabe = (text ?? "").Trim();
        if (gespeichert is not null && (eingabe == ZeitEingabe.Text(gespeichert) || eingabe == ZeitKurz(gespeichert, bezug)))
        {
            return gespeichert;
        }

        if (ZeitEingabe.TryLesen(text, bezug, out var wert))
        {
            return wert;
        }

        ModelState.AddModelError(feld, "Bitte als TT.MM.JJJJ HH:MM oder nur HH:MM eingeben.");
        return null;
    }

    private (DateTimeOffset Alarmiert, DateTimeOffset? Ausgerueckt, DateTimeOffset? Eintreffen, DateTimeOffset? Ende, DateTimeOffset? Einsatzbereit) ZeitenLesen(Einsatz bisher)
    {
        var alarmiert = ZeitLesen($"Formular.{nameof(Eingabe.Alarmiert)}", Formular.Alarmiert, null, bisher.Alarmiert);
        if (alarmiert is null && !string.IsNullOrWhiteSpace(Formular.Alarmiert))
        {
            ModelState.Remove($"Formular.{nameof(Eingabe.Alarmiert)}");
            ModelState.AddModelError($"Formular.{nameof(Eingabe.Alarmiert)}", "Alarmzeit bitte mit Datum: TT.MM.JJJJ HH:MM.");
        }

        return (
            alarmiert ?? default,
            ZeitLesen($"Formular.{nameof(Eingabe.Ausgerueckt)}", Formular.Ausgerueckt, alarmiert, bisher.Ausgerueckt),
            ZeitLesen($"Formular.{nameof(Eingabe.EintreffenEinsatzort)}", Formular.EintreffenEinsatzort, alarmiert, bisher.EintreffenEinsatzort),
            ZeitLesen($"Formular.{nameof(Eingabe.Einsatzende)}", Formular.Einsatzende, alarmiert, bisher.Einsatzende),
            ZeitLesen($"Formular.{nameof(Eingabe.EinsatzbereitschaftHergestellt)}", Formular.EinsatzbereitschaftHergestellt, alarmiert, bisher.EinsatzbereitschaftHergestellt));
    }

    private sealed record Fahrzeugzeiten(DateTimeOffset? Ausgerueckt, DateTimeOffset? Eingetroffen, DateTimeOffset? Zurueck, DateTimeOffset? Einsatzbereit);

    private Dictionary<Guid, Fahrzeugzeiten> FahrzeugzeitenLesen(Einsatz bisher, DateTimeOffset alarmiert)
    {
        var ergebnis = new Dictionary<Guid, Fahrzeugzeiten>();
        for (var i = 0; i < Formular.Fahrzeuge.Count; i++)
        {
            var f = Formular.Fahrzeuge[i];
            var alt = bisher.Fahrzeuge.FirstOrDefault(x => x.FahrzeugId == f.FahrzeugId);
            ergebnis[f.FahrzeugId] = new(
                ZeitLesen($"Formular.Fahrzeuge[{i}].Ausgerueckt", f.Ausgerueckt, alarmiert, alt?.Ausgerueckt),
                ZeitLesen($"Formular.Fahrzeuge[{i}].Eingetroffen", f.Eingetroffen, alarmiert, alt?.Eingetroffen),
                ZeitLesen($"Formular.Fahrzeuge[{i}].Zurueck", f.Zurueck, alarmiert, alt?.Zurueck),
                ZeitLesen($"Formular.Fahrzeuge[{i}].Einsatzbereit", f.Einsatzbereit, alarmiert, alt?.Einsatzbereit));
        }

        return ergebnis;
    }

    private Dictionary<Guid, (DateTimeOffset? Von, DateTimeOffset? Bis)> TeilnehmerzeitenLesen(Einsatz bisher, DateTimeOffset alarmiert)
    {
        var ergebnis = new Dictionary<Guid, (DateTimeOffset?, DateTimeOffset?)>();
        for (var i = 0; i < Formular.Teilnehmer.Count; i++)
        {
            var t = Formular.Teilnehmer[i];
            var alt = bisher.Teilnahmen.FirstOrDefault(x => x.MitgliedId == t.MitgliedId);
            var von = ZeitLesen($"Formular.Teilnehmer[{i}].Von", t.Von, alarmiert, alt?.Von);
            var bis = ZeitLesen($"Formular.Teilnehmer[{i}].Bis", t.Bis, von ?? alarmiert, alt?.Bis);
            ergebnis[t.MitgliedId] = (von, bis);
        }

        return ergebnis;
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
        einsatz.KostenersatzOffen = f.KostenersatzOffen;
        einsatz.KostenersatzAbgeschlossenAm = f.KostenersatzAbgeschlossenAm;
    }

    /// <summary>Ein Fahrzeug, auf dem jemand saß, gehört zum Einsatz, auch wenn es nicht als Zeile da ist.</summary>
    private void FahrzeugeUndTeilnehmerUebernehmen(Einsatz einsatz, Dictionary<Guid, Fahrzeugzeiten> fahrzeugzeiten, Dictionary<Guid, (DateTimeOffset? Von, DateTimeOffset? Bis)> teilnehmerzeiten)
    {
        var benoetigt = Formular.Fahrzeuge.Select(f => f.FahrzeugId)
            .Concat(Formular.Teilnehmer.Where(t => t.FahrzeugId is not null).Select(t => t.FahrzeugId!.Value))
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
            if (fahrzeugzeiten.TryGetValue(fahrzeugId, out var z))
            {
                eintrag.Ausgerueckt = z.Ausgerueckt;
                eintrag.Eingetroffen = z.Eingetroffen;
                eintrag.Zurueck = z.Zurueck;
                eintrag.Einsatzbereit = z.Einsatzbereit;
            }
        }

        var dabei = Formular.Teilnehmer.Select(t => t.MitgliedId).ToHashSet();
        foreach (var vorhanden in einsatz.Teilnahmen.Where(t => !dabei.Contains(t.MitgliedId)).ToList())
        {
            db.EinsatzTeilnahmen.Remove(vorhanden);
            einsatz.Teilnahmen.Remove(vorhanden);
        }

        foreach (var t in Formular.Teilnehmer)
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
            if (teilnehmerzeiten.TryGetValue(t.MitgliedId, out var z))
            {
                eintrag.Von = z.Von;
                eintrag.Bis = z.Bis;
            }
        }
    }

    /// <summary>Zeilen mit Id werden aktualisiert, ohne Id neu angelegt, fehlende gelöscht.</summary>
    private static void ZeilenAbgleichen<TEntitaet, TEingabe>(List<TEntitaet> vorhanden, List<TEingabe> eingaben, Func<TEingabe, Guid?> id,
        Func<TEingabe, TEntitaet> neu, Action<TEntitaet, TEingabe> uebernehmen, DbSet<TEntitaet> set)
        where TEntitaet : Entitaet
    {
        var ids = eingaben.Select(id).Where(i => i is not null).ToHashSet();
        foreach (var alt in vorhanden.Where(v => !ids.Contains(v.Id)).ToList())
        {
            set.Remove(alt);
            vorhanden.Remove(alt);
        }

        foreach (var e in eingaben)
        {
            var eintrag = vorhanden.FirstOrDefault(v => v.Id == id(e));
            if (eintrag is null)
            {
                eintrag = neu(e);
                set.Add(eintrag);
            }

            uebernehmen(eintrag, e);
        }
    }

    private void OrganisationenUebernehmen(Einsatz einsatz) =>
        ZeilenAbgleichen(einsatz.Organisationen, Formular.Organisationen, o => o.Id,
            _ => new EinsatzOrganisation { EinsatzId = einsatz.Id },
            (o, e) =>
            {
                o.Art = e.Art;
                o.Bezeichnung = Leer(e.Bezeichnung);
                o.Ansprechpartner = Leer(e.Ansprechpartner);
                o.Telefon = Leer(e.Telefon);
                o.Aktenzeichen = Leer(e.Aktenzeichen);
                o.Bemerkung = Leer(e.Bemerkung);
            }, db.EinsatzOrganisationen);

    private void BeteiligteUebernehmen(Einsatz einsatz) =>
        ZeilenAbgleichen(einsatz.Beteiligte, Formular.Beteiligte, b => b.Id,
            e => new EinsatzBeteiligter { EinsatzId = einsatz.Id, Name = e.Name.Trim() },
            (b, e) =>
            {
                b.Rolle = e.Rolle;
                b.Name = e.Name.Trim();
                if (!DarfBeteiligteDetails)
                {
                    // Die Felder standen nicht im Formular; gespeicherte Angaben bleiben, wie sie sind.
                    return;
                }

                b.Anschrift = Leer(e.Anschrift);
                b.Telefon = Leer(e.Telefon);
                b.Kennzeichen = Leer(e.Kennzeichen)?.ToUpperInvariant();
                b.Fahrzeugbeschreibung = Leer(e.Fahrzeugbeschreibung);
                b.Versicherung = Leer(e.Versicherung);
                b.Versicherungsnummer = Leer(e.Versicherungsnummer);
                b.Bemerkung = Leer(e.Bemerkung);
            }, db.EinsatzBeteiligte);

    private void AusruestungUebernehmen(Einsatz einsatz) =>
        ZeilenAbgleichen(einsatz.Ausruestung, Formular.Ausruestung, a => a.Id,
            _ => new EinsatzAusruestung { EinsatzId = einsatz.Id },
            (a, e) =>
            {
                a.KostensatzId = e.KostensatzId;
                a.Menge = e.Menge;
                a.Bemerkung = Leer(e.Bemerkung);
            }, db.EinsatzAusruestungen);

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
        db.Einsaetze
            .Include(e => e.Fahrzeuge)
            .Include(e => e.Teilnahmen)
            .Include(e => e.Organisationen)
            .Include(e => e.Beteiligte)
            .Include(e => e.Ausruestung).ThenInclude(a => a.Kostensatz)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    /// <summary>Uhrzeit allein, wenn die Zeit am Tag des Bezugs liegt; sonst mit Datum.</summary>
    public static string ZeitKurz(DateTimeOffset? zeit, DateTimeOffset? bezug)
    {
        if (zeit is null)
        {
            return "";
        }

        return bezug is not null && Anzeige.LokalesDatum(zeit.Value) == Anzeige.LokalesDatum(bezug.Value)
            ? Anzeige.Lokal(zeit.Value).ToString("HH:mm")
            : ZeitEingabe.Text(zeit);
    }

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
        KostenersatzOffen = e.KostenersatzOffen,
        KostenersatzAbgeschlossenAm = e.KostenersatzAbgeschlossenAm,
        Fahrzeuge = e.Fahrzeuge.Select(f => new FahrzeugEingabe
        {
            FahrzeugId = f.FahrzeugId,
            Ausgerueckt = ZeitKurz(f.Ausgerueckt, e.Alarmiert),
            Eingetroffen = ZeitKurz(f.Eingetroffen, e.Alarmiert),
            Zurueck = ZeitKurz(f.Zurueck, e.Alarmiert),
            Einsatzbereit = ZeitKurz(f.Einsatzbereit, e.Alarmiert),
            Kilometer = f.Kilometer,
        }).ToList(),
        Teilnehmer = e.Teilnahmen.Select(t => new TeilnehmerEingabe
        {
            MitgliedId = t.MitgliedId,
            FahrzeugId = e.Fahrzeuge.FirstOrDefault(f => f.Id == t.EinsatzFahrzeugId)?.FahrzeugId,
            Funktion = t.Funktion,
            Von = ZeitKurz(t.Von, e.Alarmiert),
            Bis = ZeitKurz(t.Bis, e.Alarmiert),
            Atemschutz = t.Atemschutz,
            AtemschutzMinuten = t.AtemschutzMinuten,
        }).ToList(),
        Organisationen = e.Organisationen.OrderBy(o => o.ErstelltAm).Select(o => new OrganisationEingabe
        {
            Id = o.Id, Art = o.Art, Bezeichnung = o.Bezeichnung, Ansprechpartner = o.Ansprechpartner,
            Telefon = o.Telefon, Aktenzeichen = o.Aktenzeichen, Bemerkung = o.Bemerkung,
        }).ToList(),
        Beteiligte = e.Beteiligte.OrderBy(b => b.ErstelltAm).Select(b => DarfBeteiligteDetails
            ? new BeteiligterEingabe
            {
                Id = b.Id, Rolle = b.Rolle, Name = b.Name, Anschrift = b.Anschrift, Telefon = b.Telefon,
                Kennzeichen = b.Kennzeichen, Fahrzeugbeschreibung = b.Fahrzeugbeschreibung,
                Versicherung = b.Versicherung, Versicherungsnummer = b.Versicherungsnummer, Bemerkung = b.Bemerkung,
            }
            : new BeteiligterEingabe { Id = b.Id, Rolle = b.Rolle, Name = b.Name }).ToList(),
        Ausruestung = e.Ausruestung.OrderBy(a => a.ErstelltAm).Select(a => new AusruestungEingabe
        {
            Id = a.Id, KostensatzId = a.KostensatzId, Menge = a.Menge, Bemerkung = a.Bemerkung,
        }).ToList(),
    };

    /// <summary>Auswahllisten laden; Formularzeilen gibt es nur für Fahrzeuge und Mitglieder, die dabei waren.</summary>
    private async Task AnzeigeVorbereitenAsync(Einsatz einsatz, CancellationToken ct)
    {
        Einsatz = einsatz;
        DarfFreigeben = await DarfAsync(Berechtigungen.EinsaetzeFreigeben);
        DarfAendern = await DarfAendernAsync(einsatz);
        DarfBeteiligteDetails = await DarfAsync(Berechtigungen.EinsaetzeBeteiligte);
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
        var fahrzeugIds = einsatz.Fahrzeuge.Select(f => f.FahrzeugId).Concat(Formular.Fahrzeuge.Select(f => f.FahrzeugId)).ToList();
        var fahrzeuge = await db.Fahrzeuge.AsNoTracking()
            .Where(f => (f.AusserDienstAm == null || f.AusserDienstAm >= tag) || fahrzeugIds.Contains(f.Id))
            .OrderBy(f => f.Funkrufname)
            .Select(f => new { f.Id, Name = f.Funkrufname + " (" + f.Typ + ")" })
            .ToListAsync(ct);
        Fahrzeugnamen = fahrzeuge.ToDictionary(f => f.Id, f => f.Name);
        // Besatzung lässt sich nur Fahrzeugen zuordnen, die als Zeile da sind.
        var dabeiFahrzeuge = Formular.Fahrzeuge.Select(f => f.FahrzeugId).ToHashSet();
        Fahrzeugauswahl = fahrzeuge.Where(f => dabeiFahrzeuge.Contains(f.Id)).Select(f => new SelectListItem(f.Name, f.Id.ToString())).ToList();
        FahrzeugeZumHinzufuegen = fahrzeuge.Where(f => !dabeiFahrzeuge.Contains(f.Id)).Select(f => new SelectListItem(f.Name, f.Id.ToString())).ToList();

        // Wer nur liest (z. B. die Gemeinde), sieht nur die Beteiligten, nicht die ganze Mitgliederliste.
        var mitgliedIds = einsatz.Teilnahmen.Select(t => t.MitgliedId).Concat(Formular.Teilnehmer.Select(t => t.MitgliedId)).ToList();
        var alleAktiven = DarfAendern;
        var mitglieder = await db.Mitglieder.AsNoTracking()
            .Where(m => (alleAktiven && m.Mitgliedschaften.Any(s => s.GueltigAb <= tag && (s.GueltigBis == null || s.GueltigBis >= tag)))
                || mitgliedIds.Contains(m.Id) || m.Id == einsatz.EinsatzleiterId)
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new { m.Id, Name = m.Nachname + ", " + m.Vorname })
            .ToListAsync(ct);
        Mitgliedernamen = mitglieder.ToDictionary(m => m.Id, m => m.Name);
        Mitgliederauswahl = mitglieder.Select(m => new SelectListItem(m.Name, m.Id.ToString())).ToList();
        var dabeiMitglieder = Formular.Teilnehmer.Select(t => t.MitgliedId).ToHashSet();
        MitgliederZumHinzufuegen = mitglieder.Where(m => !dabeiMitglieder.Contains(m.Id)).Select(m => new SelectListItem(m.Name, m.Id.ToString())).ToList();

        // Zeilen zu unbekannten Fahrzeugen oder Mitgliedern würden die Anzeige sprengen.
        Formular.Fahrzeuge.RemoveAll(f => !Fahrzeugnamen.ContainsKey(f.FahrzeugId));
        Formular.Teilnehmer.RemoveAll(t => !Mitgliedernamen.ContainsKey(t.MitgliedId));

        var saetze = await db.Kostensaetze.AsNoTracking().OrderBy(k => k.Art).ThenBy(k => k.Bezeichnung).ToListAsync(ct);
        Kostensaetze = saetze.ToDictionary(k => k.Id);
        Formular.Ausruestung.RemoveAll(a => !Kostensaetze.ContainsKey(a.KostensatzId));
        Kostensatzauswahl = saetze
            .Where(k => k.Art is KostensatzArt.Geraet or KostensatzArt.Verbrauch && k.IstGueltigAm(tag))
            .Select(k => new SelectListItem($"{k.Bezeichnung} ({Anzeige.Euro(k.Betrag)} je {Anzeige.Text(k.Einheit)})", k.Id.ToString()))
            .ToList();

        await KostenBerechnenAsync(einsatz, saetze, tag, ct);
    }

    /// <summary>Kostenersatz und Entschädigung aus dem gespeicherten Stand, nicht aus dem noch ungespeicherten Formular.</summary>
    private async Task KostenBerechnenAsync(Einsatz einsatz, List<Kostensatz> saetze, DateOnly tag, CancellationToken ct)
    {
        RundungMinuten = await parameter.ZahlAsync(ParameterSchluessel.KostenersatzRundungMinuten, tag, 30, ct);
        if (saetze.Count > 0)
        {
            Kosten = Kostenersatz.Berechnen(einsatz, saetze, RundungMinuten, id => Fahrzeugnamen.GetValueOrDefault(id, "Fahrzeug"));
        }

        if (einsatz.Teilnahmen.Count == 0)
        {
            return;
        }

        var parameterListe = await db.Parameter.AsNoTracking().Where(p => p.Schluessel.StartsWith(Entschaedigungssaetze.Praefix)).ToListAsync(ct);
        var regel = new Entschaedigungssaetze(parameterListe).Regel(Entschaedigungssaetze.VorgangEinsatz, tag);
        EntschaedigungOhneSatz = regel is null || !regel.Verguetet;
        var ende = einsatz.EinsatzbereitschaftHergestellt ?? einsatz.Einsatzende;
        Entschaedigungen = einsatz.Teilnahmen
            .Select(t =>
            {
                var von = t.Von ?? einsatz.Alarmiert;
                var bis = t.Bis ?? ende;
                var betrag = regel is null || EntschaedigungOhneSatz || bis is null || bis <= von
                    ? null
                    : regel.Berechnen(bis.Value - von, Ortszeit.Kalendertage(von, bis.Value));
                return new EntschaedigungZeile(Mitgliedernamen.GetValueOrDefault(t.MitgliedId, "Mitglied"), von, bis, betrag);
            })
            .OrderBy(z => z.Name)
            .ToList();
    }

    private async Task<bool> DarfAendernAsync(Einsatz einsatz) =>
        await DarfAsync(einsatz.Status == EinsatzStatus.Freigegeben ? Berechtigungen.EinsaetzeFreigeben : Berechtigungen.EinsaetzeBearbeiten);

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
