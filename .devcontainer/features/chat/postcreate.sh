#!/bin/bash
## Configure the browser-chat stack (LiteLLM proxy, Open WebUI, Open Terminal, SearXNG)
#
# Everything the stack needs at create time lives here, so a devcontainer.json that
# lists "./features/chat" gets a working chat stack with NOTHING added to the
# project's own postcreate.sh. Declared as this feature's postCreateCommand in
# devcontainer-feature.json; the workspace folder is the cwd.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# .devcontainer/, two levels up from .devcontainer/features/chat/.
DEVCONTAINER_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

# --- Persistent data volume ---
# The stack's runtime state (proxy logs, Open WebUI's DB + HF cache, the generated
# secrets, supervisord's pidfile/log) lives on the ~/.data volume so it survives
# rebuilds. The volume mounts root-owned on first create; the chown is idempotent
# and every feature that touches ~/.data does it, since any of them may run first.
DATA="$HOME/.data"
sudo chown "$(id -u):$(id -g)" "$DATA"
mkdir -p "$DATA/litellm" "$DATA/open-webui" "$DATA/open-terminal" \
         "$DATA/searxng" "$DATA/supervisor"

# --- LiteLLM proxy config ---
# Aggregate every configured profile's model_list into a single OpenAI-compatible
# proxy config. LiteLLM's model_list is an array, so this is a concatenation — the
# jq -s form handles one or many profiles alike. Regenerated on every create rather
# than persisted, so a model added to a profile in git reaches an existing container
# on its next rebuild. Written as JSON, which is valid YAML and is what LiteLLM
# parses the file with (yaml.safe_load).
#
# This file is the gate for the whole stack: supervisord/start.sh refuses to bring
# the services up without it (no profile models => no proxy => nothing for the chat
# UI to talk to).
DEVENV="$DEVCONTAINER_DIR/devcontainer.env"
PROFILES_DIR="$DEVCONTAINER_DIR/profiles"

if [ -f "$DEVENV" ]; then
    PROFILES_LINE=$(grep '^CONFIGURED_PROFILES=' "$DEVENV" 2>/dev/null || true)
    if [ -n "$PROFILES_LINE" ]; then
        CSV="${PROFILES_LINE#CONFIGURED_PROFILES=}"
        # Strip CR before splitting — load-bearing on Windows hosts. setup.ps1 writes
        # devcontainer.env with PowerShell's Set-Content, which uses CRLF line endings,
        # and we read the file DIRECTLY here (not via the container env). `read` splits
        # on IFS and drops the trailing newline but never the \r, so without this the
        # LAST profile in the CSV keeps a trailing CR and every path built from it
        # misses: "$PROFILES_DIR/aws\r/litellm.json" does not exist, so that profile
        # contributes nothing and — when it is the ONLY profile — the LiteLLM config is
        # never generated at all, silently killing the whole chat stack (start.sh gates
        # on that file). Symptom was Windows-only and profile-count-dependent, which is
        # why it hid for so long: with 2+ profiles the earlier ones still matched.
        # Docker's --env-file trims the CR on its own, so the agents' env vars are fine
        # and only this file-reading path is affected. Also covers a hand-edited env
        # file saved with CRLF by any editor.
        CSV="${CSV//$'\r'/}"
        IFS=',' read -ra CONFIGURED <<< "$CSV"

        LITELLM_FILES=()
        for profile in "${CONFIGURED[@]}"; do
            if [ -f "$PROFILES_DIR/$profile/litellm.json" ]; then
                LITELLM_FILES+=("$profile")
            fi
        done

        if [ ${#LITELLM_FILES[@]} -ge 1 ]; then
            mkdir -p "$HOME/.config/litellm"
            LITELLM_PATHS=()
            for p in "${LITELLM_FILES[@]}"; do LITELLM_PATHS+=("$PROFILES_DIR/$p/litellm.json"); done
            jq -s '{ model_list: [.[].model_list // [] | .[]] }' \
                "${LITELLM_PATHS[@]}" > "$HOME/.config/litellm/config.yaml"
        fi
    fi
fi

# --- `start_chat_stack` command ---
# Now that autostart is opt-in (CHAT_AUTOSTART=1 — see this feature's poststart.sh), this
# is the normal way to bring the stack up, so it gets a name on PATH instead of a path to
# remember. ~/.local/bin is the remote user's bin dir, already on PATH and where the uv
# tool entry points and the agent CLIs land.
#
# A SYMLINK, not a copy: start.sh is a bind-mounted repo file, so edits to it take effect
# without a rebuild, and there is no second copy to go stale. SCRIPT_DIR is absolute, so
# the link resolves from any cwd. `-f` to replace a link left by an earlier layout (the
# script used to live at .devcontainer/supervisord/), `-n` so an existing link is replaced
# rather than dereferenced.
#
# Being a symlink, this DOES depend on start.sh's exec bit — it is committed 755, but a
# Windows-host bind mount may not preserve it, in which case the command reports
# "Permission denied" and `bash .devcontainer/features/chat/supervisord/start.sh` still
# works. (That exec-bit-proof form is why the lifecycle hooks all invoke scripts via
# `bash <path>`.)
mkdir -p "$HOME/.local/bin"
ln -sfn "$SCRIPT_DIR/supervisord/start.sh" "$HOME/.local/bin/start_chat_stack"

# --- supervisorctl alias ---
# supervisorctl defaults to 127.0.0.1:9001, but this stack binds the control endpoint
# to :6090 via supervisord.conf — point the alias at the config that ships with this
# feature so plain `supervisorctl` connects.
#
# Rewritten rather than only-appended-when-absent: an rc file may already carry an
# alias from an earlier layout (the config used to live at .devcontainer/supervisord/),
# and a stale -c path makes bare `supervisorctl` fail with a config error. Deleting the
# line first keeps this idempotent AND self-healing across moves.
SUPERVISORD_CONF="$SCRIPT_DIR/supervisord/supervisord.conf"
for rc in "$HOME/.bashrc" "$HOME/.zshrc"; do
    [ -f "$rc" ] || continue
    sed -i '/^alias supervisorctl=/d' "$rc"
    echo "alias supervisorctl='supervisorctl -c $SUPERVISORD_CONF'" >> "$rc"
done
