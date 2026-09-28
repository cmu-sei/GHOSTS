#!/bin/bash
set -euo pipefail

# Install the Pure Zsh prompt (https://github.com/sindresorhus/pure) and wire it into
# the remote user's ~/.zshrc in place of the oh-my-zsh theme that common-utils sets up.
#
# Done at feature BUILD time (not on every create, which is where this used to live):
# a prompt has no upgrade urgency like the agent CLIs do, so baking it into a cached
# image layer keeps it out of the create path entirely. The feature's postcreate.sh
# carries only the one piece that needs the workspace — the git dirty indicator.
#
# Ordered after common-utils via installsAfter so zsh, oh-my-zsh and ~/.zshrc exist
# by the time this runs. The body runs as the remote user so the clone and the .zshrc
# edits land in that user's home. No CA env is needed: git trusts the system bundle
# (/etc/ssl/certs/ca-certificates.crt), which already carries any custom CAs.
#
# Idempotent throughout, so re-running it by hand in a live container is safe: an
# existing clone is refreshed in place, and the .zshrc edits are skipped once
# `prompt pure` is in the file.

su - "${_REMOTE_USER}" -c '
    set -euo pipefail

    PURE_DIR="$HOME/.zsh/pure"
    ZSHRC="$HOME/.zshrc"

    # Shallow clone — Pure is autoloaded from the working tree, so its history is
    # dead weight. Upstream ships no release branch or tags to pin, so this tracks
    # the default branch; fetching `HEAD` rather than a branch name keeps it working
    # if that branch is ever renamed (it is `main`, not `master`, today).
    #
    # The refresh only happens on a re-run in a live container (a fresh build takes
    # the clone path below), so a network failure there is tolerated: the clone that
    # is already on disk works fine.
    if [ -d "$PURE_DIR/.git" ]; then
        git -C "$PURE_DIR" fetch -q --depth 1 origin HEAD \
            && git -C "$PURE_DIR" reset -q --hard FETCH_HEAD \
            || echo "pure-prompt: could not refresh $PURE_DIR, keeping it as is" >&2
    else
        mkdir -p "$(dirname "$PURE_DIR")"
        git clone -q --depth 1 https://github.com/sindresorhus/pure.git "$PURE_DIR"
    fi

    [ -f "$ZSHRC" ] || touch "$ZSHRC"

    if ! grep -q "^prompt pure$" "$ZSHRC"; then
        # Pure has to be on FPATH BEFORE oh-my-zsh is sourced (that is what runs
        # compinit), so it is spliced in at the ZSH_THEME line rather than appended.
        # Blanking ZSH_THEME hands the prompt over to Pure — otherwise the
        # devcontainers theme keeps drawing its own on top.
        sed -i "s|^ZSH_THEME=.*|ZSH_THEME=\"\"\n\nFPATH=\$HOME/.zsh/pure:\$FPATH|" "$ZSHRC"

        {
            echo ""
            echo "# Pure prompt"
            # Only reached when the sed above found no ZSH_THEME line to splice into
            # (a .zshrc without oh-my-zsh). Nothing runs compinit in that case, so
            # setting FPATH here, next to promptinit, is enough.
            if ! grep -q "^FPATH=.*\.zsh/pure" "$ZSHRC"; then
                echo "FPATH=\$HOME/.zsh/pure:\$FPATH"
            fi
            echo "autoload -U promptinit; promptinit"
            echo "prompt pure"
        } >> "$ZSHRC"
    fi
'
