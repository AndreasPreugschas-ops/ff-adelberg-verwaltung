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
    /// <summary>Eine Löschfrist mit ihrem aktuellen Wert.</summary>
    /// <param name="Regel">Kennung der Regel im Löschkonzept, z. B. „L5“.</param>
    /// <param name="Beschreibung">Was die Regel löscht.</param>
    /// <param name="Schluessel">Parameterschlüssel der Frist.</param>
    /// <param name="Wert">Heute gültiger Wert.</param>
    /// <param name="Einheit">Einheit und Bezug der Frist, z. B. „Monate nach Austritt“.</param>
    public sealed record Frist(string Regel, string Beschreibung, string Schluessel, string? Wert, string Einheit);

    /// <summary>Regeln, die der Löschlauf anwendet, mit ihrem Parameter.</summary>
    public static readonly (string Regel, string Beschreibung, string Schluessel, string Einheit)[] Regeln =
    [
        (Loeschlauf.RegelAlarmdaten, "Alarm-Rohdaten aus alamos FE2 löschen (Frist beim Empfang gesetzt)", ParameterSchluessel.AlarmdatenAufbewahrungMonate, "Monate nach Empfang"),
        (Loeschlauf.RegelKontaktdaten, "Anschrift, Telefon, E-Mail, Arbeitgeber, Notizen, Barcode leeren; Größenprofil löschen (nach einem Tod sofort)", ParameterSchluessel.KontaktdatenLoeschenNachAustrittMonate, "Monate nach Austritt"),
        (Loeschlauf.RegelAnwesenheiten, "Anwesenheiten eines Kalenderjahres zu Jahressummen je Mitglied verdichten (besuchte, entschuldigte, unentschuldigte Dienste, Minuten) und die Einzelnachweise löschen", ParameterSchluessel.AnwesenheitenVerdichtenNachJahren, "Jahre nach Jahresende"),
        (Loeschlauf.RegelAusstattungen, "zurückgegebene Kleidung und Ausrüstung samt Kleiderkammer-Vorgängen löschen; das Einzelstück bleibt mit seiner Prüfhistorie", ParameterSchluessel.AusstattungLoeschenNachRueckgabeMonate, "Monate nach Rückgabe"),
        (Loeschlauf.RegelAtemschutznachweise, "Atemschutznachweise (Einsätze und Übungen unter Atemschutz) löschen, sobald der letzte Nachweis des Mitglieds so lange zurückliegt; Expositionsverzeichnis nach § 10a GefStoffV (KoAtEx)", ParameterSchluessel.AtemschutznachweiseAufbewahrungJahre, "Jahre nach dem letzten Nachweis"),
        (Loeschlauf.RegelFruehereAnschriften, "frühere private und berufliche Anschriften löschen (nach Austritt mit L6)", ParameterSchluessel.AnschriftenVerlaufLoeschenNachMonate, "Monate nach Änderung"),
        (Loeschlauf.RegelElternzuordnung, "Elternkonto vom Kind trennen, sobald es volljährig ist, keine Jugend-/Kinderabteilung mehr hat oder gesperrt wurde; Elternkonten ohne Kind deaktivieren", ParameterSchluessel.VolljaehrigkeitAlter, "Jahre, sonst sofort"),
        (Loeschlauf.RegelErziehungsberechtigte, "Erziehungsberechtigte löschen, wenn das Mitglied volljährig ist (nach Austritt mit L6)", ParameterSchluessel.ErziehungsberechtigteLoeschenAbAlter, "Jahre"),
        (Loeschlauf.RegelPruefhinweise, "erledigte und zurückgestellte Prüfhinweise löschen", ParameterSchluessel.PruefhinweiseLoeschenNachMonate, "Monate nach Abschluss"),
        (Loeschlauf.RegelAntraege, "angenommene, abgelehnte und zurückgezogene Anträge samt Erziehungsberechtigten löschen", ParameterSchluessel.AntraegeLoeschenNachMonate, "Monate nach Entscheidung"),
        (Loeschlauf.RegelImporte, "Dateien nicht übernommener Altdatenimporte verwerfen und Werte aus den Meldungen entfernen (bei Übernahme sofort); Importprotokolle nach der Aufbewahrungsfrist des Änderungsprotokolls löschen", ParameterSchluessel.ImportdateienLoeschenNachTagen, "Tage nach letzter Änderung"),
        (Loeschlauf.RegelAenderungsantraege, "übernommene, abgelehnte und zurückgezogene Änderungsanträge der Mitglieder löschen", ParameterSchluessel.AntraegeLoeschenNachMonate, "Monate nach Entscheidung"),
        (Loeschlauf.RegelAntragsentwuerfe, "liegengebliebene Entwürfe löschen, gelöschte Entwürfe sofort", ParameterSchluessel.AntragsentwuerfeLoeschenNachMonate, "Monate nach letzter Änderung"),
        (Loeschlauf.RegelUnfaelle, "abgeschlossene Dienstunfälle samt Zeugen löschen; versehentlich angelegte und gelöschte sofort", ParameterSchluessel.UnfaelleLoeschenNachJahren, "Jahre nach Abschluss"),
        (Loeschlauf.RegelVerbandbuch, "Verbandbucheinträge und Beinaheunfälle samt Zeugen und Fotos löschen", ParameterSchluessel.VerbandbuchAufbewahrungJahre, "Jahre nach dem Ereignis"),
        (Loeschlauf.RegelSachschaeden, "erstattete oder abgelehnte Sachschäden samt Fotos löschen", ParameterSchluessel.SachschadenLoeschenNachJahren, "Jahre nach Abschluss"),
        (Loeschlauf.RegelZuwendungen, "Einträge im Spenden- und Sponsoringregister samt Geberdaten löschen", ParameterSchluessel.KasseZuwendungAufbewahrungJahre, "Jahre nach dem Eingangsjahr"),
        (Loeschlauf.RegelKassendaten, "Buchungen, Belege, Kontoumsätze und Auslagenerstattungen abgeschlossener Kassenjahre löschen; Bestände werden vorgetragen", ParameterSchluessel.KassendatenAufbewahrungJahre, "Jahre nach dem Jahr des Abschlusses"),
        (Loeschlauf.RegelBankverbindungen, "Bankverbindungen nach dem Austritt löschen; gelöschte sofort", ParameterSchluessel.BankverbindungLoeschenNachMonate, "Monate nach dem Austritt"),
        (Loeschlauf.RegelEinwilligungen, "widerrufene und abgelehnte Einwilligungen löschen, nach dem Austritt alle Einwilligungen des Mitglieds", ParameterSchluessel.EinwilligungenAufbewahrungMonate, "Monate nach Widerruf, Ablehnung oder Austritt"),
        (Loeschlauf.RegelLoeschanfragen, "erledigte Löschanfragen der Mitglieder löschen", ParameterSchluessel.LoeschanfragenAufbewahrungMonate, "Monate nach Erledigung"),
        (Loeschlauf.RegelWarteliste, "Warteliste der Jugend löschen, gerechnet ab Abschluss oder, wenn offen, ab Erreichen des Mindestalters", ParameterSchluessel.WartelisteLoeschenNachMonate, "Monate"),
        (Loeschlauf.RegelJugendveranstaltungen, "Anmeldungen, Einwilligungen und Betreuer von Jugendveranstaltungen löschen; eine längere Aufbewahrungsfrist des Zuschussgebers geht vor", ParameterSchluessel.JugendveranstaltungenLoeschenNachMonate, "Monate nach dem letzten Tag"),
        (Loeschlauf.RegelBetreuer, "Betreuer der Jugend samt Einsichtnahme ins Führungszeugnis löschen; nicht aufgenommene Tätigkeit sofort (§ 72a Abs. 5 SGB VIII)", ParameterSchluessel.BetreuerLoeschenNachMonate, "Monate nach Ende der Tätigkeit"),
        (Loeschlauf.RegelAtemschutzSperren, "Atemschutz-Abmeldungen „vorübergehend nicht einsetzbar“ löschen (lassen auf die Gesundheit schließen)", ParameterSchluessel.AtemschutzSperrenLoeschenNachMonate, "Monate nach Ende oder Aufhebung"),
        (Loeschlauf.RegelAbwesenheiten, "Gemeldete Abwesenheiten mit Grund löschen; die Anwesenheit „entschuldigt“ bleibt", ParameterSchluessel.AbwesenheitLoeschenNachMonaten, "Monate nach Ende"),
        (Loeschlauf.RegelErinnerungslaeufe, "Protokoll der Erinnerungsläufe löschen", ParameterSchluessel.ErinnerungslaufAufbewahrungMonate, "Monate"),
        (Loeschlauf.RegelKonten, "deaktivierte Konten leeren (Name, E-Mail, Passwort, Rollen), nur die ID bleibt", ParameterSchluessel.KontoLoeschenNachDeaktivierungMonate, "Monate nach Deaktivierung"),
        (Loeschlauf.RegelAenderungsprotokollVerdichtet, "im Änderungsprotokoll die alten und neuen Feldwerte entfernen; wer wann was geändert hat, bleibt", ParameterSchluessel.AenderungsprotokollVerdichtenNachMonate, "Monate"),
        (Loeschlauf.RegelAenderungsprotokoll, "Einträge im Änderungsprotokoll löschen", ParameterSchluessel.AenderungsprotokollAufbewahrungMonate, "Monate"),
        (Loeschlauf.RegelRechtefreigaben, "entschiedene Rechtefreigaben (Vier-Augen) löschen, gerechnet ab der Entscheidung; offene bleiben", ParameterSchluessel.AenderungsprotokollAufbewahrungMonate, "Monate"),
        (Loeschlauf.RegelWachauftraggeber, "Auftraggeber und Ansprechpartner von Brandsicherheitswachen leeren, gerechnet ab Ende des Jahres der Wache; der Dienst bleibt", ParameterSchluessel.BrandsicherheitswachenAuftraggeberLoeschenNachMonaten, "Monate"),
        (Loeschlauf.RegelPflichtvermerke, "Erledigungsvermerke im Jahreskreis löschen, gerechnet ab dem Stichtag; der jüngste je Pflicht bleibt", ParameterSchluessel.PflichtvermerkeLoeschenNachJahren, "Jahre"),
        (Loeschlauf.RegelDienstpflicht, "Abwesenheitsanzeigen, Beurlaubungen und Anhörungen ohne Maßnahme löschen, gerechnet ab Ende des Zeitraums oder Datum", ParameterSchluessel.DienstpflichtAnzeigenLoeschenNachJahren, "Jahre"),
        (Loeschlauf.RegelDienstpflicht, "Verweise, Anträge auf Geldbuße und Beschlüsse samt ihrer Anhörung löschen", ParameterSchluessel.DienstpflichtMassnahmenLoeschenNachJahren, "Jahre"),
        (Loeschlauf.RegelMangelfotos, "Fotos zu Mängeln nach der Behebung löschen; der Mangel bleibt", ParameterSchluessel.MangelfotosLoeschenNachMonaten, "Monate"),
        (Loeschlauf.RegelFahrtenbuch, "Fahrer in Fahrtenbuch und Betankungen leeren, gerechnet ab Ende des Jahres der Fahrt; Kilometer und Liter bleiben", ParameterSchluessel.FahrtenbuchFahrerLoeschenNachJahren, "Jahre"),
        (Loeschlauf.RegelPruefprotokolle, "Prüfer, Kontrollierende, Abgebende und Füllende in Prüfprotokollen leeren, gerechnet ab Aussonderung des Geräts bzw. Außerdienststellung des Fahrzeugs; die Protokolle bleiben", ParameterSchluessel.PruefprotokolleNamenLoeschenNachJahren, "Jahre"),
        (Loeschlauf.RegelMitteilungen, "Mitteilungen samt Empfängern und Lesebestätigungen löschen, gerechnet ab der Veröffentlichung", ParameterSchluessel.MitteilungenLoeschenNachMonaten, "Monate"),
        (Loeschlauf.RegelRundfragen, "Rundfragen und Umfragen samt Antworten löschen, gerechnet ab dem Beenden", ParameterSchluessel.RundfragenLoeschenNachMonaten, "Monate"),
        (Loeschlauf.RegelBenachrichtigungen, "gelesene Benachrichtigungen löschen, gerechnet ab dem Lesen; der Bezug (z. B. die Mitteilung) bleibt", ParameterSchluessel.BenachrichtigungenGeleseneLoeschenNachTagen, "Tage"),
        (Loeschlauf.RegelBenachrichtigungen, "auch nie gelesene Benachrichtigungen löschen, gerechnet ab der Anlage", ParameterSchluessel.BenachrichtigungenLoeschenNachMonaten, "Monate"),
        (Loeschlauf.RegelLehrgangswuensche, "Lehrgangswünsche jeden Stands löschen, gerechnet ab Ende des Wunschjahres; die Lehrgangsteilnahme bleibt", ParameterSchluessel.LehrgangswuenscheLoeschenNachJahren, "Jahre"),
        (Loeschlauf.RegelBegruendungen, "freiwillige Begründung einer Absage leeren", ParameterSchluessel.RueckmeldungBegruendungLoeschenMonate, "Monate nach Dienstende"),
        (Loeschlauf.RegelRueckmeldungen, "Zu- und Absagen zu Diensten löschen", ParameterSchluessel.RueckmeldungLoeschenMonate, "Monate nach Dienstende"),
        ("Startzeit", "Der Lauf startet einmal täglich ab dieser Stunde", ParameterSchluessel.LoeschlaufAbStunde, "Uhr"),
        (Loeschlauf.RegelRestdatensatz, "Restdatensatz Ausgetretener anonymisieren (Name, Personalnummer, Geburtstag; Dienstgrade, Lehrgänge, Nachweise, Ehrungen löschen), sobald auch L8 nichts mehr hält", ParameterSchluessel.RestdatensatzAufbewahrungJahre, "Jahre nach Austritt"),
        ("L2 (Hinweis)", "Einsatzberichte aufbewahren; steht in der Datenschutzinformation, löscht noch nicht", ParameterSchluessel.EinsatzberichteAufbewahrungJahre, "Jahre"),
    ];

    /// <summary>Angaben für die Datenschutzhinweise auf den Antragsformularen.</summary>
    public static readonly (string Schluessel, string Bezeichnung)[] Formulartexte =
    [
        (ParameterSchluessel.DatenschutzBeauftragter, "Datenschutzbeauftragter mit Kontakt"),
        (ParameterSchluessel.FotoKanaele, "Kanäle für Fotos (Foto-Einwilligung)"),
        (ParameterSchluessel.Art9Grundlage, "Rechtsgrundlage für die Atemschutztauglichkeit"),
        (ParameterSchluessel.AlarmierungBetreiber, "Betreiber des Alarmierungssystems"),
        (ParameterSchluessel.Auftragsverarbeiter, "Technische Dienstleister (Auftragsverarbeiter)"),
    ];

    /// <summary>Texte für die Datenschutzinformation auf den Formularen je Parameterschlüssel.</summary>
    [BindProperty]
    public Dictionary<string, string?> Texte { get; set; } = [];

    /// <summary>Die Löschfristen mit Werten.</summary>
    public IReadOnlyList<Frist> Fristen { get; private set; } = [];
    /// <summary>Nachweise der letzten Löschläufe.</summary>
    public IReadOnlyList<Loeschnachweis> Nachweise { get; private set; } = [];

    /// <summary>Anzeigename je Konto, das eine Löschung von Hand ausgelöst hat.</summary>
    public IReadOnlyDictionary<Guid, string> Ausloeser { get; private set; } = new Dictionary<Guid, string>();

    /// <summary>Wer einen Löschlauf ausgelöst hat.</summary>
    /// <param name="n">Der Löschnachweis.</param>
    /// <returns>„nächtlicher Lauf“ oder „von Hand: Name“.</returns>
    public string AusloeserText(Loeschnachweis n) =>
        n.AusgeloestVon is not { } id ? "nächtlicher Lauf" : Ausloeser.TryGetValue(id, out var name) ? $"von Hand: {name}" : "von Hand (Konto gelöscht)";

    /// <summary>Zahl der offenen Löschanfragen der Mitglieder.</summary>
    public int OffeneLoeschanfragen { get; private set; }

    /// <summary>Lädt Fristen, Texte und Nachweise.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task OnGetAsync(CancellationToken ct)
    {
        var heute = Anzeige.Heute(zeit);
        OffeneLoeschanfragen = await db.Loeschanfragen.CountAsync(l => l.ErledigtAm == null, ct);
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
        var konten = Nachweise.Where(n => n.AusgeloestVon != null).Select(n => n.AusgeloestVon!.Value).Distinct().ToList();
        Ausloeser = await db.Users.AsNoTracking().Where(u => konten.Contains(u.Id) && u.AnonymisiertAm == null)
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? "?", ct);
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

    /// <summary>Startet den Löschlauf sofort von Hand.</summary>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Weiterleitung mit Meldung über die Anzahl der Löschungen.</returns>
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
