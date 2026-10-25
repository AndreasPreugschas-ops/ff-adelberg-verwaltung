using Fevad.Domain.Entschaedigung;
using Fevad.Domain.Kassen;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Kassen;

/// <summary>
/// Kontoauszüge einlesen und abgleichen (V07): Umsätze ohne Doppelte übernehmen, Zuordnungen vorschlagen (Referenz aus der
/// Überweisungsdatei, vorhandene Buchung, vorbereitete Ausgabe, wiederkehrende Buchung), zuordnen oder ignorieren, Salden vergleichen.
/// </summary>
public sealed class Bankimport(AppDbContext db)
{
    /// <summary>Vergleich des Bankstands mit dem Kassenbuch.</summary>
    /// <param name="Konto">Das Konto.</param>
    /// <param name="Tag">Tag des letzten Saldos laut Bank.</param>
    /// <param name="Bank">Saldo laut Bank.</param>
    /// <param name="Kassenbuch">Kontostand laut Kassenbuch am selben Tag.</param>
    public sealed record Saldenabgleich(Zahlungsmittelkonto Konto, DateOnly Tag, decimal Bank, decimal Kassenbuch)
    {
        /// <summary>Bank minus Kassenbuch; 0, wenn alles gebucht ist.</summary>
        public decimal Differenz => Bank - Kassenbuch;
    }

    /// <summary>
    /// Liest eine Datei für ein Konto ein. CAMT erkennt FEVAD an der Endung .xml oder .zip, CSV braucht ein Bankformat. Bereits
    /// eingelesene Umsätze (gleicher Fingerabdruck) werden übersprungen.
    /// </summary>
    /// <param name="kontoId">Das Konto.</param>
    /// <param name="inhalt">Dateiinhalt.</param>
    /// <param name="dateiname">Dateiname.</param>
    /// <param name="bankformatId">Bankformat für CSV.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der gespeicherte Import.</returns>
    /// <exception cref="FormatException">Die Datei passt nicht oder lässt sich nicht lesen.</exception>
    public async Task<Kontoauszugimport> EinlesenAsync(Guid kontoId, byte[] inhalt, string dateiname, Guid? bankformatId, CancellationToken ct = default)
    {
        var konto = await db.Zahlungsmittelkonten.AsNoTracking().FirstAsync(k => k.Id == kontoId, ct);
        var endung = Path.GetExtension(dateiname).ToLowerInvariant();
        Auszug auszug;
        Auszugsformat format;
        if (endung is ".xml" or ".zip")
        {
            (auszug, format) = (Kontoauszugleser.Camt(inhalt, dateiname), Auszugsformat.Camt053);
        }
        else if (endung is ".csv" or ".txt")
        {
            var bankformat = bankformatId is { } fid ? await db.Bankformate.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fid, ct) : null;
            (auszug, format) = (Kontoauszugleser.Csv(inhalt, bankformat ?? throw new FormatException("Für CSV bitte das Format der Bank wählen.")), Auszugsformat.Csv);
        }
        else
        {
            throw new FormatException("Nur camt.053 (.xml oder .zip) oder CSV.");
        }

        if (auszug.Iban is { } iban && Iban.Normalisieren(konto.Iban) is { } eigene && iban != eigene)
        {
            throw new FormatException($"Die Datei gehört zum Konto {Iban.Maskieren(iban)}, nicht zu „{konto.Bezeichnung}“.");
        }

        var zaehler = new Dictionary<(DateOnly, decimal, string?, string?), int>();
        var neu = new List<Kontoumsatz>();
        foreach (var z in auszug.Zeilen)
        {
            var schluessel = (z.Buchungstag, z.Betrag, z.Verwendungszweck, z.Referenz);
            var nummer = zaehler[schluessel] = zaehler.GetValueOrDefault(schluessel) + 1;
            neu.Add(new Kontoumsatz
            {
                KontoId = kontoId, Buchungstag = z.Buchungstag, Valuta = z.Valuta, Betrag = z.Betrag, Gegenpartei = Kuerzen(z.Gegenpartei, 200),
                Verwendungszweck = Kuerzen(z.Verwendungszweck, 1000), Referenz = Kuerzen(z.Referenz, 100),
                Fingerabdruck = Bankabgleich.Fingerabdruck(kontoId, z, nummer), Status = UmsatzStatus.Offen,
            });
        }

        var abdruecke = neu.Select(u => u.Fingerabdruck).ToList();
        var vorhanden = (await db.Kontoumsaetze.IgnoreQueryFilters().Where(u => u.KontoId == kontoId && abdruecke.Contains(u.Fingerabdruck))
            .Select(u => u.Fingerabdruck).ToListAsync(ct)).ToHashSet();
        neu = neu.Where(u => !vorhanden.Contains(u.Fingerabdruck)).ToList();

        var import = new Kontoauszugimport
        {
            KontoId = kontoId, Dateiname = Kuerzen(dateiname, 200)!, Format = format, Anzahl = auszug.Zeilen.Count, Neu = neu.Count,
            Von = auszug.Zeilen.Count > 0 ? auszug.Zeilen.Min(z => z.Buchungstag) : null,
            Bis = auszug.Zeilen.Count > 0 ? auszug.Zeilen.Max(z => z.Buchungstag) : null, Endsaldo = auszug.Endsaldo,
        };
        db.Kontoauszugimporte.Add(import);
        foreach (var u in neu)
        {
            u.ImportId = import.Id;
            db.Kontoumsaetze.Add(u);
        }

        await db.SaveChangesAsync(ct);
        return import;
    }

    /// <summary>
    /// Vorschläge für offene Umsätze einer Kasse: zuerst die Referenz aus einer FEVAD-Überweisungsdatei, dann eine vorhandene Buchung
    /// mit gleicher Wirkung, dann eine vorbereitete Ausgabe oder fällige wiederkehrende Buchung mit gleichem Betrag.
    /// </summary>
    /// <param name="kasseId">Die Kasse.</param>
    /// <param name="umsaetze">Offene Umsätze der Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Vorschlag je Umsatz, soweit einer passt.</returns>
    public async Task<IReadOnlyDictionary<Guid, Abgleichvorschlag>> VorschlaegeAsync(Guid kasseId, IReadOnlyList<Kontoumsatz> umsaetze, CancellationToken ct = default)
    {
        var ergebnis = new Dictionary<Guid, Abgleichvorschlag>();
        if (umsaetze.Count == 0)
        {
            return ergebnis;
        }

        var von = umsaetze.Min(u => u.Buchungstag).AddDays(-Bankabgleich.ToleranzTage);
        var bis = umsaetze.Max(u => u.Buchungstag).AddDays(Bankabgleich.ToleranzTage);
        var buchungen = await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasseId && b.Datum >= von && b.Datum <= bis).ToListAsync(ct);
        var vergeben = (await db.Kontoumsaetze.AsNoTracking().Where(u => u.BuchungId != null).Select(u => u.BuchungId!.Value).ToListAsync(ct)).ToHashSet();
        var anordnungen = await db.Zahlungsanordnungen.AsNoTracking().Where(a => a.KasseId == kasseId && a.Status != AnordnungStatus.Abgelehnt).ToListAsync(ct);
        var wiederkehrend = await db.WiederkehrendeBuchungen.AsNoTracking().Where(w => w.KasseId == kasseId && w.Aktiv).ToListAsync(ct);
        foreach (var u in umsaetze)
        {
            if (Bankabgleich.SchluesselAusReferenz(u.Referenz) is { } teil
                && anordnungen.FirstOrDefault(a => a.Id.ToString("N").EndsWith(teil, StringComparison.Ordinal)) is { } aus)
            {
                if (aus.BuchungId is { } gebucht && !vergeben.Contains(gebucht))
                {
                    ergebnis[u.Id] = new Abgleichvorschlag(VorschlagArt.Buchung, gebucht, $"gezahlte Ausgabe „{aus.Zweck}“ (Referenz)");
                    vergeben.Add(gebucht);
                    continue;
                }

                if (aus.Status == AnordnungStatus.Vorbereitet && !vergeben.Contains(aus.Id))
                {
                    ergebnis[u.Id] = new Abgleichvorschlag(VorschlagArt.Anordnung, aus.Id, $"vorbereitete Ausgabe „{aus.Zweck}“ (Referenz)");
                    vergeben.Add(aus.Id);
                    continue;
                }
            }

            if (Bankabgleich.PassendeBuchung(u, buchungen, vergeben) is { } b)
            {
                ergebnis[u.Id] = new Abgleichvorschlag(VorschlagArt.Buchung, b.Id, $"Beleg {b.Kennung} vom {b.Datum:dd.MM.yyyy}: {b.Text}");
                vergeben.Add(b.Id);
            }
            else if (u.Betrag < 0 && anordnungen.FirstOrDefault(a => a.Status == AnordnungStatus.Vorbereitet && a.Betrag == -u.Betrag && !vergeben.Contains(a.Id)) is { } a)
            {
                ergebnis[u.Id] = new Abgleichvorschlag(VorschlagArt.Anordnung, a.Id, $"vorbereitete Ausgabe „{a.Zweck}“");
                vergeben.Add(a.Id);
            }
            else if (wiederkehrend.FirstOrDefault(w => w.NaechsteFaelligkeit <= u.Buchungstag.AddDays(31) && !vergeben.Contains(w.Id)
                && (w.Art == BuchungArt.Einnahme ? u.Betrag > 0 : u.Betrag < 0) && (w.Betrag is null || w.Betrag == Math.Abs(u.Betrag))) is { } w)
            {
                ergebnis[u.Id] = new Abgleichvorschlag(VorschlagArt.Wiederkehrend, w.Id, $"wiederkehrende Buchung „{w.Bezeichnung}“");
                vergeben.Add(w.Id);
            }
        }

        return ergebnis;
    }

    /// <summary>Ordnet einen Umsatz einer vorhandenen Buchung desselben Kontos zu.</summary>
    /// <param name="umsatzId">Der Umsatz.</param>
    /// <param name="buchungId">Die Buchung.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <exception cref="InvalidOperationException">Buchung und Umsatz passen nicht zusammen oder die Buchung ist schon zugeordnet.</exception>
    public async Task ZuordnenAsync(Guid umsatzId, Guid buchungId, CancellationToken ct = default)
    {
        var u = await db.Kontoumsaetze.FirstAsync(x => x.Id == umsatzId, ct);
        var b = await db.Buchungen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == buchungId, ct)
            ?? throw new InvalidOperationException("Die Buchung gibt es nicht.");
        if (Kassenregeln.Wirkung(b, u.KontoId) != u.Betrag)
        {
            throw new InvalidOperationException("Betrag oder Konto der Buchung passen nicht zum Umsatz.");
        }

        if (await db.Kontoumsaetze.AnyAsync(x => x.BuchungId == buchungId && x.Id != umsatzId, ct))
        {
            throw new InvalidOperationException($"Beleg {b.Kennung} ist schon einem anderen Umsatz zugeordnet.");
        }

        (u.BuchungId, u.Status) = (buchungId, UmsatzStatus.Zugeordnet);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Setzt den Stand eines Umsatzes auf ignoriert oder wieder offen.</summary>
    /// <param name="umsatzId">Der Umsatz.</param>
    /// <param name="ignorieren">true ignoriert, false öffnet wieder (und löst eine Zuordnung).</param>
    /// <param name="ct">Abbruchsignal.</param>
    public async Task StatusSetzenAsync(Guid umsatzId, bool ignorieren, CancellationToken ct = default)
    {
        var u = await db.Kontoumsaetze.FirstAsync(x => x.Id == umsatzId, ct);
        (u.Status, u.BuchungId) = (ignorieren ? UmsatzStatus.Ignoriert : UmsatzStatus.Offen, null);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Vergleicht für jedes Konto der Kasse den letzten Saldo laut Bank mit dem Kassenbuch am selben Tag.</summary>
    /// <param name="kasseId">Die Kasse.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Ein Abgleich je Konto mit Saldo laut Bank.</returns>
    public async Task<IReadOnlyList<Saldenabgleich>> SaldenAsync(Guid kasseId, CancellationToken ct = default)
    {
        var konten = await db.Zahlungsmittelkonten.AsNoTracking().Where(k => k.KasseId == kasseId).OrderBy(k => k.Sortierung).ToListAsync(ct);
        var ergebnis = new List<Saldenabgleich>();
        foreach (var konto in konten)
        {
            var letzter = await db.Kontoauszugimporte.AsNoTracking().Where(i => i.KontoId == konto.Id && i.Endsaldo != null && i.Bis != null)
                .OrderByDescending(i => i.Bis).ThenByDescending(i => i.ErstelltAm).FirstOrDefaultAsync(ct);
            if (letzter is null)
            {
                continue;
            }

            var buchungen = await db.Buchungen.AsNoTracking().Where(b => b.KasseId == kasseId && b.Datum <= letzter.Bis!.Value).ToListAsync(ct);
            ergebnis.Add(new Saldenabgleich(konto, letzter.Bis!.Value, letzter.Endsaldo!.Value, Kassenregeln.Kontostand(konto, buchungen, letzter.Bis!.Value)));
        }

        return ergebnis;
    }

    private static string? Kuerzen(string? text, int laenge) => text is null ? null : text.Length > laenge ? text[..laenge] : text;
}
