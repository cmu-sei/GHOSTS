import { Workflow, formTrigger, code, sticky, readTable, updateRow, upsert, toTextFile, writeFile, tableNote } from '../lib/n8n.mjs';

export const OUT_DIR = '/home/node/bigfive-out';

export default function build() {
  const wf = new Workflow('Big Five 5 - Analyze and Report', 'bigFiveAnalyzeAndReport');
  wf.add(sticky('About', `## Big Five 5 - Analyze and Report
Scores the agent answers of a run, compares every participant's agent with that participant's own Mini-Markers, and writes the reports.

Form: a **Run ID** to (re)analyze that run, or empty to analyze every \`done\` run that has no summary yet.

Per run: agent rows in scores, one comparisons row per participant, one run_summaries row (per facet: mean shift with paired-t p and Holm correction, MAE, r with Fisher 95% CI, the BFI-2 baseline r and Steiger's p against it), and \`runs.analyzed_at\`.

Files, written to \`${OUT_DIR}\` inside the n8n container (bind-mounted from \`experimental/bigfive/out\` by the Aspire AppHost):
- \`<run_id>.html\` and \`<run_id>.csv\` for each run analyzed in this execution
- \`all-runs.html\` and \`all-runs.csv\` across every summarized run, regenerated every time`, [-660, -300], 560, 400));

  const form = wf.add(formTrigger('Analyze form', 'bigfive-analyze', 'Big Five · Analyze and Report',
    'Enter a Run ID to analyze that run, or leave empty to analyze every finished run without a summary. Reports are rewritten either way.', [
      { label: 'Run ID', type: 'text', placeholder: 'run-001 (optional)' },
    ], [0, 0]));
  const readRuns = readTable(wf, 'runs', [240, 0]);
  const readItems = readTable(wf, 'items', [480, 0]);
  const readScores = readTable(wf, 'scores', [720, 0]);
  const readTurns = readTable(wf, 'agent_turns', [960, 0]);
  const readComparisons = readTable(wf, 'comparisons', [1200, 0]);
  const readSummaries = readTable(wf, 'run_summaries', [1440, 0]);
  const analyze = wf.add(code('Analyze', 'analyze_runs.js', [1680, 0]));
  wf.chain(form, readRuns, readItems, readScores, readTurns, readComparisons, readSummaries, analyze);

  upsert(wf, 'scores', 'Analyze', [1680, 300], { read: readScores, from: analyze });
  upsert(wf, 'comparisons', 'Analyze', [1680, 620], { read: readComparisons, from: analyze });
  upsert(wf, 'run_summaries', 'Analyze', [1680, 940], { read: readSummaries, from: analyze });

  const stamps = wf.add(code('Run stamps', 'run_stamps.js', [1900, 1200]));
  const stampRuns = updateRow(wf, 'runs', [2120, 1200], { name: 'Update: runs (analyzed_at)', ref: readRuns.name });
  wf.chain(analyze, stamps, stampRuns);

  const render = wf.add(code('Render reports', 'render_reports.js', [1900, 1400]));
  const toFile = wf.add(toTextFile('To file', [2120, 1400]));
  const write = wf.add(writeFile('Write: out files', `=${OUT_DIR}/{{ $('Render reports').item.json.fileName }}`, [2340, 1400]));
  wf.chain(analyze, render, toFile, write);
  tableNote(wf, [-660, 120]);
  return wf;
}
