#!/bin/bash
set -euo pipefail

# Install OpenCode CLI
su - "${_REMOTE_USER}" -c "curl -fsSL https://opencode.ai/install | bash"
