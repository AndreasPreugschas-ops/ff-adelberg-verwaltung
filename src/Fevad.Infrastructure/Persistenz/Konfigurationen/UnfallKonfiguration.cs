using Fevad.Domain.Unfaelle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

internal sealed class UnfallKonfiguration : IEntityTypeConfiguration<Unfall>
{
    public void Configure(EntityTypeBuilder<Unfall> builder)
    {
        builder.HasOne(u => u.Mitglied).WithMany().HasForeignKey(u => u.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Fevad.Domain.Einsaetze.Einsatz>().WithMany().HasForeignKey(u => u.EinsatzId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Fevad.Domain.Dienstbetrieb.Dienst>().WithMany().HasForeignKey(u => u.DienstId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(u => u.Ort).HasMaxLength(200);
        builder.Property(u => u.Taetigkeit).HasMaxLength(200);
        builder.Property(u => u.Hergang).HasMaxLength(4000);
        builder.Property(u => u.VerletzteKoerperteile).HasMaxLength(200);
        builder.HasOne(u => u.Verletzungsart).WithMany().HasForeignKey(u => u.VerletzungsartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(u => u.Fotos).WithOne().HasForeignKey(f => f.UnfallId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(u => u.Erstbehandlung).HasMaxLength(200);
        builder.Property(u => u.Krankenkasse).HasMaxLength(100);
        builder.Property(u => u.ZuerstKenntnis).HasMaxLength(200);
        builder.Property(u => u.Aktenzeichen).HasMaxLength(50);
        builder.Property(u => u.Bemerkung).HasMaxLength(1000);
        builder.Ignore(u => u.Anzeigepflichtig);
        builder.HasIndex(u => u.Zeitpunkt);
    }
}

internal sealed class UnfallZeugeKonfiguration : IEntityTypeConfiguration<UnfallZeuge>
{
    public void Configure(EntityTypeBuilder<UnfallZeuge> builder)
    {
        builder.HasOne(z => z.Unfall).WithMany(u => u.Zeugen).HasForeignKey(z => z.UnfallId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(z => z.Name).HasMaxLength(200);
        builder.Property(z => z.Kontakt).HasMaxLength(300);
    }
}

/// <summary>Sachschäden nach § 17 FwG (V40).</summary>
internal sealed class SachschadenKonfiguration : IEntityTypeConfiguration<Sachschaden>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Sachschaden> builder)
    {
        builder.HasOne(s => s.Mitglied).WithMany().HasForeignKey(s => s.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Fevad.Domain.Einsaetze.Einsatz>().WithMany().HasForeignKey(s => s.EinsatzId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Fevad.Domain.Dienstbetrieb.Dienst>().WithMany().HasForeignKey(s => s.DienstId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(s => s.Fotos).WithOne().HasForeignKey(f => f.SachschadenId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(s => s.Ort).HasMaxLength(200);
        builder.Property(s => s.Gegenstand).HasMaxLength(200);
        builder.Property(s => s.Hergang).HasMaxLength(4000);
        builder.Property(s => s.Betrag).HasPrecision(10, 2);
        builder.Property(s => s.ErstattetBetrag).HasPrecision(10, 2);
        builder.Property(s => s.RegressBemerkung).HasMaxLength(1000);
        builder.Property(s => s.Bemerkung).HasMaxLength(1000);
        builder.Ignore(s => s.Abgeschlossen);
        builder.HasIndex(s => s.Zeitpunkt);
    }
}

/// <summary>Fotos zu Meldungen; genau ein Bezug (Unfall oder Sachschaden).</summary>
internal sealed class MeldungsfotoKonfiguration : IEntityTypeConfiguration<Meldungsfoto>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Meldungsfoto> builder)
    {
        builder.Property(f => f.Dateiname).HasMaxLength(200);
        builder.Property(f => f.Inhaltstyp).HasMaxLength(100);
        builder.ToTable(t => t.HasCheckConstraint("ck_meldungsfotos_bezug", "(unfall_id IS NULL) <> (sachschaden_id IS NULL)"));
    }
}
