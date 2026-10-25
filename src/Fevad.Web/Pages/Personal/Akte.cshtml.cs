using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Antraege;
using Fevad.Infrastructure.Atemschutz;
using Fevad.Infrastructure.Fristen;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Personal;

/// <summary>
/// Personalakte eines Mitglieds: Qualifikationen, wiederkehrende Nachweise und Atemschutz.
/// Die Vorsorge (Gesundheitsdatum) sieht nur, wer Atemschutz lesen darf.
/// </summary>
public class AkteModel(AppDbContext db, Personalfristenabfrage fristen, Abzeichenabfrage abzeichen, ParameterLeser parameter, TimeProvider zeit, IAuthorizationService autorisierung, Datenbereich datenbereich, IAktuellerBenutzer benutzer) : PageModel
{
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; private set; }
    /// <summary>Fristen des Mitglieds.</summary>
    public IReadOnlyList<Fristeintrag> Fristen { get; private set; } = [];
    /// <summary>Lehrgänge, Qualifikationen und Abzeichen.</summary>
    public IReadOnlyList<QualifikationNachweis> Qualifikationen { get; private set; } = [];
    /// <summary>Wiederkehrende Nachweise.</summary>
    public IReadOnlyList<Nachweis> Nachweise { get; private set; } = [];
    /// <summary>Atemschutzvorsorgen, nur Status und Frist; leer ohne Recht Atemschutz.Lesen.</summary>
    public IReadOnlyList<AtemschutzVorsorge> Vorsorgen { get; private set; } = [];
    /// <summary>Atemschutznachweise.</summary>
    public IReadOnlyList<AtemschutzNachweis> AtemschutzNachweise { get; private set; } = [];
    /// <summary>Berechneter Atemschutzstatus.</summary>
    public AtemschutzStatus? Atemschutz { get; private set; }

    /// <summary>Lehrgang Atemschutz vorhanden; dann bietet die Akte das Kennzeichen „aktiver Geräteträger“ an.</summary>
    public bool AtemschutzLehrgang { get; private set; }
    /// <summary>Atemschutz-Abmeldungen des Mitglieds.</summary>
    public IReadOnlyList<AtemschutzSperre> Sperren { get; private set; } = [];

    /// <summary>Abzeichen, die durch eine höhere Stufe abgelöst sind.</summary>
    public IReadOnlySet<Guid> Abgeloest { get; private set; } = new HashSet<Guid>();
    /// <summary>Frist der Jugendfeuerwehr, z. B. die Leistungsspange.</summary>
    public Fristeintrag? Spangenhinweis { get; private set; }

    /// <summary>Auswahl der Lehrgänge und Qualifikationen ohne Abzeichen.</summary>
    public IReadOnlyList<SelectListItem> Qualifikationsauswahl { get; private set; } = [];
    /// <summary>Auswahl der Abzeichen.</summary>
    public IReadOnlyList<SelectListItem> Abzeichenauswahl { get; private set; } = [];

    /// <summary>Gültigkeit in Monaten je Qualifikation bzw. Nachweisart (0 = einmalig), damit das Formular „gültig bis“ vorschlägt.</summary>
    public IReadOnlyDictionary<string, int> Monate { get; private set; } = new Dictionary<string, int>();

    /// <summary>Lehrgangsteilnahmen.</summary>
    public IReadOnlyList<Lehrgangsteilnahme> Teilnahmen { get; private set; } = [];
    /// <summary>Auswahl der Nachweisarten.</summary>
    public IReadOnlyList<SelectListItem> Nachweisauswahl { get; private set; } = [];
    /// <summary>Heutiges Datum in Ortszeit.</summary>
    public DateOnly Heute { get; private set; }

    /// <summary>Darf Lehrgänge und Nachweise eintragen.</summary>
    public bool DarfBearbeiten { get; private set; }
    /// <summary>Darf nur wiederkehrende Nachweise eintragen.</summary>
    public bool DarfNachweise { get; private set; }
    /// <summary>Darf Atemschutz lesen.</summary>
    public bool DarfAtemschutzLesen { get; private set; }
    /// <summary>Darf Atemschutz eintragen.</summary>
    public bool DarfAtemschutzBearbeiten { get; private set; }
    /// <summary>Wer welche Frist verlängern darf.</summary>
    public Fristrechte Rechte => new(DarfBearbeiten, DarfNachweise, DarfAtemschutzBearbeiten);

    /// <summary>Formular, das zum Verlängern einer Frist schon offen und vorbelegt ist (qualifikation, nachweis, vorsorge, atemschutz).</summary>
    public string? Offen { get; private set; }
    /// <summary>Auswahl der Atemschutz-Nachweisarten im Formular.</summary>
    public IReadOnlyList<SelectListItem> AtemschutzAuswahl { get; private set; } = Atemschutzarten;

    /// <summary>Alle Atemschutz-Nachweisarten.</summary>
    public static IReadOnlyList<SelectListItem> Atemschutzarten { get; } =
        Enum.GetValues<AtemschutzNachweisArt>().Select(a => new SelectListItem(Anzeige.Text(a), a.ToString())).ToList();

    /// <summary>Auswahl der Vorsorgeergebnisse.</summary>
    public static IReadOnlyList<SelectListItem> Vorsorgeergebnisse { get; } =
        Enum.GetValues<VorsorgeErgebnis>().Select(e => new SelectListItem(Anzeige.Text(e), e.ToString())).ToList();

    /// <summary>Formular für Lehrgang, Qualifikation oder Abzeichen.</summary>
    public sealed class QualifikationEingabe
    {
        /// <summary>Qualifikation aus dem Katalog.</summary>
        [Required(ErrorMessage = "Qualifikation fehlt.")] public Guid? QualifikationId { get; set; }
        /// <summary>Tag des Erwerbs.</summary>
        [Required(ErrorMessage = "Datum fehlt.")] public DateOnly? ErworbenAm { get; set; }
        /// <summary>Gültig bis; leer bei unbefristeten.</summary>
        public DateOnly? GueltigBis { get; set; }
        /// <summary>Ort des Lehrgangs.</summary>
        [StringLength(200)] public string? Lehrgangsort { get; set; }
        /// <summary>Lehrgangsstunden.</summary>
        [Range(0, 9999, ErrorMessage = "Stunden 0 bis 9999.")] public decimal? Stunden { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(500)] public string? Bemerkung { get; set; }
        /// <summary>Urkunden- oder Zeugnisnummer.</summary>
        [StringLength(100)] public string? Urkunde { get; set; }
    }

    /// <summary>Formular für einen wiederkehrenden Nachweis.</summary>
    public sealed class NachweisEingabe
    {
        /// <summary>Nachweisart aus dem Katalog.</summary>
        [Required(ErrorMessage = "Nachweis fehlt.")] public Guid? NachweisartId { get; set; }
        /// <summary>Tag des Nachweises.</summary>
        [Required(ErrorMessage = "Datum fehlt.")] public DateOnly? Datum { get; set; }
        /// <summary>Gültig bis; leer wird aus dem Intervall berechnet.</summary>
        public DateOnly? GueltigBis { get; set; }
        /// <summary>Wer den Nachweis durchgeführt hat.</summary>
        [StringLength(100)] public string? DurchgefuehrtVon { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(500)] public string? Bemerkung { get; set; }
    }

    /// <summary>Formular für eine Atemschutzvorsorge (nur Status und Frist).</summary>
    public sealed class VorsorgeEingabe
    {
        /// <summary>Tag der Untersuchung.</summary>
        [Required(ErrorMessage = "Datum fehlt.")] public DateOnly? UntersuchtAm { get; set; }
        /// <summary>Ergebnis.</summary>
        [Required(ErrorMessage = "Ergebnis fehlt.")] public VorsorgeErgebnis? Ergebnis { get; set; }
        /// <summary>Leer: aus den Parametern vorgeschlagen (Intervall nach Lebensalter).</summary>
        public DateOnly? NaechsteFaellig { get; set; }
        /// <summary>Bemerkung.</summary>
        [StringLength(200)] public string? Bemerkung { get; set; }
    }

    /// <summary>Formular für einen Atemschutznachweis.</summary>
    public sealed class AtemschutzEingabe
    {
        /// <summary>Art des Nachweises.</summary>
        [Required(ErrorMessage = "Art fehlt.")] public AtemschutzNachweisArt? Art { get; set; }
        /// <summary>Datum.</summary>
        [Required(ErrorMessage = "Datum fehlt.")] public DateOnly? Datum { get; set; }
        /// <summary>Dauer unter Atemschutz in Minuten.</summary>
        [Range(0, 999, ErrorMessage = "Minuten 0 bis 999.")] public int? Minuten { get; set; }
        /// <summary>Gerätetyp.</summary>
        [StringLength(100)] public string? Geraetetyp { get; set; }
        /// <summary>Ort.</summary>
        [StringLength(200)] public string? Ort { get; set; }
        /// <summary>Tätigkeit.</summary>
        [StringLength(200)] public string? Taetigkeit { get; set; }
        /// <summary>Nummer des Pressluftatmers.</summary>
        [StringLength(50)] public string? PaGeraet { get; set; }
        /// <summary>Nummer der Maske.</summary>
        [StringLength(50)] public string? Maske { get; set; }
        /// <summary>Gefahrstoff, dem die Person ausgesetzt war.</summary>
        [StringLength(200)] public string? Exposition { get; set; }
    }

    /// <summary>Zeigt die Personalakte, auf Wunsch mit vorbelegtem Formular zum Verlängern einer Frist.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="verlaengern">Bereich der zu verlängernden Frist.</param>
    /// <param name="schluessel">Schlüssel der zu verlängernden Frist.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Seite oder 404.</returns>
    public async Task<IActionResult> OnGetAsync(Guid id, Fristbereich? verlaengern, string? schluessel, CancellationToken ct)
    {
        Mitglied = await (await SichtbarAsync()).AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (Mitglied is null)
        {
            return NotFound();
        }

        Heute = Anzeige.Heute(zeit);
        DarfBearbeiten = await DarfAsync(Berechtigungen.AusbildungBearbeiten);
        DarfNachweise = await DarfAsync(Berechtigungen.AusbildungNachweise);
        DarfAtemschutzLesen = await DarfAsync(Berechtigungen.AtemschutzLesen);
        DarfAtemschutzBearbeiten = await DarfAsync(Berechtigungen.AtemschutzBearbeiten);

        Fristen = (await fristen.LadenAsync(Heute, id, ct))
            .Where(f => DarfAtemschutzLesen || !f.Gesundheitsbezogen)
            .ToList();

        Qualifikationen = await db.QualifikationNachweise.AsNoTracking().Include(q => q.Qualifikation)
            .Where(q => q.MitgliedId == id)
            .OrderBy(q => q.Qualifikation!.Sortierung).ThenByDescending(q => q.ErworbenAm)
            .ToListAsync(ct);
        Abgeloest = Abzeichenpruefung.Abgeloest(Qualifikationen, await db.Qualifikationen.AsNoTracking().ToDictionaryAsync(q => q.Id, ct));
        Spangenhinweis = Fristen.FirstOrDefault(f => f.Bereich == Fristbereich.Jugend);
        Nachweise = await db.Nachweise.AsNoTracking().Include(n => n.Nachweisart)
            .Where(n => n.MitgliedId == id && (DarfAtemschutzLesen || !n.Nachweisart!.Gesundheitsbezogen))
            .OrderByDescending(n => n.Datum)
            .ToListAsync(ct);
        AtemschutzNachweise = await db.AtemschutzNachweise.AsNoTracking()
            .Where(n => n.MitgliedId == id)
            .OrderByDescending(n => n.Datum)
            .ToListAsync(ct);

        if (DarfAtemschutzLesen)
        {
            Vorsorgen = await db.AtemschutzVorsorgen.AsNoTracking()
                .Where(v => v.MitgliedId == id)
                .OrderByDescending(v => v.UntersuchtAm)
                .ToListAsync(ct);

            AtemschutzLehrgang = Qualifikationen.Any(q => q.Qualifikation?.Code == Qualifikation.CodeAtemschutz && q.ErworbenAm <= Heute);
            Sperren = await db.AtemschutzSperren.AsNoTracking()
                .Where(s => s.MitgliedId == id && s.AufgehobenAm == null && s.Bis >= Heute)
                .OrderBy(s => s.Von)
                .ToListAsync(ct);
            if (Mitglied.AtemschutzAktiv)
            {
                var einstellungen = await fristen.EinstellungenAsync(Heute, ct);
                Atemschutz = AtemschutzStatus.Berechnen(AtemschutzLehrgang, Vorsorgen, AtemschutzNachweise, Heute,
                    einstellungen.Intervall, einstellungen.AtemschutzWarnungTage, Sperren.Any(s => s.Wirksam(Heute)));
            }
        }

        var katalog = await db.Qualifikationen.AsNoTracking().Where(q => q.Aktiv).OrderBy(q => q.Art).ThenBy(q => q.Sortierung).ToListAsync(ct);
        Qualifikationsauswahl = katalog.Where(q => q.Art != QualifikationArt.Abzeichen).Select(q => new SelectListItem(q.Bezeichnung, q.Id.ToString())).ToList();
        Abzeichenauswahl = katalog.Where(q => q.Art == QualifikationArt.Abzeichen).Select(q => new SelectListItem(q.Bezeichnung, q.Id.ToString())).ToList();
        var nachweisarten = await db.Nachweisarten.AsNoTracking()
            .Where(a => a.Aktiv && (a.Gesundheitsbezogen ? DarfAtemschutzBearbeiten : DarfNachweise))
            .OrderBy(a => a.Sortierung)
            .ToListAsync(ct);
        Nachweisauswahl = nachweisarten.Select(a => new SelectListItem(a.Bezeichnung, a.Id.ToString())).ToList();
        Monate = katalog.Select(q => (q.Id, Monate: q.GueltigkeitMonate ?? 0))
            .Concat(nachweisarten.Select(a => (a.Id, Monate: a.Einmalig ? 0 : a.IntervallMonate)))
            .ToDictionary(x => x.Id.ToString(), x => x.Monate);
        Teilnahmen = await db.Lehrgangsteilnahmen.AsNoTracking().Include(t => t.Qualifikation)
            .Where(t => t.MitgliedId == id)
            .OrderBy(t => t.Abgeschlossen).ThenByDescending(t => t.Von ?? t.ZugewiesenAm ?? t.AngemeldetAm)
            .ToListAsync(ct);

        // Aus „verlängern“ einer Frist: passendes Formular öffnen und vorbelegen.
        if (verlaengern is { } bereich && schluessel is not null)
        {
            IReadOnlyList<SelectListItem> Waehlen(IReadOnlyList<SelectListItem> liste) =>
                liste.Select(i => new SelectListItem(i.Text, i.Value, i.Value == schluessel)).ToList();
            if (Abzeichenauswahl.Any(i => i.Value == schluessel))
            {
                (Offen, Abzeichenauswahl) = ("abzeichen", Waehlen(Abzeichenauswahl));
            }
            else
            {
                (Offen, Qualifikationsauswahl, Nachweisauswahl, AtemschutzAuswahl) = bereich switch
                {
                    Fristbereich.Qualifikation => ("qualifikation", Waehlen(Qualifikationsauswahl), Nachweisauswahl, AtemschutzAuswahl),
                    Fristbereich.Nachweis => ("nachweis", Qualifikationsauswahl, Waehlen(Nachweisauswahl), AtemschutzAuswahl),
                    Fristbereich.Atemschutz when schluessel == Personalfristen.SchluesselVorsorge => ("vorsorge", Qualifikationsauswahl, Nachweisauswahl, AtemschutzAuswahl),
                    Fristbereich.Atemschutz => ("atemschutz", Qualifikationsauswahl, Nachweisauswahl, Waehlen(AtemschutzAuswahl)),
                    _ => (null, Qualifikationsauswahl, Nachweisauswahl, AtemschutzAuswahl),
                };
            }
        }

        return Page();
    }

    /// <summary>Trägt einen Lehrgang, eine Qualifikation oder ein Abzeichen ein.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="eingabe">Die Eingabe.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Akte, ohne Recht 403.</returns>
    public async Task<IActionResult> OnPostQualifikationAsync(Guid id, QualifikationEingabe eingabe, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AusbildungBearbeiten))
        {
            return Forbid();
        }

        var qualifikation = eingabe.QualifikationId is { } qid ? await db.Qualifikationen.Include(q => q.Vorstufe).FirstOrDefaultAsync(q => q.Id == qid, ct) : null;
        if (!ModelState.IsValid || qualifikation is null || !await MitgliedVorhandenAsync(id, ct))
        {
            return Fehler(id);
        }

        var nachweis = new QualifikationNachweis
        {
            MitgliedId = id,
            QualifikationId = qualifikation.Id,
            ErworbenAm = eingabe.ErworbenAm!.Value,
            GueltigBis = eingabe.GueltigBis,
            Lehrgangsort = Leer(eingabe.Lehrgangsort),
            Stunden = eingabe.Stunden,
            Bemerkung = Leer(eingabe.Bemerkung),
            Urkunde = Leer(eingabe.Urkunde),
        };
        nachweis.GueltigkeitVorbelegen(qualifikation);

        // Lehrgänge und Abzeichen werden auch nachgetragen; fehlende Voraussetzungen sperren nicht, sie werden gemeldet.
        var offen = qualifikation.Art == QualifikationArt.Abzeichen || qualifikation.Voraussetzungen is not null || qualifikation.MindestalterJahre is not null
            ? (await abzeichen.PruefenAsync(id, qualifikation, nachweis.ErworbenAm, ct)).Where(v => !v.Erfuellt).Select(v => v.Text).ToList()
            : [];
        db.QualifikationNachweise.Add(nachweis);
        await db.SaveChangesAsync(ct);

        TempData["Meldung"] = $"{qualifikation.Bezeichnung} eingetragen."
            + (offen.Count > 0 ? " Bitte prüfen, nicht erfüllt: " + string.Join("; ", offen) + "." : "");
        return RedirectToPage(new { id });
    }

    /// <summary>Trägt einen wiederkehrenden Nachweis ein.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="eingabe">Die Eingabe.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Akte, ohne Recht 403.</returns>
    public async Task<IActionResult> OnPostNachweisAsync(Guid id, NachweisEingabe eingabe, CancellationToken ct)
    {
        // Untersuchungen mit Gesundheitsbezug trägt ein, wer Atemschutz bearbeiten darf, alles andere wer Nachweise erfassen darf.
        var art = eingabe.NachweisartId is { } aid ? await db.Nachweisarten.FirstOrDefaultAsync(a => a.Id == aid, ct) : null;
        if (!await DarfAsync(art is { Gesundheitsbezogen: true } ? Berechtigungen.AtemschutzBearbeiten : Berechtigungen.AusbildungNachweise))
        {
            return Forbid();
        }

        if (!ModelState.IsValid || art is null || !await MitgliedVorhandenAsync(id, ct))
        {
            return Fehler(id);
        }

        db.Nachweise.Add(new Nachweis
        {
            MitgliedId = id,
            NachweisartId = art.Id,
            Datum = eingabe.Datum!.Value,
            GueltigBis = eingabe.GueltigBis ?? Nachweis.GueltigkeitAus(eingabe.Datum.Value, art),
            DurchgefuehrtVon = Leer(eingabe.DurchgefuehrtVon),
            Bemerkung = Leer(eingabe.Bemerkung),
        });
        await db.SaveChangesAsync(ct);

        TempData["Meldung"] = $"{art.Bezeichnung} eingetragen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Trägt eine Atemschutzvorsorge ein.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="eingabe">Die Eingabe.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Akte, ohne Recht 403.</returns>
    public async Task<IActionResult> OnPostVorsorgeAsync(Guid id, VorsorgeEingabe eingabe, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AtemschutzBearbeiten))
        {
            return Forbid();
        }

        var geburtsdatum = await db.Mitglieder.Where(m => m.Id == id).Select(m => (DateOnly?)m.Geburtsdatum).FirstOrDefaultAsync(ct);
        if (!ModelState.IsValid || geburtsdatum is null)
        {
            return Fehler(id);
        }

        var untersucht = eingabe.UntersuchtAm!.Value;
        var naechste = eingabe.NaechsteFaellig ?? AtemschutzVorsorge.NaechsteVorschlagen(untersucht, geburtsdatum.Value,
            await parameter.ZahlAsync(ParameterSchluessel.VorsorgeIntervallMonate, untersucht, 36, ct),
            await parameter.ZahlAsync(ParameterSchluessel.VorsorgeIntervallAbAlterMonate, untersucht, 12, ct),
            await parameter.ZahlAsync(ParameterSchluessel.VorsorgeKuerzeresIntervallAbAlter, untersucht, 50, ct));

        db.AtemschutzVorsorgen.Add(new AtemschutzVorsorge
        {
            MitgliedId = id,
            UntersuchtAm = untersucht,
            Ergebnis = eingabe.Ergebnis!.Value,
            NaechsteFaellig = naechste,
            Bemerkung = Leer(eingabe.Bemerkung),
        });
        await db.SaveChangesAsync(ct);

        TempData["Meldung"] = $"Vorsorge eingetragen, nächste fällig am {Anzeige.Datum(naechste)}.";
        return RedirectToPage(new { id });
    }

    /// <summary>Trägt einen Atemschutznachweis ein.</summary>
    /// <param name="id">Kennung des Mitglieds.</param>
    /// <param name="eingabe">Die Eingabe.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung zur Akte, ohne Recht 403.</returns>
    public async Task<IActionResult> OnPostAtemschutzAsync(Guid id, AtemschutzEingabe eingabe, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AtemschutzBearbeiten))
        {
            return Forbid();
        }

        if (!ModelState.IsValid || !await MitgliedVorhandenAsync(id, ct))
        {
            return Fehler(id);
        }

        var nachweis = new AtemschutzNachweis
        {
            MitgliedId = id,
            Art = eingabe.Art!.Value,
            Datum = eingabe.Datum!.Value,
            Minuten = eingabe.Minuten,
            Geraetetyp = Leer(eingabe.Geraetetyp),
            Ort = Leer(eingabe.Ort),
            Taetigkeit = Leer(eingabe.Taetigkeit),
            PaGeraet = Leer(eingabe.PaGeraet),
            Maske = Leer(eingabe.Maske),
            Exposition = Leer(eingabe.Exposition),
        };

        // Wer einträgt und bestätigen darf, bestätigt damit seinen Teil; die zweite Bestätigung folgt in der Arbeitsliste.
        if (benutzer.Id is { } benutzerId)
        {
            nachweis.Bestaetigen(benutzerId, await DarfAsync(Berechtigungen.AtemschutzBestaetigenKommando),
                await DarfAsync(Berechtigungen.AtemschutzBestaetigenWart), zeit.GetUtcNow());
        }

        db.AtemschutzNachweise.Add(nachweis);
        await db.SaveChangesAsync(ct);

        TempData["Meldung"] = $"{Anzeige.Text(eingabe.Art)} eingetragen.";
        return RedirectToPage(new { id });
    }

    /// <summary>Kennzeichen „aktiver Atemschutzgeräteträger“ setzen oder entfernen.</summary>
    public async Task<IActionResult> OnPostKennzeichenAsync(Guid id, bool aktiv, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AtemschutzBearbeiten))
        {
            return Forbid();
        }

        var mitglied = await (await SichtbarAsync()).FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mitglied is null)
        {
            return NotFound();
        }

        mitglied.AtemschutzAktiv = aktiv;
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = aktiv ? "Als aktiver Atemschutzgeräteträger gekennzeichnet." : "Wird nicht mehr als Atemschutzgeräteträger überwacht.";
        return RedirectToPage(new { id });
    }

    /// <summary>Expositionsnachweis (KoAtEx) als PDF oder CSV; Dateiname nur mit Personalnummer.</summary>
    public async Task<IActionResult> OnGetExpositionAsync(Guid id, string? format, CancellationToken ct)
    {
        if (!await DarfAsync(Berechtigungen.AtemschutzLesen))
        {
            return Forbid();
        }

        var mitglied = await (await SichtbarAsync()).AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mitglied is null)
        {
            return NotFound();
        }

        var eintraege = (await db.AtemschutzNachweise.AsNoTracking().Where(n => n.MitgliedId == id).OrderBy(n => n.Datum).ToListAsync(ct))
            .Where(n => n.Expositionsrelevant).ToList();
        var o = await db.OrganisationAsync(ct);
        var daten = new Expositionsnachweis.Daten(new AntragPdf.Kopfdaten(o.FeuerwehrName, o.FeuerwehrAnschrift, null, null, o.Logo, o.LogoInhaltstyp),
            $"{mitglied.Vorname} {mitglied.Nachname}", mitglied.Personalnummer, mitglied.Geburtsdatum, eintraege, zeit.GetUtcNow());
        // Expositionen ohne Atemschutzgerät bleiben in FEVAD, kein KoAtEx-Export (Andi, 25.10.2026); das PDF zum Aushändigen enthält sie.
        return format == "csv"
            ? File(Expositionsnachweis.Csv(daten with { Eintraege = eintraege.Where(e => e.Art != AtemschutzNachweisArt.Exposition).ToList() }), "text/csv; charset=utf-8", Expositionsnachweis.Dateiname(mitglied.Personalnummer, "csv"))
            : File(Expositionsnachweis.Pdf(daten), "application/pdf", Expositionsnachweis.Dateiname(mitglied.Personalnummer, "pdf"));
    }

    /// <summary>Löscht einen Eintrag (Soft-Delete, bleibt im Änderungsprotokoll).</summary>
    public async Task<IActionResult> OnPostLoeschenAsync(Guid id, string typ, Guid eintragId, CancellationToken ct)
    {
        var (policy, eintrag) = typ switch
        {
            "qualifikation" => (Berechtigungen.AusbildungBearbeiten, (Entitaet?)await db.QualifikationNachweise.FirstOrDefaultAsync(e => e.Id == eintragId && e.MitgliedId == id, ct)),
            "nachweis" => await db.Nachweise.Include(e => e.Nachweisart).FirstOrDefaultAsync(e => e.Id == eintragId && e.MitgliedId == id, ct) is { } n
                ? (n.Nachweisart!.Gesundheitsbezogen ? Berechtigungen.AtemschutzBearbeiten : Berechtigungen.AusbildungNachweise, n)
                : (Berechtigungen.AusbildungNachweise, null),
            "vorsorge" => (Berechtigungen.AtemschutzBearbeiten, await db.AtemschutzVorsorgen.FirstOrDefaultAsync(e => e.Id == eintragId && e.MitgliedId == id, ct)),
            "atemschutz" => (Berechtigungen.AtemschutzBearbeiten, await db.AtemschutzNachweise.FirstOrDefaultAsync(e => e.Id == eintragId && e.MitgliedId == id, ct)),
            _ => (Berechtigungen.AusbildungBearbeiten, null),
        };

        if (!await DarfAsync(policy))
        {
            return Forbid();
        }

        if (eintrag is null)
        {
            return NotFound();
        }

        db.Remove(eintrag);
        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = "Eintrag gelöscht.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> DarfAsync(string policy) => (await autorisierung.AuthorizeAsync(User, policy)).Succeeded;

    private async Task<bool> MitgliedVorhandenAsync(Guid id, CancellationToken ct) => await (await SichtbarAsync()).AnyAsync(m => m.Id == id, ct);

    /// <summary>Mitglieder der Jugendfeuerwehr nur mit Zugang zu ihr.</summary>
    private Task<IQueryable<Fevad.Domain.Mitglieder.Mitglied>> SichtbarAsync() => datenbereich.OhneJugendAsync(db.Mitglieder, User, Anzeige.Heute(zeit));

    private RedirectToPageResult Fehler(Guid id)
    {
        var meldungen = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => m.Length > 0).Distinct().ToList();
        TempData["Fehler"] = meldungen.Count > 0 ? string.Join(" ", meldungen) : "Eingabe unvollständig.";
        return RedirectToPage(new { id });
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>Wer welche Frist verlängern darf; gesundheitsbezogene Nachweise und Atemschutz nur mit Atemschutz-Recht.</summary>
public sealed record Fristrechte(bool Qualifikationen, bool Nachweise, bool Atemschutz)
{
    /// <summary>Darf der Benutzer diese Frist verlängern?</summary>
    /// <param name="f">Die Frist.</param>
    /// <returns>true, wenn die Frist einen Schlüssel hat und das passende Recht besteht.</returns>
    public bool Darf(Fristeintrag f) => f.Schluessel is not null && f.Bereich switch
    {
        Fristbereich.Qualifikation => Qualifikationen,
        Fristbereich.Nachweis => f.Gesundheitsbezogen ? Atemschutz : Nachweise,
        Fristbereich.Atemschutz => Atemschutz,
        _ => false,
    };
}
