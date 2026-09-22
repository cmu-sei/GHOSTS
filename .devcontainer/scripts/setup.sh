#!/bin/bash
# Dev Container Setup Script for macOS/Linux
#
# Usage:
#   ./setup.sh              First run: CUI data question, profile selection, credentials
#                           Re-run:   Prompts for any credential still blank, otherwise
#                                     shows configured profiles and per-profile commands
#   ./setup.sh <profile>    Update credentials, or add a new profile (e.g., aws, opal, etc)
#   ./setup.sh --spawning   Pre-container run from spawn.sh: CUI data question, profile
#              [--chat]     selection, feature selection, credentials (skippable) and
#              [--git]      Git init — everything, so the container create asks nothing
#   ./setup.sh --ensure     What initializeCommand runs: a silent no-op once
#                           devcontainer.env exists, and the first-run flow when it
#                           doesn't (a project that adopted .devcontainer/ without a spawn)
#
# Normally invoked automatically by .devcontainer/scripts/init via initializeCommand.
# Run manually only to update credentials in an existing devcontainer.env.

# $0 may be the `setup-devcontainer` symlink on PATH rather than this file, and every path
# below is derived from it — an unresolved link puts PROFILES_DIR in ~/.local/profiles.
# Resolved link by link with bare `readlink`: this also runs on the host, where `readlink -f`
# and `realpath` are absent on older macOS. A relative target resolves against the directory
# of the link that named it, which is how the kernel reads it.
SCRIPT_PATH="$0"
while [ -L "$SCRIPT_PATH" ]; do
    LINK_DIR="$(cd "$(dirname "$SCRIPT_PATH")" && pwd)"
    SCRIPT_PATH="$(readlink "$SCRIPT_PATH")"
    case "$SCRIPT_PATH" in
        /*) ;;
        *) SCRIPT_PATH="$LINK_DIR/$SCRIPT_PATH" ;;
    esac
done
SCRIPT_DIR="$(cd "$(dirname "$SCRIPT_PATH")" && pwd)"
# Sibling resources live one level up, in .devcontainer/ itself.
DEVCONTAINER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROFILES_DIR="$DEVCONTAINER_DIR/profiles"
DEVENV="$DEVCONTAINER_DIR/devcontainer.env"
DC_JSON="$DEVCONTAINER_DIR/devcontainer.json"

PROJECT_DIR="$(dirname "$DEVCONTAINER_DIR")"
PROJECT_NAME="$(basename "$PROJECT_DIR")"

# Marks a checkout of agent-dev ITSELF rather than a project built from it. Tracked in
# agent-dev's root and deleted by spawn's clean slate, so it exists in exactly one place:
# a clone of the template repo. Its only effect is to make choose_cui skip the profile
# deletion — see there for why a maintainer needs that and why nothing else keys off it.
TEMPLATE_MARKER="$PROJECT_DIR/.template-container"

# --- Argument parsing ---
# Flags are pulled out first so the remaining positionals stay exactly what update
# mode has always expected: a list of profile names.
SPAWNING=false
CHAT=false
GIT=false
ENSURE=false
POSITIONAL=()
while [ $# -gt 0 ]; do
    case "$1" in
        --spawning)
            SPAWNING=true
            ;;
        --chat)
            CHAT=true
            ;;
        --git)
            GIT=true
            ;;
        --ensure)
            ENSURE=true
            ;;
        -*)
            echo "Unknown option: $1"
            exit 1
            ;;
        *)
            POSITIONAL+=("$1")
            ;;
    esac
    shift
done
set -- "${POSITIONAL[@]}"

# Checked here rather than in the spawn branch below: update mode is keyed on there
# being positionals and comes first, so `--spawning aws` would be swallowed by it.
if [ "$SPAWNING" = true ] && [ $# -gt 0 ]; then
    echo "ERROR: --spawning takes no profile arguments."
    exit 1
fi

# --- Banner ---
# Printed at most once, and only when this run is actually going to say something:
# --ensure runs on every container start and is silent when there is nothing to
# finish, so it defers the banner to the first real prompt. SETUP_NO_BANNER is set
# for the re-exec into update mode, where it is already on screen.
BANNER_SHOWN="${SETUP_NO_BANNER:-}"
show_banner() {
    [ -n "$BANNER_SHOWN" ] && return 0
    BANNER_SHOWN=1
    local header="${PROJECT_NAME} Dev Container Setup" border="" i
    for ((i = 0; i < ${#header} + 2; i++)); do border+="─"; done
    echo ""
    echo "┌${border}┐"
    echo -e "│ \033[1;38;5;141m${PROJECT_NAME}\033[0m Dev Container Setup │"
    echo "└${border}┘"
}
[ "$ENSURE" = true ] || show_banner

# --- Helper: is there a terminal to prompt on? ---
# Every prompt in this script reads from /dev/tty, so the probe has to be an actual
# open: `[ -r /dev/tty ]` answers with access(2) on the device node, which succeeds
# on permissions alone and says "yes" even when the process has no controlling
# terminal — and then the first read blocks forever instead of failing.
have_tty() {
    (exec 3< /dev/tty) 2>/dev/null
}

# --- Helper: masked input with asterisk feedback ---
# All display goes to /dev/tty; only the result is printed to stdout.
read_masked() {
    local prompt="$1" result=""
    echo -n "$prompt" > /dev/tty
    while IFS= read -r -s -n1 char < /dev/tty; do
        if [[ -z "$char" ]]; then
            break
        elif [[ "$char" == $'\x7f' || "$char" == $'\x08' ]]; then
            if [[ -n "$result" ]]; then
                result="${result%?}"
                echo -ne '\b \b' > /dev/tty
            fi
        else
            result+="$char"
            echo -n '*' > /dev/tty
        fi
    done
    echo "" > /dev/tty
    printf '%s' "$result"
}

# --- Helper: prompt for a profile's empty env vars ---
# Reads the profile template, prompts for vars with empty values,
# and prints VAR=VALUE lines for the prompted vars to stdout.
#
# $2 = "optional" (spawn mode only): an empty answer SKIPS the var instead of being an
# error, so someone who hasn't generated a key yet can still finish the spawn. The var
# is then simply not printed, write_devenv leaves it blank, and `setup.sh <profile>`
# fills it in later. Everywhere else a blank answer is still a hard error — update mode
# would otherwise patch a real value away.
prompt_credentials() {
    local profile="$1" optional="${2:-}"
    local env_file="$PROFILES_DIR/$profile/profile.env"

    if [ ! -f "$env_file" ]; then
        echo "ERROR: Profile '$profile' not found." > /dev/tty
        return 1
    fi

    echo "" > /dev/tty
    echo "  $profile" > /dev/tty
    echo "" > /dev/tty

    while IFS= read -r line || [ -n "$line" ]; do
        [[ "$line" =~ ^[[:space:]]*# ]] && continue
        [[ -z "$line" ]] && continue

        if [[ "$line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=$ ]]; then
            local var_name="${BASH_REMATCH[1]}"
            local value=""

            if [[ "$var_name" == *SECRET* || "$var_name" == *_KEY ]]; then
                value=$(read_masked "$var_name: ")
            else
                read -p "$var_name: " value < /dev/tty
            fi

            if [ -z "$value" ]; then
                if [ "$optional" = optional ]; then
                    continue
                fi
                echo "ERROR: $var_name is required." > /dev/tty
                return 1
            fi

            echo "$var_name=$value"
        fi
    done < "$env_file"
}

# --- Helper: collect every selected profile's credentials --- (sets CRED_KEYS/CRED_VALS)
# Shared by spawn mode and the adopted-project first run; write_devenv consumes the two
# arrays. Values are deduplicated across profiles (aws and awsgov share AWS_* names).
# $1 = "optional" is passed straight through to prompt_credentials.
collect_credentials() {
    local optional="${1:-}" profile result cred_line key val k found

    CRED_KEYS=()
    CRED_VALS=()

    echo ""
    echo "  Configure Credentials"
    if [ "$optional" = optional ]; then
        echo ""
        echo "  Press Enter to skip a key you haven't generated yet — you can add it"
        echo "  later with .devcontainer/scripts/setup.sh <profile>."
    fi

    for profile in "${SELECTED[@]}"; do
        result=$(prompt_credentials "$profile" "$optional") || exit 1

        while IFS= read -r cred_line; do
            [ -z "$cred_line" ] && continue
            if [[ "$cred_line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=(.*)$ ]]; then
                key="${BASH_REMATCH[1]}"
                val="${BASH_REMATCH[2]}"
                found=0
                for k in "${CRED_KEYS[@]}"; do
                    if [ "$k" = "$key" ]; then found=1; break; fi
                done
                if [ "$found" -eq 0 ]; then
                    CRED_KEYS+=("$key")
                    CRED_VALS+=("$val")
                fi
            fi
        done <<< "$result"
    done
}

# --- Helper: which configured profiles still have a blank credential? ---
# Reads the generated devcontainer.env, not the collected arrays, so it is equally valid
# right after a write and on a later run. A profile is pending when profile.env declares
# a var with no value (that is what marks a credential) and the env file still has none.
# Prints one profile name per line.
pending_profiles() {
    local profile line
    for profile in "${SELECTED[@]}"; do
        [ -f "$PROFILES_DIR/$profile/profile.env" ] || continue
        while IFS= read -r line || [ -n "$line" ]; do
            # Strip CR: profile.env is LF via .gitattributes, but a Windows editor may
            # still have touched it, and a trailing \r would defeat the anchored match
            # and hide a pending credential.
            line="${line//$'\r'/}"
            if [[ "$line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=$ ]]; then
                # [[:space:]] covers the \r a Windows-written env file leaves.
                if grep -qE "^${BASH_REMATCH[1]}=[[:space:]]*$" "$DEVENV"; then
                    echo "$profile"
                    break
                fi
            fi
        done < "$PROFILES_DIR/$profile/profile.env"
    done
}

# --- Helper: turn a local feature line in devcontainer.json on or off ---
# Features are resolved and installed at IMAGE BUILD, and devcontainer.env is a
# --env-file (run time only), so nothing in it can gate an install. Editing
# devcontainer.json on the host before the build is the only lever there is —
# which is why this is reached from --spawning, before the container exists.
#
# Commented rather than deleted so the line stays discoverable: turning a feature
# back on is uncomment + rebuild, with no need to remember the exact key or where
# it went. Comments are legal in devcontainer.json (the CLI and VS Code both parse
# it as JSONC).
#
# The commas do NOT take care of themselves, though: commenting out the LAST entry
# of the features block leaves the one above it with a trailing comma. Today no
# toggleable feature happens to be last, so nothing shows it — reorder the block
# and it breaks. normalize_feature_commas() below fixes up the block after every
# toggle so the result never depends on that accident.
#
# Line-based (not jq) so this works on hosts without jq — and jq would strip the
# file's existing comments anyway. BRE with \(...\) and [[:space:]] behaves the same
# on BSD (macOS) and GNU sed. Both directions are idempotent: an already-commented
# line has / where "off" expects a quote, and vice versa.
#
# Redirect to a temp + mv rather than `sed -i`: this runs on the HOST, and BSD sed
# requires an argument after -i, so `sed -i "SCRIPT" file` silently mis-parses there
# (there is no set -e here to catch it) and the line survives unchanged.
set_feature() {
    local name="$1" state="$2" script
    [ -f "$DC_JSON" ] || return 0
    if [ "$state" = "on" ]; then
        script='s|^\([[:space:]]*\)// *\("\./features/'"$name"'".*\)$|\1\2|'
    else
        script='s|^\([[:space:]]*\)\("\./features/'"$name"'".*\)$|\1// \2|'
    fi
    if sed "$script" "$DC_JSON" > "$DC_JSON.tmp"; then
        mv "$DC_JSON.tmp" "$DC_JSON"
    else
        rm -f "$DC_JSON.tmp"
    fi
}

# Give every direct child of the "features" block a trailing comma except the last
# one, ignoring commented-out lines — i.e. repair the trailing comma that commenting
# out the final entry would otherwise leave behind (and restore the one that
# uncommenting a new final entry needs). Called once after a batch of toggles.
#
# Depth counting, not pattern matching on `{}`, because a direct child can span
# several lines (the common-utils feature has an options object). A line is a direct
# child's last line when the running depth comes back to 1; depth 0 is the block's own
# closing brace and ends the scan. Commented lines are skipped as candidates but still
# counted — they are balanced, so they contribute nothing either way.
normalize_feature_commas() {
    [ -f "$DC_JSON" ] || return 0
    if awk '
        { L[NR] = $0 }
        END {
            for (i = 1; i <= NR; i++)
                if (L[i] ~ /^[[:space:]]*"features"[[:space:]]*:[[:space:]]*\{[[:space:]]*$/) { start = i; break }
            if (!start) { for (i = 1; i <= NR; i++) print L[i]; exit }
            depth = 1; n = 0
            for (i = start + 1; i <= NR; i++) {
                line = L[i]
                opens = gsub(/\{/, "{", line)
                closes = gsub(/\}/, "}", line)
                depth += opens - closes
                if (depth == 0) break
                if (depth == 1 && L[i] !~ /^[[:space:]]*\/\//) { n++; term[n] = i }
            }
            for (k = 1; k <= n; k++) {
                sub(/,[[:space:]]*$/, "", L[term[k]])
                if (k < n) L[term[k]] = L[term[k]] ","
            }
            for (i = 1; i <= NR; i++) print L[i]
        }
    ' "$DC_JSON" > "$DC_JSON.tmp"; then
        mv "$DC_JSON.tmp" "$DC_JSON"
    else
        rm -f "$DC_JSON.tmp"
    fi
}

# --- Profile-dependent features ---
# Only these are offered. The rest of the feature set (certs, shell, tmux, herdr, the
# ghcr.io features) is provider-independent and stays exactly as devcontainer.json ships
# it — except ./features/_template_, which is not a profile question at all and is handled
# by set_template_feature() below.
FEATURE_NAMES=(claude codex opencode pi grok bedrock chat)
FEATURE_DESCS=(
    "Claude Code"
    "Codex CLI"
    "OpenCode"
    "Pi Coding Agent"
    "Grok Build"
    "Bedrock account gates (retention, GovCloud access)"
    "Browser chat stack - adds minutes to the build"
)

# Features setup decides on its own: still toggled in devcontainer.json, never offered.
# bedrock is not a tool anyone chooses — it is the AWS ACCOUNT gates (data retention,
# GovCloud model entitlement) that every agent reaching Bedrock needs set, so it follows
# the profile selection exactly: on when a selected profile has AWS_REGION, off otherwise.
# Offering it as a checkbox only made it possible to select an AWS profile and then turn
# off the thing that makes its models answer at all, which fails much later and looks like
# a model problem. Its line is still printed, unnumbered, so the choice stays visible.
FEATURE_AUTO=(bedrock)

feature_is_auto() {
    local f
    for f in "${FEATURE_AUTO[@]}"; do
        [ "$f" = "$1" ] && return 0
    done
    return 1
}

# --- Helper: does this profile configure this feature? ---
# The profile fragment each feature's own postcreate.sh reads IS the support
# signal: with no fragment there is nothing to configure, so the feature would
# install and then find no work. bedrock is the one keyed on a variable instead of
# a file — it carries the AWS ACCOUNT gates (data retention, GovCloud model
# entitlement), so AWS_REGION in profile.env is what marks a profile as Bedrock.
feature_supported() {
    local feature="$1" dir="$PROFILES_DIR/$2"
    case "$feature" in
        claude)   [ -f "$dir/claude.json" ] ;;
        codex)    [ -f "$dir/config.toml" ] ;;
        opencode) [ -f "$dir/opencode.json" ] ;;
        pi)       [ -f "$dir/models.json" ] ;;
        grok)     [ -f "$dir/grok.toml" ] ;;
        chat)     [ -f "$dir/litellm.json" ] ;;
        bedrock)  grep -q '^AWS_REGION=' "$dir/profile.env" 2>/dev/null ;;
        *)        return 1 ;;
    esac
}

# --- CUI data handling ---
# One project, one side of the fence. A `.cui_approved` marker file in a profile's own
# directory means that provider is approved for CUI data; the answer here DELETES every
# profile on the other side, so a public project can reach public providers only and a CUI
# project CUI-approved ones only. Policy would permit a public project to use a
# CUI-approved provider, but nothing distinguishes the two at request time — an agent picks
# a model from whatever profiles were installed — so the mixed menu buys capability at the
# cost of a line nobody can see.
#
# DELETING the losing side, rather than merely not selecting it, is the enforcement:
# profile directories are the MENU, while CONFIGURED_PROFILES is only what is switched on
# today. An unselected profile is deliberately left in place for someone to enable later
# with `setup.sh <profile>` (prompt_credentials hard-errors once the directory is gone), so
# removing the directory is the only thing that puts a provider permanently out of reach.
#
# The surviving set is also the ONLY record of the answer, which is the mechanical reason to
# negate rather than to prune one side leniently: afterwards exactly one side exists, so the
# question is asked once and never returns (the gate below goes false), the answer is
# legible from `ls profiles/`, and every later `setup.sh <profile>` is confined to the
# right side with no second check anywhere. Keeping both sides would need the answer
# recorded somewhere — a key in devcontainer.env — and re-checked in update mode.
#
# The AWS pair needs no special case for the same reason. `aws` and `awsgov` declare the
# same five var names (AWS_REGION, CLAUDE_CODE_USE_BEDROCK, AWS_BEDROCK_FORCE_HTTP1,
# AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY) into one devcontainer.env, where
# collect_credentials dedups by name, so with both configured the second would silently run
# on the first one's credentials — and the partition already separates them. Two profiles on
# the SAME side colliding that way would still be a problem; none do today.
#
# The marker is a file rather than a list here so adding a provider is adding a directory
# and nothing else, and it MUST be tracked: the spawn payload is a git archive, so an
# untracked marker silently re-labels its profile as public in every spawned project.
#
# Under --spawning the deletions ride in the initial commit (setup_git runs after this); on
# the initializeCommand path they are left as working-tree changes for the user to commit,
# because nothing here may touch an adopted project's history.
choose_cui() {
    local dir name answer
    local cui=() public=() keep=() drop=()

    for dir in "$PROFILES_DIR"/*/; do
        [ -f "$dir/profile.env" ] || continue
        name="$(basename "$dir")"
        if [ -f "$dir/.cui_approved" ]; then
            cui+=("$name")
        else
            public+=("$name")
        fi
    done

    # Nothing to decide unless both sides are represented — which is also how a later run
    # knows the question has been answered already.
    if [ ${#cui[@]} -eq 0 ] || [ ${#public[@]} -eq 0 ]; then
        return 0
    fi

    # The maintainer bypass, and the ONE exception to the partition. A checkout of agent-dev
    # itself has to keep every profile: the whole point of that container is to develop and
    # test all of them, and the enforcement here is a deletion in the working tree, so a
    # first create in a fresh clone would otherwise ask a maintainer to permanently discard
    # three quarters of the thing they are maintaining (and offer the deletion for commit).
    # The marker is tracked in agent-dev's root and deleted by spawn's clean slate, so it
    # cannot reach a spawned or adopted project — a real project never takes this branch.
    #
    # It skips the question rather than answering it, so no profile is deleted and none is
    # deemed approved either. The container that results spans both sides, which is why the
    # notice says to treat it as public: poststart.sh reads the same markers and reports any
    # unmarked profile as public, so the mixed state is stated on every start rather than
    # quietly assumed.
    if [ -f "$TEMPLATE_MARKER" ]; then
        echo ""
        echo "  CUI Data Handling — QUESTION SKIPPED"
        echo ""
        echo "  This is a checkout of the agent-dev TEMPLATE repository (.template-container"
        echo "  is present), so every profile was left in place — a maintainer needs all of"
        echo "  them. No provider was approved as a result: treat this container as PUBLIC"
        echo "  and keep CUI data out of it."
        echo ""
        echo "  If you are not maintaining agent-dev, spawn a project instead — that asks the"
        echo "  question properly and enforces the answer:"
        echo ""
        echo "    $SCRIPT_DIR/spawn.sh <path>"
        return 0
    fi

    echo ""
    echo "  CUI Data Handling"
    echo ""
    echo "  This belongs to the project, not to a single request, so the profiles on the"
    echo "  other side of the answer are DELETED — no agent here can reach them afterwards,"
    echo "  and changing your mind means spawning the project again."
    echo ""
    echo "    CUI-approved:  ${cui[*]}"
    echo "    Public:        ${public[*]}"
    echo ""
    # A read that FAILS must not be read as "no". With no controlling terminal the
    # redirection fails and `read` never runs, and falling through to the else branch would
    # permanently delete every CUI-approved profile. The call sites probe with have_tty
    # first and that is still the real guard; this keeps the destructive default from being
    # reachable by accident at all.
    if ! read -p "Will this project handle CUI data? [y/N] " answer < /dev/tty; then
        echo "" >&2
        echo "  WARNING: no terminal to ask on — every profile was left in place." >&2
        return 0
    fi

    if [[ "$answer" =~ ^[Yy]$ ]]; then
        keep=("${cui[@]}")
        drop=("${public[@]}")
    else
        keep=("${public[@]}")
        drop=("${cui[@]}")
    fi

    for name in "${drop[@]}"; do
        rm -rf "$PROFILES_DIR/$name"
    done

    echo ""
    echo "  Keeping: ${keep[*]}"
    echo "  Removed: ${drop[*]}"
}

# --- Profile selection --- (sets SELECTED)
select_profiles() {
    echo ""
    echo "  Select Profiles"
    echo ""

    AVAILABLE=()
    for dir in "$PROFILES_DIR"/*/; do
        if [ -f "$dir/profile.env" ]; then
            AVAILABLE+=("$(basename "$dir")")
        fi
    done

    if [ ${#AVAILABLE[@]} -eq 0 ]; then
        echo "ERROR: No profiles found in $PROFILES_DIR" && exit 1
    fi

    for i in "${!AVAILABLE[@]}"; do
        echo "  $((i + 1)). ${AVAILABLE[$i]}"
    done
    echo ""

    SELECTED=()
    while [ ${#SELECTED[@]} -eq 0 ]; do
        read -p "Enter profile numbers (comma or space separated): " SELECTION < /dev/tty

        IFS=', ' read -ra PICKS <<< "$SELECTION"
        for pick in "${PICKS[@]}"; do
            if [[ "$pick" =~ ^[0-9]+$ ]] && [ "$pick" -ge 1 ] && [ "$pick" -le ${#AVAILABLE[@]} ]; then
                SELECTED+=("${AVAILABLE[$((pick - 1))]}")
            else
                echo "Invalid selection: $pick"
                SELECTED=()
                break
            fi
        done

        if [ ${#SELECTED[@]} -eq 0 ]; then
            echo "Please select at least one profile."
        fi
    done
}

# --- Feature selection --- (needs SELECTED; edits devcontainer.json)
# Defaults come from the profiles: a feature no selected profile configures starts
# off, everything else starts on. chat is the exception — every profile ships a
# litellm.json, so it is always supported and its default is the --chat flag,
# because what makes it opt-in is build time, not provider support.
#
# The defaults are the product, not a starting point: the list is printed, then a single
# [y/N] decides whether to touch it at all. Accepting is one keystroke and the common case,
# because the profile selection has already determined which features have anything to do.
# The flip loop is only entered on an explicit yes — it used to be the only way past this
# screen, which read as a decision the user was required to make.
#
# FEATURE_AUTO entries are printed without a number and cannot be flipped; see there.
select_features() {
    local i n p mark num note answer valid pick customize=ask
    local states=() notes=() prompt_map=()

    for i in "${!FEATURE_NAMES[@]}"; do
        local name="${FEATURE_NAMES[$i]}"
        local supporting=() missing=()
        for p in "${SELECTED[@]}"; do
            if feature_supported "$name" "$p"; then
                supporting+=("$p")
            else
                missing+=("$p")
            fi
        done
        notes[$i]=""
        if [ ${#supporting[@]} -eq 0 ]; then
            states[$i]="off"
            notes[$i]="no config for ${SELECTED[*]}"
        elif [ "$name" = "chat" ] && [ "$CHAT" = false ]; then
            states[$i]="off"
        else
            states[$i]="on"
            if [ ${#missing[@]} -gt 0 ]; then
                notes[$i]="no config for ${missing[*]}"
            fi
        fi
        if feature_is_auto "$name"; then
            notes[$i]="automatic — follows the profiles"
        else
            prompt_map+=("$i")
        fi
    done

    echo ""
    echo "  Features"
    echo ""
    echo "  [x] gets installed. Defaults follow the profile(s) you picked."
    echo ""

    while :; do
        num=0
        for i in "${!FEATURE_NAMES[@]}"; do
            mark=" "
            [ "${states[$i]}" = "on" ] && mark="x"
            note=""
            [ -n "${notes[$i]}" ] && note="  (${notes[$i]})"
            if feature_is_auto "${FEATURE_NAMES[$i]}"; then
                printf '     [%s] %-9s %s%s\n' \
                    "$mark" "${FEATURE_NAMES[$i]}" "${FEATURE_DESCS[$i]}" "$note"
            else
                num=$((num + 1))
                printf '  %d. [%s] %-9s %s%s\n' \
                    "$num" "$mark" "${FEATURE_NAMES[$i]}" "${FEATURE_DESCS[$i]}" "$note"
            fi
        done
        echo ""

        # First pass asks whether to touch the list at all; "no" accepts the defaults. A
        # failed read is that same "no" — the call site probes for a terminal before any of
        # this, and accepting the profile-derived defaults is the safe answer regardless.
        if [ "$customize" = ask ]; then
            if ! read -p "Customize the feature set? [y/N] " answer < /dev/tty; then
                answer=""
            fi
            if ! [[ "$answer" =~ ^[Yy]$ ]]; then
                break
            fi
            customize=yes
            echo ""
        fi

        read -p "Numbers to flip (comma or space separated, Enter to accept): " answer < /dev/tty
        [ -z "$answer" ] && break

        IFS=', ' read -ra PICKS <<< "$answer"
        valid=1
        for pick in "${PICKS[@]}"; do
            if ! [[ "$pick" =~ ^[0-9]+$ ]] || [ "$pick" -lt 1 ] || [ "$pick" -gt ${#prompt_map[@]} ]; then
                echo "Invalid selection: $pick"
                valid=0
                break
            fi
        done
        echo ""
        [ "$valid" -eq 0 ] && continue

        # The numbers the user sees index prompt_map, not FEATURE_NAMES — the automatic
        # entries are printed in place but take no number.
        for pick in "${PICKS[@]}"; do
            n=${prompt_map[$((pick - 1))]}
            if [ "${states[$n]}" = "on" ]; then states[$n]="off"; else states[$n]="on"; fi
        done
    done

    for i in "${!FEATURE_NAMES[@]}"; do
        set_feature "${FEATURE_NAMES[$i]}" "${states[$i]}"
    done
    normalize_feature_commas
    echo "Updated: .devcontainer/devcontainer.json"
}

# --- Maintainer-only tooling ---
# ./features/_template_ installs nothing; it is a handle for what agent-dev's OWN container
# needs and a project built from it does not, pulled in through dependsOn (today PowerShell,
# so a maintainer can run setup.ps1/spawn.ps1 in the container — every real invocation of
# those is on the host, so a spawned project has no use for pwsh and should not pay for the
# layer).
#
# Not a question, and not in FEATURE_NAMES: it has no profile dimension, so feature_supported
# would have nothing to say about it, and the answer is already written down. The gate is the
# same root .template-container marker choose_cui reads — "is this the template repo?" has one
# answer and lives in one file. spawn's clean slate deletes the marker in staging before setup
# runs, so a spawned project switches this off without being asked, and an adopted one (which
# only ever receives .devcontainer/) does too.
#
# Called from --spawning only, in both of its branches, and deliberately NOT from --ensure:
# features are installed at image build, so an edit on the create path could only affect the
# next build, and in an adopted project it would dirty the working tree for no gain.
set_template_feature() {
    if [ -f "$TEMPLATE_MARKER" ]; then
        set_feature _template_ on
    else
        set_feature _template_ off
    fi
    normalize_feature_commas
}

# --- Helper: is a local feature switched on in devcontainer.json? ---
# Reads the file rather than a flag, so it answers what the project will actually build:
# `--chat` only seeds the default that select_features then lets the user flip. An
# uncommented entry is the on state, and a commented one can't match — the toggle prefixes
# `// `, so the quote no longer follows the indent.
feature_enabled() {
    grep -qE "^[[:space:]]*\"\./features/$1\"" "$DC_JSON"
}

# --- Browser chat stack autostart --- (sets AUTOSTART; reads devcontainer.json)
# CHAT_AUTOSTART is the one runtime answer this script collects. It is read by
# features/chat/poststart.sh out of the container environment on EVERY start, so unlike
# the feature set it is not frozen at image build and a project can change its mind by
# editing devcontainer.env — which is also why asking it is worth doing on the adopted
# path too, where the feature prompt is deliberately skipped.
#
# Only asked when the chat feature is actually on: with the stack absent the variable
# would decide nothing, and a chat-less project's env file should not carry a knob for
# services it does not have.
#
# Defaults to on ([Y/n]) because reaching this prompt means the user just chose to build
# the stack; the question is only whether they want to pay the start-up wait every time
# or bring it up with `start_chat_stack` when they need it. That default is also what a
# missing terminal gets — no prompt, no warning, since a container that starts its own
# services is the unsurprising outcome of asking for them.
#
# AUTOSTART stays EMPTY when the feature is off, which is what write_devenv keys off to
# omit the line entirely.
#
# A "no" answer echoes the command back, because that reply is the only point where the
# user has committed to starting the stack by hand: the pre-prompt text names the command
# while they are still deciding, and this is the line they can act on afterwards. The
# feature's postcreate.sh symlinks start.sh to ~/.local/bin/start_chat_stack, so it really
# is on PATH from the first create on.
choose_chat_autostart() {
    local answer
    AUTOSTART=""
    feature_enabled chat || return 0

    AUTOSTART=1
    have_tty || return 0

    echo ""
    echo "  The chat stack is four services under supervisord, and a container start"
    echo "  waits for them to answer before it finishes. Answer no to bring them up on"
    echo "  demand instead, with 'start_chat_stack' inside the container."
    echo ""
    if read -p "Start the browser chat stack automatically on container start? [Y/n] " answer < /dev/tty; then
        case "$answer" in
            [Nn]*)
                AUTOSTART=0
                echo ""
                echo "  Not started automatically. Bring the stack up from a terminal inside"
                echo "  the container with 'start_chat_stack' (on PATH after the first"
                echo "  create) — once per container start is enough, it keeps running."
                ;;
        esac
    fi
}

# --- Generate devcontainer.env --- (needs SELECTED, CRED_KEYS, CRED_VALS, AUTOSTART)
# Every var a selected profile declares is written; the ones whose value is empty
# in profile.env are filled from the collected credentials, and left EMPTY when
# nothing was collected (the --spawning case, where the keys don't exist yet).
write_devenv() {
    local profile line env_file i
    local output="CONFIGURED_PROFILES=$(IFS=,; echo "${SELECTED[*]}")"$'\n'

    # Container behaviour rather than a credential, so it goes above the profile blocks.
    # Written only when choose_chat_autostart had something to decide (chat feature on);
    # `${AUTOSTART:-}` so an unset value is simply no line, without this depending on
    # every future caller having run that prompt first.
    if [ -n "${AUTOSTART:-}" ]; then
        output+="CHAT_AUTOSTART=$AUTOSTART"$'\n'
    fi

    for profile in "${SELECTED[@]}"; do
        env_file="$PROFILES_DIR/$profile/profile.env"
        while IFS= read -r line || [ -n "$line" ]; do
            [[ "$line" =~ ^[[:space:]]*# ]] && continue
            [[ -z "$line" ]] && continue

            if [[ "$line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=(.*)$ ]]; then
                local var_name="${BASH_REMATCH[1]}"
                local var_value="${BASH_REMATCH[2]}"
                if [ -z "$var_value" ]; then
                    for i in "${!CRED_KEYS[@]}"; do
                        if [ "${CRED_KEYS[$i]}" = "$var_name" ]; then
                            line="$var_name=${CRED_VALS[$i]}"
                            break
                        fi
                    done
                fi
            fi

            output+="$line"$'\n'
        done < "$env_file"
    done

    printf '%s' "$output" > "$DEVENV"
    echo "Created: .devcontainer/devcontainer.env"
}

# --- Git repository --- (spawn mode only)
# ALL git for this project happens here, on the host, before the container is ever
# built. Nothing in the container commits anything: the two things that used to dirty
# the tree on a first create are both settled by now (the CUI profile deletion happened
# above, and the Claude Code feature writes its defaults to ~/.claude instead of the
# project), so the initial commit is the finished state and a create leaves the tree
# alone. That also retires the identity workaround the in-container commit needed —
# VS Code copies the host ~/.gitconfig in only on window attach, AFTER
# postCreateCommand, so git there has no identity to commit with. Here it does.
#
# Called last in spawn mode so every edit rides in the initial commit.
setup_git() {
    local answer="" want="$GIT" name email

    command -v git &>/dev/null || return 0

    # An ancestor directory is already a repo (spawn refuses an existing target, so
    # this means `--spawning` was run by hand inside a checkout). Someone else's
    # history: no init, no commit, no staging.
    if git -C "$PROJECT_DIR" rev-parse --git-dir &>/dev/null; then
        echo ""
        echo "  Already inside a Git repository — leaving it alone."
        return 0
    fi

    if have_tty; then
        echo ""
        echo "  Git"
        echo ""
        if [ "$want" = true ]; then
            read -p "  Initialize a Git repository? [Y/n] " answer < /dev/tty
            [[ "$answer" =~ ^[Nn]$ ]] && want=false
        else
            read -p "  Initialize a Git repository? [y/N] " answer < /dev/tty
            [[ "$answer" =~ ^[Yy]$ ]] && want=true
        fi
    fi
    [ "$want" = true ] || return 0

    # -b needs git 2.28+; on anything older fall back to a plain init and let the
    # host's own init.defaultBranch decide.
    if ! git -C "$PROJECT_DIR" init -q -b main >/dev/null 2>&1; then
        git -C "$PROJECT_DIR" init -q || return 0
    fi

    # `git add -A` sweeps the whole tree, so devcontainer.env being ignored is what
    # keeps credentials out of the commit — and since the credentials were collected a
    # moment ago, that file holds REAL keys by the time we get here. Verify the rule
    # instead of assuming it: if it is missing, stop before anything is staged.
    if ! git -C "$PROJECT_DIR" check-ignore -q .devcontainer/devcontainer.env; then
        echo ""
        echo "  WARNING: .devcontainer/.gitignore does not ignore devcontainer.env." >&2
        echo "  Repository initialized, but nothing staged or committed — restore that" >&2
        echo "  rule before committing, or credentials will land in Git history." >&2
        return 0
    fi

    # Identity, repo-local only: this is a brand-new repository, so setting it here
    # configures the project the user just created without touching their global
    # config or any other repo.
    name="$(git config --global user.name 2>/dev/null || true)"
    email="$(git config --global user.email 2>/dev/null || true)"
    if [ -z "$name" ] || [ -z "$email" ]; then
        if ! have_tty; then
            git -C "$PROJECT_DIR" add -A
            echo ""
            echo "  WARNING: no global Git user.name/user.email, and no terminal to ask." >&2
            echo "  Repository initialized and everything staged, but not committed. Set" >&2
            echo "  your identity and commit:" >&2
            echo "" >&2
            echo "    git config user.name \"Your Name\"" >&2
            echo "    git config user.email \"you@example.com\"" >&2
            echo "    git commit -m \"Initial commit\"" >&2
            return 0
        fi
        echo ""
        echo "  No global Git identity — setting one for this repository only."
        echo ""
        while [ -z "$name" ]; do
            read -p "  Your name: " name < /dev/tty
        done
        while [ -z "$email" ]; do
            read -p "  Your email: " email < /dev/tty
        done
        git -C "$PROJECT_DIR" config user.name "$name"
        git -C "$PROJECT_DIR" config user.email "$email"
    fi

    git -C "$PROJECT_DIR" add -A
    git -C "$PROJECT_DIR" commit -q -m "Initial commit"
    echo ""
    echo "  Git repository initialized with an initial commit."
}

# =====================================================================
# Update mode: ./setup.sh <profile> [profile...]
# Updates credentials for existing profiles, or adds new profiles.
# =====================================================================
if [ $# -gt 0 ]; then
    if [ ! -f "$DEVENV" ]; then
        echo "ERROR: $DEVENV not found. Run setup.sh with no arguments first." && exit 1
    fi

    # Determine which profiles are new (not yet in CONFIGURED_PROFILES)
    PROFILES_LINE=$(grep '^CONFIGURED_PROFILES=' "$DEVENV" 2>/dev/null || true)
    EXISTING_CSV="${PROFILES_LINE#CONFIGURED_PROFILES=}"
    # Strip CR: devcontainer.env may have been written by setup.ps1 on a Windows host
    # (PowerShell Set-Content = CRLF). Without this the last profile keeps a trailing
    # \r and compares unequal to its own name, so it looks unconfigured and gets
    # re-appended to CONFIGURED_PROFILES as a duplicate. See postcreate.sh's copy.
    EXISTING_CSV="${EXISTING_CSV//$'\r'/}"
    IFS=',' read -ra EXISTING <<< "$EXISTING_CSV"

    NEW_PROFILES=()
    for profile in "$@"; do
        FOUND=0
        for e in "${EXISTING[@]}"; do
            if [ "$e" = "$profile" ]; then FOUND=1; break; fi
        done
        if [ "$FOUND" -eq 0 ]; then
            NEW_PROFILES+=("$profile")
        fi
    done

    # Collect new credential values from each specified profile
    UPDATES=""
    for profile in "$@"; do
        RESULT=$(prompt_credentials "$profile") || exit 1
        UPDATES+="$RESULT"$'\n'
    done

    # Patch the values into devcontainer.env
    CONTENT=$(cat "$DEVENV")
    while IFS= read -r update_line; do
        [ -z "$update_line" ] && continue
        if [[ "$update_line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=(.*)$ ]]; then
            VAR="${BASH_REMATCH[1]}"
            VAL="${BASH_REMATCH[2]}"
            if echo "$CONTENT" | grep -q "^${VAR}="; then
                # Rewrite the line in bash rather than with sed: VAL is an arbitrary
                # credential, and sed would read a |, & or \ inside it as syntax (the
                # delimiter, "the whole match", an escape) and silently write the wrong
                # value. VAR is a validated identifier, so it is safe in the glob.
                NEW_CONTENT=""
                while IFS= read -r c_line; do
                    case "$c_line" in
                        "${VAR}="*) NEW_CONTENT+="${VAR}=${VAL}"$'\n' ;;
                        *)          NEW_CONTENT+="$c_line"$'\n' ;;
                    esac
                done <<< "$CONTENT"
                CONTENT="${NEW_CONTENT%$'\n'}"
            else
                CONTENT+=$'\n'"${VAR}=${VAL}"
            fi
        fi
    done <<< "$UPDATES"

    # For new profiles, append their non-credential env vars (ones with values in profile.env)
    for profile in "${NEW_PROFILES[@]}"; do
        ENV_FILE="$PROFILES_DIR/$profile/profile.env"
        while IFS= read -r line || [ -n "$line" ]; do
            [[ "$line" =~ ^[[:space:]]*# ]] && continue
            [[ -z "$line" ]] && continue
            if [[ "$line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=(.+)$ ]]; then
                VAR="${BASH_REMATCH[1]}"
                if ! echo "$CONTENT" | grep -q "^${VAR}="; then
                    CONTENT+=$'\n'"$line"
                fi
            fi
        done < "$ENV_FILE"
    done

    # Update CONFIGURED_PROFILES to include new profiles
    if [ ${#NEW_PROFILES[@]} -gt 0 ]; then
        UPDATED_CSV="$EXISTING_CSV"
        for p in "${NEW_PROFILES[@]}"; do
            UPDATED_CSV+=",${p}"
        done
        CONTENT=$(echo "$CONTENT" | sed "s|^CONFIGURED_PROFILES=.*|CONFIGURED_PROFILES=${UPDATED_CSV}|")
    fi

    printf '%s\n' "$CONTENT" > "$DEVENV"

    echo ""
    if [ ${#NEW_PROFILES[@]} -gt 0 ]; then
        echo "Added profile(s): ${NEW_PROFILES[*]}"
    fi
    echo "Updated: .devcontainer/devcontainer.env"
    echo ""

    # Say how to make the new values take effect — writing the file does nothing by
    # itself. devcontainer.env is a Docker `--env-file` in runArgs, so it is read ONCE, at
    # container CREATE: every process in a running container inherited its environment at
    # that moment, and reopening or restarting reuses what Docker captured then. A rebuild
    # is the only thing that re-reads the file.
    #
    # This script cannot apply it for the caller. A child process cannot alter its parent
    # shell's environment, and `source setup.sh` is not the way around that either — the
    # `exit 0` below would take the interactive shell with it. So the in-container case
    # gets the one-liner to run instead, where it is worth something: an agent started
    # from that shell inherits the new value without a rebuild.
    #
    # `set -a` rather than a bare `.`, and not because the bare form always fails — it
    # works for a name the container ALREADY has, which is the usual case here: Docker
    # exported every line of this file at create (a blank credential included, as the
    # empty string), and assigning to an already-exported variable keeps the export
    # attribute. It fails, silently, for a name that is new since that create — any var of
    # a profile added just now, or a credential appended for a profile whose var did not
    # exist yet — and nothing at this prompt can tell the two cases apart. So the bare form
    # would be right most of the time and quietly useless the rest, which is the worst
    # shape for a credential problem to take. `set -a` exports unconditionally; the `;`
    # before `set +a` (not `&&`) is what keeps it from being skipped if the source aborts.
    #
    # Still a stopgap for one shell either way, because bash and Docker do not parse this
    # file identically: Docker trims the trailing CR a Windows-written file has and takes
    # the rest of the line verbatim, while bash keeps the CR and reads whitespace, `$` and
    # backticks in a value as syntax.
    if [ -f /.dockerenv ]; then
        echo "Docker read this file when the container was created, so nothing running has"
        echo "the new values yet. Rebuild to apply them everywhere:"
        echo ""
        echo "  F1 -> \"Dev Containers: Rebuild Container\""
        echo ""
        echo "For this shell alone, and anything started from it, without a rebuild:"
        echo ""
        echo "  set -a; . \"$DEVENV\"; set +a"
    else
        echo "Docker reads this file when the container is created, so rebuild the container"
        echo "to pick the new values up — reopening or restarting reuses the environment"
        echo "captured at the last create:"
        echo ""
        echo "  F1 -> \"Dev Containers: Rebuild Container\""
    fi
    # A new profile needs more than its environment variables: postcreate.sh regenerates
    # every agent's config (and LiteLLM's) from CONFIGURED_PROFILES on create, and only a
    # rebuild runs that. Sourcing the env file cannot stand in for it, so rule it out here
    # rather than leaving the line above to imply otherwise.
    if [ ${#NEW_PROFILES[@]} -gt 0 ]; then
        echo ""
        echo "A rebuild is REQUIRED for the profile(s) just added: each agent's config is"
        echo "generated from CONFIGURED_PROFILES on create, not read from the environment."
    fi
    echo ""
    exit 0
fi

# =====================================================================
# Spawn mode: ./setup.sh --spawning [--chat]
# Invoked by spawn.sh on the HOST, in the freshly extracted project, BEFORE the
# container exists — which is the whole point. The profile questions have always
# been asked from initializeCommand, and that is too late for anything that has to
# be true at image build: by then the CLI has already parsed devcontainer.json, so
# a feature it lists is fetched and installed no matter what the answers turn out
# to be (`./features/bedrock` even drags in the aws-cli feature via dependsOn).
# Asking at spawn instead means the very first build installs only what the chosen
# profiles can actually configure.
#
# Credentials are collected here too, which is what makes the container create fully
# non-interactive: devcontainer.env is gitignored, so a real key in it is never at risk
# of being committed (setup_git asserts that rule below before it stages anything), and
# a host terminal is a far better place to paste one than initializeCommand's, which is
# frequently not a terminal at all. Any key the user hasn't generated yet can be skipped
# with an empty answer and added later with `setup.sh <profile>`.
# =====================================================================
if [ "$SPAWNING" = true ]; then
    # Safety net: spawn always extracts into a new directory, so there should be no
    # env file. If one exists, the project is already configured — leave it alone.
    if [ -f "$DEVENV" ]; then
        echo ""
        echo "  .devcontainer/devcontainer.env already exists — nothing to do."
        echo ""
        exit 0
    fi

    # Before the terminal check, because it asks nothing: the marker decides, and the
    # answer is the same whether or not anyone is watching.
    set_template_feature

    # No terminal to prompt on (spawn run from a script or CI). Apply the --chat default
    # and stop: with no env file written, the questions are still open. Point at the host
    # rather than at container create — the create has no terminal to ask on either
    # (--ensure's adopted-project path says the same thing if it gets there first).
    # --git still applies: setup_git prompts only when it can, and does nothing unless
    # the flag was passed.
    if ! have_tty; then
        if [ "$CHAT" != true ]; then
            set_feature chat off
            normalize_feature_commas
        fi
        echo ""
        echo "  No terminal available — profile, feature and credential selection"
        echo "  skipped. Before opening the project, run:"
        echo ""
        echo "    $SCRIPT_DIR/setup.sh"
        setup_git
        echo ""
        exit 0
    fi

    choose_cui
    select_profiles
    select_features
    # After the feature selection, because it is only asked when chat survived it — and
    # before the credentials so the two prompts that write devcontainer.env are adjacent.
    choose_chat_autostart
    collect_credentials optional

    echo ""
    write_devenv
    # Last, so the initial commit records the finished state: pruned profiles, the
    # feature set this project ended up with, and nothing else pending.
    setup_git

    echo ""
    echo "  Profiles: ${SELECTED[*]}"
    # Report the skipped keys, with the command that fills them in. Read back out of the
    # env file that was just written rather than tracked through the prompts, so this
    # says what the file actually contains.
    PENDING=()
    while IFS= read -r p; do
        [ -n "$p" ] && PENDING+=("$p")
    done < <(pending_profiles)
    if [ ${#PENDING[@]} -gt 0 ]; then
        echo ""
        echo "  Credentials left blank for: ${PENDING[*]}"
        echo "  Add them on the host, before or after the first build, with:"
        echo ""
        for p in "${PENDING[@]}"; do
            echo "    .devcontainer/scripts/setup.sh $p"
        done
    fi
    echo ""
    exit 0
fi

# =====================================================================
# initializeCommand fast path: devcontainer.env exists, so there is nothing to ask.
# `--spawning` now collects the credentials too, so the env file existing means every
# question this project asks has been answered — including a key deliberately left
# blank, which is the user's to fill in on the host with `setup.sh <profile>` and not
# something to re-prompt on every container start. Exit before the "already configured"
# block below so the create stays silent and, above all, non-interactive:
# initializeCommand's stdin is frequently not a terminal at all.
#
# The one case that still has work to do is a project that ADOPTED .devcontainer/
# instead of being spawned — no env file, so it falls through to the first-run flow.
# =====================================================================
if [ "$ENSURE" = true ] && [ -f "$DEVENV" ]; then
    exit 0
fi

# =====================================================================
# Full setup mode: ./setup.sh (no arguments)
# =====================================================================

SELECTED=()

# --- Already configured: show help and exit ---
if [ -f "$DEVENV" ]; then
    PROFILES_LINE=$(grep '^CONFIGURED_PROFILES=' "$DEVENV" 2>/dev/null || true)
    if [ -n "$PROFILES_LINE" ]; then
        CSV="${PROFILES_LINE#CONFIGURED_PROFILES=}"
        # Strip CR (Windows-written env file) — otherwise the trailing \r on the last
        # profile fails the profile.env existence check below, which resets SELECTED
        # and makes an already-configured project look unconfigured.
        CSV="${CSV//$'\r'/}"
        IFS=',' read -ra SELECTED <<< "$CSV"
        # Validate all configured profiles still exist
        for p in "${SELECTED[@]}"; do
            if [ ! -f "$PROFILES_DIR/$p/profile.env" ]; then
                SELECTED=()
                break
            fi
        done
        if [ ${#SELECTED[@]} -gt 0 ]; then
            # --- Credentials still pending ---
            # A key can be blank because the user skipped it at spawn time, so offer to
            # finish the job — which is precisely what update mode does, so re-exec into
            # it rather than growing a second copy. Only reached by a hand-run
            # `setup.sh`: --ensure exited above, so nothing here can interrupt a build.
            # Skipped once the values are in, so a normal re-run just prints the summary.
            PENDING=()
            while IFS= read -r p; do
                [ -n "$p" ] && PENDING+=("$p")
            done < <(pending_profiles)
            # Warn rather than prompt when there is no terminal to prompt on. Every
            # prompt reads /dev/tty directly, which on a host with no controlling
            # terminal is not a blocking read but a hard failure — five "No such
            # device or address" errors and a non-zero exit.
            if [ ${#PENDING[@]} -gt 0 ] && ! have_tty; then
                echo "" >&2
                echo "WARNING: credentials are still blank in .devcontainer/devcontainer.env" >&2
                echo "  for: ${PENDING[*]}" >&2
                echo "  No terminal is available to prompt on. Fill them in by running, on" >&2
                echo "  the host, one of:" >&2
                for p in "${PENDING[@]}"; do
                    echo "    $SCRIPT_DIR/setup.sh $p" >&2
                done
                echo "" >&2
                PENDING=()
            fi
            if [ ${#PENDING[@]} -gt 0 ]; then
                show_banner
                echo ""
                echo "  Configure Credentials"
                SETUP_NO_BANNER=1 exec "$0" "${PENDING[@]}"
            fi

            # Find unconfigured profiles
            UNCONFIGURED=()
            for dir in "$PROFILES_DIR"/*/; do
                if [ -f "$dir/profile.env" ]; then
                    NAME="$(basename "$dir")"
                    FOUND=0
                    for s in "${SELECTED[@]}"; do
                        if [ "$s" = "$NAME" ]; then FOUND=1; break; fi
                    done
                    if [ "$FOUND" -eq 0 ]; then
                        UNCONFIGURED+=("$NAME")
                    fi
                fi
            done

            echo ""
            echo "  Profiles already configured: ${SELECTED[*]}"
            echo ""
            echo "  To update credentials for a profile:"
            echo ""
            for p in "${SELECTED[@]}"; do
                echo "    $0 $p"
            done
            if [ ${#UNCONFIGURED[@]} -gt 0 ]; then
                echo ""
                echo "  To add an unconfigured profile:"
                echo ""
                for p in "${UNCONFIGURED[@]}"; do
                    echo "    $0 $p"
                done
            fi
            echo ""
            exit 0
        fi
    fi
fi

# --- First-run setup (CUI + profile selection + credentials) ---
# This is the ADOPTED-project path: `.devcontainer/` was dropped into an existing
# project, so no spawn ran and there is no env file. A spawned project never gets here
# — every question was answered on the host and --ensure exited above.
#
# Feature selection is deliberately NOT repeated here. It edits devcontainer.json,
# which the CLI parsed before initializeCommand ran, so an edit now would only take
# effect on the NEXT build — the opposite of useful on a first create.
if [ ${#SELECTED[@]} -eq 0 ]; then
    # No terminal to ask on, and unlike every other path this one has real questions
    # left. Bail out with a warning rather than prompting: each prompt reads /dev/tty
    # directly, which with no controlling terminal is a hard failure and not a blocking
    # read, and the two prompts fail in different and worse ways — choose_cui would take
    # the failed read as "not CUI" and delete every CUI-approved profile, and
    # select_profiles would spin forever. Exit 0 so the build still finishes;
    # the user runs setup.sh on the host and reopens.
    if ! have_tty; then
        echo "" >&2
        echo "WARNING: no provider profiles are configured for this project, and there is" >&2
        echo "  no terminal to ask which ones to use on, so the container will build with" >&2
        echo "  no provider credentials at all. Run this on the host and reopen the" >&2
        echo "  project:" >&2
        echo "" >&2
        echo "    $SCRIPT_DIR/setup.sh" >&2
        echo "" >&2
        exit 0
    fi
    show_banner
    choose_cui
    select_profiles
fi

collect_credentials

# Whether the web services (LiteLLM proxy, Open WebUI, Open Terminal, SearXNG) are
# INSTALLED is not asked here: that is the feature selection, which belongs to spawn mode
# because it has to happen before the image build (`spawn --chat` seeds its default; the
# ./features/chat line is commented out of devcontainer.json when it ends up off).
# Whether an installed stack STARTS ITSELF is a runtime flag, so it is fair game on this
# path — an adopted project may well have the feature and has had no chance to answer.
choose_chat_autostart

# find-skills is no longer asked about here: it installs into $HOME (user scope) on
# every create in postcreate.sh, so it's part of the container rather than a
# per-project choice and needs no persisted flag.

# --- Generate devcontainer.env ---
echo ""
write_devenv

# NB: no git here, deliberately. This is the initializeCommand path, which runs for
# ADOPTED projects too — a repo this container did not create and whose history it must
# not write to. The CUI profile deletion is left as a working-tree change for the user
# to commit. Git runs in exactly one place, `--spawning`'s setup_git, where the repo is
# one we are creating from scratch.

echo ""
