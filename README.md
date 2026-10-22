# FF Adelberg Verwaltung

Verwaltungssoftware der Freiwilligen Feuerwehr Adelberg: Mitglieder, Qualifikationen, Atemschutz,
Dienste mit Anwesenheit, Einsatzdokumentation (mit Übernahme der Alarmdaten aus alamos FE2),
Fahrzeuge, Geräte und Prüfungen.

**Nicht** im Umfang: Alarmierung und Einsatzführung. Die Primäralarmierung läuft über das digitale
Meldenetz, die Zusatzalarmierung über alamos. Diese Software übernimmt nur nachträglich die Daten.

Datenmodell und Entscheidungen: [docs/Datenmodell.md](docs/Datenmodell.md).

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
src/FfAdelberg.Domain          Entitäten und Regeln (keine Abhängigkeiten)
src/FfAdelberg.Infrastructure  EF Core, Migrationen, Identity, Audit, Stammdaten
src/FfAdelberg.Web             Razor Pages, HTMX-Partials, FE2-Webhook
tests/FfAdelberg.Domain.Tests
tests/FfAdelberg.Integration.Tests
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

4. F5 in VS Code (Projekt `FfAdelberg.Web`) oder `dotnet run --project src/FfAdelberg.Web`.
   Die Anwendung läuft unter <https://localhost:5001>, legt beim Start die Datenbank an und
   füllt die Stammdaten (Abteilungen, Dienstgrade, Funktionen, Qualifikationen, Fahrzeuge).
5. Anmelden mit dem Entwicklungskonto aus `appsettings.Development.json`
   (`admin@ff-adelberg.test` / `Entwicklung2026`). Nur für die lokale Entwicklung.

Mailpit (<http://localhost:8025>) steht als Test-Postfach bereit. Einen E-Mail-Versand
(Passwort zurücksetzen, Fristerinnerungen) hat die Anwendung noch nicht; er kommt mit Hangfire.

## Häufige Befehle

```powershell
dotnet test                                    # alle Tests (Docker muss laufen)
dotnet ef migrations add <Name> --project src/FfAdelberg.Infrastructure --startup-project src/FfAdelberg.Web --output-dir Persistenz/Migrationen
docker compose --profile app up -d --build     # gesamter Stack im Container, http://localhost:8080
```

## alamos FE2 anbinden

FE2 schickt Alarmdaten per HTTP-Ausgang an `POST /api/alarm/fe2` mit dem Header
`X-Fe2-Token: <geheimer Wert>` (Konfiguration `Fe2:WebhookToken`). Gespeichert werden die
Rohdaten getrennt vom Einsatzbericht und nach sechs Monaten zur Löschung markiert
(Hinweise IM BW zu Alarmdaten). Doppelt gesendete Alarme (gleiche `externalId`) werden ignoriert.

## Konfiguration

| Schlüssel | Bedeutung |
|---|---|
| `ConnectionStrings:Datenbank` | PostgreSQL-Verbindung |
| `Datenbank:AutoMigrate` | Migrationen beim Start anwenden (Standard `true`) |
| `Fe2:WebhookToken` | Geheimer Wert für den FE2-Webhook; leer = abgeschaltet |
| `Betrieb:Notbetrieb` | Notbetrieb im Gerätehaus (Stufe 2; bisher nur Hinweisbalken) |
| `Seed:Admin:Email`, `Seed:Admin:Passwort` | Legt beim ersten Start einen Administrator an, falls noch keiner existiert |

Im Container werden Schlüssel als Umgebungsvariablen mit `__` gesetzt, z. B. `ConnectionStrings__Datenbank`.

## Datenschutz

Verantwortlich im Sinne der DSGVO ist die Gemeinde Adelberg. Echte Mitgliederdaten erst nach
Freigabe durch Bürgermeister und Datenschutzbeauftragten, mit Verarbeitungsverzeichnis, TOMs und
Auftragsverarbeitungsvertrag mit dem Hoster. Atemschutz-Vorsorge speichert nur Status und Frist,
nie Befunde. Jede Änderung an Datensätzen wird in `audit_eintraege` protokolliert.

## Lizenz

[EUPL-1.2](LICENSE). Mitgelieferte Bibliotheken: htmx (0BSD), Alpine.js (MIT).
