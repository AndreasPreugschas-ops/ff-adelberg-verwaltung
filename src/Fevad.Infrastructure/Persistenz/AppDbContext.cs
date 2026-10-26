using System.Linq.Expressions;
using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Dokumente;
using Fevad.Domain.Einsaetze;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;
using Fevad.Domain.Laufbahn;
using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Identitaet;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Persistenz;

/// <summary>Datenbankkontext von FEVAD (PostgreSQL) mit ASP.NET Core Identity und Data-Protection-Schlüsseln.</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<Benutzer, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    /// <summary>Schlüssel für Anmelde-Cookies und Einrichtungslinks; überleben so einen Neustart oder ein Update.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>Tabelle der Parameter.</summary>
    public DbSet<Parameter> Parameter => Set<Parameter>();
    /// <summary>Spaltenzuordnungen des Finanzexports an die Kämmerei (V23).</summary>
    public DbSet<Exportformat> Exportformate => Set<Exportformat>();
    /// <summary>Tabelle der Organisationen.</summary>
    public DbSet<Organisation> Organisationen => Set<Organisation>();
    /// <summary>Tabelle der Audit-Einträge.</summary>
    public DbSet<AuditEintrag> AuditEintraege => Set<AuditEintrag>();
    /// <summary>Tabelle der Löschnachweise.</summary>
    public DbSet<Loeschnachweis> Loeschnachweise => Set<Loeschnachweis>();

    /// <summary>Tabelle der Abteilungen.</summary>
    public DbSet<Abteilung> Abteilungen => Set<Abteilung>();
    /// <summary>Tabelle der Dienstgrade.</summary>
    public DbSet<Dienstgrad> Dienstgrade => Set<Dienstgrad>();
    /// <summary>Tabelle der Funktionen.</summary>
    public DbSet<Funktion> Funktionen => Set<Funktion>();
    /// <summary>Tabelle der Mitglieder.</summary>
    public DbSet<Mitglied> Mitglieder => Set<Mitglied>();
    /// <summary>Tabelle der Mitgliedschaften.</summary>
    public DbSet<Mitgliedschaft> Mitgliedschaften => Set<Mitgliedschaft>();
    /// <summary>Tabelle der Dienstgrad-Zuordnungen.</summary>
    public DbSet<DienstgradZuordnung> DienstgradZuordnungen => Set<DienstgradZuordnung>();
    /// <summary>Tabelle der Funktion-Zuordnungen.</summary>
    public DbSet<FunktionZuordnung> FunktionZuordnungen => Set<FunktionZuordnung>();
    /// <summary>Tabelle der Erziehungsberechtigte.</summary>
    public DbSet<Erziehungsberechtigter> Erziehungsberechtigte => Set<Erziehungsberechtigter>();
    /// <summary>Arten im Einwilligungsregister (V39, V45).</summary>
    public DbSet<Einwilligungsart> Einwilligungsarten => Set<Einwilligungsart>();
    /// <summary>Erklärungen im Einwilligungsregister.</summary>
    public DbSet<Einwilligung> Einwilligungen => Set<Einwilligung>();
    /// <summary>Betreuer-Register der Jugend mit Stand der Führungszeugnisse (V47).</summary>
    public DbSet<Jugendbetreuer> Jugendbetreuer => Set<Jugendbetreuer>();
    /// <summary>Warteliste der Jugend- und Kinderfeuerwehr (V50).</summary>
    public DbSet<Interessent> Interessenten => Set<Interessent>();
    /// <summary>Antworten der Anträge je Einwilligungsart.</summary>
    public DbSet<AntragEinwilligung> AntragEinwilligungen => Set<AntragEinwilligung>();
    /// <summary>Löschanfragen der Mitglieder nach Art. 17 DSGVO.</summary>
    public DbSet<Loeschanfrage> Loeschanfragen => Set<Loeschanfrage>();
    /// <summary>Tabelle der Frühere-Anschriften.</summary>
    public DbSet<FruehereAnschrift> FruehereAnschriften => Set<FruehereAnschrift>();
    /// <summary>Änderungsanträge der Mitglieder für die eigenen Stammdaten (V36).</summary>
    public DbSet<Aenderungsantrag> Aenderungsantraege => Set<Aenderungsantrag>();
    /// <summary>Felder der Änderungsanträge mit altem und neuem Wert.</summary>
    public DbSet<Aenderungsfeld> Aenderungsfelder => Set<Aenderungsfeld>();
    /// <summary>Altdatenimporte aus CSV oder XLSX mit Datei, Zuordnung und Protokoll (V68).</summary>
    public DbSet<Fevad.Domain.Import.Importlauf> Importlaeufe => Set<Fevad.Domain.Import.Importlauf>();
    /// <summary>Gespeicherte Feldzuordnungen für wiederkehrende Exporte.</summary>
    public DbSet<Fevad.Domain.Import.Importvorlage> Importvorlagen => Set<Fevad.Domain.Import.Importvorlage>();
    /// <summary>Tabelle der Gruppen.</summary>
    public DbSet<Gruppe> Gruppen => Set<Gruppe>();
    /// <summary>Tabelle der Gruppen-Mitgliedschaften.</summary>
    public DbSet<GruppenMitgliedschaft> GruppenMitgliedschaften => Set<GruppenMitgliedschaft>();

    /// <summary>Tabelle der Ehrungsarten.</summary>
    public DbSet<Ehrungsart> Ehrungsarten => Set<Ehrungsart>();
    /// <summary>Tabelle der Ehrungen.</summary>
    public DbSet<Ehrung> Ehrungen => Set<Ehrung>();
    /// <summary>Tabelle der Prüfhinweise.</summary>
    public DbSet<Pruefhinweis> Pruefhinweise => Set<Pruefhinweis>();
    /// <summary>Tabelle der Anträge.</summary>
    public DbSet<Antrag> Antraege => Set<Antrag>();
    /// <summary>Tabelle der Antrag-Elternteile.</summary>
    public DbSet<AntragElternteil> AntragElternteile => Set<AntragElternteil>();
    /// <summary>Tabelle der Beförderungswege.</summary>
    public DbSet<Befoerderungsweg> Befoerderungswege => Set<Befoerderungsweg>();

    /// <summary>Tabelle der Qualifikationen.</summary>
    public DbSet<Qualifikation> Qualifikationen => Set<Qualifikation>();
    /// <summary>Tabelle der Qualifikation-Nachweise.</summary>
    public DbSet<QualifikationNachweis> QualifikationNachweise => Set<QualifikationNachweis>();
    /// <summary>Regelwerke für Jugendabzeichen, z. B. Jugendflamme nach Kreisrichtlinie (V50a).</summary>
    public DbSet<Abzeichenregelwerk> Abzeichenregelwerke => Set<Abzeichenregelwerk>();
    /// <summary>Regeln je Abzeichenstufe in einem Regelwerk.</summary>
    public DbSet<AbzeichenregelwerkStufe> AbzeichenregelwerkStufen => Set<AbzeichenregelwerkStufe>();
    /// <summary>Tabelle der Lehrgangsteilnahmen.</summary>
    public DbSet<Lehrgangsteilnahme> Lehrgangsteilnahmen => Set<Lehrgangsteilnahme>();
    /// <summary>Lehrgangswünsche der Mitglieder mit Stand und Herbstmeldung (V54).</summary>
    public DbSet<Lehrgangswunsch> Lehrgangswuensche => Set<Lehrgangswunsch>();
    /// <summary>Tabelle der Nachweisarten.</summary>
    public DbSet<Nachweisart> Nachweisarten => Set<Nachweisart>();
    /// <summary>Tabelle der Nachweise.</summary>
    public DbSet<Nachweis> Nachweise => Set<Nachweis>();
    /// <summary>Tabelle der Erinnerungsläufe.</summary>
    public DbSet<Erinnerungslauf> Erinnerungslaeufe => Set<Erinnerungslauf>();

    /// <summary>Tabelle der Atemschutz-Vorsorgen.</summary>
    public DbSet<AtemschutzVorsorge> AtemschutzVorsorgen => Set<AtemschutzVorsorge>();
    /// <summary>Tabelle der Atemschutz-Nachweise.</summary>
    public DbSet<AtemschutzNachweis> AtemschutzNachweise => Set<AtemschutzNachweis>();
    /// <summary>Abmeldungen „nicht einsetzbar“ der Geräteträger (V62).</summary>
    public DbSet<AtemschutzSperre> AtemschutzSperren => Set<AtemschutzSperre>();

    /// <summary>Tabelle der Dienste.</summary>
    public DbSet<Dienst> Dienste => Set<Dienst>();
    /// <summary>Katalog der Unterweisungsthemen (V53).</summary>
    public DbSet<Unterweisungsthema> Unterweisungsthemen => Set<Unterweisungsthema>();
    /// <summary>Katalog der Themen der Standortausbildung (V53).</summary>
    public DbSet<Ausbildungsthema> Ausbildungsthemen => Set<Ausbildungsthema>();
    /// <summary>Verlauf der Einsatzleitung (V14).</summary>
    public DbSet<Einsatzleitung> Einsatzleitungen => Set<Einsatzleitung>();
    /// <summary>Einsatzabschnitte (V14).</summary>
    public DbSet<Einsatzabschnitt> Einsatzabschnitte => Set<Einsatzabschnitt>();
    /// <summary>Einträge im Einsatztagebuch (V14).</summary>
    public DbSet<Tagebucheintrag> Tagebucheintraege => Set<Tagebucheintrag>();
    /// <summary>Katalog der Funktionen bei Einsatz und Übung (V14).</summary>
    public DbSet<Einsatzfunktion> Einsatzfunktionen => Set<Einsatzfunktion>();
    /// <summary>Einsatznachbesprechungen (V17).</summary>
    public DbSet<Nachbesprechung> Nachbesprechungen => Set<Nachbesprechung>();
    /// <summary>Maßnahmen aus Nachbesprechungen (V17).</summary>
    public DbSet<Massnahme> Massnahmen => Set<Massnahme>();
    /// <summary>Jahreskreis der Führungspflichten (V51).</summary>
    public DbSet<Fuehrungspflicht> Fuehrungspflichten => Set<Fuehrungspflicht>();
    /// <summary>Erledigungsvermerke zu Führungspflichten (V51).</summary>
    public DbSet<PflichtErledigung> PflichtErledigungen => Set<PflichtErledigung>();
    /// <summary>Chronik der Feuerwehr (V56, F25).</summary>
    public DbSet<ChronikEintrag> ChronikEintraege => Set<ChronikEintrag>();
    /// <summary>Vorlagen für Briefe, Urkunden und Serienmails (V56).</summary>
    public DbSet<Briefvorlage> Briefvorlagen => Set<Briefvorlage>();
    /// <summary>Vorgänge zur Dienstpflicht in der Mitgliederakte (V57).</summary>
    public DbSet<Dienstpflichtvorgang> Dienstpflichtvorgaenge => Set<Dienstpflichtvorgang>();
    /// <summary>Tabelle der Anwesenheiten.</summary>
    public DbSet<Anwesenheit> Anwesenheiten => Set<Anwesenheit>();
    /// <summary>Veranstaltungen der Jugend zu einem Dienst (V48).</summary>
    public DbSet<Jugendveranstaltung> Jugendveranstaltungen => Set<Jugendveranstaltung>();
    /// <summary>Anmeldungen zu Veranstaltungen der Jugend (V48).</summary>
    public DbSet<Veranstaltungsanmeldung> Veranstaltungsanmeldungen => Set<Veranstaltungsanmeldung>();
    /// <summary>Betreuer der Veranstaltungen der Jugend (V48).</summary>
    public DbSet<Veranstaltungsbetreuer> Veranstaltungsbetreuer => Set<Veranstaltungsbetreuer>();
    /// <summary>Zuschussgeber für Veranstaltungen der Jugend (V48).</summary>
    public DbSet<Zuschussgeber> Zuschussgeber => Set<Zuschussgeber>();
    /// <summary>Tabelle der Anwesenheitsjahressummen.</summary>
    public DbSet<Anwesenheitsjahressumme> Anwesenheitsjahressummen => Set<Anwesenheitsjahressumme>();
    /// <summary>Tabelle der Bankverbindungen.</summary>
    public DbSet<Bankverbindung> Bankverbindungen => Set<Bankverbindung>();
    /// <summary>Tabelle der Unfälle.</summary>
    public DbSet<Fevad.Domain.Unfaelle.Unfall> Unfaelle => Set<Fevad.Domain.Unfaelle.Unfall>();
    /// <summary>Tabelle der Unfall-Zeugen.</summary>
    public DbSet<Fevad.Domain.Unfaelle.UnfallZeuge> UnfallZeugen => Set<Fevad.Domain.Unfaelle.UnfallZeuge>();
    /// <summary>Katalog der Verletzungsarten (V40).</summary>
    public DbSet<Fevad.Domain.Unfaelle.Verletzungsart> Verletzungsarten => Set<Fevad.Domain.Unfaelle.Verletzungsart>();
    /// <summary>Sachschäden an privatem Eigentum nach § 17 FwG (V40).</summary>
    public DbSet<Fevad.Domain.Unfaelle.Sachschaden> Sachschaeden => Set<Fevad.Domain.Unfaelle.Sachschaden>();
    /// <summary>Fotos zu Unfall- und Schadensmeldungen (V40).</summary>
    public DbSet<Fevad.Domain.Unfaelle.Meldungsfoto> Meldungsfotos => Set<Fevad.Domain.Unfaelle.Meldungsfoto>();
    /// <summary>Tabelle der Dienst-Serien.</summary>
    public DbSet<DienstSerie> DienstSerien => Set<DienstSerie>();
    /// <summary>Tabelle der Rückmeldungen.</summary>
    public DbSet<Rueckmeldung> Rueckmeldungen => Set<Rueckmeldung>();
    /// <summary>Von Mitgliedern gemeldete Abwesenheiten (V37).</summary>
    public DbSet<Abwesenheit> Abwesenheiten => Set<Abwesenheit>();

    /// <summary>Tabelle der Statistik-Kategorien.</summary>
    public DbSet<StatistikKategorie> StatistikKategorien => Set<StatistikKategorie>();
    /// <summary>Katalog der Gefahrstoffgruppen für den Expositionsblock.</summary>
    public DbSet<Gefahrstoffgruppe> Gefahrstoffgruppen => Set<Gefahrstoffgruppe>();
    /// <summary>Tabelle der Einsätze.</summary>
    public DbSet<Einsatz> Einsaetze => Set<Einsatz>();
    /// <summary>Tabelle der Einsatz-Fahrzeuge.</summary>
    public DbSet<EinsatzFahrzeug> EinsatzFahrzeuge => Set<EinsatzFahrzeug>();
    /// <summary>Tabelle der Einsatz-Teilnahmen.</summary>
    public DbSet<EinsatzTeilnahme> EinsatzTeilnahmen => Set<EinsatzTeilnahme>();
    /// <summary>Tabelle der Alarm-Importe.</summary>
    public DbSet<AlarmImport> AlarmImporte => Set<AlarmImport>();
    /// <summary>Tabelle der Einsatz-Organisationen.</summary>
    public DbSet<EinsatzOrganisation> EinsatzOrganisationen => Set<EinsatzOrganisation>();
    /// <summary>Tabelle der Einsatz-Beteiligte.</summary>
    public DbSet<EinsatzBeteiligter> EinsatzBeteiligte => Set<EinsatzBeteiligter>();
    /// <summary>Tabelle der Einsatz-Ausrüstungen.</summary>
    public DbSet<EinsatzAusruestung> EinsatzAusruestungen => Set<EinsatzAusruestung>();
    /// <summary>Im Einsatz benutzte Geräte und Mengenartikel (V13).</summary>
    public DbSet<EinsatzGeraet> EinsatzGeraete => Set<EinsatzGeraet>();
    /// <summary>Tabelle der Kostensätze.</summary>
    public DbSet<Kostensatz> Kostensaetze => Set<Kostensatz>();
    /// <summary>Kostenersatz-Vorgänge je Einsatz (V21).</summary>
    public DbSet<Kostenersatzvorgang> Kostenersatzvorgaenge => Set<Kostenersatzvorgang>();
    /// <summary>Kostenschuldner der Vorgänge.</summary>
    public DbSet<Kostenschuldner> Kostenschuldner => Set<Kostenschuldner>();
    /// <summary>Kosten nach tatsächlichem Aufwand in den Vorgängen.</summary>
    public DbSet<Zusatzkosten> Zusatzkosten => Set<Zusatzkosten>();
    /// <summary>Kostenersatzbescheide an die Schuldner.</summary>
    public DbSet<Kostenbescheid> Kostenbescheide => Set<Kostenbescheid>();
    /// <summary>Schritte der Bescheide.</summary>
    public DbSet<BescheidEreignis> BescheidEreignisse => Set<BescheidEreignis>();
    /// <summary>Objekte mit aufgeschalteter Brandmeldeanlage und ihre Betreiber (V24).</summary>
    public DbSet<BmaObjekt> BmaObjekte => Set<BmaObjekt>();
    /// <summary>Kalkulationen des Personalstundensatzes nach § 34 Abs. 5 FwG (V24).</summary>
    public DbSet<Satzkalkulation> Satzkalkulationen => Set<Satzkalkulation>();

    /// <summary>Tabelle der Abrechnungen.</summary>
    public DbSet<Abrechnung> Abrechnungen => Set<Abrechnung>();
    /// <summary>Tabelle der Abrechnungsposten.</summary>
    public DbSet<AbrechnungsPosten> AbrechnungsPosten => Set<AbrechnungsPosten>();
    /// <summary>Anträge der Mitglieder auf Entschädigung für Lehrgänge und mehrtägige Einsätze (V32).</summary>
    public DbSet<Entschaedigungsantrag> Entschaedigungsantraege => Set<Entschaedigungsantrag>();
    /// <summary>Tage der Entschädigungsanträge.</summary>
    public DbSet<AntragTag> AntragTage => Set<AntragTag>();
    /// <summary>Belege der Entschädigungsanträge.</summary>
    public DbSet<AntragBeleg> AntragBelege => Set<AntragBeleg>();
    /// <summary>Tabelle der ausgestellten Arbeitgeberbescheinigungen.</summary>
    public DbSet<Arbeitgeberbescheinigung> Arbeitgeberbescheinigungen => Set<Arbeitgeberbescheinigung>();
    /// <summary>Tabelle der Erstattungen an Arbeitgeber nach § 15 Abs. 2 FwG.</summary>
    public DbSet<Arbeitgebererstattung> Arbeitgebererstattungen => Set<Arbeitgebererstattung>();

    /// <summary>Tabelle der Fahrzeuge.</summary>
    public DbSet<Fahrzeug> Fahrzeuge => Set<Fahrzeug>();
    /// <summary>Tabelle der Gerätearten.</summary>
    public DbSet<Geraeteart> Geraetearten => Set<Geraeteart>();
    /// <summary>Tabelle der Prüfplan-Positionen.</summary>
    public DbSet<PruefplanPosition> PruefplanPositionen => Set<PruefplanPosition>();
    /// <summary>Tabelle der Geräte.</summary>
    public DbSet<Geraet> Geraete => Set<Geraet>();
    /// <summary>Tabelle der Materialien.</summary>
    public DbSet<Material> Materialien => Set<Material>();
    /// <summary>Tabelle der Inventurgruppen.</summary>
    public DbSet<Inventurgruppe> Inventurgruppen => Set<Inventurgruppe>();
    /// <summary>Tabelle der Prüfungen.</summary>
    public DbSet<Pruefung> Pruefungen => Set<Pruefung>();
    /// <summary>Tabelle der Mängel.</summary>
    public DbSet<Mangel> Maengel => Set<Mangel>();
    /// <summary>Tabelle der Soll-Beladung je Fahrzeugfach (V59).</summary>
    public DbSet<Beladeposition> Beladepositionen => Set<Beladeposition>();
    /// <summary>Tabelle der Beladekontrollen (V59).</summary>
    public DbSet<Beladekontrolle> Beladekontrollen => Set<Beladekontrolle>();
    /// <summary>Tabelle der Werkstattaufträge (V61).</summary>
    public DbSet<Werkstattauftrag> Werkstattauftraege => Set<Werkstattauftrag>();
    /// <summary>Tabelle der Geräte in Werkstattaufträgen.</summary>
    public DbSet<Werkstattposition> Werkstattpositionen => Set<Werkstattposition>();
    /// <summary>Tabelle des Fahrtenbuchs.</summary>
    public DbSet<Fahrt> Fahrten => Set<Fahrt>();
    /// <summary>Tabelle der Betankungen.</summary>
    public DbSet<Betankung> Betankungen => Set<Betankung>();
    /// <summary>Tabelle der Fotos zu Mängeln.</summary>
    public DbSet<Mangelfoto> Mangelfotos => Set<Mangelfoto>();
    /// <summary>Tabelle der Werkstattprotokolle (Anhänge).</summary>
    public DbSet<Werkstattprotokoll> Werkstattprotokolle => Set<Werkstattprotokoll>();
    /// <summary>Katalog der Bauteilarten (V61).</summary>
    public DbSet<Bauteilart> Bauteilarten => Set<Bauteilart>();
    /// <summary>Tabelle der eingebauten Bauteile.</summary>
    public DbSet<Bauteil> Bauteile => Set<Bauteil>();
    /// <summary>Füllprotokoll der Atemluftflaschen (V61, abschaltbar).</summary>
    public DbSet<Flaschenfuellung> Flaschenfuellungen => Set<Flaschenfuellung>();

    /// <summary>Pflege-Lebenslauf der Einzelstücke (V63).</summary>
    public DbSet<Pflegeeintrag> Pflegeeintraege => Set<Pflegeeintrag>();
    /// <summary>Ausleihen und Fremdgeräte (V63).</summary>
    public DbSet<Ausleihe> Ausleihen => Set<Ausleihe>();
    /// <summary>Verbrauchsmaterial im Lager (V16).</summary>
    public DbSet<Lagerartikel> Lagerartikel => Set<Lagerartikel>();

    /// <summary>Katalog der Z-Feu-Fördergegenstände mit Festbeträgen (V30).</summary>
    public DbSet<Foerdergegenstand> Foerdergegenstaende => Set<Foerdergegenstand>();

    /// <summary>Z-Feu-Zuwendungsanträge zu Beschaffungen (V30).</summary>
    public DbSet<Foerderantrag> Foerderantraege => Set<Foerderantrag>();
    /// <summary>Chargen des Verbrauchsmaterials mit Verfall.</summary>
    public DbSet<Lagercharge> Lagerchargen => Set<Lagercharge>();
    /// <summary>Zu- und Abgänge im Lager.</summary>
    public DbSet<Lagerbewegung> Lagerbewegungen => Set<Lagerbewegung>();

    /// <summary>Tabelle der Inventuren.</summary>
    public DbSet<Inventur> Inventuren => Set<Inventur>();
    /// <summary>Tabelle der Inventur-Positionen.</summary>
    public DbSet<InventurPosition> InventurPositionen => Set<InventurPosition>();
    /// <summary>Tabelle der Beschaffungen.</summary>
    public DbSet<Beschaffung> Beschaffungen => Set<Beschaffung>();

    /// <summary>Tabelle der Dokumentkategorien.</summary>
    public DbSet<Dokumentkategorie> Dokumentkategorien => Set<Dokumentkategorie>();
    /// <summary>Tabelle der Dokumente.</summary>
    public DbSet<Dokument> Dokumente => Set<Dokument>();
    /// <summary>Tabelle der Dokument-Versionen.</summary>
    public DbSet<DokumentVersion> DokumentVersionen => Set<DokumentVersion>();
    /// <summary>Tabelle der Dokument-Dateien.</summary>
    public DbSet<DokumentDatei> DokumentDateien => Set<DokumentDatei>();
    /// <summary>Tabelle der Lesebestätigungen.</summary>
    public DbSet<Lesebestaetigung> Lesebestaetigungen => Set<Lesebestaetigung>();

    /// <summary>Tabelle der Sitzungen.</summary>
    public DbSet<Sitzung> Sitzungen => Set<Sitzung>();
    /// <summary>Tabelle der Sitzung-Teilnahmen.</summary>
    public DbSet<SitzungTeilnahme> SitzungTeilnahmen => Set<SitzungTeilnahme>();
    /// <summary>Tabelle der Beschlüsse.</summary>
    public DbSet<Beschluss> Beschluesse => Set<Beschluss>();
    /// <summary>Tabelle der Wahlen.</summary>
    public DbSet<Wahl> Wahlen => Set<Wahl>();
    /// <summary>Tabelle der Wahl-Bewerber.</summary>
    public DbSet<WahlBewerber> WahlBewerber => Set<WahlBewerber>();
    /// <summary>Vorgänge, zu denen die Gemeinde entscheiden muss (V27).</summary>
    public DbSet<Gemeindevorgang> Gemeindevorgaenge => Set<Gemeindevorgang>();
    /// <summary>Bescheide des Bürgermeisters an Mitglieder und Bewerber (V27).</summary>
    public DbSet<Mitgliederbescheid> Mitgliederbescheide => Set<Mitgliederbescheid>();
    /// <summary>Ausgestellte Dienstausweise (V27).</summary>
    public DbSet<Dienstausweis> Dienstausweise => Set<Dienstausweis>();

    /// <summary>Tabelle der Artikel.</summary>
    public DbSet<Artikel> Artikel => Set<Artikel>();
    /// <summary>Tabelle der Lagerbestände.</summary>
    public DbSet<Lagerbestand> Lagerbestaende => Set<Lagerbestand>();
    /// <summary>Tabelle der Kleiderkammervorgänge.</summary>
    public DbSet<Kleiderkammervorgang> Kleiderkammervorgaenge => Set<Kleiderkammervorgang>();
    /// <summary>Tabelle der Ausstattungen.</summary>
    public DbSet<Ausstattung> Ausstattungen => Set<Ausstattung>();
    /// <summary>Tabelle der Größenangaben.</summary>
    public DbSet<Groessenangabe> Groessenangaben => Set<Groessenangabe>();

    /// <summary>Kassen der Feuerwehr als Sondervermögen (V01).</summary>
    public DbSet<Fevad.Domain.Kassen.Kasse> Kassen => Set<Fevad.Domain.Kassen.Kasse>();
    /// <summary>Girokonten, Barkassen und Rücklagen der Kassen.</summary>
    public DbSet<Fevad.Domain.Kassen.Zahlungsmittelkonto> Zahlungsmittelkonten => Set<Fevad.Domain.Kassen.Zahlungsmittelkonto>();
    /// <summary>Gliederung der Wirtschaftspläne je Kasse.</summary>
    public DbSet<Fevad.Domain.Kassen.Planposition> Planpositionen => Set<Fevad.Domain.Kassen.Planposition>();
    /// <summary>Wirtschaftspläne je Kasse und Jahr.</summary>
    public DbSet<Fevad.Domain.Kassen.Wirtschaftsplan> Wirtschaftsplaene => Set<Fevad.Domain.Kassen.Wirtschaftsplan>();
    /// <summary>Ansätze der Wirtschaftspläne.</summary>
    public DbSet<Fevad.Domain.Kassen.Planansatz> Planansaetze => Set<Fevad.Domain.Kassen.Planansatz>();
    /// <summary>Kassenbuch (V02).</summary>
    public DbSet<Fevad.Domain.Kassen.Buchung> Buchungen => Set<Fevad.Domain.Kassen.Buchung>();
    /// <summary>Belegdateien des Kassenbuchs.</summary>
    public DbSet<Fevad.Domain.Kassen.Kassenbeleg> Kassenbelege => Set<Fevad.Domain.Kassen.Kassenbeleg>();
    /// <summary>Vorbereitete Ausgaben der Kassen.</summary>
    public DbSet<Fevad.Domain.Kassen.Zahlungsanordnung> Zahlungsanordnungen => Set<Fevad.Domain.Kassen.Zahlungsanordnung>();
    /// <summary>Veranstaltungen der Kassen als Kostenträger.</summary>
    public DbSet<Fevad.Domain.Kassen.Veranstaltung> Veranstaltungen => Set<Fevad.Domain.Kassen.Veranstaltung>();
    /// <summary>Barkassenberichte der Veranstaltungen.</summary>
    public DbSet<Fevad.Domain.Kassen.Barkassenbericht> Barkassenberichte => Set<Fevad.Domain.Kassen.Barkassenbericht>();
    /// <summary>Zeilen der Zählprotokolle.</summary>
    public DbSet<Fevad.Domain.Kassen.Zaehlposition> Zaehlpositionen => Set<Fevad.Domain.Kassen.Zaehlposition>();
    /// <summary>Wiederkehrende Buchungen der Kassen.</summary>
    public DbSet<Fevad.Domain.Kassen.WiederkehrendeBuchung> WiederkehrendeBuchungen => Set<Fevad.Domain.Kassen.WiederkehrendeBuchung>();
    /// <summary>CSV-Formate der Banken für den Kontoauszug-Import.</summary>
    public DbSet<Fevad.Domain.Kassen.Bankformat> Bankformate => Set<Fevad.Domain.Kassen.Bankformat>();
    /// <summary>Eingelesene Kontoauszugsdateien.</summary>
    public DbSet<Fevad.Domain.Kassen.Kontoauszugimport> Kontoauszugimporte => Set<Fevad.Domain.Kassen.Kontoauszugimport>();
    /// <summary>Umsätze aus Kontoauszügen.</summary>
    public DbSet<Fevad.Domain.Kassen.Kontoumsatz> Kontoumsaetze => Set<Fevad.Domain.Kassen.Kontoumsatz>();
    /// <summary>Bestandsverzeichnis der Kassen.</summary>
    public DbSet<Fevad.Domain.Kassen.Vermoegensgegenstand> Vermoegensgegenstaende => Set<Fevad.Domain.Kassen.Vermoegensgegenstand>();
    /// <summary>Jahresabschlüsse der Kassen.</summary>
    public DbSet<Fevad.Domain.Kassen.Jahresabschluss> Jahresabschluesse => Set<Fevad.Domain.Kassen.Jahresabschluss>();
    /// <summary>Endbestände der Konten in den Jahresabschlüssen.</summary>
    public DbSet<Fevad.Domain.Kassen.Kontoendbestand> Kontoendbestaende => Set<Fevad.Domain.Kassen.Kontoendbestand>();
    /// <summary>Kassenprüfungen durch die Rechnungsprüfer (V05).</summary>
    public DbSet<Fevad.Domain.Kassen.Kassenpruefung> Kassenpruefungen => Set<Fevad.Domain.Kassen.Kassenpruefung>();
    /// <summary>Beanstandungen der Kassenprüfung mit Antwort des Kassenwarts.</summary>
    public DbSet<Fevad.Domain.Kassen.Beanstandung> Beanstandungen => Set<Fevad.Domain.Kassen.Beanstandung>();
    /// <summary>Spenden- und Sponsoringregister der Kassen (V08).</summary>
    public DbSet<Fevad.Domain.Kassen.Zuwendung> Zuwendungen => Set<Fevad.Domain.Kassen.Zuwendung>();

    /// <summary>Tabelle der Rollenprofile.</summary>
    public DbSet<Rollenprofil> Rollenprofile => Set<Rollenprofil>();
    /// <summary>Tabelle der Rollen-Rechte.</summary>
    public DbSet<RolleRecht> RollenRechte => Set<RolleRecht>();
    /// <summary>Tabelle der eingeführten Rechte (für die einmalige Vorbelegung).</summary>
    public DbSet<RechtEingefuehrt> RechteEingefuehrt => Set<RechtEingefuehrt>();
    /// <summary>Angebote an das Archiv vor dem Löschen (V65).</summary>
    public DbSet<Archivangebot> Archivangebote => Set<Archivangebot>();
    /// <summary>Rechteänderungen, die auf die zweite Freigabe warten (F18).</summary>
    public DbSet<Rechtefreigabe> Rechtefreigaben => Set<Rechtefreigabe>();

    /// <inheritdoc/>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums als lesbarer Text statt Zahl: robust gegen Umsortieren und lesbar in SQL-Abfragen.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(Entitaet).IsAssignableFrom(clrType) || entityType.BaseType is not null)
            {
                continue;
            }

            var entity = builder.Entity(clrType);

            // Optimistische Nebenläufigkeit über die PostgreSQL-Systemspalte xmin.
            entity.Property<uint>("Version").IsRowVersion();

            // Soft-Delete: gelöschte Datensätze sind für normale Abfragen unsichtbar.
            var parameter = Expression.Parameter(clrType, "e");
            var filter = Expression.Lambda(
                Expression.Equal(
                    Expression.Property(parameter, nameof(Entitaet.GeloeschtAm)),
                    Expression.Constant(null, typeof(DateTimeOffset?))),
                parameter);
            entity.HasQueryFilter(filter);

            if (typeof(Katalogeintrag).IsAssignableFrom(clrType))
            {
                entity.HasIndex(nameof(Katalogeintrag.Code)).IsUnique();
                entity.Property(nameof(Katalogeintrag.Code)).HasMaxLength(20);
                entity.Property(nameof(Katalogeintrag.Bezeichnung)).HasMaxLength(100);
                entity.Property(nameof(Katalogeintrag.Kurzbezeichnung)).HasMaxLength(20);
            }
        }
    }
}
