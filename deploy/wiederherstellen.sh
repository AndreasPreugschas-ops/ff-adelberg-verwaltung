#!/usr/bin/env bash
# Spielt einen Stand aus der Sicherung in die laufende Datenbank zurück. ÜBERSCHREIBT alle aktuellen Daten.
# Aufruf: ./wiederherstellen.sh            (neuester Stand)
#         ./wiederherstellen.sh <snapshot> (ID aus "./wiederherstellen.sh --liste")
set -euo pipefail
source "$(dirname "$0")/lib.sh"

if [[ "${1:-}" == "--liste" ]]; then
  restic_lauf -- snapshots --tag fevad
  exit 0
fi
stand="${1:-latest}"

ARBEIT="$FEVAD_DIR/wiederherstellung-tmp"
trap 'rm -rf "$ARBEIT"' EXIT
rm -rf "$ARBEIT"
install -d -m 700 "$ARBEIT"

restic_lauf -v "$ARBEIT:/ziel" -- restore "$stand" --tag fevad --target /ziel --include /sicherung/fevad.dump
dump="$ARBEIT/sicherung/fevad.dump"
[[ -s "$dump" ]] || { log "Im Stand $stand fehlt fevad.dump"; exit 1; }
dc exec -T postgres pg_restore --list < "$dump" > /dev/null

echo
restic_lauf -- snapshots --tag fevad "$stand"
echo "Achtung: Alle Daten in der laufenden FEVAD-Datenbank werden durch diesen Stand ersetzt."
echo "Änderungen seit dieser Sicherung gehen verloren. Vorher wird der aktuelle Stand zusätzlich gesichert."
read -r -p "Zum Fortfahren WIEDERHERSTELLEN eintippen: " antwort
[[ "$antwort" == "WIEDERHERSTELLEN" ]] || { echo "Abgebrochen."; exit 1; }

log "Aktuellen Stand vorher sichern"
"$FEVAD_DIR/sicherung.sh"

log "Anwendung anhalten"
dc stop app

log "Datenbank neu anlegen und Stand einspielen"
dc exec -T postgres psql -U fevad -d postgres -v ON_ERROR_STOP=1 -c 'drop database fevad with (force)' -c 'create database fevad owner fevad'
dc exec -T postgres pg_restore -U fevad -d fevad --no-owner --exit-on-error < "$dump"

log "Anwendung starten"
dc up -d app
warten_auf_anwendung 180
log "Wiederherstellung fertig. Anmeldungen bleiben gültig, solange die Schlüssel im Stand enthalten waren."
