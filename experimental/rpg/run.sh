#!/usr/bin/env bash
# Run the GHOSTS RPG API (FastAPI) and frontend (Angular) together.
# Both bind to 0.0.0.0 so they can be reached through dev-container port forwarding.
#   API:      http://localhost:8095
#   Frontend: http://localhost:4300
# In VS Code, make sure BOTH ports (4300 and 8095) are forwarded in the Ports panel.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_PORT=8095
FE_PORT=4300
VENV="$ROOT/.venv"

if [ ! -x "$VENV/bin/python" ]; then
  echo "error: venv not found at $VENV (expected $VENV/bin/python)" >&2
  exit 1
fi

# Stop anything already holding the ports.
pkill -f "cd uvicorn src/ghosts_rpg" 2>/dev/null || true
pkill -f "ng serve" 2>/dev/null || true
sleep 1

# Clean up child processes on Ctrl+C / exit.
cleanup() {
  echo
  echo "stopping..."
  kill "${API_PID:-}" "${FE_PID:-}" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "starting API on http://0.0.0.0:$API_PORT ..."
(
  cd "$ROOT"
  exec "$VENV/bin/python" -m uvicorn --app-dir "$ROOT/src" ghosts_rpg.app:app --host 0.0.0.0 --port "$API_PORT"
) &
API_PID=$!

echo "starting frontend on http://0.0.0.0:$FE_PORT ..."
(
  cd "$ROOT/frontend"
  exec npm start -- --host 0.0.0.0 --port "$FE_PORT"
) &
FE_PID=$!

echo
echo "  API:      http://localhost:$API_PORT   (pid $API_PID)"
echo "  Frontend: http://localhost:$FE_PORT   (pid $FE_PID)"
echo "  (forward both ports in the VS Code Ports panel; Ctrl+C to stop both)"
echo

# Exit if either process dies.
wait -n "$API_PID" "$FE_PID"
