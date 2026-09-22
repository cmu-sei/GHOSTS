#!/bin/bash
## Prompt configuration that needs the workspace
set -euo pipefail

# The prompt itself is installed and wired into ~/.zshrc at feature build time (see
# install.sh). This is the only part that cannot be baked into the image: the setting
# lives in the WORKSPACE repo's own git config, and no workspace is mounted during
# the build.
#
# devcontainers-theme.show-dirty makes the prompt flag a dirty working tree. It is
# read out of the repo's git config by the prompts that ship with the devcontainers
# base image — the bash prompt (still in use; Pure only replaces the zsh one) and the
# oh-my-zsh "devcontainers" theme.
#
# postSTART rather than postCreate: the value is written to .git/config, which lives
# in the workspace and therefore OUTLIVES the container — this is a one-time seed per
# repo, not per-create state, and create time is the worst moment to attempt it. A
# workspace that is not a repo yet (or that gets `git init`'d after the container was
# built) would otherwise stay unconfigured until the next full rebuild; from postStart
# a restart is enough to pick it up.
#
# Seeded only when unset, since it now runs on every start: a developer who turns the
# dirty marker off (`git config devcontainers-theme.show-dirty 0`) keeps that choice
# instead of having it reverted at each boot. Same convention as the Claude
# permission-mode default and the seeded project settings file.
if git rev-parse --is-inside-work-tree &>/dev/null \
    && [ -z "$(git config --get devcontainers-theme.show-dirty || true)" ]; then
    git config devcontainers-theme.show-dirty 1
fi
