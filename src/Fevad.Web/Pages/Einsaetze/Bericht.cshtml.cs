using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Atemschutz;
using Fevad.Infrastructure.Einsaetze;
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
public class BerichtModel(AppDbContext db, TimeProvider zeit, IAuthorizationService autorisierung, IAktuellerBenutzer aktuellerBenutzer, ParameterLeser parameter, Atemschutzuebernahme atemschutz) : PageModel
{
    /// <summary>Eingaben des Berichtsformulars.</summary>
    [BindProperty]
    public Eingabe Formular { get; set; } = new();

    /// <summary>Der geladene Einsatz.</summary>
    public Einsatz? Einsatz { get; private set; }
    /// <summary>Darf den Bericht ändern.</summary>
    public bool DarfAendern { get; private set; }
    /// <summary>Darf den Bericht freigeben und die Freigabe zurücknehmen.</summary>
    public bool DarfFreigeben { get; private set; }

    /// <summary>Anschrift, Telefon, Kennzeichen und Versicherung der Beteiligten; ohne das Recht nur Rolle und Name.</summary>
    public bool DarfBeteiligteDetails { get; private set; }
    /// <summary>Der Bericht wurde aus einem FE2-Alarm angelegt.</summary>
    public bool AusAlarm { get; private set; }
    /// <summary>Name der Person, die freigegeben hat.</summary>
    public string? FreigegebenVon { get; private set; }
    /// <summary>Was einer Freigabe noch im Weg steht.</summary>
    public IReadOnlyList<string> Hindernisse { get; private set; } = [];
    /// <summary>Auswahl der Statistikkategorien.</summary>
    public IReadOnlyList<SelectListItem> Kategorien { get; private set; } = [];
    /// <summary>Auswahl der Fahrzeuge im Bericht für die Teilnehmer.</summary>
    public IReadOnlyList<SelectListItem> Fahrzeugauswahl { get; private set; } = [];
    /// <summary>Fahrzeuge, die noch nicht im Bericht stehen.</summary>
    public IReadOnlyList<SelectListItem> FahrzeugeZumHinzufuegen { get; private set; } = [];
    /// <summary>Auswahl der Mitglieder, z. B. für den Einsatzleiter.</summary>
    public IReadOnlyList<SelectListItem> Mitgliederauswahl { get; private set; } = [];
    /// <summary>Mitglieder, die noch nicht als Teilnehmer im Bericht stehen.</summary>
    public IReadOnlyList<SelectListItem> MitgliederZumHinzufuegen { get; private set; } = [];
    /// <summary>Auswahl der Kostensätze für Geräte und Verbrauchsmaterial.</summary>
    public IReadOnlyList<SelectListItem> Kostensatzauswahl { get; private set; } = [];

    /// <summary>Gefahrstoffgruppen aus dem Katalog für den Expositionsblock.</summary>
    public IReadOnlyList<string> Gefahrstoffauswahl { get; private set; } = [];

    /// <summary>
    /// Exposition, Gefahrstoffe, PA- und Maskennummer je Teilnehmer sieht und ändert nur, wer Atemschutz lesen darf; andere Rollen
    /// (Gemeinde, Führungskraft) sehen den Block nicht, und beim Speichern bleiben die gespeicherten Werte stehen.
    /// </summary>
    public bool DarfAtemschutz { get; private set; }

    /// <summary>Inventarnummern der Geräte als Vorschlag für Pressluftatmer und Maske.</summary>
    public IReadOnlyList<string> Geraetenummern { get; private set; } = [];

    /// <summary>Die gewählte Einsatzart verlangt die Exposition je Teilnehmer.</summary>
    public bool ExpositionPflicht { get; private set; }
    /// <summary>Anzeigenamen der Fahrzeuge nach Kennung.</summary>
    public IReadOnlyDictionary<Guid, string> Fahrzeugnamen { get; private set; } = new Dictionary<Guid, string>();

    private Dictionary<Guid, string?> fahrzeugklassen = [];

    /// <summary>Darf den Kostenersatz-Vorgang sehen.</summary>
    public bool DarfKostenersatz { get; private set; }
    /// <summary>Anzeigenamen der Mitglieder nach Kennung.</summary>
    public IReadOnlyDictionary<Guid, string> Mitgliedernamen { get; private set; } = new Dictionary<Guid, string>();
    /// <summary>Kostensätze nach Kennung.</summary>
    public IReadOnlyDictionary<Guid, Kostensatz> Kostensaetze { get; private set; } = new Dictionary<Guid, Kostensatz>();

    /// <summary>Kostenersatz aus dem gespeicherten Stand; null, wenn noch keine Sätze hinterlegt sind.</summary>
    public Kostenaufstellung? Kosten { get; private set; }
    /// <summary>Rundungseinheit für angefangene Zeiten in Minuten (Parameter).</summary>
    public int RundungMinuten { get; private set; }
    /// <summary>Entschädigung je Teilnehmer nach der Entschädigungssatzung.</summary>
    public IReadOnlyList<EntschaedigungZeile> Entschaedigungen { get; private set; } = [];
    /// <summary>Für den Einsatz ist kein Entschädigungssatz hinterlegt.</summary>
    public bool EntschaedigungOhneSatz { get; private set; }

    /// <summary>Entschädigung eines Teilnehmers.</summary>
    /// <param name="Name">Name des Teilnehmers.</param>
    /// <param name="Von">Beginn der Teilnahme.</param>
    /// <param name="Bis">Ende der Teilnahme, falls bekannt.</param>
    /// <param name="Betrag">Berechnete Entschädigung; null ohne Endzeit oder Satz.</param>
    public sealed record EntschaedigungZeile(string Name, DateTimeOffset Von, DateTimeOffset? Bis, Entschaedigungsbetrag? Betrag);

    /// <summary>Auswahl der Rechtsgrundlagen.</summary>
    public static IReadOnlyList<SelectListItem> Rechtsgrundlagen { get; } =
    [
        new("§ 2 Abs. 1 FwG (Pflichtaufgabe)", nameof(Rechtsgrundlage.Para2Abs1)),
        new("§ 2 Abs. 2 FwG (weitere Aufgabe, kostenersatzpflichtig)", nameof(Rechtsgrundlage.Para2Abs2)),
    ];

    /// <summary>Auswahl der Organisationsarten.</summary>
    public static IReadOnlyList<SelectListItem> Organisationsarten { get; } =
        Enum.GetValues<OrganisationArt>().Select(a => new SelectListItem(Anzeige.Text(a), a.ToString())).ToList();

    /// <summary>Auswahl der Rollen von Beteiligten.</summary>
    public static IReadOnlyList<SelectListItem> Beteiligtenrollen { get; } =
        Enum.GetValues<BeteiligtenRolle>().Select(r => new SelectListItem(Anzeige.Text(r), r.ToString())).ToList();

    /// <summary>Formular des Einsatzberichts; Zeiten als Text „TT.MM.JJJJ HH:MM“.</summary>
    public sealed class Eingabe
    {
        /// <summary>xmin beim Laden; erkennt gleichzeitige Änderungen.</summary>
        public uint Version { get; set; }

        /// <summary>Einsatzstichwort.</summary>
        [StringLength(50), Display(Name = "Stichwort")] public string? Stichwort { get; set; }
        /// <summary>Beschreibung des Stichworts.</summary>
        [Display(Name = "Stichwort-Beschreibung")] public string? StichwortBeschreibung { get; set; }
        /// <summary>Alarmmeldung.</summary>
        [Display(Name = "Meldung")] public string? Meldung { get; set; }

        /// <summary>Straße des Einsatzorts.</summary>
        [Display(Name = "Straße")] public string? Strasse { get; set; }
        /// <summary>Hausnummer.</summary>
        [Display(Name = "Hausnummer")] public string? Hausnummer { get; set; }
        /// <summary>Postleitzahl.</summary>
        [Display(Name = "PLZ")] public string? Plz { get; set; }
        /// <summary>Ort.</summary>
        [Display(Name = "Ort")] public string? Ort { get; set; }
        /// <summary>Ortsteil.</summary>
        [Display(Name = "Ortsteil")] public string? Ortsteil { get; set; }
        /// <summary>Objekt am Einsatzort.</summary>
        [Display(Name = "Objekt")] public string? Objekt { get; set; }

        /// <summary>Alarmzeit.</summary>
        [Required(ErrorMessage = "Alarmzeit fehlt."), Display(Name = "Alarmiert")] public string Alarmiert { get; set; } = "";
        /// <summary>Ausrücken des ersten Fahrzeugs.</summary>
        [Display(Name = "Ausgerückt")] public string? Ausgerueckt { get; set; }
        /// <summary>Eintreffen am Einsatzort.</summary>
        [Display(Name = "Eintreffen Einsatzort")] public string? EintreffenEinsatzort { get; set; }
        /// <summary>Ende des Einsatzes.</summary>
        [Display(Name = "Einsatzende")] public string? Einsatzende { get; set; }
        /// <summary>Einsatzbereitschaft wiederhergestellt.</summary>
        [Display(Name = "Einsatzbereit")] public string? EinsatzbereitschaftHergestellt { get; set; }

        /// <summary>Statistikkategorie.</summary>
        [Display(Name = "Statistik-Kategorie")] public Guid? StatistikKategorieId { get; set; }
        /// <summary>Rechtsgrundlage des Einsatzes.</summary>
        [Display(Name = "Rechtsgrundlage")] public Rechtsgrundlage Rechtsgrundlage { get; set; }
        /// <summary>Einsatzleiter.</summary>
        [Display(Name = "Einsatzleiter")] public Guid? EinsatzleiterId { get; set; }

        /// <summary>Lagebericht.</summary>
        [Display(Name = "Lagebericht")] public string? Lagebericht { get; set; }
        /// <summary>Getroffene Maßnahmen.</summary>
        [Display(Name = "Maßnahmen")] public string? Massnahmen { get; set; }

        /// <summary>Zahl der geretteten Personen.</summary>
        [Range(0, 999, ErrorMessage = "0 bis 999."), Display(Name = "Gerettete")] public int Gerettete { get; set; }
        /// <summary>Zahl der verletzten Personen.</summary>
        [Range(0, 999, ErrorMessage = "0 bis 999."), Display(Name = "Verletzte")] public int Verletzte { get; set; }
        /// <summary>Zahl der Toten.</summary>
        [Range(0, 999, ErrorMessage = "0 bis 999."), Display(Name = "Tote")] public int Tote { get; set; }
        /// <summary>Zahl der verletzten Einsatzkräfte.</summary>
        [Range(0, 999, ErrorMessage = "0 bis 999."), Display(Name = "Verletzte Einsatzkräfte")] public int VerletzteFeuerwehr { get; set; }
        /// <summary>Überörtliche Hilfe geleistet.</summary>
        [Display(Name = "Überörtliche Hilfe")] public bool Ueberoertlich { get; set; }
        /// <summary>Kostenersatz ist noch zu bearbeiten.</summary>
        [Display(Name = "Kostenersatz offen")] public bool KostenersatzOffen { get; set; }
        /// <summary>Tag, an dem der Kostenersatz abgeschlossen wurde.</summary>
        [Display(Name = "Kostenersatz abgeschlossen am")] public DateOnly? KostenersatzAbgeschlossenAm { get; set; }

        /// <summary>Eingesetzte Fahrzeuge.</summary>
        public List<FahrzeugEingabe> Fahrzeuge { get; set; } = [];
        /// <summary>Teilnehmende Mitglieder.</summary>
        public List<TeilnehmerEingabe> Teilnehmer { get; set; } = [];
        /// <summary>Beteiligte Organisationen.</summary>
        public List<OrganisationEingabe> Organisationen { get; set; } = [];
        /// <summary>Beteiligte Personen.</summary>
        public List<BeteiligterEingabe> Beteiligte { get; set; } = [];
        /// <summary>Eingesetzte Geräte und Verbrauchsmaterial.</summary>
        public List<AusruestungEingabe> Ausruestung { get; set; } = [];
    }

    /// <summary>Eingabezeile für ein Fahrzeug.</summary>
    public sealed class FahrzeugEingabe
    {
        /// <summary>Kennung des Fahrzeugs.</summary>
        public Guid FahrzeugId { get; set; }
        /// <summary>Ausrücken.</summary>
        [Display(Name = "Ausgerückt")] public string? Ausgerueckt { get; set; }
        /// <summary>Eintreffen am Einsatzort.</summary>
        [Display(Name = "Eingetroffen")] public string? Eingetroffen { get; set; }
        /// <summary>Rückkehr ins Gerätehaus.</summary>
        [Display(Name = "Zurück")] public string? Zurueck { get; set; }
        /// <summary>Wieder einsatzbereit.</summary>
        [Display(Name = "Einsatzbereit")] public string? Einsatzbereit { get; set; }
        /// <summary>Gefahrene Kilometer.</summary>
        [Range(0, 9999, ErrorMessage = "0 bis 9999 km.")] public int? Kilometer { get; set; }
    }

    /// <summary>Eingabezeile für einen Teilnehmer.</summary>
    public sealed class TeilnehmerEingabe
    {
        /// <summary>Kennung des Mitglieds.</summary>
        public Guid MitgliedId { get; set; }
        /// <summary>Fahrzeug, auf dem das Mitglied war.</summary>
        public Guid? FahrzeugId { get; set; }
        /// <summary>Funktion im Einsatz, z. B. Maschinist.</summary>
        [StringLength(50)] public string? Funktion { get; set; }
        /// <summary>Beginn der Teilnahme.</summary>
        [Display(Name = "Von")] public string? Von { get; set; }
        /// <summary>Ende der Teilnahme.</summary>
        [Display(Name = "Bis")] public string? Bis { get; set; }
        /// <summary>Unter Atemschutz eingesetzt.</summary>
        public bool Atemschutz { get; set; }
        /// <summary>Exposition; leer heißt nicht erfasst.</summary>
        [Range(0, 600, ErrorMessage = "0 bis 600 Minuten.")] public int? AtemschutzMinuten { get; set; }
        /// <summary>Nummer des Pressluftatmers.</summary>
        [StringLength(50)] public string? PaGeraet { get; set; }
        /// <summary>Nummer der Maske.</summary>
        [StringLength(50)] public string? Maske { get; set; }
        /// <summary>Angabe zur Exposition.</summary>
        public ExpositionStatus? Exposition { get; set; }
        /// <summary>Gefahrstoffgruppen aus dem Katalog.</summary>
        public List<string> Gefahrstoffe { get; set; } = [];
        /// <summary>Hautkontakt oder beaufschlagte Schutzkleidung.</summary>
        public bool Hautkontakt { get; set; }
        /// <summary>Dekontamination an der Einsatzstelle durchgeführt.</summary>
        public bool Dekontaminiert { get; set; }
    }

    /// <summary>Eingabezeile für eine beteiligte Organisation.</summary>
    public sealed class OrganisationEingabe
    {
        /// <summary>Kennung der gespeicherten Zeile; leer bei neuen Zeilen.</summary>
        public Guid? Id { get; set; }
        /// <summary>Art der Organisation.</summary>
        [Display(Name = "Organisation")] public OrganisationArt Art { get; set; }
        /// <summary>Dienststelle oder Einheit.</summary>
        [StringLength(200), Display(Name = "Dienststelle / Einheit")] public string? Bezeichnung { get; set; }
        /// <summary>Ansprechpartner.</summary>
        [StringLength(200), Display(Name = "Ansprechpartner")] public string? Ansprechpartner { get; set; }
        /// <summary>Telefon.</summary>
        [StringLength(50), Display(Name = "Telefon")] public string? Telefon { get; set; }
        /// <summary>Aktenzeichen der Organisation.</summary>
        [StringLength(100), Display(Name = "Aktenzeichen")] public string? Aktenzeichen { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Eingabezeile für eine beteiligte Person.</summary>
    public sealed class BeteiligterEingabe
    {
        /// <summary>Kennung der gespeicherten Zeile; leer bei neuen Zeilen.</summary>
        public Guid? Id { get; set; }
        /// <summary>Rolle der Person.</summary>
        [Display(Name = "Rolle")] public BeteiligtenRolle Rolle { get; set; }
        /// <summary>Name oder Firma.</summary>
        [Required(ErrorMessage = "Name fehlt."), StringLength(200), Display(Name = "Name / Firma")] public string Name { get; set; } = "";
        /// <summary>Anschrift.</summary>
        [StringLength(300), Display(Name = "Anschrift")] public string? Anschrift { get; set; }
        /// <summary>Telefon.</summary>
        [StringLength(50), Display(Name = "Telefon")] public string? Telefon { get; set; }
        /// <summary>Kfz-Kennzeichen.</summary>
        [StringLength(20), Display(Name = "Kennzeichen")] public string? Kennzeichen { get; set; }
        /// <summary>Beschreibung des Fahrzeugs.</summary>
        [StringLength(200), Display(Name = "Fahrzeug")] public string? Fahrzeugbeschreibung { get; set; }
        /// <summary>Versicherung.</summary>
        [StringLength(200), Display(Name = "Versicherung")] public string? Versicherung { get; set; }
        /// <summary>Versicherungsnummer.</summary>
        [StringLength(100), Display(Name = "Versicherungsnummer")] public string? Versicherungsnummer { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(1000), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Eingabezeile für Geräte oder Verbrauchsmaterial.</summary>
    public sealed class AusruestungEingabe
    {
        /// <summary>Kennung der gespeicherten Zeile; leer bei neuen Zeilen.</summary>
        public Guid? Id { get; set; }
        /// <summary>Kostensatz des Postens.</summary>
        public Guid KostensatzId { get; set; }
        /// <summary>Menge in der Einheit des Satzes.</summary>
        [Range(0.01, 99999, ErrorMessage = "Menge größer 0."), Display(Name = "Menge")] public decimal Menge { get; set; } = 1;
        /// <summary>Bemerkung.</summary>
        [StringLength(500), Display(Name = "Bemerkung")] public string? Bemerkung { get; set; }
    }

    /// <summary>Zeigt den Bericht.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        // Verfolgt laden, sonst fehlt die Schattenspalte xmin für die Versionsprüfung.
        var einsatz = await LadenAsync(id, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        DarfBeteiligteDetails = await DarfAsync(Berechtigungen.EinsaetzeBeteiligte);
        DarfKostenersatz = await DarfAsync(Berechtigungen.KostenersatzLesen);
        Formular = AusEinsatz(einsatz);
        await AnzeigeVorbereitenAsync(einsatz, ct);
        return Page();
    }

    /// <summary>Kostenaufstellung als Grundlage für den Bescheid der Gemeinde; enthält Verursacher, daher nur mit Beteiligten-Recht.</summary>
    public async Task<IActionResult> OnGetKostenersatzAsync(Guid id, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.EinsaetzeBeteiligte))
        {
            return Forbid();
        }

        var einsatz = await LadenAsync(id, ct);
        if (einsatz is null)
        {
            return NotFound();
        }

        await AnzeigeVorbereitenAsync(einsatz, ct);
        if (Kosten is null)
        {
            TempData["Fehler"] = "Für den Kostenersatz sind noch keine Kostensätze hinterlegt.";
            return RedirectToPage(new { id });
        }

        var o = await db.OrganisationAsync(ct);
        var daten = new KostenersatzPdf.Daten(
            new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp), o.GemeindeName, o.GemeindeAnschrift,
            einsatz, Anschrift.Zeile($"{einsatz.Strasse} {einsatz.Hausnummer}".Trim(), einsatz.Plz, einsatz.Ort) ?? einsatz.Ort,
            einsatz.Beteiligte.Where(b => b.Rolle == BeteiligtenRolle.Verursacher).OrderBy(b => b.ErstelltAm)
                .Select(b => new KostenersatzPdf.Kostenpflichtiger(b.Name, b.Anschrift, b.Kennzeichen, b.Versicherung, b.Versicherungsnummer)).ToList(),
            Kosten, RundungMinuten);
        return File(KostenersatzPdf.Erzeugen(daten), "application/pdf", KostenersatzPdf.Dateiname(einsatz));
    }

    /// <summary>Speichert den Bericht.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, bei Fehlern die Seite, sonst 403/404.</returns>
    public Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct) => SpeichernAsync(id, freigeben: false, ct);

    /// <summary>Speichert und gibt den Bericht frei.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, bei Fehlern die Seite, sonst 403/404.</returns>
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

    /// <summary>Entfernt eine Zeile, ohne zu speichern; die übrigen Eingaben bleiben im Formular.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="was">Art der Zeile: fahrzeug, teilnehmer, organisation, beteiligter oder ausruestung.</param>
    /// <param name="index">Position der Zeile in ihrer Liste.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite, 403 oder 404.</returns>
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

    /// <summary>Nimmt die Freigabe zurück.</summary>
    /// <param name="id">Kennung des Einsatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung, 403 oder 404.</returns>
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
        DarfAtemschutz = await DarfAsync(Berechtigungen.AtemschutzLesen);
        Uebernehmen(einsatz, zeiten);
        FahrzeugeUndTeilnehmerUebernehmen(einsatz, fahrzeugzeiten, teilnehmerzeiten);
        OrganisationenUebernehmen(einsatz);
        BeteiligteUebernehmen(einsatz);
        AusruestungUebernehmen(einsatz);
        einsatz.InBearbeitungNehmen();

        // Neue Kinder hängen erst nach DetectChanges in den Navigationen; die Hindernisse zählen sie mit.
        db.ChangeTracker.DetectChanges();
        var expositionPflicht = await ExpositionPflichtAsync(einsatz.StatistikKategorieId, ct);
        var hindernisse = einsatz.FreigabeHindernisse(expositionPflicht);
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
            einsatz.Freigeben(aktuellerBenutzer.Id ?? Guid.Empty, zeit.GetUtcNow(), expositionPflicht);
        }

        // Atemschutz und Exposition gehen mit der Freigabe in die Nachweise; ein freigegebener Bericht gleicht bei jeder Änderung ab.
        var neueNachweise = einsatz.Status == EinsatzStatus.Freigegeben ? await atemschutz.AbgleichenAsync(einsatz, ct) : 0;

        // Auch reine Teilnehmer- oder Fahrzeugänderungen sollen xmin des Einsatzes prüfen und erhöhen.
        db.Entry(einsatz).State = EntityState.Modified;

        if (!await SicherSpeichernAsync(ct))
        {
            return await NeuAnzeigenAsync(id, ct);
        }

        if (freigeben && !freigegeben)
        {
            // Ohne Teilnehmer, Kategorie oder Einsatzende gibt es keine Freigabe (Andi, 25.10.2026); der Stand selbst ist gespeichert.
            TempData["Fehler"] = $"Gespeichert, aber nicht freigegeben: {string.Join(" ", hindernisse)}";
        }
        else
        {
            TempData["Meldung"] = (freigegeben ? $"Einsatz {einsatz.Einsatznummer} freigegeben." : $"Einsatz {einsatz.Einsatznummer} gespeichert.")
                + (neueNachweise > 0 ? $" {neueNachweise} Atemschutz- bzw. Expositionsnachweise angelegt." : "");
        }
        return RedirectToPage(new { id });
    }

    private async Task<bool> ExpositionPflichtAsync(Guid? kategorieId, CancellationToken ct) =>
        kategorieId is not null && await db.StatistikKategorien.AnyAsync(k => k.Id == kategorieId && k.ExpositionPflicht, ct);

    /// <summary>Nur bekannte Fahrzeuge, Mitglieder, Kostensätze und Kategorien annehmen; manipulierte Formulare sollen nicht an Fremdschlüsseln scheitern.</summary>
    private async Task AuswahlPruefenAsync(CancellationToken ct)
    {
        var stoffe = (await db.Gefahrstoffgruppen.Select(g => g.Bezeichnung).ToListAsync(ct)).ToHashSet();
        foreach (var t in Formular.Teilnehmer)
        {
            t.Gefahrstoffe = t.Gefahrstoffe.Where(stoffe.Contains).Distinct().ToList();
        }

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
            if (DarfAtemschutz)
            {
                eintrag.PaGeraet = t.Atemschutz ? Leer(t.PaGeraet) : null;
                eintrag.Maske = t.Atemschutz ? Leer(t.Maske) : null;
                var exponiert = t.Exposition == ExpositionStatus.Exponiert;
                eintrag.Exposition = t.Exposition;
                eintrag.Gefahrstoffe = exponiert && t.Gefahrstoffe.Count > 0 ? string.Join(", ", t.Gefahrstoffe) : null;
                eintrag.Hautkontakt = exponiert && t.Hautkontakt;
                eintrag.Dekontaminiert = exponiert && t.Dekontaminiert;
            }
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
            PaGeraet = t.PaGeraet,
            Maske = t.Maske,
            Exposition = t.Exposition,
            Gefahrstoffe = t.Gefahrstoffe?.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
            Hautkontakt = t.Hautkontakt,
            Dekontaminiert = t.Dekontaminiert,
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
        DarfAtemschutz = await DarfAsync(Berechtigungen.AtemschutzLesen);
        ExpositionPflicht = await ExpositionPflichtAsync(einsatz.StatistikKategorieId, ct);
        Hindernisse = einsatz.Status == EinsatzStatus.Freigegeben ? [] : einsatz.FreigabeHindernisse(ExpositionPflicht);
        Gefahrstoffauswahl = await db.Gefahrstoffgruppen.AsNoTracking().Where(g => g.Aktiv).OrderBy(g => g.Sortierung)
            .Select(g => g.Bezeichnung).ToListAsync(ct);
        Geraetenummern = DarfAendern && DarfAtemschutz
            ? await db.Geraete.AsNoTracking().Where(g => g.AusgesondertAm == null).OrderBy(g => g.Inventarnummer).Select(g => g.Inventarnummer).ToListAsync(ct)
            : [];
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
            .Select(f => new { f.Id, Name = f.Funkrufname + " (" + f.Typ + ")", f.Kostenklasse })
            .ToListAsync(ct);
        Fahrzeugnamen = fahrzeuge.ToDictionary(f => f.Id, f => f.Name);
        fahrzeugklassen = fahrzeuge.ToDictionary(f => f.Id, f => f.Kostenklasse);
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
            Kosten = Domain.Einsaetze.Kostenersatz.Berechnen(einsatz, saetze, RundungMinuten, id => Fahrzeugnamen.GetValueOrDefault(id, "Fahrzeug"), fahrzeugklassen.GetValueOrDefault);
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
