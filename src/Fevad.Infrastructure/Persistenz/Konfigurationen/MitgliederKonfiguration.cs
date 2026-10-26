using Fevad.Domain.Gemeinsam;
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
        builder.Property(m => m.Fachgebiet).HasMaxLength(100);
        builder.Property(m => m.TelefonMobil).HasMaxLength(20);
        builder.Property(m => m.TelefonFestnetz).HasMaxLength(20);
        builder.Property(m => m.Email).HasMaxLength(200);
        builder.Property(m => m.NotfallkontaktName).HasMaxLength(100);
        builder.Property(m => m.DjfAusweisnummer).HasMaxLength(30);
        builder.Property(m => m.Abholberechtigte).HasMaxLength(300);
        builder.Property(m => m.NotfallkontaktTelefon).HasMaxLength(20);
        builder.Property(m => m.Beruf).HasMaxLength(100);
        builder.Property(m => m.Tagesverfuegbarkeit).HasConversion<int>();
        builder.Property(m => m.ArbeitgeberStrasse).HasMaxLength(200);
        builder.Property(m => m.ArbeitgeberPlz).HasMaxLength(10);
        builder.Property(m => m.ArbeitgeberTelefon).HasMaxLength(20);
        builder.Property(m => m.ArbeitgeberMobil).HasMaxLength(20);
        builder.Property(m => m.ArbeitgeberEmail).HasMaxLength(200);
        builder.Ignore(m => m.Anzeigename);
    }
}

/// <summary>Änderungsanträge der Mitglieder (V36): je Mitglied nach Stand gesucht, Felder hängen am Antrag.</summary>
internal sealed class AenderungsantragKonfiguration : IEntityTypeConfiguration<Aenderungsantrag>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Aenderungsantrag> builder)
    {
        builder.HasOne(a => a.Mitglied).WithMany().HasForeignKey(a => a.MitgliedId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.Felder).WithOne(f => f.Aenderungsantrag).HasForeignKey(f => f.AenderungsantragId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(a => a.Bemerkung).HasMaxLength(500);
        builder.Property(a => a.Ablehnungsgrund).HasMaxLength(500);
        builder.Property(a => a.EntschiedenVon).HasMaxLength(256);
        builder.HasIndex(a => new { a.MitgliedId, a.Status });
        builder.Ignore(a => a.Offen);
        builder.Ignore(a => a.BetrifftArbeitgeber);
    }
}

/// <summary>Felder eines Änderungsantrags, Längen wie die Stammdaten.</summary>
internal sealed class AenderungsfeldKonfiguration : IEntityTypeConfiguration<Aenderungsfeld>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Aenderungsfeld> builder)
    {
        builder.Property(f => f.Alt).HasMaxLength(200);
        builder.Property(f => f.Neu).HasMaxLength(200);
    }
}

internal sealed class AbteilungKonfiguration : IEntityTypeConfiguration<Abteilung>
{
    public void Configure(EntityTypeBuilder<Abteilung> builder)
    {
        builder.HasOne(a => a.Uebergeordnete).WithMany().HasForeignKey(a => a.UebergeordneteId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(a => a.MitProbezeit);
        builder.Ignore(a => a.Anschrift);
        builder.Property(a => a.Strasse).HasMaxLength(200);
        builder.Property(a => a.Plz).HasMaxLength(10);
        builder.Property(a => a.Ort).HasMaxLength(100);
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
        builder.HasOne(f => f.Abteilung).WithMany().HasForeignKey(f => f.AbteilungId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FunktionKonfiguration : IEntityTypeConfiguration<Funktion>
{
    public void Configure(EntityTypeBuilder<Funktion> builder)
    {
        builder.Property(f => f.Grundlage).HasMaxLength(200);
        builder.Property(f => f.Voraussetzungen).HasMaxLength(200);
        builder.Property(f => f.BeschlussDurch).HasMaxLength(100);
    }
}

internal sealed class EinwilligungsartKonfiguration : IEntityTypeConfiguration<Einwilligungsart>
{
    public void Configure(EntityTypeBuilder<Einwilligungsart> builder)
    {
        builder.Property(a => a.Beschreibung).HasMaxLength(500);
        builder.Property(a => a.HinweisBeiWiderruf).HasMaxLength(200);
    }
}

internal sealed class AntragEinwilligungKonfiguration : IEntityTypeConfiguration<AntragEinwilligung>
{
    public void Configure(EntityTypeBuilder<AntragEinwilligung> builder)
    {
        builder.HasOne(e => e.Antrag).WithMany(a => a.Einwilligungen).HasForeignKey(e => e.AntragId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Einwilligungsart).WithMany().HasForeignKey(e => e.EinwilligungsartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.AntragId, e.EinwilligungsartId }).IsUnique();
    }
}

internal sealed class EinwilligungKonfiguration : IEntityTypeConfiguration<Einwilligung>
{
    public void Configure(EntityTypeBuilder<Einwilligung> builder)
    {
        builder.HasOne(e => e.Mitglied).WithMany().HasForeignKey(e => e.MitgliedId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Einwilligungsart).WithMany().HasForeignKey(e => e.EinwilligungsartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Identitaet.Benutzer>().WithMany().HasForeignKey(e => e.KontoId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(e => new { e.MitgliedId, e.EinwilligungsartId });
        builder.Property(e => e.DurchName).HasMaxLength(200);
        builder.Property(e => e.Nachweis).HasMaxLength(200);
        builder.Property(e => e.WiderrufenDurch).HasMaxLength(200);
        builder.Property(e => e.FolgeaufgabeErledigtVon).HasMaxLength(200);
    }
}

internal sealed class LoeschanfrageKonfiguration : IEntityTypeConfiguration<Loeschanfrage>
{
    public void Configure(EntityTypeBuilder<Loeschanfrage> builder)
    {
        builder.Property(l => l.Name).HasMaxLength(200);
        builder.Property(l => l.Text).HasMaxLength(4000);
        builder.Property(l => l.Antwort).HasMaxLength(4000);
        builder.HasOne<Identitaet.Benutzer>().WithMany().HasForeignKey(l => l.KontoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Mitglied>().WithMany().HasForeignKey(l => l.MitgliedId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(l => l.ErledigtAm);
    }
}

internal sealed class ErziehungsberechtigterKonfiguration : IEntityTypeConfiguration<Erziehungsberechtigter>
{
    public void Configure(EntityTypeBuilder<Erziehungsberechtigter> builder)
    {
        builder.HasOne(e => e.Mitglied).WithMany(m => m.Erziehungsberechtigte).HasForeignKey(e => e.MitgliedId);
        builder.HasIndex(e => new { e.MitgliedId, e.Nummer }).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.HasOne<Identitaet.Benutzer>().WithMany().HasForeignKey(e => e.KontoId).OnDelete(DeleteBehavior.SetNull);
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

internal sealed class AntragKonfiguration : IEntityTypeConfiguration<Antrag>
{
    public void Configure(EntityTypeBuilder<Antrag> builder)
    {
        builder.ToTable("antraege");
        builder.HasOne(a => a.Abteilung).WithMany().HasForeignKey(a => a.AbteilungId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Mitglied).WithMany().HasForeignKey(a => a.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.WiedereintrittVon).WithMany().HasForeignKey(a => a.WiedereintrittVonId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.MitgliedId);
        builder.Property(a => a.Vorname).HasMaxLength(100);
        builder.Property(a => a.Nachname).HasMaxLength(100);
        builder.Property(a => a.Geburtsort).HasMaxLength(100);
        builder.Property(a => a.Strasse).HasMaxLength(200);
        builder.Property(a => a.Plz).HasMaxLength(10);
        builder.Property(a => a.Ort).HasMaxLength(100);
        builder.Property(a => a.Telefon).HasMaxLength(20);
        builder.Property(a => a.Mobil).HasMaxLength(20);
        builder.Property(a => a.Email).HasMaxLength(200);
        builder.Property(a => a.FrueherFeuerwehrName).HasMaxLength(200);
        builder.Property(a => a.FrueherLehrgaenge).HasMaxLength(500);
        builder.Property(a => a.FrueherDienstgrad).HasMaxLength(100);
        builder.Property(a => a.AndereFeuerwehrName).HasMaxLength(200);
        builder.Property(a => a.Bemerkung).HasMaxLength(1000);
    }
}

internal sealed class AntragElternteilKonfiguration : IEntityTypeConfiguration<AntragElternteil>
{
    public void Configure(EntityTypeBuilder<AntragElternteil> builder)
    {
        builder.ToTable("antrag_erziehungsberechtigte");
        builder.HasOne(e => e.Antrag).WithMany(a => a.Erziehungsberechtigte).HasForeignKey(e => e.AntragId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.AntragId, e.Nummer }).IsUnique().HasFilter("geloescht_am IS NULL");
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

internal sealed class FruehereAnschriftKonfiguration : IEntityTypeConfiguration<FruehereAnschrift>
{
    public void Configure(EntityTypeBuilder<FruehereAnschrift> builder)
    {
        builder.ToTable("fruehere_anschriften");
        builder.HasOne(a => a.Mitglied).WithMany(m => m.FruehereAnschriften).HasForeignKey(a => a.MitgliedId);
        builder.HasIndex(a => new { a.MitgliedId, a.Art, a.BisDatum });
        builder.Property(a => a.Arbeitgeber).HasMaxLength(200);
        builder.Property(a => a.Strasse).HasMaxLength(200);
        builder.Property(a => a.Plz).HasMaxLength(10);
        builder.Property(a => a.Ort).HasMaxLength(100);
    }
}

internal sealed class MitgliederbescheidKonfiguration : IEntityTypeConfiguration<Mitgliederbescheid>
{
    public void Configure(EntityTypeBuilder<Mitgliederbescheid> builder)
    {
        builder.Property(b => b.EmpfaengerName).HasMaxLength(200);
        builder.Property(b => b.EmpfaengerAnschrift).HasMaxLength(300);
        builder.Property(b => b.Sachverhalt).HasMaxLength(4000);
        builder.Property(b => b.Tenor).HasMaxLength(1000);
        builder.Property(b => b.Begruendung).HasMaxLength(4000);
        builder.Property(b => b.Stellungnahme).HasMaxLength(2000);
        builder.Property(b => b.Betrag).HasPrecision(10, 2);
        builder.HasOne(b => b.Mitglied).WithMany().HasForeignKey(b => b.MitgliedId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class DienstausweisKonfiguration : IEntityTypeConfiguration<Dienstausweis>
{
    public void Configure(EntityTypeBuilder<Dienstausweis> builder)
    {
        builder.Property(d => d.Nummer).HasMaxLength(50);
        builder.HasIndex(d => d.Nummer).IsUnique();
        builder.Property(d => d.Vermerk).HasMaxLength(300);
        builder.HasOne(d => d.Mitglied).WithMany().HasForeignKey(d => d.MitgliedId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class JugendbetreuerKonfiguration : IEntityTypeConfiguration<Jugendbetreuer>
{
    public void Configure(EntityTypeBuilder<Jugendbetreuer> builder)
    {
        builder.Property(b => b.Vorname).HasMaxLength(100);
        builder.Property(b => b.Nachname).HasMaxLength(100);
        builder.Property(b => b.Aufgabe).HasMaxLength(100);
        builder.Property(b => b.ZeugnisNichtNoetigGrund).HasMaxLength(300);
        builder.Property(b => b.EingesehenVon).HasMaxLength(200);
        builder.HasOne(b => b.Abteilung).WithMany().HasForeignKey(b => b.AbteilungId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => b.Bis);
    }
}

internal sealed class InteressentKonfiguration : IEntityTypeConfiguration<Interessent>
{
    public void Configure(EntityTypeBuilder<Interessent> builder)
    {
        builder.Property(i => i.Vorname).HasMaxLength(100);
        builder.Property(i => i.Nachname).HasMaxLength(100);
        builder.Property(i => i.Ansprechpartner).HasMaxLength(200);
        builder.Property(i => i.Telefon).HasMaxLength(50);
        builder.Property(i => i.Email).HasMaxLength(200);
        builder.Property(i => i.Bemerkung).HasMaxLength(500);
        builder.HasOne(i => i.Abteilung).WithMany().HasForeignKey(i => i.AbteilungId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Schnupperdienst).WithMany().HasForeignKey(i => i.SchnupperdienstId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(i => i.Wiedervorlage);
    }
}

internal sealed class DienstpflichtvorgangKonfiguration : IEntityTypeConfiguration<Dienstpflichtvorgang>
{
    public void Configure(EntityTypeBuilder<Dienstpflichtvorgang> builder)
    {
        builder.HasOne(v => v.Mitglied).WithMany().HasForeignKey(v => v.MitgliedId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => new { v.MitgliedId, v.Datum });
        builder.Property(v => v.Sachverhalt).HasMaxLength(2000);
        builder.Property(v => v.ErfasstVon).HasMaxLength(200);
        builder.Ignore(v => v.Bezugstag);
    }
}
