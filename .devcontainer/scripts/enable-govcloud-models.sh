#!/bin/bash
# Enable the GovCloud Bedrock Anthropic models for this container (interactive).
#
# Usage: .devcontainer/scripts/enable-govcloud-models.sh
#
# Three gates stand between a fresh GovCloud account and an Anthropic model, each
# reported by its own flag from get-foundation-model-availability:
#
#   1. Use-case form (account-level)   — put-use-case-for-model-access.
#   2. agreementAvailability           — a GovCloud account CANNOT create one ("You
#      must use your associated standard AWS account..."). Created from the LINKED
#      COMMERCIAL account with the BARE model id; one agreement per model covers
#      both gov regions (measured AVAILABLE in both within ~50 s).
#   3. entitlementAvailability (per region) — no documented API, but the console
#      calls POST https://bedrock.<region>.amazonaws.com/foundation-model-entitlement
#      with {"modelId":"<bare id>"}, SigV4 service "bedrock". Verified in GovCloud:
#      201 {"status":"SUCCESS"}, flag AVAILABLE ~5 s later, idempotent. Untested
#      without an agreement, so it always runs after gate 2; the console page is
#      the fallback if the request fails.
#      https://dev.to/aws-builders/automating-new-bedrock-foundation-model-access-3ec8
#
# authorizationStatus is never consulted: it is account-level and reads AUTHORIZED
# even while a region's entitlement is missing. The account's data retention mode
# (needed by e.g. Fable) is set by postcreate.sh, not here.
#
# The agreement is tried with the container's own GovCloud credentials first and
# only escalates to a pasted commercial key when AWS refuses. Safe to re-run: gates
# already met are skipped. No `set -euo pipefail` — the script prompts and branches
# on failures (like setup.sh).

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DEVCONTAINER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROFILES_DIR="$DEVCONTAINER_DIR/profiles"
COMM_REGION=us-east-1   # commercial endpoint for agreements (us-west-2 works too)
CONSOLE_URL_BASE="https://console.amazonaws-us-gov.com/bedrock/home"
AGREEMENT_POLL_SECONDS=120
ENTITLE_POLL_SECONDS=30
POLL_INTERVAL=5

# Use-case form answers (gate 1). These describe the ORGANISATION, not the project,
# so they are the same for every container this repo produces and are constants here
# rather than prompts. Shape verified by reading a console-submitted form back with
# get-use-case-for-model-access — see the use-case form section below.
USE_CASE_COMPANY="Software Engineering Institute"
USE_CASE_WEBSITE="https://sei.cmu.edu"
USE_CASE_INDUSTRY="Government"    # console-defined; the list is longer than it looks
USE_CASE_OTHER_INDUSTRY=""        # free text, meaningful only when industry is Other
USE_CASE_INTENDED_USERS="0"       # enum: 0=internal only, 1=external only, 2=both
USE_CASE_REASON="Cyber, AI, and software development project work on behalf of the U.S. government"

# --- Helpers ---

hr() { echo "────────────────────────────────────────────────────────────────"; }

# row ok|warn|bad <label> <text>
row() {
    local mark
    case "$1" in
        ok)   mark='\e[32m✓\e[0m' ;;
        warn) mark='\e[33m!\e[0m' ;;
        *)    mark='\e[31m✗\e[0m' ;;
    esac
    printf "  $mark %-42s %s\n" "$2" "$3"
}

# Print an API error in full, wrapped. Never truncate: AWS puts the actionable
# sentence last.
print_api_error() {
    local msg
    msg=$(echo "$1" | tr '\n' ' ' | tr -s ' ')
    msg="${msg#"${msg%%[![:space:]]*}"}"
    echo "$msg" | fold -s -w 72 | sed 's/^/      /'
}

# Gov ids are `us-gov.`-prefixed; agreements and entitlements use the bare id.
bare_model_id() { echo "${1#us-gov.}"; }

# --- Use-case form (gate 1) ---
#
# Same six keys the console submits, every value a string — the shape AWS publishes
# at API_PutUseCaseForModelAccess.html. Two of them are easy to get wrong:
#
#   intendedUsers is an ENUM, not a head count: "0" internal only, "1" external
#   only, "2" both (validated against exactly those three in aws-samples'
#   enable_anthropic_models.py). The console asks it as internal/external choices,
#   so prompting for a *number of users* — as this script once did — answered a
#   question the form never asks.
#
#   useCases is a composite the console builds as "<selected checkbox labels>. <free
#   text>", which is why a console-submitted form reads ". Research for government
#   study" with a bare leading dot. We send the description alone: the prefix is the
#   console's own artifact and this string is read by a human reviewer.
#
# Encoding is ASYMMETRIC: put takes base64(json), get returns base64(base64(json)).
# Feeding get's value back to put is rejected — decode twice, encode once.
#
# The form is WRITE-ONCE. A put against an account that already has one returns
# SUCCESS and changes nothing — verified live in both gov regions, so a re-put is not
# a way to correct a form on file. Hence: get first, and skip the step when it
# answers. The account keeps whatever it was first given.

get_use_case_json() {
    local wire json
    wire=$(aws bedrock get-use-case-for-model-access --region "$GOV_REGION" \
        --output text --query formData 2>/dev/null)
    [ -n "$wire" ] && [ "$wire" != "None" ] || return 1
    json=$(printf '%s' "$wire" | base64 -d 2>/dev/null | base64 -d 2>/dev/null)
    echo "$json" | jq -e '.companyName' >/dev/null 2>&1 || return 1
    printf '%s' "$json"
}

put_use_case_json() {
    local err
    err=$(aws bedrock put-use-case-for-model-access --region "$GOV_REGION" \
        --cli-binary-format base64 --form-data "$(printf '%s' "$1" | base64 -w0)" 2>&1) \
        && return 0
    print_api_error "$err"
    return 1
}

# Assemble the form from the constants above. jq -cn --arg escapes quotes and
# backslashes and keeps any $(...) in the free text literal.
build_use_case_json() {
    jq -cn --arg n "$USE_CASE_COMPANY" --arg w "$USE_CASE_WEBSITE" \
        --arg u "$USE_CASE_INTENDED_USERS" --arg i "$USE_CASE_INDUSTRY" \
        --arg o "$USE_CASE_OTHER_INDUSTRY" --arg c "$USE_CASE_REASON" \
        '{companyName:$n, companyWebsite:$w, intendedUsers:$u,
          industryOption:$i, otherIndustryOption:$o, useCases:$c}'
}

# --- Status ---

# Echo "<agreement>|<entitlement>|<region>" for one model, or "ERROR|<message>".
model_status() {
    local json
    json=$(aws bedrock get-foundation-model-availability \
        --model-id "$1" --region "$GOV_REGION" 2>&1) \
        || { echo "ERROR|$(echo "$json" | tr '\n' ' ' | tr -s ' ')"; return 1; }
    echo "$json" | jq -r '[.agreementAvailability.status // "UNKNOWN",
        .entitlementAvailability // "UNKNOWN", .regionAvailability // "UNKNOWN"] | join("|")'
}

# Print a row per model and sort the failures by remedy:
#   NEEDS_AGREEMENT   — bare ids with no agreement (gate 2, commercial account)
#   NEEDS_ENTITLEMENT — gov ids with an agreement but no entitlement (gate 3)
# A PENDING agreement is neither — it is propagating. Returns 0 when all are usable.
check_all() {
    local model agr ent reg
    USABLE_COUNT=0
    STATUS_ERRORS=0
    NEEDS_AGREEMENT=()
    NEEDS_ENTITLEMENT=()
    for model in "${GOV_MODELS[@]}"; do
        IFS='|' read -r agr ent reg <<< "$(model_status "$model")"
        if [ "$agr" = "ERROR" ]; then
            STATUS_ERRORS=$((STATUS_ERRORS + 1))
            row bad "$model" "could not read status"
            print_api_error "$ent"
        elif [ "$ent" = "AVAILABLE" ] && [ "$reg" = "AVAILABLE" ]; then
            USABLE_COUNT=$((USABLE_COUNT + 1))
            row ok "$model" "usable"
        elif [ "$agr" = "AVAILABLE" ]; then
            NEEDS_ENTITLEMENT+=("$model")
            row bad "$model" "needs entitlement (entitlement=$ent)"
        elif [ "$agr" = "PENDING" ]; then
            row warn "$model" "agreement PENDING (propagating)"
        else
            NEEDS_AGREEMENT+=("$(bare_model_id "$model")")
            row bad "$model" "no agreement (agreement=$agr entitlement=$ent region=$reg)"
        fi
    done
    [ "$USABLE_COUNT" -eq "${#GOV_MODELS[@]}" ]
}

# Poll one model until <agreement|entitlement> reads AVAILABLE, up to <seconds>.
wait_for_flag() {
    local model="$1" field="$2" limit="$3" waited=0 agr ent reg val
    while :; do
        IFS='|' read -r agr ent reg <<< "$(model_status "$model")"
        if [ "$agr" = "ERROR" ]; then
            row bad "$model" "could not read status"
            print_api_error "$ent"
            return 1
        fi
        [ "$field" = agreement ] && val="$agr" || val="$ent"
        if [ "$val" = "AVAILABLE" ]; then
            row ok "$model" "$field AVAILABLE (after ${waited}s)"
            return 0
        fi
        [ "$waited" -ge "$limit" ] && break
        sleep "$POLL_INTERVAL"
        waited=$((waited + POLL_INTERVAL))
    done
    row warn "$model" "$field still $val after ${waited}s"
    return 1
}

# --- Agreement (gate 2) ---

# Create the agreement for one model id in one region with whatever credentials are
# in the environment. "Agreement already exists" is success. Sets
# CREATE_AGREEMENT_ERR on failure so the caller can recognise a refusal.
create_agreement() {
    local model="$1" region="$2" out token
    CREATE_AGREEMENT_ERR=""
    out=$(aws bedrock list-foundation-model-agreement-offers \
        --model-id "$model" --region "$region" 2>&1)
    if [ $? -eq 0 ]; then
        token=$(echo "$out" | jq -r '.offers[0].offerToken // empty')
        if [ -z "$token" ]; then
            row bad "$model" "no offer returned"
            return 1
        fi
        out=$(aws bedrock create-foundation-model-agreement \
            --model-id "$model" --offer-token "$token" --region "$region" 2>&1) \
            && { row ok "$model" "agreement created in $region"; return 0; }
    fi
    case "$out" in
        *[Aa]lready\ exists*) row ok "$model" "agreement already in place"; return 0 ;;
    esac
    row bad "$model" "agreement not created in $region"
    print_api_error "$out"
    CREATE_AGREEMENT_ERR="$out"
    return 1
}

# --- Entitlement (gate 3) ---

# POST the undocumented entitlement request for one BARE model id in GOV_REGION.
# curl signs it (--aws-sigv4); credentials come from `aws configure
# export-credentials` and reach curl only on stdin (-K -), never on argv. Returns 0
# on any 2xx, else sets ENTITLE_ERR to the status and the full body.
request_entitlement() {
    local model="$1" creds key secret token token_hdr="" resp code
    ENTITLE_ERR=""
    creds=$(aws configure export-credentials --format process 2>&1) \
        || { ENTITLE_ERR="could not resolve AWS credentials: $creds"; return 1; }
    key=$(echo "$creds" | jq -r '.AccessKeyId // empty')
    secret=$(echo "$creds" | jq -r '.SecretAccessKey // empty')
    token=$(echo "$creds" | jq -r '.SessionToken // empty')
    if [ -z "$key" ] || [ -z "$secret" ]; then
        ENTITLE_ERR="aws configure export-credentials returned no access key"
        return 1
    fi
    # Built as a whole line: quote removal applies inside ${var:+...} even in a
    # heredoc. A blank config line is ignored (long-term keys have no token).
    [ -n "$token" ] && token_hdr="header = \"x-amz-security-token: $token\""
    resp=$(curl -sS -w '\n%{http_code}' -K - 2>&1 <<EOF
url = "https://bedrock.$GOV_REGION.amazonaws.com/foundation-model-entitlement"
request = "POST"
aws-sigv4 = "aws:amz:$GOV_REGION:bedrock"
user = "$key:$secret"
$token_hdr
header = "Content-Type: application/x-amz-json-1.1"
data = "{\"modelId\":\"$model\"}"
EOF
    )
    unset creds key secret token token_hdr
    code="${resp##*$'\n'}"
    case "$code" in 2*) return 0 ;; esac
    ENTITLE_ERR="HTTP $code ${resp%$'\n'*}"
    return 1
}

# --- Guards and configuration ---

if [ ! -d "$PROFILES_DIR/awsgov" ]; then
    echo ""
    echo "The awsgov profile is not configured in this container. Nothing to do."
    echo ""
    exit 0
fi
for tool in aws jq curl; do
    if ! command -v "$tool" &>/dev/null; then
        echo "ERROR: $tool is not installed. Run this inside the dev container." >&2
        exit 1
    fi
done

# Models: the ids pinned in the profile's Claude Code fragment — the same file
# postcreate.sh probes. `_MODEL$` skips the *_NAME/*_DESCRIPTION labels.
mapfile -t GOV_MODELS < <(jq -r '.env // {} | to_entries
    | map(select(.key | test("^ANTHROPIC_DEFAULT_.*_MODEL$")) | .value)
    | unique | .[]' "$PROFILES_DIR/awsgov/claude.json" 2>/dev/null)
if [ "${#GOV_MODELS[@]}" -eq 0 ]; then
    echo "ERROR: no ANTHROPIC_DEFAULT_*_MODEL entries in $PROFILES_DIR/awsgov/claude.json." >&2
    exit 1
fi

# Region: the profile's AWS_REGION — the only one these models are invoked from.
GOV_REGION=$(grep -m1 '^AWS_REGION=' "$PROFILES_DIR/awsgov/profile.env" 2>/dev/null \
    | cut -d= -f2- | tr -d '\r')
GOV_REGION="${GOV_REGION:-us-gov-east-1}"

# Prompts read /dev/tty so a credential block pasted on stdin is never taken as an
# answer. Test by opening it — `[ -r /dev/tty ]` is true even without a terminal.
if : 2>/dev/null < /dev/tty; then HAVE_TTY=1; else HAVE_TTY=0; fi

# --- Current status ---

echo ""
hr
echo -e " \e[1mGovCloud Bedrock model access\e[0m"
hr
echo ""
echo "Checking model availability in $GOV_REGION..."
echo ""
if check_all; then
    echo ""
    echo -e "\e[32mAll ${#GOV_MODELS[@]} models are already usable. Nothing to do.\e[0m"
    echo ""
    exit 0
fi
echo ""
if [ "$STATUS_ERRORS" -gt 0 ]; then
    # Usually expired or missing credentials; nothing below could succeed either.
    echo "Could not read model status — fix the error above and re-run." >&2
    echo ""
    exit 1
fi
echo "$USABLE_COUNT of ${#GOV_MODELS[@]} models usable — continuing."
echo ""
TO_AGREE=("${NEEDS_AGREEMENT[@]}")
TO_ENTITLE=("${NEEDS_ENTITLEMENT[@]}")
AGREED_MODELS=()
ENTITLE_FAILED=()

# --- Step 1 of 3: use-case form ---
# Account-level, submitted once with the GovCloud account's own credentials. The
# JSON is always shown and confirmed first — it is an attestation under the user's
# name.

hr
if GOV_USE_CASE=$(get_use_case_json); then
    echo -e " \e[1mStep 1 of 3\e[0m — use-case form: \e[32malready on file\e[0m"
    hr
    echo ""
    echo "$GOV_USE_CASE" | jq .
    echo ""
else
    echo -e " \e[1mStep 1 of 3\e[0m — use-case form"
    hr
    echo ""
    echo "Anthropic models require a use-case form (the one the Bedrock console asks"
    echo "for). This GovCloud account has none on file yet, so the organisation-level"
    echo "answers below are filled in for you — they are the same for every project."
    if [ "$HAVE_TTY" -eq 0 ]; then
        echo ""
        echo "ERROR: confirming the attestation needs a terminal. Re-run from an" >&2
        echo "interactive shell." >&2
        echo ""
        exit 1
    fi
    FORM_JSON=$(build_use_case_json) || exit 1
    echo ""
    echo "This will be submitted to the GovCloud account as your attestation:"
    echo ""
    echo "$FORM_JSON" | jq .
    echo ""
    read -r -p "Submit this form? [y/N] " CONFIRM < /dev/tty
    case "$CONFIRM" in
        [Yy]*) ;;
        *)
            echo ""
            echo "Not submitted. Re-run this script when ready."
            echo ""
            exit 1
            ;;
    esac
    echo ""
    if put_use_case_json "$FORM_JSON"; then
        row ok "use-case form" "submitted"
    else
        echo ""
        echo "ERROR: the form was rejected. Submit it from the Bedrock console instead:" >&2
        echo "  $CONSOLE_URL_BASE?region=$GOV_REGION#/modelaccess" >&2
        echo ""
        exit 1
    fi
    echo ""
fi

# --- Step 2 of 3: model access agreement ---
# Tried with the container's own GovCloud credentials first; only an authorization
# refusal escalates to a pasted commercial key.

hr
echo -e " \e[1mStep 2 of 3\e[0m — model access agreement"
hr
echo ""
if [ "${#TO_AGREE[@]}" -eq 0 ]; then
    echo -e "\e[32mNothing to do: every model already has an agreement.\e[0m"
    echo ""
else
    echo "Trying with the container's own GovCloud credentials first..."
    echo ""
    GOV_DENIED=0
    for model in "${TO_AGREE[@]}"; do
        if create_agreement "us-gov.$model" "$GOV_REGION"; then
            AGREED_MODELS+=("$model")
        else
            case "$CREATE_AGREEMENT_ERR" in
                # Account-level refusal — every remaining model would say the same.
                *AccessDenied*|*UnrecognizedClient*|*not\ authorized*|*cannot\ perform*)
                    GOV_DENIED=1
                    break ;;
            esac
        fi
    done
    echo ""

    if [ "$GOV_DENIED" -eq 0 ]; then
        if [ "${#AGREED_MODELS[@]}" -gt 0 ]; then
            echo -e "\e[32mThe GovCloud account handled it — no commercial credentials needed.\e[0m"
        else
            echo "AWS did not refuse on authorization grounds, so a commercial key would not"
            echo "change the result above — no credentials requested."
        fi
        echo ""
    else
        # The pasted block is PARSED with a regex allowlist — never eval'd or
        # sourced. The values are used only inside subshells, never exported here,
        # never written to a file.
        echo -e "\e[33mThe GovCloud account was refused, so this needs the linked commercial\e[0m"
        echo -e "\e[33maccount (short-term credentials, e.g. exported from Kion).\e[0m"
        echo ""
        echo "Paste the export block below — it continues as soon as all three arrive:"
        echo ""
        echo "  export AWS_ACCESS_KEY_ID=ASIA..."
        echo "  export AWS_SECRET_ACCESS_KEY=..."
        echo "  export AWS_SESSION_TOKEN=..."
        echo ""
        echo -e "\e[2m(Used only for this step, and never saved.)\e[0m"
        echo ""
        COMM_KEY=""; COMM_SECRET=""; COMM_TOKEN=""
        while IFS= read -r line || [ -n "$line" ]; do
            [ -z "${line//[[:space:]]/}" ] && break
            line="${line//$'\r'/}"
            line="${line#"${line%%[![:space:]]*}"}"
            line="${line#export }"
            if [[ "$line" =~ ^AWS_ACCESS_KEY_ID=[\"\']?([^\"\'[:space:]]+)[\"\']?$ ]]; then
                COMM_KEY="${BASH_REMATCH[1]}"
            elif [[ "$line" =~ ^AWS_SECRET_ACCESS_KEY=[\"\']?([^\"\'[:space:]]+)[\"\']?$ ]]; then
                COMM_SECRET="${BASH_REMATCH[1]}"
            elif [[ "$line" =~ ^AWS_SESSION_TOKEN=[\"\']?([^\"\'[:space:]]+)[\"\']?$ ]]; then
                COMM_TOKEN="${BASH_REMATCH[1]}"
            fi
            # Finish on the paste's own final Enter instead of asking for another
            # blank line: with all three values in, there is nothing left to wait
            # for. Anything still buffered behind them (a region export, a comment,
            # a stray newline) is drained here so it cannot be picked up by the
            # shell prompt once this script exits. The blank-line break above stays
            # as the escape hatch for an incomplete paste.
            if [ -n "$COMM_KEY" ] && [ -n "$COMM_SECRET" ] && [ -n "$COMM_TOKEN" ]; then
                while read -r -t 0.1 _; do :; done
                break
            fi
        done
        if [ -z "$COMM_KEY" ] || [ -z "$COMM_SECRET" ] || [ -z "$COMM_TOKEN" ]; then
            echo ""
            if [ "$HAVE_TTY" -eq 0 ]; then
                echo "ERROR: pasting credentials needs a terminal. Re-run from an interactive shell." >&2
            else
                echo "ERROR: could not read AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY and" >&2
                echo "AWS_SESSION_TOKEN from the paste." >&2
            fi
            echo ""
            exit 1
        fi

        echo ""
        echo "Verifying commercial credentials..."
        COMM_IDENTITY=$(env AWS_ACCESS_KEY_ID="$COMM_KEY" AWS_SECRET_ACCESS_KEY="$COMM_SECRET" \
            AWS_SESSION_TOKEN="$COMM_TOKEN" AWS_REGION="$COMM_REGION" \
            aws sts get-caller-identity --output json 2>&1)
        if [ $? -ne 0 ]; then
            echo ""
            echo "ERROR: AWS STS rejected those credentials:" >&2
            echo "$COMM_IDENTITY" >&2
            echo ""
            exit 1
        fi
        echo "  Account: $(echo "$COMM_IDENTITY" | jq -r '.Account')"
        echo "  Caller:  $(echo "$COMM_IDENTITY" | jq -r '.Arn')"
        echo ""
        echo "Creating agreements in $COMM_REGION (one per model covers both gov regions)."
        echo ""
        for model in "${TO_AGREE[@]}"; do
            # The subshell scopes the commercial credentials to this one call.
            if ( export AWS_ACCESS_KEY_ID="$COMM_KEY" AWS_SECRET_ACCESS_KEY="$COMM_SECRET" \
                        AWS_SESSION_TOKEN="$COMM_TOKEN" AWS_REGION="$COMM_REGION"
                 create_agreement "$model" "$COMM_REGION" ); then
                AGREED_MODELS+=("$model")
            fi
        done
        COMM_KEY=""; COMM_SECRET=""; COMM_TOKEN=""
        echo ""
    fi

    # Agreements take 3–50 s to appear in the gov region; wait so step 3 can finish
    # the job in this run.
    if [ "${#AGREED_MODELS[@]}" -gt 0 ]; then
        echo "Waiting for the agreement(s) to propagate to $GOV_REGION (up to ${AGREEMENT_POLL_SECONDS}s)..."
        echo ""
        for model in "${AGREED_MODELS[@]}"; do
            wait_for_flag "us-gov.$model" agreement "$AGREEMENT_POLL_SECONDS"
        done
        echo ""
    fi
fi

# --- Step 3 of 3: per-region entitlement ---
# No prompt and not gated on HAVE_TTY: the consent was the agreement in step 2, and
# the request is idempotent.

hr
echo -e " \e[1mStep 3 of 3\e[0m — per-region entitlement"
hr
echo ""
if [ "${#AGREED_MODELS[@]}" -gt 0 ]; then
    # Re-probe so models agreed in this run are included. Only agreement=AVAILABLE
    # models land in NEEDS_ENTITLEMENT, so nothing still PENDING is requested.
    echo "Re-reading availability:"
    echo ""
    check_all
    echo ""
    TO_ENTITLE=("${NEEDS_ENTITLEMENT[@]}")
fi
if [ "${#TO_ENTITLE[@]}" -eq 0 ]; then
    echo -e "\e[32mNothing to do: no model is waiting on an entitlement.\e[0m"
    echo ""
else
    echo "Requesting entitlements in $GOV_REGION with the container's own credentials..."
    echo ""
    for model in "${TO_ENTITLE[@]}"; do
        if request_entitlement "$(bare_model_id "$model")"; then
            row ok "$model" "entitlement requested"
            wait_for_flag "$model" entitlement "$ENTITLE_POLL_SECONDS"
        else
            ENTITLE_FAILED+=("$model")
            row bad "$model" "entitlement request failed"
            print_api_error "$ENTITLE_ERR"
        fi
    done
    echo ""
fi

# --- Final status ---

hr
echo -e " \e[1mFinal status\e[0m"
hr
echo ""
if check_all; then
    echo ""
    echo -e "\e[32mAll ${#GOV_MODELS[@]} models are usable.\e[0m"
    echo ""
    echo "Restart your agent (claude, codex, opencode, pi, grok) to pick this up."
    echo ""
    exit 0
fi
echo ""
echo -e "\e[33m$USABLE_COUNT of ${#GOV_MODELS[@]} models usable.\e[0m"
echo ""
if [ "${#ENTITLE_FAILED[@]}" -gt 0 ]; then
    echo -e "\e[1mEntitlement request failed:\e[0m ${ENTITLE_FAILED[*]}"
    echo ""
    echo "See the error above. Fallback: enable these models on this region's own Model"
    echo "access page (each gov region has its own), then re-run this script:"
    echo ""
    echo "  $CONSOLE_URL_BASE?region=$GOV_REGION#/modelaccess"
    echo "  Bedrock > Model access > Modify model access > select the models > Submit"
    echo ""
fi
for model in "${NEEDS_ENTITLEMENT[@]}"; do
    case " ${ENTITLE_FAILED[*]} " in
        *" $model "*) ;;
        *) echo "$model: entitlement requested but not AVAILABLE yet — re-run in a minute to confirm." ;;
    esac
done
if [ "${#NEEDS_AGREEMENT[@]}" -gt 0 ]; then
    echo -e "\e[1mAgreement still missing:\e[0m ${NEEDS_AGREEMENT[*]}"
    echo ""
    echo "If step 2 printed an error, that is the reason and re-running will not change"
    echo "it. Otherwise the agreement is still propagating — re-run in a minute."
    echo ""
fi
echo "Re-check at any time: .devcontainer/scripts/enable-govcloud-models.sh"
echo ""
exit 1
