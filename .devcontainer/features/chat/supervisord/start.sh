#!/bin/bash
set -euo pipefail

# Bring up the browser-chat stack under supervisord.
#
# The container's four long-running services (LiteLLM, Open Terminal, SearXNG,
# Open WebUI) are managed by a single supervisord instance rather than launched
# as individual nohup daemons. supervisord owns start order (via priority),
# crash recovery (autorestart), and log rotation. Its config and the per-service
# wrapper scripts are STATIC files committed alongside this script under
# .devcontainer/features/chat/supervisord/ — this script only prepares the volume
# state they need (secrets, the SearXNG settings overlay), points supervisord at
# them, starts it once, and BLOCKS until its control endpoint answers.
#
# THE REBUILD TEARDOWN RACE (the reason for the blocking wait): postStartCommand
# WAITS for the chat feature's poststart.sh (which invokes this script) to return,
# and on a rebuild
# the dev container CLI then tears down the transient `docker exec` session the
# instant the command returns — killing any child still mid-startup. supervisord
# wins this race because it daemonizes (nodaemon=false) and reparents to PID 1,
# but only if it is fully up before postStart returns. So we poll its
# inet_http_server on :6090 until it answers: by then supervisord (and the
# services it has spawned, which reparent to it) survive the teardown. (A restart
# runs postStart inside the persistent VS Code server, which never tears down —
# so this only matters on rebuild.)
#
# Invoked by the chat feature's poststart.sh as: supervisord/start.sh "$LOGO_NAME"
# (postStart's cwd is the workspace folder, which we read as $PWD for the Open
# Terminal --cwd.) Guards below no-op cleanly on a container that lacks the stack,
# so it can also be called unconditionally from elsewhere.

# LOGO_NAME (Open WebUI sidebar branding) is passed by poststart, which computed
# it from the workspace basename; fall back to that here if called directly.
LOGO_NAME="${1:-$(basename "$PWD")}"
export LOGO_NAME

# This script's own directory IS SUPERVISORD_DIR (the static conf + wrappers).
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# Everything below is pointless without a LiteLLM config (no profile shipped
# models => no proxy => no chat UI), so gate the whole block on $CONFIG existing.
CONFIG="$HOME/.config/litellm/config.yaml"

DATA="$HOME/.data"
SUP_DATA="$DATA/supervisor"
# Static, committed supervisord config + wrappers (this script's own directory).
SUP_CONF="$SCRIPT_DIR/supervisord.conf"
SUP_HEALTH="http://127.0.0.1:6090/"
SUPERVISORD_BIN="$HOME/.local/bin/supervisord"
OPENWEBUI_BIN="$HOME/.local/bin/open-webui"
LITELLM_BIN="$HOME/.local/bin/litellm"

# The chat stack is installed together or not at all: a spawn omits the ./features/chat
# bundle (litellm/open-webui/open-terminal/searxng/supervisord) from its
# devcontainer.json unless --chat was passed, so supervisord itself is absent. Presence
# of the supervisord binary is therefore the whole signal HERE — this script's job is to
# bring the stack up, and it is also reachable by hand as `start_chat_stack`, so it must
# not second-guess whether the user wants it. WHETHER to call it on a container start is
# decided one level up, by CHAT_AUTOSTART in the feature's poststart.sh.
# Guard the block on both the binary and a LiteLLM config, so a chat-less spawn no-ops.
#
# The two operands fail for very different reasons, so they get different treatment in
# the elif chain at the bottom: a missing BINARY is the expected chat-less path and stays
# silent, while a missing CONFIG in a container that HAS the stack installed is an
# anomaly worth reporting. Do not collapse those branches back into one bare `fi` —
# a silent exit 0 there is indistinguishable from a healthy chat-less no-op, and
# poststart.sh calls this with `|| true`, so nothing else surfaces it either.
if [ -x "$SUPERVISORD_BIN" ] && [ -f "$CONFIG" ]; then
    mkdir -p "$SUP_DATA" \
             "$DATA/litellm" "$DATA/open-terminal/logs" "$DATA/searxng" \
             "$DATA/open-webui" "$DATA/open-webui/hf-cache" \
             "$PWD/.devcontainer/work"

    # --- Persistent per-service secrets (generate once onto the volume) ---
    # All three keys must be STABLE across rebuilds: Open WebUI's
    # TERMINAL_SERVER_CONNECTIONS is a PersistentConfig seeded only on first
    # boot, so a regenerated Open Terminal key would leave its persisted
    # connection pointing at a key the terminal no longer accepts. Read from the
    # kernel CSPRNG to avoid a hard openssl dependency. The wrappers `cat` these
    # at their own runtime.
    for keyfile in \
        "$DATA/open-terminal/api-key" \
        "$DATA/open-webui/secret-key" \
        "$DATA/searxng/secret-key"; do
        if [ ! -s "$keyfile" ]; then
            head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n' > "$keyfile"
            chmod 600 "$keyfile"
        fi
    done

    # --- SearXNG settings overlay ---
    # use_default_settings:true layers this on SearXNG's shipped defaults, so we
    # only override what matters: enable the JSON format Open WebUI's search
    # client requires (defaults to html-only), and keep the bot/rate limiter and
    # public-instance hardening off — this is a single-user loopback instance.
    cat > "$DATA/searxng/settings.yml" <<EOF
use_default_settings: true
server:
  secret_key: "$(cat "$DATA/searxng/secret-key")"
  limiter: false
  public_instance: false
search:
  formats:
    - html
    - json
EOF

    # --- Launch supervisord (or reload if already running) ---
    # The static supervisord.conf expands %(ENV_*)s — supply the values that vary
    # per container: SUPERVISORD_DIR (where the conf + wrappers live), WORKSPACE_DIR
    # (the postStart cwd, for Open Terminal's --cwd) and LOGO_NAME (Open WebUI
    # branding). HOME is already exported. Idempotent across restart vs rebuild: if
    # supervisord already answers :6090 (postStart ran twice in this container's
    # life), reload instead of starting a second instance.
    export SUPERVISORD_DIR="$SCRIPT_DIR"
    export WORKSPACE_DIR="$PWD"
    if curl -sf "$SUP_HEALTH" >/dev/null 2>&1; then
        echo "Reloading supervisord config..."
        supervisorctl -c "$SUP_CONF" update || true
    else
        echo "Starting supervisord (web UI on http://127.0.0.1:6090)..."
        supervisord -c "$SUP_CONF" || true
    fi

    # Block until supervisord's control endpoint answers, so it is fully up and
    # orphaned to PID 1 before postStart returns (wins the teardown race once,
    # instead of once per service).
    for _ in $(seq 1 30); do
        if curl -sf "$SUP_HEALTH" >/dev/null 2>&1; then
            echo "supervisord is ready — services are starting under it."
            break
        fi
        sleep 1
    done
    # Non-fatal on timeout — warn but don't abort postStart (set -e + exit 1 would
    # mark the whole lifecycle step as failed).
    curl -sf "$SUP_HEALTH" >/dev/null 2>&1 || \
        echo "Warning: supervisord did not come up in time — check $SUP_DATA/supervisord.log" >&2

    # --- Block until the managed services actually accept connections ---
    # supervisord's own RUNNING state is NOT a readiness signal here: each program
    # IS its wrapper script, and supervisord flips a program to RUNNING once the
    # wrapper has merely stayed alive past startsecs. But the open-webui wrapper
    # spends up to ~60s sleeping in its dep-wait loop BEFORE it exec's open-webui,
    # and open-webui then runs DB migrations / fetches an embedding model before it
    # binds :7000 — so "RUNNING" reports long before the HTTP server is up. Probe
    # the real health endpoints instead, so the URLs printed below actually work.
    #
    # Probe only the forwarded, user-facing services (LiteLLM, Open WebUI). Open
    # WebUI's readiness implies Open Terminal + SearXNG are already up, because its
    # wrapper waits on their health before exec'ing — no need to probe those two
    # directly. Guard each on its binary being installed (same checks the wrappers
    # use), so an uninstalled service doesn't burn the timeout. Non-fatal on timeout
    # (warn, don't exit 1 — which under set -e would fail postStart); supervisord
    # owns the services regardless, so they keep coming up in the background.
    probe() {  # name  health-url  max-seconds
        printf '  waiting for %s ...' "$1"
        for _ in $(seq 1 "$3"); do
            if curl -sf "$2" >/dev/null 2>&1; then echo " ready"; return 0; fi
            sleep 1
        done
        echo " timed out after ${3}s"
        return 1
    }
    echo "Waiting for services to accept connections..."
    # LiteLLM's health path is /health/liveliness (its actual spelling — /liveness 404s).
    [ -x "$LITELLM_BIN" ] && { probe "LiteLLM proxy" "http://127.0.0.1:7010/health/liveliness" 60 || \
        echo "Warning: LiteLLM proxy not ready — check $DATA/litellm/litellm.log" >&2; }
    # open-webui needs the most headroom: ~60s dep-wait + migrations + first-boot
    # embedding-model download before it binds :7000.
    [ -x "$OPENWEBUI_BIN" ] && { probe "Open WebUI" "http://127.0.0.1:7000/health" 180 || \
        echo "Warning: Open WebUI not ready — check $DATA/open-webui/open-webui.log" >&2; }

    # --- Service port summary ---
    # These are the IN-CONTAINER ports each service binds; VS Code forwards them to
    # the same host port, so the localhost URLs below work in the common single-
    # container case. CAVEAT: when a port is already taken on the host (e.g. a second
    # container running these same services), VS Code silently maps to the next free
    # one (7001, 7011, …). That resolved host→container mapping lives in the VS Code
    # client and isn't exposed to container processes, so we can't print it here —
    # hence the pointer to the PORTS panel for the real address in that case. Only
    # forwarded services are listed; Open Terminal (7030) and SearXNG (7020) are
    # loopback-only internal dependencies of Open WebUI, not forwarded.
    echo ""
    echo "Services:"
    echo "  Supervisor     http://localhost:6090"
    [ -x "$OPENWEBUI_BIN" ] && echo "  Open WebUI     http://localhost:7000"
    [ -x "$LITELLM_BIN" ]   && echo "  LiteLLM proxy  http://localhost:7010"
    echo "(If multiple containers run these services, check the VS Code PORTS panel for the actual forwarded ports.)"

# The chat stack IS installed (supervisord present => --chat was passed) but its LiteLLM
# config is missing, so the gate above skipped everything. Say so: this is the one gate
# failure that means something is broken rather than intentionally absent, and it is
# otherwise invisible — the script would exit 0 with no output, exactly like a healthy
# chat-less container. Warn (>&2) instead of exiting non-zero: poststart.sh swallows the
# status with `|| true`, and a non-zero exit under its `set -e` would fail the whole
# postStart step for what is a recoverable, single-service problem.
elif [ -x "$SUPERVISORD_BIN" ]; then
    echo "Warning: chat stack is installed but $CONFIG is missing — not starting it." >&2
    echo "  .devcontainer/features/chat/postcreate.sh generates that file from the" >&2
    echo "  profiles named in .devcontainer/devcontainer.env (CONFIGURED_PROFILES);" >&2
    echo "  a profile only contributes if .devcontainer/profiles/<name>/litellm.json" >&2
    echo "  exists. Check that it ran to completion, then rebuild the container." >&2
fi
# No else for the remaining case: supervisord absent means the ./features/chat bundle was
# never installed (a spawn without --chat), which is a deliberate, silent no-op.
