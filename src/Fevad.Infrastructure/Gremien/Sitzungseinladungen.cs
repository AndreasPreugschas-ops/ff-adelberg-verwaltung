using System.Globalization;
using System.Text;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Gremien;

/// <summary>
/// Einladungen zu Ausschuss und Hauptversammlung (V55): Verteiler aus Satzung und Funktionen, Versand per E-Mail mit Nachweis,
/// Mail-Entwurf an den Bürgermeister (F20) und Bausteine der Tagesordnung aus fälligen Vorgängen.
/// </summary>
/// <param name="db">Datenbankkontext.</param>
/// <param name="mail">Mailversand.</param>
/// <param name="parameter">Parameter (Ausschussfunktionen).</param>
public sealed class Sitzungseinladungen(AppDbContext db, IMailVersand mail, ParameterLeser parameter)
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Ein Eingeladener.</summary>
    /// <param name="MitgliedId">Kennung des Mitglieds.</param>
    /// <param name="Name">„Nachname, Vorname“.</param>
    /// <param name="Email">E-Mail-Adresse; leer, wenn die Einladung auf Papier gehen muss.</param>
    public sealed record Empfaenger(Guid MitgliedId, string Name, string? Email);

    /// <summary>Entwurf einer Mail.</summary>
    /// <param name="An">Empfängeradresse; leer, wenn in den Stammdaten keine hinterlegt ist.</param>
    /// <param name="Betreff">Betreff.</param>
    /// <param name="Text">Inhalt.</param>
    public sealed record Entwurf(string? An, string Betreff, string Text);

    /// <summary>Ergebnis des Versands.</summary>
    /// <param name="Gesendet">Zahl der versandten Mails.</param>
    /// <param name="OhneMail">Namen der Eingeladenen ohne E-Mail-Adresse.</param>
    /// <param name="Fehler">Adressen, an die der Versand scheiterte.</param>
    public sealed record Versand(int Gesendet, IReadOnlyList<string> OhneMail, IReadOnlyList<string> Fehler);

    /// <summary>Der Mailversand ist eingerichtet.</summary>
    public bool MailAktiv => mail.Aktiv;

    /// <summary>
    /// Verteiler einer Sitzung: beim Ausschuss die Inhaber der Ausschussfunktionen (Parameter, Satzung § 13), bei der Hauptversammlung alle
    /// Angehörigen der Gemeindefeuerwehr ohne Jugend- und Kinderfeuerwehr (§ 14 Abs. 3), sonst die erfassten Teilnehmer.
    /// </summary>
    /// <param name="sitzung">Die Sitzung mit Teilnahmen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Eingeladene, nach Namen sortiert.</returns>
    public async Task<IReadOnlyList<Empfaenger>> VerteilerAsync(Sitzung sitzung, CancellationToken ct = default)
    {
        var tag = Gremienarbeit.Sitzungstag(sitzung);
        IQueryable<Mitglied> abfrage;
        if (sitzung.Art == SitzungArt.Ausschuss)
        {
            var codes = (await parameter.WertAsync(ParameterSchluessel.AusschussFunktionen, tag, ct) ?? "KDT|STV|JW|SF|KAS|AUS")
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            abfrage = db.Mitglieder.Where(m => db.FunktionZuordnungen.Any(f => f.MitgliedId == m.Id && codes.Contains(f.Funktion!.Code)
                && f.GueltigAb <= tag && (f.GueltigBis == null || f.GueltigBis >= tag)));
        }
        else if (sitzung.Art == SitzungArt.Hauptversammlung)
        {
            abfrage = db.Mitglieder.Where(m => m.Mitgliedschaften.Any(s => s.Abteilung!.Art != AbteilungArt.Jugend && s.Abteilung.Art != AbteilungArt.Kinder
                && s.Status != MitgliedStatus.Ausgeschieden && s.GueltigAb <= tag && (s.GueltigBis == null || s.GueltigBis >= tag)));
        }
        else
        {
            var ids = sitzung.Teilnahmen.Select(t => t.MitgliedId).ToList();
            abfrage = db.Mitglieder.Where(m => ids.Contains(m.Id));
        }

        return await abfrage.AsNoTracking()
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new Empfaenger(m.Id, m.Nachname + ", " + m.Vorname, m.Email))
            .ToListAsync(ct);
    }

    /// <summary>Text der Einladung mit Zeit, Ort und Tagesordnung.</summary>
    /// <param name="sitzung">Die Sitzung.</param>
    /// <param name="feuerwehr">Name der Feuerwehr.</param>
    /// <returns>Betreff und Text.</returns>
    public static (string Betreff, string Text) Einladungstext(Sitzung sitzung, string feuerwehr)
    {
        var beginn = TimeZoneInfo.ConvertTime(sitzung.Beginn, Ortszeit.Berlin);
        var text = new StringBuilder();
        text.AppendLine($"Einladung: {sitzung.Titel}");
        text.AppendLine();
        text.AppendLine($"Termin: {beginn.ToString("dddd, dd.MM.yyyy", Deutsch)}, {beginn:HH\\:mm} Uhr");
        if (!string.IsNullOrWhiteSpace(sitzung.Ort))
        {
            text.AppendLine($"Ort: {sitzung.Ort}");
        }

        text.AppendLine();
        text.AppendLine("Tagesordnung:");
        text.AppendLine(string.IsNullOrWhiteSpace(sitzung.Tagesordnung) ? "(folgt)" : sitzung.Tagesordnung.Trim());
        text.AppendLine();
        text.AppendLine($"Mit kameradschaftlichen Grüßen");
        text.AppendLine(feuerwehr);
        return ($"Einladung {sitzung.Titel} am {beginn:dd.MM.yyyy}", text.ToString());
    }

    /// <summary>Mail-Entwurf an den Bürgermeister (F20; Satzung § 13 Abs. 5, § 14 Abs. 3).</summary>
    /// <param name="sitzung">Die Sitzung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Entwurf mit der Adresse aus den Stammdaten.</returns>
    public async Task<Entwurf> BuergermeisterEntwurfAsync(Sitzung sitzung, CancellationToken ct = default)
    {
        var org = await db.Organisationen.AsNoTracking().FirstOrDefaultAsync(ct);
        var feuerwehr = org?.FeuerwehrName ?? "Freiwillige Feuerwehr";
        var (betreff, einladung) = Einladungstext(sitzung, feuerwehr);
        var anrede = string.IsNullOrWhiteSpace(org?.BuergermeisterName) ? "Sehr geehrte Damen und Herren," : $"Guten Tag {org.BuergermeisterName.Trim()},";
        var grundlage = sitzung.Art == SitzungArt.Hauptversammlung ? "§ 14 Abs. 3" : "§ 13 Abs. 5";
        var text = $"{anrede}{Environment.NewLine}{Environment.NewLine}nach {grundlage} der Feuerwehrsatzung erhalten Sie die Einladung mit Tagesordnung. "
            + $"Sie sind herzlich eingeladen, teilzunehmen oder sich vertreten zu lassen.{Environment.NewLine}{Environment.NewLine}{einladung}";
        return new Entwurf(org?.BuergermeisterEmail, betreff, text);
    }

    /// <summary>Verschickt die Einladung an alle Eingeladenen mit E-Mail-Adresse und vermerkt den Versand als Nachweis an der Sitzung.</summary>
    /// <param name="sitzungId">Kennung der Sitzung.</param>
    /// <param name="jetzt">Zeitpunkt des Versands.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das Ergebnis; ohne eingerichteten Mailversand wird nichts verschickt.</returns>
    public async Task<Versand> EinladungVersendenAsync(Guid sitzungId, DateTimeOffset jetzt, CancellationToken ct = default)
    {
        var sitzung = await db.Sitzungen.Include(s => s.Teilnahmen).FirstAsync(s => s.Id == sitzungId, ct);
        var verteiler = await VerteilerAsync(sitzung, ct);
        var ohne = verteiler.Where(e => string.IsNullOrWhiteSpace(e.Email)).Select(e => e.Name).ToList();
        if (!mail.Aktiv)
        {
            return new Versand(0, ohne, []);
        }

        var feuerwehr = await db.Organisationen.Select(o => o.FeuerwehrName).FirstOrDefaultAsync(ct) ?? "Freiwillige Feuerwehr";
        var (betreff, text) = Einladungstext(sitzung, feuerwehr);
        var gesendet = 0;
        var fehler = new List<string>();
        foreach (var e in verteiler.Where(e => !string.IsNullOrWhiteSpace(e.Email)))
        {
            try
            {
                await mail.SendenAsync(e.Email!, betreff, text, ct);
                gesendet++;
            }
            catch (Exception ex) when (ex is System.Net.Mail.SmtpException or InvalidOperationException or FormatException)
            {
                fehler.Add(e.Name);
            }
        }

        sitzung.EinladungVersandtAm = jetzt;
        sitzung.EinladungEmpfaenger = gesendet;
        sitzung.EinladungOhneMail = ohne.Count;
        sitzung.EingeladenAm ??= Ortszeit.Datum(jetzt);
        await db.SaveChangesAsync(ct);
        return new Versand(gesendet, ohne, fehler);
    }

    /// <summary>Sendet den (ggf. geänderten) Entwurf an den Bürgermeister und vermerkt den Versand.</summary>
    /// <param name="sitzungId">Kennung der Sitzung.</param>
    /// <param name="entwurf">Adresse, Betreff und Text.</param>
    /// <param name="jetzt">Zeitpunkt des Versands.</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task BuergermeisterSendenAsync(Guid sitzungId, Entwurf entwurf, DateTimeOffset jetzt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entwurf.An);
        await mail.SendenAsync(entwurf.An, entwurf.Betreff, entwurf.Text, ct);
        var sitzung = await db.Sitzungen.FirstAsync(s => s.Id == sitzungId, ct);
        sitzung.BuergermeisterEingeladenAm = jetzt;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Bausteine der Tagesordnung: feste Punkte je Sitzungsart (Satzung § 14 Abs. 2) und fällige Vorgänge (Beschlüsse in der Wiedervorlage,
    /// anstehende Wahlen).
    /// </summary>
    /// <param name="art">Art der Sitzung.</param>
    /// <param name="stichtag">Tag der Sitzung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Vorschläge, je einer pro Zeile.</returns>
    public async Task<IReadOnlyList<string>> BausteineAsync(SitzungArt art, DateOnly stichtag, CancellationToken ct = default)
    {
        var bausteine = new List<string> { "Begrüßung und Feststellung der Beschlussfähigkeit", "Genehmigung der Niederschrift der letzten Sitzung" };
        if (art == SitzungArt.Hauptversammlung)
        {
            bausteine.AddRange([
                "Bericht des Feuerwehrkommandanten über das vergangene Jahr", "Bericht des Jugendfeuerwehrwarts",
                "Bericht des Kassenverwalters über den Rechnungsabschluss", "Bericht der Kassenprüfer", "Beschluss über den Rechnungsabschluss und Entlastung",
                "Ehrungen und Beförderungen", "Grußworte",
            ]);
        }

        var wiedervorlage = await db.Beschluesse.AsNoTracking()
            .Where(b => b.Ergebnis == BeschlussErgebnis.Angenommen && b.ErledigtAm == null && b.AufgehobenAm == null && b.UmsetzenBis != null)
            .OrderBy(b => b.UmsetzenBis)
            .Select(b => new { b.Jahr, b.Nummer, b.Gegenstand })
            .Take(10)
            .ToListAsync(ct);
        bausteine.AddRange(wiedervorlage.Select(b => $"Sachstand Beschluss {b.Jahr}/{b.Nummer:00}: {Kurz(b.Gegenstand)}"));

        var vertagt = await db.Beschluesse.AsNoTracking().Where(b => b.Ergebnis == BeschlussErgebnis.Vertagt)
            .OrderByDescending(b => b.Jahr).ThenByDescending(b => b.Nummer).Select(b => b.Gegenstand).Take(5).ToListAsync(ct);
        bausteine.AddRange(vertagt.Select(g => $"Vertagt: {Kurz(g)}"));

        var bis = stichtag.AddDays(120);
        var amtsenden = await db.FunktionZuordnungen.AsNoTracking()
            .Where(f => f.Funktion!.MitWahl && f.GueltigBis != null && f.GueltigBis >= stichtag && f.GueltigBis <= bis)
            .Select(f => f.Funktion!.Bezeichnung).Distinct().ToListAsync(ct);
        bausteine.AddRange(amtsenden.Select(f => $"Wahl: {f}"));

        bausteine.Add("Verschiedenes");
        return bausteine;
    }

    /// <summary>Legt zur Hauptversammlung einen Dienst im Kalender an, damit die Mitglieder zu- oder absagen (V55).</summary>
    /// <param name="sitzungId">Kennung der Sitzung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Kennung des Dienstes.</returns>
    public async Task<Guid> DienstAnlegenAsync(Guid sitzungId, CancellationToken ct = default)
    {
        var sitzung = await db.Sitzungen.FirstAsync(s => s.Id == sitzungId, ct);
        if (sitzung.DienstId is { } vorhanden && await db.Dienste.AnyAsync(d => d.Id == vorhanden, ct))
        {
            return vorhanden;
        }

        var dienst = new Dienst
        {
            Titel = sitzung.Titel,
            Art = sitzung.Art == SitzungArt.Hauptversammlung ? DienstArt.Hauptversammlung : DienstArt.Sitzung,
            Beginn = sitzung.Beginn,
            Ende = sitzung.Ende ?? sitzung.Beginn.AddHours(2),
            Ort = sitzung.Ort,
            Status = DienstStatus.Geplant,
            AlsDienst = sitzung.Art == SitzungArt.Hauptversammlung,
            RueckmeldungErbeten = true,
        };
        db.Dienste.Add(dienst);
        sitzung.DienstId = dienst.Id;
        await db.SaveChangesAsync(ct);
        return dienst.Id;
    }

    private static string Kurz(string text) => text.Length <= 80 ? text : text[..77] + "…";
}
