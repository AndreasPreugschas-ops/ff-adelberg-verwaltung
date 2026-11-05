using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Identitaet;

/// <summary>Datenbankabbildung von <see cref="Rollenprofil"/>.</summary>
internal sealed class RollenprofilKonfiguration : IEntityTypeConfiguration<Rollenprofil>
{
    /// <summary>Bildet <see cref="Rollenprofil"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Rollenprofil> builder)
    {
        builder.Property(r => r.Name).HasMaxLength(64);
        builder.Property(r => r.Bezeichnung).HasMaxLength(100);
        builder.Property(r => r.Beschreibung).HasMaxLength(500);
        builder.HasIndex(r => r.Name).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}

/// <summary>Datenbankabbildung von <see cref="RolleRecht"/>.</summary>
internal sealed class RolleRechtKonfiguration : IEntityTypeConfiguration<RolleRecht>
{
    /// <summary>Bildet <see cref="RolleRecht"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<RolleRecht> builder)
    {
        builder.ToTable("rollen_rechte");
        builder.Property(r => r.Rolle).HasMaxLength(64);
        builder.Property(r => r.Recht).HasMaxLength(64);
        builder.HasIndex(r => new { r.Rolle, r.Recht }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}

/// <summary>Datenbankabbildung von <see cref="RechtEingefuehrt"/>.</summary>
internal sealed class RechtEingefuehrtKonfiguration : IEntityTypeConfiguration<RechtEingefuehrt>
{
    /// <summary>Bildet <see cref="RechtEingefuehrt"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<RechtEingefuehrt> builder)
    {
        builder.ToTable("rechte_eingefuehrt");
        builder.Property(r => r.Recht).HasMaxLength(64);
        builder.HasIndex(r => r.Recht).IsUnique();
    }
}

/// <summary>Datenbankabbildung von <see cref="Rechtefreigabe"/>.</summary>
internal sealed class RechtefreigabeKonfiguration : IEntityTypeConfiguration<Rechtefreigabe>
{
    /// <summary>Bildet <see cref="Rechtefreigabe"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Rechtefreigabe> builder)
    {
        builder.ToTable("rechtefreigaben");
        builder.Property(r => r.Rolle).HasMaxLength(64);
        builder.Property(r => r.Recht).HasMaxLength(64);
        builder.HasIndex(r => r.Freigegeben);
        builder.Ignore(r => r.Offen);
    }
}
