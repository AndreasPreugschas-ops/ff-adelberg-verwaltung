using System.Text.Json;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Import;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Import;

/// <summary>Ein Feld, das ein Import füllen kann.</summary>
/// <param name="Schluessel">Technischer Name, Schlüssel in der Zuordnung.</param>
/// <param name="Bezeichnung">Name in der Oberfläche.</param>
/// <param name="Pflicht">Ohne Zuordnung kein Import.</param>
/// <param name="Hinweis">Kurzer Hinweis zum erwarteten Format.</param>
/// <param name="Synonyme">Weitere Spaltenüberschriften, die für den Zuordnungsvorschlag passen.</param>
public sealed record Zielfeld(string Schluessel, string Bezeichnung, bool Pflicht = false, string? Hinweis = null, params string[] Synonyme);

/// <summary>Eine Meldung zu einer Zeile der Datei (Zeile 2 ist die erste Datenzeile, wie in Excel).</summary>
/// <param name="Zeile">Zeilennummer in der Datei.</param>
/// <param name="Text">Was nicht passt; ohne Gesundheitsangaben.</param>
public sealed record Importmeldung(int Zeile, string Text);

/// <summary>Ergebnis eines Probelaufs oder einer Übernahme.</summary>
/// <param name="Zeilen">Datenzeilen.</param>
/// <param name="Angelegt">Neu angelegte Datensätze.</param>
/// <param name="Aktualisiert">Ergänzte Datensätze.</param>
/// <param name="Fehlerhaft">Übersprungene Zeilen.</param>
/// <param name="Meldungen">Meldungen je Zeile.</param>
public sealed record Importergebnis(int Zeilen, int Angelegt, int Aktualisiert, int Fehlerhaft, IReadOnlyList<Importmeldung> Meldungen);

/// <summary>
/// Altdatenimport (V68): liest die Datei eines <see cref="Importlauf"/>, wendet die Feldzuordnung an und legt Personen, Lehrgänge,
/// Geräte, Atemschutzfristen, Einsätze oder Einsatzteilnahmen an bzw. ergänzt sie. Der Probelauf rechnet alles durch und speichert nichts; die Übernahme speichert alle
/// fehlerfreien Zeilen in einer Transaktion. Fehlerhafte Zeilen werden übersprungen und gemeldet.
/// </summary>
public sealed partial class Importdienst(AppDbContext db, TimeProvider zeit)
{
    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Die Felder je Ziel.</summary>
    /// <param name="ziel">Das Importziel.</param>
    /// <returns>Die Felder in Anzeigereihenfolge.</returns>
    public static IReadOnlyList<Zielfeld> Felder(Importziel ziel) => ziel switch
    {
        Importziel.Personen =>
        [
            new("Personalnummer", "Personalnummer", true, "eindeutig; daran werden vorhandene Personen erkannt", "Pers.-Nr.", "PersNr", "Nummer"),
            new("MpFeuerId", "Kennung aus MP-Feuer", false, null, "ID", "MP-ID"),
            new("Nachname", "Nachname", false, "Pflicht bei neuen Personen", "Name"),
            new("Vorname", "Vorname", false, "Pflicht bei neuen Personen"),
            new("Geburtsdatum", "Geburtsdatum", false, "TT.MM.JJJJ; Pflicht bei neuen Personen", "Geb.-Datum", "geboren"),
            new("Geschlecht", "Geschlecht", false, "m, w oder d"),
            new("Strasse", "Straße", false, null, "Strasse", "Anschrift"),
            new("Plz", "PLZ", false, null, "Postleitzahl"),
            new("Ort", "Ort", false, null, "Wohnort"),
            new("TelefonMobil", "Mobil", false, null, "Handy", "Mobiltelefon"),
            new("TelefonFestnetz", "Telefon privat", false, null, "Telefon"),
            new("Email", "E-Mail", false, null, "Mail", "E-Mail-Adresse"),
            new("Abteilung", "Abteilung", false, "Name oder Kürzel der Abteilung in FEVAD", "Abt"),
            new("Eintritt", "Eintritt", false, "Pflicht, wenn eine Abteilung angegeben ist", "Eintrittsdatum", "Mitglied seit"),
            new("Austritt", "Austritt", false, "leer, wenn noch Mitglied", "Austrittsdatum"),
            new("Dienstgrad", "Dienstgrad", false, "Name oder Kürzel wie in FEVAD"),
            new("DienstgradAb", "Dienstgrad seit", false, "leer: Eintritt", "Beförderung", "Beförderungsdatum"),
            new("AtemschutzAktiv", "Aktiver Atemschutzgeräteträger", false, "ja oder nein", "AGT", "Atemschutz"),
        ],
        Importziel.Qualifikationen =>
        [
            new("Personalnummer", "Personalnummer", true, "die Person muss schon in FEVAD sein", "Pers.-Nr.", "PersNr", "Nummer"),
            new("Lehrgang", "Lehrgang oder Qualifikation", true, "Name oder Kürzel wie im Katalog, auch Fahrerlaubnisse", "Qualifikation", "Ausbildung", "Bezeichnung"),
            new("ErworbenAm", "Erworben am", true, "TT.MM.JJJJ", "Datum", "Abschluss", "bestanden am"),
            new("GueltigBis", "Gültig bis", false, "leer: nach der Gültigkeit im Katalog", "gültig bis", "Ablauf"),
            new("Lehrgangsort", "Lehrgangsort", false, null, "Ort"),
            new("Bemerkung", "Bemerkung", false),
        ],
        Importziel.Geraete =>
        [
            new("Inventarnummer", "Inventarnummer", true, "eindeutig; daran werden vorhandene Geräte erkannt", "Inv.-Nr.", "InvNr"),
            new("Bezeichnung", "Bezeichnung", false, "Pflicht bei neuen Geräten", "Gerät", "Name"),
            new("Geraeteart", "Geräteart", false, "Name oder Kürzel wie im Katalog; Pflicht bei neuen Geräten", "Art", "Gerätegruppe"),
            new("Material", "Materialstamm", false, "leer: Bezeichnung; fehlt er, wird er angelegt", "Artikel"),
            new("Hersteller", "Hersteller"),
            new("Typ", "Typ", false, null, "Modell"),
            new("Seriennummer", "Seriennummer", false, null, "Ser.-Nr.", "SerNr"),
            new("Baujahr", "Baujahr", false, "JJJJ"),
            new("Herstelldatum", "Herstelldatum", false, "TT.MM.JJJJ", "hergestellt"),
            new("BeschafftAm", "Beschafft am", false, "TT.MM.JJJJ", "Beschaffung", "Kaufdatum"),
            new("Anschaffungspreis", "Anschaffungspreis", false, "in Euro", "Preis", "Kaufpreis"),
            new("IndienstAm", "In Dienst seit", false, "TT.MM.JJJJ", "Indienststellung"),
            new("Fahrzeug", "Fahrzeug", false, "Funkrufname oder Kennzeichen wie in FEVAD", "Standort"),
            new("Lagerort", "Lagerort", false, "Geräteraum oder Fach", "Fach", "Geräteraum"),
            new("Inventurgruppe", "Inventurgruppe", false, "Bezeichnung wie in FEVAD"),
            new("LetztePruefung", "Letzte Prüfung", false, "TT.MM.JJJJ; wird als bestandene Prüfung eingetragen", "geprüft am", "Prüfdatum"),
            new("NaechstePruefung", "Nächste Prüfung", false, "leer: nach dem Prüfplan der Geräteart", "fällig", "nächste Prüfung"),
            new("Bemerkung", "Bemerkung"),
        ],
        Importziel.Atemschutz =>
        [
            new("Personalnummer", "Personalnummer", true, "die Person muss schon in FEVAD sein", "Pers.-Nr.", "PersNr", "Nummer"),
            new("VorsorgeAm", "Vorsorge am", false, "Tag der Untersuchung, nur zusammen mit „gültig bis“; keine Befunde", "G26 am", "untersucht am"),
            new("VorsorgeGueltigBis", "Vorsorge gültig bis", false, "TT.MM.JJJJ", "G26 gültig bis", "G 26.3 bis", "nächste Untersuchung"),
            new("Belastungsuebung", "Letzte Belastungsübung", false, "TT.MM.JJJJ", "Übungsstrecke", "Belastungsübung"),
            new("Einsatzuebung", "Letzte Einsatzübung", false, "TT.MM.JJJJ", "Einsatzübung"),
            new("Unterweisung", "Letzte Unterweisung", false, "TT.MM.JJJJ"),
        ],
        Importziel.Einsaetze =>
        [
            new("Einsatznummer", "Einsatznummer", true, "eindeutig; daran werden vorhandene Einsätze erkannt", "Einsatz-Nr.", "Nr", "Nummer"),
            new("Alarmiert", "Alarmiert", false, "Datum, auch mit Uhrzeit; Pflicht bei neuen Einsätzen", "Alarmzeit", "Datum", "Alarmierung"),
            new("AlarmiertUhrzeit", "Alarmiert um", false, "HH:MM, wenn die Uhrzeit in einer eigenen Spalte steht", "Uhrzeit", "Alarmuhrzeit"),
            new("Einsatzende", "Einsatzende", false, "Datum, auch mit Uhrzeit", "Ende"),
            new("EinsatzendeUhrzeit", "Einsatzende um", false, "HH:MM; ohne Datum am Alarmtag, nach Mitternacht am Folgetag", "Endzeit"),
            new("Stichwort", "Stichwort", false, null, "Einsatzstichwort"),
            new("Meldung", "Meldung", false, null, "Meldebild", "Einsatzgrund"),
            new("Kategorie", "Einsatzart (Statistik)", false, "Name oder Kürzel wie im Katalog", "Einsatzart", "Art"),
            new("Strasse", "Straße", false, null, "Strasse", "Einsatzort"),
            new("Hausnummer", "Hausnummer", false, null, "Nr."),
            new("Plz", "PLZ", false, null, "Postleitzahl"),
            new("Ort", "Ort", false, null, "Gemeinde"),
            new("Objekt", "Objekt"),
            new("Einsatzleiter", "Einsatzleiter", false, "Personalnummer", "Einsatzleitung"),
            new("Lagebericht", "Bericht", false, null, "Lagebericht", "Bemerkung", "Text"),
        ],
        Importziel.Einsatzteilnahmen =>
        [
            new("Einsatznummer", "Einsatznummer", true, "der Einsatz muss schon in FEVAD sein", "Einsatz-Nr.", "Einsatz"),
            new("Personalnummer", "Personalnummer", true, "die Person muss schon in FEVAD sein", "Pers.-Nr.", "PersNr"),
            new("Funktion", "Funktion im Einsatz", false, null, "Funktion", "Position"),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(ziel)),
    };

    /// <summary>Schlägt eine Zuordnung vor: Spalten, deren Überschrift zum Feldnamen oder einem Synonym passt.</summary>
    /// <param name="ziel">Das Importziel.</param>
    /// <param name="spalten">Spaltenüberschriften der Datei.</param>
    /// <returns>Zielfeld → Spalte.</returns>
    public static Dictionary<string, string> Vorschlag(Importziel ziel, IReadOnlyList<string> spalten)
    {
        var ergebnis = new Dictionary<string, string>();
        foreach (var feld in Felder(ziel))
        {
            var namen = new[] { feld.Schluessel, feld.Bezeichnung }.Concat(feld.Synonyme).Select(Altwert.Schluessel).ToHashSet();
            if (spalten.FirstOrDefault(s => namen.Contains(Altwert.Schluessel(s)) && !ergebnis.ContainsValue(s)) is { } spalte)
            {
                ergebnis[feld.Schluessel] = spalte;
            }
        }

        return ergebnis;
    }

    /// <summary>Liest die Zuordnung eines Laufs oder einer Vorlage.</summary>
    /// <param name="json">Die gespeicherte Zuordnung.</param>
    /// <returns>Zielfeld → Spalte.</returns>
    public static Dictionary<string, string> ZuordnungLesen(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];

    /// <summary>Schreibt eine Zuordnung als JSON; leere Einträge entfallen.</summary>
    /// <param name="zuordnung">Zielfeld → Spalte.</param>
    /// <returns>Das JSON.</returns>
    public static string ZuordnungSchreiben(IReadOnlyDictionary<string, string?> zuordnung) =>
        JsonSerializer.Serialize(zuordnung.Where(z => !string.IsNullOrWhiteSpace(z.Value)).ToDictionary(z => z.Key, z => z.Value!), Json);

    /// <summary>
    /// Entfernt die zitierten Werte aus den Meldungen („…“ statt des Werts). Geschieht zusammen mit dem Löschen der Datei, weil die
    /// Werte oft Personen betreffen, die gar nicht übernommen wurden; übrig bleiben Zeile, Feld und Meldungstext.
    /// </summary>
    /// <param name="json">Die Meldungen als JSON.</param>
    /// <returns>Die Meldungen ohne Werte als JSON.</returns>
    public static string MeldungenOhneWerte(string? json) =>
        JsonSerializer.Serialize(MeldungenLesen(json).Select(m => m with { Text = ZitierterWert().Replace(m.Text, "„…“") }).ToList(), Json);

    [System.Text.RegularExpressions.GeneratedRegex("„[^“]*“")]
    private static partial System.Text.RegularExpressions.Regex ZitierterWert();

    /// <summary>Liest die gespeicherten Meldungen eines Laufs.</summary>
    /// <param name="json">Die Meldungen als JSON.</param>
    /// <returns>Die Meldungen.</returns>
    public static IReadOnlyList<Importmeldung> MeldungenLesen(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<Importmeldung>>(json) ?? [];

    /// <summary>
    /// Rechnet den Lauf durch. Im Probelauf wird nichts gespeichert; bei der Übernahme werden die fehlerfreien Zeilen gespeichert,
    /// die Datei gelöscht und der Lauf als Protokoll abgeschlossen. Das Ergebnis steht danach auch im Lauf.
    /// </summary>
    /// <param name="laufId">Kennung des Laufs.</param>
    /// <param name="probelauf"><c>true</c> für den Probelauf.</param>
    /// <param name="benutzer">Benutzername für das Protokoll.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Das Ergebnis.</returns>
    /// <exception cref="InvalidOperationException">Der Lauf ist nicht mehr vorbereitet oder eine Pflichtzuordnung fehlt.</exception>
    /// <exception cref="FormatException">Die Datei ist nicht lesbar.</exception>
    public async Task<Importergebnis> AusfuehrenAsync(Guid laufId, bool probelauf, string? benutzer, CancellationToken ct)
    {
        var lauf = await db.Importlaeufe.AsNoTracking().SingleAsync(l => l.Id == laufId, ct);
        if (lauf.Status != ImportlaufStatus.Vorbereitet)
        {
            throw new InvalidOperationException("Der Import ist schon abgeschlossen.");
        }

        var zuordnung = ZuordnungLesen(lauf.Zuordnung);
        var fehlend = Felder(lauf.Ziel).Where(f => f.Pflicht && !zuordnung.ContainsKey(f.Schluessel)).Select(f => f.Bezeichnung).ToList();
        if (fehlend.Count > 0)
        {
            throw new InvalidOperationException($"Bitte zuordnen: {string.Join(", ", fehlend)}.");
        }

        var tabelle = Tabellenleser.Lesen(lauf.Inhalt, lauf.Dateiname);
        var index = zuordnung.Where(z => tabelle.Spalten.Contains(z.Value)).ToDictionary(z => z.Key, z => tabelle.Spalten.ToList().IndexOf(z.Value));
        var zeilen = tabelle.Zeilen
            .Select((z, i) => (Nummer: i + 2, Werte: (IReadOnlyDictionary<string, string?>)index.ToDictionary(x => x.Key, x => (string?)NullWennLeer(z[x.Value]))))
            .ToList();

        await using var transaktion = probelauf ? null : await db.Database.BeginTransactionAsync(ct);
        var ergebnis = lauf.Ziel switch
        {
            Importziel.Personen => await new Personenimport(db, Heute()).AusfuehrenAsync(zeilen, ct),
            Importziel.Qualifikationen => await new Qualifikationsimport(db).AusfuehrenAsync(zeilen, ct),
            Importziel.Geraete => await new Geraeteimport(db).AusfuehrenAsync(zeilen, ct),
            Importziel.Atemschutz => await new Atemschutzimport(db).AusfuehrenAsync(zeilen, ct),
            Importziel.Einsaetze => await new Einsatzimport(db).AusfuehrenAsync(zeilen, ct),
            Importziel.Einsatzteilnahmen => await new Teilnahmeimport(db).AusfuehrenAsync(zeilen, ct),
            _ => throw new InvalidOperationException("Unbekanntes Ziel."),
        };

        if (probelauf)
        {
            db.ChangeTracker.Clear();
        }
        else
        {
            await db.SaveChangesAsync(ct);
        }

        var gespeichert = await db.Importlaeufe.SingleAsync(l => l.Id == laufId, ct);
        gespeichert.Zeilen = ergebnis.Zeilen;
        gespeichert.Angelegt = ergebnis.Angelegt;
        gespeichert.Aktualisiert = ergebnis.Aktualisiert;
        gespeichert.Fehlerhaft = ergebnis.Fehlerhaft;
        gespeichert.Meldungen = JsonSerializer.Serialize(ergebnis.Meldungen, Json);
        if (probelauf)
        {
            gespeichert.ProbelaufAm = zeit.GetUtcNow();
        }
        else
        {
            gespeichert.Status = ImportlaufStatus.Uebernommen;
            gespeichert.UebernommenAm = zeit.GetUtcNow();
            gespeichert.UebernommenVon = benutzer;
            gespeichert.Inhalt = [];
            gespeichert.Meldungen = MeldungenOhneWerte(gespeichert.Meldungen);
        }

        await db.SaveChangesAsync(ct);
        if (transaktion is not null)
        {
            await transaktion.CommitAsync(ct);
        }

        return ergebnis;
    }

    private DateOnly Heute() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit.GetUtcNow(), Fevad.Domain.Gemeinsam.Ortszeit.Berlin).DateTime);

    private static string? NullWennLeer(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>Zählt Ergebnisse und sammelt Meldungen eines Laufs.</summary>
internal sealed class Zaehler
{
    public int Zeilen;
    public int Angelegt;
    public int Aktualisiert;
    public int Fehlerhaft;
    public List<Importmeldung> Meldungen { get; } = [];

    /// <summary>Merkt einen Fehler der Zeile.</summary>
    public void Fehler(int zeile, string text)
    {
        Fehlerhaft++;
        Meldungen.Add(new(zeile, text));
    }

    /// <summary>Das Ergebnis.</summary>
    public Importergebnis Ergebnis() => new(Zeilen, Angelegt, Aktualisiert, Fehlerhaft, Meldungen);
}

/// <summary>Prüft Textlängen gegen das Datenmodell, damit eine zu lange Zelle nur ihre Zeile und nicht den ganzen Import scheitern lässt.</summary>
internal static class Laengen
{
    /// <summary>Meldungen für Werte, die länger sind als die Spalte in der Datenbank.</summary>
    /// <typeparam name="T">Die Entität, in die die Werte geschrieben werden.</typeparam>
    /// <param name="db">Kontext mit dem Datenmodell.</param>
    /// <param name="werte">Werte der Zeile je Zielfeld.</param>
    /// <param name="ziel">Ziel für die Feldbezeichnungen.</param>
    /// <param name="felder">Zielfeld und Eigenschaft der Entität; ohne Eigenschaft gilt der Name des Zielfelds.</param>
    /// <returns>Eine Meldung je zu langem Wert.</returns>
    public static IEnumerable<string> Pruefen<T>(AppDbContext db, IReadOnlyDictionary<string, string?> werte, Importziel ziel, params (string Feld, string? Eigenschaft)[] felder)
    {
        var typ = db.Model.FindEntityType(typeof(T))!;
        foreach (var (feld, eigenschaft) in felder)
        {
            if (werte.GetValueOrDefault(feld) is { } text && typ.FindProperty(eigenschaft ?? feld)?.GetMaxLength() is { } max && text.Length > max)
            {
                var name = Importdienst.Felder(ziel).FirstOrDefault(f => f.Schluessel == feld)?.Bezeichnung ?? feld;
                yield return $"{name} länger als {max} Zeichen.";
            }
        }
    }
}

/// <summary>Sucht Katalogeinträge nach Code, Kurzbezeichnung oder Bezeichnung, unabhängig von Schreibweise und Umlauten.</summary>
internal sealed class Katalogsuche<T>(IEnumerable<T> eintraege) where T : Fevad.Domain.Gemeinsam.Katalogeintrag
{
    private readonly Dictionary<string, T> _nachSchluessel = eintraege
        .SelectMany(e => new[] { e.Code, e.Kurzbezeichnung, e.Bezeichnung }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => (Schluessel: Altwert.Schluessel(s), Eintrag: e)))
        .GroupBy(x => x.Schluessel)
        .ToDictionary(g => g.Key, g => g.First().Eintrag);

    /// <summary>Der passende Eintrag oder null.</summary>
    public T? Finden(string? text) => _nachSchluessel.GetValueOrDefault(Altwert.Schluessel(text));
}

/// <summary>Personen anlegen oder ergänzen; erkannt an Personalnummer oder MP-Feuer-Kennung.</summary>
internal sealed class Personenimport(AppDbContext db, DateOnly heute)
{
    /// <summary>Verarbeitet alle Zeilen.</summary>
    public async Task<Importergebnis> AusfuehrenAsync(IReadOnlyList<(int Nummer, IReadOnlyDictionary<string, string?> Werte)> zeilen, CancellationToken ct)
    {
        var mitglieder = await db.Mitglieder.Include(m => m.Mitgliedschaften).Include(m => m.Dienstgrade).ToListAsync(ct);
        var nachNummer = mitglieder.ToDictionary(m => m.Personalnummer, StringComparer.OrdinalIgnoreCase);
        var nachMp = mitglieder.Where(m => m.LegacyMpFeuerId is not null).GroupBy(m => m.LegacyMpFeuerId!).ToDictionary(g => g.Key, g => g.First());
        var abteilungen = new Katalogsuche<Abteilung>(await db.Abteilungen.ToListAsync(ct));
        var dienstgrade = new Katalogsuche<Dienstgrad>(await db.Dienstgrade.ToListAsync(ct));
        var z = new Zaehler();
        var neu = new HashSet<Guid>();

        foreach (var (nummer, w) in zeilen)
        {
            z.Zeilen++;
            var fehler = new List<string>();
            var pn = w.GetValueOrDefault("Personalnummer");
            if (pn is null)
            {
                z.Fehler(nummer, "Personalnummer fehlt.");
                continue;
            }

            if (pn.Length > 20)
            {
                fehler.Add("Personalnummer länger als 20 Zeichen.");
            }

            DateOnly? Datum(string feld, string name)
            {
                var text = w.GetValueOrDefault(feld);
                if (text is null)
                {
                    return null;
                }

                if (Altwert.Datum(text, out var d))
                {
                    return d;
                }

                fehler.Add($"{name} „{text}“ ist kein Datum.");
                return null;
            }

            var geburt = Datum("Geburtsdatum", "Geburtsdatum");
            var eintritt = Datum("Eintritt", "Eintritt");
            var austritt = Datum("Austritt", "Austritt");
            var dienstgradAb = Datum("DienstgradAb", "Dienstgrad seit");
            var abteilung = w.GetValueOrDefault("Abteilung") is { } a ? abteilungen.Finden(a) : null;
            if (w.GetValueOrDefault("Abteilung") is { } at && abteilung is null)
            {
                fehler.Add($"Abteilung „{at}“ gibt es in FEVAD nicht.");
            }

            if (abteilung is not null && eintritt is null && w.GetValueOrDefault("Eintritt") is null)
            {
                fehler.Add("Eintritt fehlt.");
            }

            if (eintritt is { } e1 && austritt is { } a1 && a1 < e1)
            {
                fehler.Add("Austritt liegt vor dem Eintritt.");
            }

            var dienstgrad = w.GetValueOrDefault("Dienstgrad") is { } dg ? dienstgrade.Finden(dg) : null;
            if (w.GetValueOrDefault("Dienstgrad") is { } dgt && dienstgrad is null)
            {
                fehler.Add($"Dienstgrad „{dgt}“ gibt es in FEVAD nicht.");
            }

            Geschlecht? geschlecht = null;
            if (w.GetValueOrDefault("Geschlecht") is { } g)
            {
                geschlecht = Altwert.Schluessel(g) switch
                {
                    "m" or "maennlich" or "herr" => Geschlecht.Maennlich,
                    "w" or "weiblich" or "frau" => Geschlecht.Weiblich,
                    "d" or "divers" => Geschlecht.Divers,
                    _ => null,
                };
                if (geschlecht is null)
                {
                    fehler.Add($"Geschlecht „{g}“ ist nicht m, w oder d.");
                }
            }

            bool? atemschutz = null;
            if (w.GetValueOrDefault("AtemschutzAktiv") is { } agt)
            {
                if (Altwert.JaNein(agt, out var ja))
                {
                    atemschutz = ja;
                }
                else
                {
                    fehler.Add($"Atemschutz „{agt}“ ist nicht ja oder nein.");
                }
            }

            var mp = w.GetValueOrDefault("MpFeuerId");
            var mitglied = nachNummer.GetValueOrDefault(pn) ?? (mp is null ? null : nachMp.GetValueOrDefault(mp));
            var istNeu = mitglied is null;
            if (istNeu && (w.GetValueOrDefault("Nachname") is null || w.GetValueOrDefault("Vorname") is null || geburt is null))
            {
                fehler.Add("Neue Person braucht Nachname, Vorname und Geburtsdatum.");
            }

            fehler.AddRange(Laengen.Pruefen<Mitglied>(db, w, Importziel.Personen, ("MpFeuerId", nameof(Mitglied.LegacyMpFeuerId)), ("Vorname", null),
                ("Nachname", null), ("Strasse", null), ("Plz", null), ("Ort", null), ("TelefonMobil", null), ("TelefonFestnetz", null), ("Email", null)));

            if (fehler.Count > 0)
            {
                z.Fehler(nummer, string.Join(" ", fehler));
                continue;
            }

            if (mitglied is null)
            {
                mitglied = new Mitglied { Personalnummer = pn, Vorname = w["Vorname"]!, Nachname = w["Nachname"]!, Geburtsdatum = geburt!.Value };
                db.Mitglieder.Add(mitglied);
                nachNummer[pn] = mitglied;
                neu.Add(mitglied.Id);
            }

            if (mp is not null)
            {
                mitglied.LegacyMpFeuerId = mp;
                nachMp[mp] = mitglied;
            }

            mitglied.Vorname = w.GetValueOrDefault("Vorname") ?? mitglied.Vorname;
            mitglied.Nachname = w.GetValueOrDefault("Nachname") ?? mitglied.Nachname;
            mitglied.Geburtsdatum = geburt ?? mitglied.Geburtsdatum;
            mitglied.Geschlecht = geschlecht ?? mitglied.Geschlecht;
            mitglied.Strasse = w.GetValueOrDefault("Strasse") ?? mitglied.Strasse;
            mitglied.Plz = w.GetValueOrDefault("Plz") ?? mitglied.Plz;
            mitglied.Ort = w.GetValueOrDefault("Ort") ?? mitglied.Ort;
            mitglied.TelefonMobil = w.GetValueOrDefault("TelefonMobil") ?? mitglied.TelefonMobil;
            mitglied.TelefonFestnetz = w.GetValueOrDefault("TelefonFestnetz") ?? mitglied.TelefonFestnetz;
            mitglied.Email = w.GetValueOrDefault("Email") ?? mitglied.Email;
            mitglied.AtemschutzAktiv = atemschutz ?? mitglied.AtemschutzAktiv;

            if (abteilung is not null && !mitglied.Mitgliedschaften.Any(s => s.AbteilungId == abteilung.Id && s.GueltigAb == eintritt))
            {
                mitglied.Mitgliedschaften.Add(new Mitgliedschaft
                {
                    AbteilungId = abteilung.Id, GueltigAb = eintritt!.Value, GueltigBis = austritt,
                    Status = austritt is { } bis && bis < heute ? MitgliedStatus.Ausgeschieden : MitgliedStatus.Aktiv,
                    Bemerkung = "aus Altdatenimport",
                });
            }

            if (dienstgrad is not null && !mitglied.Dienstgrade.Any(d => d.DienstgradId == dienstgrad.Id))
            {
                mitglied.Dienstgrade.Add(new DienstgradZuordnung
                {
                    DienstgradId = dienstgrad.Id, Ab = dienstgradAb ?? eintritt ?? mitglied.Mitgliedschaften.Select(s => (DateOnly?)s.GueltigAb).Min() ?? heute,
                    Bemerkung = "aus Altdatenimport",
                });
            }

            if (istNeu)
            {
                z.Angelegt++;
            }
            else if (!neu.Contains(mitglied.Id))
            {
                z.Aktualisiert++;
            }
        }

        return z.Ergebnis();
    }
}

/// <summary>Lehrgänge und Qualifikationen je Person eintragen; doppelte (gleicher Lehrgang am gleichen Tag) werden nur ergänzt.</summary>
internal sealed class Qualifikationsimport(AppDbContext db)
{
    /// <summary>Verarbeitet alle Zeilen.</summary>
    public async Task<Importergebnis> AusfuehrenAsync(IReadOnlyList<(int Nummer, IReadOnlyDictionary<string, string?> Werte)> zeilen, CancellationToken ct)
    {
        var mitglieder = await db.Mitglieder.Select(m => new { m.Id, m.Personalnummer, m.LegacyMpFeuerId }).ToListAsync(ct);
        var nachNummer = mitglieder.ToDictionary(m => m.Personalnummer, m => m.Id, StringComparer.OrdinalIgnoreCase);
        var nachMp = mitglieder.Where(m => m.LegacyMpFeuerId is not null).GroupBy(m => m.LegacyMpFeuerId!).ToDictionary(g => g.Key, g => g.First().Id);
        var katalog = new Katalogsuche<Qualifikation>(await db.Qualifikationen.ToListAsync(ct));
        var vorhanden = await db.QualifikationNachweise.ToListAsync(ct);
        var z = new Zaehler();

        foreach (var (nummer, w) in zeilen)
        {
            z.Zeilen++;
            var fehler = new List<string>();
            var pn = w.GetValueOrDefault("Personalnummer");
            Guid? mitgliedId = pn is null ? null : nachNummer.TryGetValue(pn, out var id) ? id : nachMp.TryGetValue(pn, out var mpId) ? mpId : null;
            if (mitgliedId is null)
            {
                fehler.Add(pn is null ? "Personalnummer fehlt." : $"Personalnummer „{pn}“ gibt es in FEVAD nicht.");
            }

            var text = w.GetValueOrDefault("Lehrgang");
            var qualifikation = katalog.Finden(text);
            if (qualifikation is null)
            {
                fehler.Add(text is null ? "Lehrgang fehlt." : $"Lehrgang „{text}“ steht nicht im Katalog.");
            }

            if (!Altwert.Datum(w.GetValueOrDefault("ErworbenAm"), out var erworben))
            {
                fehler.Add(w.GetValueOrDefault("ErworbenAm") is { } e ? $"Erworben am „{e}“ ist kein Datum." : "Erworben am fehlt.");
            }

            DateOnly? gueltigBis = null;
            if (w.GetValueOrDefault("GueltigBis") is { } gb)
            {
                if (Altwert.Datum(gb, out var bis))
                {
                    gueltigBis = bis;
                }
                else
                {
                    fehler.Add($"Gültig bis „{gb}“ ist kein Datum.");
                }
            }

            fehler.AddRange(Laengen.Pruefen<QualifikationNachweis>(db, w, Importziel.Qualifikationen, ("Lehrgangsort", null), ("Bemerkung", null)));
            if (fehler.Count > 0)
            {
                z.Fehler(nummer, string.Join(" ", fehler));
                continue;
            }

            gueltigBis ??= qualifikation!.GueltigkeitMonate is { } monate ? erworben.AddMonths(monate) : null;
            var nachweis = vorhanden.FirstOrDefault(n => n.MitgliedId == mitgliedId && n.QualifikationId == qualifikation!.Id && n.ErworbenAm == erworben);
            if (nachweis is null)
            {
                nachweis = new QualifikationNachweis
                {
                    MitgliedId = mitgliedId!.Value, QualifikationId = qualifikation!.Id, ErworbenAm = erworben, GueltigBis = gueltigBis,
                    Lehrgangsort = w.GetValueOrDefault("Lehrgangsort"), Bemerkung = w.GetValueOrDefault("Bemerkung") ?? "aus Altdatenimport",
                };
                db.QualifikationNachweise.Add(nachweis);
                vorhanden.Add(nachweis);
                z.Angelegt++;
            }
            else if (nachweis.GueltigBis != gueltigBis || (w.GetValueOrDefault("Lehrgangsort") is { } ort && nachweis.Lehrgangsort != ort))
            {
                nachweis.GueltigBis = gueltigBis;
                nachweis.Lehrgangsort = w.GetValueOrDefault("Lehrgangsort") ?? nachweis.Lehrgangsort;
                z.Aktualisiert++;
            }
        }

        return z.Ergebnis();
    }
}
