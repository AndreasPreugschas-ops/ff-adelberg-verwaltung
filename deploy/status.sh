#!/usr/bin/env bash
# Überblick für den Administrator: Version, Container, letzte Sicherungen, Timer, Plattenplatz.
set -euo pipefail
source "$(dirname "$0")/lib.sh"

echo "== FEVAD $FEVAD_VERSION auf https://$FEVAD_DOMAIN"
dc ps --format 'table {{.Service}}\t{{.Image}}\t{{.Status}}'
echo
echo "== Gesundheit: $(dc exec -T caddy wget -qO- -T 5 http://app:8080/health 2>/dev/null || echo 'keine Antwort')"
echo
echo "== Letzte Sicherungen"
restic_lauf -- snapshots --tag fevad --latest 5 --compact || echo "(Repository nicht erreichbar)"
echo
echo "== Zeitpläne"
systemctl list-timers 'fevad-*' --no-pager 2>/dev/null || true
echo
echo "== Plattenplatz"
df -h / /var/lib/docker 2>/dev/null | sort -u
