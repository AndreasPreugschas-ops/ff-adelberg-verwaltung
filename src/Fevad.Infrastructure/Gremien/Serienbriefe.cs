using System.Globalization;
using Fevad.Domain.Gremien;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Mail;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Gremien;

/// <summary>
/// Serienbriefe und Serienmails aus Briefvorlagen (V56): Empfänger je Abteilung, Platzhalter je Person gefüllt, als PDF, Word-Datei oder eine Mail
/// je Empfänger. Welche Mitglieder in Frage kommen, gibt der Aufrufer vor, damit die Abschirmung der Jugendfeuerwehr gilt.
/// </summary>
/// <param name="mail">Mailversand.</param>
public sealed class Serienbriefe(IMailVersand mail)
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Ein Empfänger mit den Werten seiner Platzhalter.</summary>
    /// <param name="MitgliedId">Kennung des Mitglieds (beim Elternbrief des Kindes).</param>
    /// <param name="Name">„Nachname, Vorname“ des Empfängers.</param>
    /// <param name="Anschrift">Anschriftzeilen für das Fenster des Briefs.</param>
    /// <param name="Email">E-Mail-Adresse; leer, wenn das Schreiben auf Papier gehen muss.</param>
    /// <param name="Werte">Werte der personenbezogenen Platzhalter.</param>
    public sealed record Empfaenger(Guid MitgliedId, string Name, IReadOnlyList<string> Anschrift, string? Email, IReadOnlyDictionary<string, string?> Werte);

    /// <summary>Ein gefülltes Schreiben.</summary>
    /// <param name="An">Der Empfänger.</param>
    /// <param name="Betreff">Betreff oder Überschrift.</param>
    /// <param name="Text">Gefüllter Text.</param>
    public sealed record Schreiben(Empfaenger An, string Betreff, string Text);

    /// <summary>Ergebnis des Mailversands.</summary>
    /// <param name="Gesendet">Zahl der versandten Mails.</param>
    /// <param name="OhneMail">Empfänger ohne E-Mail-Adresse; sie brauchen das Schreiben auf Papier.</param>
    /// <param name="Fehler">Adressen, an die der Versand scheiterte.</param>
    public sealed record Versand(int Gesendet, IReadOnlyList<string> OhneMail, IReadOnlyList<string> Fehler);

    /// <summary>Der Mailversand ist eingerichtet.</summary>
    public bool MailAktiv => mail.Aktiv;

    /// <summary>Empfänger: Mitglieder, die am Stichtag einer Abteilung angehören, oder deren Erziehungsberechtigte.</summary>
    /// <param name="sichtbar">Die Mitglieder, die der Benutzer sehen darf.</param>
    /// <param name="art">Mitglieder oder Erziehungsberechtigte.</param>
    /// <param name="abteilungId">Nur diese Abteilung; <c>null</c> für alle.</param>
    /// <param name="stichtag">Stichtag der Mitgliedschaft und des Dienstgrads.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Empfänger nach Namen.</returns>
    public async Task<IReadOnlyList<Empfaenger>> EmpfaengerAsync(
        IQueryable<Mitglied> sichtbar, VorlageEmpfaenger art, Guid? abteilungId, DateOnly stichtag, CancellationToken ct = default)
    {
        var mitglieder = await sichtbar.AsNoTracking()
            .Where(m => m.AnonymisiertAm == null && m.Mitgliedschaften.Any(s => s.Status != MitgliedStatus.Ausgeschieden
                && s.GueltigAb <= stichtag && (s.GueltigBis == null || s.GueltigBis >= stichtag) && (abteilungId == null || s.AbteilungId == abteilungId)))
            .OrderBy(m => m.Nachname).ThenBy(m => m.Vorname)
            .Select(m => new
            {
                m.Id, m.Vorname, m.Nachname, m.Titel, m.Geschlecht, m.Strasse, m.Plz, m.Ort, m.Email,
                Dienstgrad = m.Dienstgrade.Where(d => d.Ab <= stichtag).OrderByDescending(d => d.Ab).Select(d => d.Dienstgrad!.Bezeichnung).FirstOrDefault(),
                Eltern = m.Erziehungsberechtigte.OrderBy(e => e.Nummer)
                    .Select(e => new { e.Vorname, e.Nachname, e.Verhaeltnis, e.Strasse, e.Plz, e.Ort, e.Email }).ToList(),
            })
            .ToListAsync(ct);

        if (art == VorlageEmpfaenger.Mitglieder)
        {
            return mitglieder.Select(m => Neu(m.Id, m.Vorname, m.Nachname, m.Strasse, m.Plz, m.Ort, m.Email,
                Platzhalter.Briefanrede(m.Geschlecht, m.Titel, m.Vorname, m.Nachname), m.Dienstgrad, null)).ToList();
        }

        // Elternbrief: je Erziehungsberechtigtem ein Schreiben; fehlt die eigene Anschrift, gilt die des Kindes.
        return mitglieder.SelectMany(m => m.Eltern.Select(e =>
        {
            var geschlecht = e.Verhaeltnis switch { Sorgeverhaeltnis.Mutter => Geschlecht.Weiblich, Sorgeverhaeltnis.Vater => Geschlecht.Maennlich, _ => (Geschlecht?)null };
            var eigene = !string.IsNullOrWhiteSpace(e.Strasse);
            return Neu(m.Id, e.Vorname, e.Nachname, eigene ? e.Strasse : m.Strasse, eigene ? e.Plz : m.Plz, eigene ? e.Ort : m.Ort, e.Email,
                Platzhalter.Briefanrede(geschlecht, null, e.Vorname, e.Nachname), null, $"{m.Vorname} {m.Nachname}");
        })).ToList();
    }

    /// <summary>Füllt die Vorlage für jeden Empfänger.</summary>
    /// <param name="vorlage">Die Vorlage.</param>
    /// <param name="empfaenger">Die Empfänger.</param>
    /// <param name="feuerwehr">Name der Feuerwehr.</param>
    /// <param name="datum">Datum des Schreibens.</param>
    /// <returns>Die Schreiben.</returns>
    public static IReadOnlyList<Schreiben> Fuellen(Briefvorlage vorlage, IEnumerable<Empfaenger> empfaenger, string feuerwehr, DateOnly datum) =>
        empfaenger.Select(e =>
        {
            var werte = new Dictionary<string, string?>(e.Werte)
            {
                ["Datum"] = datum.ToString("dd.MM.yyyy", Deutsch),
                ["Jahr"] = datum.Year.ToString(Deutsch),
                ["Feuerwehr"] = feuerwehr,
            };
            return new Schreiben(e, Platzhalter.Ersetzen(vorlage.Betreff, werte).Trim(), Platzhalter.Ersetzen(vorlage.Text, werte).Replace("\r\n", "\n").Trim());
        }).ToList();

    /// <summary>Sendet jedem Empfänger mit Adresse eine eigene Mail, damit niemand die Adressen der anderen sieht.</summary>
    /// <param name="schreiben">Die gefüllten Schreiben.</param>
    /// <param name="feuerwehr">Name der Feuerwehr als Absender unter dem Text.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<Versand> MailsSendenAsync(IReadOnlyList<Schreiben> schreiben, string feuerwehr, CancellationToken ct = default)
    {
        var gesendet = 0;
        var ohne = new List<string>();
        var fehler = new List<string>();
        foreach (var s in schreiben)
        {
            if (string.IsNullOrWhiteSpace(s.An.Email))
            {
                ohne.Add(s.An.Name);
                continue;
            }

            try
            {
                await mail.SendenAsync(s.An.Email.Trim(), s.Betreff, $"{s.Text}{Environment.NewLine}{feuerwehr}", ct);
                gesendet++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                fehler.Add(s.An.Email.Trim());
            }
        }

        return new Versand(gesendet, ohne, fehler);
    }

    private static Empfaenger Neu(
        Guid mitgliedId, string vorname, string nachname, string? strasse, string? plz, string? ort, string? email, string anrede, string? dienstgrad, string? kind)
    {
        var plzOrt = string.Join(' ', new[] { plz, ort }.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim()));
        var anschrift = new[] { $"{vorname} {nachname}", strasse?.Trim(), plzOrt }.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).ToList();
        var werte = new Dictionary<string, string?>
        {
            ["Briefanrede"] = anrede,
            ["Vorname"] = vorname,
            ["Nachname"] = nachname,
            ["Dienstgrad"] = dienstgrad ?? "",
            ["Strasse"] = strasse?.Trim() ?? "",
            ["PlzOrt"] = plzOrt,
            ["Kind"] = kind ?? "",
        };
        return new Empfaenger(mitgliedId, $"{nachname}, {vorname}", anschrift, email, werte);
    }
}
