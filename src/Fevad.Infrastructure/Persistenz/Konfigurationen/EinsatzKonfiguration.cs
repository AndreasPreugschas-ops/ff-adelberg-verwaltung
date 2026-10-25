using Fevad.Domain.Einsaetze;
using Fevad.Domain.Technik;
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
        builder.Property(t => t.PaGeraet).HasMaxLength(50);
        builder.Property(t => t.Maske).HasMaxLength(50);
        builder.Property(t => t.Gefahrstoffe).HasMaxLength(200);
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

internal sealed class EinsatzOrganisationKonfiguration : IEntityTypeConfiguration<EinsatzOrganisation>
{
    public void Configure(EntityTypeBuilder<EinsatzOrganisation> builder)
    {
        builder.HasOne(o => o.Einsatz).WithMany(e => e.Organisationen).HasForeignKey(o => o.EinsatzId);
        builder.Property(o => o.Bezeichnung).HasMaxLength(200);
        builder.Property(o => o.Ansprechpartner).HasMaxLength(200);
        builder.Property(o => o.Telefon).HasMaxLength(50);
        builder.Property(o => o.Aktenzeichen).HasMaxLength(100);
        builder.Property(o => o.Bemerkung).HasMaxLength(1000);
    }
}

internal sealed class EinsatzBeteiligterKonfiguration : IEntityTypeConfiguration<EinsatzBeteiligter>
{
    public void Configure(EntityTypeBuilder<EinsatzBeteiligter> builder)
    {
        builder.HasOne(b => b.Einsatz).WithMany(e => e.Beteiligte).HasForeignKey(b => b.EinsatzId);
        builder.Property(b => b.Name).HasMaxLength(200);
        builder.Property(b => b.Anschrift).HasMaxLength(300);
        builder.Property(b => b.Telefon).HasMaxLength(50);
        builder.Property(b => b.Kennzeichen).HasMaxLength(20);
        builder.Property(b => b.Fahrzeugbeschreibung).HasMaxLength(200);
        builder.Property(b => b.Versicherung).HasMaxLength(200);
        builder.Property(b => b.Versicherungsnummer).HasMaxLength(100);
        builder.Property(b => b.Bemerkung).HasMaxLength(1000);
    }
}

internal sealed class EinsatzAusruestungKonfiguration : IEntityTypeConfiguration<EinsatzAusruestung>
{
    public void Configure(EntityTypeBuilder<EinsatzAusruestung> builder)
    {
        builder.HasOne(a => a.Einsatz).WithMany(e => e.Ausruestung).HasForeignKey(a => a.EinsatzId);
        builder.HasOne(a => a.Kostensatz).WithMany().HasForeignKey(a => a.KostensatzId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(a => a.Menge).HasPrecision(10, 2);
        builder.Property(a => a.Bemerkung).HasMaxLength(500);
    }
}

/// <summary>Tabelle einsatz_geraete: entweder Einzelgerät oder Materialstamm, nie beides.</summary>
internal sealed class EinsatzGeraetKonfiguration : IEntityTypeConfiguration<EinsatzGeraet>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EinsatzGeraet> builder)
    {
        builder.HasOne(g => g.Einsatz).WithMany(e => e.Geraete).HasForeignKey(g => g.EinsatzId);
        builder.HasOne(g => g.Geraet).WithMany().HasForeignKey(g => g.GeraetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.Material).WithMany().HasForeignKey(g => g.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.Fahrzeug).WithMany().HasForeignKey(g => g.FahrzeugId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Mangel>().WithMany().HasForeignKey(g => g.MangelId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(g => g.Bemerkung).HasMaxLength(500);
        builder.HasIndex(g => g.ErledigtAm);
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_einsatz_geraete_geraet_oder_material",
            "(geraet_id IS NULL) <> (material_id IS NULL)"));
    }
}

internal sealed class KostensatzKonfiguration : IEntityTypeConfiguration<Kostensatz>
{
    public void Configure(EntityTypeBuilder<Kostensatz> builder)
    {
        builder.HasIndex(k => new { k.Art, k.FahrzeugId, k.GueltigAb });
        builder.Property(k => k.Bezeichnung).HasMaxLength(200);
        builder.Property(k => k.Quelle).HasMaxLength(200);
        builder.Property(k => k.Betrag).HasPrecision(10, 2);
        builder.HasOne(k => k.Fahrzeug).WithMany().HasForeignKey(k => k.FahrzeugId).OnDelete(DeleteBehavior.Restrict);
    }
}
