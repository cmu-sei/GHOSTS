#!/bin/bash
# Install Herdr with the official stable-channel installer.
set -euo pipefail

# Features run as root at image build; install into the remote user's
# ~/.local/bin so `herdr update` can replace the binary without sudo.
# Also allow running this script as the container user before a rebuild.
if [ "$(id -u)" = "0" ] && [ -n "${_REMOTE_USER:-}" ]; then
    su - "${_REMOTE_USER}" -s /bin/bash -c '
        set -euo pipefail
        export SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt
        curl -fsSL https://herdr.dev/install.sh | sh
    '
else
    export SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt
    curl -fsSL https://herdr.dev/install.sh | sh
fi
