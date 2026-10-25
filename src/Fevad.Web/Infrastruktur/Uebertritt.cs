using Fevad.Domain.Laufbahn;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Laufbahn;
using Fevad.Infrastructure.Persistenz;
using Fevad.Web.Pages.Laufbahn;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Übertritt in eine andere Abteilung, aus der Laufbahn und bei der Annahme eines Übertrittsantrags. Alle Daten (Lehrgänge,
/// Abzeichen, Dienstgrade, Dienstzeit) bleiben am Mitglied; nur Zug und Gruppe der alten Abteilung enden. In die
/// Einsatzabteilung mit Probezeit nach <see cref="ProbezeitWahl"/>; zwischen zwei Einsatzabteilungen geht Status und Probezeit
/// unverändert mit.
/// </summary>
public sealed class Uebertritt(AppDbContext db, Laufbahnabfrage laufbahn)
{
    /// <summary>Ergebnis eines Übertritts.</summary>
    /// <param name="Neu">Die neue Mitgliedschaft.</param>
    /// <param name="Ziel">Die Zielabteilung.</param>
    /// <param name="GruppenBeendet">Zug und Gruppe der alten Abteilung wurden beendet.</param>
    public sealed record Ergebnis(Mitgliedschaft Neu, Abteilung Ziel, bool GruppenBeendet)
    {
        /// <summary>Meldung für die Oberfläche mit Ziel, Datum und gegebenenfalls Probezeit.</summary>
        /// <param name="ab">Tag des Übertritts.</param>
        /// <returns>Der Text.</returns>
        public string Meldung(DateOnly ab)
        {
            var text = $"Übertritt in die {Ziel.Bezeichnung} zum {Anzeige.Datum(ab)} gespeichert";
            if (Neu.Status == MitgliedStatus.Probezeit)
            {
                text += $", Probezeit bis {Anzeige.Datum(Neu.ProbezeitBis)}";
            }

            return GruppenBeendet ? text + ". Zug und Gruppe der bisherigen Abteilung sind beendet." : text + ".";
        }
    }

    /// <summary>
    /// Beendet <paramref name="alt"/> und legt die neue Mitgliedschaft an (hinzugefügt, noch nicht gespeichert). Wirft
    /// <see cref="InvalidOperationException"/> mit einer Meldung für die Oberfläche, wenn Altersgrenzen oder Probezeit nicht passen.
    /// </summary>
    public async Task<Ergebnis> AusfuehrenAsync(
        Mitgliedschaft alt, Guid zielAbteilungId, DateOnly ab, ProbezeitWahl probezeit, DateOnly? probezeitBis, string? probezeitBemerkung,
        CancellationToken ct)
    {
        await db.Entry(alt).Reference(m => m.Abteilung).LoadAsync(ct);
        var ziel = await db.Abteilungen.SingleAsync(a => a.Id == zielAbteilungId, ct);
        var mitglied = await db.Mitglieder.SingleAsync(m => m.Id == alt.MitgliedId, ct);
        var regeln = await laufbahn.RegelnAsync(ab, ct);
        if (Mitgliedschaftswechsel.Pruefen(mitglied.Geburtsdatum, ziel.Art, ab, regeln) is { } fehler)
        {
            throw new InvalidOperationException(fehler);
        }

        var wechselInnerhalbEinsatz = ziel.MitProbezeit && alt.Abteilung!.MitProbezeit && alt.Status != MitgliedStatus.Befreit;
        var status = !ziel.MitProbezeit ? MitgliedStatus.Aktiv
            : wechselInnerhalbEinsatz ? alt.Status
            : probezeit == ProbezeitWahl.Verzicht ? MitgliedStatus.Aktiv
            : MitgliedStatus.Probezeit;
        var bisherigeProbezeit = (alt.ProbezeitBis, alt.ProbezeitBemerkung);
        var neu = Mitgliedschaftswechsel.Uebertreten(alt, ziel.Id, status, ab);
        if (status == MitgliedStatus.Probezeit)
        {
            (neu.ProbezeitBis, neu.ProbezeitBemerkung) = wechselInnerhalbEinsatz
                ? bisherigeProbezeit
                : (probezeit == ProbezeitWahl.Verkuerzt
                    ? probezeitBis ?? throw new InvalidOperationException("Für eine verkürzte Probezeit das Ende angeben.")
                    : ab.AddMonths(regeln.ProbezeitMonate), Leer(probezeitBemerkung));
            if (neu.ProbezeitBis < ab)
            {
                throw new InvalidOperationException("Das Ende der Probezeit liegt vor dem Übertritt.");
            }
        }
        else if (ziel.MitProbezeit && probezeit == ProbezeitWahl.Verzicht && !wechselInnerhalbEinsatz)
        {
            neu.Bemerkung = Leer(probezeitBemerkung) is { } grund ? $"Ohne Probezeit: {grund}" : "Ohne Probezeit";
        }

        var gruppen = await db.GruppenMitgliedschaften
            .Where(g => g.MitgliedId == alt.MitgliedId && g.Gruppe!.AbteilungId == alt.AbteilungId).ToListAsync(ct);
        db.GruppenMitgliedschaften.RemoveRange(gruppen);

        await db.SaveChangesAsync(ct);
        db.Mitgliedschaften.Add(neu);
        return new Ergebnis(neu, ziel, gruppen.Count > 0);
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
