using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Gemeinsam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fevad.Infrastructure.Persistenz.Konfigurationen;

internal sealed class ParameterKonfiguration : IEntityTypeConfiguration<Parameter>
{
    public void Configure(EntityTypeBuilder<Parameter> builder)
    {
        builder.HasIndex(p => new { p.Schluessel, p.GueltigAb }).IsUnique();
        builder.Property(p => p.Schluessel).HasMaxLength(100);
        builder.Property(p => p.Wert).HasMaxLength(500);
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
    }
}

internal sealed class DienstKonfiguration : IEntityTypeConfiguration<Dienst>
{
    public void Configure(EntityTypeBuilder<Dienst> builder)
    {
        builder.HasIndex(d => d.Beginn);
        builder.Property(d => d.Titel).HasMaxLength(200);
        builder.HasOne(d => d.Abteilung).WithMany().HasForeignKey(d => d.AbteilungId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Verantwortlicher).WithMany().HasForeignKey(d => d.VerantwortlicherId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(d => d.Gruppe).WithMany().HasForeignKey(d => d.GruppeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Qualifikation).WithMany().HasForeignKey(d => d.QualifikationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Serie).WithMany(s => s.Dienste).HasForeignKey(d => d.SerieId).OnDelete(DeleteBehavior.SetNull);
        builder.Ignore(d => d.Dauer);
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
    }
}
