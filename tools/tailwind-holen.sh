#!/usr/bin/env sh
# Lädt das Tailwind-CSS-Standalone-Binary nach tools/tailwindcss.
set -e
VERSION=v4.1.14
case "$(uname -s)-$(uname -m)" in
  Linux-x86_64) DATEI=tailwindcss-linux-x64 ;;
  Linux-aarch64) DATEI=tailwindcss-linux-arm64 ;;
  Darwin-arm64) DATEI=tailwindcss-macos-arm64 ;;
  Darwin-x86_64) DATEI=tailwindcss-macos-x64 ;;
  *) echo "Plattform nicht unterstützt" >&2; exit 1 ;;
esac
ZIEL="$(dirname "$0")/tailwindcss"
curl -sSLo "$ZIEL" "https://github.com/tailwindlabs/tailwindcss/releases/download/$VERSION/$DATEI"
chmod +x "$ZIEL"
echo "Tailwind $VERSION liegt unter $ZIEL"
