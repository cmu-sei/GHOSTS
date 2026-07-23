#!/bin/sh
set -e

CONFIG_FILE=/usr/share/nginx/html/assets/config.json
API_URL_VAL=${API_URL:-/api}
WS_URL_VAL=${WS_URL:-ws://localhost:8099}

# Write config.json
cat > $CONFIG_FILE <<EOF
{
  "apiUrl": "$API_URL_VAL",
  "wsUrl": "$WS_URL_VAL"
}
EOF

echo "Generated $CONFIG_FILE with apiUrl=$API_URL_VAL, wsUrl=$WS_URL_VAL"

exec "$@"
