using FfAdelberg.Domain.Mitglieder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FfAdelberg.Infrastructure.Persistenz.Konfigurationen;

internal sealed class MitgliedKonfiguration : IEntityTypeConfiguration<Mitglied>
{
    public void Configure(EntityTypeBuilder<Mitglied> builder)
    {
        builder.HasIndex(m => m.Personalnummer).IsUnique();
        builder.HasIndex(m => m.Fe2ExternalId).IsUnique();
        builder.HasIndex(m => new { m.Nachname, m.Vorname });
        builder.Property(m => m.Personalnummer).HasMaxLength(20);
        builder.Property(m => m.Vorname).HasMaxLength(100);
        builder.Property(m => m.Nachname).HasMaxLength(100);
        builder.Property(m => m.TelefonMobil).HasMaxLength(20);
        builder.Property(m => m.TelefonFestnetz).HasMaxLength(20);
        builder.Property(m => m.Email).HasMaxLength(200);
        builder.Ignore(m => m.Anzeigename);
    }
}

internal sealed class MitgliedschaftKonfiguration : IEntityTypeConfiguration<Mitgliedschaft>
{
    public void Configure(EntityTypeBuilder<Mitgliedschaft> builder)
    {
        builder.HasOne(m => m.Mitglied).WithMany(m => m.Mitgliedschaften).HasForeignKey(m => m.MitgliedId);
        builder.HasOne(m => m.Abteilung).WithMany().HasForeignKey(m => m.AbteilungId).OnDelete(DeleteBehavior.Restrict);

        // Höchstens eine offene Mitgliedschaft je Mitglied und Abteilung.
        builder.HasIndex(m => new { m.MitgliedId, m.AbteilungId })
            .IsUnique()
            .HasFilter("gueltig_bis IS NULL AND geloescht_am IS NULL");
    }
}

internal sealed class DienstgradZuordnungKonfiguration : IEntityTypeConfiguration<DienstgradZuordnung>
{
    public void Configure(EntityTypeBuilder<DienstgradZuordnung> builder)
    {
        builder.HasOne(d => d.Mitglied).WithMany(m => m.Dienstgrade).HasForeignKey(d => d.MitgliedId);
        builder.HasOne(d => d.Dienstgrad).WithMany().HasForeignKey(d => d.DienstgradId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FunktionZuordnungKonfiguration : IEntityTypeConfiguration<FunktionZuordnung>
{
    public void Configure(EntityTypeBuilder<FunktionZuordnung> builder)
    {
        builder.HasOne(f => f.Mitglied).WithMany(m => m.Funktionen).HasForeignKey(f => f.MitgliedId);
        builder.HasOne(f => f.Funktion).WithMany().HasForeignKey(f => f.FunktionId).OnDelete(DeleteBehavior.Restrict);
    }
}
