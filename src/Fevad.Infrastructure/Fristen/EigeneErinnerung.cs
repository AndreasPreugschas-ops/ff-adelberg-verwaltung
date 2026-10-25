using System.Globalization;
using System.Text;
using Fevad.Domain.Gemeinsam;
using Fevad.Infrastructure.Dienstbetrieb;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Kleiderkammer;
using Fevad.Infrastructure.Mail;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Fristen;

/// <summary>
/// Erinnert jedes Mitglied mit Konto selbst an seine Fristen, an fällige Prüfungen seiner Schutzausrüstung und an offene
/// Rückmeldungen (V38), nicht nur die Funktionsträger. Jede Art lässt sich im Konto abschalten; während einer gemeldeten
/// Abwesenheit kommt nichts. Läuft im selben Takt wie <see cref="FristErinnerung"/>.
/// </summary>
public sealed class EigeneErinnerung(
    AppDbContext db,
    Personalfristenabfrage personalfristen,
    Kleiderkammerstand kleiderkammer,
    Dienstplanung planung,
    ParameterLeser parameter,
    IMailVersand mail,
    ILogger<EigeneErinnerung> log)
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Was eine Mail an ein Mitglied enthält.</summary>
    /// <param name="Fristen">Eigene Fristen, die gelb oder rot sind: Bezeichnung, Fälligkeit, Ampel.</param>
    /// <param name="Ausstattung">Fällige Prüfungen oder Aussonderungen der eigenen Schutzausrüstung.</param>
    /// <param name="Rueckmeldungen">Termine mit offener Rückmeldung: Titel und Beginn in UTC.</param>
    public sealed record Inhalt(
        IReadOnlyList<(string Bezeichnung, DateOnly? FaelligAm, Fristampel Ampel)> Fristen,
        IReadOnlyList<(string Bezeichnung, DateOnly? FaelligAm, Fristampel Ampel)> Ausstattung,
        IReadOnlyList<(string Titel, DateTimeOffset Beginn)> Rueckmeldungen)
    {
        /// <summary>Es gibt nichts zu erinnern.</summary>
        public bool Leer => Fristen.Count == 0 && Ausstattung.Count == 0 && Rueckmeldungen.Count == 0;
    }

    /// <summary>Verschickt die Erinnerungen an alle Mitglieder, bei denen etwas ansteht.</summary>
    /// <param name="jetzt">Zeitpunkt des Laufs in UTC.</param>
    /// <param name="heute">Heutiger Tag in Ortszeit.</param>
    /// <param name="adresse">Öffentliche Adresse von FEVAD für den Link, leer ohne.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Anzahl der verschickten Mails.</returns>
    public async Task<int> SendenAsync(DateTimeOffset jetzt, DateOnly heute, string? adresse, CancellationToken ct)
    {
        var konten = await db.Users.AsNoTracking()
            .Where(u => u.MitgliedId != null && u.DeaktiviertAm == null && u.Email != null
                && (!u.OhneErinnerungFristen || !u.OhneErinnerungAusstattung || !u.OhneErinnerungRueckmeldungen))
            .ToListAsync(ct);
        if (konten.Count == 0)
        {
            return 0;
        }

        // Wer gerade eine Abwesenheit gemeldet hat, bekommt keine Erinnerung („Ruhe im Urlaub“).
        var abwesend = (await db.Abwesenheiten.Where(a => a.Von <= heute && a.Bis >= heute).Select(a => a.MitgliedId).ToListAsync(ct)).ToHashSet();
        konten = konten.Where(k => !abwesend.Contains(k.MitgliedId!.Value)).ToList();

        var fristen = konten.Any(k => !k.OhneErinnerungFristen)
            ? (await personalfristen.LadenAsync(heute, ct: ct)).Where(f => f.Ampel != Fristampel.Gruen).ToLookup(f => f.MitgliedId)
            : null;
        var psa = konten.Any(k => !k.OhneErinnerungAusstattung)
            ? (await kleiderkammer.PsaAsync(heute, ct)).Where(p => p.MitgliedId is not null).ToLookup(p => p.MitgliedId!.Value)
            : null;
        var warnungTage = await parameter.ZahlAsync(ParameterSchluessel.FristWarnungTage, heute, 90, ct);
        var vorschau = await parameter.ZahlAsync(ParameterSchluessel.StartseiteVorschauTage, heute, 28, ct);
        var schluss = await planung.RueckmeldungSchlussStundenAsync(ct);
        var termine = konten.Any(k => !k.OhneErinnerungRueckmeldungen)
            ? (await planung.TermineAsync(jetzt, jetzt.AddDays(vorschau), null, ct)).Where(d => d.RueckmeldungMoeglich(jetzt, schluss)).ToList()
            : [];
        var terminIds = termine.Select(d => d.Id).ToList();
        var gemeldet = (await db.Rueckmeldungen.Where(r => terminIds.Contains(r.DienstId)).Select(r => new { r.DienstId, r.MitgliedId }).ToListAsync(ct))
            .Select(r => (r.DienstId, r.MitgliedId)).ToHashSet();

        var gesendet = 0;
        foreach (var konto in konten)
        {
            var mitgliedId = konto.MitgliedId!.Value;
            var rueckmeldungen = new List<(string, DateTimeOffset)>();
            if (!konto.OhneErinnerungRueckmeldungen && termine.Count > 0)
            {
                var profil = await planung.ProfilAsync(mitgliedId, ct);
                rueckmeldungen.AddRange(termine
                    .Where(d => !gemeldet.Contains((d.Id, mitgliedId)) && profil.Betrifft(d, Dienstplanung.LokalesDatum(d.Beginn)))
                    .Select(d => (d.Titel, d.Beginn)));
            }

            var inhalt = new Inhalt(
                fristen is null || konto.OhneErinnerungFristen ? []
                    : fristen[mitgliedId].OrderByDescending(f => f.Ampel).ThenBy(f => f.FaelligAm).Select(f => (f.Bezeichnung, f.FaelligAm, f.Ampel)).ToList(),
                psa is null || konto.OhneErinnerungAusstattung ? [] : Ausstattung(psa[mitgliedId], heute, warnungTage),
                rueckmeldungen);
            if (inhalt.Leer)
            {
                continue;
            }

            try
            {
                await mail.SendenAsync(konto.Email!, Betreff(inhalt), Text(inhalt, heute, adresse), ct);
                gesendet++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Eigene Erinnerung an Konto {Konto} fehlgeschlagen.", konto.Id);
            }
        }

        return gesendet;
    }

    private static List<(string, DateOnly?, Fristampel)> Ausstattung(IEnumerable<PsaStand> stuecke, DateOnly heute, int warnungTage) =>
        stuecke.SelectMany(p => p.Stand.Faelligkeiten
                .Select(f => ($"{p.Artikel} {p.Stand.Nummer}: Prüfung", f.FaelligAm, f.Ampel))
                .Append(($"{p.Artikel} {p.Stand.Nummer}: Ende der Lebensdauer", p.Stand.AussonderungFaellig,
                    Frist.Bewerten(p.Stand.AussonderungFaellig, heute, warnungTage)))
                .Where(x => x.Item3 != Fristampel.Gruen))
            .ToList();

    /// <summary>Betreff der Mail.</summary>
    /// <param name="inhalt">Inhalt der Mail.</param>
    /// <returns>Der Betreff.</returns>
    public static string Betreff(Inhalt inhalt)
    {
        var teile = new List<string>();
        if (inhalt.Fristen.Count + inhalt.Ausstattung.Count > 0)
        {
            teile.Add($"{inhalt.Fristen.Count + inhalt.Ausstattung.Count} Frist(en)");
        }

        if (inhalt.Rueckmeldungen.Count > 0)
        {
            teile.Add($"{inhalt.Rueckmeldungen.Count} offene Rückmeldung(en)");
        }

        return "FEVAD: " + string.Join(", ", teile);
    }

    /// <summary>Text der Mail; Zeiten in Ortszeit und 24-Stunden-Format.</summary>
    /// <param name="inhalt">Inhalt der Mail.</param>
    /// <param name="heute">Heutiger Tag.</param>
    /// <param name="adresse">Öffentliche Adresse von FEVAD, leer ohne.</param>
    /// <returns>Der Text.</returns>
    public static string Text(Inhalt inhalt, DateOnly heute, string? adresse)
    {
        var text = new StringBuilder();
        text.AppendLine($"Stand {heute.ToString("dd.MM.yyyy", Deutsch)}.");
        Abschnitt("DEINE FRISTEN", inhalt.Fristen);
        Abschnitt("DEINE SCHUTZAUSRÜSTUNG (bitte in der Kleiderkammer vorbeibringen)", inhalt.Ausstattung);
        if (inhalt.Rueckmeldungen.Count > 0)
        {
            text.AppendLine().AppendLine("BITTE ZU- ODER ABSAGEN");
            foreach (var (titel, beginn) in inhalt.Rueckmeldungen)
            {
                text.AppendLine($"- {TimeZoneInfo.ConvertTime(beginn, Ortszeit.Berlin).ToString("ddd dd.MM.yyyy, HH:mm", Deutsch)} Uhr: {titel}");
            }
        }

        if (!string.IsNullOrEmpty(adresse))
        {
            text.AppendLine().AppendLine($"Meine Termine: {adresse}/Termine").AppendLine($"Meine Akte: {adresse}/Konto/Akte");
        }

        text.AppendLine().AppendLine("Diese Mail kommt automatisch. Abbestellen kannst du sie je Art unter „Mein Konto“. Während einer gemeldeten Abwesenheit kommt keine.");
        return text.ToString();

        void Abschnitt(string titel, IReadOnlyList<(string Bezeichnung, DateOnly? FaelligAm, Fristampel Ampel)> eintraege)
        {
            if (eintraege.Count == 0)
            {
                return;
            }

            text.AppendLine().AppendLine(titel);
            foreach (var (bezeichnung, faellig, ampel) in eintraege)
            {
                var stand = faellig is { } d ? (ampel == Fristampel.Rot ? "abgelaufen seit " : "fällig am ") + d.ToString("dd.MM.yyyy", Deutsch) : "kein Nachweis";
                text.AppendLine($"- {bezeichnung}: {stand}");
            }
        }
    }
}
