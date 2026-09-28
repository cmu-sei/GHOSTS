#!/bin/bash
set -euo pipefail

# Install oh-my-logo CLI globally
su - "${_REMOTE_USER}" -c ". ${NVM_DIR}/nvm.sh && npm install -g oh-my-logo"
