#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DATA="$HOME/.data"
sudo chown "$(id -u):$(id -g)" "$DATA"

# --- Persistent shell history ---
mkdir -p $DATA/shell-history
touch "$DATA/shell-history/.bash_history" "$DATA/shell-history/.zsh_history"

# Redirect HISTFILE in .bashrc (idempotent)
if ! grep -q 'HISTFILE=.*\.data' "$HOME/.bashrc" 2>/dev/null; then
    echo 'export HISTFILE="$HOME/.data/shell-history/.bash_history"' >> "$HOME/.bashrc"
fi

# Redirect HISTFILE in .zshrc (idempotent)
if [ -f "$HOME/.zshrc" ] && ! grep -q 'HISTFILE=.*\.data' "$HOME/.zshrc" 2>/dev/null; then
    echo 'export HISTFILE="$HOME/.data/shell-history/.zsh_history"' >> "$HOME/.zshrc"
fi

# Install find-skills
echo "Installing find-skills skill..."
npx -y skills@latest add vercel-labs/skills -s find-skills \
    -g --copy -a claude-code -a codex -a opencode -a pi -y || echo "postcreate: find-skills install failed (continuing)" >&2

# Put setup.sh on Path. The target must be ABSOLUTE: a relative one resolves against the
# link's own directory (~/.local/bin), not the workspace, so it dangles.
mkdir -p "$HOME/.local/bin"
ln -sfn "$SCRIPT_DIR/setup.sh" "$HOME/.local/bin/setup-devcontainer"

# Aspire CLI: the F5 launch config in .vscode/launch.json runs ~/.dotnet/tools/aspire
echo "Installing Aspire CLI..."
dotnet tool install -g Aspire.Cli

echo "Installing Angular CLI..."
npm install -g @angular/cli@latest

# npm install on the workspace bind mount can fail with ENOTDIR; don't fail the create over it
echo "Installing frontend dependencies..."
(cd "$SCRIPT_DIR/../../src/Ghosts.Frontend" && npm install) || echo "postcreate: frontend npm install failed (continuing)" >&2

echo "Installing Python packages..."
pip install --no-cache-dir --break-system-packages openai anthropic mkdocs-material
