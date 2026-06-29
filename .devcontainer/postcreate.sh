#!/bin/bash
set -euo pipefail

# Show uncommitted Git changes in Zsh prompt
if git rev-parse --is-inside-work-tree &>/dev/null; then
    git config devcontainers-theme.show-dirty 1
fi

# Update agents to latest version (runs on every rebuild, bypassing image cache)
echo "Updating Claude Code..."
claude update || true

echo "Updating OpenCode..."
opencode upgrade || true

echo "Updating Pi Coding Agent..."
pi update self || true

echo "Updating Codex CLI..."
# Reuse the feature's installer (single source of truth; also the upgrade path)
bash "$(dirname "$0")/features/codex/install.sh" || true

# Install Pure prompt
mkdir -p "$HOME/.zsh"
git clone https://github.com/sindresorhus/pure.git "$HOME/.zsh/pure"
sed -i "s|^ZSH_THEME=.*|ZSH_THEME=\"\"\n\nFPATH=\$HOME/.zsh/pure:\$FPATH|" $HOME/.zshrc

cat >>$HOME/.zshrc <<'EOF'

# Pure prompt
autoload -U promptinit; promptinit
prompt pure
EOF

# --- Persistent data volume ---
# A single Docker volume at ~/.data/ stores all agent data and shell history
# to survive container rebuilds.
DATA="$HOME/.data"
sudo chown "$(id -u):$(id -g)" "$DATA"
mkdir -p "$DATA/claude" "$DATA/opencode" "$DATA/opencode-state" "$DATA/pi" "$DATA/codex" "$DATA/shell-history"

# --- Symlink agent data directories into the persistent volume ---
# If the volume directory is empty and the home directory has content from the
# feature installer, seed the volume with those files before symlinking.
for name in claude pi codex; do
    vol_dir="$DATA/$name"
    home_dir="$HOME/.$name"
    if [ -d "$home_dir" ] && [ ! -L "$home_dir" ]; then
        if [ -z "$(ls -A "$vol_dir" 2>/dev/null)" ]; then
            cp -a "$home_dir/." "$vol_dir/"
        fi
        rm -rf "$home_dir"
    fi
    ln -sfn "$vol_dir" "$home_dir"
done

# --- OpenCode data directory ---
# Symlink ~/.local/share/opencode to persistent volume
mkdir -p "$DATA/opencode"
if [ -d "$HOME/.local/share/opencode" ] && [ ! -L "$HOME/.local/share/opencode" ]; then
    if [ -z "$(ls -A "$DATA/opencode" 2>/dev/null)" ]; then
        cp -a "$HOME/.local/share/opencode/." "$DATA/opencode/"
    fi
    rm -rf "$HOME/.local/share/opencode"
fi
mkdir -p "$HOME/.local/share"
ln -sfn "$DATA/opencode" "$HOME/.local/share/opencode"

# --- OpenCode state directory ---
# Persist ~/.local/state/opencode (model selection, prompt history) to the
# volume, mirroring the share-dir handling above. Config (~/.config/opencode)
# is intentionally NOT persisted — postcreate.sh regenerates it from profiles.
mkdir -p "$DATA/opencode-state"
if [ -d "$HOME/.local/state/opencode" ] && [ ! -L "$HOME/.local/state/opencode" ]; then
    if [ -z "$(ls -A "$DATA/opencode-state" 2>/dev/null)" ]; then
        cp -a "$HOME/.local/state/opencode/." "$DATA/opencode-state/"
    fi
    rm -rf "$HOME/.local/state/opencode"
fi
mkdir -p "$HOME/.local/state"
ln -sfn "$DATA/opencode-state" "$HOME/.local/state/opencode"

# --- Generate agent configs from configured profiles ---
WORKSPACE_DIR="$(pwd)"
DEVENV="$WORKSPACE_DIR/.devcontainer/devcontainer.env"
PROFILES_DIR="$WORKSPACE_DIR/.devcontainer/profiles"

if [ -f "$DEVENV" ]; then
    PROFILES_LINE=$(grep '^CONFIGURED_PROFILES=' "$DEVENV" 2>/dev/null || true)
    if [ -n "$PROFILES_LINE" ]; then
        CSV="${PROFILES_LINE#CONFIGURED_PROFILES=}"
        IFS=',' read -ra CONFIGURED <<< "$CSV"

        # --- OpenCode config ---
        OC_FILES=()
        for profile in "${CONFIGURED[@]}"; do
            if [ -f "$PROFILES_DIR/$profile/opencode.json" ]; then
                OC_FILES+=("$profile")
            fi
        done

        if [ ${#OC_FILES[@]} -eq 1 ]; then
            mkdir -p "$HOME/.config/opencode"
            cp "$PROFILES_DIR/${OC_FILES[0]}/opencode.json" "$HOME/.config/opencode/opencode.json"
        elif [ ${#OC_FILES[@]} -ge 2 ]; then
            mkdir -p "$HOME/.config/opencode"
            OC_PATHS=()
            for p in "${OC_FILES[@]}"; do OC_PATHS+=("$PROFILES_DIR/$p/opencode.json"); done
            # Merge providers and disabled_providers; model is omitted so
            # OpenCode uses whatever the user last selected.
            jq -s '{
                "$schema": "https://opencode.ai/config.json",
                disabled_providers: ([.[].disabled_providers // [] | .[]] | unique | if length > 0 then . else null end),
                provider: (reduce (.[].provider // {}) as $p ({}; . * $p))
            } | with_entries(select(.value != null))' "${OC_PATHS[@]}" > "$HOME/.config/opencode/opencode.json"
        fi

        # --- Pi Coding Agent config ---
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

        # --- Codex CLI config ---
        # Codex reads ~/.codex/config.toml. Only the aws profile ships one: it
        # targets OpenAI GPT-5.5 via Codex's built-in amazon-bedrock provider
        # (Mantle/Responses, SigV4). awsgov has none — GPT-5.4 IS live on
        # GovCloud Bedrock Mantle, but Codex hardcodes a Mantle region allowlist
        # that (as of 0.137.0) excludes us-gov-west-1, so it fatal-errors there.
        # Revisit when openai/codex#26269 (configurable Mantle regions) merges.
        # SEI profiles (opal, etc) aren't Bedrock. No merging: copy the first
        # configured profile that provides a config.toml.
        CODEX_FILES=()
        for profile in "${CONFIGURED[@]}"; do
            if [ -f "$PROFILES_DIR/$profile/config.toml" ]; then
                CODEX_FILES+=("$profile")
            fi
        done

        if [ ${#CODEX_FILES[@]} -ge 1 ]; then
            mkdir -p "$HOME/.codex"
            cp "$PROFILES_DIR/${CODEX_FILES[0]}/config.toml" "$HOME/.codex/config.toml"
        fi

        # --- Claude Code: restrict model picker for profiles without Opus ---
        # GovCloud does not have Opus, so hide it from the /model picker. It also
        # only ships Sonnet 4.5 (Claude 3 Haiku is legacy and unreliable on
        # GovCloud Bedrock), so the picker offers Sonnet alone.
        CLAUDE_SETTINGS="$HOME/.claude/settings.json"
        HAS_OPUS=true
        for profile in "${CONFIGURED[@]}"; do
            if [ "$profile" = "awsgov" ]; then
                HAS_OPUS=false
                break
            fi
        done
        if [ "$HAS_OPUS" = false ]; then
            if [ -f "$CLAUDE_SETTINGS" ]; then
                jq '.availableModels = ["sonnet"]' \
                    "$CLAUDE_SETTINGS" > "$CLAUDE_SETTINGS.tmp"
                mv "$CLAUDE_SETTINGS.tmp" "$CLAUDE_SETTINGS"
            else
                jq -n '{availableModels: ["sonnet"]}' \
                    > "$CLAUDE_SETTINGS"
            fi
        elif [ -f "$CLAUDE_SETTINGS" ]; then
            jq 'del(.availableModels)' "$CLAUDE_SETTINGS" > "$CLAUDE_SETTINGS.tmp"
            mv "$CLAUDE_SETTINGS.tmp" "$CLAUDE_SETTINGS"
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

# --- Persistent shell history ---
touch "$DATA/shell-history/.bash_history" "$DATA/shell-history/.zsh_history"

# Redirect HISTFILE in .bashrc (idempotent)
if ! grep -q 'HISTFILE=.*\.data' "$HOME/.bashrc" 2>/dev/null; then
    echo 'export HISTFILE="$HOME/.data/shell-history/.bash_history"' >> "$HOME/.bashrc"
fi

# Redirect HISTFILE in .zshrc (idempotent)
if [ -f "$HOME/.zshrc" ] && ! grep -q 'HISTFILE=.*\.data' "$HOME/.zshrc" 2>/dev/null; then
    echo 'export HISTFILE="$HOME/.data/shell-history/.zsh_history"' >> "$HOME/.zshrc"
fi

# --- Persistent claude.json (settings) ---
# Symlink ~/.claude.json into the persistent claude volume so
# settings survive container rebuilds.
if [ ! -f "$HOME/.claude/claude.json" ]; then
    echo '{}' > "$HOME/.claude/claude.json"
fi
ln -sf "$HOME/.claude/claude.json" "$HOME/.claude.json"

# --- tmux configuration ---
cat > "$HOME/.tmux.conf" <<'EOF'
# UTF-8 support (required for Pure prompt symbols)
set -g default-terminal "tmux-256color"

# Extended keys (CSI u / modifyOtherKeys) so pi coding agent works correctly
set -s extended-keys on
set -as terminal-features ',xterm-256color:extkeys'

# Increase scrollback buffer
set -g history-limit 50000

# Enable mouse support
set -g mouse on

# Use Zsh as default shell inside tmux
set -g default-shell /bin/zsh
EOF

# Show git dirty status in zsh prompt
git config devcontainers-theme.show-dirty 1

sudo chown -R $(whoami): /home/vscode/.microsoft

echo ""
echo "========================================="
echo "Installing Microsoft Aspire..."
echo "========================================="
dotnet tool install -g Aspire.Cli

# Install Angular CLI 21 and setup Angular project
echo ""
echo "========================================="
echo "Installing Angular CLI 21..."
echo "========================================="
npm install -g @angular/cli@latest

echo ""
echo "Installing Angular dependencies..."
cd src/Ghosts.Frontend
npm install
cd ../..

# Install Python packages for AI integrations
echo ""
echo "========================================="
echo "Installing Python packages..."
echo "========================================="
pip install --no-cache-dir --break-system-packages openai anthropic mkdocs-material

echo ""
echo "========================================="
echo "Setup completed successfully!"
echo "========================================="

