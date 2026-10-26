using System.Text.Json;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Technik;

/// <summary>
/// Soll-Beladung eines Fahrzeugfachs (V59): welches Material in welcher Menge in welchem Fach liegen soll.
/// Das Ist kommt aus den Geräten, die auf dem Fahrzeug in diesem Fach geführt werden. Normbeladungen werden als eigene Listen
/// gepflegt, DIN-Texte werden nicht übernommen.
/// </summary>
public class Beladeposition : Entitaet
{
    /// <summary>Kennung des Fahrzeugs.</summary>
    public Guid FahrzeugId { get; set; }
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Geräteraum bzw. Fach, z. B. „G3“; entspricht dem Lagerort der Geräte.</summary>
    public required string Fach { get; set; }

    /// <summary>Was im Fach liegen soll, z. B. „Rettungsschere“ oder „Ölbindemittel 20 l“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Materialstamm; Geräte dieses Materials im Fach zählen als Ist.</summary>
    public Guid? MaterialId { get; set; }
    /// <summary>Der Materialstamm.</summary>
    public Material? Material { get; set; }

    /// <summary>Geräteart, wenn jedes Material dieser Art genügt (z. B. „Druckschlauch“); gilt nur ohne Materialstamm.</summary>
    public Guid? GeraeteartId { get; set; }
    /// <summary>Die Geräteart.</summary>
    public Geraeteart? Geraeteart { get; set; }

    /// <summary>Soll-Menge.</summary>
    public int Menge { get; set; } = 1;

    /// <summary>Reihenfolge innerhalb des Fachs.</summary>
    public int Sortierung { get; set; }

    /// <summary>Bemerkung, z. B. Herkunft der Liste („Normbeladung HLF 10, Stand 2023“).</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Ohne Materialstamm und Geräteart: Mengenposition, die nur gezählt und nicht über Geräte abgeglichen wird.</summary>
    public bool IstMengenposition => MaterialId is null && GeraeteartId is null;
}

/// <summary>
/// Beladekontrolle eines Fahrzeugs (V59): Abgleich der Soll-Beladung mit dem, was tatsächlich gefunden wurde, per Scan oder Haken.
/// Das Ergebnis wird beim Abschluss festgeschrieben; die Fehlliste kann als Mangel am Fahrzeug gemeldet werden.
/// </summary>
public class Beladekontrolle : Entitaet
{
    /// <summary>Kennung des Fahrzeugs.</summary>
    public Guid FahrzeugId { get; set; }
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Tag der Kontrolle.</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Kontrollierendes Mitglied (angemeldetes Konto), wie der Prüfer im Prüfbuch.</summary>
    public Guid? KontrolliertVonId { get; set; }
    /// <summary>Das kontrollierende Mitglied.</summary>
    public Mitglied? KontrolliertVon { get; set; }

    /// <summary>Alles war da.</summary>
    public bool Vollstaendig { get; set; }

    /// <summary>Zahl der fehlenden Stücke über alle Positionen.</summary>
    public int Fehlend { get; set; }

    /// <summary>Ergebnis je Position als JSON (<see cref="Beladeabgleich.Zeile"/>).</summary>
    public string Ergebnis { get; set; } = "[]";

    /// <summary>Mangel, der aus der Fehlliste gemeldet wurde.</summary>
    public Guid? MangelId { get; set; }

    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Liest das gespeicherte Ergebnis.</summary>
    /// <returns>Die Zeilen der Kontrolle.</returns>
    public IReadOnlyList<Beladeabgleich.Zeile> Zeilen() =>
        JsonSerializer.Deserialize<List<Beladeabgleich.Zeile>>(Ergebnis, Beladeabgleich.Json) ?? [];
}

/// <summary>Abgleich von Soll-Beladung und Ist (zugeordnete Geräte) bzw. Gefundenem (Beladekontrolle), V59.</summary>
public static class Beladeabgleich
{
    /// <summary>JSON-Einstellungen für das gespeicherte Ergebnis.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Ein Gerät auf dem Fahrzeug.</summary>
    /// <param name="Id">Kennung.</param>
    /// <param name="Inventarnummer">Nummer am Gerät.</param>
    /// <param name="Bezeichnung">Bezeichnung.</param>
    /// <param name="MaterialId">Materialstamm.</param>
    /// <param name="GeraeteartId">Geräteart.</param>
    /// <param name="Fach">Lagerort bzw. Fach.</param>
    public sealed record Geraet(Guid Id, string Inventarnummer, string Bezeichnung, Guid MaterialId, Guid GeraeteartId, string? Fach);

    /// <summary>Ergebnis einer Soll-Position oder eines Geräts ohne Soll.</summary>
    /// <param name="PositionId">Soll-Position; leer bei Geräten ohne Soll.</param>
    /// <param name="Fach">Fach.</param>
    /// <param name="Bezeichnung">Bezeichnung der Position bzw. des Geräts.</param>
    /// <param name="Soll">Soll-Menge; 0 bei Geräten ohne Soll.</param>
    /// <param name="Ist">Laut FEVAD zugeordnete Stücke bzw. bei der Kontrolle gefundene.</param>
    /// <param name="Fehlende">Inventarnummern zugeordneter Geräte, die nicht gefunden wurden.</param>
    public sealed record Zeile(Guid? PositionId, string Fach, string Bezeichnung, int Soll, int Ist, IReadOnlyList<string> Fehlende)
    {
        /// <summary>Fehlende Stücke gegenüber dem Soll.</summary>
        public int Fehlt => Math.Max(0, Soll - Ist);
        /// <summary>Stücke über dem Soll.</summary>
        public int Zuviel => Math.Max(0, Ist - Soll);
        /// <summary>Soll und Ist stimmen überein.</summary>
        public bool Stimmt => Soll == Ist;
    }

    /// <summary>Schreibweise eines Fachs für den Vergleich: ohne Leerraum am Rand, Groß-/Kleinschreibung egal.</summary>
    /// <param name="fach">Fach oder Lagerort.</param>
    /// <returns>Vergleichsschlüssel; leer wird „“.</returns>
    public static string Fachschluessel(string? fach) => (fach ?? "").Trim().ToUpperInvariant();

    /// <summary>Passt das Gerät zur Position (gleiches Fach und gleiches Material bzw. gleiche Geräteart)?</summary>
    /// <param name="position">Soll-Position.</param>
    /// <param name="geraet">Gerät.</param>
    /// <returns>true, wenn das Gerät als Ist der Position zählt.</returns>
    public static bool Passt(Beladeposition position, Geraet geraet) =>
        Fachschluessel(position.Fach) == Fachschluessel(geraet.Fach)
        && (position.MaterialId is { } material ? material == geraet.MaterialId
            : position.GeraeteartId is { } art && art == geraet.GeraeteartId);

    /// <summary>
    /// Ordnet die Geräte den Positionen zu: je Fach zuerst Positionen mit Materialstamm, dann solche mit Geräteart, jeweils
    /// höchstens bis zur Soll-Menge; was übrig bleibt, zählt bei der ersten passenden Position als zu viel oder ist ohne Soll.
    /// </summary>
    /// <param name="positionen">Soll-Beladung.</param>
    /// <param name="geraete">Geräte auf dem Fahrzeug (nicht ausgesondert).</param>
    /// <returns>Zuordnung Position → Geräte und Geräte ohne Soll.</returns>
    public static (IReadOnlyDictionary<Guid, List<Geraet>> JePosition, IReadOnlyList<Geraet> OhneSoll) Zuordnen(
        IReadOnlyList<Beladeposition> positionen, IReadOnlyList<Geraet> geraete)
    {
        var jePosition = positionen.ToDictionary(p => p.Id, _ => new List<Geraet>());
        var frei = geraete.OrderBy(g => g.Inventarnummer, StringComparer.Ordinal).ToList();
        var reihenfolge = positionen.OrderBy(p => p.MaterialId is null ? 1 : 0).ThenBy(p => p.Sortierung).ToList();

        // Erster Durchgang bis zum Soll, zweiter für den Überschuss.
        foreach (var bisSoll in new[] { true, false })
        {
            foreach (var position in reihenfolge.Where(p => !p.IstMengenposition))
            {
                var liste = jePosition[position.Id];
                foreach (var g in frei.Where(g => Passt(position, g)).ToList())
                {
                    if (bisSoll && liste.Count >= position.Menge)
                    {
                        break;
                    }

                    liste.Add(g);
                    frei.Remove(g);
                }
            }
        }

        return (jePosition, frei);
    }

    /// <summary>Soll/Ist laut FEVAD: zugeordnete Geräte je Position und Geräte ohne Soll.</summary>
    /// <param name="positionen">Soll-Beladung.</param>
    /// <param name="geraete">Geräte auf dem Fahrzeug (nicht ausgesondert).</param>
    /// <returns>Je Position und je Gerät ohne Soll eine Zeile, sortiert nach Fach.</returns>
    public static IReadOnlyList<Zeile> SollIst(IReadOnlyList<Beladeposition> positionen, IReadOnlyList<Geraet> geraete)
    {
        var (jePosition, ohneSoll) = Zuordnen(positionen, geraete);
        return positionen
            .Select(p => new Zeile(p.Id, p.Fach, p.Bezeichnung, p.Menge, p.IstMengenposition ? p.Menge : jePosition[p.Id].Count, []))
            .Concat(ohneSoll.Select(g => new Zeile(null, g.Fach ?? "", $"{g.Inventarnummer} {g.Bezeichnung}", 0, 1, [])))
            .OrderBy(z => Fachschluessel(z.Fach), StringComparer.Ordinal)
            .ThenBy(z => z.Bezeichnung, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>
    /// Ergebnis einer Beladekontrolle: je Position, wie viele Stücke gefunden wurden, und welche zugeordneten Geräte fehlen.
    /// Gefundene Geräte zählen bei ihrer Position, auch wenn sie über dem Soll liegen; Mengenpositionen zählen mit der eingegebenen Zahl.
    /// </summary>
    /// <param name="positionen">Soll-Beladung.</param>
    /// <param name="geraete">Geräte auf dem Fahrzeug (nicht ausgesondert).</param>
    /// <param name="gefunden">Kennungen der gefundenen (gescannten oder abgehakten) Geräte.</param>
    /// <param name="gezaehlt">Gezählte Menge je Mengenposition; fehlt eine Angabe, gilt 0.</param>
    /// <returns>Je Position eine Zeile; Geräte ohne Soll erscheinen nur, wenn sie fehlen.</returns>
    public static IReadOnlyList<Zeile> Kontrolle(
        IReadOnlyList<Beladeposition> positionen,
        IReadOnlyList<Geraet> geraete,
        IReadOnlySet<Guid> gefunden,
        IReadOnlyDictionary<Guid, int> gezaehlt)
    {
        var (jePosition, ohneSoll) = Zuordnen(positionen, geraete);
        var zeilen = positionen.Select(p =>
        {
            if (p.IstMengenposition)
            {
                return new Zeile(p.Id, p.Fach, p.Bezeichnung, p.Menge, Math.Max(0, gezaehlt.GetValueOrDefault(p.Id)), []);
            }

            var liste = jePosition[p.Id];
            var fehlende = liste.Where(g => !gefunden.Contains(g.Id)).Select(g => g.Inventarnummer).ToList();
            return new Zeile(p.Id, p.Fach, p.Bezeichnung, p.Menge, liste.Count - fehlende.Count, fehlende);
        }).ToList();

        zeilen.AddRange(ohneSoll.Where(g => !gefunden.Contains(g.Id))
            .Select(g => new Zeile(null, g.Fach ?? "", g.Bezeichnung, 0, 0, [g.Inventarnummer])));

        return zeilen
            .OrderBy(z => Fachschluessel(z.Fach), StringComparer.Ordinal)
            .ThenBy(z => z.Bezeichnung, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>Fehlliste als Text für die Mängelmeldung, je Zeile „Fach: Bezeichnung, fehlt n (Nr. …)“.</summary>
    /// <param name="zeilen">Ergebnis der Kontrolle.</param>
    /// <returns>Leer, wenn nichts fehlt.</returns>
    public static string Fehlliste(IEnumerable<Zeile> zeilen) =>
        string.Join('\n', zeilen
            .Where(z => z.Fehlt > 0 || z.Fehlende.Count > 0)
            .Select(z =>
            {
                var fach = string.IsNullOrWhiteSpace(z.Fach) ? "ohne Fach" : z.Fach;
                var teile = new List<string>();
                if (z.Fehlt > 0)
                {
                    teile.Add($"fehlt {z.Fehlt} von {z.Soll}");
                }

                if (z.Fehlende.Count > 0)
                {
                    teile.Add($"nicht gefunden: Nr. {string.Join(", ", z.Fehlende)}");
                }

                return $"{fach}: {z.Bezeichnung}, {string.Join("; ", teile)}";
            }));

    /// <summary>Fehlende Stücke insgesamt: je Zeile die Fehlmenge gegenüber dem Soll oder, wenn mehr, die nicht gefundenen Geräte.</summary>
    /// <param name="zeilen">Ergebnis der Kontrolle.</param>
    /// <returns>Anzahl.</returns>
    public static int FehlendGesamt(IEnumerable<Zeile> zeilen) =>
        zeilen.Sum(z => Math.Max(z.Fehlt, z.Fehlende.Count));
}
