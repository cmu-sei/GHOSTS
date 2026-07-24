#!/bin/bash
set -euo pipefail

# Install (or upgrade) the Claude Code CLI via the official installer script.
# Runs in two contexts: as root during the feature build (with _REMOTE_USER
# set) and as the remote user if reused later. Drop to the remote user only
# when invoked as root; otherwise run directly.
INSTALL_CMD='curl -fsSL https://claude.ai/install.sh | bash'

if [ "$(id -u)" = "0" ] && [ -n "${_REMOTE_USER:-}" ]; then
    su - "${_REMOTE_USER}" -c "$INSTALL_CMD"
else
    eval "$INSTALL_CMD"
fi
