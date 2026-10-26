// Import Human Data: turn the two spreadsheets into the rows of participants, items, human_bfi2,
// human_mini_markers and the human rows of scores. Output: ONE item holding an array per table.
// The instrument keying (facet + reverse flag per item) lives here and nowhere else; it is written to
// the `items` table and every other workflow reads it from there.
// @include lib/scoring.js

const ADJECTIVES = ['Bashful', 'Bold', 'Careless', 'Cold', 'Complex', 'Cooperative', 'Creative', 'Deep', 'Disorganized', 'Efficient',
  'Energetic', 'Envious', 'Extraverted', 'Fretful', 'Harsh', 'Imaginative', 'Inefficient', 'Intellectual', 'Jealous', 'Kind',
  'Moody', 'Organized', 'Philosophical', 'Practical', 'Quiet', 'Relaxed', 'Rude', 'Shy', 'Sloppy', 'Sympathetic',
  'Systematic', 'Talkative', 'Temperamental', 'Touchy', 'Uncreative', 'Unenvious', 'Unintellectual', 'Unsympathetic', 'Warm', 'Withdrawn'];
// Saucier (1994) Mini-Marker keying, tda1..tda40 in ADJECTIVES order: [facet, reverse].
const MM_KEYING = [
  ['extraversion', true], ['extraversion', false], ['conscientiousness', true], ['agreeableness', true], ['openness', false],
  ['agreeableness', false], ['openness', false], ['openness', false], ['conscientiousness', true], ['conscientiousness', false],
  ['extraversion', false], ['emotional_stability', true], ['extraversion', false], ['emotional_stability', true], ['agreeableness', true],
  ['openness', false], ['conscientiousness', true], ['openness', false], ['emotional_stability', true], ['agreeableness', false],
  ['emotional_stability', true], ['conscientiousness', false], ['openness', false], ['conscientiousness', false], ['extraversion', true],
  ['emotional_stability', false], ['agreeableness', true], ['extraversion', true], ['conscientiousness', true], ['agreeableness', false],
  ['conscientiousness', false], ['extraversion', false], ['emotional_stability', true], ['emotional_stability', true], ['openness', true],
  ['emotional_stability', false], ['openness', true], ['agreeableness', true], ['agreeableness', false], ['extraversion', true]];
// BFI-2 (Soto & John, 2017) keying, bfi1..bfi60: [facet, reverse]. Negative emotionality is reported as
// emotional_stability = 6 - NE by the scorer.
const BFI2_KEYING = [
  ['extraversion', false], ['agreeableness', false], ['conscientiousness', true], ['negative_emotionality', true], ['openness', true],
  ['extraversion', false], ['agreeableness', false], ['conscientiousness', true], ['negative_emotionality', true], ['openness', false],
  ['extraversion', true], ['agreeableness', true], ['conscientiousness', false], ['negative_emotionality', false], ['openness', false],
  ['extraversion', true], ['agreeableness', true], ['conscientiousness', false], ['negative_emotionality', false], ['openness', false],
  ['extraversion', false], ['agreeableness', true], ['conscientiousness', true], ['negative_emotionality', true], ['openness', true],
  ['extraversion', true], ['agreeableness', false], ['conscientiousness', true], ['negative_emotionality', true], ['openness', true],
  ['extraversion', true], ['agreeableness', false], ['conscientiousness', false], ['negative_emotionality', false], ['openness', false],
  ['extraversion', true], ['agreeableness', true], ['conscientiousness', false], ['negative_emotionality', false], ['openness', false],
  ['extraversion', false], ['agreeableness', true], ['conscientiousness', false], ['negative_emotionality', true], ['openness', true],
  ['extraversion', false], ['agreeableness', true], ['conscientiousness', true], ['negative_emotionality', true], ['openness', true],
  ['extraversion', true], ['agreeableness', false], ['conscientiousness', false], ['negative_emotionality', false], ['openness', true],
  ['extraversion', false], ['agreeableness', false], ['conscientiousness', true], ['negative_emotionality', false], ['openness', false]];

const bfiRows = $('Extract: Human Responses sheet').all().map((i) => i.json);
const lookupRows = $('Extract: bfi lookup sheet').all().map((i) => i.json);
const mmRows = $('Extract: Mini-Marker sheet').all().map((i) => i.json);

// Column lookup that ignores case and surrounding spaces ("Participant ID", "Gender", "bfi1").
const col = (row, name) => {
  const want = name.toLowerCase().replace(/[\s_]/g, '');
  for (const k of Object.keys(row)) if (k.toLowerCase().replace(/[\s_]/g, '') === want) return row[k];
  return undefined;
};
const pidOf = (row) => {
  const v = col(row, 'Participant ID') ?? col(row, 'participant_id') ?? col(row, 'pid');
  const n = Number(v);
  return v === null || v === undefined || v === '' || !Number.isInteger(n) ? null : n;
};
const text = (v) => (v === null || v === undefined ? '' : String(v).trim());

// bfi lookup: any row holding a cell "bfiN" next to the item text, whatever the headers are.
const bfiText = {};
for (const row of lookupRows) {
  const vals = Object.values(row).map(text);
  const key = vals.find((v) => /^bfi\d{1,2}$/i.test(v));
  const t = vals.find((v) => v && !/^bfi\d{1,2}$/i.test(v));
  if (key && t) bfiText[key.toLowerCase()] = t;
}
const missingText = BFI2_KEYING.map((_, i) => `bfi${i + 1}`).filter((k) => !bfiText[k]);
if (missingText.length) throw new Error(`bfi lookup sheet is missing the text for: ${missingText.join(', ')}`);

const items = [];
BFI2_KEYING.forEach(([facet, reverse], i) => {
  const item_key = `bfi${i + 1}`;
  items.push({ key: `bfi2:${item_key}`, instrument: 'bfi2', item_key, position: i + 1, text: bfiText[item_key], facet, reverse });
});
MM_KEYING.forEach(([facet, reverse], i) => {
  const item_key = `tda${i + 1}`;
  items.push({ key: `mini_markers:${item_key}`, instrument: 'mini_markers', item_key, position: i + 1, text: ADJECTIVES[i], facet, reverse });
});

const mmByPid = new Map();
for (const row of mmRows) {
  const pid = pidOf(row);
  if (pid === null) continue;
  const answers = {};
  let any = false;
  for (let i = 1; i <= 40; i++) {
    const v = toNumber(col(row, `tda${i}`));
    if (v !== null) { answers[`tda${i}`] = v; any = true; }
  }
  if (any) mmByPid.set(pid, answers);
}

const participants = [];
const human_bfi2 = [];
const human_mini_markers = [];
const scores = [];
const seen = new Set();
for (const row of bfiRows) {
  const pid = pidOf(row);
  if (pid === null || seen.has(pid)) continue;
  seen.add(pid);
  const answers = {};
  const parts = [];
  for (let i = 1; i <= 60; i++) {
    const k = `bfi${i}`;
    const v = toNumber(col(row, k));
    if (v !== null) { answers[k] = v; parts.push(`${bfiText[k]}: ${v}`); }
  }
  const mm = mmByPid.get(pid) || null;
  participants.push({
    key: String(pid),
    participant_id: pid,
    age: toNumber(col(row, 'age')),
    gender: text(col(row, 'Gender')),
    ethnicity: text(col(row, 'ethnicity')),
    persona: parts.length ? `${parts.join('; ')}.` : '',
    has_mini_markers: mm !== null,
  });
  human_bfi2.push({ key: String(pid), participant_id: pid, ...answers });
  const bfiScores = facetScores(items, 'bfi2', answers);
  if (hasAllFacets(bfiScores)) {
    scores.push({ key: `human_bfi2::${pid}`, subject: 'human_bfi2', run_id: '', participant_id: pid, ...Object.fromEntries(FACETS.map((f) => [f, round3(bfiScores[f])])) });
  }
  if (mm) {
    human_mini_markers.push({ key: String(pid), participant_id: pid, ...mm });
    const mmScores = facetScores(items, 'mini_markers', mm);
    if (hasAllFacets(mmScores)) {
      scores.push({ key: `human_mini_markers::${pid}`, subject: 'human_mini_markers', run_id: '', participant_id: pid, ...Object.fromEntries(FACETS.map((f) => [f, round3(mmScores[f])])) });
    }
  }
}
if (!participants.length) throw new Error('Human Responses sheet produced no participants; check the sheet name and the "Participant ID" column');

return [{
  json: {
    summary: `${participants.length} participants (${human_mini_markers.length} with Mini-Markers), ${items.length} items, ${scores.length} human score rows`,
    participants,
    items,
    human_bfi2,
    human_mini_markers,
    scores,
  },
}];
