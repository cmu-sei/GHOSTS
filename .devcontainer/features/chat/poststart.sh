#!/bin/bash
## Bring up the browser-chat stack on container start, when asked to
#
# Declared as this feature's postStartCommand in devcontainer-feature.json, so a
# project's own poststart.sh needs no supervisord block. The real work (volume
# state, secrets, launching/reloading supervisord, blocking until the services
# answer) lives in supervisord/start.sh next to the static config and wrappers it
# drives — see the header comment there, especially the rebuild teardown race.
#
# `|| true` so a hard failure in the helper can't fail the whole postStart step,
# matching how the top-level poststart used to invoke it.
set -euo pipefail

# --- Opt in to autostart ---
# Four long-running services (LiteLLM, Open WebUI, Open Terminal, SearXNG) under
# supervisord, and start.sh BLOCKS until the control endpoint answers before postStart
# can return — so every container start pays for the stack whether or not anyone opens a
# browser. Opt-in instead: set CHAT_AUTOSTART=1 to bring it up automatically.
#
# Read from the container environment, so .devcontainer/devcontainer.env is the place to
# set it — that file is passed through by the --env-file runArg, which applies to the
# whole container and therefore to this hook. (containerEnv/remoteEnv in devcontainer.json
# work too; a plain `export` in a terminal does not, since postStart is not a child of it.)
#
# `${CHAT_AUTOSTART:-}` rather than the bare name: unset is the normal case, and `set -u`
# would abort on it. Exact match on "1" — no truthiness guessing.
#
# Not starting is a normal outcome, not a failure: exit 0 so the feature's postStart step
# stays green. The stack is still one command away — `start_chat_stack`, symlinked onto
# PATH by this feature's postcreate.sh, which always runs before postStart — and calling
# start.sh directly is safe (its own guards no-op on a container that lacks the stack).
if [ "${CHAT_AUTOSTART:-}" != "1" ]; then
    echo "Browser chat stack: not started (run 'start_chat_stack', or set CHAT_AUTOSTART=1 to autostart)."
    exit 0
fi

# The workspace basename doubles as Open WebUI's sidebar branding (WEBUI_NAME).
# start.sh derives the same value when called with no argument; passing it keeps
# the branding source visible at the call site.
#
# Invoked via `bash <path>` (not executed directly) so it runs regardless of the
# exec bit, which a Windows-host bind mount may not preserve — the same reason
# supervisord.conf launches the service wrappers that way.
SCRIPT_DIR=$(dirname "$0")
bash "$SCRIPT_DIR/supervisord/start.sh" "$(basename "$PWD")" || true
