#!/bin/bash
set -euo pipefail

# Install Pi Coding Agent CLI from the published npm release, using the exact
# command upstream documents at https://pi.dev/docs/latest — including
# --ignore-scripts, which upstream states Pi "does not require ... for normal
# npm installs". It skips two inert dependency lifecycle scripts (@google/genai's
# echo-only preinstall and protobufjs's version-scheme warning postinstall) and
# nothing else; every native module in the tree is a prebuilt per-platform
# optional dep, so no compile step is being suppressed.
#
# NODE_EXTRA_CA_CERTS is exported inside the su body so npm trusts the
# SEI/Zscaler CA while fetching from the registry: containerEnv is runtime-only
# (unset during the image build when features run) and `su -` resets the env
# anyway. The system bundle already carries the custom CAs (installed by
# features/sei-certs, which is listed ahead of this feature — there is no Dockerfile
# to bake them in any more).
su - "${_REMOTE_USER}" -c "
    set -euo pipefail
    . ${NVM_DIR}/nvm.sh
    export NODE_EXTRA_CA_CERTS='/etc/ssl/certs/ca-certificates.crt'
    npm install -g --ignore-scripts @earendil-works/pi-coding-agent
"
