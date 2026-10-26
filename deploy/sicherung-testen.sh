#!/usr/bin/env bash
# Restore-Test: holt den neuesten Stand aus dem restic-Repository, spielt ihn in eine Wegwerf-Datenbank ein
# und vergleicht sie mit der laufenden. Die laufende Anwendung bleibt unberührt.
# Läuft wöchentlich über fevad-sicherungstest.timer.
set -euo pipefail
source "$(dirname "$0")/lib.sh"

ARBEIT="$FEVAD_DIR/sicherungstest-tmp"
TESTDB="fevad-sicherungstest"
aufraeumen() { docker rm -f "$TESTDB" > /dev/null 2>&1 || true; rm -rf "$ARBEIT"; }
ende() {
  local rc=$?
  aufraeumen
  if [[ $rc -ne 0 ]]; then
    ping_melden "${PING_SICHERUNGSTEST:-}" fail "Restore-Test fehlgeschlagen (Exit $rc), siehe journalctl -u fevad-sicherungstest"
    betrieb_melden Restoretest false "Restore-Test fehlgeschlagen (Exit $rc)"
  fi
  exit "$rc"
}
trap ende EXIT

ping_melden "${PING_SICHERUNGSTEST:-}" start
aufraeumen
install -d -m 700 "$ARBEIT"

log "Neuesten Stand holen"
restic_lauf -- snapshots --tag fevad --latest 1
restic_lauf -v "$ARBEIT:/ziel" -- restore latest --tag fevad --host fevad --target /ziel --include /sicherung/fevad.dump
dump="$ARBEIT/sicherung/fevad.dump"
[[ -s "$dump" ]] || { log "Im neuesten Stand fehlt fevad.dump"; exit 1; }

log "In Wegwerf-Datenbank einspielen"
docker run -d --name "$TESTDB" --network none \
  -e POSTGRES_DB=fevad -e POSTGRES_USER=fevad -e POSTGRES_PASSWORD=test \
  "$POSTGRES_IMAGE" > /dev/null
for _ in $(seq 60); do
  docker exec "$TESTDB" pg_isready -U fevad -d fevad -q 2> /dev/null && break
  sleep 1
done
# pg_isready meldet schon während des Initialisierungslaufs; erst der zweite Start ist der echte.
sleep 3
for _ in $(seq 60); do
  docker exec "$TESTDB" psql -U fevad -d fevad -qAtc 'select 1' > /dev/null 2>&1 && break
  sleep 1
done
docker exec -i "$TESTDB" pg_restore -U fevad -d fevad --no-owner --exit-on-error < "$dump"

# Zeilen je Tabelle (exakt gezählt) in laufender und wiederhergestellter Datenbank.
ZAEHLEN=$(cat <<'SQL'
select format('select %L || '' '' || count(*) from %I.%I', table_name, table_schema, table_name)
from information_schema.tables
where table_schema = 'public' and table_type = 'BASE TABLE'
order by table_name \gexec
SQL
)
zaehlen() { "$@" psql -U fevad -d fevad -qAt <<< "$ZAEHLEN"; }

wiederhergestellt=$(zaehlen docker exec -i "$TESTDB")
laufend=$(zaehlen dc exec -T postgres)

migration_alt=$(docker exec "$TESTDB" psql -U fevad -d fevad -qAtc 'select max(migration_id) from "__EFMigrationsHistory"')
migration_neu=$(dc exec -T postgres psql -U fevad -d fevad -qAtc 'select max(migration_id) from "__EFMigrationsHistory"')

tabellen=$(wc -l <<< "$wiederhergestellt")
zeilen=$(awk '{ s += $2 } END { print s + 0 }' <<< "$wiederhergestellt")
log "Wiederhergestellt: $tabellen Tabellen, $zeilen Zeilen, Migration $migration_alt"

# Seit der Sicherung können Daten dazugekommen sein; weniger Tabellen oder eine leere Datenbank sind ein Fehler.
fehler=""
[[ "$tabellen" -lt "$(wc -l <<< "$laufend")" && "$migration_alt" == "$migration_neu" ]] && fehler+="Tabellen fehlen. "
[[ "$zeilen" -eq 0 ]] && fehler+="Datenbank ist leer. "
[[ -z "$migration_alt" ]] && fehler+="Keine Migrationshistorie. "
if [[ -n "$fehler" ]]; then
  log "Restore-Test FEHLGESCHLAGEN: $fehler"
  diff <(echo "$laufend") <(echo "$wiederhergestellt") || true
  exit 1
fi

echo "Tabelle laufend → wiederhergestellt:"
join -a1 <(sort <<< "$laufend") <(sort <<< "$wiederhergestellt") | awk '{ printf "  %-40s %8s → %s\n", $1, $2, $3 }'

ping_melden "${PING_SICHERUNGSTEST:-}" "" "Restore-Test ok: $tabellen Tabellen, $zeilen Zeilen"
betrieb_melden Restoretest true "$tabellen Tabellen, $zeilen Zeilen, Migration $migration_alt"
log "Restore-Test erfolgreich"
