#!/bin/bash
set -euo pipefail

# Install xAI's Grok CLI ("Grok Build", binary `grok`) via the official
# standalone installer, exactly as https://x.ai/cli documents it. The CLI is a
# single static Rust binary — no Node, no npm — that the installer drops under
# ~/.grok/downloads and links from ~/.grok/bin (and ~/.local/bin, which is on
# PATH). Everything Grok owns lives under ~/.grok (config.toml, auth.json,
# sessions, docs, the binary itself), so this feature's postcreate.sh persists
# that whole directory to the data volume like ~/.claude, ~/.codex and ~/.pi. The
# ~/.local/bin link is made explicitly: the installer only creates it when
# ~/.local/bin is already on PATH, which depends on which rc files the `su -`
# login shell happens to read, while postcreate.sh's `bash -l` (and `grok
# update` in it) rely on ~/.profile's ~/.local/bin entry, not on the
# ~/.grok/bin block the installer appends to ~/.zshrc.
#
# Upgrade path is `grok update` in this feature's postcreate.sh (a self-update
# subcommand, like `pi update self`) — this script runs only at image build.
#
# Grok has no native Bedrock (SigV4) provider. It reaches Bedrock through the
# Mantle OpenAI-compatible endpoint, which authenticates with a *Bedrock API
# key* as a bearer token, minted on demand by a `[auth_provider] command`. That
# command is the bedrock-api-key wrapper installed below, backed by the
# aws-bedrock-token-generator library (which ships no CLI of its own) in a uv
# venv on the base image's system Python — the same `only-system` mode the chat
# stack uses, so no interpreter download. SSL_CERT_FILE is exported inside the
# su body so uv trusts the SEI/Zscaler CA while fetching from PyPI:
# containerEnv is runtime-only (unset during the image build when features
# run) and `su -` resets the env anyway.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

install -m 755 "$SCRIPT_DIR/bedrock-api-key" /usr/local/bin/bedrock-api-key

su - "${_REMOTE_USER}" -c "
    set -euo pipefail
    export SSL_CERT_FILE='/etc/ssl/certs/ca-certificates.crt'
    curl -fsSL https://x.ai/cli/install.sh | bash
    mkdir -p \"\$HOME/.local/bin\"
    ln -sfn \"\$HOME/.grok/bin/grok\" \"\$HOME/.local/bin/grok\"
    uv venv --python-preference only-system \"\$HOME/.local/bedrock-token-venv\"
    uv pip install --python \"\$HOME/.local/bedrock-token-venv/bin/python\" aws-bedrock-token-generator
"
