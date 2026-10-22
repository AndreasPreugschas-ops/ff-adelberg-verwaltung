using Fevad.Domain.Technik;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

internal sealed class FahrzeugKonfiguration : IEntityTypeConfiguration<Fahrzeug>
{
    public void Configure(EntityTypeBuilder<Fahrzeug> builder)
    {
        builder.HasIndex(f => f.Funkrufname).IsUnique();
        builder.Property(f => f.Funkrufname).HasMaxLength(50);
        builder.Property(f => f.Typ).HasMaxLength(50);
        builder.Property(f => f.Kennzeichen).HasMaxLength(20);
    }
}

internal sealed class PruefplanPositionKonfiguration : IEntityTypeConfiguration<PruefplanPosition>
{
    public void Configure(EntityTypeBuilder<PruefplanPosition> builder)
    {
        builder.HasOne(p => p.Geraeteart).WithMany(g => g.Pruefplan).HasForeignKey(p => p.GeraeteartId);
        builder.Property(p => p.Checkliste).HasColumnType("jsonb");
    }
}

internal sealed class GeraetKonfiguration : IEntityTypeConfiguration<Geraet>
{
    public void Configure(EntityTypeBuilder<Geraet> builder)
    {
        builder.HasIndex(g => g.Inventarnummer).IsUnique();
        builder.Property(g => g.Inventarnummer).HasMaxLength(30);
        builder.Property(g => g.Bezeichnung).HasMaxLength(200);
        builder.HasOne(g => g.Geraeteart).WithMany().HasForeignKey(g => g.GeraeteartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.Fahrzeug).WithMany(f => f.Geraete).HasForeignKey(g => g.FahrzeugId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PruefungKonfiguration : IEntityTypeConfiguration<Pruefung>
{
    public void Configure(EntityTypeBuilder<Pruefung> builder)
    {
        builder.HasOne(p => p.Geraet).WithMany(g => g.Pruefungen).HasForeignKey(p => p.GeraetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Fahrzeug).WithMany().HasForeignKey(p => p.FahrzeugId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.PruefplanPosition).WithMany().HasForeignKey(p => p.PruefplanPositionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(p => p.Pruefer).WithMany().HasForeignKey(p => p.PrueferId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(p => p.Ergebnisse).HasColumnType("jsonb");
        builder.HasIndex(p => p.NaechsteFaellig);
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_pruefungen_geraet_oder_fahrzeug",
            "(geraet_id IS NULL) <> (fahrzeug_id IS NULL)"));
    }
}

internal sealed class MangelKonfiguration : IEntityTypeConfiguration<Mangel>
{
    public void Configure(EntityTypeBuilder<Mangel> builder)
    {
        builder.HasOne(m => m.Geraet).WithMany().HasForeignKey(m => m.GeraetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Fahrzeug).WithMany().HasForeignKey(m => m.FahrzeugId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.GemeldetVon).WithMany().HasForeignKey(m => m.GemeldetVonId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_maengel_geraet_oder_fahrzeug",
            "(geraet_id IS NULL) <> (fahrzeug_id IS NULL)"));
    }
}
