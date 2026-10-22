using Fevad.Domain.Atemschutz;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
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
