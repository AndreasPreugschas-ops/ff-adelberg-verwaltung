using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fevad.Infrastructure.Datenschutz;

/// <summary>
/// Auskunft nach Art. 15 DSGVO: alles, was FEVAD zu einem Konto und dem verknüpften Mitglied speichert.
/// Die Abschnitte werden aus dem Datenmodell abgeleitet (jede Tabelle mit Verweis auf das Mitglied),
/// damit neue Module ohne Zutun in der Auskunft erscheinen.
/// </summary>
public sealed partial class Selbstauskunft(AppDbContext db, UserManager<Benutzer> benutzer)
{
    /// <summary>Ein Abschnitt der Auskunft, je Tabelle einer.</summary>
    /// <param name="Titel">Überschrift.</param>
    /// <param name="Spalten">Spaltenüberschriften.</param>
    /// <param name="Zeilen">Werte je Datensatz.</param>
    public sealed record Abschnitt(string Titel, IReadOnlyList<string> Spalten, IReadOnlyList<IReadOnlyList<object?>> Zeilen);

    /// <summary>Überschriften und Reihenfolge bekannter Tabellen; unbekannte folgen mit ihrem Klassennamen.</summary>
    private static readonly (string Typ, string Titel)[] Titel =
    [
        ("Mitgliedschaft", "Mitgliedschaften"),
        ("Erziehungsberechtigter", "Erziehungsberechtigte"),
        ("Antrag", "Anträge (Aufnahme, Übertritt, Volljährigkeit)"),
        ("Einwilligung", "Einwilligungen (Fotos, WhatsApp, Jugendarbeit)"),
        ("Loeschanfrage", "Löschanfragen"),
        ("DienstgradZuordnung", "Dienstgrade"),
        ("FunktionZuordnung", "Funktionen und Ämter"),
        ("GruppenMitgliedschaft", "Gruppen"),
        ("QualifikationNachweis", "Lehrgänge und Qualifikationen"),
        ("Nachweis", "Wiederkehrende Nachweise"),
        ("AtemschutzVorsorge", "Atemschutz: arbeitsmedizinische Vorsorge (nur Status und Frist)"),
        ("AtemschutzNachweis", "Atemschutz: Übungen und Einsätze"),
        ("AtemschutzSperre", "Atemschutz: Abmeldungen „vorübergehend nicht einsetzbar“"),
        ("Anwesenheit", "Anwesenheit bei Diensten"),
        ("Anwesenheitsjahressumme", "Anwesenheit bei Diensten (Jahressummen nach der Verdichtung)"),
        ("Rueckmeldung", "Zu- und Absagen zu Diensten"),
        ("EinsatzTeilnahme", "Einsatzteilnahmen"),
        ("Ehrung", "Ehrungen"),
        ("Unfall", "Dienstunfälle"),
        ("Pruefhinweis", "Prüfergebnisse (Beförderung, Ehrung, Funktion)"),
        ("AbrechnungsPosten", "Entschädigung"),
        ("Ausstattung", "Ausgegebene Kleidung und Ausrüstung"),
        ("Kleiderkammervorgang", "Kleiderkammer: Vorgänge"),
        ("Groessenangabe", "Größen"),
        ("Tauschanforderung", "Tauschanforderungen an die Kleiderkammer"),
        ("Nachweiseinreichung", "Eingereichte Nachweise"),
        ("SitzungTeilnahme", "Teilnahme an Sitzungen"),
        ("WahlBewerber", "Kandidaturen bei Wahlen"),
    ];

    private static readonly HashSet<string> Technisch =
    [
        nameof(Entitaet.Id), nameof(Entitaet.ErstelltVon), nameof(Entitaet.GeaendertAm), nameof(Entitaet.GeaendertVon),
        nameof(Entitaet.GeloeschtAm), "Version",
    ];

    /// <summary>Überschriften, die sich nicht aus dem Eigenschaftsnamen ergeben.</summary>
    private static readonly Dictionary<string, string> Namen = new()
    {
        [nameof(Entitaet.ErstelltAm)] = "in FEVAD angelegt",
        [nameof(Mitglied.Email)] = "E-Mail-Adresse",
        [nameof(Erziehungsberechtigter.KontoId)] = "Elternkonto",
        [nameof(Erziehungsberechtigter.KontoZugeordnetAm)] = "Elternkonto zugeordnet am",
        [nameof(Erziehungsberechtigter.KeineKontozuordnung)] = "Sperre: kein Elternkonto",
        [nameof(Mitglied.Plz)] = "PLZ",
        [nameof(Mitglied.Strasse)] = "Straße",
        [nameof(Mitglied.TelefonMobil)] = "Mobil",
        [nameof(Mitglied.TelefonFestnetz)] = "Telefon privat",
        [nameof(Mitglied.NotfallkontaktName)] = "Notfallkontakt",
        [nameof(Mitglied.NotfallkontaktTelefon)] = "Telefon Notfallkontakt",
        [nameof(Mitglied.DjfAusweisnummer)] = "DJF-Ausweisnummer",
        [nameof(Einwilligung.JugendlicherZugestimmt)] = "Zustimmung des Jugendlichen",
        [nameof(Einwilligung.DurchName)] = "erklärt von",
        [nameof(Mitglied.DjfAusweisAm)] = "DJF-Ausweis ausgegeben am",
        [nameof(Mitglied.Abholregelung)] = "Heimweg nach dem Dienst",
        [nameof(Mitglied.NotfallmappeAm)] = "Notfallmappe abgegeben am",
        [nameof(Mitglied.ArbeitgeberStrasse)] = "Straße Arbeitgeber",
        [nameof(Mitglied.ArbeitgeberPlz)] = "PLZ Arbeitgeber",
        [nameof(Mitglied.Arbeitsort)] = "Ort Arbeitgeber",
        [nameof(Mitglied.ArbeitgeberTelefon)] = "Telefon dienstlich",
        [nameof(Mitglied.ArbeitgeberMobil)] = "Mobil dienstlich",
        [nameof(Mitglied.ArbeitgeberEmail)] = "E-Mail dienstlich",
        [nameof(Mitgliedschaft.ProbezeitBis)] = "Probezeit bis",
        [nameof(Mitglied.LegacyMpFeuerId)] = "Kennung aus MP-FEUER",
        [nameof(Mitglied.Fe2ExternalId)] = "Kennung für alamos FE2",
        [nameof(Mitglied.Fe2SyncAktiv)] = "Abgleich mit alamos FE2",
    };

    /// <summary>Eigenschaften, über die ein verknüpfter Datensatz (Katalog, Dienst, Sitzung …) lesbar benannt wird.</summary>
    private static readonly string[] Bezeichner = ["Bezeichnung", "Titel", "Thema", "Name", "Stichwort", "Einsatznummer", "Dateiname"];

    /// <summary>Lädt alle gespeicherten Daten zum Konto und zum verknüpften Mitglied.</summary>
    /// <param name="benutzerId">Konto, für das die Auskunft erstellt wird.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Abschnitte in fester Reihenfolge.</returns>
    public async Task<IReadOnlyList<Abschnitt>> LadenAsync(Guid benutzerId, CancellationToken ct = default)
    {
        var abschnitte = new List<Abschnitt>();
        if (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == benutzerId, ct) is not { } konto)
        {
            return abschnitte;
        }

        var rollen = await benutzer.GetRolesAsync(konto);
        abschnitte.Add(new Abschnitt("Benutzerkonto",
            ["Anmeldename", "E-Mail-Adresse", "Telefon", "Rollen", "Zwei-Faktor-Anmeldung", "Kalender-Abo eingerichtet", "Letzte Anmeldung"],
            [[konto.UserName, konto.Email, konto.PhoneNumber, string.Join(", ", rollen.Select(Rollen.Bezeichnung)), konto.TwoFactorEnabled,
                konto.KalenderToken is not null, konto.LetzteAnmeldungAm]]));

        abschnitte.AddRange(await AbschnitteAsync(typeof(Benutzer), benutzerId, ct));

        if (konto.MitgliedId is { } mitgliedId)
        {
            abschnitte.AddRange(await AbschnitteAsync(typeof(Mitglied), mitgliedId, ct));
        }

        abschnitte.AddRange(await KommunikationAsync(benutzerId, konto.MitgliedId, ct));
        return abschnitte;
    }

    /// <summary>
    /// Empfang von Mitteilungen und Rundfragen (V42) mit Lesestand und eigenen Antworten. Die Empfängertabellen sind keine
    /// <see cref="Entitaet"/> und fallen deshalb nicht unter die automatische Ableitung. Zählt das Konto oder, bei Elternpost,
    /// das Mitglied als Empfänger. Antworten auf anonyme Umfragen lassen sich keiner Person zuordnen und fehlen deshalb.
    /// </summary>
    private async Task<IEnumerable<Abschnitt>> KommunikationAsync(Guid kontoId, Guid? mitgliedId, CancellationToken ct)
    {
        var abschnitte = new List<Abschnitt>();
        var mitteilungen = await db.MitteilungEmpfaenger.AsNoTracking()
            .Where(e => e.KontoId == kontoId || (mitgliedId != null && e.MitgliedId == mitgliedId))
            .Join(db.Mitteilungen.IgnoreQueryFilters(), e => e.MitteilungId, m => m.Id, (e, m) => new { m.Titel, m.VeroeffentlichtAm, e.GelesenAm, e.BestaetigtAm })
            .OrderBy(x => x.VeroeffentlichtAm)
            .ToListAsync(ct);
        if (mitteilungen.Count > 0)
        {
            abschnitte.Add(new Abschnitt("Mitteilungen (Empfang und Lesestand)", ["Titel", "Veröffentlicht am", "Gelesen am", "Bestätigt am"],
                mitteilungen.Select(x => (IReadOnlyList<object?>)[x.Titel, x.VeroeffentlichtAm, x.GelesenAm, x.BestaetigtAm]).ToList()));
        }

        var rundfragen = await db.RundfrageEmpfaenger.AsNoTracking()
            .Where(e => e.KontoId == kontoId || (mitgliedId != null && e.MitgliedId == mitgliedId))
            .Join(db.Rundfragen.IgnoreQueryFilters(), e => e.RundfrageId, r => r.Id, (e, r) => new { Empfaenger = e, Rundfrage = r })
            .OrderBy(x => x.Rundfrage.VerschicktAm)
            .ToListAsync(ct);
        if (rundfragen.Count > 0)
        {
            var ids = rundfragen.Select(x => x.Empfaenger.Id).ToList();
            var antworten = await db.RundfrageAntworten.AsNoTracking()
                .Where(a => a.EmpfaengerId != null && ids.Contains(a.EmpfaengerId.Value))
                .ToDictionaryAsync(a => a.EmpfaengerId!.Value, ct);
            abschnitte.Add(new Abschnitt("Rundfragen und Umfragen", ["Frage", "Verschickt am", "Beantwortet am", "Antwort", "Bemerkung"],
                rundfragen.Select(x =>
                {
                    var r = x.Rundfrage;
                    var antwort = r.Anonym ? (x.Empfaenger.BeantwortetAm is null ? null : "anonym, keiner Person zugeordnet")
                        : antworten.TryGetValue(x.Empfaenger.Id, out var a)
                            ? string.Join(", ", a.Auswahl.Where(i => i >= 0 && i < r.Moeglichkeiten.Count).Select(i => r.Moeglichkeiten[i]))
                            : null;
                    var bemerkung = !r.Anonym && antworten.TryGetValue(x.Empfaenger.Id, out var b) ? b.Bemerkung : null;
                    return (IReadOnlyList<object?>)[r.Titel, r.VerschicktAm, x.Empfaenger.BeantwortetAm, antwort, bemerkung];
                }).ToList()));
        }

        return abschnitte;
    }

    /// <summary>
    /// Alle gespeicherten Daten zu einem Datensatz beliebiger Art, etwa für das Archivpaket (V65): der Datensatz selbst und alle Tabellen,
    /// die per Fremdschlüssel auf ihn verweisen.
    /// </summary>
    /// <param name="typ">Klasse des Datensatzes, z. B. <see cref="Mitglied"/>.</param>
    /// <param name="id">Schlüssel des Datensatzes.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Die Abschnitte, Stammdaten zuerst.</returns>
    public async Task<IReadOnlyList<Abschnitt>> DatensatzAsync(Type typ, Guid id, CancellationToken ct = default)
    {
        var abschnitte = new List<Abschnitt>();
        if (typ != typeof(Mitglied))
        {
            abschnitte.Add(await TabelleAsync(typ, "Stammdaten", new Dictionary<string, object> { ["Id"] = id }, ct));
        }

        abschnitte.AddRange(await AbschnitteAsync(typ, id, ct));
        return abschnitte;
    }

    /// <summary>Alle Zeilen einer Tabelle, deren Spalten die Werte haben, aufbereitet wie die Auskunft.</summary>
    /// <param name="typ">Klasse der Tabelle.</param>
    /// <param name="titel">Überschrift des Abschnitts.</param>
    /// <param name="bedingungen">Eigenschaft und gesuchter Wert; alle müssen zutreffen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Abschnitt.</returns>
    public async Task<Abschnitt> TabelleAsync(Type typ, string titel, IReadOnlyDictionary<string, object> bedingungen, CancellationToken ct = default)
    {
        var methode = typeof(Selbstauskunft).GetMethod(nameof(GefiltertAsync), BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(typ);
        return await (Task<Abschnitt>)methode.Invoke(this, [titel, bedingungen, ct])!;
    }

    private Task<Abschnitt> GefiltertAsync<T>(string titel, IReadOnlyDictionary<string, object> bedingungen, CancellationToken ct)
        where T : class
    {
        var typ = db.Model.FindEntityType(typeof(T))!;
        var e = System.Linq.Expressions.Expression.Parameter(typeof(T), "e");
        System.Linq.Expressions.Expression rumpf = System.Linq.Expressions.Expression.Constant(true);
        foreach (var (spalte, wert) in bedingungen)
        {
            var art = typ.FindProperty(spalte)!.ClrType;
            rumpf = System.Linq.Expressions.Expression.AndAlso(rumpf, System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Call(typeof(EF), nameof(EF.Property), [art], e, System.Linq.Expressions.Expression.Constant(spalte)),
                System.Linq.Expressions.Expression.Constant(wert, art)));
        }

        return AbschnittAsync(typ, titel, System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(rumpf, e), null, false, ct);
    }

    /// <summary>Der Datensatz selbst (beim Mitglied) und alle Tabellen, die per Fremdschlüssel auf ihn verweisen.</summary>
    private async Task<IEnumerable<Abschnitt>> AbschnitteAsync(Type haupttyp, Guid id, CancellationToken ct)
    {
        var abschnitte = new List<(int Rang, Abschnitt Abschnitt)>();
        if (haupttyp == typeof(Mitglied))
        {
            var mitglied = db.Model.FindEntityType(typeof(Mitglied))!;
            var stammdaten = await AbschnittAsync<Mitglied>(mitglied, "Stammdaten", m => m.Id == id, null, false, ct);
            abschnitte.Add((-1, stammdaten));
        }

        foreach (var typ in db.Model.GetEntityTypes().Where(t => !t.IsOwned() && t.ClrType != haupttyp && typeof(Entitaet).IsAssignableFrom(t.ClrType)))
        {
            foreach (var fk in typ.GetForeignKeys().Where(f => f.PrincipalEntityType.ClrType == haupttyp && f.Properties.Count == 1))
            {
                var name = typ.ClrType.Name;
                var spalte = fk.Properties[0].Name;

                // Gehört der Datensatz der Person (MitgliedId/BenutzerId), kommen alle Felder. Ist sie nur beteiligt
                // (Protokollführer, Einsatzleiter, Verantwortlicher …), nur Bezeichnung und Datum: der Inhalt
                // gehört der Feuerwehr und ist durch die Rechte der jeweiligen Seite geschützt.
                var geteilt = spalte != haupttyp.Name + "Id";
                var rang = geteilt ? -1 : Array.FindIndex(Titel, t => t.Typ == name);
                var titel = rang >= 0 ? Titel[rang].Titel
                    : geteilt ? $"{Beschriftung(name)} (als {Beschriftung(spalte.EndsWith("Id", StringComparison.Ordinal) ? spalte[..^2] : spalte)})"
                    : Beschriftung(name);

                var methode = typeof(Selbstauskunft).GetMethod(nameof(VerweisendeAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(typ.ClrType);
                var abschnitt = await (Task<Abschnitt>)methode.Invoke(this, [typ, fk.Properties[0], titel, id, geteilt, ct])!;
                if (abschnitt.Zeilen.Count > 0)
                {
                    abschnitte.Add((rang < 0 ? Titel.Length : rang, abschnitt));
                }
            }
        }

        return abschnitte.OrderBy(a => a.Rang).ThenBy(a => a.Abschnitt.Titel, StringComparer.CurrentCulture).Select(a => a.Abschnitt);
    }

    private Task<Abschnitt> VerweisendeAsync<T>(IEntityType typ, IProperty fremdschluessel, string titel, Guid id, bool geteilt, CancellationToken ct)
        where T : class
    {
        var spalte = fremdschluessel.Name;
        return fremdschluessel.ClrType == typeof(Guid?)
            ? AbschnittAsync<T>(typ, titel, e => EF.Property<Guid?>(e, spalte) == id, fremdschluessel, geteilt, ct)
            : AbschnittAsync<T>(typ, titel, e => EF.Property<Guid>(e, spalte) == id, fremdschluessel, geteilt, ct);
    }

    private async Task<Abschnitt> AbschnittAsync<T>(IEntityType typ, string titel, System.Linq.Expressions.Expression<Func<T, bool>> filter,
        IProperty? fremdschluessel, bool geteilt, CancellationToken ct)
        where T : class
    {
        // Ausgeblendete (soft-gelöschte) Datensätze sind noch gespeichert und gehören zur Auskunft.
        var zeilen = await db.Set<T>().IgnoreQueryFilters().AsNoTracking().Where(filter).ToListAsync(ct);
        var properties = typ.GetProperties()
            .Where(p => p.PropertyInfo is not null && !Technisch.Contains(p.Name) && p != fremdschluessel && p.ClrType != typeof(byte[]))
            .Where(p => !geteilt || Bezeichner.Contains(p.Name)
                || Nullable.GetUnderlyingType(p.ClrType) is { } t && (t == typeof(DateOnly) || t == typeof(DateTimeOffset))
                || p.ClrType == typeof(DateOnly) || p.ClrType == typeof(DateTimeOffset))
            // Reihenfolge wie im Code, „angelegt am“ aus der Basisklasse zuletzt.
            .OrderBy(p => p.PropertyInfo!.DeclaringType == typeof(Entitaet))
            .ThenBy(p => p.PropertyInfo!.MetadataToken)
            .ToList();

        // Verweise auf andere Tabellen durch deren Bezeichnung ersetzen; ohne lesbaren Namen entfällt die Spalte.
        var texte = new Dictionary<IProperty, IReadOnlyDictionary<Guid, string>>();
        foreach (var fk in typ.GetForeignKeys().Where(f => f.Properties.Count == 1 && f.Properties[0] != fremdschluessel))
        {
            var p = fk.Properties[0];
            if (!properties.Contains(p))
            {
                continue;
            }

            var bezeichner = Bezeichner.Select(fk.PrincipalEntityType.FindProperty).FirstOrDefault(x => x?.ClrType == typeof(string));
            var ids = zeilen.Select(z => p.PropertyInfo!.GetValue(z)).OfType<Guid>().Distinct().ToList();
            if (bezeichner is null)
            {
                properties.Remove(p);
                continue;
            }

            var methode = typeof(Selbstauskunft).GetMethod(nameof(TexteAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(fk.PrincipalEntityType.ClrType);
            texte[p] = await (Task<IReadOnlyDictionary<Guid, string>>)methode.Invoke(this, [ids, bezeichner.Name, ct])!;
        }

        var spalten = properties.Select(p => Namen.GetValueOrDefault(p.Name)
            ?? p.PropertyInfo!.GetCustomAttribute<DisplayAttribute>()?.GetName()
                ?? Beschriftung(texte.ContainsKey(p) && p.Name.EndsWith("Id", StringComparison.Ordinal) ? p.Name[..^2] : p.Name))
            .ToList();
        for (var i = 0; i < spalten.Count; i++)
        {
            // Gleich lautende Überschriften unterscheiden, der Export braucht eindeutige Namen.
            if (spalten.Take(i).Contains(spalten[i]))
            {
                spalten[i] += $" ({properties[i].Name})";
            }
        }

        var werte = zeilen.Select(z => (IReadOnlyList<object?>)properties.Select(p =>
        {
            var wert = p.PropertyInfo!.GetValue(z);
            return texte.TryGetValue(p, out var t) && wert is Guid g ? t.GetValueOrDefault(g) : wert;
        }).ToList()).ToList();
        return new Abschnitt(titel, spalten, werte);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> TexteAsync<T>(List<Guid> ids, string eigenschaft, CancellationToken ct)
        where T : class
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var zeilen = await db.Set<T>().IgnoreQueryFilters().AsNoTracking()
            .Where(e => ids.Contains(EF.Property<Guid>(e, nameof(Entitaet.Id))))
            .Select(e => new { Id = EF.Property<Guid>(e, nameof(Entitaet.Id)), Text = EF.Property<string>(e, eigenschaft) })
            .ToListAsync(ct);
        return zeilen.ToDictionary(z => z.Id, z => z.Text);
    }

    /// <summary>„GueltigAb“ → „Gültig ab“: Wörter trennen und die im Code ausgeschriebenen Umlaute zurückwandeln.</summary>
    public static string Beschriftung(string name)
    {
        var woerter = Woerter().Split(name).Where(w => w.Length > 0).Select(Umlaute).ToList();
        if (woerter.Count == 0)
        {
            return name;
        }

        return string.Join(' ', woerter.Select((w, i) => i == 0 ? w : w.ToLowerInvariant()));
    }

    private static readonly (string Von, string Nach)[] Ersetzungen =
    [
        ("Aenderung", "Änderung"), ("Ae", "Ä"), ("Oe", "Ö"), ("Ue", "Ü"),
        ("aend", "änd"), ("aeh", "äh"), ("aell", "äll"), ("aess", "äß"), ("aet", "ät"), ("aechst", "ächst"),
        ("oesse", "öße"), ("oess", "öß"), ("oerd", "örd"),
        ("uend", "ünd"), ("uelt", "ült"), ("uess", "üss"), ("uebung", "übung"), ("ueck", "ück"), ("uef", "üf"),
        ("uehr", "ühr"), ("uest", "üst"),
    ];

    private static string Umlaute(string wort)
    {
        foreach (var (von, nach) in Ersetzungen)
        {
            if (von.Length == 2 && char.IsUpper(von[0]))
            {
                // Nur am Wortanfang, sonst würde aus „Feuerwehr“ nichts, aus „Bauer“ aber „Baür“.
                if (wort.StartsWith(von, StringComparison.Ordinal) && wort.Length > 2 && char.IsLower(wort[2]))
                {
                    wort = nach + wort[2..];
                }

                continue;
            }

            wort = wort.Replace(von, nach, StringComparison.Ordinal);
        }

        return wort;
    }

    [GeneratedRegex("(?<=[a-zäöüß])(?=[A-ZÄÖÜ])")]
    private static partial Regex Woerter();
}
