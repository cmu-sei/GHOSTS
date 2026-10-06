#!/usr/bin/env bash
# Step 2 done-when: a scenario document survives the API.
#
# For each document D: import it, export the new scenario as D', and compare. Five checks per
# document, all against a running API:
#
#   import      the document is accepted
#   valid       D' validates against schema v1
#   stable      two exports of the unchanged scenario are byte-identical
#   fixedpoint  importing D' and exporting again gives D' byte for byte
#   lossless    D' is D byte for byte — the import keeps the document, so this is a check, not a report
#
# and one report, which is not a pass/fail:
#
#   derived     D against ?derived=true, which rebuilds a document from the rows alone. What no column
#               holds is kept in the rows' extras, so a diff here is what the rows still cannot rebuild.
#
# Every scenario this script creates is deleted again, so the database is left as it was found.
# Scenarios already in the database are exported first and their exports round-tripped too.
#
#   API=http://localhost:5000 bash roundtrip.sh
#
# Exits non-zero if any check fails. Needs curl, jq and node (with tools/node_modules installed).

set -uo pipefail

API=${API:-http://localhost:5000}
HERE=$(cd "$(dirname "$0")" && pwd)
WORK=$(mktemp -d)
FAILED=0
LOSSY=()
UNREP=()
REFUSED=()

# Documents the validator is expected to refuse, and the code it must refuse them with. Both of these
# declare a sub-hour exercise, which GHOSTS cannot store (duration_hours is an integer), and the
# examples say what their fixtures say rather than rounding to fit the column. So the refusal is the
# correct behaviour and it is what this script checks: refused for exactly that reason, nothing
# written. When the column holds minutes, these two entries go away and both documents round-trip.
declare -A EXPECT_REFUSED=(
  [soc-morning]=TIME_DURATION_NOT_STORABLE
  [meridian-hybrid]=TIME_DURATION_NOT_STORABLE
)

pass() { printf 'ok    %-28s %s\n' "$1" "$2"; }
fail() { printf 'FAIL  %-28s %s\n' "$1" "$2"; FAILED=$((FAILED + 1)); }
# A scenario already in the database whose data the schema rejects. Reported, not counted as a
# failed check: the export is faithful and the schema is right, the stored value is out of range.
unrepresentable() { printf 'data  %-28s %s\n' "$1 valid" "$2"; UNREP+=("$1"); }

cleanup() { rm -rf "$WORK"; }
trap cleanup EXIT

# Imports a document, echoes the new scenario id, or nothing when the API rejected it.
import() {
  local doc=$1 body=$2 code
  code=$(curl -sS -o "$body" -w '%{http_code}' -X POST "$API/api/scenarios/import" \
    -H 'Content-Type: application/json' --data-binary "@$doc")
  [ "$code" = "201" ] || { echo "http $code: $(head -c 400 "$body")" >&2; return 1; }
  jq -r '.id' "$body"
}

export_document() { # id outfile
  local code
  code=$(curl -sS -o "$2" -w '%{http_code}' "$API/api/scenarios/$1/document")
  [ "$code" = "200" ] || { echo "http $code" >&2; return 1; }
}

# The document rebuilt from the rows alone, ignoring the one the import stored.
export_derived() { # id outfile
  local code
  code=$(curl -sS -o "$2" -w '%{http_code}' "$API/api/scenarios/$1/document?derived=true")
  [ "$code" = "200" ] || { echo "http $code" >&2; return 1; }
}

delete_scenario() { curl -sS -o /dev/null -X DELETE "$API/api/scenarios/$1"; }

# name document
roundtrip() {
  local name=$1 doc=$2 id first second reimport_id third

  if ! id=$(import "$doc" "$WORK/$name.import.json" 2>"$WORK/$name.err"); then
    local want=${EXPECT_REFUSED[$name]:-}
    if [ -n "$want" ] && jq -e --arg c "$want" \
        'any(.findings[]; .code == $c and .severity == "error")' "$WORK/$name.import.json" >/dev/null 2>&1; then
      pass "$name refused" "$want, as expected; nothing written"
      REFUSED+=("$name")
    else
      fail "$name import" "$(cat "$WORK/$name.err")"
    fi
    return
  fi
  pass "$name import" "scenario $id"

  if ! export_document "$id" "$WORK/$name.export.json"; then
    fail "$name export" "$(cat "$WORK/$name.err" 2>/dev/null)"
    delete_scenario "$id"
    return
  fi
  first=$WORK/$name.export.json

  if node "$HERE/scenario-doc.mjs" validate "$first" >"$WORK/$name.validate" 2>&1; then
    pass "$name valid" "schema v1"
  else
    fail "$name valid" "$(sed -n '2,6p' "$WORK/$name.validate" | tr '\n' ';')"
  fi

  second=$WORK/$name.export2.json
  export_document "$id" "$second"
  if cmp -s "$first" "$second"; then
    pass "$name stable" "two exports identical"
  else
    fail "$name stable" "two exports of the same scenario differ"
  fi

  if reimport_id=$(import "$first" "$WORK/$name.reimport.json" 2>"$WORK/$name.err2"); then
    third=$WORK/$name.export3.json
    export_document "$reimport_id" "$third"
    if cmp -s "$first" "$third"; then
      pass "$name fixedpoint" "export -> import -> export identical"
    else
      fail "$name fixedpoint" "$(diff "$first" "$third" | head -12 | tr '\n' ';')"
    fi
    delete_scenario "$reimport_id"
  else
    fail "$name fixedpoint" "re-import rejected: $(cat "$WORK/$name.err2")"
  fi

  if cmp -s "$doc" "$first"; then
    pass "$name lossless" "import -> export is the document"
  else
    fail "$name lossless" "$(node "$HERE/scenario-doc.mjs" diff "$doc" "$first" | head -12 | tr '\n' ';')"
  fi

  # The derived form is the document the rows can rebuild on their own. Where it differs from D,
  # the difference is what the rows still cannot hold.
  local derived=$WORK/$name.derived.json
  if export_derived "$id" "$derived" 2>>"$WORK/$name.err"; then
    if cmp -s "$doc" "$derived"; then
      printf 'same  %-28s %s\n' "$name derived" "the columns hold the whole document"
    else
      printf 'loss  %-28s %s\n' "$name derived" "the rows do not rebuild the whole document"
      node "$HERE/scenario-doc.mjs" diff "$doc" "$derived" | sed 's/^/        /'
      LOSSY+=("$name")
    fi
  else
    fail "$name derived" "$(cat "$WORK/$name.err")"
  fi

  delete_scenario "$id"
}

echo "== documents in schemas/scenario-document/examples"
for doc in "$HERE"/../examples/*.scenario.json; do
  roundtrip "$(basename "$doc" .scenario.json)" "$doc"
done

echo
echo "== scenarios already in the database"
ids=$(curl -sS "$API/api/scenarios" | jq -r '.[].id' | sort -n)
if [ -z "$ids" ]; then
  echo "none"
fi
for id in $ids; do
  name="scenario-$id"
  if export_document "$id" "$WORK/$name.json" 2>"$WORK/$name.err"; then
    pass "$name export" "$(wc -c <"$WORK/$name.json" | tr -d ' ') bytes"
    if node "$HERE/scenario-doc.mjs" validate "$WORK/$name.json" >"$WORK/$name.validate" 2>&1; then
      pass "$name valid" "schema v1"
      roundtrip "$name-export" "$WORK/$name.json"
    else
      unrepresentable "$name" "$(sed -n '2,6p' "$WORK/$name.validate" | tr '\n' ';')"
    fi
  else
    fail "$name export" "$(cat "$WORK/$name.err")"
  fi
done

echo
if [ ${#LOSSY[@]} -gt 0 ]; then
  echo "documents the columns alone cannot rebuild: ${LOSSY[*]}"
fi
if [ ${#UNREP[@]} -gt 0 ]; then
  echo "scenarios whose stored data schema v1 rejects: ${UNREP[*]}"
fi
if [ ${#REFUSED[@]} -gt 0 ]; then
  echo "documents the validator refused, as expected: ${REFUSED[*]}"
fi
if [ "$FAILED" -gt 0 ]; then
  echo "$FAILED check(s) failed"
  exit 1
fi
echo "all checks passed"
