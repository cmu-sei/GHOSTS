// Analyze and Report: render the per-run report and CSV for every run analyzed in this execution, plus
// all-runs.html / all-runs.csv across every summarized run. Output: one item per file { fileName, content }.
// New rows from Analyze override what the Read nodes returned for the same key.
// @include lib/scoring.js
// @include lib/stats.js
// @include lib/report.js

const rows = (node) => $(node).all().map((i) => i.json).filter((r) => r && r.id !== undefined);
const typed = (r) => Object.fromEntries(Object.entries(r).map(([k, v]) => [k, typeof v === 'string' && /^-?\d+(\.\d+)?$/.test(v) && k !== 'key' && k !== 'run_id' && k !== 'locked_by' ? Number(v) : v]));
const analysis = $('Analyze').first().json;

const merge = (existing, fresh) => {
  const byKey = new Map(existing.map((r) => [String(r.key), typed(r)]));
  for (const r of fresh) byKey.set(String(r.key), r);
  return [...byKey.values()];
};
const summaries = merge(rows('Read: run_summaries'), analysis.run_summaries);
const comparisons = merge(rows('Read: comparisons'), analysis.comparisons);
const runs = rows('Read: runs').map(typed);
const compsByRun = {};
for (const c of comparisons) (compsByRun[String(c.run_id)] = compsByRun[String(c.run_id)] || []).push(c);
for (const list of Object.values(compsByRun)) list.sort((a, b) => Number(a.participant_id) - Number(b.participant_id));

const COMPARISON_COLUMNS = ['run_id', 'participant_id', ...FACETS.flatMap((f) => [`agent_${f}`, `human_${f}`, `diff_${f}`]), 'mean_abs_diff'];
const SUMMARY_COLUMNS = ['run_id', 'model', 'provider', 'n', 'mean_abs_diff',
  ...FACETS.flatMap((f) => ['shift', 'shift_p', 'shift_p_holm', 'mae', 'r', 'r_ci_low', 'r_ci_high', 'baseline_r', 'steiger_p'].map((s) => `${f}_${s}`)), 'analyzed_at'];

const files = [];
for (const rid of analysis.analyzed) {
  const summary = summaries.find((s) => String(s.run_id) === rid);
  const comps = compsByRun[rid] || [];
  const run = runs.find((r) => String(r.run_id) === rid);
  files.push({ fileName: `${rid}.html`, content: runReport(summary, comps, run) });
  files.push({ fileName: `${rid}.csv`, content: toCsv(comps, COMPARISON_COLUMNS) });
}
if (summaries.length) {
  files.push({ fileName: 'all-runs.html', content: allRunsReport(summaries, compsByRun) });
  const ranked = [...summaries].sort((a, b) => Number(a.mean_abs_diff) - Number(b.mean_abs_diff));
  files.push({ fileName: 'all-runs.csv', content: toCsv(ranked, SUMMARY_COLUMNS) });
}
return files.map((f) => ({ json: f }));
