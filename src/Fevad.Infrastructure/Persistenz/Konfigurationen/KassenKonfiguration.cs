using Fevad.Domain.Kassen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

/// <summary>Kassen als Sondervermögen (V01).</summary>
internal sealed class KasseKonfiguration : IEntityTypeConfiguration<Kasse>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Kasse> builder)
    {
        builder.Property(k => k.Name).HasMaxLength(100);
        builder.Property(k => k.Bemerkung).HasMaxLength(1000);
        builder.HasOne(k => k.Abteilung).WithMany().HasForeignKey(k => k.AbteilungId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(k => k.Konten).WithOne(k => k.Kasse).HasForeignKey(k => k.KasseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(k => k.Positionen).WithOne(p => p.Kasse).HasForeignKey(p => p.KasseId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Konten einer Kasse.</summary>
internal sealed class ZahlungsmittelkontoKonfiguration : IEntityTypeConfiguration<Zahlungsmittelkonto>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Zahlungsmittelkonto> builder)
    {
        builder.Property(k => k.Bezeichnung).HasMaxLength(100);
        builder.Property(k => k.Iban).HasMaxLength(34);
        builder.Property(k => k.Bank).HasMaxLength(100);
        builder.Property(k => k.Anfangsbestand).HasPrecision(12, 2);
    }
}

/// <summary>Gliederung des Wirtschaftsplans; Nummer je Kasse eindeutig.</summary>
internal sealed class PlanpositionKonfiguration : IEntityTypeConfiguration<Planposition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Planposition> builder)
    {
        builder.Property(p => p.Nummer).HasMaxLength(10);
        builder.Property(p => p.Bezeichnung).HasMaxLength(150);
        builder.Property(p => p.Hinweis).HasMaxLength(500);
        builder.Ignore(p => p.Hauptgruppe);
        builder.HasIndex(p => new { p.KasseId, p.Nummer }).IsUnique();
    }
}

/// <summary>Wirtschaftsplan je Kasse und Jahr.</summary>
internal sealed class WirtschaftsplanKonfiguration : IEntityTypeConfiguration<Wirtschaftsplan>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Wirtschaftsplan> builder)
    {
        builder.HasOne(w => w.Kasse).WithMany().HasForeignKey(w => w.KasseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(w => w.Beschluss).WithMany().HasForeignKey(w => w.BeschlussId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(w => w.Ansaetze).WithOne(a => a.Wirtschaftsplan).HasForeignKey(a => a.WirtschaftsplanId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(w => w.Bemerkung).HasMaxLength(1000);
        builder.Ignore(w => w.Aenderbar);
        builder.HasIndex(w => new { w.KasseId, w.Jahr }).IsUnique();
    }
}

/// <summary>Ansätze eines Wirtschaftsplans.</summary>
internal sealed class PlanansatzKonfiguration : IEntityTypeConfiguration<Planansatz>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Planansatz> builder)
    {
        builder.HasOne(a => a.Planposition).WithMany().HasForeignKey(a => a.PlanpositionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(a => a.Betrag).HasPrecision(12, 2);
        builder.Property(a => a.Erlaeuterung).HasMaxLength(500);
        builder.HasIndex(a => new { a.WirtschaftsplanId, a.PlanpositionId }).IsUnique();
    }
}

/// <summary>Kassenbuch; Belegnummer je Kasse und Jahr eindeutig.</summary>
internal sealed class BuchungKonfiguration : IEntityTypeConfiguration<Buchung>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Buchung> builder)
    {
        builder.HasOne(b => b.Kasse).WithMany().HasForeignKey(b => b.KasseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Konto).WithMany().HasForeignKey(b => b.KontoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Gegenkonto).WithMany().HasForeignKey(b => b.GegenkontoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Planposition).WithMany().HasForeignKey(b => b.PlanpositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Ausschussbeschluss).WithMany().HasForeignKey(b => b.AusschussbeschlussId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(b => b.StornoVon).WithMany().HasForeignKey(b => b.StornoVonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Zahlungsanordnung>().WithMany().HasForeignKey(b => b.ZahlungsanordnungId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(b => b.Belege).WithOne().HasForeignKey(b => b.BuchungId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Veranstaltung).WithMany().HasForeignKey(b => b.VeranstaltungId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(b => b.Betrag).HasPrecision(12, 2);
        builder.Property(b => b.Partner).HasMaxLength(200);
        builder.Property(b => b.Text).HasMaxLength(500);
        builder.Property(b => b.EigenbelegBegruendung).HasMaxLength(500);
        builder.Property(b => b.Beschlussverweis).HasMaxLength(200);
        builder.Ignore(b => b.Festgeschrieben);
        builder.Ignore(b => b.Kennung);
        builder.HasIndex(b => new { b.KasseId, b.Jahr, b.Belegnummer }).IsUnique();
        builder.HasIndex(b => new { b.KasseId, b.Datum });
        builder.ToTable(t => t.HasCheckConstraint("ck_buchungen_betrag", "betrag > 0"));
    }
}

/// <summary>Belegdateien; gehören zu einer Buchung oder einer vorbereiteten Ausgabe.</summary>
internal sealed class KassenbelegKonfiguration : IEntityTypeConfiguration<Kassenbeleg>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Kassenbeleg> builder)
    {
        builder.Property(b => b.Dateiname).HasMaxLength(200);
        builder.Property(b => b.Inhaltstyp).HasMaxLength(100);
        builder.Property(b => b.Pruefsumme).HasMaxLength(64);
        builder.ToTable(t => t.HasCheckConstraint("ck_kassenbelege_bezug", "buchung_id IS NOT NULL OR zahlungsanordnung_id IS NOT NULL"));
    }
}

/// <summary>Vorbereitete Ausgaben.</summary>
internal sealed class ZahlungsanordnungKonfiguration : IEntityTypeConfiguration<Zahlungsanordnung>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Zahlungsanordnung> builder)
    {
        builder.HasOne(a => a.Kasse).WithMany().HasForeignKey(a => a.KasseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Planposition).WithMany().HasForeignKey(a => a.PlanpositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Ausschussbeschluss).WithMany().HasForeignKey(a => a.AusschussbeschlussId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(a => a.Belege).WithOne().HasForeignKey(b => b.ZahlungsanordnungId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(a => a.Betrag).HasPrecision(12, 2);
        builder.Property(a => a.Empfaenger).HasMaxLength(200);
        builder.Property(a => a.Zweck).HasMaxLength(500);
        builder.Property(a => a.Bemerkung).HasMaxLength(1000);
        builder.HasIndex(a => new { a.KasseId, a.Status });
    }
}

/// <summary>Veranstaltungen als Kostenträger (V03).</summary>
internal sealed class VeranstaltungKonfiguration : IEntityTypeConfiguration<Veranstaltung>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Veranstaltung> builder)
    {
        builder.HasOne(v => v.Kasse).WithMany().HasForeignKey(v => v.KasseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(v => v.Barkassenberichte).WithOne(b => b.Veranstaltung).HasForeignKey(b => b.VeranstaltungId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(v => v.Name).HasMaxLength(150);
        builder.Property(v => v.Mitveranstalter).HasMaxLength(200);
        builder.Property(v => v.AnteilProzent).HasPrecision(5, 2);
        builder.Property(v => v.Bemerkung).HasMaxLength(1000);
        builder.Ignore(v => v.Jahr);
        builder.HasIndex(v => new { v.KasseId, v.Beginn });
        builder.ToTable(t => t.HasCheckConstraint("ck_veranstaltungen_anteil", "anteil_prozent > 0 AND anteil_prozent <= 100"));
    }
}

/// <summary>Barkassenberichte mit Zählprotokoll (V03).</summary>
internal sealed class BarkassenberichtKonfiguration : IEntityTypeConfiguration<Barkassenbericht>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Barkassenbericht> builder)
    {
        builder.HasMany(b => b.Zaehlung).WithOne().HasForeignKey(z => z.BarkassenberichtId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(b => b.Buchung).WithMany().HasForeignKey(b => b.BuchungId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(b => b.Stelle).HasMaxLength(100);
        builder.Property(b => b.Wechselgeld).HasPrecision(12, 2);
        builder.Property(b => b.GezaehltVon).HasMaxLength(150);
        builder.Property(b => b.ZweitePerson).HasMaxLength(150);
        builder.Property(b => b.Bemerkung).HasMaxLength(1000);
        builder.Ignore(b => b.Endbestand);
        builder.Ignore(b => b.Einnahme);
        builder.HasIndex(b => b.BuchungId).IsUnique();
    }
}

/// <summary>Zeilen der Zählprotokolle.</summary>
internal sealed class ZaehlpositionKonfiguration : IEntityTypeConfiguration<Zaehlposition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Zaehlposition> builder)
    {
        builder.Property(z => z.Wert).HasPrecision(8, 2);
        builder.HasIndex(z => new { z.BarkassenberichtId, z.Wert }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("ck_zaehlpositionen_anzahl", "anzahl >= 0"));
    }
}
