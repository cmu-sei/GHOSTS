#!/bin/bash
## Configure Herdr terminal workspaces
#
# Everything Herdr needs at create time lives here, so a devcontainer.json that lists
# "./features/herdr" gets a working workspace manager with NOTHING added to the project's
# own postcreate.sh. Declared as this feature's postCreateCommand in
# devcontainer-feature.json; the workspace folder is the cwd.
#
# Herdr owns its server lifecycle: start it interactively from a terminal. No supervisord
# entry and no forwarded port, so there is no poststart half to this feature.
set -euo pipefail

# --- Persistent data volume ---
# A single Docker volume at ~/.data/ stores all agent data and shell history to survive
# container rebuilds. The volume mounts root-owned on first create; the chown is
# idempotent and every feature that touches ~/.data does it, since any of them may run
# first.
DATA="$HOME/.data"
sudo chown "$(id -u):$(id -g)" "$DATA"
mkdir -p "$DATA/herdr" "$HOME/.config"

# --- Symlink the config directory into the persistent volume ---
# Herdr keeps its state under ~/.config/herdr (an XDG dir, not a ~/.herdr), so that is
# what gets redirected onto the volume — saved layouts, conversations and worktrees all
# live there. If the volume directory is empty and the home directory has content from
# the feature installer, seed the volume with those files before symlinking.
if [ -d "$HOME/.config/herdr" ] && [ ! -L "$HOME/.config/herdr" ]; then
    if [ -z "$(ls -A "$DATA/herdr" 2>/dev/null)" ]; then
        cp -a "$HOME/.config/herdr/." "$DATA/herdr/"
    fi
    rm -rf "$HOME/.config/herdr"
fi
ln -sfn "$DATA/herdr" "$HOME/.config/herdr"

# --- Seed Herdr's config (first create only) ---
# Login shells load ~/.profile as well as interactive shell configuration, keeping
# ~/.local/bin and the container's language tools available in panes. onboarding = false
# makes the first create fully automated. Seeded only once: Herdr's own settings writes
# and any later user edits belong to the user, and this file is on the volume.
if [ ! -f "$HOME/.config/herdr/config.toml" ]; then
    cat > "$HOME/.config/herdr/config.toml" <<'EOF'
onboarding = false

[terminal]
shell_mode = "login"

[worktrees]
directory = "~/.data/herdr/worktrees"
EOF
fi

# Read the persisted update-channel preference; never force a server handoff.
echo "Updating Herdr..."
herdr update || true

# --- Official agent integrations ---
# The integrations preserve unrelated agent settings, and Codex's enables user-level
# hooks without touching provider/model defaults (those stay generated exclusively in
# /etc/codex/config.toml). OpenCode's plugin directory is not persisted, so its
# integration has to be reinstalled on every create.
#
# Gated on each agent actually being on PATH, which is what this feature can know: the
# agents are separate features now, so a devcontainer.json may list any subset of them
# (or none) alongside this one. No prompts and no config flag — installing Herdr IS the
# opt-in, exactly as before; what changed is that an absent agent no longer gets a stub
# config directory created for an integration it will never use.
#
# Each agent's own feature generates its config; feature postCreateCommands run in the
# CLI's computed install order, which is not the devcontainer.json listing order, so this
# may land either side of a given agent's config generation. Both are idempotent and
# write different files, and re-running `herdr integration install <agent>` is safe, so
# order does not change the outcome.
for agent in claude codex opencode pi grok; do
    command -v "$agent" &>/dev/null || continue
    case "$agent" in
        claude)   AGENT_DIR="${CLAUDE_CONFIG_DIR:-$HOME/.claude}" ;;
        codex)    AGENT_DIR="${CODEX_HOME:-$HOME/.codex}" ;;
        opencode) AGENT_DIR="$HOME/.config/opencode" ;;
        pi)       AGENT_DIR="${PI_CODING_AGENT_DIR:-$HOME/.pi/agent}" ;;
        grok)     AGENT_DIR="${GROK_HOME:-$HOME/.grok}" ;;
    esac
    mkdir -p "$AGENT_DIR"
    echo "Installing Herdr integration for $agent..."
    herdr integration install "$agent" || true
done

# --- Herdr skill ---
# Same user-scope layout as the find-skills install in scripts/postcreate.sh: -g for
# user scope and --copy to avoid dangling relative symlinks under the volume-backed agent
# homes. The -a flags must be repeated (a comma-separated list is parsed as one literal
# name and fatal-errors); Codex, OpenCode and Grok read ~/.agents/skills directly.
echo "Installing Herdr skill..."
npx -y skills@latest add herdrdev/herdr -s herdr \
    -g --copy -a claude-code -a codex -a opencode -a pi -y || true
