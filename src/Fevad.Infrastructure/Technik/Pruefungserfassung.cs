using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Technik;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Technik;

/// <summary>
/// Legt Prüfprotokolle an und storniert sie; gemeinsam für Einzel- und Sammelprüfung (V58).
/// Folgen einer Prüfung: Mangel bei Befund, Aussonderung, Aufheben der Sperre nach dem Einsatz (V13).
/// </summary>
public sealed class Pruefungserfassung(AppDbContext db, TimeProvider zeit)
{
    /// <summary>Fügt die Prüfung samt Folgen dem Kontext hinzu; gespeichert wird beim Aufrufer.</summary>
    /// <param name="mangel">Befund; bei „nicht bestanden“ ohne Text wird einer erzeugt.</param>
    public async Task AnlegenAsync(Pruefung pruefung, PruefplanPosition? position, string? mangel, MangelSchwere schwere, CancellationToken ct)
    {
        pruefung.Abgeschlossen = true;

        // Nur eine bestandene Prüfung setzt die Frist neu.
        if (position is { IntervallMonate: > 0 } && pruefung.Ergebnis is PruefErgebnis.Bestanden or PruefErgebnis.BestandenMitMaengeln)
        {
            pruefung.NaechsteFaelligkeitVorbelegen(position);
        }

        db.Pruefungen.Add(pruefung);

        mangel = mangel?.Trim();
        // Nicht bestanden heißt außer Betrieb, auch wenn der Mangel beschrieben ist.
        if (pruefung.Ergebnis == PruefErgebnis.NichtBestanden)
        {
            mangel = string.IsNullOrEmpty(mangel) ? $"{Text(pruefung.Pruefart)} nicht bestanden" : mangel;
            schwere = MangelSchwere.AusserBetrieb;
        }

        if (!string.IsNullOrEmpty(mangel) && pruefung.Ergebnis != PruefErgebnis.Ausgesondert)
        {
            db.Maengel.Add(new Mangel
            {
                GeraetId = pruefung.GeraetId,
                FahrzeugId = pruefung.FahrzeugId,
                Beschreibung = mangel,
                Schwere = schwere,
                Status = MangelStatus.Offen,
                GemeldetAm = zeit.GetUtcNow(),
                GemeldetVonId = pruefung.PrueferId,
                PruefungId = pruefung.Id,
            });
        }

        if (pruefung.GeraetId is null)
        {
            return;
        }

        if (pruefung.Ergebnis == PruefErgebnis.Ausgesondert && await db.Geraete.FirstOrDefaultAsync(g => g.Id == pruefung.GeraetId, ct) is { } geraet)
        {
            geraet.AusgesondertAm ??= pruefung.Datum;
            // Die volle Bemerkung steht im Prüfprotokoll; der Grund ist auf 200 Zeichen begrenzt.
            var grund = $"{Text(pruefung.Pruefart)} vom {pruefung.Datum:dd.MM.yyyy}" + (pruefung.Bemerkung is null ? "" : $": {pruefung.Bemerkung}");
            geraet.Aussonderungsgrund ??= grund.Length <= 200 ? grund : grund[..199] + "…";
        }

        if (pruefung.ErledigtEinsatzpruefung)
        {
            var sperren = await db.Maengel
                .Where(m => m.GeraetId == pruefung.GeraetId && m.EinsatzId != null && m.Status != MangelStatus.Behoben)
                .ToListAsync(ct);
            foreach (var sperre in sperren)
            {
                sperre.Status = MangelStatus.Behoben;
                sperre.BehobenAm = zeit.GetUtcNow();
                sperre.ErledigtDurchPruefungId = pruefung.Id;
                sperre.Massnahme = $"{Text(pruefung.Pruefart)} am {pruefung.Datum:dd.MM.yyyy}: {Text(pruefung.Ergebnis)}";
            }
        }
    }

    /// <summary>Storniert eine Prüfung; Einsatzsperren, die sie aufgehoben hat, gelten wieder.</summary>
    public async Task<string?> StornierenAsync(Guid pruefungId, Guid benutzerId, string grund, CancellationToken ct)
    {
        var pruefung = await db.Pruefungen.FirstOrDefaultAsync(p => p.Id == pruefungId, ct);
        if (pruefung is null)
        {
            return "Prüfung nicht gefunden.";
        }

        if (pruefung.StornoHindernis() is { } hindernis)
        {
            return hindernis;
        }

        if (string.IsNullOrWhiteSpace(grund))
        {
            return "Bitte den Grund für das Storno angeben.";
        }

        pruefung.Stornieren(benutzerId, grund, zeit.GetUtcNow());

        foreach (var sperre in await db.Maengel.Where(m => m.ErledigtDurchPruefungId == pruefungId).ToListAsync(ct))
        {
            sperre.Status = MangelStatus.Offen;
            sperre.BehobenAm = null;
            sperre.ErledigtDurchPruefungId = null;
            sperre.Massnahme = $"Prüfung vom {pruefung.Datum:dd.MM.yyyy} storniert, Sperre gilt wieder.";
        }

        return null;
    }

    private static string Text(Enum wert) =>
        wert.GetType().GetField(wert.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? wert.ToString();
}
