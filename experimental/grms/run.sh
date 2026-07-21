#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$SCRIPT_DIR"
UI_DIR="$SCRIPT_DIR/frontend"

API_PORT="${GRMS_PORT:-8090}"
UI_PORT="${PORT:-4200}"

# ── cleanup ──────────────────────────────────────────────────────────────────
API_PID=""
UI_PID=""

cleanup() {
  echo ""
  echo "Stopping..."
  [[ -n "$API_PID" ]] && kill "$API_PID" 2>/dev/null || true
  [[ -n "$UI_PID" ]]  && kill "$UI_PID"  2>/dev/null || true
  wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

# ── API ───────────────────────────────────────────────────────────────────────
echo "==> Starting GRMS API on port $API_PORT..."

cd "$API_DIR"

if [[ ! -d ".venv" ]]; then
  echo "    Creating Python virtual environment..."
  python3 -m venv .venv
fi

source .venv/bin/activate

pip install -q -r requirements.txt

uvicorn grms.main:app --host 0.0.0.0 --port "$API_PORT" &
API_PID=$!

# ── Frontend ──────────────────────────────────────────────────────────────────
echo "==> Starting GRMS frontend on port $UI_PORT..."

cd "$UI_DIR"

if [[ ! -d "node_modules" ]]; then
  echo "    Installing npm dependencies..."
  npm install
fi

PORT="$UI_PORT" npm start &
UI_PID=$!

# ── wait ──────────────────────────────────────────────────────────────────────
echo ""
echo "  API:      http://localhost:$API_PORT"
echo "  Docs:     http://localhost:$API_PORT/docs"
echo "  Frontend: http://localhost:$UI_PORT"
echo ""
echo "Press Ctrl+C to stop."

wait
