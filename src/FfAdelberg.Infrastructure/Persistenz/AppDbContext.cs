using System.Linq.Expressions;
using FfAdelberg.Domain.Atemschutz;
using FfAdelberg.Domain.Ausbildung;
using FfAdelberg.Domain.Dienstbetrieb;
using FfAdelberg.Domain.Einsaetze;
using FfAdelberg.Domain.Gemeinsam;
using FfAdelberg.Domain.Mitglieder;
using FfAdelberg.Domain.Technik;
using FfAdelberg.Infrastructure.Identitaet;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FfAdelberg.Infrastructure.Persistenz;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<Benutzer, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    /// <summary>Schlüssel für Anmelde-Cookies und Einrichtungslinks; überleben so einen Neustart oder ein Update.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Parameter> Parameter => Set<Parameter>();
    public DbSet<AuditEintrag> AuditEintraege => Set<AuditEintrag>();

    public DbSet<Abteilung> Abteilungen => Set<Abteilung>();
    public DbSet<Dienstgrad> Dienstgrade => Set<Dienstgrad>();
    public DbSet<Funktion> Funktionen => Set<Funktion>();
    public DbSet<Mitglied> Mitglieder => Set<Mitglied>();
    public DbSet<Mitgliedschaft> Mitgliedschaften => Set<Mitgliedschaft>();
    public DbSet<DienstgradZuordnung> DienstgradZuordnungen => Set<DienstgradZuordnung>();
    public DbSet<FunktionZuordnung> FunktionZuordnungen => Set<FunktionZuordnung>();

    public DbSet<Qualifikation> Qualifikationen => Set<Qualifikation>();
    public DbSet<QualifikationNachweis> QualifikationNachweise => Set<QualifikationNachweis>();

    public DbSet<AtemschutzVorsorge> AtemschutzVorsorgen => Set<AtemschutzVorsorge>();
    public DbSet<AtemschutzNachweis> AtemschutzNachweise => Set<AtemschutzNachweis>();

    public DbSet<Dienst> Dienste => Set<Dienst>();
    public DbSet<Anwesenheit> Anwesenheiten => Set<Anwesenheit>();

    public DbSet<StatistikKategorie> StatistikKategorien => Set<StatistikKategorie>();
    public DbSet<Einsatz> Einsaetze => Set<Einsatz>();
    public DbSet<EinsatzFahrzeug> EinsatzFahrzeuge => Set<EinsatzFahrzeug>();
    public DbSet<EinsatzTeilnahme> EinsatzTeilnahmen => Set<EinsatzTeilnahme>();
    public DbSet<AlarmImport> AlarmImporte => Set<AlarmImport>();

    public DbSet<Fahrzeug> Fahrzeuge => Set<Fahrzeug>();
    public DbSet<Geraeteart> Geraetearten => Set<Geraeteart>();
    public DbSet<PruefplanPosition> PruefplanPositionen => Set<PruefplanPosition>();
    public DbSet<Geraet> Geraete => Set<Geraet>();
    public DbSet<Pruefung> Pruefungen => Set<Pruefung>();
    public DbSet<Mangel> Maengel => Set<Mangel>();

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
