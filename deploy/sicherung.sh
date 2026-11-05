#!/usr/bin/env bash
# Nächtliche Sicherung: Datenbank-Dump (inkl. Dokumente) und Konfiguration verschlüsselt per restic,
# danach alte Stände ausdünnen. Läuft über fevad-sicherung.timer, kann jederzeit von Hand gestartet werden.
set -euo pipefail
source "$(dirname "$0")/lib.sh"

ARBEIT="$FEVAD_DIR/sicherung-tmp"
ende() {
  local rc=$?
  rm -rf "$ARBEIT"
  if [[ $rc -ne 0 ]]; then
    ping_melden "${PING_SICHERUNG:-}" fail "Sicherung fehlgeschlagen (Exit $rc), siehe journalctl -u fevad-sicherung"
    betrieb_melden Sicherung false "Sicherung fehlgeschlagen (Exit $rc)"
  fi
  exit "$rc"
}
trap ende EXIT

ping_melden "${PING_SICHERUNG:-}" start
rm -rf "$ARBEIT"
install -d -m 700 "$ARBEIT"

log "Datenbank sichern"
# Erst in eine Datei, damit ein abgebrochener Dump nie als gültiger Stand im Repository landet.
dc exec -T postgres pg_dump -U fevad -d fevad --format=custom --compress=6 > "$ARBEIT/fevad.dump"
dc exec -T postgres pg_restore --list < "$ARBEIT/fevad.dump" > /dev/null
groesse=$(du -h "$ARBEIT/fevad.dump" | cut -f1)
log "Dump ist lesbar ($groesse)"

# Konfiguration mitsichern, damit ein neuer Server ohne Raten aufgesetzt werden kann.
cp "$FEVAD_DIR/.env" "$FEVAD_DIR/compose.yml" "$FEVAD_DIR/Caddyfile" "$ARBEIT/"

if ! restic_lauf -- cat config > /dev/null 2>&1; then
  log "Repository wird angelegt"
  restic_lauf -- init
fi

log "Hochladen"
restic_lauf -v "$ARBEIT:/sicherung:ro" -- backup /sicherung --tag fevad --host fevad --no-scan

log "Alte Stände ausdünnen"
restic_lauf -- forget --tag fevad --host fevad --prune \
  --keep-daily "${SICHERUNG_TAEGLICH:-14}" --keep-weekly "${SICHERUNG_WOECHENTLICH:-8}" --keep-monthly "${SICHERUNG_MONATLICH:-12}"

# Ausbaustufe „Mittel“: Zweitkopie bei einem zweiten Anbieter. Übersteht den Verlust des Kontos oder Rechenzentrums
# beim Sicherungsanbieter. Leer = keine Zweitkopie (Stufe „Klein“, dort monatlich von Hand auf USB, docs/Betrieb.md).
if [[ -n "${ZWEITKOPIE_REPOSITORY:-}" ]]; then
  if ! restic_zweitkopie cat config > /dev/null 2>&1; then
    log "Zweitkopie wird angelegt"
    restic_zweitkopie init --from-repo "$RESTIC_REPOSITORY" --copy-chunker-params
  fi
  log "Zweitkopie übertragen"
  restic_zweitkopie copy --tag fevad --host fevad
  restic_zweitkopie forget --tag fevad --host fevad --prune \
    --keep-daily "${SICHERUNG_TAEGLICH:-14}" --keep-weekly "${SICHERUNG_WOECHENTLICH:-8}" --keep-monthly "${SICHERUNG_MONATLICH:-12}"
fi

# Sonntags zusätzlich einen Teil der Daten im Ziel lesen und prüfen.
if [[ "$(date +%u)" == "7" ]]; then
  log "Repository prüfen (5 % der Daten)"
  restic_lauf -- check --read-data-subset=5%
fi

zweit=""; [[ -n "${ZWEITKOPIE_REPOSITORY:-}" ]] && zweit=", Zweitkopie ok"
ping_melden "${PING_SICHERUNG:-}" "" "Sicherung ok, Dump $groesse$zweit"
betrieb_melden Sicherung true "Dump $groesse$zweit"
log "Sicherung fertig"
