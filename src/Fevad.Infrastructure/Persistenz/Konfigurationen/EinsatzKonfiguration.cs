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
        builder.Property(e => e.EingesetzteMittel).HasMaxLength(1000);
        builder.Property(e => e.Schadenart).HasMaxLength(100);
        builder.Property(e => e.Pressetext).HasMaxLength(Einsatz.PressetextLaenge);
        builder.Property(e => e.PresseFreigegebenVon).HasMaxLength(200);
        builder.Property(e => e.GefahrstoffBezeichnung).HasMaxLength(200);
        builder.Property(e => e.UnNummer).HasMaxLength(4);
        builder.Property(e => e.GefahrstoffMenge).HasMaxLength(100);
        builder.Property(e => e.Behoerdenmeldung).HasMaxLength(300);
        builder.Property(e => e.Rueckgabe).HasMaxLength(1000);
        builder.HasOne(e => e.StatistikKategorie).WithMany().HasForeignKey(e => e.StatistikKategorieId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Einsatzleiter).WithMany().HasForeignKey(e => e.EinsatzleiterId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.BmaObjekt).WithMany().HasForeignKey(e => e.BmaObjektId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EinsatzleitungKonfiguration : IEntityTypeConfiguration<Einsatzleitung>
{
    public void Configure(EntityTypeBuilder<Einsatzleitung> builder)
    {
        builder.HasOne(l => l.Einsatz).WithMany(e => e.Einsatzleitungen).HasForeignKey(l => l.EinsatzId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.Mitglied).WithMany().HasForeignKey(l => l.MitgliedId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(l => l.MitgliedId);
        builder.Property(l => l.Extern).HasMaxLength(200);
        builder.Property(l => l.Bemerkung).HasMaxLength(500);
    }
}

internal sealed class EinsatzabschnittKonfiguration : IEntityTypeConfiguration<Einsatzabschnitt>
{
    public void Configure(EntityTypeBuilder<Einsatzabschnitt> builder)
    {
        builder.HasOne(a => a.Einsatz).WithMany(e => e.Abschnitte).HasForeignKey(a => a.EinsatzId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Leiter).WithMany().HasForeignKey(a => a.LeiterId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(a => a.Bezeichnung).HasMaxLength(100);
        builder.Property(a => a.LeiterExtern).HasMaxLength(200);
        builder.Property(a => a.Auftrag).HasMaxLength(500);
    }
}

internal sealed class TagebucheintragKonfiguration : IEntityTypeConfiguration<Tagebucheintrag>
{
    public void Configure(EntityTypeBuilder<Tagebucheintrag> builder)
    {
        builder.HasOne(t => t.Einsatz).WithMany(e => e.Tagebuch).HasForeignKey(t => t.EinsatzId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => new { t.EinsatzId, t.Zeitpunkt });
        builder.Property(t => t.Text).HasMaxLength(2000);
        builder.Property(t => t.Verfasser).HasMaxLength(200);
    }
}

internal sealed class NachbesprechungKonfiguration : IEntityTypeConfiguration<Nachbesprechung>
{
    public void Configure(EntityTypeBuilder<Nachbesprechung> builder)
    {
        builder.HasOne(n => n.Einsatz).WithOne(e => e.Nachbesprechung).HasForeignKey<Nachbesprechung>(n => n.EinsatzId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(n => n.EinsatzId).IsUnique();
        builder.Property(n => n.Teilnehmer).HasMaxLength(500);
        builder.Property(n => n.Gut).HasMaxLength(4000);
        builder.Property(n => n.Verbesserung).HasMaxLength(4000);
    }
}

internal sealed class MassnahmeKonfiguration : IEntityTypeConfiguration<Massnahme>
{
    public void Configure(EntityTypeBuilder<Massnahme> builder)
    {
        builder.HasOne(m => m.Nachbesprechung).WithMany(n => n.Massnahmen).HasForeignKey(m => m.NachbesprechungId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.Verantwortlich).WithMany().HasForeignKey(m => m.VerantwortlichId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(m => m.Fahrzeug).WithMany().HasForeignKey(m => m.FahrzeugId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(m => m.Geraet).WithMany().HasForeignKey(m => m.GeraetId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.VerantwortlichId);
        builder.Property(m => m.Titel).HasMaxLength(300);
        builder.Property(m => m.Bemerkung).HasMaxLength(1000);
    }
}

internal sealed class EinsatzfunktionKonfiguration : IEntityTypeConfiguration<Einsatzfunktion>
{
    public void Configure(EntityTypeBuilder<Einsatzfunktion> builder) => builder.Property(f => f.Qualifikationen).HasMaxLength(100);
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
        builder.Property(t => t.Korrekturhinweis).HasMaxLength(200);
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

internal sealed class KostenersatzvorgangKonfiguration : IEntityTypeConfiguration<Kostenersatzvorgang>
{
    public void Configure(EntityTypeBuilder<Kostenersatzvorgang> builder)
    {
        builder.HasOne(v => v.Einsatz).WithMany().HasForeignKey(v => v.EinsatzId);
        builder.HasIndex(v => v.EinsatzId).IsUnique();
        builder.Property(v => v.Sachverhalt).HasMaxLength(2000);
        builder.Property(v => v.EmpfehlungBegruendung).HasMaxLength(2000);
        builder.Property(v => v.EmpfohlenVonName).HasMaxLength(200);
        builder.Property(v => v.EntscheidungBegruendung).HasMaxLength(2000);
        builder.Property(v => v.EntschiedenVonName).HasMaxLength(200);
        builder.Ignore(v => v.Stand);
        builder.Ignore(v => v.FeuerwehrDarfAendern);
    }
}

internal sealed class KostenschuldnerKonfiguration : IEntityTypeConfiguration<Kostenschuldner>
{
    public void Configure(EntityTypeBuilder<Kostenschuldner> builder)
    {
        builder.HasOne(s => s.Kostenersatzvorgang).WithMany(v => v.Schuldner).HasForeignKey(s => s.KostenersatzvorgangId);
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Anschrift).HasMaxLength(300);
        builder.Property(s => s.Kennzeichen).HasMaxLength(20);
        builder.Property(s => s.Halterauskunft).HasMaxLength(200);
        builder.Property(s => s.Versicherung).HasMaxLength(200);
        builder.Property(s => s.Versicherungsnummer).HasMaxLength(100);
        builder.Property(s => s.AnteilProzent).HasPrecision(5, 2);
    }
}

internal sealed class ZusatzkostenKonfiguration : IEntityTypeConfiguration<Zusatzkosten>
{
    public void Configure(EntityTypeBuilder<Zusatzkosten> builder)
    {
        builder.HasOne(z => z.Kostenersatzvorgang).WithMany(v => v.Zusatzkosten).HasForeignKey(z => z.KostenersatzvorgangId);
        builder.Property(z => z.Bezeichnung).HasMaxLength(200);
        builder.Property(z => z.Betrag).HasPrecision(12, 2);
    }
}

internal sealed class KostenbescheidKonfiguration : IEntityTypeConfiguration<Kostenbescheid>
{
    public void Configure(EntityTypeBuilder<Kostenbescheid> builder)
    {
        builder.HasOne(b => b.Kostenersatzvorgang).WithMany(v => v.Bescheide).HasForeignKey(b => b.KostenersatzvorgangId);
        // Nach der Löschung des Schuldners (L3) bleibt der Bescheid mit Beträgen für die Forderungsübersicht.
        builder.HasOne(b => b.Kostenschuldner).WithMany().HasForeignKey(b => b.KostenschuldnerId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(b => b.Kassenzeichen).HasMaxLength(50);
        builder.Ignore(b => b.Betrag);
        builder.Ignore(b => b.Bezahlt);
        builder.Ignore(b => b.Offen);
        builder.Ignore(b => b.Erledigt);
        builder.Ignore(b => b.Stand);
    }
}

internal sealed class BmaObjektKonfiguration : IEntityTypeConfiguration<BmaObjekt>
{
    public void Configure(EntityTypeBuilder<BmaObjekt> builder)
    {
        builder.Property(o => o.Bezeichnung).HasMaxLength(200);
        builder.Property(o => o.Anschrift).HasMaxLength(300);
        builder.Property(o => o.Aufschaltnummer).HasMaxLength(50);
        builder.Property(o => o.Konzessionaer).HasMaxLength(200);
        builder.Property(o => o.BetreiberName).HasMaxLength(200);
        builder.Property(o => o.BetreiberAnschrift).HasMaxLength(300);
        builder.Property(o => o.Ansprechpartner).HasMaxLength(300);
        builder.Property(o => o.Bemerkung).HasMaxLength(1000);
    }
}

internal sealed class SatzkalkulationKonfiguration : IEntityTypeConfiguration<Satzkalkulation>
{
    public void Configure(EntityTypeBuilder<Satzkalkulation> builder)
    {
        builder.Property(k => k.Kosten1).HasPrecision(12, 2);
        builder.Property(k => k.Kosten2).HasPrecision(12, 2);
        builder.Property(k => k.Kosten3).HasPrecision(12, 2);
        builder.Property(k => k.Kosten4).HasPrecision(12, 2);
        builder.Property(k => k.StundenJeAngehoerigem).HasPrecision(8, 2);
        builder.Property(k => k.Entschaedigungssatz).HasPrecision(8, 2);
        builder.Property(k => k.Erlaeuterung).HasMaxLength(2000);
        builder.Property(k => k.Bearbeiter).HasMaxLength(200);
    }
}

internal sealed class BescheidEreignisKonfiguration : IEntityTypeConfiguration<BescheidEreignis>
{
    public void Configure(EntityTypeBuilder<BescheidEreignis> builder)
    {
        builder.HasOne(e => e.Kostenbescheid).WithMany(b => b.Ereignisse).HasForeignKey(e => e.KostenbescheidId);
        builder.Property(e => e.Betrag).HasPrecision(12, 2);
        builder.Property(e => e.Vermerk).HasMaxLength(1000);
        builder.Property(e => e.Bearbeiter).HasMaxLength(200);
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

/// <summary>Tabelle der Versandvermerke des Einsatzberichts; gelöscht mit dem Einsatz.</summary>
internal sealed class EinsatzberichtVersandKonfiguration : IEntityTypeConfiguration<EinsatzberichtVersand>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EinsatzberichtVersand> builder)
    {
        builder.HasIndex(v => v.EinsatzId);
        builder.Property(v => v.Empfaenger).HasMaxLength(1000);
        builder.Property(v => v.VersandtVon).HasMaxLength(200);
        builder.HasOne<Einsatz>().WithMany().HasForeignKey(v => v.EinsatzId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Tabelle der Anhänge zum Einsatzbericht; gelöscht mit dem Einsatz.</summary>
internal sealed class EinsatzAnhangKonfiguration : IEntityTypeConfiguration<EinsatzAnhang>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EinsatzAnhang> builder)
    {
        builder.HasIndex(a => a.EinsatzId);
        builder.Property(a => a.Dateiname).HasMaxLength(200);
        builder.Property(a => a.Inhaltstyp).HasMaxLength(100);
        builder.Property(a => a.Beschreibung).HasMaxLength(200);
        builder.HasOne<Einsatz>().WithMany().HasForeignKey(a => a.EinsatzId).OnDelete(DeleteBehavior.Cascade);
    }
}
