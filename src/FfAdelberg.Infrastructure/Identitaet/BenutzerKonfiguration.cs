using FfAdelberg.Domain.Mitglieder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FfAdelberg.Infrastructure.Identitaet;

internal sealed class BenutzerKonfiguration : IEntityTypeConfiguration<Benutzer>
{
    public void Configure(EntityTypeBuilder<Benutzer> builder)
    {
        builder.HasOne<Mitglied>()
            .WithMany()
            .HasForeignKey(b => b.MitgliedId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(b => b.MitgliedId).IsUnique();
    }
}
