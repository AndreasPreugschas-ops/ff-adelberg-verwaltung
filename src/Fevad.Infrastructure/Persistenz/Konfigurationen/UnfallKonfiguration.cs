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
        builder.Property(u => u.ArtDerVerletzung).HasMaxLength(200);
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
