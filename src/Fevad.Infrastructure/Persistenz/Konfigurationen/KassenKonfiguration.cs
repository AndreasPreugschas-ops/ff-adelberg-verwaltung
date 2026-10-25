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
        builder.Property(b => b.AusnahmeBegruendung).HasMaxLength(500);
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
        builder.HasOne(a => a.Mitglied).WithMany().HasForeignKey(a => a.MitgliedId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(a => a.Betrag).HasPrecision(12, 2);
        builder.Property(a => a.Empfaenger).HasMaxLength(200);
        builder.Property(a => a.Zweck).HasMaxLength(500);
        builder.Property(a => a.Bemerkung).HasMaxLength(1000);
        builder.HasIndex(a => new { a.KasseId, a.Status });
        builder.HasIndex(a => a.MitgliedId);
        builder.ToTable(t => t.HasCheckConstraint("ck_zahlungsanordnungen_mitglied", "art <> 'Auslagenerstattung' OR mitglied_id IS NOT NULL"));
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

/// <summary>Wiederkehrende Buchungen (V07).</summary>
internal sealed class WiederkehrendeBuchungKonfiguration : IEntityTypeConfiguration<WiederkehrendeBuchung>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WiederkehrendeBuchung> builder)
    {
        builder.ToTable("wiederkehrende_buchungen");
        builder.HasOne(w => w.Kasse).WithMany().HasForeignKey(w => w.KasseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(w => w.Konto).WithMany().HasForeignKey(w => w.KontoId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(w => w.Planposition).WithMany().HasForeignKey(w => w.PlanpositionId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(w => w.Bezeichnung).HasMaxLength(200);
        builder.Property(w => w.BetragParameter).HasMaxLength(200);
        builder.Property(w => w.Betrag).HasPrecision(12, 2);
        builder.Property(w => w.Partner).HasMaxLength(200);
        builder.Property(w => w.Text).HasMaxLength(500);
        builder.HasIndex(w => new { w.KasseId, w.NaechsteFaelligkeit });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_wiederkehrende_buchungen_tag", "tag BETWEEN 1 AND 31");
            t.HasCheckConstraint("ck_wiederkehrende_buchungen_art", "art <> 'Umbuchung'");
            t.HasCheckConstraint("ck_wiederkehrende_buchungen_betrag", "betrag IS NULL OR betrag > 0");
        });
    }
}

/// <summary>CSV-Formate der Banken (V07).</summary>
internal sealed class BankformatKonfiguration : IEntityTypeConfiguration<Bankformat>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Bankformat> builder)
    {
        builder.Property(f => f.Name).HasMaxLength(200);
        foreach (var spalte in new[] { nameof(Bankformat.SpalteBuchungstag), nameof(Bankformat.SpalteValuta), nameof(Bankformat.SpalteBetrag),
                     nameof(Bankformat.SpalteSoll), nameof(Bankformat.SpalteHaben), nameof(Bankformat.SpalteSollHaben), nameof(Bankformat.SpalteGegenpartei),
                     nameof(Bankformat.SpalteVerwendungszweck), nameof(Bankformat.SpalteReferenz) })
        {
            builder.Property(spalte).HasMaxLength(100);
        }

        builder.Property(f => f.Datumsformat).HasMaxLength(20);
        builder.HasIndex(f => f.Name).IsUnique().HasFilter("geloescht_am IS NULL");
        builder.ToTable(t => t.HasCheckConstraint("ck_bankformate_kopfzeilen", "kopfzeilen_ueberspringen BETWEEN 0 AND 50"));
    }
}

/// <summary>Eingelesene Kontoauszugsdateien (V07).</summary>
internal sealed class KontoauszugimportKonfiguration : IEntityTypeConfiguration<Kontoauszugimport>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Kontoauszugimport> builder)
    {
        builder.HasOne(i => i.Konto).WithMany().HasForeignKey(i => i.KontoId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(i => i.Dateiname).HasMaxLength(200);
        builder.Property(i => i.Endsaldo).HasPrecision(12, 2);
        builder.HasIndex(i => new { i.KontoId, i.Bis });
    }
}

/// <summary>Umsätze aus Kontoauszügen (V07); der Fingerabdruck ist je Konto eindeutig.</summary>
internal sealed class KontoumsatzKonfiguration : IEntityTypeConfiguration<Kontoumsatz>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Kontoumsatz> builder)
    {
        builder.HasOne(u => u.Konto).WithMany().HasForeignKey(u => u.KontoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Kontoauszugimport>().WithMany().HasForeignKey(u => u.ImportId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(u => u.Buchung).WithMany().HasForeignKey(u => u.BuchungId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(u => u.Betrag).HasPrecision(12, 2);
        builder.Property(u => u.Gegenpartei).HasMaxLength(200);
        builder.Property(u => u.Verwendungszweck).HasMaxLength(1000);
        builder.Property(u => u.Referenz).HasMaxLength(100);
        builder.Property(u => u.Fingerabdruck).HasMaxLength(64);
        builder.HasIndex(u => new { u.KontoId, u.Fingerabdruck }).IsUnique();
        builder.HasIndex(u => new { u.KontoId, u.Status });
        builder.HasIndex(u => u.BuchungId).IsUnique().HasFilter("buchung_id IS NOT NULL AND geloescht_am IS NULL");
    }
}

/// <summary>Bestandsverzeichnis der Kassen (V04).</summary>
internal sealed class VermoegensgegenstandKonfiguration : IEntityTypeConfiguration<Vermoegensgegenstand>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Vermoegensgegenstand> builder)
    {
        builder.ToTable("vermoegensgegenstaende", t =>
        {
            t.HasCheckConstraint("ck_vermoegensgegenstaende_wert", "wert >= 0");
            t.HasCheckConstraint("ck_vermoegensgegenstaende_abgang", "abgang_am IS NULL OR abgang_am >= anschaffung");
        });
        builder.HasOne(v => v.Kasse).WithMany().HasForeignKey(v => v.KasseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.Buchung).WithMany().HasForeignKey(v => v.BuchungId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(v => v.Geraet).WithMany().HasForeignKey(v => v.GeraetId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(v => v.Inventarnummer).HasMaxLength(50);
        builder.Property(v => v.Bezeichnung).HasMaxLength(200);
        builder.Property(v => v.Wert).HasPrecision(12, 2);
        builder.Property(v => v.Erloes).HasPrecision(12, 2);
        builder.Property(v => v.Standort).HasMaxLength(200);
        builder.Property(v => v.AbgangGrund).HasMaxLength(200);
        builder.Property(v => v.Bemerkung).HasMaxLength(1000);
        builder.HasIndex(v => new { v.KasseId, v.Anschaffung });
        builder.HasIndex(v => v.BuchungId);
    }
}

/// <summary>Jahresabschlüsse der Kassen (V05); je Kasse und Jahr höchstens einer.</summary>
internal sealed class JahresabschlussKonfiguration : IEntityTypeConfiguration<Jahresabschluss>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Jahresabschluss> builder)
    {
        builder.ToTable("jahresabschluesse");
        builder.HasOne(j => j.Kasse).WithMany().HasForeignKey(j => j.KasseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(j => j.EntlastungBeschluss).WithMany().HasForeignKey(j => j.EntlastungBeschlussId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(j => j.Endbestaende).WithOne().HasForeignKey(e => e.JahresabschlussId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(j => j.Einnahmen).HasPrecision(12, 2);
        builder.Property(j => j.Ausgaben).HasPrecision(12, 2);
        builder.Property(j => j.Bemerkung).HasMaxLength(1000);
        builder.HasIndex(j => new { j.KasseId, j.Jahr }).IsUnique().HasFilter("geloescht_am IS NULL");
    }
}

/// <summary>Endbestände der Konten im Jahresabschluss.</summary>
internal sealed class KontoendbestandKonfiguration : IEntityTypeConfiguration<Kontoendbestand>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Kontoendbestand> builder)
    {
        builder.ToTable("kontoendbestaende");
        builder.HasOne(e => e.Konto).WithMany().HasForeignKey(e => e.KontoId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Bestand).HasPrecision(12, 2);
    }
}

/// <summary>Kassenprüfungen mit zwei verschiedenen Prüfern (V05).</summary>
internal sealed class KassenpruefungKonfiguration : IEntityTypeConfiguration<Kassenpruefung>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Kassenpruefung> builder)
    {
        builder.ToTable("kassenpruefungen", t =>
        {
            t.HasCheckConstraint("ck_kassenpruefungen_zeitraum", "bis >= von");
            t.HasCheckConstraint("ck_kassenpruefungen_pruefer", "pruefer1id <> pruefer2id");
        });
        builder.HasOne(p => p.Kasse).WithMany().HasForeignKey(p => p.KasseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Pruefer1).WithMany().HasForeignKey(p => p.Pruefer1Id).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Pruefer2).WithMany().HasForeignKey(p => p.Pruefer2Id).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Beanstandungen).WithOne().HasForeignKey(b => b.KassenpruefungId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(p => p.Ergebnis).HasMaxLength(2000);
        builder.Ignore(p => p.Abgeschlossen);
        builder.HasIndex(p => new { p.KasseId, p.Bis });
    }
}

/// <summary>Beanstandungen der Kassenprüfung.</summary>
internal sealed class BeanstandungKonfiguration : IEntityTypeConfiguration<Beanstandung>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Beanstandung> builder)
    {
        builder.ToTable("beanstandungen");
        builder.HasOne(b => b.Buchung).WithMany().HasForeignKey(b => b.BuchungId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(b => b.Text).HasMaxLength(2000);
        builder.Property(b => b.Antwort).HasMaxLength(2000);
        builder.HasIndex(b => b.BuchungId);
    }
}

/// <summary>Spenden- und Sponsoringregister (V08).</summary>
internal sealed class ZuwendungKonfiguration : IEntityTypeConfiguration<Zuwendung>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Zuwendung> builder)
    {
        builder.ToTable("zuwendungen");
        builder.HasOne(z => z.Kasse).WithMany().HasForeignKey(z => z.KasseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(z => z.Buchung).WithMany().HasForeignKey(z => z.BuchungId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(z => z.Geber).HasMaxLength(200);
        builder.Property(z => z.Anschrift).HasMaxLength(500);
        builder.Property(z => z.Betrag).HasPrecision(12, 2);
        builder.Property(z => z.Sache).HasMaxLength(500);
        builder.Property(z => z.Zweck).HasMaxLength(500);
        builder.Property(z => z.AnnahmeVerweis).HasMaxLength(200);
        builder.Property(z => z.Bemerkung).HasMaxLength(1000);
        builder.Ignore(z => z.SteuerbereichFalsch);
        builder.HasIndex(z => new { z.KasseId, z.Eingang });
        builder.HasIndex(z => z.BuchungId);
    }
}
