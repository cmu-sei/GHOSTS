#!/bin/bash
set -euo pipefail

# Nothing to install at image build: this feature's whole job happens at create time
# (postcreate.sh), against a live AWS account whose credentials only exist in the running
# container — devcontainer.env is a --env-file, so none of the AWS_* vars are set while
# features are being built.
#
# The feature exists anyway, rather than as a block in some other script, for two reasons:
#
#   - It owns the Bedrock gates that are ACCOUNT-level, so they belong to no single agent.
#     Claude Code, LiteLLM, OpenCode, Pi and Grok all fail the same way when the Region's
#     data retention mode is too restrictive, or when a GovCloud model has no entitlement.
#   - Listing it is what pulls in the AWS CLI, via dependsOn in devcontainer-feature.json.
#     A devcontainer.json that has no Bedrock profile lists neither, and pays for neither.
#
# Do not "optimize" this file away: a feature without an install.sh entrypoint is not a
# valid feature, and the CLI fails the build when it cannot run one.
echo "bedrock: nothing to install at build time (see postcreate.sh)."
