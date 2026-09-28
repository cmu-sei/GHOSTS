#!/usr/bin/env bash
# Step 1 done-when: the four RPG fixtures are expressed in the schema.
# Converts each fixture, validates the result, and checks no source value was dropped.
#   GHOSTS=/path/to/GHOSTS bash tools/test.sh
set -euo pipefail
cd "$(dirname "$0")/.."
GHOSTS=${GHOSTS:-/tmp/ghosts}
FIX="$GHOSTS/experimental/rpg/fixtures/scenarios"
export NODE_PATH=${NODE_PATH:-/workspaces/content-dev/server/node_modules}
fail=0
for f in phishing-drill soc-morning operation-overlord meridian-hybrid; do
  node tools/scenario-doc.mjs convert "$FIX/$f.json" "examples/$f.scenario.json" || fail=1
  node tools/scenario-doc.mjs coverage "$FIX/$f.json" "examples/$f.scenario.json" || fail=1
done
node tools/scenario-doc.mjs validate examples/*.scenario.json || fail=1
# Canonical form is idempotent: canonicalizing an example changes nothing.
for f in examples/*.scenario.json; do
  cp "$f" /tmp/canon-check.json; node tools/scenario-doc.mjs canonicalize /tmp/canon-check.json >/dev/null
  cmp -s "$f" /tmp/canon-check.json || { echo "FAIL  not canonical: $f"; fail=1; }
done
# Negative: the deliberately invalid document must be rejected for each planted defect,
# not merely fail to run. Each expected error must appear in the validator's output.
out=$(node tools/scenario-doc.mjs validate tests/invalid.scenario.json 2>&1) && { echo "FAIL  invalid document passed"; fail=1; }
for want in '/slug must match pattern' '/adversaries/0/techniques/0 must match pattern' '/adversaries/0/capability must be <= 5' \
            '/timeline/events/0/owner must be equal to one of the allowed values' '/timeline/events/0/at must match pattern' \
            '/timeline/events/1 must match a schema in anyOf' '/rulesOfPlay/duration must match pattern' 'must NOT have additional properties'; do
  grep -qF -- "$want" <<<"$out" || { echo "FAIL  expected error not reported: $want"; fail=1; }
done
[ $fail -eq 0 ] && echo "ok    invalid document rejected for all 8 planted defects"
exit $fail
