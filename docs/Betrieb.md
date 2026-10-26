# Betriebshandbuch FEVAD

Wie FEVAD auf einem Server läuft, gesichert, aktualisiert und überwacht wird. Grundlage ist das
Betriebsmodell „Cloud primär“ (Stufe 1): ein gemieteter Server (VPS) in Deutschland, auf dem
Docker Compose die Anwendung, die Datenbank und den HTTPS-Zugang betreibt. Der Notbetrieb auf dem
Gerätehaus-PC (Stufe 2) kommt später und ist hier nur vorgemerkt.

Alle Dateien für den Server liegen im Ordner [`deploy/`](../deploy) des Repositorys.

## Überblick

```
Internet ──443/80──▶ Caddy (HTTPS, Let's Encrypt) ──▶ FEVAD-Anwendung :8080 ──▶ PostgreSQL 17
                                                         (Image aus GHCR)          (Volume, nicht von außen erreichbar)

systemd-Timer auf dem Server:
  02:30 täglich      sicherung.sh          pg_dump + Konfiguration ──restic, verschlüsselt──▶ Sicherungsziel
  So 04:30           sicherung-testen.sh   neuester Stand ──▶ Wegwerf-Datenbank, Vergleich mit der laufenden
  alle 5 Minuten     waechter.sh           Gesundheit, Zertifikat, Platte ──Ping──▶ Überwachungsdienst
```

| Datei | Zweck |
|---|---|
| `compose.yml` | Dienste `postgres`, `app`, `caddy`; Werte aus `.env` |
| `Caddyfile` | HTTPS mit automatischem Zertifikat, Sicherheits-Header, Weiterleitung an die Anwendung |
| `env.beispiel` | Vorlage für `.env` mit allen Einstellungen und Erklärungen |
| `einrichten.sh` | Legt `.env` mit Zufallsgeheimnissen an und installiert die Timer |
| `sicherung.sh` | Nächtliche Sicherung |
| `sicherung-testen.sh` | Wöchentlicher Restore-Test |
| `wiederherstellen.sh` | Stand aus der Sicherung zurückspielen |
| `aktualisieren.sh` | Update auf ein neues Release |
| `waechter.sh` | Überwachung alle 5 Minuten |
| `status.sh` | Überblick für den Administrator |
| `systemd/` | Timer und Dienste für Sicherung, Restore-Test und Wächter |

## Was vor dem Start geklärt sein muss

Diese Punkte entscheiden Gemeinde und Kommandant, nicht die Software. Ohne sie keine Echtdaten.

- **Freigabe** durch Bürgermeister und Datenschutzbeauftragten, Verarbeitungsverzeichnis und TOMs
  (siehe Thread „Datenschutz-Unterlagen“).
- **Server:** VPS bei einem deutschen Anbieter mit Rechenzentrum in Deutschland, z. B. Hetzner Cloud
  (CX22: 2 vCPU, 4 GB RAM, 40 GB) oder IONOS. Größenordnung 5 bis 10 € im Monat.
- **Sicherungsziel** bei einem zweiten Anbieter oder zumindest getrennt vom Server, z. B. Hetzner
  Storage Box BX11 (1 TB, ca. 4 € im Monat). Die Daten sind vor dem Hochladen verschlüsselt, der
  Anbieter sieht nur Chiffrat.
- **Auftragsverarbeitungsverträge** mit Server- und Sicherungsanbieter, abgeschlossen von der Gemeinde
  als Verantwortlicher. Beide Anbieter bieten den AVV online an.
- **Domain**, z. B. `fevad.ff-adelberg.de`, mit einem A-Eintrag (und AAAA bei IPv6) auf die
  Server-Adresse.
- **Mailkonto** für den Versand (Einrichtungs- und Passwortlinks), z. B. ein Postfach der Gemeinde
  oder der Feuerwehr mit SMTP-Zugang (Port 587, STARTTLS).
- **Überwachung:** ein Konto bei einem Dienst, der „Lebenszeichen“ erwartet und bei Ausbleiben eine
  E-Mail schickt, z. B. healthchecks.io (kostenlos bis 20 Prüfungen). Es werden nur Statusmeldungen
  ohne personenbezogene Daten übertragen.
- **Wer betreut den Server?** Mindestens zwei Personen mit Zugang, damit Urlaub oder Krankheit
  kein Problem werden.

## Anbieter: Hetzner oder IONOS (F7)

Festlegung (Andi, 25.10.2026): Server in Deutschland bei Hetzner oder IONOS, jeweils mit AVV. Beide
Wege funktionieren mit denselben Dateien aus `deploy/`; die Wahl trifft die Gemeinde vor dem Echtbetrieb.

| | Hetzner | IONOS |
|---|---|---|
| Server | Cloud-Server mit 2 vCPU, 4 GB RAM, 40 GB SSD, Standort Nürnberg oder Falkenstein | VPS mit 2 vCPU, 4 GB RAM, Rechenzentrum in Deutschland wählen |
| Betriebssystem | Ubuntu 24.04 LTS | Ubuntu 24.04 LTS |
| Sicherungsziel | Storage Box (SFTP), getrennt vom Server; `RESTIC_REPOSITORY=sftp:…` wie unten | S3 Object Storage mit Standort Deutschland; `RESTIC_REPOSITORY=s3:https://<Endpunkt>/<Bucket>/fevad`, Zugangsschlüssel als `AWS_ACCESS_KEY_ID` und `AWS_SECRET_ACCESS_KEY` in `.env` |
| AVV | im Kundenkonto online abschließen | im Kundenkonto online abschließen |
| Firewall | Cloud-Firewall in der Konsole zusätzlich zu `ufw`: nur 22, 80, 443 | Firewall-Richtlinie in der Konsole zusätzlich zu `ufw`: nur 22, 80, 443 |
| Zweiter Zugang | Kommandant als Mitglied im Projekt | Kommandant als zusätzlicher Benutzer im Vertrag |
| Grobe Kosten | 5 bis 10 € Server, rund 4 € Storage Box im Monat | ähnliche Größenordnung |

Unabhängig vom Anbieter gilt: Server und Sicherung nicht im selben Konto mit demselben Passwort, AVV
für beide Leistungen, Zwei-Faktor-Anmeldung im Kundenkonto einschalten. Die Preise ändern sich; vor der
Bestellung beim Anbieter nachsehen.

## Zweiter Zugang und Notfallumschlag (F9)

Festlegung (Andi, 25.10.2026): Den zweiten Zugang zu Server, Domain und Sicherungsschlüssel hat der
Kommandant. Damit hängt der Betrieb nicht an einer Person.

| Zugang | Wie der Kommandant ihn bekommt | Erledigt am |
|---|---|---|
| Kundenkonto beim Anbieter (Server und Sicherung) | eigener Benutzer mit Zwei-Faktor-Anmeldung, kein geteiltes Passwort | |
| SSH auf den Server | eigener öffentlicher Schlüssel in `/root/.ssh/authorized_keys`; Anmeldung einmal ausprobieren | |
| Domain beim Registrar | eigener Zugang oder hinterlegtes Passwort, damit der DNS-Eintrag geändert werden kann | |
| `RESTIC_PASSWORD` | Ausdruck im versiegelten Umschlag im Rathaus und im Passwortsafe der Gemeinde | |
| FEVAD | eigenes Konto mit Rolle Administrator (neben Kommandant) | |
| Diese Anleitung | Ausdruck des Betriebshandbuchs beim Umschlag, Stand mit Datum | |

Einmal im Jahr (z. B. zur Hauptversammlung) prüfen: Zugänge gehen noch, Umschlag ist versiegelt,
Restore-Test ist grün. Scheidet einer der beiden aus, Zugänge sofort entziehen und `RESTIC_PASSWORD`
nicht wechseln (sonst sind alte Sicherungen unlesbar), sondern einen neuen restic-Schlüssel anlegen
(`restic key add`) und den alten entfernen (`restic key remove`).

## Server einrichten

Beschrieben für Debian 13 oder Ubuntu 24.04 LTS, als `root` bzw. mit `sudo`.

### Grundabsicherung

```bash
# Updates und automatische Sicherheitsupdates
apt update && apt full-upgrade -y
apt install -y unattended-upgrades curl git openssl ufw
dpkg-reconfigure -plow unattended-upgrades

# Zeitzone für Protokolle und Timer
timedatectl set-timezone Europe/Berlin

# SSH nur mit Schlüssel (vorher eigenen öffentlichen Schlüssel in ~/.ssh/authorized_keys hinterlegen!)
sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication no/' /etc/ssh/sshd_config
sed -i 's/^#\?PermitRootLogin.*/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config
systemctl reload ssh

# Firewall: nur SSH, HTTP (für das Zertifikat) und HTTPS
ufw default deny incoming
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443
ufw enable

# Systemprotokoll (enthält IP-Adressen von SSH-Anmeldungen) nach 14 Tagen löschen
mkdir -p /etc/systemd/journald.conf.d
printf '[Journal]\nMaxRetentionSec=14day\n' > /etc/systemd/journald.conf.d/fevad.conf
systemctl restart systemd-journald
```

Hinweis: Docker öffnet die Ports 80 und 443 selbst und umgeht dabei `ufw`. Das ist hier gewollt;
die Datenbank veröffentlicht bewusst keinen Port.

Beim Hoster zusätzlich die Hoster-Firewall mit denselben Regeln aktivieren, falls angeboten.

### Docker installieren

Nach der offiziellen Anleitung (<https://docs.docker.com/engine/install/debian/> bzw. `/ubuntu/`),
kurz:

```bash
curl -fsSL https://get.docker.com | sh
systemctl enable --now docker
docker compose version
```

### FEVAD-Dateien holen

```bash
git clone --depth 1 https://github.com/AndreasPreugschas-ops/ff-adelberg-verwaltung.git /root/fevad-repo
cp -r /root/fevad-repo/deploy /opt/fevad
cd /opt/fevad
./einrichten.sh
```

`einrichten.sh` legt `/opt/fevad/.env` an (nur für root lesbar), füllt Datenbankpasswort,
Sicherungspasswort und FE2-Token mit Zufallswerten und aktiviert die drei Timer.

### Einstellungen in `.env`

```bash
nano /opt/fevad/.env
```

Pflicht: `FEVAD_DOMAIN`, `ACME_EMAIL`, `FEVAD_VERSION` (z. B. `0.1.0`), `RESTIC_REPOSITORY`.
Für den ersten Start zusätzlich `ERSTER_ADMIN_EMAIL` und `ERSTER_ADMIN_PASSWORT`. Mail- und
Überwachungswerte, sobald vorhanden. Jede Zeile ist in der Datei erklärt.

**`RESTIC_PASSWORD` sofort außerhalb des Servers verwahren:** ausgedruckt und versiegelt im Rathaus
und im Passwortsafe des Administrators. Ohne dieses Passwort ist die Sicherung nach einem Verlust des
Servers wertlos. Dasselbe gilt sinngemäß für den SSH-Schlüssel zum Sicherungsziel.

### Sicherungsziel anbinden (Hetzner Storage Box per SFTP)

```bash
# Schlüssel nur für die Sicherung, ohne Passphrase (der Timer läuft unbeaufsichtigt)
ssh-keygen -t ed25519 -N '' -C fevad-sicherung -f /opt/fevad/ssh/id_ed25519

# Öffentlichen Schlüssel bei der Storage Box hinterlegen (SSH-Support im Hetzner-Konto einschalten)
cat /opt/fevad/ssh/id_ed25519.pub | ssh -p 23 u123456@u123456.your-storagebox.de install-ssh-key

# Host-Schlüssel des Ziels bekannt machen, sonst verweigert ssh im Container die Verbindung
ssh-keyscan -p 23 u123456.your-storagebox.de > /opt/fevad/ssh/known_hosts
chmod 600 /opt/fevad/ssh/*
```

In `.env`: `RESTIC_REPOSITORY=sftp://u123456@u123456.your-storagebox.de:23/fevad`.
Andere Ziele (S3-kompatibler Speicher, restic REST-Server) gehen ebenso, siehe restic-Dokumentation;
für S3 werden `AWS_ACCESS_KEY_ID` und `AWS_SECRET_ACCESS_KEY` in `.env` durchgereicht.

### Container-Image freigeben

Das Image `ghcr.io/andreaspreugschas-ops/fevad` entsteht in GitHub Actions. Damit der Server es ohne
Anmeldung holen kann, einmalig in GitHub unter *Packages → fevad → Package settings* die Sichtbarkeit
auf **Public** stellen (der Code ist ohnehin öffentlich). Alternativ auf dem Server
`docker login ghcr.io` mit einem Token, das nur `read:packages` darf.

### Erster Start

```bash
cd /opt/fevad
docker compose up -d
docker compose logs -f app      # Strg+C beendet nur die Anzeige
```

Die Anwendung legt die Datenbank an, füllt die Stammdaten und den ersten Administrator. Caddy holt
das Zertifikat, sobald die Domain auf den Server zeigt. Danach:

1. `https://<FEVAD_DOMAIN>` öffnen, mit dem ersten Administrator anmelden, Authenticator-App
   einrichten (Pflicht für Administratoren), Wiederherstellungscodes sicher ablegen.
2. In `.env` `ERSTER_ADMIN_EMAIL` und `ERSTER_ADMIN_PASSWORT` leeren, dann `docker compose up -d`.
   Solange das Passwort gesetzt ist, warnt die Anwendung im Log.
3. `./sicherung.sh` einmal von Hand ausführen (legt das restic-Repository an), danach
   `./sicherung-testen.sh`. Beide müssen mit „fertig“ bzw. „erfolgreich“ enden.
4. Die Release-Checkliste unten durchgehen.

Beim Start prüft die Anwendung die Konfiguration: Fehlt `Anmeldung:OeffentlicheAdresse` oder ist sie
keine https-Adresse, startet sie nicht (Meldung im Log). Fehlender Mailserver, gesetztes
Startpasswort, ein zu kurzes FE2-Token und ein vergessenes `ZweiFaktorPflichtAus` erscheinen als
Warnung.

## Release-Checkliste (vor dem Echtbetrieb und nach jedem größeren Update)

- [ ] **Zwei-Faktor-Pflicht ist an.** Auf dem Server läuft `ASPNETCORE_ENVIRONMENT=Production`;
      `Anmeldung:ZweiFaktorPflichtAus` wirkt dort nicht und darf nicht gesetzt sein
      (`docker compose logs app | grep ZweiFaktor` zeigt sonst eine Warnung).
- [ ] **2FA durchtesten** mit einem Testkonto in einer Pflichtrolle (z. B. Gerätewart):
  - Nach der Anmeldung leitet FEVAD auf die Einrichtung der Authenticator-App um; andere Seiten
    sind bis dahin gesperrt.
  - QR-Code mit Authenticator-App einlesen, Code bestätigen, Wiederherstellungscodes erscheinen.
  - Abmelden, Anmeldung mit Passwort und Code.
  - Anmeldung mit einem Wiederherstellungscode; der Code ist danach verbraucht.
  - Administrator setzt die Zwei-Faktor-Anmeldung des Kontos zurück; beim nächsten Anmelden wird
    die Einrichtung wieder verlangt.
  - Ein Konto nur mit Rolle Mitglied oder Führungskraft wird nicht zur Einrichtung gezwungen.
- [ ] **Öffentliche Adresse:** Einrichtungslink für ein Testkonto erzeugen; der Link beginnt mit
      `https://<FEVAD_DOMAIN>`.
- [ ] **Mailversand:** Einrichtungslink per E-Mail schicken; Mail kommt an, Absender stimmt, Link
      funktioniert.
- [ ] **HTTPS:** `http://<FEVAD_DOMAIN>` leitet auf https um, Zertifikat gültig (Schloss im Browser).
- [ ] **Sicherung und Restore-Test** liefen erfolgreich (`./status.sh`, Überwachungsdienst grün).
- [ ] **Überwachung:** App kurz anhalten (`docker compose stop app`), nach spätestens 10 Minuten
      kommt die Alarm-Mail; wieder starten (`docker compose start app`).
- [ ] `RESTIC_PASSWORD` liegt versiegelt im Rathaus und im Passwortsafe.
- [ ] FE2-Token (`FE2_WEBHOOK_TOKEN`) in alamos FE2 eingetragen, sobald die Anbindung genutzt wird.
- [ ] **Betriebsseite** (Verwaltung → Betrieb) ohne Warnung: Sicherung und Restore-Test gemeldet,
      Löschlauf gelaufen, Fehlerrate unauffällig.
- [ ] **Zweiter Zugang** (siehe oben) vollständig und ausprobiert.
- [ ] **Barrierefreiheit:** `BARRIEREFREIHEIT_KONTAKT` und `BARRIEREFREIHEIT_STAND` gesetzt,
      `/Barrierefreiheit` ohne Anmeldung erreichbar; offene Punkte aus `docs/Barrierefreiheit.md` geprüft.
- [ ] **Gesamtexport** einmal mit Bestätigung (Code oder Passkey) erstellt, ZIP geöffnet und wieder gelöscht.
- [ ] **Datenschutz:** Freigabe durch Bürgermeister und DSB liegt vor; vor der Altdatenübernahme der
      Freigabevermerk im Import.
- [ ] **CI** des Release-Commits grün, einschließlich Paketprüfung und Trivy; offene Dependabot-PRs gesichtet.

## Sicherung

**Was:** die gesamte Datenbank als `pg_dump` (Custom-Format) – Mitglieder, Einsätze, Dienste,
Benutzerkonten, Änderungsprotokoll, Anmeldeschlüssel und die in der Datenbank abgelegten Dokumente –
sowie `.env`, `compose.yml` und `Caddyfile`. Nicht gesichert wird das Zertifikat (holt Caddy neu).

**Wie:** `sicherung.sh` schreibt den Dump zuerst in eine Datei und prüft, dass er lesbar ist. Erst dann
lädt restic ihn verschlüsselt (AES-256) und dedupliziert hoch. Ein abgebrochener Dump wird nie als
gültiger Stand gespeichert. Sonntags liest restic zusätzlich 5 % der Daten im Ziel zur Kontrolle.

**Wann:** täglich gegen 02:30 Uhr (bis 02:45 Uhr gestreut). War der Server um die Zeit aus, holt
systemd die Sicherung nach dem Start nach.

**Wie lange:** 14 tägliche, 8 wöchentliche und 12 monatliche Stände (`SICHERUNG_*` in `.env`). Gelöschte
Datensätze verschwinden damit spätestens nach einem Jahr auch aus der Sicherung; das Löschkonzept der
Datenschutz-Unterlagen geht von diesen Werten aus.

Von Hand: `/opt/fevad/sicherung.sh`. Protokoll: `journalctl -u fevad-sicherung`.

**Zweite Kopie (empfohlen):** einmal im Monat `restic copy` auf eine USB-Platte, die im Rathaus liegt,
oder ein zweites Repository bei einem anderen Anbieter. Damit übersteht die Sicherung auch den Verlust
des Kontos beim Sicherungsanbieter.

## Restore-Test

`sicherung-testen.sh` läuft jeden Sonntag gegen 04:30 Uhr und ändert nichts an der laufenden Anwendung:

1. holt den neuesten Stand aus dem Sicherungsziel,
2. spielt ihn in eine Wegwerf-Datenbank (eigener Container ohne Netzwerk) ein,
3. vergleicht Tabellen, Zeilenzahlen und Migrationsstand mit der laufenden Datenbank und gibt die
   Gegenüberstellung aus,
4. löscht die Wegwerf-Datenbank wieder und meldet das Ergebnis an die Überwachung.

Er scheitert, wenn der Stand fehlt, nicht einspielbar ist, Tabellen fehlen oder die Datenbank leer
ist. Protokoll: `journalctl -u fevad-sicherungstest`.

Zusätzlich einmal im Quartal eine **vollständige Wiederherstellung auf einem Testserver** durchspielen
(neuer kleiner VPS, „Neuer Server nach Totalausfall“ unten), danach den Testserver löschen. Das prüft
auch, ob Passwort und Schlüssel im Rathaus wirklich passen.

## Wiederherstellung

### Einzelner Stand auf demselben Server

```bash
cd /opt/fevad
./wiederherstellen.sh --liste          # verfügbare Stände mit ID und Uhrzeit
./wiederherstellen.sh                  # neuester Stand
./wiederherstellen.sh 1a2b3c4d         # bestimmter Stand
```

Das Skript zeigt den Stand, fragt nach der Eingabe `WIEDERHERSTELLEN`, sichert den aktuellen Zustand
vorher noch einmal, hält die Anwendung an, legt die Datenbank neu an, spielt den Stand ein und startet
die Anwendung wieder. Änderungen seit dem gewählten Stand sind danach weg; Anmeldungen bleiben gültig.

Nach einer Wiederherstellung Daten, die nach dem Stand gelöscht worden waren, erneut löschen: unter
**Datenschutz** den Löschlauf vor der Freigabe an die Benutzer einmal von Hand anstoßen. Einzeln
endgültig gelöschte Dokumente stehen mit Zeitpunkt in den Löschnachweisen und müssen erneut gelöscht werden.

### Neuer Server nach Totalausfall

1. Neuen Server wie oben einrichten (Grundabsicherung, Docker), Ordner `deploy/` nach `/opt/fevad`.
2. SSH-Schlüssel und `known_hosts` für das Sicherungsziel nach `/opt/fevad/ssh/` legen.
3. Konfiguration aus der Sicherung holen (Passwort aus dem Rathaus):
   ```bash
   cd /opt/fevad
   docker run --rm -it -e RESTIC_REPOSITORY=sftp://… -v "$PWD/ssh:/root/.ssh:ro" -v "$PWD:/ziel" \
     restic/restic:0.18.1 restore latest --tag fevad --target /ziel --include /sicherung/.env
   mv sicherung/.env .env && rm -r sicherung && chmod 600 .env
   ```
4. `./einrichten.sh` (installiert die Timer; die vorhandene `.env` bleibt).
5. DNS-Eintrag auf die neue Server-Adresse umstellen, Stack starten (legt eine leere Datenbank an)
   und den Stand einspielen:
   ```bash
   docker compose up -d
   ./wiederherstellen.sh      # sichert vorher die leere Datenbank mit, das ist harmlos
   ```
6. Release-Checkliste durchgehen.

## Updates

### FEVAD

Releases entstehen als GitHub-Release (README, „Versionen und Releases“). Auf dem Server immer eine feste
Version eintragen, nicht `latest`:

```bash
cd /opt/fevad
./aktualisieren.sh 0.2.0
```

Ablauf: Image holen (bricht ab, wenn es die Version nicht gibt), Sicherung, Version in `.env`
eintragen, Container neu starten, bis zu 5 Minuten auf „Healthy“ warten. Datenbankmigrationen
laufen beim Start der Anwendung automatisch.

Vorher die Release-Notizen auf GitHub lesen. Eine neue MAJOR-Version (z. B. `2.0.0`) verlangt laut
Versionierungsregel Handarbeit, die in den Notizen steht.

**Zurück auf die alte Version:** Startet die neue Version nicht, zeigt das Skript die Schritte an.
Kurz: alte Version in `.env` eintragen; hat die neue Version die Datenbank schon migriert, zusätzlich
den Stand von unmittelbar vor dem Update mit `./wiederherstellen.sh` zurückspielen (das Update hat ihn
eben angelegt); dann `./aktualisieren.sh`.

Neue Fassungen der Dateien in `deploy/` (Skripte, `compose.yml`) kommen nicht über das Image. Bei
einem Release, dessen Notizen das erwähnen:

```bash
cd /root/fevad-repo && git pull
cp /root/fevad-repo/deploy/*.sh /root/fevad-repo/deploy/compose.yml /root/fevad-repo/deploy/Caddyfile /opt/fevad/
cp -r /root/fevad-repo/deploy/systemd /opt/fevad/ && /opt/fevad/einrichten.sh
```

`.env` wird dabei nie überschrieben; neue Einstellungen stehen in `env.beispiel` und werden von Hand
übernommen.

### PostgreSQL, Caddy, Betriebssystem

- Betriebssystem: Sicherheitsupdates spielt `unattended-upgrades` ein. Einmal im Monat
  `apt full-upgrade` und bei Kernel-Updates `reboot` (Container starten von selbst).
- Caddy und PostgreSQL (innerhalb von Version 17) holt `aktualisieren.sh` bei jedem Lauf mit; ohne
  FEVAD-Update: `./aktualisieren.sh` ohne Versionsangabe.
- Ein Wechsel auf eine neue PostgreSQL-Hauptversion (z. B. 18) geht nur über Dump und Restore und
  kommt als eigenes FEVAD-Release mit Anleitung.

## Überwachung

| Prüfung | Wie oft | Meldet sich bei | Ping-URL in `.env` |
|---|---|---|---|
| Wächter | alle 5 Minuten | Anwendung nicht „Healthy“, HTTPS nicht erreichbar, Zertifikat läuft in weniger als 14 Tagen ab, Platte über 85 %, Container gestoppt | `PING_WAECHTER` |
| Sicherung | täglich | Fehler im Dump oder beim Hochladen | `PING_SICHERUNG` |
| Restore-Test | sonntags | Stand fehlt oder nicht einspielbar | `PING_SICHERUNGSTEST` |

Der Überwachungsdienst erwartet regelmäßig ein Lebenszeichen. Bleibt es aus, weil der Server, das
Rechenzentrum oder das Netz weg ist, schlägt er ebenfalls Alarm. Bei healthchecks.io je Prüfung
einen Check anlegen: Wächter mit Periode 5 Minuten und Karenz 10 Minuten, Sicherung 1 Tag / 2 Stunden,
Restore-Test 7 Tage / 6 Stunden, Zeitzone Europe/Berlin. Benachrichtigung per E-Mail an mindestens
zwei Betreuer.

Ohne Ping-URLs schreiben die Skripte nur ins Systemprotokoll:
`journalctl -u 'fevad-*' --since today`.

Schneller Überblick: `/opt/fevad/status.sh` (Version, Container, letzte Sicherungen, Timer, Platte).

In FEVAD selbst zeigt **Verwaltung → Betrieb** (nur Administrator) Version, Laufzeit, Antwortzeiten,
Serverfehler, letzte Sicherung, letzten Restore-Test und letzten Löschlauf mit Warnungen. Sicherung und
Restore-Test schreiben ihr Ergebnis dafür in die Tabelle `betriebsmeldungen`; die Schwellen stehen in
den Parametern unter „Betrieb“.

Abhängigkeiten: Dependabot schlägt montags Updates für NuGet, GitHub Actions und das Basis-Image vor;
die CI bricht bei Paketen mit bekannter Sicherheitslücke und bei kritischen, behebbaren Lücken im Image.

## Protokolle und Datenschutz

- Caddy schreibt bewusst **kein Zugriffsprotokoll** mit IP-Adressen, nur Fehler.
- Container-Protokolle sind auf 5 × 10 MB je Dienst begrenzt (`docker compose logs app`).
- Das Systemprotokoll (SSH-Anmeldungen mit IP-Adressen) wird nach 14 Tagen gelöscht (siehe
  Grundabsicherung).
- PostgreSQL ist nur im internen Docker-Netz erreichbar; Zugriff für Wartung nur über den Server:
  `docker compose exec postgres psql -U fevad -d fevad`.
- Die Anwendung nimmt `X-Forwarded-*` nur aus dem internen Netz (`FEVAD_NETZ`) an.
- `.env` enthält alle Geheimnisse, ist nur für root lesbar und liegt nicht im Repository.

## Häufige Störungen

| Anzeichen | Ursache und Abhilfe |
|---|---|
| Browser meldet Zertifikatsfehler direkt nach dem Einrichten | DNS zeigt noch nicht auf den Server oder Port 80 ist zu. `docker compose logs caddy` zeigt den Grund; Caddy versucht es selbst erneut. |
| Seite meldet „503“ | Anwendung startet noch oder ist abgestürzt: `docker compose logs --tail 100 app`. |
| Anwendung startet nicht, Log „Konfiguration unvollständig“ | Fehlende Pflichtwerte in `.env`, Meldung nennt den Schlüssel. Korrigieren, `docker compose up -d`. |
| Keine E-Mails | `MAIL_*` in `.env` prüfen; Warnung im Log beim Start. Links lassen sich in der Benutzerverwaltung auch kopieren. |
| Sicherung meldet „Repository nicht erreichbar“ | Zugang zum Sicherungsziel (SSH-Schlüssel, `known_hosts`, Konto) prüfen: `./status.sh`. |
| Sicherung meldet „repository is already locked“ | Ein vorheriger Lauf wurde hart abgebrochen. `docker run --rm --env-file .env -v "$PWD/ssh:/root/.ssh:ro" restic/restic:0.18.1 unlock`. |
| Platte voll | `docker system df`, alte Images: `docker image prune -a`. Datenbankgröße: `./status.sh`. |

## Papier-Notfallsatz (R18)

Unter Verwaltung › Papier-Notfallsatz druckt der Kommandant (oder der Stellvertreter) einmal im Monat einen
Sammeldruck: Einsatzabteilung mit Telefon, Notfallkontakt und Atemschutz-Einsetzbarkeit (nur ja/nein),
Prüfstand der Fahrzeuge, Geräte mit fälliger Prüfung oder Mangel und eine leere Anwesenheitsliste mit
vorgedruckten Namen. Der Satz liegt in einer verschlossenen Mappe im Gerätehaus; Schlüssel haben Kommandant,
Stellvertreter und Gruppenführer. Beim Austausch die alte Fassung im Aktenvernichter vernichten (mindestens
Sicherheitsstufe P-4 nach DIN 66399). Jeder Druck wird vermerkt; ist der letzte älter als der Parameter
`Betrieb.Notfallsatz.ErneuernNachTagen` (31 Tage), erinnert „Was liegt an“. Nach einem Ausfall die
Anwesenheit aus der Liste in FEVAD nachtragen (Dienste und Einsätze sind rückdatierbar).

## Messwerte für den Notbetrieb (F10, E13)

Festlegung (Andi, 25.10.2026): erst messen, dann über den Notbetrieb Stufe 2 (V67) entscheiden (F30).
Bis dahin gilt der Papier-Notfallsatz. So wird gemessen:

1. **Internet am Gerätehaus**, vier Wochen lang (R19): ein Gerät im Gerätehaus (Laptop oder Raspberry Pi)
   ruft alle 5 Minuten `https://<FEVAD_DOMAIN>/health` auf und schreibt Zeit, Ergebnis und Dauer in
   eine Datei, z. B. `while true; do echo "$(date '+%F %T') $(curl -s -o /dev/null -w '%{http_code} %{time_total}' -m 10 https://<FEVAD_DOMAIN>/health)"; sleep 300; done >> messung.txt`.
   Auswertung: Anteil der Ausfälle, längster Ausfall, Antwortzeit (Median).
2. **Mobilfunk in der Halle:** mit zwei Handys verschiedener Netze an den Stellplätzen der Fahrzeuge
   und an der Kleiderkammer je ein Speedtest; Ergebnis in eine Tabelle (Ort, Netz, Download, Upload, Ping).
3. **WLAN-Ausleuchtung** der Halle mit einer WLAN-Analyse-App: Signalstärke je Stellplatz;
   unter −70 dBm gilt als schwach.
4. **Strom:** Gibt es eine Notstromeinspeisung, und welche Steckdosen hängen daran (Router, PC)?

Entscheidungshilfe: Ist das Internet in vier Wochen nie länger als eine Stunde am Stück weg und reicht WLAN
oder Mobilfunk an allen Stellplätzen, genügt ein besserer Zugangspunkt in der Halle statt eines
Replikats. Sonst Stufe 2 mit Lese-Replikat und Outbox (V67) planen.

## Mehrere Gemeinden (V73, nicht gebaut)

Eine Instanz für mehrere Gemeinden des Verwaltungsverbands ist bewusst nicht gebaut. Die Daten hängen an
genau einer Organisation (Stammdaten: Feuerwehr und Trägergemeinde als Verantwortliche); Abfragen holen
sie zentral über `OrganisationAsync`. Ein späterer Ausbau würde eine Mandanten-Spalte und einen globalen
Abfragefilter ergänzen, ohne bestehende Tabellen umzubauen. Andere Wehren nutzen FEVAD über eine eigene
Instanz (EUPL-Lizenz); das ist auch datenschutzrechtlich die einfachere Lösung, weil jede Gemeinde
eigene Verantwortliche bleibt.

## Später: Gerätehaus-PC (Stufe 2)

Der Gerätehaus-PC bekommt einen schreibgeschützten Spiegel der Datenbank und den Notbetrieb
(`Betrieb:Notbetrieb`). Vorgesehen: Replikation nur über VPN (WireGuard) oder TLS, vollverschlüsselte
Platte (LUKS bzw. BitLocker), abgeschlossener Standort. Das Image ist dasselbe; die Anleitung folgt
mit der Umsetzung des Notbetriebs.
