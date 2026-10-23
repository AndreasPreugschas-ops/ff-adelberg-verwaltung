using Fevad.Domain.Mitglieder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

internal sealed class MitgliedKonfiguration : IEntityTypeConfiguration<Mitglied>
{
    public void Configure(EntityTypeBuilder<Mitglied> builder)
    {
        builder.HasIndex(m => m.Personalnummer).IsUnique();
        builder.HasIndex(m => m.Fe2ExternalId).IsUnique();
        builder.HasIndex(m => new { m.Nachname, m.Vorname });
        builder.Property(m => m.Personalnummer).HasMaxLength(20);
        builder.Property(m => m.Vorname).HasMaxLength(100);
        builder.Property(m => m.Nachname).HasMaxLength(100);
        builder.Property(m => m.TelefonMobil).HasMaxLength(20);
        builder.Property(m => m.TelefonFestnetz).HasMaxLength(20);
        builder.Property(m => m.Email).HasMaxLength(200);
        builder.Property(m => m.Beruf).HasMaxLength(100);
        builder.Property(m => m.ArbeitgeberStrasse).HasMaxLength(200);
        builder.Property(m => m.ArbeitgeberPlz).HasMaxLength(10);
        builder.Property(m => m.ArbeitgeberTelefon).HasMaxLength(20);
        builder.Property(m => m.ArbeitgeberMobil).HasMaxLength(20);
        builder.Property(m => m.ArbeitgeberEmail).HasMaxLength(200);
        builder.Ignore(m => m.Anzeigename);
    }
}

internal sealed class AbteilungKonfiguration : IEntityTypeConfiguration<Abteilung>
{
    public void Configure(EntityTypeBuilder<Abteilung> builder)
    {
        builder.HasOne(a => a.Uebergeordnete).WithMany().HasForeignKey(a => a.UebergeordneteId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(a => a.MitProbezeit);
    }
}

internal sealed class GruppeKonfiguration : IEntityTypeConfiguration<Gruppe>
{
    public void Configure(EntityTypeBuilder<Gruppe> builder)
    {
        builder.HasOne(g => g.Abteilung).WithMany().HasForeignKey(g => g.AbteilungId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.Uebergeordnete).WithMany().HasForeignKey(g => g.UebergeordneteId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MitgliedschaftKonfiguration : IEntityTypeConfiguration<Mitgliedschaft>
{
    public void Configure(EntityTypeBuilder<Mitgliedschaft> builder)
    {
        builder.HasOne(m => m.Mitglied).WithMany(m => m.Mitgliedschaften).HasForeignKey(m => m.MitgliedId);
        builder.HasOne(m => m.Abteilung).WithMany().HasForeignKey(m => m.AbteilungId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(m => m.ProbezeitBemerkung).HasMaxLength(500);

        // Höchstens eine offene Mitgliedschaft je Mitglied und Abteilung.
        builder.HasIndex(m => new { m.MitgliedId, m.AbteilungId })
            .IsUnique()
            .HasFilter("gueltig_bis IS NULL AND geloescht_am IS NULL");
    }
}

internal sealed class DienstgradZuordnungKonfiguration : IEntityTypeConfiguration<DienstgradZuordnung>
{
    public void Configure(EntityTypeBuilder<DienstgradZuordnung> builder)
    {
        builder.HasOne(d => d.Mitglied).WithMany(m => m.Dienstgrade).HasForeignKey(d => d.MitgliedId);
        builder.HasOne(d => d.Dienstgrad).WithMany().HasForeignKey(d => d.DienstgradId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FunktionZuordnungKonfiguration : IEntityTypeConfiguration<FunktionZuordnung>
{
    public void Configure(EntityTypeBuilder<FunktionZuordnung> builder)
    {
        builder.HasOne(f => f.Mitglied).WithMany(m => m.Funktionen).HasForeignKey(f => f.MitgliedId);
        builder.HasOne(f => f.Funktion).WithMany().HasForeignKey(f => f.FunktionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ErziehungsberechtigterKonfiguration : IEntityTypeConfiguration<Erziehungsberechtigter>
{
    public void Configure(EntityTypeBuilder<Erziehungsberechtigter> builder)
    {
        builder.HasOne(e => e.Mitglied).WithMany(m => m.Erziehungsberechtigte).HasForeignKey(e => e.MitgliedId);
        builder.HasIndex(e => new { e.MitgliedId, e.Nummer }).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.Property(e => e.Vorname).HasMaxLength(100);
        builder.Property(e => e.Nachname).HasMaxLength(100);
        builder.Property(e => e.Strasse).HasMaxLength(200);
        builder.Property(e => e.Plz).HasMaxLength(10);
        builder.Property(e => e.Ort).HasMaxLength(100);
        builder.Property(e => e.Telefon).HasMaxLength(20);
        builder.Property(e => e.Mobil).HasMaxLength(20);
        builder.Property(e => e.Email).HasMaxLength(200);
    }
}

internal sealed class GruppenMitgliedschaftKonfiguration : IEntityTypeConfiguration<GruppenMitgliedschaft>
{
    public void Configure(EntityTypeBuilder<GruppenMitgliedschaft> builder)
    {
        builder.HasOne(g => g.Gruppe).WithMany(g => g.Mitglieder).HasForeignKey(g => g.GruppeId);
        builder.HasOne(g => g.Mitglied).WithMany().HasForeignKey(g => g.MitgliedId);
        builder.HasIndex(g => new { g.GruppeId, g.MitgliedId }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}
