using Fevad.Domain.Dokumente;
using Fevad.Infrastructure.Identitaet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

internal sealed class DokumentKonfiguration : IEntityTypeConfiguration<Dokument>
{
    public void Configure(EntityTypeBuilder<Dokument> builder)
    {
        builder.Property(d => d.Titel).HasMaxLength(200);
        builder.Property(d => d.Beschreibung).HasMaxLength(2000);
        builder.HasOne(d => d.Kategorie).WithMany().HasForeignKey(d => d.KategorieId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => d.Titel);
        builder.HasOne(d => d.Geraeteart).WithMany().HasForeignKey(d => d.GeraeteartId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(d => d.Geraet).WithMany().HasForeignKey(d => d.GeraetId).OnDelete(DeleteBehavior.SetNull);
        builder.Ignore(d => d.AktuelleVersion);
        builder.Ignore(d => d.NaechsteVersionsnummer);
    }
}

internal sealed class DokumentVersionKonfiguration : IEntityTypeConfiguration<DokumentVersion>
{
    public void Configure(EntityTypeBuilder<DokumentVersion> builder)
    {
        builder.HasOne(v => v.Dokument).WithMany(d => d.Versionen).HasForeignKey(v => v.DokumentId);
        builder.HasIndex(v => new { v.DokumentId, v.Nummer }).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.Property(v => v.Dateiname).HasMaxLength(200);
        builder.Property(v => v.Inhaltstyp).HasMaxLength(100);
        builder.Property(v => v.Pruefsumme).HasMaxLength(64);
        builder.Property(v => v.Aenderungshinweis).HasMaxLength(500);
        builder.HasOne<DokumentDatei>().WithMany().HasForeignKey(v => v.DateiId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DokumentDateiKonfiguration : IEntityTypeConfiguration<DokumentDatei>
{
    public void Configure(EntityTypeBuilder<DokumentDatei> builder)
    {
        // bytea in eigener Tabelle: pg_dump und die Replikation zum Gerätehaus-PC erfassen die Dateien mit.
        builder.ToTable("dokument_dateien");
        builder.Property(d => d.Inhalt).HasColumnType("bytea");
    }
}

internal sealed class LesebestaetigungKonfiguration : IEntityTypeConfiguration<Lesebestaetigung>
{
    public void Configure(EntityTypeBuilder<Lesebestaetigung> builder)
    {
        builder.HasOne(l => l.DokumentVersion).WithMany(v => v.Lesebestaetigungen).HasForeignKey(l => l.DokumentVersionId);
        builder.HasOne<Benutzer>().WithMany().HasForeignKey(l => l.BenutzerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.DokumentVersionId, l.BenutzerId }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}
