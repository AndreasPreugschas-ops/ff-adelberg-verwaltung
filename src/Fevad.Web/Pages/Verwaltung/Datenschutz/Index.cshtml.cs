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
        (Loeschlauf.RegelErinnerungslaeufe, "Protokoll der Erinnerungsläufe löschen", ParameterSchluessel.ErinnerungslaufAufbewahrungMonate, "Monate"),
        (Loeschlauf.RegelKonten, "deaktivierte Konten leeren (Name, E-Mail, Passwort, Rollen), nur die ID bleibt", ParameterSchluessel.KontoLoeschenNachDeaktivierungMonate, "Monate nach Deaktivierung"),
        (Loeschlauf.RegelAenderungsprotokoll, "Einträge im Änderungsprotokoll löschen", ParameterSchluessel.AenderungsprotokollAufbewahrungMonate, "Monate"),
        (Loeschlauf.RegelBegruendungen, "freiwillige Begründung einer Absage leeren", ParameterSchluessel.RueckmeldungBegruendungLoeschenMonate, "Monate nach Dienstende"),
        (Loeschlauf.RegelRueckmeldungen, "Zu- und Absagen zu Diensten löschen", ParameterSchluessel.RueckmeldungLoeschenMonate, "Monate nach Dienstende"),
        ("Startzeit", "Der Lauf startet einmal täglich ab dieser Stunde", ParameterSchluessel.LoeschlaufAbStunde, "Uhr"),
    ];

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
        Nachweise = await db.Loeschnachweise.AsNoTracking().OrderByDescending(l => l.Zeitpunkt).ThenBy(l => l.Regel).Take(200).ToListAsync(ct);
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
