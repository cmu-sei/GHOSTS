#!/bin/bash
set -euo pipefail

# Install this feature's tmux.conf as the remote user's ~/.tmux.conf.
#
# Done at feature BUILD time rather than on every create (where this used to live, in
# the project's postcreate.sh): the config is static, so baking it into a cached image
# layer keeps it off the create path — and it also stops clobbering hand edits made in
# a running container on every rebuild-less create. A dotfiles repo still wins, since
# the CLI applies dotfiles after features.
#
# The file is written whole (not appended to), so this feature is its single owner —
# see the header comment in tmux.conf about why the settings are not split across the
# features that motivate them.

SRC="$(dirname "$0")/tmux.conf"

# Two contexts, like the codex feature's installer: the feature build runs as root with
# _REMOTE_USER set (so the destination home has to be looked up, and the copy chowned),
# while a manual re-run in a live container is already the right user.
if [ "$(id -u)" = "0" ] && [ -n "${_REMOTE_USER:-}" ]; then
    USER_HOME="$(getent passwd "${_REMOTE_USER}" | cut -d: -f6)"
    install -m 644 -o "${_REMOTE_USER}" -g "$(id -gn "${_REMOTE_USER}")" \
        "$SRC" "$USER_HOME/.tmux.conf"
else
    install -m 644 "$SRC" "$HOME/.tmux.conf"
fi
