#!/bin/sh
# Wraps the stock n8n entrypoint so custom root CAs mounted at /opt/ghosts-certs (the
# same .devcontainer/certs used to build the devcontainer trust store) are trusted by
# Node inside the container. Without this, workflow nodes making outbound HTTPS calls
# through TLS inspection fail with "unable to get local issuer certificate".
# NODE_EXTRA_CA_CERTS takes a single file, so the .crt files are concatenated first.
# Note: n8n's own /opt/custom-certificates hook is deliberately not used — it needs a
# writable mount for c_rehash and switches Node to --use-openssl-ca.
set -e

CERT_DIR="/opt/ghosts-certs"
BUNDLE="/tmp/custom-ca-bundle.crt"

if [ -d "$CERT_DIR" ] && [ -n "$(find "$CERT_DIR" -type f -name '*.crt' -print -quit)" ]; then
  cat "$CERT_DIR"/*.crt > "$BUNDLE"
  export NODE_EXTRA_CA_CERTS="$BUNDLE"
  echo "[certs] Trusting custom CAs from $CERT_DIR via NODE_EXTRA_CA_CERTS=$BUNDLE"
else
  echo "[certs] No .crt files in $CERT_DIR — using default trust store"
fi

exec /docker-entrypoint.sh "$@"
