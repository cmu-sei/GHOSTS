// Analyze and Report: score the agent answers of the selected runs, compare each participant's agent
// with their own human Mini-Markers, and summarize the run with its statistics. Output: ONE item with
// the rows to upsert (scores, comparisons, run_summaries), the runs to stamp, and the list of analyzed runs.
// Selection: the form's Run ID, or every `done` run without a run_summaries row.
// @include lib/scoring.js
// @include lib/stats.js

const rows = (node) => $(node).all().map((i) => i.json).filter((r) => r && r.id !== undefined);
const typed = (r) => Object.fromEntries(Object.entries(r).map(([k, v]) => [k, typeof v === 'string' && /^-?\d+(\.\d+)?$/.test(v) && k !== 'key' && k !== 'run_id' && k !== 'locked_by' ? Number(v) : v]));
const form = $('Analyze form').first().json;
const requested = String(Object.entries(form).find(([k]) => /run/i.test(k))?.[1] ?? '').trim();

const runs = rows('Read: runs').map(typed);
const items = rows('Read: items');
const scores = rows('Read: scores').map(typed);
const turns = rows('Read: agent_turns').map(typed);
const summaries = rows('Read: run_summaries').map(typed);

let selected;
if (requested) {
  selected = runs.filter((r) => String(r.run_id) === requested);
  if (!selected.length) throw new Error(`Run "${requested}" does not exist in the runs table`);
} else {
  // Every done run with no summary, or whose summary predates the run's finish (e.g. analyzed while still running).
  const summarizedAt = new Map(summaries.map((s) => [String(s.run_id), Date.parse(s.analyzed_at) || 0]));
  selected = runs.filter((r) => r.status === 'done' && (!summarizedAt.has(String(r.run_id)) || summarizedAt.get(String(r.run_id)) < (Date.parse(r.finished_at) || 0)));
  if (!selected.length && !summaries.length) throw new Error('Nothing to analyze: no finished run without a summary, and no summaries yet. Enter a Run ID to analyze a run explicitly.');
}

const humanMM = new Map(scores.filter((s) => s.subject === 'human_mini_markers').map((s) => [String(s.participant_id), s]));
const humanBFI = new Map(scores.filter((s) => s.subject === 'human_bfi2').map((s) => [String(s.participant_id), s]));
if (!humanMM.size) throw new Error('No human_mini_markers rows in scores; run "Big Five 2 - Import Human Data" first');

// p-values are stored with 6 decimals (the run_summaries p columns allow 6); anything smaller reads as 0 = "< .000001".
const round6 = (x) => (x === null || x === undefined ? null : Math.round(x * 1e6) / 1e6);
const iso = new Date().toISOString().replace(/\.\d{3}Z$/, '+00:00');
const outScores = [];
const outComparisons = [];
const outSummaries = [];
const runUpdates = [];
const notes = [];

for (const run of selected) {
  const rid = String(run.run_id);
  const latest = new Map();
  for (const t of turns) {
    if (String(t.run_id) !== rid || t.ok !== true) continue;
    const pid = String(t.participant_id);
    const prev = latest.get(pid);
    if (!prev || String(t.answered_at || '') >= String(prev.answered_at || '')) latest.set(pid, t);
  }
  if (!latest.size) {
    notes.push(`${rid}: no usable agent turns, skipped`);
    continue;
  }
  const agent = new Map();
  for (const [pid, t] of latest) {
    const answers = {};
    for (let i = 1; i <= 40; i++) answers[`tda${i}`] = t[`tda${i}`];
    const s = facetScores(items, 'mini_markers', answers);
    if (!hasAllFacets(s)) continue;
    agent.set(pid, s);
    outScores.push({ key: `agent:${rid}:${pid}`, subject: 'agent', run_id: rid, participant_id: Number(pid), ...Object.fromEntries(FACETS.map((f) => [f, round3(s[f])])) });
  }

  const pairs = [...agent.keys()].filter((pid) => humanMM.has(pid)).sort((a, b) => Number(a) - Number(b));
  const comps = pairs.map((pid) => {
    const a = agent.get(pid);
    const h = humanMM.get(pid);
    const row = { key: `${rid}:${pid}`, run_id: rid, participant_id: Number(pid) };
    const abs = [];
    for (const f of FACETS) {
      row[`agent_${f}`] = round3(a[f]);
      row[`human_${f}`] = round3(Number(h[f]));
      row[`diff_${f}`] = round3(a[f] - Number(h[f]));
      abs.push(Math.abs(a[f] - Number(h[f])));
    }
    row.mean_abs_diff = round3(mean(abs));
    return row;
  });
  outComparisons.push(...comps);

  const summary = { key: rid, run_id: rid, model: run.model || '', provider: run.provider || '', n: comps.length, mean_abs_diff: round3(mean(comps.map((c) => c.mean_abs_diff))), analyzed_at: iso };
  const shiftPs = [];
  for (const f of FACETS) {
    const a = comps.map((c) => c[`agent_${f}`]);
    const h = comps.map((c) => c[`human_${f}`]);
    const r = pearson(a, h);
    const [lo, hi] = fisherCI(r, comps.length);
    const t = pairedT(a, h);
    summary[`${f}_shift`] = round3(mean(comps.map((c) => c[`diff_${f}`])));
    summary[`${f}_mae`] = round3(mean(comps.map((c) => Math.abs(c[`diff_${f}`]))));
    summary[`${f}_r`] = round3(r);
    summary[`${f}_r_ci_low`] = round3(lo);
    summary[`${f}_r_ci_high`] = round3(hi);
    summary[`${f}_shift_p`] = round6(t.p);
    shiftPs.push(t.p);
    // Baseline: how well the human's own BFI-2 predicts their Mini-Markers, on the same participants.
    const withBfi = pairs.filter((pid) => humanBFI.has(pid));
    const bfi = withBfi.map((pid) => Number(humanBFI.get(pid)[f]));
    const hm = withBfi.map((pid) => Number(humanMM.get(pid)[f]));
    const ag = withBfi.map((pid) => agent.get(pid)[f]);
    const baseline = pearson(bfi, hm);
    summary[`${f}_baseline_r`] = round3(baseline);
    const st = steiger(pearson(ag, hm), baseline, pearson(ag, bfi), withBfi.length);
    summary[`${f}_steiger_p`] = round6(st.p);
  }
  holm(shiftPs).forEach((p, i) => { summary[`${FACETS[i]}_shift_p_holm`] = round6(p); });
  outSummaries.push(summary);
  runUpdates.push({ id: run.id, analyzed_at: iso });
  notes.push(`${rid}: ${agent.size} agent scores, ${comps.length} compared, MAE ${summary.mean_abs_diff}`);
}

return [{
  json: {
    summary: notes.join('; ') || 'no run analyzed; reports regenerated from existing summaries',
    analyzed: outSummaries.map((s) => s.run_id),
    scores: outScores,
    comparisons: outComparisons,
    run_summaries: outSummaries,
    runs: runUpdates,
  },
}];
