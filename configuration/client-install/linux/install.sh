#!/bin/sh
# Installs the GHOSTS Universal client on a Linux host and runs it as a systemd service.
#
#   sudo ./install.sh --api http://ghosts-api:5000/api --package ghosts-client-universal.zip [--user ghosts] [--dir /opt/ghosts]
#
# --package  a zip of `dotnet publish src/Ghosts.Client.Universal` output
# --api      the GHOSTS API root, as the host reaches it on the management network
# --user     the account the client runs as (created if missing); not root, so browsers behave
# --dir      where the client is installed
# --root     install under this directory instead of /, without creating the user or touching systemd
#            (for tests and image builds)
#
# The host must report a hostname equal to its name in the terrain manifest: GHOSTS matches clients by exact name.
# Needs: unzip, and the .NET 10 runtime at /usr/bin/dotnet.
set -eu

HERE=$(cd "$(dirname "$0")" && pwd)
API="" PACKAGE="" USER_NAME="ghosts" DIR="/opt/ghosts" ROOT=""

usage() { sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//' >&2; exit 2; }

while [ $# -gt 0 ]; do
  case "$1" in
    --api) API="${2:-}"; shift 2 ;;
    --package) PACKAGE="${2:-}"; shift 2 ;;
    --user) USER_NAME="${2:-}"; shift 2 ;;
    --dir) DIR="${2:-}"; shift 2 ;;
    --root) ROOT="${2:-}"; shift 2 ;;
    -h|--help) usage ;;
    *) echo "Unknown option: $1" >&2; usage ;;
  esac
done
[ -n "$API" ] && [ -n "$PACKAGE" ] || usage
[ -f "$PACKAGE" ] || { echo "No package at $PACKAGE" >&2; exit 2; }
command -v unzip > /dev/null || { echo "unzip is required" >&2; exit 1; }

TARGET="$ROOT$DIR"
UNIT_DIR="$ROOT/etc/systemd/system"
# The client's files, and later a recursive chown, go into --dir: refuse one that holds anything but an earlier install.
if [ -d "$TARGET" ] && [ -n "$(ls -A "$TARGET")" ] && [ ! -f "$TARGET/.ghosts-client" ]; then
  echo "$DIR already holds other files; give --dir an empty or new directory" >&2
  exit 2
fi
mkdir -p "$TARGET" "$UNIT_DIR"

echo "Installing the GHOSTS client into $TARGET"
unzip -oq "$PACKAGE" -d "$TARGET"
rm -rf "$TARGET/instance"   # an instance folder copied from another host would carry its id
touch "$TARGET/.ghosts-client"

mkdir -p "$TARGET/config"
sed "s|__GHOSTS_API__|$API|g" "$HERE/../application.json" > "$TARGET/config/application.json"

sed -e "s|__DIR__|$DIR|g" -e "s|__GHOSTS_API__|$API|g" -e "s|__USER__|$USER_NAME|g" \
  "$HERE/ghosts-client.service" > "$UNIT_DIR/ghosts-client.service"

if [ -n "$ROOT" ]; then
  echo "Installed under $ROOT; skipped the user and systemd."
  exit 0
fi

if ! id "$USER_NAME" > /dev/null 2>&1; then
  useradd --system --create-home --shell /usr/sbin/nologin "$USER_NAME"
fi
chown -R "$USER_NAME:$USER_NAME" "$TARGET"
systemctl daemon-reload
systemctl enable ghosts-client.service
systemctl restart ghosts-client.service   # restart, so a re-install runs the new client
echo "GHOSTS client installed and started as $USER_NAME. Status: systemctl status ghosts-client"
