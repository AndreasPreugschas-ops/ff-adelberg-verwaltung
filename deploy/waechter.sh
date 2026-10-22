#!/usr/bin/env bash
# Wächter, alle 5 Minuten über fevad-waechter.timer: prüft Anwendung, Datenbank, HTTPS-Zertifikat und Plattenplatz
# und meldet sich bei der Überwachung. Bleibt die Meldung aus (Server weg, Strom, Netz), schlägt diese Alarm.
set -uo pipefail
source "$(dirname "$0")/lib.sh"

probleme=()

gesundheit=$(dc exec -T caddy wget -qO- -T 10 http://app:8080/health 2>/dev/null || true)
[[ "$gesundheit" == "Healthy" ]] || probleme+=("Anwendung meldet „${gesundheit:-keine Antwort}“")

# Von außen über HTTPS, wie ein Benutzer: prüft Caddy, Zertifikat und Weiterleitung zugleich.
if ! curl -fs -m 15 -o /dev/null --resolve "$FEVAD_DOMAIN:443:127.0.0.1" "https://$FEVAD_DOMAIN/health"; then
  probleme+=("https://$FEVAD_DOMAIN/health nicht erreichbar")
fi

ablauf=$(echo | timeout 15 openssl s_client -connect 127.0.0.1:443 -servername "$FEVAD_DOMAIN" 2>/dev/null \
  | openssl x509 -noout -enddate 2>/dev/null | cut -d= -f2)
if [[ -n "$ablauf" ]]; then
  tage=$(( ($(date -d "$ablauf" +%s) - $(date +%s)) / 86400 ))
  (( tage < ${WAECHTER_ZERTIFIKAT_MIN_TAGE:-14} )) && probleme+=("Zertifikat läuft in $tage Tagen ab")
fi

for pfad in / /var/lib/docker; do
  [[ -d "$pfad" ]] || continue
  belegt=$(df --output=pcent "$pfad" | tail -1 | tr -dc '0-9')
  (( belegt > ${WAECHTER_PLATTE_MAX_PROZENT:-85} )) && probleme+=("Platte $pfad zu $belegt % belegt")
done

for dienst in postgres app caddy; do
  status=$(dc ps -a --format '{{.State}}' "$dienst" 2>/dev/null)
  [[ "$status" == "running" ]] || probleme+=("Container $dienst: ${status:-fehlt}")
done

if (( ${#probleme[@]} > 0 )); then
  text=$(printf '%s\n' "${probleme[@]}")
  log "Probleme:"$'\n'"$text"
  ping_melden "${PING_WAECHTER:-}" fail "$text"
  exit 1
fi
ping_melden "${PING_WAECHTER:-}" "" "ok"
