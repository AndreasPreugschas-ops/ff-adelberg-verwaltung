using System.Globalization;
using System.Text;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Auswertungen;

/// <summary>
/// Stichtagsnachweis der Einsatzabteilung zum 31.12. (V28): Angehörige mit Eintrittsdatum und Abteilung für die
/// Pauschalförderung des Landes und für Verbandsbeiträge, dazu die Zuwendungen der Gemeinde an die Kameradschaftskasse
/// nach Entschädigungssatzung § 5.
/// </summary>
public sealed class Stichtagsliste(AppDbContext db, ParameterLeser parameter)
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Ein Angehöriger der Einsatzabteilung am Stichtag.</summary>
    /// <param name="Personalnummer">Personalnummer.</param>
    /// <param name="Nachname">Nachname.</param>
    /// <param name="Vorname">Vorname.</param>
    /// <param name="Abteilung">Einsatzabteilung am Stichtag.</param>
    /// <param name="Status">Status am Stichtag (Probezeit, aktiv, befreit).</param>
    /// <param name="EintrittEinsatzabteilung">Beginn der ununterbrochenen Zugehörigkeit zur Einsatzabteilung bis zum Stichtag.</param>
    public sealed record Angehoeriger(string Personalnummer, string Nachname, string Vorname, string Abteilung, MitgliedStatus Status, DateOnly EintrittEinsatzabteilung);

    /// <summary>Ein Betrag mit Rechenweg.</summary>
    /// <param name="Bezeichnung">Wofür, z. B. „Pauschalförderung des Landes“.</param>
    /// <param name="Rechnung">Rechenweg, z. B. „42 × 90,00 €“.</param>
    /// <param name="Euro">Betrag in Euro.</param>
    /// <param name="Grundlage">Rechtsgrundlage bzw. Quelle des Satzes.</param>
    public sealed record Betrag(string Bezeichnung, string Rechnung, decimal Euro, string Grundlage);

    /// <summary>Der ganze Nachweis.</summary>
    /// <param name="Stichtag">Stichtag, der 31.12. des Jahres.</param>
    /// <param name="Angehoerige">Angehörige, nach Abteilung und Name sortiert.</param>
    /// <param name="Betraege">Pauschalförderung und Zuwendungen an die Kameradschaftskasse.</param>
    public sealed record Nachweis(DateOnly Stichtag, IReadOnlyList<Angehoeriger> Angehoerige, IReadOnlyList<Betrag> Betraege);

    /// <summary>Erstellt den Nachweis zum 31.12. des Jahres.</summary>
    public async Task<Nachweis> ErstellenAsync(int jahr, CancellationToken ct = default)
    {
        var stichtag = new DateOnly(jahr, 12, 31);
        var einsatz = await db.Mitgliedschaften.AsNoTracking()
            .Where(m => m.Abteilung!.Art == AbteilungArt.Einsatz && !m.Vordienstzeit)
            .Select(m => new { m.MitgliedId, m.GueltigAb, m.GueltigBis, m.Status, Abteilung = m.Abteilung!.Bezeichnung, Sortierung = m.Abteilung.Sortierung })
            .ToListAsync(ct);

        var amStichtag = einsatz
            .Where(m => m.GueltigAb <= stichtag && (m.GueltigBis == null || m.GueltigBis >= stichtag) && m.Status != MitgliedStatus.Ausgeschieden)
            .GroupBy(m => m.MitgliedId)
            .Select(g => g.OrderByDescending(m => m.GueltigAb).First())
            .ToList();

        var ids = amStichtag.Select(m => m.MitgliedId).ToList();
        var namen = await db.Mitglieder.AsNoTracking().Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => new { m.Personalnummer, m.Nachname, m.Vorname }, ct);

        // Gelöschte Mitglieder (Papierkorb) zählen nicht, auch wenn ihre Mitgliedschaften noch da sind.
        var angehoerige = amStichtag
            .Where(m => namen.ContainsKey(m.MitgliedId))
            .Select(m =>
            {
                // Lückenlos aneinander anschließende Zeiten in Einsatzabteilungen gelten als eine Zugehörigkeit (z. B. Wechsel der Abteilung).
                var kette = einsatz.Where(e => e.MitgliedId == m.MitgliedId && e.GueltigAb <= stichtag).OrderByDescending(e => e.GueltigAb).ToList();
                var beginn = m.GueltigAb;
                foreach (var e in kette)
                {
                    if (e.GueltigAb < beginn && (e.GueltigBis is null || e.GueltigBis.Value >= beginn.AddDays(-1)))
                    {
                        beginn = e.GueltigAb;
                    }
                }

                var n = namen[m.MitgliedId];
                return (m.Sortierung, Zeile: new Angehoeriger(n.Personalnummer, n.Nachname, n.Vorname, m.Abteilung, m.Status, beginn));
            })
            .OrderBy(x => x.Sortierung).ThenBy(x => x.Zeile.Nachname, StringComparer.CurrentCulture).ThenBy(x => x.Zeile.Vorname, StringComparer.CurrentCulture)
            .Select(x => x.Zeile)
            .ToList();

        var satz = await DezimalAsync(ParameterSchluessel.PauschalfoerderungJeAngehoerigemEuro, stichtag, ct);
        var zuschuss = await DezimalAsync(ParameterSchluessel.KameradschaftskasseZuschussEuro, stichtag, ct);
        var jugend = await DezimalAsync(ParameterSchluessel.KameradschaftskasseZuschussJugendEuro, stichtag, ct);
        var betraege = new List<Betrag>
        {
            new("Pauschalförderung des Landes", $"{angehoerige.Count} × {Euro(satz)}", angehoerige.Count * satz, "VwV Z-Feu 2025–2031, Angehörige der Einsatzabteilung zum 31.12."),
            new("Zuschuss der Gemeinde an die Kameradschaftskasse", "pauschal", zuschuss, "Entschädigungssatzung § 5 Abs. 1"),
            new("Zuschuss der Gemeinde zur Förderung der Jugendfeuerwehr", "pauschal", jugend, "Entschädigungssatzung § 5 Abs. 2"),
        };

        return new Nachweis(stichtag, angehoerige, betraege);
    }

    /// <summary>Liste als CSV (Semikolon, UTF-8 mit BOM), Beträge am Ende.</summary>
    public static byte[] Csv(Nachweis n)
    {
        var csv = new StringBuilder();
        csv.Append("Personalnummer;Nachname;Vorname;Abteilung;Status;Eintritt Einsatzabteilung\r\n");
        foreach (var a in n.Angehoerige)
        {
            csv.Append($"{Feld(a.Personalnummer)};{Feld(a.Nachname)};{Feld(a.Vorname)};{Feld(a.Abteilung)};{StatusText(a.Status)};{a.EintrittEinsatzabteilung:dd.MM.yyyy}\r\n");
        }

        csv.Append("\r\n");
        csv.Append($"Angehörige der Einsatzabteilung am {n.Stichtag:dd.MM.yyyy};{n.Angehoerige.Count}\r\n");
        foreach (var b in n.Betraege)
        {
            csv.Append($"{Feld(b.Bezeichnung)};{b.Euro.ToString("0.00", Deutsch)};{Feld(b.Rechnung)};{Feld(b.Grundlage)}\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    /// <summary>Text eines Mitgliedsstatus.</summary>
    public static string StatusText(MitgliedStatus status) => status switch
    {
        MitgliedStatus.Probezeit => "Probezeit",
        MitgliedStatus.Aktiv => "aktiv",
        MitgliedStatus.Befreit => "befreit",
        _ => status.ToString(),
    };

    /// <summary>Betrag mit zwei Nachkommastellen und Eurozeichen.</summary>
    public static string Euro(decimal betrag) => betrag.ToString("N2", Deutsch) + " €";

    private async Task<decimal> DezimalAsync(string schluessel, DateOnly stichtag, CancellationToken ct) =>
        decimal.TryParse((await parameter.WertAsync(schluessel, stichtag, ct))?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var wert) ? wert : 0m;

    private static string Feld(string wert)
    {
        if (wert.Length > 0 && wert[0] is '=' or '+' or '-' or '@')
        {
            wert = "'" + wert;
        }

        return wert.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{wert.Replace("\"", "\"\"")}\"" : wert;
    }
}
