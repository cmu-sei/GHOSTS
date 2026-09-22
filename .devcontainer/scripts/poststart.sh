#!/bin/bash
set -euo pipefail

# --- Welcome logo ---
# The workspace name rendered by oh-my-logo, which is an OPTIONAL cosmetic feature:
# agent-dev's devcontainer.json lists ./features/oh-my-logo, and a spawned project can drop it.
# So its absence must not be an error — unguarded, the command substitution below exits
# 127 and `set -e` kills poststart before the CUI notice or the agent list ever print.
# The palette is derived from the workspace name so a given project always gets the same
# colours; that hashing is worthless without a renderer, so it lives inside the guard
# too. The greeting itself stays unconditional — a container without oh-my-logo should
# still say hello rather than nothing.
LOGO_NAME=$(basename "$PWD")

echo ""
if command -v oh-my-logo >/dev/null 2>&1; then
    PALETTES=(grad-blue sunset dawn nebula mono ocean fire forest gold purple mint coral matrix)
    LOGO_HASH=$(echo -n "$LOGO_NAME" | tr '[:lower:]' '[:upper:]' | md5sum | cut -c1-8)
    LOGO_PALETTE=${PALETTES[$(( 16#$LOGO_HASH % ${#PALETTES[@]} ))]}

    export COLORTERM=truecolor
    # Blanked on failure rather than printed: stderr is folded into the capture, so an
    # installed-but-broken renderer (missing font, no colour support) would otherwise
    # print its error message where the logo belongs — and abort under `set -e`.
    LOGO_OUTPUT=$(oh-my-logo "$LOGO_NAME" "$LOGO_PALETTE" --filled --color --block-font chrome 2>&1) \
        || LOGO_OUTPUT=""
    if [ -n "$LOGO_OUTPUT" ]; then
        echo "$LOGO_OUTPUT"
    fi
fi
echo -e "Welcome to the \033[1m$LOGO_NAME\033[0m development container!"
echo ""

# --- CUI Data Notice ---
# Read from the `.cui_approved` markers, not from profile names: the CUI question deletes
# one entire side of that partition (`choose_cui` in setup.sh), so the profiles that
# survived ARE the answer, and a provider added later needs nothing here. A single
# unmarked profile makes this a public project — the two sides never coexist, so the only
# way to see a mix is a project that was never asked (a hand-copied `.devcontainer/` whose
# setup had no terminal), which is exactly the case that should get the warning.
PROFILES_DIR=".devcontainer/profiles"
PROFILE_COUNT=0
PUBLIC_SEEN=0

for dir in "$PROFILES_DIR"/*/; do
    [ -f "$dir/profile.env" ] || continue
    PROFILE_COUNT=$((PROFILE_COUNT + 1))
    if [ ! -f "$dir/.cui_approved" ]; then
        PUBLIC_SEEN=1
    fi
done

if [ "$PROFILE_COUNT" -eq 0 ]; then
    :
elif [ -f ".template-container" ]; then
    # A checkout of agent-dev itself: setup skipped the question, so both sides are
    # present and nothing was decided. Say that explicitly rather than reporting it as a
    # public project — the profiles here are a maintainer's test matrix, not a policy answer.
    echo -e "\e[33m⚠️  Template checkout — all profiles present, no provider approved.\e[0m"
    echo -e "\e[33m   CUI data is NOT permitted here. Spawn a project to answer the question.\e[0m"
    echo ""
elif [ "$PUBLIC_SEEN" -eq 1 ]; then
    echo -e "\e[33m⚠️  Public providers — CUI data is NOT permitted in this environment.\e[0m"
    echo ""
else
    echo -e "\e[32m✅ CUI-approved providers — CUI data is permitted in this environment.\e[0m"
    echo ""
fi

# --- Installed agents ---
# Detected, not hardcoded: each agent ships as its own dev container feature, so which
# ones are present depends on the project's devcontainer.json (setup's feature prompt
# lets a spawned project drop any of the five). The binary on PATH is the ground truth.
# `command -v` is a shell builtin and spawns nothing, so this adds nothing measurable to a
# container start — deliberately no `--version` calls here, which would mean five process
# launches every time.
AGENTS=()
for agent in claude codex opencode pi grok; do
    if command -v "$agent" >/dev/null 2>&1; then
        AGENTS+=("$agent")
    fi
done

if [ ${#AGENTS[@]} -gt 0 ]; then
    AGENT_LIST=$(printf '%s, ' "${AGENTS[@]}")
    echo "Installed agents: ${AGENT_LIST%, }"
else
    echo "No coding agents are installed in this container."
fi
echo ""


echo ""
echo "Type Ctrl-Shift-\` (backtick) to open a new terminal and get started building. 🛠️"
echo ""
