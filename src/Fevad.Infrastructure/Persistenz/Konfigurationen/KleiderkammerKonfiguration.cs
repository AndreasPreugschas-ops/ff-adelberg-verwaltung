using Fevad.Domain.Kleiderkammer;
using Fevad.Domain.Technik;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

internal sealed class ArtikelKonfiguration : IEntityTypeConfiguration<Artikel>
{
    public void Configure(EntityTypeBuilder<Artikel> builder)
    {
        builder.HasOne(a => a.Geraeteart).WithMany().HasForeignKey(a => a.GeraeteartId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(a => a.Norm).HasMaxLength(100);

        // Je Geräteart höchstens ein Artikel, sonst ist ein Einzelstück keinem Artikel eindeutig zugeordnet.
        builder.HasIndex(a => a.GeraeteartId).IsUnique().HasFilter("geraeteart_id IS NOT NULL AND geloescht_am IS NULL");
    }
}

/// <summary>Nachbestellungen der Kleiderkammer laufen als Beschaffung mit Artikel und Größe.</summary>
internal sealed class BeschaffungArtikelKonfiguration : IEntityTypeConfiguration<Beschaffung>
{
    public void Configure(EntityTypeBuilder<Beschaffung> builder)
    {
        builder.HasOne(b => b.Artikel).WithMany().HasForeignKey(b => b.ArtikelId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(b => b.Groesse).HasMaxLength(20);
    }
}

internal sealed class LagerbestandKonfiguration : IEntityTypeConfiguration<Lagerbestand>
{
    public void Configure(EntityTypeBuilder<Lagerbestand> builder)
    {
        builder.HasOne(l => l.Artikel).WithMany(a => a.Bestaende).HasForeignKey(l => l.ArtikelId);
        builder.Property(l => l.Groesse).HasMaxLength(20);
        builder.Property(l => l.Lagerort).HasMaxLength(50);
        builder.HasIndex(l => new { l.ArtikelId, l.Groesse }).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.ToTable(t => t.HasCheckConstraint("ck_lagerbestaende_menge", "menge >= 0 AND mindestmenge >= 0"));
    }
}

internal sealed class KleiderkammervorgangKonfiguration : IEntityTypeConfiguration<Kleiderkammervorgang>
{
    public void Configure(EntityTypeBuilder<Kleiderkammervorgang> builder)
    {
        builder.HasOne(v => v.Mitglied).WithMany().HasForeignKey(v => v.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(v => v.Nummer).HasMaxLength(20);
        builder.HasIndex(v => v.Nummer).IsUnique();
    }
}

internal sealed class AusstattungKonfiguration : IEntityTypeConfiguration<Ausstattung>
{
    public void Configure(EntityTypeBuilder<Ausstattung> builder)
    {
        builder.HasOne(a => a.Mitglied).WithMany().HasForeignKey(a => a.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Artikel).WithMany().HasForeignKey(a => a.ArtikelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Geraet).WithMany().HasForeignKey(a => a.GeraetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.AusgabeVorgang).WithMany().HasForeignKey(a => a.AusgabeVorgangId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.RuecknahmeVorgang).WithMany().HasForeignKey(a => a.RuecknahmeVorgangId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(a => a.Groesse).HasMaxLength(20);
        builder.HasIndex(a => new { a.MitgliedId, a.ZurueckAm });

        // Ein Einzelstück kann nur bei einem Mitglied zugleich sein.
        builder.HasIndex(a => a.GeraetId).IsUnique().HasFilter("zurueck_am IS NULL AND geloescht_am IS NULL");
        builder.ToTable(t => t.HasCheckConstraint("ck_ausstattungen_menge", "menge > 0"));
    }
}

internal sealed class GroessenangabeKonfiguration : IEntityTypeConfiguration<Groessenangabe>
{
    public void Configure(EntityTypeBuilder<Groessenangabe> builder)
    {
        builder.HasOne(g => g.Mitglied).WithMany().HasForeignKey(g => g.MitgliedId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(g => g.Wert).HasMaxLength(20);
        builder.HasIndex(g => new { g.MitgliedId, g.Groessenart }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}
