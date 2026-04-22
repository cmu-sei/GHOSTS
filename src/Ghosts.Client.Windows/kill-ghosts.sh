#!/usr/bin/env bash
# Linux/macOS equivalent of kill-ghosts.bat
# Stops GHOSTS client and any browser/driver processes it may have spawned.

set -euo pipefail

kill_if_running() {
    pkill -f "$1" 2>/dev/null && echo "Stopped: $1" || true
}

kill_if_running "ghosts.client"
kill_if_running "geckodriver"
kill_if_running "chromedriver"
kill_if_running "firefox"
kill_if_running "chrome"

echo "Done."
