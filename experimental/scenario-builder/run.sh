#!/usr/bin/env bash
# Run the Scenario Builder API (.NET) and UI (Angular) together.
#   API: http://localhost:5280   (SQLite file api/ScenarioBuilder.Api/scenario-builder.db; delete it to reset)
#   UI:  http://localhost:4310   (proxies /api to :5280; builds the agent bundle first)
# Model servers are started separately; see README.md › Models.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ ! -d "$ROOT/ui/node_modules" ]; then
  echo "error: $ROOT/ui/node_modules is missing; install it as README.md › Run it shows" >&2
  exit 1
fi

# Stop both on Ctrl+C / exit. Signal the whole process group: killing npm alone leaves ng serve running.
cleanup() {
  trap - EXIT INT TERM
  echo
  echo "stopping..."
  kill -- -$$ 2>/dev/null || kill "${API_PID:-}" "${UI_PID:-}" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "starting API on http://localhost:5280 ..."
(
  cd "$ROOT/api/ScenarioBuilder.Api"
  exec dotnet run
) &
API_PID=$!

echo "starting UI on http://localhost:4310 ..."
(
  cd "$ROOT/ui"
  # A background job's stdin is /dev/null, so ng's first-run prompts would abort ng serve.
  export NG_FORCE_AUTOCOMPLETE=false NG_CLI_ANALYTICS=false
  # ng serve alone binds only [::1]; VS Code's port forward connects over IPv4.
  exec npm start -- --host 0.0.0.0
) &
UI_PID=$!

echo
echo "  API: http://localhost:5280   (pid $API_PID)"
echo "  UI:  http://localhost:4310   (pid $UI_PID)"
echo "  (Ctrl+C to stop both)"
echo

# Exit if either process dies.
wait -n "$API_PID" "$UI_PID"
