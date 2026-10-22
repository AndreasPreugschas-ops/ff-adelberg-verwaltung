#!/usr/bin/env bash
# Update auf ein Release aus GHCR: sichert vorher, zieht das Image, startet neu und prüft /health.
# Aufruf: ./aktualisieren.sh 0.2.0     (empfohlen: feste Version, siehe GitHub-Releases)
#         ./aktualisieren.sh            (nur Neustart mit der Version aus .env, z. B. nach .env-Änderung)
set -euo pipefail
source "$(dirname "$0")/lib.sh"

alt="${FEVAD_VERSION:?}"
neu="${1:-$alt}"
neu="${neu#v}"

log "Image ghcr.io/andreaspreugschas-ops/fevad:$neu holen"
FEVAD_VERSION="$neu" dc pull app

log "Vorher sichern"
"$FEVAD_DIR/sicherung.sh"

if [[ "$neu" != "$alt" ]]; then
  sed -i "s/^FEVAD_VERSION=.*/FEVAD_VERSION=$neu/" "$FEVAD_DIR/.env"
  log "Version in .env: $alt → $neu"
fi
# lib.sh hat .env exportiert; Compose nimmt Umgebungsvariablen vor .env.
export FEVAD_VERSION="$neu"

dc pull postgres caddy
dc up -d --remove-orphans

if warten_auf_anwendung 300; then
  log "FEVAD $neu läuft."
  docker image prune -f > /dev/null
else
  dc logs --tail 50 app
  cat <<HINWEIS

Die neue Version startet nicht. Zurück auf $alt:
  1. sed -i 's/^FEVAD_VERSION=.*/FEVAD_VERSION=$alt/' $FEVAD_DIR/.env
  2. Hat die neue Version die Datenbank schon migriert, startet auch $alt nicht mehr.
     Dann zusätzlich den Stand von eben zurückspielen: $FEVAD_DIR/wiederherstellen.sh
  3. $FEVAD_DIR/aktualisieren.sh
HINWEIS
  exit 1
fi
