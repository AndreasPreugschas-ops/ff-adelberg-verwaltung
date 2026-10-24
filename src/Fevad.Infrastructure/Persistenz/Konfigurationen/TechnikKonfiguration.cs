using Fevad.Domain.Technik;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

internal sealed class FahrzeugKonfiguration : IEntityTypeConfiguration<Fahrzeug>
{
    public void Configure(EntityTypeBuilder<Fahrzeug> builder)
    {
        builder.HasIndex(f => f.Funkrufname).IsUnique();
        builder.Property(f => f.Funkrufname).HasMaxLength(50);
        builder.Property(f => f.Typ).HasMaxLength(50);
        builder.Property(f => f.Kennzeichen).HasMaxLength(20);
        builder.Property(f => f.Bemerkung).HasMaxLength(1000);
        builder.HasOne(f => f.Pruefvorlage).WithMany().HasForeignKey(f => f.PruefvorlageId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PruefplanPositionKonfiguration : IEntityTypeConfiguration<PruefplanPosition>
{
    public void Configure(EntityTypeBuilder<PruefplanPosition> builder)
    {
        builder.HasOne(p => p.Geraeteart).WithMany(g => g.Pruefplan).HasForeignKey(p => p.GeraeteartId);
        builder.Property(p => p.Checkliste).HasColumnType("jsonb");
        builder.Property(p => p.Pruefer).HasMaxLength(200);
    }
}

internal sealed class GeraetKonfiguration : IEntityTypeConfiguration<Geraet>
{
    public void Configure(EntityTypeBuilder<Geraet> builder)
    {
        builder.HasIndex(g => g.Inventarnummer).IsUnique();
        builder.Property(g => g.Inventarnummer).HasMaxLength(30);
        builder.Property(g => g.Bezeichnung).HasMaxLength(200);
        builder.Property(g => g.Seriennummer).HasMaxLength(100);
        builder.Property(g => g.Groesse).HasMaxLength(20);
        builder.Property(g => g.Lagerort).HasMaxLength(50);
        builder.Property(g => g.Aussonderungsgrund).HasMaxLength(200);
        builder.Property(g => g.Bemerkung).HasMaxLength(1000);
        builder.Property(g => g.Anschaffungspreis).HasPrecision(10, 2);
        builder.HasOne<Beschaffung>().WithMany().HasForeignKey(g => g.BeschaffungId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(g => g.Geraeteart).WithMany().HasForeignKey(g => g.GeraeteartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.Fahrzeug).WithMany(f => f.Geraete).HasForeignKey(g => g.FahrzeugId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(g => g.Material).WithMany(m => m.Geraete).HasForeignKey(g => g.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.Inventurgruppe).WithMany().HasForeignKey(g => g.InventurgruppeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MaterialKonfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.Property(m => m.Bezeichnung).HasMaxLength(200);
        builder.Property(m => m.Hersteller).HasMaxLength(100);
        builder.Property(m => m.Typ).HasMaxLength(100);
        builder.Property(m => m.Herstellerteilenummer).HasMaxLength(100);
        builder.Property(m => m.Bemerkung).HasMaxLength(1000);
        builder.Property(m => m.Stueckwert).HasPrecision(10, 2);
        builder.HasOne(m => m.Geraeteart).WithMany().HasForeignKey(m => m.GeraeteartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => m.Herstellerteilenummer);
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_materialien_inventur_nur_mit_inventarisierung",
            "NOT inventurpflichtig OR inventarisierungspflichtig"));
    }
}

internal sealed class InventurgruppeKonfiguration : IEntityTypeConfiguration<Inventurgruppe>
{
    public void Configure(EntityTypeBuilder<Inventurgruppe> builder)
    {
        builder.Property(g => g.Bezeichnung).HasMaxLength(100);
        builder.Property(g => g.Beschreibung).HasMaxLength(500);
        builder.HasIndex(g => g.Bezeichnung).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.HasOne(g => g.Fahrzeug).WithMany().HasForeignKey(g => g.FahrzeugId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(g => g.FahrzeugId).IsUnique().HasFilter("fahrzeug_id IS NOT NULL AND geloescht_am IS NULL");
    }
}

internal sealed class PruefungKonfiguration : IEntityTypeConfiguration<Pruefung>
{
    public void Configure(EntityTypeBuilder<Pruefung> builder)
    {
        builder.HasOne(p => p.Geraet).WithMany(g => g.Pruefungen).HasForeignKey(p => p.GeraetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Fahrzeug).WithMany().HasForeignKey(p => p.FahrzeugId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.PruefplanPosition).WithMany().HasForeignKey(p => p.PruefplanPositionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(p => p.Pruefer).WithMany().HasForeignKey(p => p.PrueferId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(p => p.Ergebnisse).HasColumnType("jsonb");
        builder.HasIndex(p => p.NaechsteFaellig);
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_pruefungen_geraet_oder_fahrzeug",
            "(geraet_id IS NULL) <> (fahrzeug_id IS NULL)"));
    }
}

internal sealed class MangelKonfiguration : IEntityTypeConfiguration<Mangel>
{
    public void Configure(EntityTypeBuilder<Mangel> builder)
    {
        builder.HasOne(m => m.Geraet).WithMany().HasForeignKey(m => m.GeraetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Fahrzeug).WithMany().HasForeignKey(m => m.FahrzeugId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.GemeldetVon).WithMany().HasForeignKey(m => m.GemeldetVonId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Pruefung>().WithMany().HasForeignKey(m => m.PruefungId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(m => m.Beschreibung).HasMaxLength(2000);
        builder.Property(m => m.Massnahme).HasMaxLength(2000);
        builder.HasIndex(m => m.Status);
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_maengel_geraet_oder_fahrzeug",
            "(geraet_id IS NULL) <> (fahrzeug_id IS NULL)"));
    }
}

internal sealed class InventurKonfiguration : IEntityTypeConfiguration<Inventur>
{
    public void Configure(EntityTypeBuilder<Inventur> builder)
    {
        builder.Property(i => i.Bezeichnung).HasMaxLength(200);
        builder.HasOne(i => i.Inventurgruppe).WithMany().HasForeignKey(i => i.InventurgruppeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InventurPositionKonfiguration : IEntityTypeConfiguration<InventurPosition>
{
    public void Configure(EntityTypeBuilder<InventurPosition> builder)
    {
        builder.HasOne(p => p.Inventur).WithMany(i => i.Positionen).HasForeignKey(p => p.InventurId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Geraet).WithMany().HasForeignKey(p => p.GeraetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.InventurId, p.GeraetId }).IsUnique();
        builder.Property(p => p.SollStandort).HasMaxLength(100);
        builder.Property(p => p.Bemerkung).HasMaxLength(500);
    }
}

internal sealed class BeschaffungKonfiguration : IEntityTypeConfiguration<Beschaffung>
{
    public void Configure(EntityTypeBuilder<Beschaffung> builder)
    {
        builder.Property(b => b.Bezeichnung).HasMaxLength(200);
        builder.Property(b => b.Begruendung).HasMaxLength(2000);
        builder.Property(b => b.Lieferant).HasMaxLength(200);
        builder.Property(b => b.Bestellnummer).HasMaxLength(50);
        builder.Property(b => b.Bemerkung).HasMaxLength(2000);
        builder.Property(b => b.Kostenschaetzung).HasPrecision(10, 2);
        builder.Property(b => b.Istkosten).HasPrecision(10, 2);
        builder.HasOne(b => b.Geraeteart).WithMany().HasForeignKey(b => b.GeraeteartId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(b => b.Fahrzeug).WithMany().HasForeignKey(b => b.FahrzeugId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(b => b.Mangel).WithMany().HasForeignKey(b => b.MangelId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(b => b.Status);
    }
}
