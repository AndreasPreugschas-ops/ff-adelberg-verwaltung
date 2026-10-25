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

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<Benutzer, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    /// <summary>Schlüssel für Anmelde-Cookies und Einrichtungslinks; überleben so einen Neustart oder ein Update.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Parameter> Parameter => Set<Parameter>();
    public DbSet<Organisation> Organisationen => Set<Organisation>();
    public DbSet<AuditEintrag> AuditEintraege => Set<AuditEintrag>();
    public DbSet<Loeschnachweis> Loeschnachweise => Set<Loeschnachweis>();

    public DbSet<Abteilung> Abteilungen => Set<Abteilung>();
    public DbSet<Dienstgrad> Dienstgrade => Set<Dienstgrad>();
    public DbSet<Funktion> Funktionen => Set<Funktion>();
    public DbSet<Mitglied> Mitglieder => Set<Mitglied>();
    public DbSet<Mitgliedschaft> Mitgliedschaften => Set<Mitgliedschaft>();
    public DbSet<DienstgradZuordnung> DienstgradZuordnungen => Set<DienstgradZuordnung>();
    public DbSet<FunktionZuordnung> FunktionZuordnungen => Set<FunktionZuordnung>();
    public DbSet<Erziehungsberechtigter> Erziehungsberechtigte => Set<Erziehungsberechtigter>();
    public DbSet<FruehereAnschrift> FruehereAnschriften => Set<FruehereAnschrift>();
    public DbSet<Gruppe> Gruppen => Set<Gruppe>();
    public DbSet<GruppenMitgliedschaft> GruppenMitgliedschaften => Set<GruppenMitgliedschaft>();

    public DbSet<Ehrungsart> Ehrungsarten => Set<Ehrungsart>();
    public DbSet<Ehrung> Ehrungen => Set<Ehrung>();
    public DbSet<Pruefhinweis> Pruefhinweise => Set<Pruefhinweis>();
    public DbSet<Antrag> Antraege => Set<Antrag>();
    public DbSet<AntragElternteil> AntragElternteile => Set<AntragElternteil>();
    public DbSet<Befoerderungsweg> Befoerderungswege => Set<Befoerderungsweg>();

    public DbSet<Qualifikation> Qualifikationen => Set<Qualifikation>();
    public DbSet<QualifikationNachweis> QualifikationNachweise => Set<QualifikationNachweis>();
    public DbSet<Lehrgangsteilnahme> Lehrgangsteilnahmen => Set<Lehrgangsteilnahme>();
    public DbSet<Nachweisart> Nachweisarten => Set<Nachweisart>();
    public DbSet<Nachweis> Nachweise => Set<Nachweis>();
    public DbSet<Erinnerungslauf> Erinnerungslaeufe => Set<Erinnerungslauf>();

    public DbSet<AtemschutzVorsorge> AtemschutzVorsorgen => Set<AtemschutzVorsorge>();
    public DbSet<AtemschutzNachweis> AtemschutzNachweise => Set<AtemschutzNachweis>();
    /// <summary>Abmeldungen „nicht einsetzbar“ der Geräteträger (V62).</summary>
    public DbSet<AtemschutzSperre> AtemschutzSperren => Set<AtemschutzSperre>();

    public DbSet<Dienst> Dienste => Set<Dienst>();
    public DbSet<Anwesenheit> Anwesenheiten => Set<Anwesenheit>();
    public DbSet<Anwesenheitsjahressumme> Anwesenheitsjahressummen => Set<Anwesenheitsjahressumme>();
    public DbSet<Bankverbindung> Bankverbindungen => Set<Bankverbindung>();
    public DbSet<Fevad.Domain.Unfaelle.Unfall> Unfaelle => Set<Fevad.Domain.Unfaelle.Unfall>();
    public DbSet<Fevad.Domain.Unfaelle.UnfallZeuge> UnfallZeugen => Set<Fevad.Domain.Unfaelle.UnfallZeuge>();
    public DbSet<DienstSerie> DienstSerien => Set<DienstSerie>();
    public DbSet<Rueckmeldung> Rueckmeldungen => Set<Rueckmeldung>();

    public DbSet<StatistikKategorie> StatistikKategorien => Set<StatistikKategorie>();
    /// <summary>Katalog der Gefahrstoffgruppen für den Expositionsblock.</summary>
    public DbSet<Gefahrstoffgruppe> Gefahrstoffgruppen => Set<Gefahrstoffgruppe>();
    public DbSet<Einsatz> Einsaetze => Set<Einsatz>();
    public DbSet<EinsatzFahrzeug> EinsatzFahrzeuge => Set<EinsatzFahrzeug>();
    public DbSet<EinsatzTeilnahme> EinsatzTeilnahmen => Set<EinsatzTeilnahme>();
    public DbSet<AlarmImport> AlarmImporte => Set<AlarmImport>();
    public DbSet<EinsatzOrganisation> EinsatzOrganisationen => Set<EinsatzOrganisation>();
    public DbSet<EinsatzBeteiligter> EinsatzBeteiligte => Set<EinsatzBeteiligter>();
    public DbSet<EinsatzAusruestung> EinsatzAusruestungen => Set<EinsatzAusruestung>();
    /// <summary>Im Einsatz benutzte Geräte und Mengenartikel (V13).</summary>
    public DbSet<EinsatzGeraet> EinsatzGeraete => Set<EinsatzGeraet>();
    public DbSet<Kostensatz> Kostensaetze => Set<Kostensatz>();

    public DbSet<Abrechnung> Abrechnungen => Set<Abrechnung>();
    public DbSet<AbrechnungsPosten> AbrechnungsPosten => Set<AbrechnungsPosten>();

    public DbSet<Fahrzeug> Fahrzeuge => Set<Fahrzeug>();
    public DbSet<Geraeteart> Geraetearten => Set<Geraeteart>();
    public DbSet<PruefplanPosition> PruefplanPositionen => Set<PruefplanPosition>();
    public DbSet<Geraet> Geraete => Set<Geraet>();
    public DbSet<Material> Materialien => Set<Material>();
    public DbSet<Inventurgruppe> Inventurgruppen => Set<Inventurgruppe>();
    public DbSet<Pruefung> Pruefungen => Set<Pruefung>();
    public DbSet<Mangel> Maengel => Set<Mangel>();
    public DbSet<Inventur> Inventuren => Set<Inventur>();
    public DbSet<InventurPosition> InventurPositionen => Set<InventurPosition>();
    public DbSet<Beschaffung> Beschaffungen => Set<Beschaffung>();

    public DbSet<Dokumentkategorie> Dokumentkategorien => Set<Dokumentkategorie>();
    public DbSet<Dokument> Dokumente => Set<Dokument>();
    public DbSet<DokumentVersion> DokumentVersionen => Set<DokumentVersion>();
    public DbSet<DokumentDatei> DokumentDateien => Set<DokumentDatei>();
    public DbSet<Lesebestaetigung> Lesebestaetigungen => Set<Lesebestaetigung>();

    public DbSet<Sitzung> Sitzungen => Set<Sitzung>();
    public DbSet<SitzungTeilnahme> SitzungTeilnahmen => Set<SitzungTeilnahme>();
    public DbSet<Beschluss> Beschluesse => Set<Beschluss>();
    public DbSet<Wahl> Wahlen => Set<Wahl>();
    public DbSet<WahlBewerber> WahlBewerber => Set<WahlBewerber>();

    public DbSet<Artikel> Artikel => Set<Artikel>();
    public DbSet<Lagerbestand> Lagerbestaende => Set<Lagerbestand>();
    public DbSet<Kleiderkammervorgang> Kleiderkammervorgaenge => Set<Kleiderkammervorgang>();
    public DbSet<Ausstattung> Ausstattungen => Set<Ausstattung>();
    public DbSet<Groessenangabe> Groessenangaben => Set<Groessenangabe>();

    public DbSet<Rollenprofil> Rollenprofile => Set<Rollenprofil>();
    public DbSet<RolleRecht> RollenRechte => Set<RolleRecht>();
    public DbSet<RechtEingefuehrt> RechteEingefuehrt => Set<RechtEingefuehrt>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums als lesbarer Text statt Zahl: robust gegen Umsortieren und lesbar in SQL-Abfragen.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
    }

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
