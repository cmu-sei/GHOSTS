#!/usr/bin/env sh
set -eu

HTML_ROOT=${HTML_ROOT:-/usr/share/nginx/html}
ASSETS_DIR="${HTML_ROOT}/assets"
API_URL_VAL=${API_URL:-/api}

# Ensure target exists
mkdir -p "$ASSETS_DIR"

# Write config atomically
TMP="$(mktemp)"
printf '{\n  "apiUrl": "%s"\n}\n' "$API_URL_VAL" > "$TMP"
mv "$TMP" "${ASSETS_DIR}/config.json"

echo "Generated ${ASSETS_DIR}/config.json with apiUrl=${API_URL_VAL}"

exec nginx -g 'daemon off;'
