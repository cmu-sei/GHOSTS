#!/bin/bash
## Clear the Amazon Bedrock account-level access gates
#
# Everything Bedrock needs at create time lives here, so a devcontainer.json that lists
# "./features/bedrock" gets a usable Bedrock account with NOTHING added to the project's
# own postcreate.sh. Declared as this feature's postCreateCommand in
# devcontainer-feature.json; the workspace folder is the cwd.
#
# These two gates are ACCOUNT-level, which is why they are here and not in an agent's
# feature: every agent that reaches Bedrock (Claude Code, LiteLLM, OpenCode, Pi, Grok)
# fails the same way when the invoking Region's data retention mode is too restrictive or
# a GovCloud model has no entitlement, and none of them owns the fix. Per-agent model and
# provider config stays in each agent's own postcreate.sh.
#
# Both blocks are gated on a Bedrock profile actually being configured, so this is inert
# under the SEI profiles (opal, etc). The AWS CLI comes from this feature's dependsOn.
set -euo pipefail

SCRIPT_DIR=$(dirname "$0")
# .devcontainer/, two levels up from .devcontainer/features/bedrock/.
DEVCONTAINER_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
DEVENV="$DEVCONTAINER_DIR/devcontainer.env"
PROFILES_DIR="$DEVCONTAINER_DIR/profiles"

# dependsOn guarantees the CLI at build time, so a miss here means a container built
# before this feature was added, or a failed install. Warn rather than fail: nothing below
# is worth breaking a create over.
if ! command -v aws &>/dev/null; then
    echo -e "\e[33m⚠️  The AWS CLI is not installed — skipping the Bedrock account checks.\e[0m"
    echo "    Rebuild the container so ./features/bedrock can pull it in (dependsOn: aws-cli)."
    exit 0
fi

# --- Bedrock data retention ---
# The one Bedrock access gate that applies in BOTH partitions: the account's data
# retention mode in the invoking Region. Some models (today the Fable family)
# require human review as a condition of access and refuse to run unless that
# Region's mode is 'aws_review' or 'provider_data_share' — the default 'inherit'
# (like 'none' and 'default') does not satisfy them, and the failure surfaces as a
# ValidationException naming the retention mode rather than an access error.
#
# Set unconditionally, with no prompt: postCreateCommand has no TTY, and a model
# that cannot run at all is not a useful default. Because it is applied without
# asking, the output discloses what the mode authorizes whenever it actually
# changes something. The setting is per invoking Region and account-wide, so it is
# read first and written only when not already permissive enough —
# 'provider_data_share' is never downgraded, and an already-'aws_review' account
# gets one line instead of the disclosure on every create.
#
# AWS_REGION comes from devcontainer.env via docker --env-file, so a bare `aws`
# call already targets the right account and region. Every step is
# failure-tolerant: postcreate runs under `set -euo pipefail`, and an expired
# credential or a network blip must warn, never break container create.
if [ -f "$DEVENV" ] && grep -q '^CONFIGURED_PROFILES=.*aws' "$DEVENV" 2>/dev/null; then
    RETENTION_REGION="${AWS_REGION:-}"
    if [ -z "$RETENTION_REGION" ]; then
        echo -e "\e[33m⚠️  AWS_REGION is not set — skipping the Bedrock data retention check.\e[0m"
    else
        echo "Checking Bedrock data retention mode in $RETENTION_REGION..."
        # stderr is captured, not discarded, so a failure can name the AWS error.
        RETENTION_ERR=""
        RETENTION_MODE=$(AWS_MAX_ATTEMPTS=2 aws bedrock get-account-data-retention \
            --region "$RETENTION_REGION" --cli-connect-timeout 5 --cli-read-timeout 10 \
            --query mode --output text 2>&1) || RETENTION_ERR="$RETENTION_MODE"
        if [ -n "$RETENTION_ERR" ]; then
            echo -e "\e[33m⚠️  Could not read the Bedrock data retention mode in $RETENTION_REGION:\e[0m"
            echo "    $RETENTION_ERR"
            echo "    Continuing — models that require human review may refuse to run."
        else
            case "$RETENTION_MODE" in
                aws_review|provider_data_share)
                    echo "Bedrock data retention in $RETENTION_REGION is already '$RETENTION_MODE' — left as is."
                    ;;
                *)
                    RETENTION_PUT_ERR=""
                    RETENTION_PUT_OUT=$(AWS_MAX_ATTEMPTS=2 aws bedrock put-account-data-retention \
                        --mode aws_review --region "$RETENTION_REGION" \
                        --cli-connect-timeout 5 --cli-read-timeout 10 2>&1) \
                        || RETENTION_PUT_ERR="$RETENTION_PUT_OUT"
                    if [ -n "$RETENTION_PUT_ERR" ]; then
                        echo -e "\e[33m⚠️  Could not set the Bedrock data retention mode in $RETENTION_REGION:\e[0m"
                        echo "    $RETENTION_PUT_ERR"
                        echo "    Continuing — models that require human review may refuse to run."
                    else
                        echo "Bedrock data retention in $RETENTION_REGION set: '$RETENTION_MODE' -> 'aws_review'."
                        echo ""
                        echo "What 'aws_review' authorizes for this account in $RETENTION_REGION:"
                        echo "  • Prompts and completions are retained inside the AWS boundary for up to 30 days."
                        echo "  • AWS may have humans review that content, for models whose provider requires it."
                        echo "  • Content is NOT shared with the model provider."
                        echo "  • Account-wide but per Region; each model's own allowed modes still govern its data."
                        echo ""
                        echo "  https://docs.aws.amazon.com/bedrock/latest/userguide/data-retention.html"
                        echo ""
                    fi
                    ;;
            esac
        fi
    fi
fi

# --- Verify GovCloud Anthropic model access ---
# A fresh GovCloud account cannot call the Anthropic models until three gates are
# cleared: the account's use-case form, a model agreement created from the linked
# commercial account, and a per-region entitlement. Until then every agent request
# fails with an access/entitlement error that reads like a bad credential, so probe
# for it here. Commercial accounts need none of this — their Anthropic models are
# entitled out of the box — so this stays awsgov-gated.
#
# The per-region entitlement IS requested automatically here, and it is the only
# gate that can be: it needs no human decision (the consent is the EULA in the
# agreement, already made) and no commercial credentials. It goes through an
# undocumented endpoint — POST /foundation-model-entitlement, the one the Bedrock
# console itself calls — because no CLI/SDK operation exposes it:
#   https://dev.to/aws-builders/automating-new-bedrock-foundation-model-access-3ec8
# Verified live in GovCloud with the account's own credentials: a model with
# agreementAvailability=AVAILABLE and entitlementAvailability=NOT_AVAILABLE came
# back HTTP 201 {"status":"SUCCESS"} and read AVAILABLE seconds later; an
# already-entitled model returns the same 201, so it is idempotent. It is only
# attempted when the agreement is AVAILABLE — untested without one, and without an
# agreement there is nothing this side can consent to anyway.
#
# The other two gates need the LINKED COMMERCIAL account, which this container does
# not have, so they are only reported — with a pointer at the interactive helper.
# This block cannot prompt: postCreateCommand has no TTY and that repair asks
# questions (a form, possibly a pasted credential block).
#
# `entitlementAvailability` (with `regionAvailability`) is the usability check;
# `agreementAvailability` only decides whether the entitlement can be requested.
# `authorizationStatus` is deliberately NOT consulted: it is account-level, so it
# reads AUTHORIZED in the most common broken case. Data retention is a separate
# gate, owned by the block above.
#
# One region: Anthropic models are invoked from $AWS_REGION only (awsgov's configs
# mention us-gov-west-1 solely for a Mantle GPT model), so probing the other gov
# region would report failures for a region nothing calls.
#
# The model list is derived from profiles/awsgov/claude.json rather than duplicated
# here, so it cannot drift from the ids the agents actually use. get-foundation-
# model-availability is a control-plane call: no inference, no token spend.
#
# Time budget: a few seconds when everything is already usable, plus up to ~35s per
# model that actually needs entitling (one POST, then polling for propagation).
# Every step is failure-tolerant — postcreate runs under `set -euo pipefail`, and a
# network blip or an expired key must warn, never break container create.

# Request the per-region entitlement for one model. Undocumented endpoint (see
# above); signed with `curl --aws-sigv4` because there is no CLI operation for it
# and the container has no Python/uv to sign with. Returns 0 on any 2xx; on failure
# ENTITLE_ERR holds the reason. Credentials reach curl through a config on STDIN,
# never argv (argv is world-readable in `ps`), and are never echoed or written to
# disk. The model id must be the BARE one — strip any `us-gov.` prefix.
request_entitlement() {
    local model="$1" region="$2"
    local creds key secret token token_header resp code body
    ENTITLE_ERR=""
    creds=$(aws configure export-credentials --format process 2>&1) \
        || { ENTITLE_ERR="could not export credentials: $creds"; return 1; }
    key=$(jq -r '.AccessKeyId // empty' <<<"$creds" 2>/dev/null) || key=""
    secret=$(jq -r '.SecretAccessKey // empty' <<<"$creds" 2>/dev/null) || secret=""
    if [ -z "$key" ] || [ -z "$secret" ]; then
        ENTITLE_ERR="could not parse credentials from aws configure export-credentials"
        return 1
    fi
    # Absent for long-term keys (the normal container case), so its header line is
    # built only when there is one. It is assembled HERE, not inline in the
    # heredoc as `${token:+header = "..."}`: those quotes would be removed as
    # shell quoting during expansion, and curl's config parser then reads an
    # unquoted value only up to the first space — sending a valueless
    # `x-amz-security-token:` header (verified with `curl --libcurl`). A variable
    # substituted into the heredoc keeps its quotes.
    token=$(jq -r '.SessionToken // empty' <<<"$creds" 2>/dev/null) || token=""
    token_header=""
    if [ -n "$token" ]; then
        token_header="header = \"x-amz-security-token: $token\""
    fi
    resp=$(curl -sS --connect-timeout 5 --max-time 20 -w '\n%{http_code}' -K - <<EOF 2>&1
url = "https://bedrock.$region.amazonaws.com/foundation-model-entitlement"
request = "POST"
aws-sigv4 = "aws:amz:$region:bedrock"
user = "$key:$secret"
$token_header
header = "Content-Type: application/x-amz-json-1.1"
data = "{\"modelId\":\"$model\"}"
EOF
    ) || { ENTITLE_ERR="$resp"; return 1; }
    code="${resp##*$'\n'}"
    body="${resp%$'\n'*}"
    case "$code" in
        2*) return 0 ;;
    esac
    ENTITLE_ERR="HTTP $code $body"
    return 1
}

# Read one model's availability flags into GOV_AGR / GOV_ENT / GOV_REG. Returns
# non-zero when the call or the parse fails, with GOV_READ_ERR naming why. Short
# timeouts and a low retry count: this must never stall container create.
read_model_availability() {
    local model="$1" region="$2" json
    GOV_AGR=""; GOV_ENT=""; GOV_REG=""; GOV_READ_ERR=""
    json=$(AWS_MAX_ATTEMPTS=2 aws bedrock get-foundation-model-availability \
        --model-id "$model" --region "$region" \
        --cli-connect-timeout 5 --cli-read-timeout 10 --output json 2>&1) \
        || { GOV_READ_ERR=$(echo "$json" | tr '\n' ' ' | tr -s ' '); return 1; }
    GOV_AGR=$(jq -r '.agreementAvailability.status // "UNKNOWN"' <<<"$json" 2>/dev/null) || GOV_AGR=""
    GOV_ENT=$(jq -r '.entitlementAvailability // "UNKNOWN"' <<<"$json" 2>/dev/null) || GOV_ENT=""
    GOV_REG=$(jq -r '.regionAvailability // "UNKNOWN"' <<<"$json" 2>/dev/null) || GOV_REG=""
    if [ -z "$GOV_AGR" ] || [ -z "$GOV_ENT" ] || [ -z "$GOV_REG" ]; then
        GOV_READ_ERR="could not parse the availability response"
        return 1
    fi
    return 0
}

if [ -f "$DEVENV" ] && grep -q '^CONFIGURED_PROFILES=.*awsgov' "$DEVENV" 2>/dev/null; then
    GOV_REGION="${AWS_REGION:-us-gov-east-1}"
    GOV_CLAUDE_JSON="$PROFILES_DIR/awsgov/claude.json"
    GOV_MODELS=()
    if [ -f "$GOV_CLAUDE_JSON" ]; then
        # Only ANTHROPIC_DEFAULT_*_MODEL values are model ids — the sibling
        # *_MODEL_NAME/*_MODEL_DESCRIPTION keys and ANTHROPIC_CUSTOM_MODEL_OPTION
        # are not. Values repeat (the haiku slot maps to sonnet), hence sort -u.
        mapfile -t GOV_MODELS < <(jq -r '.env // {} | to_entries[]
            | select(.key | test("^ANTHROPIC_DEFAULT_.*_MODEL$"))
            | .value' "$GOV_CLAUDE_JSON" 2>/dev/null | sort -u || true)
    fi
    if [ ${#GOV_MODELS[@]} -eq 0 ]; then
        echo "No Anthropic model ids found in $GOV_CLAUDE_JSON — skipping the GovCloud model access check."
    else
        echo "Checking GovCloud Anthropic model access in $GOV_REGION..."
        # Entries are "<model>|<reason>"; the reason is split off at the FIRST `|`
        # so an AWS message containing one survives intact.
        GOV_UNAVAILABLE=()
        for gov_model in "${GOV_MODELS[@]}"; do
            if ! read_model_availability "$gov_model" "$GOV_REGION"; then
                GOV_UNAVAILABLE+=("$gov_model|could not read status: $GOV_READ_ERR")
                continue
            fi

            # Already usable — say nothing per model.
            if [ "$GOV_ENT" = "AVAILABLE" ] && [ "$GOV_REG" = "AVAILABLE" ]; then
                continue
            fi

            # Agreement in place, region available: the entitlement is the only
            # missing piece, and it is the one gate this container can close.
            if [ "$GOV_AGR" = "AVAILABLE" ] && [ "$GOV_REG" = "AVAILABLE" ]; then
                # Announced before the call: the request plus its propagation poll
                # can take ~35s, and a silent stall here looks like a hang.
                echo "Requesting entitlement for $gov_model in $GOV_REGION..."
                if ! request_entitlement "${gov_model#us-gov.}" "$GOV_REGION"; then
                    GOV_UNAVAILABLE+=("$gov_model|entitlement request failed: $(echo "$ENTITLE_ERR" | tr '\n' ' ' | tr -s ' ')")
                    continue
                fi
                # The grant is not instant; poll for propagation, 5s x 6.
                GOV_WAITED=0
                while [ "$GOV_WAITED" -lt 30 ]; do
                    sleep 5
                    GOV_WAITED=$((GOV_WAITED + 5))
                    read_model_availability "$gov_model" "$GOV_REGION" || true
                    [ "$GOV_ENT" = "AVAILABLE" ] && break
                done
                if [ "$GOV_ENT" = "AVAILABLE" ]; then
                    echo -e "  \e[32m✓\e[0m enabled $gov_model in $GOV_REGION"
                elif [ -n "$GOV_READ_ERR" ]; then
                    GOV_UNAVAILABLE+=("$gov_model|entitlement requested, but it did not read back as AVAILABLE within 30s; last status read failed: $GOV_READ_ERR")
                else
                    GOV_UNAVAILABLE+=("$gov_model|entitlement requested, but it did not become AVAILABLE within 30s (entitlement=$GOV_ENT region=$GOV_REG)")
                fi
                continue
            fi

            # No agreement yet, or the region itself is unavailable — needs the
            # commercial account, so only report it.
            GOV_UNAVAILABLE+=("$gov_model|agreement=$GOV_AGR entitlement=$GOV_ENT region=$GOV_REG")
        done

        if [ ${#GOV_UNAVAILABLE[@]} -gt 0 ]; then
            echo ""
            echo -e "\e[33m┌──────────────────────────────────────────────────────────────┐\e[0m"
            echo -e "\e[33m│ ⚠️  Some GovCloud Anthropic models are NOT usable yet         │\e[0m"
            echo -e "\e[33m└──────────────────────────────────────────────────────────────┘\e[0m"
            echo ""
            echo "Still not usable by this GovCloud account in $GOV_REGION:"
            for gov_entry in "${GOV_UNAVAILABLE[@]}"; do
                echo "  • ${gov_entry%%|*}"
                # Reasons print in FULL, only re-wrapped: AWS puts the actionable
                # sentence last, so truncating hides the diagnosis.
                echo "${gov_entry#*|}" | fold -s -w 72 | sed 's/^/      /'
            done
            echo ""
            echo "The per-region entitlement is requested automatically for every model"
            echo "whose agreement is already in place. What is left needs the use-case"
            echo "form and/or a model agreement, and those can only be created from the"
            echo "LINKED COMMERCIAL account."
            # The interactive fixer is a maintenance script in .devcontainer/scripts/, not
            # part of this feature, so it can be absent when the feature travels alone.
            # Point at it only when it is actually there; the gates it closes are the same
            # ones the Bedrock console walks through either way.
            GOV_FIXER="$DEVCONTAINER_DIR/scripts/enable-govcloud-models.sh"
            if [ -f "$GOV_FIXER" ]; then
                echo "To work through it:"
                echo ""
                echo -e "  \e[1m.devcontainer/scripts/enable-govcloud-models.sh\e[0m"
                echo ""
                echo "It reports what is missing and walks through enabling it, including the"
                echo "use-case form the Bedrock console would otherwise ask you to fill in."
                echo "It tries this with the container's own credentials first; only if AWS"
                echo "refuses does it ask for short-term credentials for the LINKED COMMERCIAL"
                echo "account (e.g. exported from Kion)."
            else
                echo "Work through it in the Bedrock console of the linked commercial account:"
                echo ""
                echo "  https://docs.aws.amazon.com/bedrock/latest/userguide/model-access.html"
            fi
            echo ""
        else
            echo "GovCloud Anthropic model access confirmed."
        fi
    fi
fi
