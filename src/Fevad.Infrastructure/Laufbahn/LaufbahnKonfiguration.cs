using Fevad.Domain.Laufbahn;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Laufbahn;

internal sealed class EhrungsartKonfiguration : IEntityTypeConfiguration<Ehrungsart>
{
    public void Configure(EntityTypeBuilder<Ehrungsart> builder)
    {
        builder.Property(e => e.Reihe).HasMaxLength(20);
        builder.Property(e => e.Verleiher).HasMaxLength(100);
    }
}

internal sealed class EhrungKonfiguration : IEntityTypeConfiguration<Ehrung>
{
    public void Configure(EntityTypeBuilder<Ehrung> builder)
    {
        builder.HasOne(e => e.Mitglied).WithMany().HasForeignKey(e => e.MitgliedId);
        builder.HasOne(e => e.Ehrungsart).WithMany().HasForeignKey(e => e.EhrungsartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.MitgliedId, e.EhrungsartId });
        builder.HasIndex(e => e.VerliehenAm);
        builder.Property(e => e.Urkundennummer).HasMaxLength(50);
        builder.Property(e => e.Bemerkung).HasMaxLength(500);
    }
}

internal sealed class BefoerderungswegKonfiguration : IEntityTypeConfiguration<Befoerderungsweg>
{
    public void Configure(EntityTypeBuilder<Befoerderungsweg> builder)
    {
        builder.HasOne(b => b.Dienstgrad).WithMany().HasForeignKey(b => b.DienstgradId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(b => b.DienstgradId);
        builder.Property(b => b.Bezeichnung).HasMaxLength(50);
        builder.Property(b => b.Hinweis).HasMaxLength(300);
    }
}
