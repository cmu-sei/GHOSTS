#!/bin/bash
# Install the browser-chat stack — LiteLLM proxy, Open WebUI, Open Terminal, SearXNG,
# and supervisord — in one pinned bundle. These five install together or not at all:
# they exist only to serve the Open WebUI browser chat, and a container either has the
# whole stack or none of it (a `spawn` without --chat omits this entire feature, so
# supervisord/start.sh keys its whole block off the supervisord binary's presence).
#
# The feature is self-contained: postcreate.sh generates the LiteLLM config from the
# configured profiles and poststart.sh starts the stack, both declared as lifecycle
# hooks in devcontainer-feature.json — a project only lists "./features/chat" in its
# devcontainer.json and adds nothing to its own postcreate/poststart.
set -euo pipefail

# All five install as uv-managed environments that reuse the base image's system
# Python — EXCEPT Open WebUI, which needs its own managed 3.12 (see below). `uv tool
# install` auto-symlinks each console entry point into ~/.local/bin (the remote
# user's PATH, mirroring how the claude/codex binaries are exposed) — no manual
# venv/`ln` needed. `--python-preference only-system` (no version) makes uv reuse
# whatever system interpreter is on PATH (3.14 today; floats with the base tag) and
# NEVER download a managed build — avoiding the ~28MB GitHub fetch (and its TLS-proxy
# gauntlet) for services that don't need a pinned interpreter; it errors rather than
# silently downloading if no system Python is found.
#
# uv comes from ghcr.io/devcontainers-extra/features/uv, declared as a dependsOn in
# devcontainer-feature.json so it is both PULLED IN and ordered before this feature
# (installsAfter would only have ordered it, leaving uv missing unless the project
# happened to list it too); it lands on /usr/local/bin, on PATH for
# all shells including the non-interactive `su -` login shell below — no PATH fix-up.
#
# SSL_CERT_FILE is exported inside the su body so uv/git trust the SEI/Zscaler CA
# while fetching packages (and Open WebUI's managed CPython 3.12 from GitHub, and
# SearXNG's git clone). uv reads SSL_CERT_FILE but otherwise falls back to its OWN
# bundled webpki roots, which lack the corporate CA — behind the proxy that transfer
# fails `invalid peer certificate: UnknownIssuer`. containerEnv.SSL_CERT_FILE is
# runtime-only (unset during the image build when this feature runs) and `su -`
# resets the env anyway, so it must be set here. The system bundle already carries
# the custom CAs (installed by features/sei-certs, which is listed ahead of this
# feature — there is no Dockerfile to bake them in any more).
#
# Each service pins an exact version so its config/data schema, tool schema, or
# supervisord.conf stays stable across container creates; upgrades are deliberate
# (bump the pin, rebuild — see .claude/skills/chat-stack/SKILL.md, "Pinned-uv
# infrastructure services").
#
# Runs as root during the feature build; everything below executes as the remote
# user so the tool environments, venv, clone, and entry-point symlinks land in that
# user's home.

# --- LiteLLM ---
# Pinned to a PyPI release that includes the two changes the awsgov profile needs to
# reach OpenAI gpt-5.4 on GovCloud Bedrock Mantle with its existing SigV4 credentials
# (no Bedrock API key):
#   - BerriAI/litellm#29490 — the bedrock_mantle Responses route
#   - BerriAI/litellm#29788 — SigV4/IAM auth fallback on that route
# Both are verified in the 1.93.0 sdist, along with Python 3.14 support.
LITELLM_VERSION="1.93.0"

# --- Open WebUI ---
# The ONE service that pulls its own managed Python: open-webui 0.11.0 is hard-capped
# below 3.13 (requires-python >=3.11,<3.13.0a1), so it passes `--python 3.12` and uv
# downloads a managed CPython 3.12 (no system 3.12 exists). open-webui hard-depends on
# torch (via its pinned accelerate + sentence-transformers, used for local RAG/embeddings).
# On Linux x86 the default PyPI torch wheel bundles the full CUDA stack (nvidia/* +
# triton, ~3.5G) even though this container has no GPU. `--torch-backend=cpu` resolves
# torch from PyTorch's CPU index (2.13.0+cpu, no nvidia/* packages); `--with` installs it
# into the same tool environment so it satisfies open-webui's torch pin. Keep the torch
# pin in lockstep with what open-webui's deps resolve to when bumping the open-webui pin
# (verify with `uv pip compile --python-version 3.12 --torch-backend=cpu`).
OPENWEBUI_VERSION="0.11.0"
TORCH_VERSION="2.13.0"
OPENWEBUI_PYTHON="3.12"

# --- Open Terminal ---
# Sandbox command/file API Open WebUI's chat calls as tools. Runs fine on system Python.
OPENTERMINAL_VERSION="0.11.34"

# --- supervisor ---
# The process manager for the four services above; `uv tool install` symlinks BOTH
# entry points (`supervisord` to run the daemon, `supervisorctl` for start.sh's
# health/reload path and manual service management). Pure-python, no compiled deps.
# Its pin keeps the committed supervisord.conf schema stable across creates.
SUPERVISOR_VERSION="4.3.0"

# --- SearXNG ---
# UNLIKE the others, SearXNG canNOT use `uv tool install`: the `searxng` name on PyPI
# is an empty placeholder. SearXNG is a Flask app distributed only as a git repo,
# installed as an editable build from a working tree, with no console entry point — it
# runs as `python -m searx.webapp`. So it clones at a pinned COMMIT (SearXNG is
# rolling-release and ships no version tags) into a uv venv at the ~/.local/searxng-venv
# path the wrapper expects. Build deps (gcc/g++/make, libxml2-dev, libxslt-dev) for its
# compiled deps (lxml, msgspec) are already in the base image, so no apt step; the
# editable build needs a few build-time packages in the venv first (per SearXNG's docs),
# then an editable install with `--no-build-isolation` (uv defaults to PEP 517 and
# rejects the old pip `--use-pep517` flag).
SEARXNG_REF="e6559c9ad6f3f5216833cff843b76ef759eb6223"

su - "${_REMOTE_USER}" -c "
    set -euo pipefail
    export SSL_CERT_FILE='/etc/ssl/certs/ca-certificates.crt'

    # LiteLLM, Open Terminal, supervisor — system Python, isolated tool envs.
    uv tool install --python-preference only-system 'litellm[proxy]==${LITELLM_VERSION}'
    uv tool install --python-preference only-system 'open-terminal==${OPENTERMINAL_VERSION}'
    uv tool install --python-preference only-system 'supervisor==${SUPERVISOR_VERSION}'

    # Open WebUI — managed CPython 3.12 + CPU-only torch in the same tool env.
    uv tool install --python '${OPENWEBUI_PYTHON}' \
        --with 'torch==${TORCH_VERSION}' --torch-backend=cpu \
        'open-webui==${OPENWEBUI_VERSION}'

    # SearXNG — shallow clone at the pinned commit + editable venv install.
    SRC=\"\$HOME/.local/searxng-src\"
    VENV=\"\$HOME/.local/searxng-venv\"
    rm -rf \"\$SRC\"
    git init -q \"\$SRC\"
    git -C \"\$SRC\" remote add origin https://github.com/searxng/searxng.git
    git -C \"\$SRC\" fetch -q --depth 1 origin \"${SEARXNG_REF}\"
    git -C \"\$SRC\" checkout -q FETCH_HEAD
    uv venv --python-preference only-system \"\$VENV\"
    uv pip install --python \"\$VENV/bin/python\" setuptools wheel pyyaml msgspec typing-extensions pybind11
    uv pip install --python \"\$VENV/bin/python\" --no-build-isolation -e \"\$SRC\"
"
