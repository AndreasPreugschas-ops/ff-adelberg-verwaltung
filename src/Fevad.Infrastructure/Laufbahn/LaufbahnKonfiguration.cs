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
        builder.Property(e => e.FunktionCode).HasMaxLength(20);
        builder.Property(e => e.Antragsweg).HasMaxLength(300);
        builder.Property(e => e.Kontingent).HasMaxLength(200);
        builder.HasOne(e => e.Vorstufe).WithMany().HasForeignKey(e => e.VorstufeId).OnDelete(DeleteBehavior.Restrict);
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

internal sealed class PruefhinweisKonfiguration : IEntityTypeConfiguration<Pruefhinweis>
{
    public void Configure(EntityTypeBuilder<Pruefhinweis> builder)
    {
        builder.HasOne(p => p.Mitglied).WithMany().HasForeignKey(p => p.MitgliedId);
        builder.HasIndex(p => new { p.MitgliedId, p.Schluessel }).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.HasIndex(p => p.Status);
        builder.Property(p => p.Schluessel).HasMaxLength(120);
        builder.Property(p => p.Text).HasMaxLength(500);
    }
}
