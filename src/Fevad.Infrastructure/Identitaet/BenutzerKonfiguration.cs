using Fevad.Domain.Mitglieder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Identitaet;

internal sealed class BenutzerKonfiguration : IEntityTypeConfiguration<Benutzer>
{
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
