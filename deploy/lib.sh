# Gemeinsame Funktionen der Betriebsskripte. Wird mit "source" eingebunden, nicht direkt aufgerufen.
# shellcheck shell=bash

FEVAD_DIR="${FEVAD_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)}"
RESTIC_IMAGE="${RESTIC_IMAGE:-restic/restic:0.18.1}"
POSTGRES_IMAGE="${POSTGRES_IMAGE:-postgres:17}"

if [[ ! -f "$FEVAD_DIR/.env" ]]; then
  echo "Fehlt: $FEVAD_DIR/.env (zuerst einrichten.sh ausführen)" >&2
  exit 1
fi
set -a
# shellcheck source=/dev/null
source "$FEVAD_DIR/.env"
set +a

log() { echo "$(TZ=Europe/Berlin date '+%Y-%m-%d %H:%M:%S') $*"; }

dc() { docker compose --project-directory "$FEVAD_DIR" -f "$FEVAD_DIR/compose.yml" --env-file "$FEVAD_DIR/.env" "$@"; }

# restic im Container, damit auf dem Server nur Docker nötig ist.
# $FEVAD_DIR/ssh enthält Schlüssel und known_hosts für ein SFTP-Ziel; weitere -v-Optionen vor "--".
restic_lauf() {
  local optionen=()
  while [[ $# -gt 0 && "$1" != "--" ]]; do optionen+=("$1"); shift; done
  [[ "${1:-}" == "--" ]] && shift
  : "${RESTIC_REPOSITORY:?RESTIC_REPOSITORY fehlt in .env}" "${RESTIC_PASSWORD:?RESTIC_PASSWORD fehlt in .env}"
  docker run --rm \
    -e RESTIC_REPOSITORY -e RESTIC_PASSWORD -e TZ=Europe/Berlin \
    -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY \
    -v fevad_restic-cache:/root/.cache/restic \
    -v "$FEVAD_DIR/ssh:/root/.ssh:ro" \
    --hostname fevad \
    "${optionen[@]}" \
    "$RESTIC_IMAGE" "$@"
}

# Meldung an die Überwachung (healthchecks.io-kompatibel): ping <url> [start|fail|<exitcode>] [text]
ping_melden() {
  local url="$1" art="${2:-}" text="${3:-}"
  [[ -z "$url" ]] && return 0
  [[ -n "$art" ]] && url="$url/$art"
  curl -fsS -m 10 --retry 3 --data-raw "$text" "$url" >/dev/null || log "Hinweis: Überwachung unter $url nicht erreichbar"
}

# Wartet, bis /health der Anwendung „Healthy“ meldet (Abfrage aus dem Caddy-Container im internen Netz).
warten_auf_anwendung() {
  local sekunden="${1:-180}" ende=$((SECONDS + ${1:-180}))
  while (( SECONDS < ende )); do
    if [[ "$(dc exec -T caddy wget -qO- -T 5 http://app:8080/health 2>/dev/null)" == "Healthy" ]]; then
      return 0
    fi
    sleep 3
  done
  log "Anwendung meldet nach $sekunden s nicht „Healthy“."
  return 1
}
