using Fevad.Domain.Gremien;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

internal sealed class SitzungKonfiguration : IEntityTypeConfiguration<Sitzung>
{
    public void Configure(EntityTypeBuilder<Sitzung> builder)
    {
        builder.HasIndex(s => s.Beginn);
        builder.Property(s => s.Titel).HasMaxLength(200);
        builder.Property(s => s.Ort).HasMaxLength(200);
        builder.HasOne(s => s.Protokollfuehrer).WithMany().HasForeignKey(s => s.ProtokollfuehrerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(s => s.ProtokollDokument).WithMany().HasForeignKey(s => s.ProtokollDokumentId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(t => t.HasCheckConstraint("ck_sitzungen_ende_nach_beginn", "ende IS NULL OR ende >= beginn"));
    }
}

internal sealed class SitzungTeilnahmeKonfiguration : IEntityTypeConfiguration<SitzungTeilnahme>
{
    public void Configure(EntityTypeBuilder<SitzungTeilnahme> builder)
    {
        builder.HasOne(t => t.Sitzung).WithMany(s => s.Teilnahmen).HasForeignKey(t => t.SitzungId);
        builder.HasOne(t => t.Mitglied).WithMany().HasForeignKey(t => t.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.SitzungId, t.MitgliedId }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}

internal sealed class BeschlussKonfiguration : IEntityTypeConfiguration<Beschluss>
{
    public void Configure(EntityTypeBuilder<Beschluss> builder)
    {
        builder.HasOne(b => b.Sitzung).WithMany(s => s.Beschluesse).HasForeignKey(b => b.SitzungId);
        builder.HasIndex(b => new { b.Jahr, b.Nummer }).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.Property(b => b.Tagesordnungspunkt).HasMaxLength(20);
        builder.Property(b => b.Gegenstand).HasMaxLength(4000);
        builder.Property(b => b.Bemerkung).HasMaxLength(1000);
        builder.Ignore(b => b.Kennung);
    }
}

internal sealed class WahlKonfiguration : IEntityTypeConfiguration<Wahl>
{
    public void Configure(EntityTypeBuilder<Wahl> builder)
    {
        builder.HasOne(w => w.Sitzung).WithMany(s => s.Wahlen).HasForeignKey(w => w.SitzungId);
        builder.HasOne(w => w.Funktion).WithMany().HasForeignKey(w => w.FunktionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(w => w.Bemerkung).HasMaxLength(1000);
        builder.Ignore(w => w.Amtsende);
        builder.Ignore(w => w.MehrheitsBasis);
        builder.Ignore(w => w.IstStichwahl);
    }
}

internal sealed class WahlBewerberKonfiguration : IEntityTypeConfiguration<WahlBewerber>
{
    public void Configure(EntityTypeBuilder<WahlBewerber> builder)
    {
        builder.HasOne(b => b.Wahl).WithMany(w => w.Bewerber).HasForeignKey(b => b.WahlId);
        builder.HasOne(b => b.Mitglied).WithMany().HasForeignKey(b => b.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => new { b.WahlId, b.MitgliedId }).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.Ignore(b => b.Ersatzmitglied);
    }
}
