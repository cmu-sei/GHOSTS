#!/bin/bash
## Install any root CA certificates placed beside this script. None is a valid state:
## outside a TLS-inspecting network there is nothing to add.
set -euo pipefail
src=$(dirname "$0")
dest=${SEI_CERTS_DEST:-/usr/local/share/ca-certificates/custom}
mkdir -p "$dest"
shopt -s nullglob
certs=("$src"/*.crt)
if [ ${#certs[@]} -eq 0 ]; then
    echo "sei-certs: no .crt files in $src; nothing to install."
    exit 0
fi
cp "${certs[@]}" "$dest"
find "$dest" -type f ! -name '*.crt' -delete
update-ca-certificates
