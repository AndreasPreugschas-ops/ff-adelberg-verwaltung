using Fevad.Domain.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

/// <summary>Importläufe (V68): Datei als bytea, Zuordnung und Meldungen als Text.</summary>
internal sealed class ImportlaufKonfiguration : IEntityTypeConfiguration<Importlauf>
{
    /// <summary>Bildet <see cref="Importlauf"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Importlauf> builder)
    {
        builder.Property(l => l.Dateiname).HasMaxLength(200);
        builder.Property(l => l.Freigabe).HasMaxLength(500);
        builder.Property(l => l.UebernommenVon).HasMaxLength(256);
        builder.HasIndex(l => l.Status);
    }
}

/// <summary>Importvorlagen: Name je Ziel eindeutig.</summary>
internal sealed class ImportvorlageKonfiguration : IEntityTypeConfiguration<Importvorlage>
{
    /// <summary>Bildet <see cref="Importvorlage"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Importvorlage> builder)
    {
        builder.Property(v => v.Name).HasMaxLength(100);
        builder.HasIndex(v => new { v.Ziel, v.Name }).IsUnique();
    }
}
