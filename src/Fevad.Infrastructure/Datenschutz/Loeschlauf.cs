using Fevad.Domain.Unfaelle;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;
using Fevad.Domain.Kassen;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>
/// Wendet die Fristen des Löschkonzepts an: löscht oder leert fällige Daten endgültig, schwärzt die zugehörigen
/// Einträge im Änderungsprotokoll und schreibt je Regel einen <see cref="Loeschnachweis"/>.
/// Alle Fristen kommen aus <see cref="Parameter"/>; der Lauf ist idempotent und darf jederzeit wiederholt werden.
/// </summary>
public sealed class Loeschlauf(AppDbContext db, ParameterLeser parameter, Archivierung archiv, ILogger<Loeschlauf> log)
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    /// <summary>Regelname im Löschnachweis für den Lauf als Ganzes.</summary>
    public const string RegelLauf = "Löschlauf";
    /// <summary>Regelname L1: Rohdaten der Alarmierung.</summary>
    public const string RegelAlarmdaten = "L1 Alarm-Rohdaten";
    /// <summary>Regelname L3: Beteiligte und Ansprechpartner im Einsatzbericht.</summary>
    public const string RegelEinsatzbeteiligte = "L3 Beteiligte im Einsatzbericht";
    /// <summary>Regelname L47: abgeschaltete Objekte mit Brandmeldeanlage samt Betreiber.</summary>
    public const string RegelBmaObjekte = "L47 BMA-Objekte";
    /// <summary>Regelname L48: Personenbezug entschiedener Gemeinde-Vorgänge.</summary>
    public const string RegelGemeindevorgaenge = "L48 Gemeinde-Vorgänge";
    /// <summary>Regelname L49: Bescheide des Bürgermeisters an Mitglieder und Bewerber.</summary>
    public const string RegelMitgliederbescheide = "L49 Bescheide an Mitglieder";
    /// <summary>Regelname L50: zurückgegebene oder abgelaufene Dienstausweise.</summary>
    public const string RegelDienstausweise = "L50 Dienstausweise";
    /// <summary>Regelname L6: Kontaktdaten nach dem Austritt.</summary>
    public const string RegelKontaktdaten = "L6 Kontaktdaten nach Austritt";
    /// <summary>Regelname L7: Anonymisierung des Restdatensatzes Ausgetretener.</summary>
    public const string RegelRestdatensatz = "L7 Restdatensatz anonymisiert";
    /// <summary>Regelname L8: Atemschutznachweise nach Ablauf der Aufbewahrung.</summary>
    public const string RegelAtemschutznachweise = "L8 Atemschutznachweise";
    /// <summary>Regelname L5: Verdichtung der Anwesenheiten zu Jahressummen.</summary>
    public const string RegelAnwesenheiten = "L5 Anwesenheiten verdichtet";
    /// <summary>Regelname L14: Größenprofil nach dem Austritt.</summary>
    public const string RegelGroessen = "L14 Größenprofil nach Austritt";
    /// <summary>Regelname L14: zurückgegebene Ausstattung und Kleiderkammer-Vorgänge.</summary>
    public const string RegelAusstattungen = "L14 Ausstattung nach Rückgabe";
    /// <summary>Regelname L15: Erziehungsberechtigte volljähriger oder ausgetretener Mitglieder.</summary>
    public const string RegelErziehungsberechtigte = "L15 Erziehungsberechtigte";
    /// <summary>Regelname L31: erledigte und zurückgestellte Prüfhinweise.</summary>
    public const string RegelPruefhinweise = "L31 Prüfhinweise";
    /// <summary>Regelname L32: abgeschlossene Anträge.</summary>
    public const string RegelAntraege = "L32 Anträge abgeschlossen";
    /// <summary>Regelname L32 für Änderungsanträge der Mitglieder (V36): entschiedene oder zurückgezogene nach derselben Frist.</summary>
    public const string RegelAenderungsantraege = "L32 Änderungsanträge";
    /// <summary>Regelname L33: liegengebliebene und gelöschte Antragsentwürfe.</summary>
    public const string RegelAntragsentwuerfe = "L33 Antragsentwürfe";
    /// <summary>Regelname L41: liegengebliebene Importdateien und alte Importprotokolle (V68).</summary>
    public const string RegelImporte = "L41 Altdatenimporte";
    /// <summary>Regelname L34: frühere Anschriften.</summary>
    public const string RegelFruehereAnschriften = "L34 Frühere Anschriften";
    /// <summary>Regelname L35: Elternkonten ohne Kind in Jugend- oder Kinderabteilung.</summary>
    public const string RegelElternzuordnung = "L35 Elternzuordnung";
    /// <summary>Regelname L36: abgeschlossene Dienstunfälle samt Zeugen.</summary>
    public const string RegelUnfaelle = "L36 Dienstunfälle";
    /// <summary>Regelname L37: Bankverbindungen nach dem Austritt.</summary>
    public const string RegelBankverbindungen = "L37 Bankverbindungen";
    /// <summary>Regelname L38: beendete Abmeldungen „vorübergehend nicht einsetzbar“.</summary>
    public const string RegelAtemschutzSperren = "L38 Atemschutz-Abmeldungen";
    /// <summary>Regelname L39: gemeldete Abwesenheiten nach ihrem Ende.</summary>
    public const string RegelAbwesenheiten = "L39 Abwesenheiten";
    /// <summary>Regelname L36: Verbandbucheinträge und Beinaheunfälle nach der Aufbewahrungsfrist.</summary>
    public const string RegelVerbandbuch = "L36 Verbandbuch und Beinaheunfälle";
    /// <summary>Regelname L40: erledigte Sachschäden.</summary>
    public const string RegelSachschaeden = "L40 Sachschäden";
    /// <summary>Regelname L42: Buchungen, Belege, Kontoumsätze und Erstattungen abgeschlossener Kassenjahre nach der Aufbewahrungsfrist.</summary>
    public const string RegelKassendaten = "L42 Kassendaten";
    /// <summary>Regelname L43: Einträge im Spenden- und Sponsoringregister samt Geberdaten nach der Aufbewahrungsfrist.</summary>
    public const string RegelZuwendungen = "L43 Spendenregister";
    /// <summary>Regelname L10a: Protokoll der Erinnerungsläufe.</summary>
    public const string RegelErinnerungslaeufe = "L10a Erinnerungsläufe";
    /// <summary>Regelname L18: deaktivierte Benutzerkonten.</summary>
    public const string RegelKonten = "L18 Benutzerkonten";
    /// <summary>Regelname L19: Einträge im Änderungsprotokoll nach Ablauf der Aufbewahrung.</summary>
    public const string RegelAenderungsprotokoll = "L19 Änderungsprotokoll";
    /// <summary>Regelname L19: Entfernen der Feldwerte aus älteren Protokolleinträgen.</summary>
    public const string RegelAenderungsprotokollVerdichtet = "L19 Änderungsprotokoll verdichtet";
    /// <summary>Regelname L24: Begründungen von Absagen.</summary>
    public const string RegelBegruendungen = "L24 Absage-Begründungen";
    /// <summary>Regelname L25: Zu- und Absagen zu Diensten.</summary>
    public const string RegelRueckmeldungen = "L25 Rückmeldungen";
    /// <summary>Regelname L29: endgültig gelöschtes Dokument.</summary>
    public const string RegelDokument = "L29 Dokument endgültig gelöscht";
    /// <summary>Regelname L29: ältere Versionen eines Dokuments.</summary>
    public const string RegelDokumentVersionen = "L29 alte Dokumentversionen";

    /// <summary>Regelname: abgeschlossene Entschädigungsabrechnungen nach der Aufbewahrungsfrist.</summary>
    public const string RegelAbrechnungen = "L13 Abrechnungsläufe";
    /// <summary>Regelname: abgelehnte und zurückgezogene Entschädigungsanträge.</summary>
    public const string RegelEntschaedigungsantraege = "L44 Entschädigungsanträge";
    /// <summary>Regelname: Belegdateien abgerechneter Entschädigungsanträge.</summary>
    public const string RegelEntschaedigungsbelege = "L44 Belege der Entschädigungsanträge";
    /// <summary>Regelname: Arbeitgeberbescheinigungen nach der Aufbewahrungsfrist.</summary>
    public const string RegelArbeitgeberbescheinigungen = "L45 Arbeitgeberbescheinigungen";
    /// <summary>Regelname: entschiedene Erstattungen an Arbeitgeber nach der Aufbewahrungsfrist.</summary>
    public const string RegelArbeitgebererstattungen = "L46 Erstattungen an Arbeitgeber";

    /// <summary>Regelname: Einwilligungen nach Widerruf, Ablehnung oder Austritt.</summary>
    public const string RegelEinwilligungen = "L51 Einwilligungen";
    /// <summary>Regelname: erledigte Löschanfragen.</summary>
    public const string RegelLoeschanfragen = "L52 Löschanfragen";
    /// <summary>Regelname: Betreuer der Jugend samt Angaben zum Führungszeugnis nach dem Ende der Tätigkeit.</summary>
    public const string RegelBetreuer = "L53 Betreuer und Führungszeugnisse";
    /// <summary>Regelname: Heimweg, Abholberechtigte und Status der Notfallmappe nach dem Ende in Jugend- und Kinderfeuerwehr.</summary>
    public const string RegelNotfallangaben = "L54 Notfallangaben der Jugend";
    /// <summary>Regelname: Anmeldungen und Betreuer von Jugendveranstaltungen nach der Frist ab dem letzten Tag.</summary>
    public const string RegelJugendveranstaltungen = "L55 Anmeldungen zu Jugendveranstaltungen";
    /// <summary>Regelname: Warteliste der Jugend nach der Frist ab Wiedervorlage oder Abschluss.</summary>
    public const string RegelWarteliste = "L56 Warteliste der Jugend";
    /// <summary>Regelname: entschiedene Rechtefreigaben (Vier-Augen, F18) nach der Frist des Änderungsprotokolls.</summary>
    public const string RegelRechtefreigaben = "L57 Rechtefreigaben";

    /// <summary>Ergebnis einer Löschregel.</summary>
    /// <param name="Regel">Regelname aus dem Löschkonzept.</param>
    /// <param name="Anzahl">Zahl der gelöschten, anonymisierten oder verdichteten Datensätze.</param>
    public sealed record Ergebnis(string Regel, int Anzahl);

    /// <summary>
    /// Nächtlicher Aufruf: führt den Lauf aus, wenn die Startstunde erreicht ist und heute noch keiner lief.
    /// Gibt null zurück, wenn nichts zu tun war.
    /// </summary>
    public async Task<IReadOnlyList<Ergebnis>?> FaelligAusfuehrenAsync(DateTimeOffset jetzt, CancellationToken ct)
    {
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(jetzt, Berlin).DateTime);
        var abStunde = await parameter.ZahlAsync(ParameterSchluessel.LoeschlaufAbStunde, heute, 3, ct);
        var letzter = await db.Loeschnachweise.Where(l => l.Regel == RegelLauf).MaxAsync(l => (DateTimeOffset?)l.Zeitpunkt, ct);
        return IstFaellig(jetzt, abStunde, letzter) ? await AusfuehrenAsync(jetzt, null, ct) : null;
    }

    /// <summary>Einmal je Kalendertag (Ortszeit) ab der Startstunde; ein Lauf von Hand am selben Tag zählt mit.</summary>
    public static bool IstFaellig(DateTimeOffset jetzt, int abStunde, DateTimeOffset? letzterLauf)
    {
        var lokal = TimeZoneInfo.ConvertTime(jetzt, Berlin);
        return lokal.Hour >= abStunde
            && (letzterLauf is null || TimeZoneInfo.ConvertTime(letzterLauf.Value, Berlin).Date < lokal.Date);
    }

    /// <summary>Wendet alle Regeln sofort an, z. B. auf Knopfdruck des Administrators.</summary>
    public async Task<IReadOnlyList<Ergebnis>> AusfuehrenAsync(DateTimeOffset jetzt, Guid? ausgeloestVon, CancellationToken ct)
    {
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(jetzt, Berlin).DateTime);
        async Task<int> Monate(string schluessel, int ersatz) => await parameter.ZahlAsync(schluessel, heute, ersatz, ct);

        var begruendungMonate = await Monate(ParameterSchluessel.RueckmeldungBegruendungLoeschenMonate, 3);
        var rueckmeldungMonate = await Monate(ParameterSchluessel.RueckmeldungLoeschenMonate, 12);
        var kontaktMonate = await Monate(ParameterSchluessel.KontaktdatenLoeschenNachAustrittMonate, 3);
        var elternBisAlter = await parameter.ZahlAsync(ParameterSchluessel.ErziehungsberechtigteLoeschenAbAlter, heute, 18, ct);
        var pruefhinweisMonate = await Monate(ParameterSchluessel.PruefhinweiseLoeschenNachMonate, 12);
        var kontoMonate = await Monate(ParameterSchluessel.KontoLoeschenNachDeaktivierungMonate, 6);
        var protokollMonate = await Monate(ParameterSchluessel.AenderungsprotokollAufbewahrungMonate, 36);
        var erinnerungMonate = await Monate(ParameterSchluessel.ErinnerungslaufAufbewahrungMonate, 12);
        var antragMonate = await Monate(ParameterSchluessel.AntraegeLoeschenNachMonate, 12);
        var entwurfMonate = await Monate(ParameterSchluessel.AntragsentwuerfeLoeschenNachMonate, 6);
        var anschriftMonate = await Monate(ParameterSchluessel.AnschriftenVerlaufLoeschenNachMonate, 24);
        var volljaehrig = await parameter.ZahlAsync(ParameterSchluessel.VolljaehrigkeitAlter, heute, 18, ct);
        var beteiligteMonate = await Monate(ParameterSchluessel.EinsatzbeteiligteLoeschenNachMonate, 12);
        var bmaMonate = await Monate(ParameterSchluessel.BmaObjekteLoeschenNachMonaten, 12);
        var gemeindeJahre = await parameter.ZahlAsync(ParameterSchluessel.GemeindevorgaengePersonenbezugLoeschenNachJahren, heute, 3, ct);
        var ablehnungMonate = await Monate(ParameterSchluessel.MitgliederbescheideAblehnungLoeschenNachMonaten, 12);
        var bescheidJahre = await parameter.ZahlAsync(ParameterSchluessel.MitgliederbescheideLoeschenNachJahren, heute, 3, ct);
        var ausweisMonate = await Monate(ParameterSchluessel.DienstausweiseLoeschenNachMonaten, 12);
        var atemschutzJahre = await parameter.ZahlAsync(ParameterSchluessel.AtemschutznachweiseAufbewahrungJahre, heute, 40, ct);
        var anwesenheitJahre = await parameter.ZahlAsync(ParameterSchluessel.AnwesenheitenVerdichtenNachJahren, heute, 6, ct);
        var ausstattungMonate = await Monate(ParameterSchluessel.AusstattungLoeschenNachRueckgabeMonate, 12);
        var verdichtenMonate = await Monate(ParameterSchluessel.AenderungsprotokollVerdichtenNachMonate, 12);
        var restJahre = await parameter.ZahlAsync(ParameterSchluessel.RestdatensatzAufbewahrungJahre, heute, 10, ct);
        var unfallJahre = await parameter.ZahlAsync(ParameterSchluessel.UnfaelleLoeschenNachJahren, heute, 5, ct);
        var bankMonate = await parameter.ZahlAsync(ParameterSchluessel.BankverbindungLoeschenNachMonate, heute, 12, ct);
        var sperrenMonate = await parameter.ZahlAsync(ParameterSchluessel.AtemschutzSperrenLoeschenNachMonate, heute, 24, ct);
        var abwesenheitMonate = await Monate(ParameterSchluessel.AbwesenheitLoeschenNachMonaten, 12);
        var verbandbuchJahre = await parameter.ZahlAsync(ParameterSchluessel.VerbandbuchAufbewahrungJahre, heute, 5, ct);
        var sachschadenJahre = await parameter.ZahlAsync(ParameterSchluessel.SachschadenLoeschenNachJahren, heute, 3, ct);
        var kassenJahre = await parameter.ZahlAsync(ParameterSchluessel.KassendatenAufbewahrungJahre, heute, 10, ct);
        var zuwendungJahre = await parameter.ZahlAsync(ParameterSchluessel.KasseZuwendungAufbewahrungJahre, heute, 10, ct);
        var abrechnungJahre = await parameter.ZahlAsync(ParameterSchluessel.AbrechnungenAufbewahrungJahre, heute, 10, ct);
        var antragBelegMonate = await Monate(ParameterSchluessel.EntschaedigungsantraegeBelegeLoeschenNachMonaten, 12);
        var antragAbgelehntMonate = await Monate(ParameterSchluessel.EntschaedigungsantraegeAbgelehntLoeschenNachMonaten, 12);
        var bescheinigungJahre = await parameter.ZahlAsync(ParameterSchluessel.ArbeitgeberbescheinigungenAufbewahrungJahre, heute, 3, ct);
        var erstattungJahre = await parameter.ZahlAsync(ParameterSchluessel.ArbeitgebererstattungenAufbewahrungJahre, heute, 10, ct);
        var einwilligungMonate = await Monate(ParameterSchluessel.EinwilligungenAufbewahrungMonate, 36);
        var loeschanfrageMonate = await Monate(ParameterSchluessel.LoeschanfragenAufbewahrungMonate, 36);
        var betreuerMonate = await Monate(ParameterSchluessel.BetreuerLoeschenNachMonate, 3);
        var veranstaltungMonate = await Monate(ParameterSchluessel.JugendveranstaltungenLoeschenNachMonate, 12);
        var wartelisteMonate = await Monate(ParameterSchluessel.WartelisteLoeschenNachMonate, 12);

        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        var ergebnisse = new List<Ergebnis>
        {
            new(RegelAlarmdaten, await AlarmdatenAsync(heute, ct)),
            new(RegelBegruendungen, await BegruendungenAsync(jetzt.AddMonths(-begruendungMonate), ct)),
            new(RegelRueckmeldungen, await RueckmeldungenAsync(jetzt.AddMonths(-rueckmeldungMonate), ct)),
            new(RegelEinsatzbeteiligte, await EinsatzbeteiligteAsync(heute.AddMonths(-beteiligteMonate), ct)),
            new(RegelBmaObjekte, await BmaObjekteAsync(heute.AddMonths(-bmaMonate), ct)),
            new(RegelGemeindevorgaenge, await GemeindevorgaengeAsync(heute.AddYears(-gemeindeJahre), ct)),
            new(RegelMitgliederbescheide, await MitgliederbescheideAsync(heute.AddMonths(-ablehnungMonate), jetzt.AddMonths(-ablehnungMonate), heute.AddYears(-bescheidJahre), heute, ct)),
            new(RegelDienstausweise, await DienstausweiseAsync(heute.AddMonths(-ausweisMonate), ct)),
        };
        ergebnisse.Add(new(RegelAnwesenheiten, await AnwesenheitenVerdichtenAsync(heute, anwesenheitJahre, ct)));
        var (kontakt, groessen, eltern, hinweise, anschriften) = await AusgetreteneAsync(
            heute, heute.AddMonths(-kontaktMonate), heute.AddYears(-elternBisAlter), jetzt.AddMonths(-pruefhinweisMonate), heute.AddMonths(-anschriftMonate), ct);
        ergebnisse.Add(new(RegelKontaktdaten, kontakt));
        ergebnisse.Add(new(RegelFruehereAnschriften, anschriften));
        ergebnisse.Add(new(RegelElternzuordnung, await ElternzuordnungenAsync(heute, volljaehrig, jetzt, ct)));
        ergebnisse.Add(new(RegelErziehungsberechtigte, eltern));
        ergebnisse.Add(new(RegelPruefhinweise, hinweise));
        ergebnisse.Add(new(RegelGroessen, groessen));
        ergebnisse.Add(new(RegelAusstattungen, await AusstattungenAsync(heute.AddMonths(-ausstattungMonate), ct)));
        ergebnisse.Add(new(RegelAtemschutznachweise, await AtemschutznachweiseAsync(heute.AddYears(-atemschutzJahre), ct)));
        var unfallGrenze = heute.AddYears(-unfallJahre);
        ergebnisse.Add(new(RegelUnfaelle, await UnfaelleAsync(u => u.Art == UnfallArt.Unfall && u.AbgeschlossenAm != null && u.AbgeschlossenAm < unfallGrenze, ct)));
        var verbandbuchGrenze = jetzt.AddYears(-verbandbuchJahre);
        ergebnisse.Add(new(RegelVerbandbuch, await UnfaelleAsync(u => u.Art != UnfallArt.Unfall && u.Zeitpunkt < verbandbuchGrenze, ct)));
        ergebnisse.Add(new(RegelSachschaeden, await SachschaedenAsync(heute.AddYears(-sachschadenJahre), ct)));
        ergebnisse.Add(new(RegelZuwendungen, await ZuwendungenAsync(new DateOnly(heute.Year - zuwendungJahre, 1, 1), ct)));
        ergebnisse.Add(new(RegelKassendaten, await KassendatenAsync(heute.Year - kassenJahre, heute, ct)));
        ergebnisse.Add(new(RegelAbrechnungen, await AbrechnungenAsync(heute.Year - abrechnungJahre, ct)));
        ergebnisse.Add(new(RegelEntschaedigungsbelege, await EntschaedigungsbelegeAsync(jetzt.AddMonths(-antragBelegMonate), ct)));
        ergebnisse.Add(new(RegelEntschaedigungsantraege, await EntschaedigungsantraegeAsync(heute.AddMonths(-antragAbgelehntMonate), ct)));
        ergebnisse.Add(new(RegelArbeitgeberbescheinigungen, await ArbeitgeberbescheinigungenAsync(heute.Year - bescheinigungJahre, ct)));
        ergebnisse.Add(new(RegelArbeitgebererstattungen, await ArbeitgebererstattungenAsync(heute.Year - erstattungJahre, ct)));
        ergebnisse.Add(new(RegelBankverbindungen, await BankverbindungenAsync(heute.AddMonths(-bankMonate), ct)));
        ergebnisse.Add(new(RegelEinwilligungen, await EinwilligungenAsync(heute.AddMonths(-einwilligungMonate), ct)));
        ergebnisse.Add(new(RegelLoeschanfragen, await LoeschanfragenAsync(jetzt.AddMonths(-loeschanfrageMonate), ct)));
        ergebnisse.Add(new(RegelBetreuer, await BetreuerAsync(heute.AddMonths(-betreuerMonate), jetzt.AddMonths(-betreuerMonate), ct)));
        ergebnisse.Add(new(RegelNotfallangaben, await NotfallangabenAsync(heute, ct)));
        ergebnisse.Add(new(RegelJugendveranstaltungen, await JugendveranstaltungenAsync(jetzt, veranstaltungMonate, ct)));
        ergebnisse.Add(new(RegelWarteliste, await WartelisteAsync(heute.AddMonths(-wartelisteMonate), jetzt.AddMonths(-wartelisteMonate), ct)));
        ergebnisse.Add(new(RegelAtemschutzSperren, await AtemschutzSperrenAsync(heute.AddMonths(-sperrenMonate), jetzt.AddMonths(-sperrenMonate), ct)));
        ergebnisse.Add(new(RegelAbwesenheiten, await AbwesenheitenAsync(heute.AddMonths(-abwesenheitMonate), ct)));
        ergebnisse.Add(new(RegelRestdatensatz, await RestdatensaetzeAsync(heute.AddYears(-restJahre), heute, jetzt, ct)));
        ergebnisse.Add(new(RegelKonten, await KontenAsync(jetzt.AddMonths(-kontoMonate), jetzt, ct)));
        var (antraege, entwuerfe) = await AntraegeAsync(heute.AddMonths(-antragMonate), jetzt.AddMonths(-entwurfMonate), ct);
        ergebnisse.Add(new(RegelAntraege, antraege));
        ergebnisse.Add(new(RegelAntragsentwuerfe, entwuerfe));
        ergebnisse.Add(new(RegelAenderungsantraege, await AenderungsantraegeAsync(jetzt.AddMonths(-antragMonate), ct)));
        var importTage = await parameter.ZahlAsync(ParameterSchluessel.ImportdateienLoeschenNachTagen, heute, 14, ct);
        ergebnisse.Add(new(RegelImporte, await ImporteAsync(jetzt.AddDays(-importTage), jetzt.AddMonths(-protokollMonate), ct)));
        var erinnerungGrenze = jetzt.AddMonths(-erinnerungMonate);
        ergebnisse.Add(new(RegelErinnerungslaeufe, await db.Erinnerungslaeufe.IgnoreQueryFilters()
            .Where(e => e.Zeitpunkt < erinnerungGrenze).ExecuteDeleteAsync(ct)));

        var einsatzJahre = await parameter.ZahlAsync(ParameterSchluessel.EinsatzberichteAufbewahrungJahre, heute, 10, ct);
        await EinsatzberichteAnbietenAsync(heute.Year - einsatzJahre, heute, ct);
        var protokollGrenze = jetzt.AddMonths(-protokollMonate);
        ergebnisse.Add(new(RegelRechtefreigaben, await RechtefreigabenAsync(protokollGrenze, ct)));
        ergebnisse.Add(new(RegelAenderungsprotokoll, await db.AuditEintraege
            .Where(a => a.Zeitpunkt < protokollGrenze).ExecuteDeleteAsync(ct)));

        // L19 Verdichtung: wer wann was geändert hat, bleibt bis zur Aufbewahrungsfrist; die Feldwerte selbst nur kürzer.
        var verdichtenGrenze = jetzt.AddMonths(-verdichtenMonate);
        ergebnisse.Add(new(RegelAenderungsprotokollVerdichtet, await db.AuditEintraege
            .Where(a => a.Zeitpunkt < verdichtenGrenze && a.Aenderungen != null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Aenderungen, (string?)null), ct)));

        foreach (var e in ergebnisse.Where(e => e.Anzahl > 0))
        {
            db.Loeschnachweise.Add(new Loeschnachweis { Zeitpunkt = jetzt, Regel = e.Regel, Anzahl = e.Anzahl, AusgeloestVon = ausgeloestVon });
        }

        db.Loeschnachweise.Add(new Loeschnachweis { Zeitpunkt = jetzt, Regel = RegelLauf, Anzahl = ergebnisse.Sum(e => e.Anzahl), AusgeloestVon = ausgeloestVon });
        await db.SaveChangesAsync(ct);
        await transaktion.CommitAsync(ct);

        log.LogInformation("Löschlauf: {Ergebnisse}", string.Join(", ", ergebnisse.Select(e => $"{e.Regel} {e.Anzahl}")));
        return ergebnisse;
    }

    /// <summary>
    /// L48: entschiedene oder zurückgezogene Gemeinde-Vorgänge mit Mitgliedsbezug verlieren nach der Frist Freitexte und Verweis;
    /// Art, Betreff, Daten und Ergebnis bleiben. Vorgänge ohne Mitgliedsbezug bleiben ganz.
    /// </summary>
    private async Task<int> GemeindevorgaengeAsync(DateOnly grenze, CancellationToken ct)
    {
        var grenzeUtc = Ortszeit.TagesbeginnUtc(grenze);
        var ids = await db.Gemeindevorgaenge.IgnoreQueryFilters()
            .Where(v => v.MitgliedId != null && ((v.EntschiedenAm != null && v.EntschiedenAm < grenze)
                || (v.Zurueckgezogen && v.GeaendertAm != null && v.GeaendertAm < grenzeUtc)))
            .Select(v => v.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.Gemeindevorgaenge.IgnoreQueryFilters().Where(v => ids.Contains(v.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(v => v.MitgliedId, (Guid?)null)
                .SetProperty(v => v.Sachverhalt, (string?)null)
                .SetProperty(v => v.Beschlussvorschlag, (string?)null)
                .SetProperty(v => v.Entscheidungsvermerk, (string?)null), ct);
        await db.ProtokollSchwaerzenAsync<Gemeindevorgang>(ids, ct);
        return ids.Count;
    }

    /// <summary>
    /// L49: Ablehnungen der Aufnahme nach der kurzen Frist ab Bekanntgabe, eingestellte Verfahren nach derselben Frist ab der letzten Änderung,
    /// alle anderen Bescheide nach der langen Frist ab Bestandskraft (Bekanntgabe plus ein Monat Widerspruchsfrist). Die Akte führt das Hauptamt.
    /// </summary>
    private async Task<int> MitgliederbescheideAsync(DateOnly kurzGrenze, DateTimeOffset kurzGrenzeZeit, DateOnly langGrenze, DateOnly heute, CancellationToken ct)
    {
        var bestandskraftVor = langGrenze.AddMonths(-1);
        var faellig = await db.Mitgliederbescheide.IgnoreQueryFilters()
            .Where(b => (b.Art == MitgliederbescheidArt.AblehnungAufnahme && b.BekanntgegebenAm != null && b.BekanntgegebenAm < kurzGrenze)
                || (b.Art != MitgliederbescheidArt.AblehnungAufnahme && b.BekanntgegebenAm != null && b.BekanntgegebenAm < bestandskraftVor)
                || (b.Eingestellt && b.GeaendertAm != null && b.GeaendertAm < kurzGrenzeZeit))
            .Select(b => new { b.Id, b.Art, b.EmpfaengerName, b.BekanntgegebenAm, b.ErstelltAm }).ToListAsync(ct);

        // V65: erst anbieten; gelöscht wird, was das Archiv nicht will, übernommen hat oder nicht rechtzeitig beschieden hat.
        var frei = await archiv.FreigabeAsync(Archivgut.Mitgliederbescheid,
            faellig.Select(b => new Archivierung.Kandidat(b.Id, $"{Archivierung.Text(b.Art)} an {b.EmpfaengerName}",
                b.BekanntgegebenAm?.Year ?? b.ErstelltAm.Year)).ToList(), heute, ct);
        var ids = faellig.Select(b => b.Id).Where(frei.Contains).ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.Mitgliederbescheide.IgnoreQueryFilters().Where(b => ids.Contains(b.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<Mitgliederbescheid>(ids, ct);
        await archiv.ErledigtAsync(Archivgut.Mitgliederbescheid, ids, heute, ct);
        return ids.Count;
    }

    /// <summary>
    /// L2 und V65: Einsatzberichte nach der Aufbewahrungsfrist dem Archiv anbieten. FEVAD löscht sie nicht selbst; das Angebot hält fest,
    /// was das Archiv übernommen hat.
    /// </summary>
    private async Task EinsatzberichteAnbietenAsync(int bisJahr, DateOnly heute, CancellationToken ct)
    {
        var grenze = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(new DateTime(bisJahr, 1, 1), Berlin));
        var schon = db.Archivangebote.Where(a => a.Art == Archivgut.Einsatzbericht).Select(a => a.DatensatzId);
        var faellig = await db.Einsaetze.AsNoTracking().Where(e => e.Alarmiert < grenze && !schon.Contains(e.Id))
            .OrderBy(e => e.Alarmiert).Select(e => new { e.Id, e.Einsatznummer, e.Stichwort, e.Alarmiert }).ToListAsync(ct);
        await archiv.FreigabeAsync(Archivgut.Einsatzbericht,
            faellig.Select(e => new Archivierung.Kandidat(e.Id, $"Einsatz {e.Einsatznummer} vom {TimeZoneInfo.ConvertTime(e.Alarmiert, Berlin):dd.MM.yyyy} {e.Stichwort}".Trim(),
                TimeZoneInfo.ConvertTime(e.Alarmiert, Berlin).Year)).ToList(), heute, ct);
    }

    /// <summary>L57: freigegebene oder abgelehnte Rechtefreigaben so lange wie das Änderungsprotokoll (L19); offene bleiben.</summary>
    private async Task<int> RechtefreigabenAsync(DateTimeOffset grenze, CancellationToken ct)
    {
        var ids = await db.Rechtefreigaben.IgnoreQueryFilters()
            .Where(f => (f.Freigegeben != null && f.EntschiedenAm < grenze) || (f.GeloeschtAm != null && f.GeloeschtAm < grenze))
            .Select(f => f.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.Rechtefreigaben.IgnoreQueryFilters().Where(f => ids.Contains(f.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<Rechtefreigabe>(ids, ct);
        return ids.Count;
    }

    /// <summary>L50: Dienstausweise nach der Frist ab Rückgabe; nicht zurückgegebene nach der Frist ab Ablauf der Gültigkeit.</summary>
    private async Task<int> DienstausweiseAsync(DateOnly grenze, CancellationToken ct)
    {
        var ids = await db.Dienstausweise.IgnoreQueryFilters()
            .Where(d => (d.ZurueckAm != null && d.ZurueckAm < grenze) || (d.ZurueckAm == null && d.GueltigBis < grenze))
            .Select(d => d.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.Dienstausweise.IgnoreQueryFilters().Where(d => ids.Contains(d.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<Dienstausweis>(ids, ct);
        return ids.Count;
    }

    /// <summary>
    /// L47: abgeschaltete BMA-Objekte mit Betreiber und Ansprechpartner, wenn die Abschaltung vor der Grenze liegt und bei keinem Einsatz des
    /// Objekts mehr Kostenersatz offen ist oder nach der Grenze abgeschlossen wurde (wie L3). Der Verweis am Einsatz wird gelöst, die
    /// Einsatzanschrift bleibt.
    /// </summary>
    private async Task<int> BmaObjekteAsync(DateOnly grenze, CancellationToken ct)
    {
        var objekte = await db.BmaObjekte.IgnoreQueryFilters()
            .Where(o => !o.Aktiv && o.AbgeschaltetAm != null && o.AbgeschaltetAm < grenze
                && !db.Einsaetze.IgnoreQueryFilters().Any(e => e.BmaObjektId == o.Id
                    && (e.KostenersatzOffen || (e.KostenersatzAbgeschlossenAm != null && e.KostenersatzAbgeschlossenAm >= grenze))))
            .Select(o => o.Id).ToListAsync(ct);
        if (objekte.Count == 0)
        {
            return 0;
        }

        await db.Einsaetze.IgnoreQueryFilters().Where(e => e.BmaObjektId != null && objekte.Contains(e.BmaObjektId.Value))
            .ExecuteUpdateAsync(u => u.SetProperty(e => e.BmaObjektId, (Guid?)null), ct);
        await db.BmaObjekte.IgnoreQueryFilters().Where(o => objekte.Contains(o.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<BmaObjekt>(objekte, ct);
        return objekte.Count;
    }

    /// <summary>
    /// L3: Beteiligte und Ansprechpartner anderer Organisationen, wenn das Einsatzjahr vor der Grenze endete
    /// und kein Kostenersatz offen ist bzw. sein Abschluss vor der Grenze liegt. Der Einsatzbericht selbst bleibt (L2).
    /// </summary>
    private async Task<int> EinsatzbeteiligteAsync(DateOnly grenze, CancellationToken ct)
    {
        // Einsatzjahr endet am 31.12.; vor der Grenze heißt: Einsatz vor dem 01.01. des Grenzjahres (Ortszeit).
        var jahresbeginn = Ortszeit.TagesbeginnUtc(new DateOnly(grenze.Year, 1, 1));
        var einsaetze = db.Einsaetze.IgnoreQueryFilters()
            .Where(e => e.Alarmiert < jahresbeginn && !e.KostenersatzOffen
                && (e.KostenersatzAbgeschlossenAm == null || e.KostenersatzAbgeschlossenAm < grenze))
            .Select(e => e.Id);

        var beteiligte = await db.EinsatzBeteiligte.IgnoreQueryFilters().Where(b => einsaetze.Contains(b.EinsatzId)).Select(b => b.Id).ToListAsync(ct);
        var organisationen = await db.EinsatzOrganisationen.IgnoreQueryFilters().Where(o => einsaetze.Contains(o.EinsatzId)).Select(o => o.Id).ToListAsync(ct);
        // Kostenschuldner sind Beteiligte des Kostenersatz-Vorgangs und folgen derselben Frist.
        var schuldner = await db.Kostenschuldner.IgnoreQueryFilters()
            .Where(s => einsaetze.Contains(s.Kostenersatzvorgang!.EinsatzId)).Select(s => s.Id).ToListAsync(ct);
        if (schuldner.Count > 0)
        {
            await db.Kostenschuldner.IgnoreQueryFilters().Where(s => schuldner.Contains(s.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Kostenschuldner>(schuldner, ct);
        }

        // Freitexte des Vorgangs können persönliche Verhältnisse enthalten; Beträge, Stand und Bearbeiter bleiben (L3).
        var vorgaenge = await db.Kostenersatzvorgaenge.IgnoreQueryFilters()
            .Where(v => einsaetze.Contains(v.EinsatzId) && (v.Sachverhalt != null || v.EmpfehlungBegruendung != null || v.EntscheidungBegruendung != null))
            .Select(v => v.Id).ToListAsync(ct);
        if (vorgaenge.Count > 0)
        {
            await db.Kostenersatzvorgaenge.IgnoreQueryFilters().Where(v => vorgaenge.Contains(v.Id))
                .ExecuteUpdateAsync(u => u.SetProperty(v => v.Sachverhalt, (string?)null)
                    .SetProperty(v => v.EmpfehlungBegruendung, (string?)null)
                    .SetProperty(v => v.EntscheidungBegruendung, (string?)null), ct);
            await db.ProtokollSchwaerzenAsync<Kostenersatzvorgang>(vorgaenge, ct);
        }

        // Vermerke der Bescheidschritte ebenso; Schritte, Daten und Beträge bleiben für die Forderungsübersicht.
        var vermerke = await db.BescheidEreignisse.IgnoreQueryFilters()
            .Where(e => e.Vermerk != null && einsaetze.Contains(e.Kostenbescheid!.Kostenersatzvorgang!.EinsatzId)).Select(e => e.Id).ToListAsync(ct);
        if (vermerke.Count > 0)
        {
            await db.BescheidEreignisse.IgnoreQueryFilters().Where(e => vermerke.Contains(e.Id))
                .ExecuteUpdateAsync(u => u.SetProperty(e => e.Vermerk, (string?)null), ct);
            await db.ProtokollSchwaerzenAsync<BescheidEreignis>(vermerke, ct);
        }

        if (beteiligte.Count > 0)
        {
            await db.EinsatzBeteiligte.IgnoreQueryFilters().Where(b => beteiligte.Contains(b.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<EinsatzBeteiligter>(beteiligte, ct);
        }

        if (organisationen.Count > 0)
        {
            await db.EinsatzOrganisationen.IgnoreQueryFilters().Where(o => organisationen.Contains(o.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<EinsatzOrganisation>(organisationen, ct);
        }

        return beteiligte.Count + organisationen.Count + schuldner.Count + vorgaenge.Count;
    }

    /// <summary>
    /// L5: Anwesenheiten eines Kalenderjahres werden nach der Frist ab Jahresende zu einer Jahressumme je Mitglied verdichtet
    /// (besuchte, entschuldigte, unentschuldigte Dienste, Minuten, davon Fortbildung) und dann gelöscht. Abrechnungsposten
    /// verlieren nur den Verweis auf die Einzelanwesenheit, die Posten selbst bleiben. Zählt die gelöschten Anwesenheiten.
    /// </summary>
    private async Task<int> AnwesenheitenVerdichtenAsync(DateOnly heute, int jahre, CancellationToken ct)
    {
        // Das Jahr J ist fällig, wenn sein 31.12. plus Frist vor heute liegt: alle Dienste vor dem 1.1. des Jahres (heute − Frist).
        var grenze = Ortszeit.TagesbeginnUtc(new DateOnly(heute.AddYears(-jahre).Year, 1, 1));
        var faellig = await db.Anwesenheiten.IgnoreQueryFilters()
            .Where(a => a.Dienst!.Beginn < grenze)
            .Select(a => new { a.Id, a.MitgliedId, a.Status, a.Von, a.Bis, a.GeloeschtAm, a.Dienst!.Beginn, a.Dienst.Ende, a.Dienst.ZaehltFuerFortbildung })
            .ToListAsync(ct);
        if (faellig.Count == 0)
        {
            return 0;
        }

        // Nur Anwesenheiten zählen, die in der Oberfläche noch sichtbar waren; ausgeblendete werden nur entfernt.
        var werte = faellig
            .Where(a => a.GeloeschtAm == null)
            .Select(a => new Anwesenheitsjahressumme.Einzelwert(
                a.MitgliedId, Ortszeit.Datum(a.Beginn).Year, a.Status,
                a.Status == AnwesenheitStatus.Anwesend ? (a.Bis ?? a.Ende) - (a.Von ?? a.Beginn) : TimeSpan.Zero, a.ZaehltFuerFortbildung))
            .ToList();
        var mitglieder = werte.Select(w => w.MitgliedId).Distinct().ToList();
        var vorhandene = await db.Anwesenheitsjahressummen.Where(s => mitglieder.Contains(s.MitgliedId)).ToListAsync(ct);
        foreach (var summe in Anwesenheitsjahressumme.Verdichten(werte, vorhandene))
        {
            if (!vorhandene.Contains(summe))
            {
                db.Anwesenheitsjahressummen.Add(summe);
            }
        }

        await db.SaveChangesAsync(ct);

        var ids = faellig.Select(a => a.Id).ToList();
        await db.AbrechnungsPosten.IgnoreQueryFilters().Where(p => p.AnwesenheitId != null && ids.Contains(p.AnwesenheitId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.AnwesenheitId, (Guid?)null), ct);
        await db.Anwesenheiten.IgnoreQueryFilters().Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<Anwesenheit>(ids, ct);
        return ids.Count;
    }

    /// <summary>
    /// L14: Zurückgegebene Ausstattung nach der Frist ab Rückgabe löschen, dazu Kleiderkammer-Vorgänge, auf die danach
    /// keine Ausstattung mehr verweist. Das Einzelstück selbst (Gerät) bleibt mit seiner Prüfhistorie.
    /// </summary>
    private async Task<int> AusstattungenAsync(DateOnly grenze, CancellationToken ct)
    {
        var ausstattungen = await db.Ausstattungen.IgnoreQueryFilters()
            .Where(a => a.ZurueckAm != null && a.ZurueckAm < grenze)
            .Select(a => a.Id).ToListAsync(ct);
        if (ausstattungen.Count > 0)
        {
            await db.Ausstattungen.IgnoreQueryFilters().Where(a => ausstattungen.Contains(a.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Ausstattung>(ausstattungen, ct);
        }

        var grenzeUtc = Ortszeit.TagesbeginnUtc(grenze);
        var vorgaenge = await db.Kleiderkammervorgaenge.IgnoreQueryFilters()
            .Where(v => v.Zeitpunkt < grenzeUtc
                && !db.Ausstattungen.IgnoreQueryFilters().Any(a => a.AusgabeVorgangId == v.Id || a.RuecknahmeVorgangId == v.Id))
            .Select(v => v.Id).ToListAsync(ct);
        if (vorgaenge.Count > 0)
        {
            await db.Kleiderkammervorgaenge.IgnoreQueryFilters().Where(v => vorgaenge.Contains(v.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Kleiderkammervorgang>(vorgaenge, ct);
        }

        return ausstattungen.Count + vorgaenge.Count;
    }

    /// <summary>
    /// L7: Der Restdatensatz eines Ausgetretenen wird nach der Frist anonymisiert, sobald auch keine Atemschutznachweise (L8)
    /// mehr vorhanden sind. Name, Personalnummer und Kennungen werden ersetzt, das Geburtsdatum auf den 1. Januar des Jahrzehnts gerundet (Altersgruppen bleiben, das Geburtsjahr nicht),
    /// Dienstgrade, Lehrgänge, Nachweise, Ehrungen, Gruppen und Vorsorgen gelöscht und ein Konto entkoppelt. Mitgliedschaftszeiträume,
    /// Funktionen, Einsatzteilnahmen, Anwesenheitssummen und Abrechnungsposten bleiben ohne Personenbezug für Statistik und Kasse.
    /// </summary>
    private async Task<int> RestdatensaetzeAsync(DateOnly grenze, DateOnly heute, DateTimeOffset jetzt, CancellationToken ct)
    {
        var faellig = await db.Mitglieder.IgnoreQueryFilters()
            .Where(m => m.AnonymisiertAm == null
                && m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null)
                && !m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null && (s.GueltigBis == null || s.GueltigBis >= grenze))
                && !db.AtemschutzNachweise.IgnoreQueryFilters().Any(n => n.MitgliedId == m.Id))
            .Select(m => new
            {
                m.Id, m.Geburtsdatum, m.Personalnummer, m.Vorname, m.Nachname,
                Austritt = m.Mitgliedschaften.Where(s => !s.Vordienstzeit && s.GeloeschtAm == null).Max(s => s.GueltigBis),
            })
            .ToListAsync(ct);

        // V65: die Mitgliederakte vor der Anonymisierung dem Archiv anbieten.
        var frei = await archiv.FreigabeAsync(Archivgut.Mitgliederakte,
            faellig.Select(m => new Archivierung.Kandidat(m.Id, $"{m.Nachname}, {m.Vorname} ({m.Personalnummer})", m.Austritt?.Year ?? heute.Year)).ToList(),
            heute, ct);
        var ids = faellig.Where(m => frei.Contains(m.Id)).ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        var alle = ids.Select(m => m.Id).ToList();
        foreach (var m in ids)
        {
            // Fiktive Testdaten behalten ihr Präfix, damit der Testdaten-Aufräumer sie weiter findet.
            var kennung = (m.Personalnummer.StartsWith("T-", StringComparison.Ordinal) ? "T-" : "") + "X" + m.Id.ToString("N")[^12..];
            await db.Mitglieder.IgnoreQueryFilters().Where(x => x.Id == m.Id).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Personalnummer, kennung)
                .SetProperty(x => x.Vorname, "gelöscht")
                .SetProperty(x => x.Nachname, kennung)
                .SetProperty(x => x.Geburtsdatum, new DateOnly(m.Geburtsdatum.Year / 10 * 10, 1, 1))
                .SetProperty(x => x.Anrede, (string?)null)
                .SetProperty(x => x.Titel, (string?)null)
                .SetProperty(x => x.LegacyMpFeuerId, (string?)null)
                .SetProperty(x => x.Barcode, (string?)null)
                .SetProperty(x => x.Fe2SyncAktiv, false)
                .SetProperty(x => x.AnonymisiertAm, jetzt), ct);
        }

        async Task Loeschen<T>(IQueryable<T> menge, Func<IQueryable<T>, IQueryable<Guid>> id) where T : Entitaet
        {
            var weg = await id(menge).ToListAsync(ct);
            if (weg.Count > 0)
            {
                await menge.Where(e => weg.Contains(e.Id)).ExecuteDeleteAsync(ct);
                await db.ProtokollSchwaerzenAsync<T>(weg, ct);
            }
        }

        await Loeschen(db.Lehrgangsteilnahmen.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.Einwilligungen.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.QualifikationNachweise.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.Nachweise.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.Ehrungen.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.DienstgradZuordnungen.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.GruppenMitgliedschaften.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await Loeschen(db.AtemschutzVorsorgen.IgnoreQueryFilters().Where(x => alle.Contains(x.MitgliedId)), q => q.Select(x => x.Id));
        await db.Users.Where(u => u.MitgliedId != null && alle.Contains(u.MitgliedId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.MitgliedId, (Guid?)null), ct);
        await db.ProtokollSchwaerzenAsync<Mitglied>(alle, ct);
        await archiv.ErledigtAsync(Archivgut.Mitgliederakte, alle, heute, ct);
        return alle.Count;
    }

    /// <summary>L37: Bankverbindungen von Mitgliedern, die seit der Frist keine Mitgliedschaft mehr haben, dazu gelöschte.</summary>
    private async Task<int> BankverbindungenAsync(DateOnly grenze, CancellationToken ct)
    {
        var ids = await db.Bankverbindungen.IgnoreQueryFilters()
            .Where(b => b.GeloeschtAm != null
                || !db.Mitgliedschaften.IgnoreQueryFilters().Any(s => s.MitgliedId == b.MitgliedId && !s.Vordienstzeit && s.GeloeschtAm == null
                    && (s.GueltigBis == null || s.GueltigBis >= grenze)))
            .Select(b => b.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Bankverbindungen.IgnoreQueryFilters().Where(b => ids.Contains(b.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Fevad.Domain.Entschaedigung.Bankverbindung>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>
    /// L51: Einwilligungen löschen, die seit der Frist widerrufen oder abgelehnt sind, und alle Einwilligungen von Mitgliedern, die seit
    /// der Frist keine Mitgliedschaft mehr haben; dazu gelöschte. Die Frist deckt die Verjährung ab, falls eine Veröffentlichung strittig wird.
    /// </summary>
    private async Task<int> EinwilligungenAsync(DateOnly grenze, CancellationToken ct)
    {
        var ids = await db.Einwilligungen.IgnoreQueryFilters()
            .Where(e => e.GeloeschtAm != null
                || e.WiderrufenAm < grenze
                || (!e.Erteilt && e.Am < grenze)
                || !db.Mitgliedschaften.IgnoreQueryFilters().Any(s => s.MitgliedId == e.MitgliedId && !s.Vordienstzeit && s.GeloeschtAm == null
                    && (s.GueltigBis == null || s.GueltigBis >= grenze)))
            .Select(e => e.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Einwilligungen.IgnoreQueryFilters().Where(e => ids.Contains(e.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Einwilligung>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>L52: erledigte Löschanfragen nach der Frist löschen; sie dienen bis dahin als Nachweis der Antwort (Art. 5 Abs. 2 DSGVO).</summary>
    private async Task<int> LoeschanfragenAsync(DateTimeOffset grenze, CancellationToken ct)
    {
        var ids = await db.Loeschanfragen.IgnoreQueryFilters()
            .Where(l => l.ErledigtAm < grenze || (l.GeloeschtAm != null && l.GeloeschtAm < grenze))
            .Select(l => l.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Loeschanfragen.IgnoreQueryFilters().Where(l => ids.Contains(l.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Loeschanfrage>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>
    /// L53: Betreuer-Einträge mit den Angaben zum Führungszeugnis nach der Frist ab Ende der Tätigkeit löschen, nicht aufgenommene Tätigkeiten
    /// (Ende vor Beginn) sofort, dazu gelöschte (§ 72a Abs. 5 Satz 3 und 4 SGB VIII). Das Protokoll wird geschwärzt.
    /// </summary>
    private async Task<int> BetreuerAsync(DateOnly grenze, DateTimeOffset grenzeZeit, CancellationToken ct)
    {
        var ids = await db.Jugendbetreuer.IgnoreQueryFilters()
            .Where(b => b.Bis < grenze || b.Bis < b.Von || (b.GeloeschtAm != null && b.GeloeschtAm < grenzeZeit))
            .Select(b => b.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Jugendbetreuer.IgnoreQueryFilters().Where(b => ids.Contains(b.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Jugendbetreuer>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>
    /// L55: Anmeldungen (mit Einwilligung und Zahlungseingang) und Betreuer einer Jugendveranstaltung löschen, wenn der letzte Tag länger
    /// als die Frist zurückliegt; verlangt der Zuschussgeber eine längere Aufbewahrung der Teilnehmerliste, gilt seine. Abmeldungen gehen
    /// nach derselben Frist ab der Abmeldung. Die Veranstaltung selbst (Beitrag, Zuschuss) bleibt ohne Personenbezug stehen.
    /// </summary>
    private async Task<int> JugendveranstaltungenAsync(DateTimeOffset jetzt, int monate, CancellationToken ct)
    {
        var veranstaltungen = await db.Jugendveranstaltungen.IgnoreQueryFilters()
            .Select(v => new { v.Id, v.Dienst!.Ende, Aufbewahrung = v.Zuschussgeber == null ? null : v.Zuschussgeber.AufbewahrungMonate })
            .ToListAsync(ct);
        var abgelaufen = veranstaltungen.Where(v => v.Ende < jetzt.AddMonths(-Math.Max(monate, v.Aufbewahrung ?? 0))).Select(v => v.Id).ToList();
        var grenzeAbmeldung = jetzt.AddMonths(-monate);
        var anmeldungen = await db.Veranstaltungsanmeldungen.IgnoreQueryFilters()
            .Where(a => abgelaufen.Contains(a.JugendveranstaltungId) || (a.GeloeschtAm != null && a.GeloeschtAm < grenzeAbmeldung))
            .Select(a => a.Id).ToListAsync(ct);
        var betreuer = await db.Veranstaltungsbetreuer.IgnoreQueryFilters()
            .Where(b => abgelaufen.Contains(b.JugendveranstaltungId) || (b.GeloeschtAm != null && b.GeloeschtAm < grenzeAbmeldung))
            .Select(b => b.Id).ToListAsync(ct);
        if (anmeldungen.Count > 0)
        {
            await db.Veranstaltungsanmeldungen.IgnoreQueryFilters().Where(a => anmeldungen.Contains(a.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Veranstaltungsanmeldung>(anmeldungen, ct);
        }

        if (betreuer.Count > 0)
        {
            await db.Veranstaltungsbetreuer.IgnoreQueryFilters().Where(b => betreuer.Contains(b.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Veranstaltungsbetreuer>(betreuer, ct);
        }

        return anmeldungen.Count + betreuer.Count;
    }

    /// <summary>
    /// L56: Einträge der Warteliste löschen, wenn Abschluss oder, ohne Abschluss, die Wiedervorlage länger als die Frist zurückliegt; dazu
    /// gelöschte nach derselben Frist. Das Protokoll wird geschwärzt.
    /// </summary>
    private async Task<int> WartelisteAsync(DateOnly grenze, DateTimeOffset grenzeZeit, CancellationToken ct)
    {
        var ids = await db.Interessenten.IgnoreQueryFilters()
            .Where(i => (i.ErledigtAm ?? i.Wiedervorlage) < grenze || (i.GeloeschtAm != null && i.GeloeschtAm < grenzeZeit))
            .Select(i => i.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Interessenten.IgnoreQueryFilters().Where(i => ids.Contains(i.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Interessent>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>
    /// L54: Heimweg, Abholberechtigte und den Tag der Notfallmappe leeren, sobald das Mitglied in keiner Jugend- oder Kinderfeuerwehr
    /// mehr ist (Übertritt, Austritt). Eine künftige Mitgliedschaft dort zählt mit, damit die Angaben vor dem Eintritt erfasst werden können.
    /// Die alten Werte im Änderungsprotokoll löscht L19.
    /// </summary>
    private async Task<int> NotfallangabenAsync(DateOnly heute, CancellationToken ct) =>
        await db.Mitglieder.IgnoreQueryFilters()
            .Where(m => (m.Abholregelung != null || m.Abholberechtigte != null || m.NotfallmappeAm != null)
                && !m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null
                    && (s.Abteilung!.Art == AbteilungArt.Jugend || s.Abteilung.Art == AbteilungArt.Kinder)
                    && (s.GueltigBis == null || s.GueltigBis >= heute)))
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Abholregelung, (Abholregelung?)null)
                .SetProperty(m => m.Abholberechtigte, (string?)null)
                .SetProperty(m => m.NotfallmappeAm, (DateOnly?)null), ct);

    /// <summary>
    /// L38: Abmeldungen „vorübergehend nicht einsetzbar“ nach der Frist ab Ende bzw. Aufhebung löschen. Auch ohne Grund lassen sie
    /// auf die Gesundheit schließen; das Protokoll wird geschwärzt.
    /// </summary>
    private async Task<int> AtemschutzSperrenAsync(DateOnly grenze, DateTimeOffset grenzeZeit, CancellationToken ct)
    {
        var ids = await db.AtemschutzSperren.IgnoreQueryFilters()
            .Where(s => s.GeloeschtAm != null || (s.AufgehobenAm == null ? s.Bis < grenze : s.AufgehobenAm < grenzeZeit))
            .Select(s => s.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.AtemschutzSperren.IgnoreQueryFilters().Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Fevad.Domain.Atemschutz.AtemschutzSperre>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>L40: erledigte Sachschäden samt Fotos nach der Frist ab Erstattung oder Ablehnung löschen; offene bleiben.</summary>
    private async Task<int> SachschaedenAsync(DateOnly grenze, CancellationToken ct)
    {
        var ids = await db.Sachschaeden.IgnoreQueryFilters()
            .Where(s => (s.AbgeschlossenAm != null && s.AbgeschlossenAm < grenze) || s.GeloeschtAm != null)
            .Select(s => s.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Meldungsfotos.IgnoreQueryFilters().Where(f => f.SachschadenId != null && ids.Contains(f.SachschadenId.Value)).ExecuteDeleteAsync(ct);
            await db.Sachschaeden.IgnoreQueryFilters().Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Sachschaden>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>
    /// L42: Kassenjahre, deren Frist nach § 147 AO abgelaufen ist (Ende des Kalenderjahrs des Abschlusses oder der letzten Ausnahmebuchung
    /// vor <paramref name="bisJahr"/>), verlieren Buchungen, Belege, Kontoumsätze, Barkassenberichte und Auslagenerstattungen. Die Wirkung
    /// der Buchungen geht in den Anfangsbestand der Konten über, damit die Bestände stimmen. Kassenprüfungen des Jahres gehen mit. Jahresabschluss, Wirtschaftsplan und
    /// Bestandsverzeichnis bleiben. Nicht abgeschlossene Jahre bleiben immer.
    /// </summary>
    /// <param name="bisJahr">Erstes Jahr, in dem die Frist noch läuft.</param>
    /// <param name="heute">Stichtag für die Anbietung an das Archiv (V65).</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der gelöschten Buchungen.</returns>
    private async Task<int> KassendatenAsync(int bisJahr, DateOnly heute, CancellationToken ct)
    {
        var abschluesse = await db.Jahresabschluesse.AsNoTracking().Include(j => j.Kasse).Where(j => j.Jahr < bisJahr).OrderBy(j => j.Jahr).ToListAsync(ct);
        var anzahl = 0;
        foreach (var a in abschluesse)
        {
            var buchungen = await db.Buchungen.IgnoreQueryFilters().AsNoTracking().Where(b => b.KasseId == a.KasseId && b.Jahr == a.Jahr).ToListAsync(ct);
            var letzte = buchungen.Select(b => b.ErstelltAm).Append(a.AbgeschlossenAm).Max();
            if (buchungen.Count == 0 || TimeZoneInfo.ConvertTime(letzte, Berlin).Year >= bisJahr)
            {
                continue;
            }

            // V65: das Kassenjahr vor dem Löschen dem Archiv anbieten.
            var frei = await archiv.FreigabeAsync(Archivgut.Kassenjahr, [new(a.Id, $"{a.Kasse?.Name} {a.Jahr}", a.Jahr)], heute, ct);
            if (!frei.Contains(a.Id))
            {
                continue;
            }

            var ids = buchungen.Select(b => b.Id).ToList();
            var naechstesJahr = new DateOnly(a.Jahr + 1, 1, 1);
            var jahresende = new DateTimeOffset(a.Jahr + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var konten = await db.Zahlungsmittelkonten.IgnoreQueryFilters().Where(k => k.KasseId == a.KasseId).ToListAsync(ct);
            var kontoIds = konten.Select(k => k.Id).ToList();
            foreach (var konto in konten)
            {
                konto.Anfangsbestand += buchungen.Where(b => b.GeloeschtAm == null).Sum(b => Kassenregeln.Wirkung(b, konto.Id));
                if (konto.Eroeffnet < naechstesJahr)
                {
                    konto.Eroeffnet = naechstesJahr;
                }
            }

            await db.SaveChangesAsync(ct);
            await db.Buchungen.IgnoreQueryFilters().Where(b => b.StornoVonId != null && ids.Contains(b.StornoVonId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.StornoVonId, (Guid?)null), ct);
            await db.Kassenbelege.IgnoreQueryFilters().Where(b => b.BuchungId != null && ids.Contains(b.BuchungId.Value)).ExecuteDeleteAsync(ct);
            await db.Barkassenberichte.IgnoreQueryFilters().Where(b => b.BuchungId != null && ids.Contains(b.BuchungId.Value)).ExecuteDeleteAsync(ct);
            // Gezahlte Erstattungen gehen mit ihrer Buchung, abgelehnte nach ihrem Jahr; Belege gezahlter hängen schon an der Buchung.
            await db.Zahlungsanordnungen.IgnoreQueryFilters()
                .Where(z => z.KasseId == a.KasseId && ((z.BuchungId != null && ids.Contains(z.BuchungId.Value))
                    || (z.BuchungId == null && z.Status != AnordnungStatus.Vorbereitet && z.ErstelltAm < jahresende)))
                .ExecuteDeleteAsync(ct);
            await db.Kontoumsaetze.IgnoreQueryFilters().Where(u => kontoIds.Contains(u.KontoId) && u.Buchungstag < naechstesJahr).ExecuteDeleteAsync(ct);
            await db.Kontoauszugimporte.IgnoreQueryFilters()
                .Where(i => kontoIds.Contains(i.KontoId) && i.ErstelltAm < jahresende && !db.Kontoumsaetze.IgnoreQueryFilters().Any(u => u.ImportId == i.Id))
                .ExecuteDeleteAsync(ct);
            // Prüfungen des Jahres samt Beanstandungen gehören zu den Unterlagen des Jahresabschlusses.
            await db.Kassenpruefungen.IgnoreQueryFilters().Where(p => p.KasseId == a.KasseId && p.Bis < naechstesJahr).ExecuteDeleteAsync(ct);
            anzahl += await db.Buchungen.IgnoreQueryFilters().Where(b => ids.Contains(b.Id)).ExecuteDeleteAsync(ct);
            await archiv.ErledigtAsync(Archivgut.Kassenjahr, [a.Id], heute, ct);
        }

        return anzahl;
    }

    /// <summary>
    /// L13: Abgeschlossene Entschädigungsabrechnungen, deren Abrechnungsjahr vor <paramref name="bisJahr"/> endete, samt Posten und den darin
    /// abgerechneten Anträgen (L44 bewilligt). Entwürfe bleiben, sie werden ohnehin neu berechnet.
    /// </summary>
    /// <param name="bisJahr">Erstes Abrechnungsjahr, dessen Frist noch läuft.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der gelöschten Abrechnungen.</returns>
    private async Task<int> AbrechnungenAsync(int bisJahr, CancellationToken ct)
    {
        var grenze = new DateOnly(bisJahr, 1, 1);
        var ids = await db.Abrechnungen.IgnoreQueryFilters()
            .Where(a => a.Status == AbrechnungStatus.Abgeschlossen && a.Bis < grenze).Select(a => a.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        var posten = db.AbrechnungsPosten.IgnoreQueryFilters().Where(p => ids.Contains(p.AbrechnungId));
        var postenIds = await posten.Select(p => p.Id).ToListAsync(ct);
        var antragIds = await posten.Where(p => p.EntschaedigungsantragId != null).Select(p => p.EntschaedigungsantragId!.Value).Distinct().ToListAsync(ct);
        await posten.ExecuteDeleteAsync(ct);
        await db.Abrechnungen.IgnoreQueryFilters().Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync(ct);
        // Ein Antrag, der noch in einer jüngeren Abrechnung steht, bleibt.
        antragIds = await db.Entschaedigungsantraege.IgnoreQueryFilters()
            .Where(a => antragIds.Contains(a.Id) && !db.AbrechnungsPosten.IgnoreQueryFilters().Any(p => p.EntschaedigungsantragId == a.Id))
            .Select(a => a.Id).ToListAsync(ct);
        await db.Entschaedigungsantraege.IgnoreQueryFilters().Where(a => antragIds.Contains(a.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<AbrechnungsPosten>(postenIds, ct);
        await db.ProtokollSchwaerzenAsync<Abrechnung>(ids, ct);
        await db.ProtokollSchwaerzenAsync<Entschaedigungsantrag>(antragIds, ct);
        return ids.Count;
    }

    /// <summary>
    /// L44: Belegdateien bewilligter Anträge, deren Abrechnung vor der Grenze abgeschlossen wurde. Die Antragsdaten bleiben bis L13;
    /// die Belege hält die Gemeindekasse.
    /// </summary>
    private async Task<int> EntschaedigungsbelegeAsync(DateTimeOffset grenze, CancellationToken ct) =>
        await db.AntragBelege.IgnoreQueryFilters()
            .Where(b => db.AbrechnungsPosten.IgnoreQueryFilters().Any(p => p.EntschaedigungsantragId == b.EntschaedigungsantragId
                && p.Abrechnung!.Status == AbrechnungStatus.Abgeschlossen && p.Abrechnung.AbgeschlossenAm < grenze))
            .ExecuteDeleteAsync(ct);

    /// <summary>L44: Abgelehnte und zurückgezogene Anträge samt Tagen und Belegen nach der Frist ab der Entscheidung.</summary>
    private async Task<int> EntschaedigungsantraegeAsync(DateOnly grenze, CancellationToken ct)
    {
        var ids = await db.Entschaedigungsantraege.IgnoreQueryFilters()
            .Where(a => (a.Stand == AntragStand.Abgelehnt || a.Stand == AntragStand.Zurueckgezogen) && a.EntschiedenAm != null && a.EntschiedenAm < grenze)
            .Select(a => a.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Entschaedigungsantraege.IgnoreQueryFilters().Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Entschaedigungsantrag>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>L45: Arbeitgeberbescheinigungen, die vor <paramref name="bisJahr"/> ausgestellt wurden (Ortszeit); die Prüfseite meldet den Code danach als unbekannt.</summary>
    /// <param name="bisJahr">Erstes Ausstellungsjahr, dessen Frist noch läuft.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der gelöschten Bescheinigungen.</returns>
    private async Task<int> ArbeitgeberbescheinigungenAsync(int bisJahr, CancellationToken ct)
    {
        var grenze = Ortszeit.TagesbeginnUtc(new DateOnly(bisJahr, 1, 1));
        var ids = await db.Arbeitgeberbescheinigungen.IgnoreQueryFilters().Where(b => b.AusgestelltAm < grenze).Select(b => b.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Arbeitgeberbescheinigungen.IgnoreQueryFilters().Where(b => ids.Contains(b.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Arbeitgeberbescheinigung>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>L46: Erstattungen an Arbeitgeber, die vor <paramref name="bisJahr"/> entschieden wurden; offene bleiben.</summary>
    /// <param name="bisJahr">Erstes Entscheidungsjahr, dessen Frist noch läuft.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Zahl der gelöschten Erstattungen.</returns>
    private async Task<int> ArbeitgebererstattungenAsync(int bisJahr, CancellationToken ct)
    {
        var grenze = new DateOnly(bisJahr, 1, 1);
        var ids = await db.Arbeitgebererstattungen.IgnoreQueryFilters().Where(e => e.EntschiedenAm != null && e.EntschiedenAm < grenze).Select(e => e.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Arbeitgebererstattungen.IgnoreQueryFilters().Where(e => ids.Contains(e.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Arbeitgebererstattung>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>L43: Spendenregister bis zum Ende des Eingangsjahrs vor der Grenze löschen, entfernte Einträge sofort; samt Änderungsprotokoll der Geberdaten.</summary>
    private async Task<int> ZuwendungenAsync(DateOnly grenze, CancellationToken ct)
    {
        var ids = await db.Zuwendungen.IgnoreQueryFilters().Where(z => z.GeloeschtAm != null || z.Eingang < grenze).Select(z => z.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Zuwendungen.IgnoreQueryFilters().Where(z => ids.Contains(z.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Zuwendung>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>L39: Abwesenheiten nach der Frist ab ihrem Ende löschen; die Anwesenheit „entschuldigt“ der Dienste bleibt.</summary>
    private async Task<int> AbwesenheitenAsync(DateOnly grenze, CancellationToken ct)
    {
        var ids = await db.Abwesenheiten.IgnoreQueryFilters()
            .Where(a => a.GeloeschtAm != null || a.Bis < grenze)
            .Select(a => a.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Rueckmeldungen.IgnoreQueryFilters().Where(r => r.AbwesenheitId != null && ids.Contains(r.AbwesenheitId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.AbwesenheitId, (Guid?)null), ct);
            await db.Abwesenheiten.IgnoreQueryFilters().Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Fevad.Domain.Dienstbetrieb.Abwesenheit>(ids, ct);
        }

        return ids.Count;
    }

    /// <summary>L36: fällige Unfälle samt Zeugen und Fotos löschen, dazu versehentlich angelegte (gelöschte) sofort.</summary>
    /// <param name="faellig">Welche Unfälle nach ihrer Frist dran sind.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Zahl der gelöschten Unfälle.</returns>
    private async Task<int> UnfaelleAsync(System.Linq.Expressions.Expression<Func<Unfall, bool>> faellig, CancellationToken ct)
    {
        var ids = await db.Unfaelle.IgnoreQueryFilters().Where(faellig)
            .Union(db.Unfaelle.IgnoreQueryFilters().Where(u => u.GeloeschtAm != null))
            .Select(u => u.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Meldungsfotos.IgnoreQueryFilters().Where(f => f.UnfallId != null && ids.Contains(f.UnfallId.Value)).ExecuteDeleteAsync(ct);
        }

        var zeugen = await db.UnfallZeugen.IgnoreQueryFilters().Where(z => ids.Contains(z.UnfallId) || z.GeloeschtAm != null)
            .Select(z => z.Id).ToListAsync(ct);
        if (zeugen.Count > 0)
        {
            await db.UnfallZeugen.IgnoreQueryFilters().Where(z => zeugen.Contains(z.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Fevad.Domain.Unfaelle.UnfallZeuge>(zeugen, ct);
        }

        if (ids.Count > 0)
        {
            await db.Unfaelle.IgnoreQueryFilters().Where(u => ids.Contains(u.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Fevad.Domain.Unfaelle.Unfall>(ids, ct);
        }

        return ids.Count;
    }

    private async Task<int> AlarmdatenAsync(DateOnly heute, CancellationToken ct)
    {
        var ids = await db.AlarmImporte.IgnoreQueryFilters().Where(a => a.LoeschenAb <= heute).Select(a => a.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.AlarmImporte.IgnoreQueryFilters().Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<AlarmImport>(ids, ct);
        return ids.Count;
    }

    private async Task<int> BegruendungenAsync(DateTimeOffset grenze, CancellationToken ct)
    {
        // Auch Rückmeldungen ohne aktuelle Begründung: ältere Protokolleinträge können eine frühere enthalten.
        var ids = await db.Rueckmeldungen.IgnoreQueryFilters()
            .Where(r => db.Dienste.IgnoreQueryFilters().Any(d => d.Id == r.DienstId && d.Ende < grenze))
            .Select(r => r.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        var geleert = await db.Rueckmeldungen.IgnoreQueryFilters().Where(r => ids.Contains(r.Id) && r.Begruendung != null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Begruendung, (string?)null), ct);
        await db.ProtokollSchwaerzenAsync<Rueckmeldung>(ids, ct);
        return geleert;
    }

    private async Task<int> RueckmeldungenAsync(DateTimeOffset grenze, CancellationToken ct)
    {
        var ids = await db.Rueckmeldungen.IgnoreQueryFilters()
            .Where(r => db.Dienste.IgnoreQueryFilters().Any(d => d.Id == r.DienstId && d.Ende < grenze))
            .Select(r => r.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.Rueckmeldungen.IgnoreQueryFilters().Where(r => ids.Contains(r.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<Rueckmeldung>(ids, ct);
        return ids.Count;
    }

    /// <summary>
    /// L8: Die Atemschutznachweise eines Mitglieds (Einsätze und Übungen unter Atemschutz, FwDV 7) sind sein Expositionsverzeichnis
    /// nach § 10a GefStoffV (KoAtEx der DGUV). Sie bleiben, bis der letzte Nachweis des Mitglieds vor dem Stichtag liegt, und werden
    /// dann vollständig gelöscht; L6 und L7 lassen sie unberührt. Der Lauf schreibt nur Anzahlen, keine Inhalte.
    /// </summary>
    private async Task<int> AtemschutznachweiseAsync(DateOnly grenze, CancellationToken ct)
    {
        var faellig = db.AtemschutzNachweise.IgnoreQueryFilters()
            .GroupBy(n => n.MitgliedId)
            .Where(g => g.Max(n => n.Datum) < grenze)
            .Select(g => g.Key);
        var ids = await db.AtemschutzNachweise.IgnoreQueryFilters().Where(n => faellig.Contains(n.MitgliedId)).Select(n => n.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.AtemschutzNachweise.IgnoreQueryFilters().Where(n => ids.Contains(n.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<AtemschutzNachweis>(ids, ct);
        return ids.Count;
    }

    /// <summary>
    /// Ausgetreten ist, wer eine eigene Mitgliedschaft hatte und seit dem Stichtag in keiner Abteilung mehr ist.
    /// Nach einem Tod gilt das ohne Wartefrist ab dem Sterbetag. Der Restdatensatz (Name, Geburtsdatum, Laufbahn)
    /// bleibt für Bescheinigungen und Ehrungen (L7); die Atemschutznachweise bleiben als Expositionsverzeichnis bis zur Frist aus L8.
    /// </summary>
    private async Task<(int Kontakt, int Groessen, int Erziehungsberechtigte, int Pruefhinweise, int Anschriften)> AusgetreteneAsync(
        DateOnly heute, DateOnly grenze, DateOnly volljaehrigBis, DateTimeOffset hinweiseAbgeschlossenVor, DateOnly anschriftenBisVor, CancellationToken ct)
    {
        var ausgetreten = db.Mitglieder.IgnoreQueryFilters()
            .Where(m => m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null)
                && (!m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null && s.Status != MitgliedStatus.Ausgeschieden
                        && (s.GueltigBis == null || s.GueltigBis >= grenze))
                    || (m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null && s.Austrittsgrund == Austrittsgrund.Tod && s.GueltigBis < heute)
                        && !m.Mitgliedschaften.Any(s => !s.Vordienstzeit && s.GeloeschtAm == null && s.Status != MitgliedStatus.Ausgeschieden
                            && (s.GueltigBis == null || s.GueltigBis >= heute)))));

        var ids = await ausgetreten
            .Where(m => m.Strasse != null || m.Plz != null || m.Ort != null || m.TelefonMobil != null || m.TelefonFestnetz != null
                || m.Email != null || m.NotfallkontaktName != null || m.NotfallkontaktTelefon != null || m.Beruf != null || m.Arbeitgeber != null || m.ArbeitgeberStrasse != null || m.ArbeitgeberPlz != null
                || m.Arbeitsort != null || m.ArbeitgeberTelefon != null || m.ArbeitgeberMobil != null || m.ArbeitgeberEmail != null
                || m.Notizen != null || m.Barcode != null || m.DjfAusweisnummer != null || m.DjfAusweisAm != null || m.Fe2SyncAktiv)
            .Select(m => m.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Mitglieder.IgnoreQueryFilters().Where(m => ids.Contains(m.Id)).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Strasse, (string?)null)
                .SetProperty(m => m.Plz, (string?)null)
                .SetProperty(m => m.Ort, (string?)null)
                .SetProperty(m => m.TelefonMobil, (string?)null)
                .SetProperty(m => m.TelefonFestnetz, (string?)null)
                .SetProperty(m => m.Email, (string?)null)
                .SetProperty(m => m.NotfallkontaktName, (string?)null)
                .SetProperty(m => m.NotfallkontaktTelefon, (string?)null)
                .SetProperty(m => m.Beruf, (string?)null)
                .SetProperty(m => m.Arbeitgeber, (string?)null)
                .SetProperty(m => m.ArbeitgeberStrasse, (string?)null)
                .SetProperty(m => m.ArbeitgeberPlz, (string?)null)
                .SetProperty(m => m.Arbeitsort, (string?)null)
                .SetProperty(m => m.ArbeitgeberTelefon, (string?)null)
                .SetProperty(m => m.ArbeitgeberMobil, (string?)null)
                .SetProperty(m => m.ArbeitgeberEmail, (string?)null)
                .SetProperty(m => m.Notizen, (string?)null)
                .SetProperty(m => m.Barcode, (string?)null)
                .SetProperty(m => m.DjfAusweisnummer, (string?)null)
                .SetProperty(m => m.DjfAusweisAm, (DateOnly?)null)
                .SetProperty(m => m.Fe2SyncAktiv, false), ct);
        }

        // Das Protokoll aller Ausgetretenen schwärzen, auch wenn die Felder schon von Hand geleert wurden.
        var ausgetreteneIds = ausgetreten.Select(m => m.Id);
        await db.ProtokollSchwaerzenAsync<Mitglied>(await ausgetreteneIds.ToListAsync(ct), ct);
        var groessenIds = await db.Groessenangaben.IgnoreQueryFilters().Where(g => ausgetreteneIds.Contains(g.MitgliedId))
            .Select(g => g.Id).ToListAsync(ct);
        if (groessenIds.Count > 0)
        {
            await db.Groessenangaben.IgnoreQueryFilters().Where(g => groessenIds.Contains(g.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Groessenangabe>(groessenIds, ct);
        }

        // Erziehungsberechtigte braucht es nur, solange das Mitglied minderjährig und dabei ist (Elterninformation: Löschung mit Volljährigkeit).
        var eltern = await db.Erziehungsberechtigte.IgnoreQueryFilters()
            .Where(e => ausgetreteneIds.Contains(e.MitgliedId)
                || db.Mitglieder.IgnoreQueryFilters().Any(m => m.Id == e.MitgliedId && m.Geburtsdatum <= volljaehrigBis))
            .Select(e => e.Id).ToListAsync(ct);
        if (eltern.Count > 0)
        {
            await db.Erziehungsberechtigte.IgnoreQueryFilters().Where(e => eltern.Contains(e.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Erziehungsberechtigter>(eltern, ct);
        }

        // Prüfhinweise sind nur Vorschläge: abgeschlossene nach Frist, die von Ausgetretenen mit L6.
        var hinweise = await db.Pruefhinweise.IgnoreQueryFilters()
            .Where(p => ausgetreteneIds.Contains(p.MitgliedId)
                || (p.Status != PruefhinweisStatus.Offen && p.AbgeschlossenAm != null && p.AbgeschlossenAm < hinweiseAbgeschlossenVor))
            .Select(p => p.Id).ToListAsync(ct);
        if (hinweise.Count > 0)
        {
            await db.Pruefhinweise.IgnoreQueryFilters().Where(p => hinweise.Contains(p.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Pruefhinweis>(hinweise, ct);
        }

        // Frühere Anschriften nach Frist ab ihrer Ablösung, die von Ausgetretenen mit L6.
        var anschriften = await db.FruehereAnschriften.IgnoreQueryFilters()
            .Where(a => ausgetreteneIds.Contains(a.MitgliedId) || a.BisDatum < anschriftenBisVor || a.GeloeschtAm != null)
            .Select(a => a.Id).ToListAsync(ct);
        if (anschriften.Count > 0)
        {
            await db.FruehereAnschriften.IgnoreQueryFilters().Where(a => anschriften.Contains(a.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<FruehereAnschrift>(anschriften, ct);
        }

        return (ids.Count, groessenIds.Count, eltern.Count, hinweise.Count, anschriften.Count);
    }

    /// <summary>
    /// Elternkonten: Zuordnung aufheben, wenn das Kind volljährig ist, keine Jugend-/Kinderabteilung mehr hat oder eine Sperre
    /// gesetzt wurde; Elternkonten ohne Kind deaktivieren (danach L18). Zählt die aufgehobenen Zuordnungen.
    /// </summary>
    private async Task<int> ElternzuordnungenAsync(DateOnly heute, int volljaehrig, DateTimeOffset jetzt, CancellationToken ct)
    {
        var ids = await db.Erziehungsberechtigte.IgnoreQueryFilters().Where(Elternkonten.Faellig(heute, volljaehrig))
            .Select(e => e.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Erziehungsberechtigte.IgnoreQueryFilters().Where(e => ids.Contains(e.Id)).ExecuteUpdateAsync(s => s
                .SetProperty(e => e.KontoId, (Guid?)null)
                .SetProperty(e => e.KontoZugeordnetAm, (DateTimeOffset?)null), ct);
        }

        await Elternkonten.VerwaisteDeaktivierenAsync(db, jetzt, ct);
        return ids.Count;
    }

    /// <summary>
    /// Anträge: entschiedene oder zurückgezogene nach Frist ab der Entscheidung (L32), Entwürfe nach Frist ab der letzten
    /// Änderung und verworfene sofort (L33). Angenommene Anträge sind dann schon ins Mitglied übernommen.
    /// </summary>
    private async Task<(int Abgeschlossen, int Entwuerfe)> AntraegeAsync(DateOnly entschiedenVor, DateTimeOffset geaendertVor, CancellationToken ct)
    {
        var abgeschlossen = await db.Antraege.IgnoreQueryFilters()
            .Where(a => a.GeloeschtAm == null && a.Status != AntragStatus.Entwurf && a.Status != AntragStatus.Eingegangen
                && a.EntschiedenAm != null && a.EntschiedenAm < entschiedenVor)
            .Select(a => a.Id).ToListAsync(ct);
        var entwuerfe = await db.Antraege.IgnoreQueryFilters()
            .Where(a => a.GeloeschtAm != null || (a.Status == AntragStatus.Entwurf && (a.GeaendertAm ?? a.ErstelltAm) < geaendertVor))
            .Select(a => a.Id).ToListAsync(ct);
        var alle = abgeschlossen.Concat(entwuerfe).ToList();

        // Auch einzeln entfernte Erziehungsberechtigte noch laufender Anträge.
        var eltern = await db.AntragElternteile.IgnoreQueryFilters()
            .Where(e => alle.Contains(e.AntragId) || e.GeloeschtAm != null)
            .Select(e => e.Id).ToListAsync(ct);
        if (eltern.Count > 0)
        {
            await db.AntragElternteile.IgnoreQueryFilters().Where(e => eltern.Contains(e.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<AntragElternteil>(eltern, ct);
        }

        if (alle.Count > 0)
        {
            await db.Antraege.IgnoreQueryFilters().Where(a => alle.Contains(a.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Antrag>(alle, ct);
        }

        return (abgeschlossen.Count, entwuerfe.Count);
    }

    /// <summary>Änderungsanträge (V36): entschiedene oder zurückgezogene nach der Frist ab der Entscheidung (L32), gelöschte sofort, samt Feldern.</summary>
    /// <param name="entschiedenVor">Grenze für den Entscheidungszeitpunkt.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Anzahl gelöschter Anträge.</returns>
    private async Task<int> AenderungsantraegeAsync(DateTimeOffset entschiedenVor, CancellationToken ct)
    {
        var ids = await db.Aenderungsantraege.IgnoreQueryFilters()
            .Where(a => a.GeloeschtAm != null || (a.Status != AenderungsantragStatus.Offen && a.EntschiedenAm != null && a.EntschiedenAm < entschiedenVor))
            .Select(a => a.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return 0;
        }

        var felder = await db.Aenderungsfelder.IgnoreQueryFilters().Where(f => ids.Contains(f.AenderungsantragId)).Select(f => f.Id).ToListAsync(ct);
        await db.Aenderungsfelder.IgnoreQueryFilters().Where(f => felder.Contains(f.Id)).ExecuteDeleteAsync(ct);
        await db.Aenderungsantraege.IgnoreQueryFilters().Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync(ct);
        await db.ProtokollSchwaerzenAsync<Aenderungsfeld>(felder, ct);
        await db.ProtokollSchwaerzenAsync<Aenderungsantrag>(ids, ct);
        return ids.Count;
    }

    /// <summary>
    /// Altdatenimporte (V68, L41): Dateien nicht übernommener Läufe nach der Frist verwerfen und die Werte aus ihren Meldungen
    /// entfernen (der Lauf bleibt als Protokoll),
    /// abgeschlossene Läufe nach der Aufbewahrungsfrist des Änderungsprotokolls ganz löschen.
    /// </summary>
    /// <param name="dateiVor">Läufe, die seit diesem Zeitpunkt nicht geändert wurden, verlieren ihre Datei.</param>
    /// <param name="protokollVor">Abgeschlossene Läufe, die vor diesem Zeitpunkt angelegt wurden, werden gelöscht.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Anzahl verworfener und gelöschter Läufe.</returns>
    private async Task<int> ImporteAsync(DateTimeOffset dateiVor, DateTimeOffset protokollVor, CancellationToken ct)
    {
        // Einzeln per ExecuteUpdate, weil der Löschlauf geladene Entitäten anderer Regeln nicht vorzeitig speichern darf.
        var liegengeblieben = await db.Importlaeufe.IgnoreQueryFilters().AsNoTracking()
            .Where(l => l.Status == Fevad.Domain.Import.ImportlaufStatus.Vorbereitet && (l.GeaendertAm ?? l.ErstelltAm) < dateiVor)
            .Select(l => new { l.Id, l.Meldungen })
            .ToListAsync(ct);
        foreach (var lauf in liegengeblieben)
        {
            var meldungen = Fevad.Infrastructure.Import.Importdienst.MeldungenOhneWerte(lauf.Meldungen);
            await db.Importlaeufe.IgnoreQueryFilters().Where(l => l.Id == lauf.Id).ExecuteUpdateAsync(u => u
                .SetProperty(l => l.Status, Fevad.Domain.Import.ImportlaufStatus.Verworfen)
                .SetProperty(l => l.Inhalt, Array.Empty<byte>())
                .SetProperty(l => l.Meldungen, meldungen), ct);
        }

        var verworfen = liegengeblieben.Count;
        var ids = await db.Importlaeufe.IgnoreQueryFilters()
            .Where(l => l.Status != Fevad.Domain.Import.ImportlaufStatus.Vorbereitet && l.ErstelltAm < protokollVor)
            .Select(l => l.Id).ToListAsync(ct);
        if (ids.Count > 0)
        {
            await db.Importlaeufe.IgnoreQueryFilters().Where(l => ids.Contains(l.Id)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Fevad.Domain.Import.Importlauf>(ids, ct);
        }

        return verworfen + ids.Count;
    }

    /// <summary>
    /// Leert Konten, die länger als die Frist deaktiviert sind. Gelöscht wird nicht, weil Lesebestätigungen und
    /// Protokolleinträge auf die ID verweisen; übrig bleibt ein Konto ohne Namen, Adresse, Passwort und Rollen.
    /// </summary>
    private async Task<int> KontenAsync(DateTimeOffset grenze, DateTimeOffset jetzt, CancellationToken ct)
    {
        var ids = await db.Users.Where(u => u.DeaktiviertAm != null && u.DeaktiviertAm < grenze && u.AnonymisiertAm == null)
            .Select(u => u.Id).ToListAsync(ct);
        var anonymisiert = new List<Guid>();
        foreach (var id in ids)
        {
            var name = $"geloescht-{id:N}";
            // Bedingung erneut prüfen: ein inzwischen wieder aktiviertes Konto bleibt unberührt. Der neue
            // ConcurrencyStamp lässt eine Speicherung aus einem vorher geladenen Formular scheitern.
            var geaendert = await db.Users
                .Where(u => u.Id == id && u.DeaktiviertAm != null && u.DeaktiviertAm < grenze && u.AnonymisiertAm == null)
                .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.UserName, name)
                .SetProperty(u => u.NormalizedUserName, name.ToUpperInvariant())
                .SetProperty(u => u.Email, (string?)null)
                .SetProperty(u => u.NormalizedEmail, (string?)null)
                .SetProperty(u => u.EmailConfirmed, false)
                .SetProperty(u => u.PasswordHash, (string?)null)
                .SetProperty(u => u.PhoneNumber, (string?)null)
                .SetProperty(u => u.PhoneNumberConfirmed, false)
                .SetProperty(u => u.TwoFactorEnabled, false)
                .SetProperty(u => u.SecurityStamp, Guid.NewGuid().ToString())
                .SetProperty(u => u.KalenderToken, (string?)null)
                .SetProperty(u => u.MitgliedId, (Guid?)null)
                .SetProperty(u => u.LetzteAnmeldungAm, (DateTimeOffset?)null)
                .SetProperty(u => u.ConcurrencyStamp, Guid.NewGuid().ToString())
                .SetProperty(u => u.AnonymisiertAm, jetzt), ct);
            if (geaendert > 0)
            {
                anonymisiert.Add(id);
            }
        }

        if (anonymisiert.Count > 0)
        {
            await db.UserRoles.Where(r => anonymisiert.Contains(r.UserId)).ExecuteDeleteAsync(ct);
            await db.UserTokens.Where(t => anonymisiert.Contains(t.UserId)).ExecuteDeleteAsync(ct);
            await db.UserClaims.Where(c => anonymisiert.Contains(c.UserId)).ExecuteDeleteAsync(ct);
            await db.UserLogins.Where(l => anonymisiert.Contains(l.UserId)).ExecuteDeleteAsync(ct);
            await db.ProtokollSchwaerzenAsync<Benutzer>(anonymisiert, ct);
            await db.ProtokollSchwaerzenAsync<IdentityUserRole<Guid>>(anonymisiert, ct);
        }

        return anonymisiert.Count;
    }
}
