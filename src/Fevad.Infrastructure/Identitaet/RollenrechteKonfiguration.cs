using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Identitaet;

internal sealed class RollenprofilKonfiguration : IEntityTypeConfiguration<Rollenprofil>
{
    public void Configure(EntityTypeBuilder<Rollenprofil> builder)
    {
        builder.Property(r => r.Name).HasMaxLength(64);
        builder.Property(r => r.Bezeichnung).HasMaxLength(100);
        builder.Property(r => r.Beschreibung).HasMaxLength(500);
        builder.HasIndex(r => r.Name).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}

internal sealed class RolleRechtKonfiguration : IEntityTypeConfiguration<RolleRecht>
{
    public void Configure(EntityTypeBuilder<RolleRecht> builder)
    {
        builder.ToTable("rollen_rechte");
        builder.Property(r => r.Rolle).HasMaxLength(64);
        builder.Property(r => r.Recht).HasMaxLength(64);
        builder.HasIndex(r => new { r.Rolle, r.Recht }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}

internal sealed class RechtEingefuehrtKonfiguration : IEntityTypeConfiguration<RechtEingefuehrt>
{
    public void Configure(EntityTypeBuilder<RechtEingefuehrt> builder)
    {
        builder.ToTable("rechte_eingefuehrt");
        builder.Property(r => r.Recht).HasMaxLength(64);
        builder.HasIndex(r => r.Recht).IsUnique();
    }
}
