using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Datenschutz;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Pages.Verwaltung.Datenschutz;

/// <summary>Fristen des Löschkonzepts und Nachweise der Löschläufe; Lauf von Hand anstoßen.</summary>
public class IndexModel(AppDbContext db, Loeschlauf loeschlauf, ParameterLeser parameter, IAktuellerBenutzer benutzer, TimeProvider zeit) : PageModel
{
    public sealed record Frist(string Regel, string Beschreibung, string Schluessel, string? Wert, string Einheit);

    /// <summary>Regeln, die der Löschlauf anwendet, mit ihrem Parameter.</summary>
    public static readonly (string Regel, string Beschreibung, string Schluessel, string Einheit)[] Regeln =
    [
        (Loeschlauf.RegelAlarmdaten, "Alarm-Rohdaten aus alamos FE2 löschen (Frist beim Empfang gesetzt)", ParameterSchluessel.AlarmdatenAufbewahrungMonate, "Monate nach Empfang"),
        (Loeschlauf.RegelKontaktdaten, "Anschrift, Telefon, E-Mail, Arbeitgeber, Notizen, Barcode leeren; Größenprofil löschen", ParameterSchluessel.KontaktdatenLoeschenNachAustrittMonate, "Monate nach Austritt"),
        (Loeschlauf.RegelErziehungsberechtigte, "Erziehungsberechtigte löschen, wenn das Mitglied volljährig ist (nach Austritt mit L6)", ParameterSchluessel.ErziehungsberechtigteLoeschenAbAlter, "Jahre"),
        (Loeschlauf.RegelPruefhinweise, "erledigte und zurückgestellte Prüfhinweise löschen", ParameterSchluessel.PruefhinweiseLoeschenNachMonate, "Monate nach Abschluss"),
        (Loeschlauf.RegelAntraege, "angenommene, abgelehnte und zurückgezogene Anträge samt Erziehungsberechtigten löschen", ParameterSchluessel.AntraegeLoeschenNachMonate, "Monate nach Entscheidung"),
        (Loeschlauf.RegelAntragsentwuerfe, "liegengebliebene Entwürfe löschen, gelöschte Entwürfe sofort", ParameterSchluessel.AntragsentwuerfeLoeschenNachMonate, "Monate nach letzter Änderung"),
        (Loeschlauf.RegelErinnerungslaeufe, "Protokoll der Erinnerungsläufe löschen", ParameterSchluessel.ErinnerungslaufAufbewahrungMonate, "Monate"),
        (Loeschlauf.RegelKonten, "deaktivierte Konten leeren (Name, E-Mail, Passwort, Rollen), nur die ID bleibt", ParameterSchluessel.KontoLoeschenNachDeaktivierungMonate, "Monate nach Deaktivierung"),
        (Loeschlauf.RegelAenderungsprotokoll, "Einträge im Änderungsprotokoll löschen", ParameterSchluessel.AenderungsprotokollAufbewahrungMonate, "Monate"),
        (Loeschlauf.RegelBegruendungen, "freiwillige Begründung einer Absage leeren", ParameterSchluessel.RueckmeldungBegruendungLoeschenMonate, "Monate nach Dienstende"),
        (Loeschlauf.RegelRueckmeldungen, "Zu- und Absagen zu Diensten löschen", ParameterSchluessel.RueckmeldungLoeschenMonate, "Monate nach Dienstende"),
        ("Startzeit", "Der Lauf startet einmal täglich ab dieser Stunde", ParameterSchluessel.LoeschlaufAbStunde, "Uhr"),
    ];

    /// <summary>Angaben für die Datenschutzhinweise auf den Antragsformularen.</summary>
    public static readonly (string Schluessel, string Bezeichnung)[] Formulartexte =
    [
        (ParameterSchluessel.DatenschutzVerantwortlicher, "Verantwortliche Stelle mit Anschrift"),
        (ParameterSchluessel.DatenschutzBeauftragter, "Datenschutzbeauftragter mit Kontakt"),
        (ParameterSchluessel.FotoKanaele, "Kanäle für Fotos (Foto-Einwilligung)"),
    ];

    [BindProperty]
    public Dictionary<string, string?> Texte { get; set; } = [];

    public IReadOnlyList<Frist> Fristen { get; private set; } = [];
    public IReadOnlyList<Loeschnachweis> Nachweise { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var fristen = new List<Frist>();
        foreach (var r in Regeln)
        {
            fristen.Add(new Frist(r.Regel, r.Beschreibung, r.Schluessel, await parameter.WertAsync(r.Schluessel, heute, ct), r.Einheit));
        }

        Fristen = fristen;
        foreach (var (schluessel, _) in Formulartexte)
        {
            Texte[schluessel] = await parameter.WertAsync(schluessel, heute, ct);
        }

        Nachweise = await db.Loeschnachweise.AsNoTracking().OrderByDescending(l => l.Zeitpunkt).ThenBy(l => l.Regel).Take(200).ToListAsync(ct);
    }

    /// <summary>Neue Texte gelten ab heute; der bisherige Wert bleibt mit seinem Gültigkeitsbeginn erhalten.</summary>
    public async Task<IActionResult> OnPostTexteAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        var geaendert = 0;
        foreach (var (schluessel, bezeichnung) in Formulartexte)
        {
            var neu = Texte.GetValueOrDefault(schluessel)?.Trim();
            if (string.IsNullOrEmpty(neu) || neu.Length > 500)
            {
                TempData["Fehler"] = $"{bezeichnung}: bitte angeben (höchstens 500 Zeichen).";
                return RedirectToPage();
            }

            if (await parameter.WertAsync(schluessel, heute, ct) == neu)
            {
                continue;
            }

            var vonHeute = await db.Parameter.FirstOrDefaultAsync(p => p.Schluessel == schluessel && p.GueltigAb == heute, ct);
            if (vonHeute is null)
            {
                db.Parameter.Add(new Parameter { Schluessel = schluessel, Wert = neu, GueltigAb = heute, Quelle = "Einstellung in FEVAD" });
            }
            else
            {
                vonHeute.Wert = neu;
            }

            geaendert++;
        }

        await db.SaveChangesAsync(ct);
        TempData["Meldung"] = geaendert == 0 ? "Nichts geändert." : "Angaben für die Antragsformulare gespeichert, gültig ab heute.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAusfuehrenAsync(CancellationToken ct)
    {
        var ergebnisse = await loeschlauf.AusfuehrenAsync(zeit.GetUtcNow(), benutzer.Id, ct);
        var gesamt = ergebnisse.Sum(e => e.Anzahl);
        TempData["Meldung"] = gesamt == 0
            ? "Löschlauf ausgeführt: nichts fällig."
            : "Löschlauf ausgeführt: " + string.Join(", ", ergebnisse.Where(e => e.Anzahl > 0).Select(e => $"{e.Regel} {e.Anzahl}")) + ".";
        return RedirectToPage();
    }
}
