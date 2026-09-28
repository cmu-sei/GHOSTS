#!/bin/bash
## Configure Claude Code
#
# Everything Claude Code needs at create time lives here, so a devcontainer.json that
# lists "./features/claude" gets a working agent with NOTHING added to the project's own
# postcreate.sh. Declared as this feature's postCreateCommand in
# devcontainer-feature.json; the workspace folder is the cwd.
set -euo pipefail

TARGET=claude
# ABSOLUTE, not the bare `dirname "$0"` the other features use: the statusline symlink
# below points INTO this directory from ~/.local/bin, and a relative target resolves
# against the LINK's directory, not the cwd — `.devcontainer/features/claude/…` would
# dangle at `~/.local/bin/.devcontainer/…`. The feature's postCreateCommand is invoked
# with a workspace-relative path, so $0 is relative and this cd is what fixes it.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# .devcontainer/, two levels up from .devcontainer/features/claude/.
DEVCONTAINER_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

# --- Persistent data volume ---
# A single Docker volume at ~/.data/ stores all agent data and shell history
# to survive container rebuilds.
DATA="$HOME/.data"
sudo chown "$(id -u):$(id -g)" "$DATA"
mkdir -p "$DATA/$TARGET"

# --- Symlink agent data directories into the persistent volume ---
vol_dir="$DATA/$TARGET"
home_dir="$HOME/.$TARGET"
if [ -d "$home_dir" ] && [ ! -L "$home_dir" ]; then
    if [ -z "$(ls -A "$vol_dir" 2>/dev/null)" ]; then
        cp -a "$home_dir/." "$vol_dir/"
    fi
    rm -rf "$home_dir"
fi
ln -sfn "$vol_dir" "$home_dir"

# --- Persistent claude.json (settings) ---
if [ ! -f "$HOME/.claude/claude.json" ]; then
    echo '{}' > "$HOME/.claude/claude.json"
fi
ln -sf "$HOME/.claude/claude.json" "$HOME/.claude.json"

# Put statusline script on Path
mkdir -p $HOME/.local/bin
ln -sfn $SCRIPT_DIR/statusline-command.sh $HOME/.local/bin/statusline

# --- User-level Claude Code settings ---
# EVERYTHING this feature generates goes in the user-level settings file, never the
# project's .claude/settings.json: configuring the agent is the container's job,
# writing into the project's working tree is not. (An earlier version seeded a project
# file, which left an uncommitted change behind on every first create — and one key
# below cannot live there at all; see auto mode.) $HOME/.claude is symlinked into the
# persistent volume above, so all of it survives rebuilds.
#
# The `echo '{}'` BOOTSTRAPS the file for the two jq passes below and the profile merge
# further down — each is gated on it existing and does nothing without it.
CLAUDE_SETTINGS="$HOME/.claude/settings.json"
[ -f "$CLAUDE_SETTINGS" ] || echo '{}' > "$CLAUDE_SETTINGS"

# --- Defaults: commit/PR attribution and the statusline ---
# Same shape as every other agent's tracked config fragment. The merge order puts the
# EXISTING file second, so it wins at every key: this seeds, it never overwrites, and a
# developer's own statusLine or attribution survives later creates. The statusLine
# command is bare `statusline` — the PATH symlink made above — so moving or renaming the
# script never invalidates a settings file already written to the volume.
if [ -f "$SCRIPT_DIR/default-settings.json" ]; then
    jq -s '.[0] * .[1]' "$SCRIPT_DIR/default-settings.json" "$CLAUDE_SETTINGS" \
        > "$CLAUDE_SETTINGS.tmp" && mv "$CLAUDE_SETTINGS.tmp" "$CLAUDE_SETTINGS"
fi

# --- Default Claude Code to auto permission mode ---
# Auto mode delegates each permission decision to a safety classifier instead of
# prompting, which suits a container where the agents are already sandboxed.
# This one is user scope out of necessity, not just convention: "auto" is
# source-restricted to managed policy, user settings, and the CLI flag, so the same key
# in a project's .claude/settings.json (or settings.local.json) is IGNORED as
# repo-controllable — putting it there looks correct but silently does nothing.
# Seeded only when no defaultMode is set yet: a developer who picks a different mode
# (/config, or editing the file) keeps it across later creates.
if ! jq -e '.permissions.defaultMode' "$CLAUDE_SETTINGS" &>/dev/null; then
    jq '.permissions = ((.permissions // {}) + { defaultMode: "auto" })' \
        "$CLAUDE_SETTINGS" > "$CLAUDE_SETTINGS.tmp" \
        && mv "$CLAUDE_SETTINGS.tmp" "$CLAUDE_SETTINGS"
fi

# --- Claude Code config from the configured profiles (pinned models) ---
# Claude Code's pinned-model vars are env-only — none of them has a dedicated
# settings key — so they are merged into the USER settings file's `env` block
# rather than being frozen into devcontainer.env at first setup (which is
# generated once and gitignored, so a model bump there never reached an existing
# container). Claude Code applies `env` to its own model resolution, not just to
# spawned subprocesses.
#
# claude.json is the SINGLE source for these vars: they are no longer in
# profile.env, so a correctly-configured devcontainer.env does not set them at
# all. A container created before that change still carries the old ANTHROPIC_*
# lines in its env file; harmless (a settings `env` entry replaces the inherited
# value, and the fragments cover every key those lines could set), so nothing here
# rewrites a developer's env file.
#
# User scope (not project) on purpose: it sits with permissions.defaultMode above
# and the picker reset below, and $HOME/.claude is symlinked into the volume.
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

        CLAUDE_FILES=()
        for profile in "${CONFIGURED[@]}"; do
            if [ -f "$PROFILES_DIR/$profile/claude.json" ]; then
                CLAUDE_FILES+=("$profile")
            fi
        done

        if [ ${#CLAUDE_FILES[@]} -ge 1 ] && [ -f "$CLAUDE_SETTINGS" ]; then
            CLAUDE_PATHS=()
            for p in "${CLAUDE_FILES[@]}"; do CLAUDE_PATHS+=("$PROFILES_DIR/$p/claude.json"); done
            # Merge each configured profile's env map (last wins), then fold it
            # into the settings file. Additive: only the keys the fragments
            # define are written, so a developer's own `env` entries survive.
            # Trade-off of that: a pin REMOVED from claude.json (or one left by a
            # profile since dropped) lingers in an existing container's
            # settings.json until deleted by hand. No key-clearing pass here —
            # contrast the codex feature's rm -f and Pi's del(.enabledModels).
            MERGED_ENV=$(jq -s '{ env: (reduce (.[].env // {}) as $e ({}; . * $e)) }' \
                "${CLAUDE_PATHS[@]}")
            jq --argjson merged "$MERGED_ENV" \
                '.env = ((.env // {}) + $merged.env)' \
                "$CLAUDE_SETTINGS" > "$CLAUDE_SETTINGS.tmp"
            mv "$CLAUDE_SETTINGS.tmp" "$CLAUDE_SETTINGS"
        fi
    fi
fi

# --- Claude Code: stock /model picker ---
# availableModels used to be pinned to ["opus","sonnet","haiku"] to hide Fable
# while it had no Bedrock deployment. Both profiles pin a Fable id now, so the
# allowlist has nothing to hide — and it was a trap anyway: it filters
# everything, including ANTHROPIC_CUSTOM_MODEL_OPTION unless that exact id is
# also listed. Cleared rather than left alone because the key is one WE wrote
# into the persisted user settings and would otherwise stay stale in existing
# containers (same reset intent as the codex feature's rm -f and Pi's
# del(.enabledModels)). Do not re-add the allowlist.
if [ -f "$CLAUDE_SETTINGS" ]; then
    jq 'del(.availableModels)' "$CLAUDE_SETTINGS" > "$CLAUDE_SETTINGS.tmp"
    mv "$CLAUDE_SETTINGS.tmp" "$CLAUDE_SETTINGS"
fi

# Update agents to latest version (runs on every rebuild, bypassing image cache)
echo "Updating Claude Code..."
claude update || true
