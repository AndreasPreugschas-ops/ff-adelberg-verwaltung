using Fevad.Domain.Einsaetze;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

internal sealed class EinsatzKonfiguration : IEntityTypeConfiguration<Einsatz>
{
    public void Configure(EntityTypeBuilder<Einsatz> builder)
    {
        builder.HasIndex(e => e.Einsatznummer).IsUnique();
        builder.HasIndex(e => e.LeitstellenNummer);
        builder.HasIndex(e => e.Alarmiert);
        builder.Property(e => e.Einsatznummer).HasMaxLength(20);
        builder.Property(e => e.LeitstellenNummer).HasMaxLength(50);
        builder.Property(e => e.Stichwort).HasMaxLength(50);
        builder.HasOne(e => e.StatistikKategorie).WithMany().HasForeignKey(e => e.StatistikKategorieId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Einsatzleiter).WithMany().HasForeignKey(e => e.EinsatzleiterId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class EinsatzFahrzeugKonfiguration : IEntityTypeConfiguration<EinsatzFahrzeug>
{
    public void Configure(EntityTypeBuilder<EinsatzFahrzeug> builder)
    {
        builder.HasOne(f => f.Einsatz).WithMany(e => e.Fahrzeuge).HasForeignKey(f => f.EinsatzId);
        builder.HasOne(f => f.Fahrzeug).WithMany().HasForeignKey(f => f.FahrzeugId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EinsatzTeilnahmeKonfiguration : IEntityTypeConfiguration<EinsatzTeilnahme>
{
    public void Configure(EntityTypeBuilder<EinsatzTeilnahme> builder)
    {
        builder.HasOne(t => t.Einsatz).WithMany(e => e.Teilnahmen).HasForeignKey(t => t.EinsatzId);
        builder.HasOne(t => t.Mitglied).WithMany().HasForeignKey(t => t.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.EinsatzFahrzeug).WithMany().HasForeignKey(t => t.EinsatzFahrzeugId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(t => new { t.EinsatzId, t.MitgliedId }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}

internal sealed class AlarmImportKonfiguration : IEntityTypeConfiguration<AlarmImport>
{
    public void Configure(EntityTypeBuilder<AlarmImport> builder)
    {
        builder.HasIndex(a => new { a.Quelle, a.ExterneId }).IsUnique();
        builder.HasIndex(a => a.LoeschenAb);
        builder.Property(a => a.ExterneId).HasMaxLength(100);
        builder.Property(a => a.Rohdaten).HasColumnType("jsonb");
        builder.HasOne(a => a.Einsatz).WithMany().HasForeignKey(a => a.EinsatzId).OnDelete(DeleteBehavior.SetNull);
    }
}
