using Fevad.Domain.Ausbildung;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Laufbahn;
using Fevad.Infrastructure.Identitaet;
using Fevad.Infrastructure.Laufbahn;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fevad.Infrastructure.Persistenz.Seed;

/// <summary>Bringt die Datenbank beim Start auf Stand: Migrationen, Rollen, Stammdaten und Rechte.</summary>
public static class DatenbankInitialisierung
{
    /// <summary>Migrationen anwenden, Rollen und Stammdaten anlegen, optional einen ersten Administrator.</summary>
    public static async Task InitialisierenAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var konfiguration = sp.GetRequiredService<IConfiguration>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatenbankInitialisierung));

        if (konfiguration.GetValue("Datenbank:AutoMigrate", true))
        {
            await db.Database.MigrateAsync(ct);
        }

        await RollenAnlegenAsync(sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>());

        await KatalogAnlegenAsync(db, Stammdaten.Abteilungen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Dienstgrade, ct);
        await DienstgradeUmbenennenAsync(db, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Funktionen, ct);
        var neueQualifikationen = await KatalogAnlegenAsync(db, Stammdaten.Qualifikationen, ct);
        await StufenfolgenAnlegenAsync(db, neueQualifikationen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.StatistikKategorien, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Gefahrstoffgruppen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Verletzungsarten, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Dokumentkategorien, ct);
        var neueNachweisarten = await KatalogAnlegenAsync(db, Stammdaten.Nachweisarten, ct);
        await NachweisartenVerknuepfenAsync(db, neueNachweisarten, neueQualifikationen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Geraetearten, ct);
        await KatalogAnlegenAsync(db, Stammdaten.PsaGeraetearten, ct);
        await KatalogAnlegenAsync(db, Stammdaten.AtemschutzGeraetearten, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Foerdergegenstaende, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Haushaltsstellen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Kostenansaetze, ct);
        await BauteilartenAnlegenAsync(db, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Einwilligungsarten, ct);
        await UnterweisungsthemenAnlegenAsync(db, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Ausbildungsthemen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Einsatzfunktionen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Fuehrungspflichten, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Briefvorlagen, ct);

        if (!await db.Organisationen.IgnoreQueryFilters().AnyAsync(ct))
        {
            db.Organisationen.Add(Stammdaten.Organisation);
        }

        // Die Beispielsätze aus Bad Überkingen weichen den Sätzen der Entschädigungssatzung Adelberg 2018 (V35); selbst gepflegte Sätze bleiben.
        await db.Parameter.IgnoreQueryFilters().Where(p => p.Quelle == Stammdaten.FruehereBeispielquelle).ExecuteDeleteAsync(ct);

        foreach (var kostensatz in Stammdaten.Kostensaetze)
        {
            if (!await db.Kostensaetze.IgnoreQueryFilters().AnyAsync(k => k.Art == kostensatz.Art && k.Bezeichnung == kostensatz.Bezeichnung, ct))
            {
                db.Kostensaetze.Add(kostensatz);
            }
        }

        foreach (var parameter in Stammdaten.Parameter)
        {
            if (!await db.Parameter.IgnoreQueryFilters().AnyAsync(p => p.Schluessel == parameter.Schluessel, ct))
            {
                db.Parameter.Add(parameter);
            }
        }

        // Vorläufiger Wert aus der Mustersatzung (#11); bewusst geänderte Werte bleiben.
        var vorlaeufig = await db.Parameter.FirstOrDefaultAsync(p => p.Schluessel == ParameterSchluessel.AusschussEinladungsfristTage
            && p.Wert == "7" && p.Quelle == "Mustersatzung (vorläufig, mit Satzung Adelberg abgleichen)", ct);
        if (vorlaeufig is not null)
        {
            var aktuell = Stammdaten.Parameter.Single(p => p.Schluessel == ParameterSchluessel.AusschussEinladungsfristTage);
            vorlaeufig.Wert = aktuell.Wert;
            vorlaeufig.Quelle = aktuell.Quelle;
        }

        // L38 war vorläufig 12 Monate; Andi hat 24 festgelegt (25.10.2026). Bewusst geänderte Werte bleiben.
        var sperrenLoeschen = await db.Parameter.FirstOrDefaultAsync(p => p.Schluessel == ParameterSchluessel.AtemschutzSperrenLoeschenNachMonate
            && p.Wert == "12" && p.Quelle == "Löschkonzept FEVAD L38 (Vorschlag Datenschutz-Thread, DSB-Freigabe offen)", ct);
        if (sperrenLoeschen is not null)
        {
            var aktuell = Stammdaten.Parameter.Single(p => p.Schluessel == ParameterSchluessel.AtemschutzSperrenLoeschenNachMonate);
            sperrenLoeschen.Wert = aktuell.Wert;
            sperrenLoeschen.Quelle = aktuell.Quelle;
        }

        // Jahreskreis (V29, V30): Haushaltsanmeldung und Z-Feu-Antrag haben eigene Seiten; bewusst geänderte Vorlagen bleiben.
        foreach (var (code, vorlage) in new[] { ("HH", "/Haushalt/Index"), ("ZFEU", "/Beschaffung/Foerderung/Index") })
        {
            var pflicht = await db.Fuehrungspflichten.FirstOrDefaultAsync(p => p.Code == code && p.Vorlage == "/Beschaffung/Index", ct);
            if (pflicht is not null)
            {
                pflicht.Vorlage = vorlage;
            }
        }

        // Kassenprüfer bekommen ihre Rolle aus der Funktion (V05); ältere Datenbanken kennen die Zuordnung noch nicht.
        var kassenpruefer = await db.Funktionen.FirstOrDefaultAsync(f => f.Code == "KP" && f.VorgeschlageneRolle == null, ct);
        if (kassenpruefer is not null)
        {
            kassenpruefer.VorgeschlageneRolle = Rollen.Kassenpruefer;
            kassenpruefer.AmtszeitJahre ??= 5;
        }

        // Ausschussmitglieder lesen den Haushaltsentwurf über ihre Funktion; ältere Datenbanken kennen die Zuordnung noch nicht.
        var ausschuss = await db.Funktionen.FirstOrDefaultAsync(f => f.Code == "AUS" && f.VorgeschlageneRolle == null, ct);
        if (ausschuss is not null)
        {
            ausschuss.VorgeschlageneRolle = Rollen.Ausschussmitglied;
        }

        // Leistungsspange nach DJF-Richtlinie 15 bis 18 Jahre (V49); ein bewusst gesetztes Höchstalter bleibt.
        var spange = await db.Qualifikationen.FirstOrDefaultAsync(q => q.Code == Qualifikation.CodeLeistungsspange && q.HoechstalterJahre == null, ct);
        if (spange is not null)
        {
            spange.HoechstalterJahre = 18;
        }

        // Fahrerliste je Fahrzeug (V59): ältere Datenbanken kennen die Gewichtsgrenze der Fahrerlaubnisse noch nicht.
        foreach (var (code, kg) in Stammdaten.FahrerlaubnisMasse)
        {
            var erlaubnis = await db.Qualifikationen.FirstOrDefaultAsync(q => q.Code == code && q.FahrzeugmasseBisKg == null, ct);
            if (erlaubnis is not null)
            {
                erlaubnis.FahrzeugmasseBisKg = kg;
                if (code == "FW-FS" && erlaubnis.Bezeichnung == "Feuerwehr-Fahrberechtigung (bis 4,75 t / 7,5 t)")
                {
                    erlaubnis.Bezeichnung = Stammdaten.FwFahrberechtigungKlein;
                }
            }
        }

        // PSA-Lebensdauer und Wäschegrenze (V63): ältere Datenbanken haben keine Vorbelegung; bewusst gepflegte Werte bleiben.
        foreach (var (code, vorgabe) in Stammdaten.PsaLebensdauer)
        {
            var art = await db.Geraetearten.FirstOrDefaultAsync(a => a.Code == code, ct);
            if (art is not null)
            {
                art.AussonderungNachJahren ??= vorgabe.Jahre;
                art.HoechstWaeschen ??= vorgabe.Waeschen;
            }
        }

        // Realer Fuhrpark (Andi, 25.10.2026): MTW Baujahr 1996, HLF 10 als Pilotfall der Zweckbindung. Bewusst gepflegte Werte bleiben.
        var mtw = await db.Fahrzeuge.FirstOrDefaultAsync(f => f.Funkrufname == "Florian Adelberg 19" && f.Baujahr == 1999, ct);
        if (mtw is not null)
        {
            mtw.Baujahr = 1996;
        }

        var hlf = await db.Fahrzeuge.FirstOrDefaultAsync(f => f.Funkrufname == "Florian Adelberg 43" && f.Foerdermittel == null && f.ZweckbindungBis == null, ct);
        if (hlf is not null)
        {
            var vorlage = Stammdaten.Fahrzeuge.Single(f => f.Funkrufname == hlf.Funkrufname);
            hlf.Foerdermittel = vorlage.Foerdermittel;
            hlf.Foerdergeber = vorlage.Foerdergeber;
            hlf.ZweckbindungBis = vorlage.ZweckbindungBis;
        }

        // Wege brauchen die Ids der Dienstgrade, neue Fahrzeuge die Prüfvorlage aus den Gerätearten.
        await db.SaveChangesAsync(ct);
        await AbzeichenregelwerkeAnlegenAsync(db, ct);

        // Die Kinderfeuerwehr ist Unterabteilung der Jugendfeuerwehr; eine bewusst andere Zuordnung bleibt.
        var kinder = await db.Abteilungen.SingleOrDefaultAsync(x => x.Code == AbteilungCode.Kinder && x.UebergeordneteId == null, ct);
        if (kinder is not null)
        {
            kinder.UebergeordneteId = await db.Abteilungen.Where(x => x.Code == AbteilungCode.Jugend).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        }

        // Früher gab es nur „Einsatzabteilung“; neben EA2 bis EA5 bekommt sie ihre Nummer, solange niemand sie umbenannt hat.
        var ersteEinsatz = await db.Abteilungen.SingleOrDefaultAsync(
            x => x.Code == AbteilungCode.Einsatz && x.Bezeichnung == "Einsatzabteilung" && x.Kurzbezeichnung == null, ct);
        if (ersteEinsatz is not null)
        {
            var vorlage = Stammdaten.Abteilungen.Single(a => a.Code == AbteilungCode.Einsatz);
            ersteEinsatz.Bezeichnung = vorlage.Bezeichnung;
            ersteEinsatz.Kurzbezeichnung = vorlage.Kurzbezeichnung;
        }

        await LaufbahnStammdaten.AnlegenAsync(db, ct);

        if (!await db.Zuschussgeber.IgnoreQueryFilters().AnyAsync(ct))
        {
            db.Zuschussgeber.AddRange(Stammdaten.Zuschussgeber);
            await db.SaveChangesAsync(ct);
        }

        await ArtikelAnlegenAsync(db, ct);

        foreach (var fahrzeug in Stammdaten.Fahrzeuge)
        {
            if (!await db.Fahrzeuge.IgnoreQueryFilters().AnyAsync(f => f.Funkrufname == fahrzeug.Funkrufname, ct))
            {
                if (Stammdaten.FahrzeugPruefvorlagen.TryGetValue(fahrzeug.Funkrufname, out var vorlage))
                {
                    fahrzeug.PruefvorlageId = await db.Geraetearten.Where(g => g.Code == vorlage).Select(g => (Guid?)g.Id).FirstOrDefaultAsync(ct);
                }

                db.Fahrzeuge.Add(fahrzeug);
            }
        }

        await db.SaveChangesAsync(ct);
        await Kassen.Kassenfuehrung.KassenAnlegenAsync(db, Ortszeit.Datum(DateTimeOffset.UtcNow), ct);
        await Kassen.Kassenfuehrung.WiederkehrendeAnlegenAsync(db, Ortszeit.Datum(DateTimeOffset.UtcNow), ct);
        await RechteVorbelegenAsync(db, ct);

        await AdministratorAnlegenAsync(sp.GetRequiredService<UserManager<Benutzer>>(), konfiguration, log);
    }

    private static async Task RollenAnlegenAsync(RoleManager<IdentityRole<Guid>> rollen)
    {
        foreach (var name in Rollen.Alle)
        {
            if (!await rollen.RoleExistsAsync(name))
            {
                await rollen.CreateAsync(new IdentityRole<Guid>(name) { Id = Guid.CreateVersion7() });
            }
        }
    }

    /// <summary>
    /// Profile der mitgelieferten Rollen und die Vorbelegung jedes Rechts, das die Datenbank noch nicht kennt. Ein schon
    /// eingeführtes Recht wird nie wieder vorbelegt, auch wenn ihm der Administrator alle Rollen entzogen hat. Eine neu
    /// mitgelieferte Rolle bekommt beim ersten Start ihre Vorbelegung auch für schon eingeführte Rechte.
    /// </summary>
    private static async Task RechteVorbelegenAsync(AppDbContext db, CancellationToken ct)
    {
        var profile = await db.Rollenprofile.IgnoreQueryFilters().Select(r => r.Name).ToListAsync(ct);
        var neueRollen = Rollen.Alle.Except(profile).ToList();
        foreach (var name in neueRollen)
        {
            db.Rollenprofile.Add(new Rollenprofil { Name = name, Bezeichnung = Rollen.Bezeichnung(name) });
        }

        var abteilungen = await db.Abteilungen.IgnoreQueryFilters().ToDictionaryAsync(a => a.Code, a => a.Id, ct);
        void Vorbelegen(string recht, ICollection<string>? nurRollen)
        {
            foreach (var rolle in Berechtigungen.Vorbelegung.GetValueOrDefault(recht, []).Where(r => nurRollen?.Contains(r) != false))
            {
                db.RollenRechte.Add(new RolleRecht { Rolle = rolle, Recht = recht });
            }

            foreach (var (rolle, _, codes) in Berechtigungen.VorbelegungAbteilungen.Where(v => v.Recht == recht && nurRollen?.Contains(v.Rolle) != false))
            {
                var ids = codes.Where(abteilungen.ContainsKey).Select(c => abteilungen[c]).ToList();
                if (ids.Count > 0)
                {
                    db.RollenRechte.Add(new RolleRecht { Rolle = rolle, Recht = recht, Abteilungen = ids });
                }
            }
        }

        // Eine neue Rolle bekommt für schon eingeführte Rechte ihre Vorbelegung, auch die mit Abteilungsbezug.
        var eingefuehrt = await db.RechteEingefuehrt.Select(r => r.Recht).ToListAsync(ct);
        foreach (var recht in eingefuehrt)
        {
            Vorbelegen(recht, neueRollen);
        }

        foreach (var recht in Berechtigungen.Katalog.Select(r => r.Name).Except(eingefuehrt))
        {
            Vorbelegen(recht, null);
            db.RechteEingefuehrt.Add(new RechtEingefuehrt { Recht = recht });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Kleiderkammer-Artikel mit Geräteart; braucht gespeicherte Gerätearten.</summary>
    private static async Task ArtikelAnlegenAsync(AppDbContext db, CancellationToken ct)
    {
        var vorhanden = await db.Artikel.IgnoreQueryFilters().Select(a => a.Code).ToListAsync(ct);
        foreach (var (artikel, geraeteart) in Stammdaten.Artikel.Where(a => !vorhanden.Contains(a.Artikel.Code)))
        {
            artikel.GeraeteartId = geraeteart is null
                ? null
                : await db.Geraetearten.Where(g => g.Code == geraeteart).Select(g => (Guid?)g.Id).FirstOrDefaultAsync(ct);

            // Ohne passende Geräteart (z. B. gelöscht) würde aus dem Einzelstück ein Mengenartikel; dann lieber nicht anlegen.
            if (geraeteart is null || artikel.GeraeteartId is not null)
            {
                db.Artikel.Add(artikel);
            }
        }
    }

    /// <summary>Stellt Dienstgrade mit der früheren Seed-Bezeichnung auf die Schreibweise der VwV Feuerwehrbekleidung um; eigene Änderungen bleiben.</summary>
    /// <param name="db">Datenbankkontext.</param>
    /// <param name="ct">Abbruchsignal.</param>
    private static async Task DienstgradeUmbenennenAsync(AppDbContext db, CancellationToken ct)
    {
        var neu = Stammdaten.Dienstgrade.ToDictionary(d => d.Code, d => d.Bezeichnung);
        foreach (var d in await db.Dienstgrade.IgnoreQueryFilters().Where(d => Stammdaten.DienstgradeFruehereBezeichnung.Keys.Contains(d.Code)).ToListAsync(ct))
        {
            if (Stammdaten.DienstgradeFruehereBezeichnung[d.Code] == d.Bezeichnung)
            {
                d.Bezeichnung = neu[d.Code];
            }
        }
    }

    /// <summary>Legt fehlende Bauteilarten (V61) zur jeweiligen Geräteart an; gepflegte Fristen bleiben.</summary>
    /// <param name="db">Datenbankkontext.</param>
    /// <param name="ct">Abbruchsignal.</param>
    private static async Task BauteilartenAnlegenAsync(AppDbContext db, CancellationToken ct)
    {
        var vorhanden = await db.Bauteilarten.IgnoreQueryFilters().Select(b => b.Code).ToListAsync(ct);
        await db.Geraetearten.IgnoreQueryFilters().LoadAsync(ct);
        var arten = db.Geraetearten.Local.ToDictionary(g => g.Code);
        foreach (var (art, geraeteart) in Stammdaten.Bauteilarten)
        {
            if (!vorhanden.Contains(art.Code) && arten.TryGetValue(geraeteart, out var zu))
            {
                art.GeraeteartId = zu.Id;
                db.Bauteilarten.Add(art);
            }
        }
    }

    private static async Task<IReadOnlyList<T>> KatalogAnlegenAsync<T>(AppDbContext db, IEnumerable<T> eintraege, CancellationToken ct)
        where T : Katalogeintrag
    {
        var vorhanden = await db.Set<T>().IgnoreQueryFilters().Select(k => k.Code).ToListAsync(ct);
        var neu = eintraege.Where(e => !vorhanden.Contains(e.Code)).ToList();
        db.Set<T>().AddRange(neu);
        return neu;
    }

    /// <summary>Verknüpft Vorstufen nur bei neu angelegten Einträgen, damit Änderungen in der Oberfläche bleiben.</summary>
    /// <summary>Verknüpft neu angelegte Nachweisarten mit der Qualifikation, deren Inhaber sie brauchen (z. B. Führungsfortbildung für Gruppenführer).</summary>
    /// <param name="db">Datenbankkontext.</param>
    /// <param name="neu">Neu angelegte Nachweisarten.</param>
    /// <param name="neueQualifikationen">Neu angelegte, noch nicht gespeicherte Qualifikationen.</param>
    /// <param name="ct">Abbruchsignal.</param>
    private static async Task NachweisartenVerknuepfenAsync(AppDbContext db, IReadOnlyList<Nachweisart> neu, IReadOnlyList<Qualifikation> neueQualifikationen, CancellationToken ct)
    {
        foreach (var (nachweisart, code) in Stammdaten.NachweisartQualifikationen)
        {
            if (neu.FirstOrDefault(n => n.Code == nachweisart) is not { } art)
            {
                continue;
            }

            art.QualifikationId = neueQualifikationen.FirstOrDefault(q => q.Code == code)?.Id
                ?? await db.Qualifikationen.IgnoreQueryFilters().Where(q => q.Code == code).Select(q => (Guid?)q.Id).FirstOrDefaultAsync(ct);
        }
    }

    /// <summary>Legt fehlende Unterweisungsthemen an und verknüpft sie über die Codes mit Nachweisart und Qualifikation (V53).</summary>
    /// <param name="db">Datenbankkontext.</param>
    /// <param name="ct">Abbruchsignal.</param>
    private static async Task UnterweisungsthemenAnlegenAsync(AppDbContext db, CancellationToken ct)
    {
        var vorhanden = await db.Unterweisungsthemen.IgnoreQueryFilters().Select(t => t.Code).ToListAsync(ct);
        var nachweisarten = await db.Nachweisarten.IgnoreQueryFilters().ToDictionaryAsync(a => a.Code, a => a.Id, ct);
        var qualifikationen = await db.Qualifikationen.IgnoreQueryFilters().ToDictionaryAsync(q => q.Code, q => q.Id, ct);
        foreach (var (thema, nachweisart, qualifikation) in Stammdaten.Unterweisungsthemen.Where(t => !vorhanden.Contains(t.Thema.Code)))
        {
            thema.NachweisartId = nachweisart is not null && nachweisarten.TryGetValue(nachweisart, out var n) ? n : null;
            thema.QualifikationId = qualifikation is not null && qualifikationen.TryGetValue(qualifikation, out var q) ? q : null;
            db.Unterweisungsthemen.Add(thema);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task StufenfolgenAnlegenAsync(AppDbContext db, IReadOnlyList<Qualifikation> neu, CancellationToken ct)
    {
        var codes = Stammdaten.Stufenfolgen.Select(s => s.Vorstufe).ToList();
        var ids = (await db.Qualifikationen.IgnoreQueryFilters().Where(q => codes.Contains(q.Code)).Select(q => new { q.Code, q.Id }).ToListAsync(ct))
            .Concat(neu.Select(q => new { q.Code, q.Id }))
            .DistinctBy(q => q.Code)
            .ToDictionary(q => q.Code, q => q.Id);
        foreach (var q in neu)
        {
            var stufe = Stammdaten.Stufenfolgen.FirstOrDefault(s => s.Code == q.Code);
            if (stufe.Vorstufe is not null && ids.TryGetValue(stufe.Vorstufe, out var vorstufe))
            {
                q.VorstufeId = vorstufe;
            }
        }
    }

    /// <summary>Legt fehlende Jugendflamme-Regelwerke mit ihren Stufen an; vorhandene und selbst gepflegte bleiben unverändert.</summary>
    private static async Task AbzeichenregelwerkeAnlegenAsync(AppDbContext db, CancellationToken ct)
    {
        var vorhanden = await db.Abzeichenregelwerke.IgnoreQueryFilters().Select(r => r.Code).ToListAsync(ct);
        var stufen = await db.Qualifikationen.IgnoreQueryFilters().ToDictionaryAsync(q => q.Code, q => q.Id, ct);
        var standardDa = await db.Abzeichenregelwerke.AnyAsync(r => r.Standard, ct);
        foreach (var (regelwerk, regeln) in Stammdaten.Abzeichenregelwerke.Where(r => !vorhanden.Contains(r.Regelwerk.Code)))
        {
            regelwerk.Standard &= !standardDa;
            foreach (var (code, mindestalter, voraussetzungen, monate, hinweis) in regeln)
            {
                if (stufen.TryGetValue(code, out var id))
                {
                    regelwerk.Stufen.Add(new()
                    {
                        QualifikationId = id, MindestalterJahre = mindestalter, Voraussetzungen = voraussetzungen, NachweisHoechstensMonate = monate, Hinweis = hinweis,
                    });
                }
            }

            db.Abzeichenregelwerke.Add(regelwerk);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Legt den ersten Administrator an, wenn Seed:Admin:Email und Seed:Admin:Passwort gesetzt sind und es noch keinen gibt.</summary>
    private static async Task AdministratorAnlegenAsync(UserManager<Benutzer> benutzer, IConfiguration konfiguration, ILogger log)
    {
        var email = konfiguration["Seed:Admin:Email"];
        var passwort = konfiguration["Seed:Admin:Passwort"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(passwort))
        {
            return;
        }

        if ((await benutzer.GetUsersInRoleAsync(Rollen.Administrator)).Count > 0)
        {
            return;
        }

        var admin = new Benutzer { UserName = email, Email = email, EmailConfirmed = true };
        var ergebnis = await benutzer.CreateAsync(admin, passwort);
        if (!ergebnis.Succeeded)
        {
            log.LogError("Administrator konnte nicht angelegt werden: {Fehler}", string.Join(", ", ergebnis.Errors.Select(e => e.Description)));
            return;
        }

        await benutzer.AddToRolesAsync(admin, [Rollen.Administrator, Rollen.Kommandant]);
        log.LogInformation("Erster Administrator {Email} angelegt.", email);
    }
}
