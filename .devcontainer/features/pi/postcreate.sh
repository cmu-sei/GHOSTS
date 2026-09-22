#!/bin/bash
## Configure the Pi Coding Agent
#
# Everything Pi needs at create time lives here, so a devcontainer.json that lists
# "./features/pi" gets a working agent with NOTHING added to the project's own
# postcreate.sh. Declared as this feature's postCreateCommand in
# devcontainer-feature.json; the workspace folder is the cwd.
set -euo pipefail

TARGET=pi
# .devcontainer/, two levels up from .devcontainer/features/pi/.
DEVCONTAINER_DIR="$(cd "$(dirname "$0")/../.." && pwd)"

# --- Persistent data volume ---
# A single Docker volume at ~/.data/ stores all agent data and shell history
# to survive container rebuilds. The volume mounts root-owned on first create; the
# chown is idempotent and every feature that touches ~/.data does it, since any of
# them may run first.
DATA="$HOME/.data"
sudo chown "$(id -u):$(id -g)" "$DATA"
mkdir -p "$DATA/$TARGET"

# --- Symlink agent data directories into the persistent volume ---
# If the volume directory is empty and the home directory has content from the feature
# installer, seed the volume with those files before symlinking.
vol_dir="$DATA/$TARGET"
home_dir="$HOME/.$TARGET"
if [ -d "$home_dir" ] && [ ! -L "$home_dir" ]; then
    if [ -z "$(ls -A "$vol_dir" 2>/dev/null)" ]; then
        cp -a "$home_dir/." "$vol_dir/"
    fi
    rm -rf "$home_dir"
fi
ln -sfn "$vol_dir" "$home_dir"

# --- Pi config from the configured profiles ---
# Regenerated on every create rather than persisted, so a provider or model added to a
# profile in git reaches an existing container on its next rebuild. ~/.pi is the volume
# symlink made above, so the files below land on the volume.
DEVENV="$DEVCONTAINER_DIR/devcontainer.env"
PROFILES_DIR="$DEVCONTAINER_DIR/profiles"

if [ -f "$DEVENV" ]; then
    PROFILES_LINE=$(grep '^CONFIGURED_PROFILES=' "$DEVENV" 2>/dev/null || true)
    if [ -n "$PROFILES_LINE" ]; then
        CSV="${PROFILES_LINE#CONFIGURED_PROFILES=}"
        # Strip CR before splitting — load-bearing on Windows hosts, where setup.ps1
        # writes devcontainer.env with CRLF and we read the file DIRECTLY (Docker's
        # --env-file trims for us, this path does not). Without it the LAST profile in
        # the CSV keeps a trailing \r and every path built from it misses, silently.
        # Full story in features/chat/postcreate.sh, where the bug first surfaced.
        CSV="${CSV//$'\r'/}"
        IFS=',' read -ra CONFIGURED <<< "$CSV"

        PI_FILES=()
        for profile in "${CONFIGURED[@]}"; do
            if [ -f "$PROFILES_DIR/$profile/models.json" ]; then
                PI_FILES+=("$profile")
            fi
        done

        if [ ${#PI_FILES[@]} -eq 1 ]; then
            mkdir -p "$HOME/.pi/agent"
            cp "$PROFILES_DIR/${PI_FILES[0]}/models.json" "$HOME/.pi/agent/models.json"
        elif [ ${#PI_FILES[@]} -ge 2 ]; then
            mkdir -p "$HOME/.pi/agent"
            PI_PATHS=()
            for p in "${PI_FILES[@]}"; do PI_PATHS+=("$PROFILES_DIR/$p/models.json"); done
            jq -s '{ providers: (reduce .[].providers as $p ({}; . * $p)) }' \
                "${PI_PATHS[@]}" > "$HOME/.pi/agent/models.json"
        fi

        # Set enabledModels in Pi settings when custom models override a
        # built-in provider (e.g. amazon-bedrock), to hide the built-in
        # models that won't work. Clear it otherwise to restore defaults.
        PI_SETTINGS="$HOME/.pi/agent/settings.json"
        PI_MODELS="$HOME/.pi/agent/models.json"
        HAS_BUILTIN_OVERRIDE=false
        if [ -f "$PI_MODELS" ]; then
            if jq -e '.providers["amazon-bedrock"].models' "$PI_MODELS" &>/dev/null; then
                HAS_BUILTIN_OVERRIDE=true
            fi
        fi
        if [ "$HAS_BUILTIN_OVERRIDE" = true ]; then
            ENABLED_JSON=$(jq -c '[.providers | to_entries[] | .key as $p |
                .value.models[]? | "\($p)/\(.id)"]' "$PI_MODELS")
            if [ -f "$PI_SETTINGS" ]; then
                jq --argjson em "$ENABLED_JSON" '.enabledModels = $em' \
                    "$PI_SETTINGS" > "$PI_SETTINGS.tmp"
                mv "$PI_SETTINGS.tmp" "$PI_SETTINGS"
            else
                jq -n --argjson em "$ENABLED_JSON" '{enabledModels: $em}' \
                    > "$PI_SETTINGS"
            fi
        elif [ -f "$PI_SETTINGS" ]; then
            jq 'del(.enabledModels)' "$PI_SETTINGS" > "$PI_SETTINGS.tmp"
            mv "$PI_SETTINGS.tmp" "$PI_SETTINGS"
        fi
    fi
fi

# Update to the latest version (runs on every create, bypassing the image cache)
echo "Updating Pi Coding Agent..."
pi update self || true
