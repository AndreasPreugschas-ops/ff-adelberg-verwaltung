using Fevad.Domain.Einsaetze;
using Fevad.Domain.Haushalt;
using Fevad.Domain.Technik;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

/// <summary>EF-Konfiguration für <see cref="Haushaltsstelle"/>.</summary>
internal sealed class HaushaltsstelleKonfiguration : IEntityTypeConfiguration<Haushaltsstelle>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Haushaltsstelle> builder)
    {
        builder.Property(h => h.Produkt).HasMaxLength(100);
        builder.Property(h => h.Kostenstelle).HasMaxLength(30);
        builder.Property(h => h.Innenauftrag).HasMaxLength(30);
        builder.Property(h => h.Sachkonto).HasMaxLength(30);
    }
}

/// <summary>EF-Konfiguration für <see cref="Haushaltsanmeldung"/>.</summary>
internal sealed class HaushaltsanmeldungKonfiguration : IEntityTypeConfiguration<Haushaltsanmeldung>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Haushaltsanmeldung> builder)
    {
        builder.HasIndex(a => a.Jahr).IsUnique();
        builder.Property(a => a.Ausschussbeschluss).HasMaxLength(200);
        builder.Property(a => a.Bemerkung).HasMaxLength(2000);
        builder.HasMany(a => a.Positionen).WithOne(p => p.Haushaltsanmeldung).HasForeignKey(p => p.HaushaltsanmeldungId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(a => a.Angemeldet);
        builder.Ignore(a => a.Offen);
        builder.Ignore(a => a.Zuwendungen);
    }
}

/// <summary>EF-Konfiguration für <see cref="Haushaltsposition"/>.</summary>
internal sealed class HaushaltspositionKonfiguration : IEntityTypeConfiguration<Haushaltsposition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Haushaltsposition> builder)
    {
        builder.HasOne(p => p.Haushaltsstelle).WithMany().HasForeignKey(p => p.HaushaltsstelleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Beschaffung>().WithMany().HasForeignKey(p => p.BeschaffungId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Fahrzeug>().WithMany().HasForeignKey(p => p.FahrzeugId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Mangel>().WithMany().HasForeignKey(p => p.MangelId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(p => new { p.HaushaltsanmeldungId, p.Herkunft });
        builder.Property(p => p.Bezeichnung).HasMaxLength(200);
        builder.Property(p => p.Begruendung).HasMaxLength(2000);
        builder.Property(p => p.Rechtsgrundlage).HasMaxLength(300);
        builder.Property(p => p.FolgenBeiAblehnung).HasMaxLength(1000);
        builder.Property(p => p.Herkunft).HasMaxLength(100);
        builder.Property(p => p.Bedarfsplan).HasMaxLength(200);
        foreach (var betrag in new[] { nameof(Haushaltsposition.Menge), nameof(Haushaltsposition.Einzelpreis), nameof(Haushaltsposition.RateFolgejahr1),
            nameof(Haushaltsposition.RateFolgejahr2), nameof(Haushaltsposition.RateFolgejahr3), nameof(Haushaltsposition.Verpflichtungsermaechtigung),
            nameof(Haushaltsposition.Zuwendung), nameof(Haushaltsposition.Folgekosten), nameof(Haushaltsposition.BeschlossenerBetrag) })
        {
            builder.Property(betrag).HasPrecision(12, 2);
        }

        builder.Ignore(p => p.Betrag);
        builder.Ignore(p => p.BetragPlanjahr);
    }
}

/// <summary>EF-Konfiguration für <see cref="Kostenansatz"/>.</summary>
internal sealed class KostenansatzKonfiguration : IEntityTypeConfiguration<Kostenansatz>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Kostenansatz> builder)
    {
        builder.Property(k => k.Satz).HasPrecision(10, 2);
        builder.Property(k => k.GeraeteartCode).HasMaxLength(20);
        builder.Property(k => k.HaushaltsstelleCode).HasMaxLength(20);
        builder.Property(k => k.Quelle).HasMaxLength(300);
    }
}
