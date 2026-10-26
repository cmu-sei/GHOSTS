// Shared scoring (spliced into Code nodes by build.mjs via `// @include lib/scoring.js`).
// facet = mean of its items; a reverse item is scale_max + 1 - value; BFI-2 negative emotionality is
// reported as emotional_stability = 6 - NE. `items` are rows of the `items` table.
const FACETS = ['extraversion', 'agreeableness', 'conscientiousness', 'emotional_stability', 'openness'];
const SCALE_MAX = { bfi2: 5, mini_markers: 9 };

function toNumber(v) {
  if (v === null || v === undefined || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

function isTrue(v) {
  return v === true || v === 'true' || v === 1 || v === '1';
}

// answers: { item_key: value }. Returns { facet: mean } with null for a facet that has no answers.
function facetScores(items, instrument, answers) {
  const max = SCALE_MAX[instrument];
  const sums = {};
  for (const it of items) {
    if (it.instrument !== instrument) continue;
    const v = toNumber(answers[it.item_key]);
    if (v === null) continue;
    const scored = isTrue(it.reverse) ? max + 1 - v : v;
    (sums[it.facet] = sums[it.facet] || []).push(scored);
  }
  const out = {};
  for (const f of FACETS) out[f] = null;
  for (const [facet, vals] of Object.entries(sums)) {
    const mean = vals.reduce((a, b) => a + b, 0) / vals.length;
    if (facet === 'negative_emotionality') out.emotional_stability = 6 - mean;
    else if (FACETS.includes(facet)) out[facet] = mean;
  }
  return out;
}

function hasAllFacets(scores) {
  return FACETS.every((f) => scores[f] !== null && scores[f] !== undefined);
}

function round3(x) {
  return x === null || x === undefined ? null : Math.round(x * 1000) / 1000;
}
