#!/bin/bash
set -euo pipefail

# Install (or upgrade) the Codex CLI via the official standalone installer.
#
# This script is the single source of truth for getting Codex onto the box.
# It runs in two contexts:
#   1. Dev container feature build (as root, with _REMOTE_USER set) — the
#      install is baked into a cached image layer.
#   2. postcreate.sh (as the remote user, on every container create) — to
#      upgrade to the latest version. The curl installer is also the only
#      upgrade path; Codex has no incremental self-update subcommand, so both
#      contexts run the same command.

INSTALL_CMD='curl -fsSL https://chatgpt.com/codex/install.sh | CODEX_NON_INTERACTIVE=1 sh'

# Drop to the remote user when invoked as root during the feature build;
# otherwise (postcreate) we are already the remote user, so run directly.
if [ "$(id -u)" = "0" ] && [ -n "${_REMOTE_USER:-}" ]; then
    su - "${_REMOTE_USER}" -c "$INSTALL_CMD"
else
    eval "$INSTALL_CMD"
fi
