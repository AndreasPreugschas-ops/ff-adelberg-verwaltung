using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

internal sealed class ExportformatKonfiguration : IEntityTypeConfiguration<Exportformat>
{
    public void Configure(EntityTypeBuilder<Exportformat> builder)
    {
        builder.HasIndex(f => f.Art).IsUnique();
        builder.Property(f => f.Trennzeichen).HasMaxLength(3);
        builder.Property(f => f.Datumsformat).HasMaxLength(20);
        builder.Property(f => f.Spalten).HasMaxLength(2000);
        builder.Property(f => f.Buchungsstelle).HasMaxLength(50);
        builder.Property(f => f.Sachkonto).HasMaxLength(50);
        builder.Ignore(f => f.Trenner);
    }
}

internal sealed class ParameterKonfiguration : IEntityTypeConfiguration<Parameter>
{
    public void Configure(EntityTypeBuilder<Parameter> builder)
    {
        builder.HasIndex(p => new { p.Schluessel, p.GueltigAb }).IsUnique();
        builder.Property(p => p.Schluessel).HasMaxLength(100);
        builder.Property(p => p.Wert).HasMaxLength(2000);
    }
}

internal sealed class OrganisationKonfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.Ignore(o => o.FeuerwehrAnschrift);
        builder.Ignore(o => o.GemeindeAnschrift);
        builder.Property(o => o.FeuerwehrName).HasMaxLength(200);
        builder.Property(o => o.FeuerwehrStrasse).HasMaxLength(200);
        builder.Property(o => o.FeuerwehrPlz).HasMaxLength(10);
        builder.Property(o => o.FeuerwehrOrt).HasMaxLength(100);
        builder.Property(o => o.FeuerwehrTelefon).HasMaxLength(50);
        builder.Property(o => o.FeuerwehrEmail).HasMaxLength(200);
        builder.Property(o => o.GemeindeName).HasMaxLength(200);
        builder.Property(o => o.GemeindeVertretung).HasMaxLength(200);
        builder.Property(o => o.GemeindeStrasse).HasMaxLength(200);
        builder.Property(o => o.GemeindePlz).HasMaxLength(10);
        builder.Property(o => o.GemeindeOrt).HasMaxLength(100);
        builder.Property(o => o.GemeindeTelefon).HasMaxLength(50);
        builder.Property(o => o.GemeindeEmail).HasMaxLength(200);
        builder.Property(o => o.BuergermeisterName).HasMaxLength(200);
        builder.Property(o => o.BuergermeisterEmail).HasMaxLength(200);
        builder.Property(o => o.Logo).HasColumnType("bytea");
        builder.Property(o => o.LogoInhaltstyp).HasMaxLength(50);
        builder.Property(o => o.Steuernummer).HasMaxLength(20);
        builder.Property(o => o.UmsatzsteuerId).HasMaxLength(20);
        builder.Property(o => o.Finanzamt).HasMaxLength(100);
        builder.Property(o => o.UmsatzsteuerHinweis).HasMaxLength(500);
    }
}

internal sealed class AuditEintragKonfiguration : IEntityTypeConfiguration<AuditEintrag>
{
    public void Configure(EntityTypeBuilder<AuditEintrag> builder)
    {
        builder.HasIndex(a => new { a.Tabelle, a.DatensatzId });
        builder.HasIndex(a => a.Zeitpunkt);
        builder.Property(a => a.Tabelle).HasMaxLength(100);
        builder.Property(a => a.Aenderungen).HasColumnType("jsonb");
    }
}

internal sealed class QualifikationNachweisKonfiguration : IEntityTypeConfiguration<QualifikationNachweis>
{
    public void Configure(EntityTypeBuilder<QualifikationNachweis> builder)
    {
        builder.HasOne(q => q.Mitglied).WithMany().HasForeignKey(q => q.MitgliedId);
        builder.HasOne(q => q.Qualifikation).WithMany().HasForeignKey(q => q.QualifikationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(q => q.GueltigBis);
        builder.Property(q => q.Stunden).HasPrecision(6, 1);
        builder.Property(q => q.Lehrgangsort).HasMaxLength(200);
        builder.Property(q => q.Bemerkung).HasMaxLength(500);
        builder.Property(q => q.Urkunde).HasMaxLength(100);
        builder.HasOne(q => q.Abzeichenregelwerk).WithMany().HasForeignKey(q => q.AbzeichenregelwerkId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AbzeichenregelwerkKonfiguration : IEntityTypeConfiguration<Abzeichenregelwerk>
{
    public void Configure(EntityTypeBuilder<Abzeichenregelwerk> builder)
    {
        builder.ToTable("abzeichenregelwerke");
        builder.Property(r => r.Geltungsbereich).HasMaxLength(200);
        builder.Property(r => r.Quelle).HasMaxLength(300);
        builder.HasMany(r => r.Stufen).WithOne(s => s.Regelwerk).HasForeignKey(s => s.RegelwerkId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AbzeichenregelwerkStufeKonfiguration : IEntityTypeConfiguration<AbzeichenregelwerkStufe>
{
    public void Configure(EntityTypeBuilder<AbzeichenregelwerkStufe> builder)
    {
        builder.ToTable("abzeichenregelwerk_stufen");
        builder.HasOne(s => s.Qualifikation).WithMany().HasForeignKey(s => s.QualifikationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.RegelwerkId, s.QualifikationId }).IsUnique();
        builder.Property(s => s.Voraussetzungen).HasMaxLength(100);
        builder.Property(s => s.Hinweis).HasMaxLength(300);
    }
}

internal sealed class LehrgangsteilnahmeKonfiguration : IEntityTypeConfiguration<Lehrgangsteilnahme>
{
    public void Configure(EntityTypeBuilder<Lehrgangsteilnahme> builder)
    {
        builder.ToTable("lehrgangsteilnahmen");
        builder.HasOne(t => t.Mitglied).WithMany().HasForeignKey(t => t.MitgliedId);
        builder.HasOne(t => t.Qualifikation).WithMany().HasForeignKey(t => t.QualifikationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Nachweis).WithMany().HasForeignKey(t => t.NachweisId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(t => new { t.MitgliedId, t.Abgeschlossen });
        builder.Property(t => t.Lehrgangsort).HasMaxLength(200);
        builder.Property(t => t.Bemerkung).HasMaxLength(500);
    }
}

internal sealed class LehrgangswunschKonfiguration : IEntityTypeConfiguration<Lehrgangswunsch>
{
    public void Configure(EntityTypeBuilder<Lehrgangswunsch> builder)
    {
        builder.ToTable("lehrgangswuensche");
        builder.HasOne(w => w.Mitglied).WithMany().HasForeignKey(w => w.MitgliedId);
        builder.HasOne(w => w.Qualifikation).WithMany().HasForeignKey(w => w.QualifikationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(w => w.Teilnahme).WithMany().HasForeignKey(w => w.TeilnahmeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(w => new { w.Jahr, w.Status });
        builder.Property(w => w.Begruendung).HasMaxLength(500);
        builder.Property(w => w.Bemerkung).HasMaxLength(500);
    }
}

internal sealed class QualifikationKonfiguration : IEntityTypeConfiguration<Qualifikation>
{
    public void Configure(EntityTypeBuilder<Qualifikation> builder)
    {
        builder.HasOne(q => q.Vorstufe).WithMany().HasForeignKey(q => q.VorstufeId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(q => q.Voraussetzungen).HasMaxLength(100);
    }
}

internal sealed class NachweisartKonfiguration : IEntityTypeConfiguration<Nachweisart>
{
    public void Configure(EntityTypeBuilder<Nachweisart> builder)
    {
        builder.HasOne(a => a.Qualifikation).WithMany().HasForeignKey(a => a.QualifikationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(a => a.Beschreibung).HasMaxLength(500);
        builder.ToTable(t => t.HasCheckConstraint("ck_nachweisarten_intervall", "intervall_monate > 0"));
    }
}

internal sealed class UnterweisungsthemaKonfiguration : IEntityTypeConfiguration<Unterweisungsthema>
{
    public void Configure(EntityTypeBuilder<Unterweisungsthema> builder)
    {
        builder.HasOne(t => t.Qualifikation).WithMany().HasForeignKey(t => t.QualifikationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Nachweisart).WithMany().HasForeignKey(t => t.NachweisartId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(t => t.Beschreibung).HasMaxLength(500);
    }
}

internal sealed class AusbildungsthemaKonfiguration : IEntityTypeConfiguration<Ausbildungsthema>
{
    public void Configure(EntityTypeBuilder<Ausbildungsthema> builder) => builder.Property(t => t.Grundlage).HasMaxLength(100);
}

internal sealed class NachweisKonfiguration : IEntityTypeConfiguration<Nachweis>
{
    public void Configure(EntityTypeBuilder<Nachweis> builder)
    {
        builder.HasOne(n => n.Mitglied).WithMany().HasForeignKey(n => n.MitgliedId);
        builder.HasOne(n => n.Nachweisart).WithMany().HasForeignKey(n => n.NachweisartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(n => new { n.MitgliedId, n.NachweisartId, n.Datum });
        builder.Property(n => n.DurchgefuehrtVon).HasMaxLength(100);
        builder.Property(n => n.Bemerkung).HasMaxLength(500);
    }
}

internal sealed class LoeschnachweisKonfiguration : IEntityTypeConfiguration<Loeschnachweis>
{
    public void Configure(EntityTypeBuilder<Loeschnachweis> builder)
    {
        builder.HasIndex(l => l.Zeitpunkt);
        builder.Property(l => l.Regel).HasMaxLength(100);
    }
}

internal sealed class ErinnerungslaufKonfiguration : IEntityTypeConfiguration<Erinnerungslauf>
{
    public void Configure(EntityTypeBuilder<Erinnerungslauf> builder)
    {
        builder.HasIndex(e => e.Zeitpunkt);
    }
}

internal sealed class AtemschutzVorsorgeKonfiguration : IEntityTypeConfiguration<AtemschutzVorsorge>
{
    public void Configure(EntityTypeBuilder<AtemschutzVorsorge> builder)
    {
        builder.HasOne(v => v.Mitglied).WithMany().HasForeignKey(v => v.MitgliedId);
        builder.Property(v => v.Bemerkung).HasMaxLength(200);
    }
}

internal sealed class AtemschutzNachweisKonfiguration : IEntityTypeConfiguration<AtemschutzNachweis>
{
    public void Configure(EntityTypeBuilder<AtemschutzNachweis> builder)
    {
        builder.HasOne(n => n.Mitglied).WithMany().HasForeignKey(n => n.MitgliedId);
        builder.HasIndex(n => new { n.MitgliedId, n.Art, n.Datum });
        builder.Property(n => n.PaGeraet).HasMaxLength(50);
        builder.Property(n => n.Maske).HasMaxLength(50);
        builder.Property(n => n.Exposition).HasMaxLength(200);
        builder.Ignore(n => n.Expositionsrelevant);
        builder.Ignore(n => n.Bestaetigt);
        builder.HasIndex(n => n.EinsatzId);
    }
}

/// <summary>Tabelle atemschutz_sperren mit Index auf dem Mitglied.</summary>
internal sealed class AtemschutzSperreKonfiguration : IEntityTypeConfiguration<AtemschutzSperre>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AtemschutzSperre> builder)
    {
        builder.HasOne(s => s.Mitglied).WithMany().HasForeignKey(s => s.MitgliedId);
        builder.HasIndex(s => new { s.MitgliedId, s.Bis });
        builder.Ignore(s => s.Tage);
    }
}

internal sealed class DienstKonfiguration : IEntityTypeConfiguration<Dienst>
{
    public void Configure(EntityTypeBuilder<Dienst> builder)
    {
        builder.HasIndex(d => d.Beginn);
        builder.Property(d => d.Titel).HasMaxLength(200);
        builder.HasMany(d => d.Abteilungen).WithMany().UsingEntity<Dictionary<string, object>>("DienstAbteilungen",
            r => r.HasOne<Abteilung>().WithMany().HasForeignKey("AbteilungId").OnDelete(DeleteBehavior.Restrict),
            l => l.HasOne<Dienst>().WithMany().HasForeignKey("DienstId").OnDelete(DeleteBehavior.Cascade),
            j => j.HasKey("DienstId", "AbteilungId"));
        builder.HasOne(d => d.Verantwortlicher).WithMany().HasForeignKey(d => d.VerantwortlicherId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(d => d.Gruppe).WithMany().HasForeignKey(d => d.GruppeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Qualifikation).WithMany().HasForeignKey(d => d.QualifikationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Serie).WithMany(s => s.Dienste).HasForeignKey(d => d.SerieId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(d => d.Unterweisungsthemen).WithMany().UsingEntity<Dictionary<string, object>>("DienstUnterweisungsthemen",
            r => r.HasOne<Unterweisungsthema>().WithMany().HasForeignKey("UnterweisungsthemaId").OnDelete(DeleteBehavior.Restrict),
            l => l.HasOne<Dienst>().WithMany().HasForeignKey("DienstId").OnDelete(DeleteBehavior.Cascade),
            j => j.HasKey("DienstId", "UnterweisungsthemaId"));
        builder.HasOne<Fevad.Domain.Einsaetze.Kostensatz>().WithMany().HasForeignKey(d => d.KostensatzId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(d => d.Auftraggeber).HasMaxLength(500);
        builder.Property(d => d.AuftraggeberKontakt).HasMaxLength(200);
        builder.HasOne(d => d.Ausbildungsthema).WithMany().HasForeignKey(d => d.AusbildungsthemaId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(d => d.Lernziele).HasMaxLength(2000);
        builder.Property(d => d.FahrzeugeUndMaterial).HasMaxLength(1000);
        builder.Ignore(d => d.Dauer);
        builder.Ignore(d => d.AbteilungIds);
        builder.ToTable(t => t.HasCheckConstraint("ck_dienste_ende_nach_beginn", "ende >= beginn"));
    }
}

internal sealed class AnwesenheitKonfiguration : IEntityTypeConfiguration<Anwesenheit>
{
    public void Configure(EntityTypeBuilder<Anwesenheit> builder)
    {
        builder.HasOne(a => a.Dienst).WithMany(d => d.Anwesenheiten).HasForeignKey(a => a.DienstId);
        builder.HasOne(a => a.Mitglied).WithMany().HasForeignKey(a => a.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.DienstId, a.MitgliedId }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}

internal sealed class AnwesenheitsjahressummeKonfiguration : IEntityTypeConfiguration<Anwesenheitsjahressumme>
{
    public void Configure(EntityTypeBuilder<Anwesenheitsjahressumme> builder)
    {
        builder.HasOne(s => s.Mitglied).WithMany().HasForeignKey(s => s.MitgliedId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.MitgliedId, s.Jahr }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}

internal sealed class BankverbindungKonfiguration : IEntityTypeConfiguration<Bankverbindung>
{
    public void Configure(EntityTypeBuilder<Bankverbindung> builder)
    {
        builder.HasOne(b => b.Mitglied).WithMany().HasForeignKey(b => b.MitgliedId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(b => b.MitgliedId).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.Property(b => b.Iban).HasMaxLength(34);
        builder.Property(b => b.Kontoinhaber).HasMaxLength(100);
    }
}

internal sealed class EntschaedigungsantragKonfiguration : IEntityTypeConfiguration<Entschaedigungsantrag>
{
    public void Configure(EntityTypeBuilder<Entschaedigungsantrag> builder)
    {
        builder.HasOne(a => a.Mitglied).WithMany().HasForeignKey(a => a.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Lehrgangsteilnahme).WithMany().HasForeignKey(a => a.LehrgangsteilnahmeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(a => a.Einsatz).WithMany().HasForeignKey(a => a.EinsatzId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(a => a.Tage).WithOne().HasForeignKey(t => t.EntschaedigungsantragId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.Belege).WithOne().HasForeignKey(b => b.EntschaedigungsantragId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.MitgliedId, a.Stand });
        builder.Property(a => a.Bezeichnung).HasMaxLength(200);
        builder.Property(a => a.Ort).HasMaxLength(200);
        builder.Property(a => a.Bemerkung).HasMaxLength(1000);
        builder.Property(a => a.Entscheidungsvermerk).HasMaxLength(500);
        builder.Property(a => a.VerdienstausfallBetrag).HasPrecision(10, 2);
        builder.Property(a => a.SozialversicherungBetrag).HasPrecision(10, 2);
        builder.Property(a => a.AuslagenBetrag).HasPrecision(10, 2);
        builder.Property(a => a.Kilometer).HasPrecision(8, 1);
        builder.Property(a => a.Fahrkosten).HasPrecision(10, 2);
        builder.Ignore(a => a.ErsterTag);
        builder.Ignore(a => a.AufeinanderfolgendeTage);
    }
}

internal sealed class AntragBelegKonfiguration : IEntityTypeConfiguration<AntragBeleg>
{
    public void Configure(EntityTypeBuilder<AntragBeleg> builder)
    {
        builder.Property(b => b.Dateiname).HasMaxLength(200);
        builder.Property(b => b.Inhaltstyp).HasMaxLength(100);
        builder.Property(b => b.Pruefsumme).HasMaxLength(64);
    }
}

internal sealed class ArbeitgeberbescheinigungKonfiguration : IEntityTypeConfiguration<Arbeitgeberbescheinigung>
{
    public void Configure(EntityTypeBuilder<Arbeitgeberbescheinigung> builder)
    {
        builder.HasOne(b => b.Mitglied).WithMany().HasForeignKey(b => b.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => b.Pruefcode).IsUnique();
        builder.HasIndex(b => b.MitgliedId);
        builder.Property(b => b.Pruefcode).HasMaxLength(14);
        builder.Property(b => b.Arbeitgeber).HasMaxLength(200);
        builder.Property(b => b.Zeilen).HasMaxLength(20000);
        builder.Property(b => b.Stunden).HasPrecision(8, 2);
    }
}

internal sealed class ArbeitgebererstattungKonfiguration : IEntityTypeConfiguration<Arbeitgebererstattung>
{
    public void Configure(EntityTypeBuilder<Arbeitgebererstattung> builder)
    {
        builder.HasOne(e => e.Mitglied).WithMany().HasForeignKey(e => e.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.MitgliedId, e.Von });
        builder.Property(e => e.Arbeitgeber).HasMaxLength(200);
        builder.Property(e => e.Pruefcode).HasMaxLength(14);
        builder.Property(e => e.Vermerk).HasMaxLength(500);
        builder.Property(e => e.Entgelt).HasPrecision(10, 2);
        builder.Property(e => e.Sozialversicherung).HasPrecision(10, 2);
        builder.Ignore(e => e.Summe);
        builder.Ignore(e => e.SperrtVerdienstausfall);
    }
}

internal sealed class AbrechnungKonfiguration : IEntityTypeConfiguration<Abrechnung>
{
    public void Configure(EntityTypeBuilder<Abrechnung> builder)
    {
        builder.Property(a => a.Bezeichnung).HasMaxLength(100);
        builder.Property(a => a.Hinweise).HasMaxLength(4000);
        builder.HasIndex(a => new { a.Von, a.Bis });
        builder.Ignore(a => a.Summe);
    }
}

internal sealed class AbrechnungsPostenKonfiguration : IEntityTypeConfiguration<AbrechnungsPosten>
{
    public void Configure(EntityTypeBuilder<AbrechnungsPosten> builder)
    {
        builder.HasOne(p => p.Abrechnung).WithMany(a => a.Posten).HasForeignKey(p => p.AbrechnungId);
        builder.HasOne(p => p.Mitglied).WithMany().HasForeignKey(p => p.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.EinsatzTeilnahme).WithMany().HasForeignKey(p => p.EinsatzTeilnahmeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Anwesenheit).WithMany().HasForeignKey(p => p.AnwesenheitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.FunktionZuordnung).WithMany().HasForeignKey(p => p.FunktionZuordnungId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.GeleiteterDienst).WithMany().HasForeignKey(p => p.GeleiteterDienstId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.GeleiteterDienstId);
        builder.HasOne(p => p.Entschaedigungsantrag).WithMany().HasForeignKey(p => p.EntschaedigungsantragId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.EntschaedigungsantragId);
        builder.HasIndex(p => p.EinsatzTeilnahmeId);
        builder.HasIndex(p => p.AnwesenheitId);
        builder.HasIndex(p => p.FunktionZuordnungId);
        builder.Property(p => p.Vorgang).HasMaxLength(40);
        builder.Property(p => p.Bezeichnung).HasMaxLength(200);
        builder.Property(p => p.Menge).HasPrecision(8, 2);
        builder.Property(p => p.Satz).HasPrecision(10, 2);
        builder.Property(p => p.Betrag).HasPrecision(10, 2);
    }
}

internal sealed class RueckmeldungKonfiguration : IEntityTypeConfiguration<Rueckmeldung>
{
    public void Configure(EntityTypeBuilder<Rueckmeldung> builder)
    {
        builder.HasOne(r => r.Dienst).WithMany(d => d.Rueckmeldungen).HasForeignKey(r => r.DienstId);
        builder.HasOne(r => r.Mitglied).WithMany().HasForeignKey(r => r.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.DienstId, r.MitgliedId }).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.Property(r => r.Begruendung).HasMaxLength(500);
        builder.HasOne(r => r.Abwesenheit).WithMany().HasForeignKey(r => r.AbwesenheitId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Gemeldete Abwesenheiten (V37): je Mitglied nach Zeitraum gesucht.</summary>
internal sealed class AbwesenheitKonfiguration : IEntityTypeConfiguration<Abwesenheit>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Abwesenheit> builder)
    {
        builder.HasOne(a => a.Mitglied).WithMany().HasForeignKey(a => a.MitgliedId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.MitgliedId, a.Von, a.Bis });
        builder.Property(a => a.Bemerkung).HasMaxLength(500);
        builder.Ignore(a => a.Tage);
    }
}

internal sealed class JugendveranstaltungKonfiguration : IEntityTypeConfiguration<Jugendveranstaltung>
{
    public void Configure(EntityTypeBuilder<Jugendveranstaltung> builder)
    {
        builder.HasOne(v => v.Dienst).WithMany().HasForeignKey(v => v.DienstId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => v.DienstId).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.HasOne(v => v.Zuschussgeber).WithMany().HasForeignKey(v => v.ZuschussgeberId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(v => v.Teilnehmerbeitrag).HasPrecision(10, 2);
        builder.Property(v => v.ZuschussBetrag).HasPrecision(10, 2);
        builder.Property(v => v.Bemerkung).HasMaxLength(1000);
    }
}

internal sealed class VeranstaltungsanmeldungKonfiguration : IEntityTypeConfiguration<Veranstaltungsanmeldung>
{
    public void Configure(EntityTypeBuilder<Veranstaltungsanmeldung> builder)
    {
        builder.HasOne(a => a.Jugendveranstaltung).WithMany(v => v.Anmeldungen).HasForeignKey(a => a.JugendveranstaltungId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Mitglied).WithMany().HasForeignKey(a => a.MitgliedId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.JugendveranstaltungId, a.MitgliedId }).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.Property(a => a.AngemeldetVon).HasMaxLength(200);
    }
}

internal sealed class VeranstaltungsbetreuerKonfiguration : IEntityTypeConfiguration<Veranstaltungsbetreuer>
{
    public void Configure(EntityTypeBuilder<Veranstaltungsbetreuer> builder)
    {
        builder.HasOne(b => b.Jugendveranstaltung).WithMany(v => v.Betreuer).HasForeignKey(b => b.JugendveranstaltungId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(b => b.Jugendbetreuer).WithMany().HasForeignKey(b => b.JugendbetreuerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(b => new { b.JugendveranstaltungId, b.JugendbetreuerId }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}

internal sealed class ZuschussgeberKonfiguration : IEntityTypeConfiguration<Zuschussgeber>
{
    public void Configure(EntityTypeBuilder<Zuschussgeber> builder)
    {
        builder.Property(z => z.Name).HasMaxLength(200);
        builder.Property(z => z.SatzJeTeilnehmertag).HasPrecision(10, 2);
        builder.Property(z => z.Bemerkung).HasMaxLength(1000);
    }
}

internal sealed class ArchivangebotKonfiguration : IEntityTypeConfiguration<Archivangebot>
{
    public void Configure(EntityTypeBuilder<Archivangebot> builder)
    {
        builder.HasIndex(a => new { a.Art, a.DatensatzId });
        builder.HasIndex(a => a.ErledigtAm);
        builder.Property(a => a.Bezeichnung).HasMaxLength(300);
        builder.Property(a => a.Pruefsumme).HasMaxLength(64);
    }
}
