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
    /// <summary>Tabelle der Lehrgangsteilnahmen.</summary>
    public DbSet<Lehrgangsteilnahme> Lehrgangsteilnahmen => Set<Lehrgangsteilnahme>();
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
    /// <summary>Tabelle der Anwesenheiten.</summary>
    public DbSet<Anwesenheit> Anwesenheiten => Set<Anwesenheit>();
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

    /// <summary>Tabelle der Abrechnungen.</summary>
    public DbSet<Abrechnung> Abrechnungen => Set<Abrechnung>();
    /// <summary>Tabelle der Abrechnungsposten.</summary>
    public DbSet<AbrechnungsPosten> AbrechnungsPosten => Set<AbrechnungsPosten>();

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

    /// <summary>Tabelle der Rollenprofile.</summary>
    public DbSet<Rollenprofil> Rollenprofile => Set<Rollenprofil>();
    /// <summary>Tabelle der Rollen-Rechte.</summary>
    public DbSet<RolleRecht> RollenRechte => Set<RolleRecht>();
    /// <summary>Tabelle der eingeführten Rechte (für die einmalige Vorbelegung).</summary>
    public DbSet<RechtEingefuehrt> RechteEingefuehrt => Set<RechtEingefuehrt>();

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
