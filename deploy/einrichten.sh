#!/usr/bin/env bash
# Ersteinrichtung auf dem Server (als root, im Verzeichnis /opt/fevad):
#   legt .env mit Zufallsgeheimnissen an (falls noch nicht vorhanden) und installiert die systemd-Timer
#   für Sicherung, Restore-Test und Wächter. Mehrfach aufrufen schadet nicht.
set -euo pipefail
FEVAD_DIR="$(cd "$(dirname "$0")" && pwd)"

[[ $EUID -eq 0 ]] || { echo "Bitte als root ausführen (sudo $0)." >&2; exit 1; }
command -v docker > /dev/null || { echo "Docker fehlt, siehe docs/Betrieb.md." >&2; exit 1; }

zufall() { openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c "$1"; }

if [[ ! -f "$FEVAD_DIR/.env" ]]; then
  install -m 600 "$FEVAD_DIR/env.beispiel" "$FEVAD_DIR/.env"
  sed -i \
    -e "s/^POSTGRES_PASSWORD=$/POSTGRES_PASSWORD=$(zufall 40)/" \
    -e "s/^RESTIC_PASSWORD=$/RESTIC_PASSWORD=$(zufall 48)/" \
    -e "s/^FE2_WEBHOOK_TOKEN=$/FE2_WEBHOOK_TOKEN=$(zufall 48)/" \
    "$FEVAD_DIR/.env"
  echo ".env angelegt. Jetzt FEVAD_DOMAIN, ACME_EMAIL, Mail, ersten Administrator und RESTIC_REPOSITORY eintragen:"
  echo "  nano $FEVAD_DIR/.env"
  echo "RESTIC_PASSWORD zusätzlich außerhalb des Servers verwahren (Passwortsafe, Ausdruck im Rathaus)."
fi
chmod 600 "$FEVAD_DIR/.env"
install -d -m 700 "$FEVAD_DIR/ssh"

for datei in "$FEVAD_DIR"/systemd/*.service "$FEVAD_DIR"/systemd/*.timer; do
  sed "s|@FEVAD_DIR@|$FEVAD_DIR|g" "$datei" > "/etc/systemd/system/$(basename "$datei")"
done
systemctl daemon-reload
systemctl enable --now fevad-sicherung.timer fevad-sicherungstest.timer fevad-waechter.timer
echo "Timer aktiv:"
systemctl list-timers 'fevad-*' --no-pager
