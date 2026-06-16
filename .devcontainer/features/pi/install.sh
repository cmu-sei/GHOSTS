#!/bin/bash
set -euo pipefail

# Install Pi Coding Agent CLI
su - "${_REMOTE_USER}" -c ". ${NVM_DIR}/nvm.sh && npm install -g @earendil-works/pi-coding-agent"
