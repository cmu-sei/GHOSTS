import { Workflow, manualTrigger, code, sticky, readTable, toTextFile, writeFile, tableNote } from '../lib/n8n.mjs';
import { OUT_DIR } from './05-analyze-and-report.mjs';

export default function build() {
  const wf = new Workflow('Network 4 - Analyze and Report', 'networkAnalyzeAndReport');
  wf.add(sticky('About', `## Network 4 - Analyze and Report
Analyzes every \`done\` run of the network experiments and writes the report. Press **Execute workflow**; it can be run at any time and always covers everything finished so far.

Per label (model): exposure curves against the share-with-probability-p rule, interview scores, tie formation against the friend-of-a-friend and random-tie rules, belief curves against GHOSTS's Bayes update and DeGroot averaging, and whether each person's own traits predict their agent's behavior. The rule arms are simulated here, 1,000 times each, on the same saved networks.

Files, written to \`${OUT_DIR}\` inside the n8n container (\`experimental/bigfive/out\`):
- \`network-study.html\`: the report
- \`network-runs.csv\`: one row per run
- \`network-agents.csv\`: one row per label and agent, with the person's trait scores and the agent's activity`, [-660, -300], 560, 400));
  const manual = wf.add(manualTrigger('Manual Trigger', [0, 0]));
  const reads = ['sim_runs', 'sim_cast', 'sim_networks', 'sim_messages', 'sim_turns', 'sim_ties'].map((t, i) => readTable(wf, t, [240 + i * 240, 0]));
  const analyze = wf.add(code('Analyze and render', 'net_analyze.js', [1680, 0]));
  const toFile = wf.add(toTextFile('To file', [1920, 0]));
  const write = wf.add(writeFile('Write: out files', `=${OUT_DIR}/{{ $('Analyze and render').item.json.fileName }}`, [2160, 0]));
  wf.chain(manual, ...reads, analyze, toFile, write);
  tableNote(wf, [-660, 120]);
  return wf;
}
