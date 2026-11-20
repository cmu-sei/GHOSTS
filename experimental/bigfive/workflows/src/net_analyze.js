// Analyze and Report: every done run of the network experiments -> network-study.html, network-runs.csv,
// network-agents.csv. Output: one item per file { fileName, content }.
// Rows written by an abandoned attempt of a run carry another execution id and are left out.
// @include lib/scoring.js
// @include lib/stats.js
// @include lib/graph.js
// @include lib/netstats.js
// @include lib/report.js
// @include lib/net_report.js

const TEXT = new Set(['key', 'run_id', 'execution', 'locked_by', 'text', 'raw_output', 'names', 'detail', 'edges', 'roles', 'label', 'model', 'judge_model']);
const typed = (r) => Object.fromEntries(Object.entries(r).map(([k, v]) => [k, typeof v === 'string' && !TEXT.has(k) && /^-?\d+(\.\d+)?$/.test(v) ? Number(v) : v]));
const rows = (table) => $(`Read: ${table}`).all().map((i) => i.json).filter((r) => r && r.id !== undefined).map(typed);

const runs = rows('sim_runs').filter((r) => r.status === 'done');
if (!runs.length) throw new Error('Nothing to analyze: no sim_runs row is done yet');
const cast = rows('sim_cast').sort((a, b) => a.agent - b.agent);
const analysis = netAnalyze({ runs, cast, networks: rows('sim_networks'), messages: rows('sim_messages'), turns: rows('sim_turns'), ties: rows('sim_ties') });

const runRows = networkRunRows(analysis);
const agentRows = analysis.flatMap((a) => a.traits.rows.map((r) => ({ label: a.label, model: a.model, ...r })));
return [
  { fileName: 'network-study.html', content: networkReport(analysis, cast) },
  { fileName: 'network-runs.csv', content: toCsv(runRows, Object.keys(runRows[0])) },
  { fileName: 'network-agents.csv', content: toCsv(agentRows, Object.keys(agentRows[0])) },
].map((json) => ({ json }));
