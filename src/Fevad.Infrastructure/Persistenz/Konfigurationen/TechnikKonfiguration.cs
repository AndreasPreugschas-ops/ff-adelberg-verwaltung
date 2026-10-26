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
        builder.Property(f => f.Kostenklasse).HasMaxLength(50);
        builder.Property(f => f.Bemerkung).HasMaxLength(1000);
        builder.HasOne(f => f.Pruefvorlage).WithMany().HasForeignKey(f => f.PruefvorlageId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(f => f.Anschaffungswert).HasPrecision(12, 2);
        builder.Property(f => f.Aussonderungsgrund).HasMaxLength(500);
        builder.Property(f => f.Fin).HasMaxLength(17);
        builder.Property(f => f.Besatzung).HasMaxLength(10);
        builder.Property(f => f.Pumpe).HasMaxLength(50);
        builder.Property(f => f.Betriebsstunden).HasPrecision(10, 1);
        builder.Property(f => f.ZuwendungsbescheidAktenzeichen).HasMaxLength(100);
        Anlagegut.Konfigurieren(builder);
    }
}

internal sealed class BeladepositionKonfiguration : IEntityTypeConfiguration<Beladeposition>
{
    public void Configure(EntityTypeBuilder<Beladeposition> builder)
    {
        builder.HasOne(p => p.Fahrzeug).WithMany(f => f.Beladeplan).HasForeignKey(p => p.FahrzeugId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Material).WithMany().HasForeignKey(p => p.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Geraeteart).WithMany().HasForeignKey(p => p.GeraeteartId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(p => p.Fach).HasMaxLength(50);
        builder.Property(p => p.Bezeichnung).HasMaxLength(200);
        builder.Property(p => p.Bemerkung).HasMaxLength(500);
        builder.HasIndex(p => new { p.FahrzeugId, p.Fach });
        builder.ToTable(t => t.HasCheckConstraint("ck_beladepositionen_menge", "menge > 0"));
    }
}

internal sealed class BeladekontrolleKonfiguration : IEntityTypeConfiguration<Beladekontrolle>
{
    public void Configure(EntityTypeBuilder<Beladekontrolle> builder)
    {
        builder.HasOne(k => k.Fahrzeug).WithMany().HasForeignKey(k => k.FahrzeugId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(k => k.KontrolliertVon).WithMany().HasForeignKey(k => k.KontrolliertVonId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Mangel>().WithMany().HasForeignKey(k => k.MangelId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(k => k.Ergebnis).HasColumnType("jsonb");
        builder.Property(k => k.Bemerkung).HasMaxLength(1000);
        builder.HasIndex(k => new { k.FahrzeugId, k.Datum });
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
        Anlagegut.Konfigurieren(builder);
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
        builder.Property(p => p.Stornogrund).HasMaxLength(500);
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
        builder.HasOne<Pruefung>().WithMany().HasForeignKey(m => m.ErledigtDurchPruefungId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Fevad.Domain.Einsaetze.Einsatz>().WithMany().HasForeignKey(m => m.EinsatzId).OnDelete(DeleteBehavior.SetNull);
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
        builder.Property(i => i.Zaehler).HasMaxLength(200);
        builder.Property(i => i.Pruefer).HasMaxLength(200);
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

/// <summary>Gemeinsame Spalten der Anlagendaten von Geräten und Fahrzeugen (V60).</summary>
internal static class Anlagegut
{
    /// <summary>Längen, Genauigkeit und Index der Anlagennummer.</summary>
    public static void Konfigurieren<T>(EntityTypeBuilder<T> builder)
        where T : class, IAnlagegut
    {
        builder.Property(a => a.Anlagennummer).HasMaxLength(30);
        builder.HasIndex(a => a.Anlagennummer);
        builder.Property(a => a.Foerdermittel).HasPrecision(12, 2);
        builder.Property(a => a.Foerdergeber).HasMaxLength(100);
        builder.Property(a => a.Erloes).HasPrecision(12, 2);
        builder.Property(a => a.Aussonderungsbeschluss).HasMaxLength(200);
    }
}

internal sealed class WerkstattauftragKonfiguration : IEntityTypeConfiguration<Werkstattauftrag>
{
    public void Configure(EntityTypeBuilder<Werkstattauftrag> builder)
    {
        builder.Property(w => w.Nummer).HasMaxLength(20);
        builder.HasIndex(w => w.Nummer).IsUnique();
        builder.Property(w => w.Werkstattname).HasMaxLength(200);
        builder.Property(w => w.BelegWerkstatt).HasMaxLength(100);
        builder.Property(w => w.Bemerkung).HasMaxLength(1000);
        builder.HasOne(w => w.AbgegebenVon).WithMany().HasForeignKey(w => w.AbgegebenVonId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(w => w.ZurueckAm);
    }
}

internal sealed class WerkstattpositionKonfiguration : IEntityTypeConfiguration<Werkstattposition>
{
    public void Configure(EntityTypeBuilder<Werkstattposition> builder)
    {
        builder.HasOne(p => p.Werkstattauftrag).WithMany(w => w.Positionen).HasForeignKey(p => p.WerkstattauftragId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Geraet).WithMany().HasForeignKey(p => p.GeraetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Pruefung>().WithMany().HasForeignKey(p => p.PruefungId).OnDelete(DeleteBehavior.SetNull);
        // Kombinierte Leistungen als Bitmaske; als Text würde „Gereinigt, Geprueft, …“ zu lang.
        builder.Property(p => p.Leistungen).HasConversion<int>();
        builder.Property(p => p.Bemerkung).HasMaxLength(500);
        builder.HasIndex(p => new { p.WerkstattauftragId, p.GeraetId }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}

internal sealed class WerkstattprotokollKonfiguration : IEntityTypeConfiguration<Werkstattprotokoll>
{
    public void Configure(EntityTypeBuilder<Werkstattprotokoll> builder)
    {
        builder.HasOne<Werkstattauftrag>().WithMany(w => w.Protokolle).HasForeignKey(p => p.WerkstattauftragId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(p => p.Dateiname).HasMaxLength(200);
        builder.Property(p => p.Inhaltstyp).HasMaxLength(100);
    }
}

internal sealed class BauteilartKonfiguration : IEntityTypeConfiguration<Bauteilart>
{
    public void Configure(EntityTypeBuilder<Bauteilart> builder)
    {
        builder.HasOne(b => b.Geraeteart).WithMany().HasForeignKey(b => b.GeraeteartId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(b => b.Grundlage).HasMaxLength(200);
    }
}

internal sealed class BauteilKonfiguration : IEntityTypeConfiguration<Bauteil>
{
    public void Configure(EntityTypeBuilder<Bauteil> builder)
    {
        builder.HasOne(b => b.Geraet).WithMany().HasForeignKey(b => b.GeraetId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(b => b.Bauteilart).WithMany().HasForeignKey(b => b.BauteilartId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(b => b.Bemerkung).HasMaxLength(200);
    }
}

internal sealed class FlaschenfuellungKonfiguration : IEntityTypeConfiguration<Flaschenfuellung>
{
    public void Configure(EntityTypeBuilder<Flaschenfuellung> builder)
    {
        builder.HasOne(f => f.Geraet).WithMany().HasForeignKey(f => f.GeraetId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(f => f.Anlage).WithMany().HasForeignKey(f => f.AnlageId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(f => f.GefuelltVon).WithMany().HasForeignKey(f => f.GefuelltVonId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(f => f.Bemerkung).HasMaxLength(200);
        builder.ToTable(t => t.HasCheckConstraint("ck_flaschenfuellungen_druck", "druck_bar > 0 AND druck_bar <= 450"));
        builder.HasIndex(f => new { f.GeraetId, f.GefuelltAm });
    }
}
