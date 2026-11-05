using Fevad.Domain.Mitglieder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Identitaet;

/// <summary>Datenbankabbildung von <see cref="Benutzer"/>.</summary>
internal sealed class BenutzerKonfiguration : IEntityTypeConfiguration<Benutzer>
{
    /// <summary>Bildet <see cref="Benutzer"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Benutzer> builder)
    {
        builder.HasOne<Mitglied>()
            .WithMany()
            .HasForeignKey(b => b.MitgliedId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(b => b.MitgliedId).IsUnique();
        builder.HasIndex(b => b.KalenderToken).IsUnique();
        builder.Property(b => b.KalenderToken).HasMaxLength(64);
        // Flags als Zahl: die Kombination mehrerer Arten passt nicht in die Textspalte der Enum-Konvention.
        builder.Property(b => b.SammelmailOhne).HasConversion<int>();
    }
}
