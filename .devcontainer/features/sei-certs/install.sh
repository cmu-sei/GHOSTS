#!/bin/bash
## Install certs
set -euo pipefail
dest=/usr/local/share/ca-certificates/custom
mkdir -p $dest
cp $(dirname "$0")/*.crt $dest

find $dest -type f ! -name '*.crt' -delete
if find $dest -type f -name '*.crt' -print -quit | grep -q .; then
    update-ca-certificates
fi
