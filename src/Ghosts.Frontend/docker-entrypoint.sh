#!/bin/sh
set -e

CONFIG_FILE=/usr/share/nginx/html/assets/config.json
API_URL_VAL=${API_URL:-/api}
N8N_API_URL_VAL=${N8N_API_URL:-http://localhost:5678}
GRAFANA_URL_VAL=${GRAFANA_URL:-http://localhost:3000}

# Write config.json
cat > $CONFIG_FILE <<EOF
{
  "apiUrl": "$API_URL_VAL",
  "n8nApiUrl": "$N8N_API_URL_VAL",
  "grafanaUrl": "$GRAFANA_URL_VAL"
}
EOF

echo "Generated $CONFIG_FILE with apiUrl=$API_URL_VAL, n8nApiUrl=$N8N_API_URL_VAL, grafanaUrl=$GRAFANA_URL_VAL"

exec "$@"
