#!/bin/bash
set -euo pipefail

# Install Claude Code CLI
su - "${_REMOTE_USER}" -c "curl -fsSL https://claude.ai/install.sh | bash"
