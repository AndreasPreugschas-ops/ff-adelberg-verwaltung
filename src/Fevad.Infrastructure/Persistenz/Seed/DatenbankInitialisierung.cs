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
        await KatalogAnlegenAsync(db, Stammdaten.Funktionen, ct);
        var neueQualifikationen = await KatalogAnlegenAsync(db, Stammdaten.Qualifikationen, ct);
        await StufenfolgenAnlegenAsync(db, neueQualifikationen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.StatistikKategorien, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Gefahrstoffgruppen, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Verletzungsarten, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Dokumentkategorien, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Nachweisarten, ct);
        await KatalogAnlegenAsync(db, Stammdaten.Geraetearten, ct);
        await KatalogAnlegenAsync(db, Stammdaten.PsaGeraetearten, ct);

        if (!await db.Organisationen.IgnoreQueryFilters().AnyAsync(ct))
        {
            db.Organisationen.Add(Stammdaten.Organisation);
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

        // Wege brauchen die Ids der Dienstgrade, neue Fahrzeuge die Prüfvorlage aus den Gerätearten.
        await db.SaveChangesAsync(ct);

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
    /// eingeführtes Recht wird nie wieder vorbelegt, auch wenn ihm der Administrator alle Rollen entzogen hat.
    /// </summary>
    private static async Task RechteVorbelegenAsync(AppDbContext db, CancellationToken ct)
    {
        var profile = await db.Rollenprofile.IgnoreQueryFilters().Select(r => r.Name).ToListAsync(ct);
        foreach (var name in Rollen.Alle.Except(profile))
        {
            db.Rollenprofile.Add(new Rollenprofil { Name = name, Bezeichnung = Rollen.Bezeichnung(name) });
        }

        var eingefuehrt = await db.RechteEingefuehrt.Select(r => r.Recht).ToListAsync(ct);
        var abteilungen = await db.Abteilungen.IgnoreQueryFilters().ToDictionaryAsync(a => a.Code, a => a.Id, ct);
        foreach (var recht in Berechtigungen.Katalog.Select(r => r.Name).Except(eingefuehrt))
        {
            foreach (var rolle in Berechtigungen.Vorbelegung.GetValueOrDefault(recht, []))
            {
                db.RollenRechte.Add(new RolleRecht { Rolle = rolle, Recht = recht });
            }

            foreach (var (rolle, _, codes) in Berechtigungen.VorbelegungAbteilungen.Where(v => v.Recht == recht))
            {
                var ids = codes.Where(abteilungen.ContainsKey).Select(c => abteilungen[c]).ToList();
                if (ids.Count > 0)
                {
                    db.RollenRechte.Add(new RolleRecht { Rolle = rolle, Recht = recht, Abteilungen = ids });
                }
            }

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

    private static async Task<IReadOnlyList<T>> KatalogAnlegenAsync<T>(AppDbContext db, IEnumerable<T> eintraege, CancellationToken ct)
        where T : Katalogeintrag
    {
        var vorhanden = await db.Set<T>().IgnoreQueryFilters().Select(k => k.Code).ToListAsync(ct);
        var neu = eintraege.Where(e => !vorhanden.Contains(e.Code)).ToList();
        db.Set<T>().AddRange(neu);
        return neu;
    }

    /// <summary>Verknüpft Vorstufen nur bei neu angelegten Einträgen, damit Änderungen in der Oberfläche bleiben.</summary>
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
