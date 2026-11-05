using Fevad.Domain.Technik;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

/// <summary>Datenbankabbildung von <see cref="Fahrzeug"/>.</summary>
internal sealed class FahrzeugKonfiguration : IEntityTypeConfiguration<Fahrzeug>
{
    /// <summary>Bildet <see cref="Fahrzeug"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
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

/// <summary>Datenbankabbildung von <see cref="Beladeposition"/>.</summary>
internal sealed class BeladepositionKonfiguration : IEntityTypeConfiguration<Beladeposition>
{
    /// <summary>Bildet <see cref="Beladeposition"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
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

/// <summary>Datenbankabbildung von <see cref="Beladekontrolle"/>.</summary>
internal sealed class BeladekontrolleKonfiguration : IEntityTypeConfiguration<Beladekontrolle>
{
    /// <summary>Bildet <see cref="Beladekontrolle"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
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

/// <summary>Datenbankabbildung von <see cref="PruefplanPosition"/>.</summary>
internal sealed class PruefplanPositionKonfiguration : IEntityTypeConfiguration<PruefplanPosition>
{
    /// <summary>Bildet <see cref="PruefplanPosition"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<PruefplanPosition> builder)
    {
        builder.HasOne(p => p.Geraeteart).WithMany(g => g.Pruefplan).HasForeignKey(p => p.GeraeteartId);
        builder.Property(p => p.Checkliste).HasColumnType("jsonb");
        builder.Property(p => p.Pruefer).HasMaxLength(200);
    }
}

/// <summary>Datenbankabbildung von <see cref="Geraet"/>.</summary>
internal sealed class GeraetKonfiguration : IEntityTypeConfiguration<Geraet>
{
    /// <summary>Bildet <see cref="Geraet"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
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

/// <summary>Datenbankabbildung von <see cref="Material"/>.</summary>
internal sealed class MaterialKonfiguration : IEntityTypeConfiguration<Material>
{
    /// <summary>Bildet <see cref="Material"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
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

/// <summary>Datenbankabbildung von <see cref="Inventurgruppe"/>.</summary>
internal sealed class InventurgruppeKonfiguration : IEntityTypeConfiguration<Inventurgruppe>
{
    /// <summary>Bildet <see cref="Inventurgruppe"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Inventurgruppe> builder)
    {
        builder.Property(g => g.Bezeichnung).HasMaxLength(100);
        builder.Property(g => g.Beschreibung).HasMaxLength(500);
        builder.HasIndex(g => g.Bezeichnung).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.HasOne(g => g.Fahrzeug).WithMany().HasForeignKey(g => g.FahrzeugId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(g => g.FahrzeugId).IsUnique().HasFilter("fahrzeug_id IS NOT NULL AND geloescht_am IS NULL");
    }
}

/// <summary>Datenbankabbildung von <see cref="Pruefung"/>.</summary>
internal sealed class PruefungKonfiguration : IEntityTypeConfiguration<Pruefung>
{
    /// <summary>Bildet <see cref="Pruefung"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
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

/// <summary>Datenbankabbildung von <see cref="Mangel"/>.</summary>
internal sealed class MangelKonfiguration : IEntityTypeConfiguration<Mangel>
{
    /// <summary>Bildet <see cref="Mangel"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
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
        builder.Property(m => m.Werkstattname).HasMaxLength(200);
        builder.Property(m => m.Kosten).HasPrecision(10, 2);
        builder.HasOne(m => m.ErsatzGeraet).WithMany().HasForeignKey(m => m.ErsatzGeraetId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Fahrzeug>().WithMany().HasForeignKey(m => m.ErsatzVorherFahrzeugId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Fahrzeug>().WithMany().HasForeignKey(m => m.UrsprungFahrzeugId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(m => m.ErsatzVorherLagerort).HasMaxLength(50);
        builder.Property(m => m.UrsprungLagerort).HasMaxLength(50);
        builder.HasMany(m => m.Fotos).WithOne().HasForeignKey(f => f.MangelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(m => m.Status);
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_maengel_geraet_oder_fahrzeug",
            "(geraet_id IS NULL) <> (fahrzeug_id IS NULL)"));
    }
}

/// <summary>Datenbankabbildung von <see cref="Inventur"/>.</summary>
internal sealed class InventurKonfiguration : IEntityTypeConfiguration<Inventur>
{
    /// <summary>Bildet <see cref="Inventur"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Inventur> builder)
    {
        builder.Property(i => i.Bezeichnung).HasMaxLength(200);
        builder.Property(i => i.Zaehler).HasMaxLength(200);
        builder.Property(i => i.Pruefer).HasMaxLength(200);
        builder.HasOne(i => i.Inventurgruppe).WithMany().HasForeignKey(i => i.InventurgruppeId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Datenbankabbildung von <see cref="InventurPosition"/>.</summary>
internal sealed class InventurPositionKonfiguration : IEntityTypeConfiguration<InventurPosition>
{
    /// <summary>Bildet <see cref="InventurPosition"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<InventurPosition> builder)
    {
        builder.HasOne(p => p.Inventur).WithMany(i => i.Positionen).HasForeignKey(p => p.InventurId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Geraet).WithMany().HasForeignKey(p => p.GeraetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.InventurId, p.GeraetId }).IsUnique();
        builder.Property(p => p.SollStandort).HasMaxLength(100);
        builder.Property(p => p.Bemerkung).HasMaxLength(500);
    }
}

/// <summary>Datenbankabbildung von <see cref="Beschaffung"/>.</summary>
internal sealed class BeschaffungKonfiguration : IEntityTypeConfiguration<Beschaffung>
{
    /// <summary>Bildet <see cref="Beschaffung"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
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

/// <summary>Datenbankabbildung von <see cref="Werkstattauftrag"/>.</summary>
internal sealed class WerkstattauftragKonfiguration : IEntityTypeConfiguration<Werkstattauftrag>
{
    /// <summary>Bildet <see cref="Werkstattauftrag"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
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

/// <summary>Datenbankabbildung von <see cref="Werkstattposition"/>.</summary>
internal sealed class WerkstattpositionKonfiguration : IEntityTypeConfiguration<Werkstattposition>
{
    /// <summary>Bildet <see cref="Werkstattposition"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
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

/// <summary>Datenbankabbildung von <see cref="Fahrt"/>.</summary>
internal sealed class FahrtKonfiguration : IEntityTypeConfiguration<Fahrt>
{
    /// <summary>Bildet <see cref="Fahrt"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Fahrt> builder)
    {
        builder.HasOne(f => f.Fahrzeug).WithMany().HasForeignKey(f => f.FahrzeugId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(f => f.Fahrer).WithMany().HasForeignKey(f => f.FahrerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Fevad.Domain.Einsaetze.Einsatz>().WithMany().HasForeignKey(f => f.EinsatzId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Fevad.Domain.Dienstbetrieb.Dienst>().WithMany().HasForeignKey(f => f.DienstId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(f => f.Ziel).HasMaxLength(200);
        builder.Property(f => f.Bemerkung).HasMaxLength(1000);
        builder.HasIndex(f => new { f.FahrzeugId, f.Beginn });
        builder.ToTable(t => t.HasCheckConstraint("ck_fahrten_km", "km_ende IS NULL OR km_beginn IS NULL OR km_ende >= km_beginn"));
    }
}

/// <summary>Datenbankabbildung von <see cref="Betankung"/>.</summary>
internal sealed class BetankungKonfiguration : IEntityTypeConfiguration<Betankung>
{
    /// <summary>Bildet <see cref="Betankung"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Betankung> builder)
    {
        builder.HasOne(b => b.Fahrzeug).WithMany().HasForeignKey(b => b.FahrzeugId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(b => b.GetanktVon).WithMany().HasForeignKey(b => b.GetanktVonId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(b => b.Liter).HasPrecision(8, 2);
        builder.Property(b => b.Betrag).HasPrecision(10, 2);
        builder.Property(b => b.Beleg).HasMaxLength(100);
        builder.HasIndex(b => new { b.FahrzeugId, b.Datum });
        builder.ToTable(t => t.HasCheckConstraint("ck_betankungen_liter", "liter > 0"));
    }
}

/// <summary>Datenbankabbildung von <see cref="Mangelfoto"/>.</summary>
internal sealed class MangelfotoKonfiguration : IEntityTypeConfiguration<Mangelfoto>
{
    /// <summary>Bildet <see cref="Mangelfoto"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Mangelfoto> builder)
    {
        builder.Property(f => f.Dateiname).HasMaxLength(200);
        builder.Property(f => f.Inhaltstyp).HasMaxLength(100);
    }
}

/// <summary>Datenbankabbildung von <see cref="Werkstattprotokoll"/>.</summary>
internal sealed class WerkstattprotokollKonfiguration : IEntityTypeConfiguration<Werkstattprotokoll>
{
    /// <summary>Bildet <see cref="Werkstattprotokoll"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Werkstattprotokoll> builder)
    {
        builder.HasOne<Werkstattauftrag>().WithMany(w => w.Protokolle).HasForeignKey(p => p.WerkstattauftragId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(p => p.Dateiname).HasMaxLength(200);
        builder.Property(p => p.Inhaltstyp).HasMaxLength(100);
    }
}

/// <summary>Datenbankabbildung von <see cref="Bauteilart"/>.</summary>
internal sealed class BauteilartKonfiguration : IEntityTypeConfiguration<Bauteilart>
{
    /// <summary>Bildet <see cref="Bauteilart"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Bauteilart> builder)
    {
        builder.HasOne(b => b.Geraeteart).WithMany().HasForeignKey(b => b.GeraeteartId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(b => b.Grundlage).HasMaxLength(200);
    }
}

/// <summary>Datenbankabbildung von <see cref="Bauteil"/>.</summary>
internal sealed class BauteilKonfiguration : IEntityTypeConfiguration<Bauteil>
{
    /// <summary>Bildet <see cref="Bauteil"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Bauteil> builder)
    {
        builder.HasOne(b => b.Geraet).WithMany().HasForeignKey(b => b.GeraetId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(b => b.Bauteilart).WithMany().HasForeignKey(b => b.BauteilartId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(b => b.Bemerkung).HasMaxLength(200);
    }
}

/// <summary>Datenbankabbildung von <see cref="Flaschenfuellung"/>.</summary>
internal sealed class FlaschenfuellungKonfiguration : IEntityTypeConfiguration<Flaschenfuellung>
{
    /// <summary>Bildet <see cref="Flaschenfuellung"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
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

/// <summary>Datenbankabbildung von <see cref="Pflegeeintrag"/>.</summary>
internal sealed class PflegeeintragKonfiguration : IEntityTypeConfiguration<Pflegeeintrag>
{
    /// <summary>Bildet <see cref="Pflegeeintrag"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Pflegeeintrag> builder)
    {
        builder.HasOne(p => p.Geraet).WithMany().HasForeignKey(p => p.GeraetId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Einsatz).WithMany().HasForeignKey(p => p.EinsatzId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(p => p.Durchfuehrung).HasMaxLength(100);
        builder.Property(p => p.Bemerkung).HasMaxLength(200);
        builder.HasIndex(p => new { p.GeraetId, p.Datum });
    }
}

/// <summary>Datenbankabbildung von <see cref="Ausleihe"/>.</summary>
internal sealed class AusleiheKonfiguration : IEntityTypeConfiguration<Ausleihe>
{
    /// <summary>Bildet <see cref="Ausleihe"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Ausleihe> builder)
    {
        builder.HasOne(a => a.Geraet).WithMany().HasForeignKey(a => a.GeraetId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(a => a.Bezeichnung).HasMaxLength(200);
        builder.Property(a => a.Partner).HasMaxLength(200);
        builder.Property(a => a.Rueckgabevermerk).HasMaxLength(200);
        builder.Property(a => a.Bemerkung).HasMaxLength(200);
        builder.Ignore(a => a.Gegenstand);
        builder.ToTable(t => t.HasCheckConstraint("ck_ausleihen_gegenstand", "geraet_id IS NOT NULL OR bezeichnung IS NOT NULL"));
        builder.HasIndex(a => a.ZurueckAm);
    }
}

/// <summary>Datenbankabbildung von <see cref="Lagerartikel"/>.</summary>
internal sealed class LagerartikelKonfiguration : IEntityTypeConfiguration<Lagerartikel>
{
    /// <summary>Bildet <see cref="Lagerartikel"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Lagerartikel> builder)
    {
        builder.Property(a => a.Bezeichnung).HasMaxLength(200);
        builder.Property(a => a.Einheit).HasMaxLength(20);
        builder.Property(a => a.Lagerort).HasMaxLength(100);
        builder.Property(a => a.Mindestmenge).HasPrecision(10, 2);
        builder.Property(a => a.Stueckpreis).HasPrecision(10, 2);
        builder.HasMany(a => a.Chargen).WithOne(c => c.Lagerartikel).HasForeignKey(c => c.LagerartikelId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(a => a.Bestand);
        builder.Ignore(a => a.Nachbestellen);
        builder.Ignore(a => a.NaechsterVerfall);
        builder.HasIndex(a => a.Bezeichnung);
    }
}

/// <summary>Datenbankabbildung von <see cref="Lagercharge"/>.</summary>
internal sealed class LagerchargeKonfiguration : IEntityTypeConfiguration<Lagercharge>
{
    /// <summary>Bildet <see cref="Lagercharge"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Lagercharge> builder)
    {
        builder.Property(c => c.Menge).HasPrecision(10, 2);
        builder.ToTable(t => t.HasCheckConstraint("ck_lagerchargen_menge", "menge >= 0"));
    }
}

/// <summary>Datenbankabbildung von <see cref="Lagerbewegung"/>.</summary>
internal sealed class LagerbewegungKonfiguration : IEntityTypeConfiguration<Lagerbewegung>
{
    /// <summary>Bildet <see cref="Lagerbewegung"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Lagerbewegung> builder)
    {
        builder.HasOne(b => b.Lagerartikel).WithMany().HasForeignKey(b => b.LagerartikelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Lagercharge>().WithMany().HasForeignKey(b => b.LagerchargeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(b => b.Einsatz).WithMany().HasForeignKey(b => b.EinsatzId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Fevad.Domain.Einsaetze.Zusatzkosten>().WithMany().HasForeignKey(b => b.ZusatzkostenId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(b => b.Menge).HasPrecision(10, 2);
        builder.Property(b => b.Stueckpreis).HasPrecision(10, 2);
        builder.Property(b => b.Bemerkung).HasMaxLength(200);
        builder.Ignore(b => b.Wert);
        builder.HasIndex(b => new { b.LagerartikelId, b.Datum });
    }
}

/// <summary>EF-Konfiguration für <see cref="Foerdergegenstand"/>.</summary>
internal sealed class FoerdergegenstandKonfiguration : IEntityTypeConfiguration<Foerdergegenstand>
{
    /// <summary>Bildet <see cref="Foerdergegenstand"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Foerdergegenstand> builder)
    {
        builder.Property(f => f.Festbetrag).HasPrecision(12, 2);
        builder.Property(f => f.FestbetragRahmenvertrag).HasPrecision(12, 2);
        builder.Property(f => f.Fundstelle).HasMaxLength(300);
    }
}

/// <summary>EF-Konfiguration für <see cref="Foerderantrag"/>.</summary>
internal sealed class FoerderantragKonfiguration : IEntityTypeConfiguration<Foerderantrag>
{
    /// <summary>Bildet <see cref="Foerderantrag"/> auf die Datenbank ab: Tabelle, Schlüssel, Feldlängen, Indizes und Beziehungen.</summary>
    /// <param name="builder">Builder von EF Core für diese Entität.</param>
    public void Configure(EntityTypeBuilder<Foerderantrag> builder)
    {
        builder.HasIndex(a => a.BeschaffungId).IsUnique();
        builder.HasOne(a => a.Beschaffung).WithMany().HasForeignKey(a => a.BeschaffungId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Foerdergegenstand).WithMany().HasForeignKey(a => a.FoerdergegenstandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Altfahrzeug).WithMany().HasForeignKey(a => a.AltfahrzeugId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(a => a.Fahrzeug).WithMany().HasForeignKey(a => a.FahrzeugId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(a => a.Zuwendung).HasPrecision(12, 2);
        builder.Property(a => a.Gemeinderatsbeschluss).HasMaxLength(100);
        builder.Property(a => a.BescheidAktenzeichen).HasMaxLength(100);
        builder.Property(a => a.Bemerkung).HasMaxLength(2000);
        builder.Ignore(a => a.ErwarteterBetrag);
        builder.Ignore(a => a.BeginnGesperrt);
        builder.Ignore(a => a.Offen);
    }
}
