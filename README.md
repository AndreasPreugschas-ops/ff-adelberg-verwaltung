# FEVAD

**FEVAD** (**FE**uerwehr, **V**erwaltung, **AD**elberg) ist die Verwaltungssoftware der Freiwilligen Feuerwehr Adelberg: Mitglieder, Qualifikationen, Atemschutz,
Dienste mit Anwesenheit, Einsatzdokumentation (mit Übernahme der Alarmdaten aus alamos FE2),
Fahrzeuge, Geräte und Prüfungen.

**Nicht** im Umfang: Alarmierung und Einsatzführung. Die Primäralarmierung läuft über das digitale
Meldenetz, die Zusatzalarmierung über alamos. Diese Software übernimmt nur nachträglich die Daten.

Datenmodell und Entscheidungen: [docs/Datenmodell.md](docs/Datenmodell.md).
Server-Betrieb, Sicherung und Updates: [docs/Betrieb.md](docs/Betrieb.md).

## Technik

| Baustein | Wahl |
|---|---|
| Laufzeit | .NET 10 (LTS) |
| Oberfläche | ASP.NET Core Razor Pages, HTMX, Alpine.js, Tailwind CSS (Standalone, ohne Node) |
| Datenbank | PostgreSQL 17 über EF Core (Npgsql), snake_case-Tabellen |
| Anmeldung | ASP.NET Core Identity, Rollen und Policies, TOTP-2FA |
| Tests | xUnit, Testcontainers (PostgreSQL im Container) |
| Betrieb | Docker Compose, GitHub Actions, Image in GHCR |

Lösungsstruktur:

```
src/Fevad.Domain          Entitäten und Regeln (keine Abhängigkeiten)
src/Fevad.Infrastructure  EF Core, Migrationen, Identity, Audit, Stammdaten
src/Fevad.Web             Razor Pages, HTMX-Partials, FE2-Webhook
tests/Fevad.Domain.Tests
tests/Fevad.Integration.Tests
```

## Einrichten unter Windows (VS Code)

1. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), [Docker Desktop](https://www.docker.com/products/docker-desktop/), Git.
2. VS Code mit den Erweiterungen *C# Dev Kit*, *Docker*, *Tailwind CSS IntelliSense*.
3. Repository klonen, dann im Terminal:

   ```powershell
   dotnet tool restore
   ./tools/tailwind-holen.ps1
   docker compose up -d postgres mailpit
   dotnet dev-certs https --trust
   ```

4. F5 in VS Code (Projekt `Fevad.Web`) oder `dotnet run --project src/Fevad.Web`.
   Die Anwendung läuft unter <https://localhost:5001>, legt beim Start die Datenbank an und
   füllt die Stammdaten (Abteilungen, Dienstgrade, Funktionen, Qualifikationen, Fahrzeuge).
5. Anmelden mit dem Entwicklungskonto aus `appsettings.Development.json`
   (`admin@ff-adelberg.test` / `Entwicklung2026`). Nur für die lokale Entwicklung.
   Weil das Konto Administrator ist, verlangt die Anwendung zuerst die Einrichtung einer
   Authenticator-App (siehe unten).

Mailpit (<http://localhost:8025>) fängt in der Entwicklung alle E-Mails ab (Einrichtungs- und
Passwortlinks).

## Zugänge

- Konten legt nur der Administrator unter **Benutzer** an: E-Mail-Adresse, verknüpftes Mitglied
  (höchstens ein Konto je Mitglied) und Rollen. Selbstregistrierung gibt es nicht.
- Ein neues Konto hat kein Passwort. Die Anwendung erzeugt einen **Einrichtungslink**
  (gültig `Anmeldung:LinkGueltigStunden`, nur einmal), den der Administrator per E-Mail schickt
  oder persönlich weitergibt. Ohne Mailserver funktioniert das genauso, nur ohne E-Mail.
- **Zwei-Faktor-Pflicht** für alle Rollen in `Rollen.ZweiFaktorPflicht` (alle außer Führungskraft
  und Mitglied): Wer so eine Rolle hat, landet nach der Anmeldung zuerst auf der Einrichtung der
  Authenticator-App (TOTP, QR-Code) und bekommt zehn Wiederherstellungscodes.
- Handy verloren: Wiederherstellungscode verwenden oder der Administrator setzt die
  Zwei-Faktor-Anmeldung zurück. Konten werden nicht gelöscht, sondern deaktiviert.
- Rollenänderungen, Deaktivieren und „auf allen Geräten abmelden“ greifen spätestens nach
  `Anmeldung:SitzungPruefenMinuten`. Kontoänderungen und Rollenvergaben stehen im Änderungsprotokoll.
- Die Schlüssel für Anmelde-Cookies und Links liegen in der Datenbank (`data_protection_keys`),
  damit ein Neustart oder Update niemanden abmeldet.

## Häufige Befehle

```powershell
dotnet test                                    # alle Tests (Docker muss laufen)
dotnet ef migrations add <Name> --project src/Fevad.Infrastructure --startup-project src/Fevad.Web --output-dir Persistenz/Migrationen
docker compose --profile app up -d --build     # gesamter Stack im Container, http://localhost:8080
```

## alamos FE2 anbinden

FE2 schickt Alarmdaten per HTTP-Ausgang an `POST /api/alarm/fe2` mit dem Header
`X-Fe2-Token: <geheimer Wert>` (Konfiguration `Fe2:WebhookToken`). Gespeichert werden die
Rohdaten getrennt vom Einsatzbericht und nach sechs Monaten vom nächtlichen Löschlauf gelöscht
(Hinweise IM BW zu Alarmdaten). Doppelt gesendete Alarme (gleiche `externalId`) werden ignoriert.

Aus jedem Alarm (`type` = `ALARM` oder ohne Typ) entsteht sofort ein Einsatzbericht-Entwurf mit
der nächsten Einsatznummer: Stichwort, Meldung, Adresse, Koordinaten und die Fahrzeuge aus
`vehicles[].radioName` (Zuordnung über den Funkrufnamen). Statusmeldungen und `CLOSE` werden
bestätigt, aber nicht gespeichert. Gibt es schon einen Bericht mit derselben Leitstellennummer,
wird der Alarm nur damit verknüpft. Scheitert der Entwurf, bleibt der Alarm offen und lässt sich
in der Einsatzliste mit „Jetzt übernehmen“ nachholen.

Ablauf in der Oberfläche: Entwurf → in Bearbeitung (erstes Speichern) → freigegeben. Bearbeiten
dürfen Kommandant, Stellvertreter und Führungskräfte (`Einsaetze.Bearbeiten`), freigeben und
freigegebene Berichte ändern oder wieder öffnen nur `Einsaetze.Freigeben`. Das Format der
Einsatznummer steht im Parameter `Einsatz.Nummer.Format` (Standard `{JJJJ}-{NNN}`, also `2026-017`).

## Konfiguration

| Schlüssel | Bedeutung |
|---|---|
| `ConnectionStrings:Datenbank` | PostgreSQL-Verbindung |
| `Datenbank:AutoMigrate` | Migrationen beim Start anwenden (Standard `true`) |
| `Fe2:WebhookToken` | Geheimer Wert für den FE2-Webhook; leer = abgeschaltet |
| `Betrieb:Notbetrieb` | Notbetrieb im Gerätehaus (Stufe 2; bisher nur Hinweisbalken) |
| `Seed:Admin:Email`, `Seed:Admin:Passwort` | Legt beim ersten Start einen Administrator an, falls noch keiner existiert |
| `Anmeldung:OeffentlicheAdresse` | Öffentliche Adresse, z. B. `https://verwaltung.ff-adelberg.de`; daraus entstehen die Links in E-Mails. Außerhalb der Entwicklung Pflicht |
| `Anmeldung:LinkGueltigStunden` | Gültigkeit von Einrichtungs- und Passwortlinks (Standard 72) |
| `Anmeldung:ZweiFaktorPflichtAus` | `true` schaltet die Zwei-Faktor-Pflicht zum lokalen Testen ab; wirkt nur in Development |
| `Anmeldung:SitzungPruefenMinuten` | Abstand, in dem laufende Sitzungen gegen die Datenbank geprüft werden (Standard 5) |
| `Mail:SmtpServer`, `Mail:Port`, `Mail:StartTls`, `Mail:Benutzername`, `Mail:Passwort`, `Mail:Absender` | SMTP-Versand (STARTTLS, Port 587). Leer = kein Mailversand, Links gibt der Administrator von Hand weiter |

Im Container werden Schlüssel als Umgebungsvariablen mit `__` gesetzt, z. B. `ConnectionStrings__Datenbank`.

## Versionen und Releases

FEVAD nummeriert Releases nach [Semantic Versioning](https://semver.org/lang/de/): `MAJOR.MINOR.PATCH`.
Solange die Version mit `0.` beginnt, ist FEVAD im Aufbau; ab `1.0.0` läuft es im Echtbetrieb.

| Teil | Erhöhen bei |
|---|---|
| PATCH (`0.1.0` → `0.1.1`) | Fehlerbehebung ohne neue Funktion |
| MINOR (`0.1.1` → `0.2.0`) | neue Funktion oder neues Modul; Datenbankmigrationen laufen automatisch |
| MAJOR (`1.4.2` → `2.0.0`) | Änderung, die beim Update Handarbeit verlangt (Konfiguration, Datenübernahme) |

Die Versionsnummer kommt allein vom GitHub-Release, im Code steht keine. Release veröffentlichen:

1. Auf GitHub **Releases → Draft a new release**.
2. Unter *Choose a tag* einen neuen Tag eintippen, z. B. `v0.2.0`, Ziel `main`.
3. Titel `FEVAD 0.2.0`, dann **Generate release notes** (listet die gemergten Pull Requests) und
   bei Bedarf ergänzen. Für eine Testversion (`v0.2.0-beta.1`) *Set as a pre-release* anhaken.
4. **Publish release**.

Die CI baut daraufhin das Image `ghcr.io/andreaspreugschas-ops/fevad` mit den Tags `0.2.0` und
`stable` (bei Vorabversionen ohne `stable`). Die Oberfläche zeigt die Version in der Fußzeile, z. B.
`FEVAD 0.2.0 (1a2b3c4)`. Zwischen zwei Releases trägt `main` die nächste Patch-Version mit Zusatz,
z. B. `0.2.1-dev.4` (vier Commits nach `v0.2.0`); lokale Builds heißen `0.0.0-dev`.

Image-Tags: `stable` ist das neueste Release (für Gerätehaus-PC und VPS), `x.y.z` ein festes
Release, `latest` der aktuelle Stand von `main`, der Commit-Hash ein einzelner Build.

## Betrieb auf dem Server

Für den Echtbetrieb liegt in [`deploy/`](deploy) alles bereit: Docker Compose mit Caddy (HTTPS über
Let's Encrypt), PostgreSQL und dem Release-Image aus GHCR, nächtliche verschlüsselte Sicherung mit
restic, wöchentlicher Restore-Test, Update-Skript und Wächter für die Überwachung. Einrichtung,
Release-Checkliste und Notfälle beschreibt das [Betriebshandbuch](docs/Betrieb.md).

Außerhalb der Entwicklung prüft die Anwendung beim Start ihre Konfiguration und startet ohne gültige
`Anmeldung:OeffentlicheAdresse` (https) nicht.

## Datenschutz

Verantwortlich im Sinne der DSGVO ist die Gemeinde Adelberg. Echte Mitgliederdaten erst nach
Freigabe durch Bürgermeister und Datenschutzbeauftragten, mit Verarbeitungsverzeichnis, TOMs und
Auftragsverarbeitungsvertrag mit dem Hoster. Atemschutz-Vorsorge speichert nur Status und Frist,
nie Befunde. Jede Änderung an Datensätzen wird in `audit_eintraege` protokolliert.

## Lizenz

[EUPL-1.2](LICENSE). Mitgelieferte Bibliotheken: htmx (0BSD), Alpine.js (MIT).
