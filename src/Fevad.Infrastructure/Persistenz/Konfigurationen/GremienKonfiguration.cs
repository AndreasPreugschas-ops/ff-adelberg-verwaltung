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
        builder.Property(b => b.AufgehobenDurch).HasMaxLength(200);
        builder.Ignore(b => b.Kennung);
        builder.Ignore(b => b.Stand);
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

internal sealed class GemeindevorgangKonfiguration : IEntityTypeConfiguration<Gemeindevorgang>
{
    public void Configure(EntityTypeBuilder<Gemeindevorgang> builder)
    {
        builder.Property(v => v.Betreff).HasMaxLength(300);
        builder.Property(v => v.Zustaendig).HasMaxLength(50);
        builder.Property(v => v.Sachverhalt).HasMaxLength(4000);
        builder.Property(v => v.Rechtsgrundlage).HasMaxLength(500);
        builder.Property(v => v.Beschlussvorschlag).HasMaxLength(2000);
        builder.Property(v => v.Entscheidungsvermerk).HasMaxLength(1000);
        builder.Property(v => v.EingetragenVon).HasMaxLength(200);
        builder.HasOne(v => v.Mitglied).WithMany().HasForeignKey(v => v.MitgliedId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class FuehrungspflichtKonfiguration : IEntityTypeConfiguration<Fuehrungspflicht>
{
    public void Configure(EntityTypeBuilder<Fuehrungspflicht> builder)
    {
        builder.Property(p => p.Verantwortlich).HasMaxLength(100);
        builder.Property(p => p.Vorlage).HasMaxLength(200);
        builder.Property(p => p.Rechtsgrundlage).HasMaxLength(200);
        builder.Property(p => p.Hinweis).HasMaxLength(1000);
        builder.Ignore(p => p.Funktionscodes);
    }
}

internal sealed class PflichtErledigungKonfiguration : IEntityTypeConfiguration<PflichtErledigung>
{
    public void Configure(EntityTypeBuilder<PflichtErledigung> builder)
    {
        builder.HasOne(e => e.Pflicht).WithMany(p => p.Erledigungen).HasForeignKey(e => e.PflichtId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.PflichtId, e.Stichtag }).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.Property(e => e.ErledigtVon).HasMaxLength(200);
        builder.Property(e => e.Bemerkung).HasMaxLength(1000);
    }
}

internal sealed class ChronikEintragKonfiguration : IEntityTypeConfiguration<ChronikEintrag>
{
    public void Configure(EntityTypeBuilder<ChronikEintrag> builder)
    {
        builder.ToTable("chronik_eintraege");
        builder.HasOne(c => c.Dokument).WithMany().HasForeignKey(c => c.DokumentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(c => c.Datum);
        builder.Property(c => c.Titel).HasMaxLength(200);
        builder.Property(c => c.Text).HasMaxLength(4000);
        builder.Property(c => c.ErfasstVon).HasMaxLength(200);
    }
}

internal sealed class BriefvorlageKonfiguration : IEntityTypeConfiguration<Briefvorlage>
{
    public void Configure(EntityTypeBuilder<Briefvorlage> builder)
    {
        builder.Property(v => v.Betreff).HasMaxLength(200);
        builder.Property(v => v.Text).HasMaxLength(8000);
    }
}
