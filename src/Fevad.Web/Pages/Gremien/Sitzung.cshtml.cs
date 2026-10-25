using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Gremien;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Gremien;

/// <summary>Sitzung anlegen und bearbeiten: Einladung, Teilnehmer, Niederschrift, Beschlüsse, Wahlen.</summary>
public class SitzungModel(AppDbContext db, Gremienarbeit gremien, ParameterLeser parameter, IAuthorizationService autorisierung, TimeProvider zeit) : PageModel
{
    /// <summary>Formulardaten der Sitzung.</summary>
    [BindProperty] public Eingabe Formular { get; set; } = new();
    /// <summary>Teilnahmestatus je Mitglied aus dem Teilnahmeformular.</summary>
    [BindProperty] public List<TeilnahmeEingabe> Teilnahmen { get; set; } = [];
    /// <summary>Formulardaten für einen neuen Beschluss.</summary>
    [BindProperty] public BeschlussEingabe NeuerBeschluss { get; set; } = new();

    /// <summary>Die bearbeitete Sitzung; <c>null</c> beim Anlegen.</summary>
    public Sitzung? Sitzung { get; private set; }
    /// <summary>Die Seite legt eine neue Sitzung an.</summary>
    public bool IstNeu => Sitzung is null;
    /// <summary>Der Benutzer darf die Sitzung bearbeiten.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Aktive Mitglieder für Teilnahme und Protokollführung.</summary>
    public IReadOnlyList<Auswahl.Person> Mitglieder { get; private set; } = [];
    /// <summary>Dokumente der Ablage, die als unterschriebene Niederschrift verknüpft werden können.</summary>
    public IReadOnlyList<SelectListItem> Protokolldokumente { get; private set; } = [];
    /// <summary>Ob zwischen Einladung und Beginn die Einladungsfrist lag; <c>null</c>, wenn kein Einladungsdatum erfasst ist.</summary>
    public bool? EinladungsfristEingehalten { get; private set; }
    /// <summary>Einladungsfrist in Tagen für diese Sitzungsart (Parameter).</summary>
    public int EinladungsfristTage { get; private set; }
    /// <summary>Anwesende und erfasste Stimmberechtigte zur Prüfung der Beschlussfähigkeit.</summary>
    public (int Anwesend, int Stimmberechtigt) Beschlussfaehigkeit { get; private set; }

    /// <summary>Eingabefelder der Sitzung.</summary>
    public sealed class Eingabe
    {
        /// <summary>Zeilenversion zur Erkennung gleichzeitiger Änderungen.</summary>
        public uint Version { get; set; }
        /// <summary>Art der Sitzung.</summary>
        [Display(Name = "Art")] public SitzungArt Art { get; set; }
        /// <summary>Titel der Sitzung.</summary>
        [Required(ErrorMessage = "Titel fehlt."), StringLength(200), Display(Name = "Titel")] public string Titel { get; set; } = "";
        /// <summary>Beginn als „TT.MM.JJJJ HH:MM“ in Ortszeit.</summary>
        [Required(ErrorMessage = "Beginn fehlt."), Display(Name = "Beginn")] public string Beginn { get; set; } = "";
        /// <summary>Ende als „TT.MM.JJJJ HH:MM“ in Ortszeit, optional.</summary>
        [Display(Name = "Ende")] public string? Ende { get; set; }
        /// <summary>Ort der Sitzung.</summary>
        [StringLength(200), Display(Name = "Ort")] public string? Ort { get; set; }
        /// <summary>Tag, an dem eingeladen wurde.</summary>
        [Display(Name = "Eingeladen am")] public DateOnly? EingeladenAm { get; set; }
        /// <summary>Tagesordnung als Freitext.</summary>
        [Display(Name = "Tagesordnung")] public string? Tagesordnung { get; set; }
        /// <summary>Text der Niederschrift.</summary>
        [Display(Name = "Niederschrift")] public string? Protokoll { get; set; }
        /// <summary>Mitglied, das die Niederschrift führt.</summary>
        [Display(Name = "Protokollführer")] public Guid? ProtokollfuehrerId { get; set; }
        /// <summary>Verknüpftes Dokument mit der unterschriebenen Niederschrift.</summary>
        [Display(Name = "Unterschriebene Niederschrift (Dokument)")] public Guid? ProtokollDokumentId { get; set; }
        /// <summary>Bearbeitungsstand der Sitzung.</summary>
        [Display(Name = "Status")] public SitzungStatus Status { get; set; }
        /// <summary>Tag, an dem die Niederschrift genehmigt wurde.</summary>
        [Display(Name = "Niederschrift genehmigt am")] public DateOnly? ProtokollGenehmigtAm { get; set; }
        /// <summary>Tag, an dem die Niederschrift dem Bürgermeister vorgelegt wurde.</summary>
        [Display(Name = "Dem Bürgermeister vorgelegt am")] public DateOnly? DemBuergermeisterVorgelegtAm { get; set; }
    }

    /// <summary>Teilnahme eines Mitglieds an der Sitzung.</summary>
    public sealed class TeilnahmeEingabe
    {
        /// <summary>Kennung des Mitglieds.</summary>
        public Guid MitgliedId { get; set; }
        /// <summary>Teilnahmestatus; <c>null</c> entfernt die Teilnahme.</summary>
        public TeilnahmeStatus? Status { get; set; }
        /// <summary>Das Mitglied ist in dieser Sitzung stimmberechtigt.</summary>
        public bool Stimmberechtigt { get; set; }
    }

    /// <summary>Eingabefelder eines Beschlusses.</summary>
    public sealed class BeschlussEingabe
    {
        /// <summary>Tagesordnungspunkt, zu dem der Beschluss gehört.</summary>
        [StringLength(20), Display(Name = "TOP")] public string? Tagesordnungspunkt { get; set; }
        /// <summary>Wortlaut des Beschlusses.</summary>
        [Required(ErrorMessage = "Gegenstand fehlt."), StringLength(4000), Display(Name = "Beschluss")] public string Gegenstand { get; set; } = "";
        /// <summary>Ergebnis der Abstimmung.</summary>
        [Display(Name = "Ergebnis")] public BeschlussErgebnis? Ergebnis { get; set; }
        /// <summary>Anzahl der Ja-Stimmen.</summary>
        [Range(0, 999), Display(Name = "Ja")] public int? JaStimmen { get; set; }
        /// <summary>Anzahl der Nein-Stimmen.</summary>
        [Range(0, 999), Display(Name = "Nein")] public int? NeinStimmen { get; set; }
        /// <summary>Anzahl der Enthaltungen.</summary>
        [Range(0, 999), Display(Name = "Enthaltungen")] public int? Enthaltungen { get; set; }
        /// <summary>Frist für die Umsetzung; der Beschluss erscheint dann in der Wiedervorlage.</summary>
        [Display(Name = "Umsetzen bis")] public DateOnly? UmsetzenBis { get; set; }
    }

    /// <summary>Zeigt eine Sitzung oder ein leeres Formular für eine neue.</summary>
    /// <param name="id">Kennung der Sitzung; <c>null</c> legt neu an.</param>
    /// <param name="art">Vorbelegte Sitzungsart beim Anlegen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404, wenn die Sitzung fehlt.</returns>
    public async Task<IActionResult> OnGetAsync(Guid? id, SitzungArt? art, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (Sitzung is null)
        {
            if (!DarfBearbeiten)
            {
                return Forbid();
            }

            Formular.Art = art ?? SitzungArt.Ausschuss;
            Formular.Titel = Anzeige.Text(Formular.Art);
            return Page();
        }

        var s = Sitzung;
        Formular = new Eingabe
        {
            Version = db.Entry(s).Property<uint>("Version").CurrentValue,
            Art = s.Art,
            Titel = s.Titel,
            Beginn = ZeitEingabe.Text(s.Beginn),
            Ende = ZeitEingabe.Text(s.Ende),
            Ort = s.Ort,
            EingeladenAm = s.EingeladenAm,
            Tagesordnung = s.Tagesordnung,
            Protokoll = s.Protokoll,
            ProtokollfuehrerId = s.ProtokollfuehrerId,
            ProtokollDokumentId = s.ProtokollDokumentId,
            Status = s.Status,
            ProtokollGenehmigtAm = s.ProtokollGenehmigtAm,
            DemBuergermeisterVorgelegtAm = s.DemBuergermeisterVorgelegtAm,
        };
        return Page();
    }

    /// <summary>Speichert die Sitzung oder legt sie neu an.</summary>
    /// <param name="id">Kennung der Sitzung; <c>null</c> legt neu an.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur gespeicherten Sitzung oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostAsync(Guid? id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct))
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        NurPruefen("Formular.");
        DateTimeOffset? beginn = null;
        DateTimeOffset? ende = null;
        if (!ZeitEingabe.TryLesen(Formular.Beginn, null, out beginn) || beginn is null)
        {
            ModelState.AddModelError("Formular.Beginn", "Bitte als TT.MM.JJJJ HH:MM eingeben.");
        }
        else if (!ZeitEingabe.TryLesen(Formular.Ende, beginn, out ende))
        {
            ModelState.AddModelError("Formular.Ende", "Bitte als TT.MM.JJJJ HH:MM oder nur HH:MM eingeben.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var s = Sitzung;
        if (s is null)
        {
            s = new Sitzung { Titel = "" };
            db.Sitzungen.Add(s);
        }
        else
        {
            db.Entry(s).Property<uint>("Version").OriginalValue = Formular.Version;
        }

        s.Art = Formular.Art;
        s.Titel = Formular.Titel.Trim();
        s.Beginn = beginn!.Value;
        s.Ende = ende;
        s.Ort = Leer(Formular.Ort);
        s.EingeladenAm = Formular.EingeladenAm;
        s.Tagesordnung = Leer(Formular.Tagesordnung);
        s.Protokoll = Leer(Formular.Protokoll);
        s.ProtokollfuehrerId = Formular.ProtokollfuehrerId;
        s.ProtokollDokumentId = Formular.ProtokollDokumentId;
        s.Status = Formular.Status;
        s.ProtokollGenehmigtAm = Formular.ProtokollGenehmigtAm;
        s.DemBuergermeisterVorgelegtAm = Formular.DemBuergermeisterVorgelegtAm;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Fehler"] = "Jemand anderes hat die Sitzung inzwischen geändert. Bitte erneut bearbeiten.";
            return RedirectToPage(new { id = s.Id });
        }

        TempData["Meldung"] = "Sitzung gespeichert.";
        return RedirectToPage(new { id = s.Id });
    }

    /// <summary>Speichert die Teilnahmeliste der Sitzung.</summary>
    /// <param name="id">Kennung der Sitzung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Sitzung.</returns>
    public async Task<IActionResult> OnPostTeilnahmeAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct) || Sitzung is null)
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        var bekannt = Mitglieder.Select(m => m.Id).ToHashSet();
        foreach (var eingabe in Teilnahmen.Where(t => bekannt.Contains(t.MitgliedId)))
        {
            var vorhanden = Sitzung.Teilnahmen.FirstOrDefault(t => t.MitgliedId == eingabe.MitgliedId);
            if (eingabe.Status is not { } status)
            {
                if (vorhanden is not null)
                {
                    db.SitzungTeilnahmen.Remove(vorhanden);
                }

                continue;
            }

            if (vorhanden is null)
            {
                db.SitzungTeilnahmen.Add(new SitzungTeilnahme { SitzungId = Sitzung.Id, MitgliedId = eingabe.MitgliedId, Status = status, Stimmberechtigt = eingabe.Stimmberechtigt });
            }
            else
            {
                vorhanden.Status = status;
                vorhanden.Stimmberechtigt = eingabe.Stimmberechtigt;
            }
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Teilnehmer gespeichert.";
        return RedirectToPage(new { id });
    }

    /// <summary>Erfasst einen neuen Beschluss mit fortlaufender Nummer.</summary>
    /// <param name="id">Kennung der Sitzung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Beschlussabschnitt oder die Seite mit Fehlern.</returns>
    public async Task<IActionResult> OnPostBeschlussAsync(Guid id, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct) || Sitzung is null)
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        NurPruefen("NeuerBeschluss.");
        var b = NeuerBeschluss;
        var ergebnis = b.Ergebnis;
        if (ergebnis is null && b.JaStimmen is { } ja && b.NeinStimmen is { } nein)
        {
            ergebnis = Beschluss.AusStimmen(ja, nein);
        }

        if (ergebnis is null)
        {
            ModelState.AddModelError("NeuerBeschluss.Ergebnis", "Ergebnis wählen oder Ja- und Nein-Stimmen eintragen.");
        }

        if (!ModelState.IsValid)
        {
            TempData["Fehler"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToPage(null, null, new { id }, "beschluesse");
        }

        var beschluss = new Beschluss
        {
            Tagesordnungspunkt = Leer(b.Tagesordnungspunkt),
            Gegenstand = b.Gegenstand.Trim(),
            Ergebnis = ergebnis!.Value,
            JaStimmen = b.JaStimmen,
            NeinStimmen = b.NeinStimmen,
            Enthaltungen = b.Enthaltungen,
            UmsetzenBis = b.UmsetzenBis,
        };
        await gremien.BeschlussAnlegenAsync(Sitzung, beschluss, ct);
        TempData["Meldung"] = $"Beschluss {beschluss.Kennung} gespeichert.";
        return RedirectToPage(null, null, new { id }, "beschluesse");
    }

    /// <summary>Markiert einen Beschluss als umgesetzt.</summary>
    /// <param name="id">Kennung der Sitzung.</param>
    /// <param name="beschlussId">Kennung des Beschlusses.</param>
    /// <param name="am">Erledigungsdatum; ohne Angabe heute.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Beschlussabschnitt.</returns>
    public async Task<IActionResult> OnPostErledigtAsync(Guid id, Guid beschlussId, DateOnly? am, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct) || Sitzung?.Beschluesse.FirstOrDefault(b => b.Id == beschlussId) is not { } beschluss)
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        beschluss.ErledigtAm = am ?? Anzeige.Heute(zeit);
        await db.SaveChangesAsync(ct);
        return RedirectToPage(null, null, new { id }, "beschluesse");
    }

    /// <summary>Löscht einen Beschluss der Sitzung.</summary>
    /// <param name="id">Kennung der Sitzung.</param>
    /// <param name="beschlussId">Kennung des Beschlusses.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zum Beschlussabschnitt.</returns>
    public async Task<IActionResult> OnPostBeschlussLoeschenAsync(Guid id, Guid beschlussId, CancellationToken ct)
    {
        if (!await LadenAsync(id, ct) || Sitzung?.Beschluesse.FirstOrDefault(b => b.Id == beschlussId) is not { } beschluss)
        {
            return NotFound();
        }

        if (!DarfBearbeiten)
        {
            return Forbid();
        }

        db.Beschluesse.Remove(beschluss);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = $"Beschluss {beschluss.Kennung} gelöscht. Die Nummer wird nicht neu vergeben.";
        return RedirectToPage(null, null, new { id }, "beschluesse");
    }

    private async Task<bool> LadenAsync(Guid? id, CancellationToken ct)
    {
        DarfBearbeiten = (await autorisierung.AuthorizeAsync(User, Berechtigungen.GremienBearbeiten)).Succeeded;
        if (id is not null)
        {
            Sitzung = await db.Sitzungen
                .Include(s => s.Teilnahmen)
                .Include(s => s.Beschluesse)
                .Include(s => s.Wahlen).ThenInclude(w => w.Funktion)
                .Include(s => s.Wahlen).ThenInclude(w => w.Bewerber).ThenInclude(b => b.Mitglied)
                .Include(s => s.Protokollfuehrer)
                .Include(s => s.ProtokollDokument)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == id, ct);
            if (Sitzung is null)
            {
                return false;
            }
        }

        Mitglieder = await Auswahl.AktiveMitgliederAsync(db, ct);
        Protokolldokumente = await db.Dokumente.AsNoTracking()
            .Where(d => d.AufgehobenAm == null && (d.Kategorie!.Code == "PROT" || d.Id == Formular.ProtokollDokumentId || (Sitzung != null && d.Id == Sitzung.ProtokollDokumentId)))
            .OrderByDescending(d => d.ErstelltAm)
            .Take(100)
            .Select(d => new SelectListItem(d.Titel, d.Id.ToString()))
            .ToListAsync(ct);

        if (Sitzung is not null)
        {
            var tag = Gremienarbeit.Sitzungstag(Sitzung);
            var schluessel = Sitzung.Art == SitzungArt.Hauptversammlung
                ? ParameterSchluessel.HauptversammlungEinladungsfristTage
                : ParameterSchluessel.AusschussEinladungsfristTage;
            EinladungsfristTage = await parameter.ZahlAsync(schluessel, tag, Sitzung.Art == SitzungArt.Hauptversammlung ? 14 : 3, ct);
            EinladungsfristEingehalten = Sitzung.Art is SitzungArt.Hauptversammlung or SitzungArt.Ausschuss
                ? Sitzung.EinladungsfristEingehalten(Sitzung.EingeladenAm, tag, EinladungsfristTage)
                : null;
            var stimmberechtigt = Sitzung.Teilnahmen.Where(t => t.Stimmberechtigt).ToList();
            var anwesend = stimmberechtigt.Count(t => t.Status == TeilnahmeStatus.Anwesend);
            // Hauptversammlung: Bezug sind alle Angehörigen der Einsatzabteilung, nicht nur die erfassten (Satzung § 14 Abs. 4).
            var bezug = Sitzung.Art == SitzungArt.Hauptversammlung
                ? Math.Max(await db.Mitglieder.CountAsync(m => m.Mitgliedschaften.Any(s => s.Abteilung!.Art == AbteilungArt.Einsatz
                    && s.Status != MitgliedStatus.Ausgeschieden && s.GueltigAb <= tag && (s.GueltigBis == null || s.GueltigBis >= tag)), ct),
                    stimmberechtigt.Count)
                : stimmberechtigt.Count;
            Beschlussfaehigkeit = (anwesend, bezug);
        }

        return true;
    }

    /// <summary>Jeder Handler prüft nur sein eigenes Formular.</summary>
    private void NurPruefen(string praefix)
    {
        foreach (var schluessel in ModelState.Keys.Where(k => !k.StartsWith(praefix, StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(schluessel);
        }
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
