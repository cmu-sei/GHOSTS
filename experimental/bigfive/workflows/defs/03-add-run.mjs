import { Workflow, formTrigger, code, sticky, ifNode, extractXlsx, loop, readTable, createRow, batchCreate, tableNote } from '../lib/n8n.mjs';

export default function build() {
  const wf = new Workflow('Big Five 3 - Add Run', 'bigFiveAddRun');
  wf.add(sticky('About', `## Big Five 3 - Add Run
One form, two uses:
- **Queue an LLM run**: give a new Run ID, the provider (ollama or bedrock), the model id, temperature and batch size, no file. "Big Five 4 - Run Agents" picks the run up.
- **Import agent answers made elsewhere**: additionally upload a sheet with \`Participant ID\` and \`tda1..tda40\` (same layout as the human Mini-Marker sheet). The run is stored finished and can be analyzed right away.

Model ids: Bedrock takes an inference-profile id such as \`us.anthropic.claude-haiku-4-5-20251001-v1:0\`; Ollama takes a tag such as \`llama3:70b\`. A Run ID that already exists is refused.`, [-660, -260], 560, 340));

  const form = wf.add(formTrigger('Add run form', 'bigfive-add-run', 'Big Five · Add Run',
    'Queue a model run, or upload agent answers produced elsewhere. Leave the file empty to queue.', [
      { label: 'Run ID', type: 'text', required: true, placeholder: 'run-002' },
      { label: 'Provider', type: 'dropdown', required: true, options: ['bedrock', 'ollama'] },
      { label: 'Model', type: 'text', required: true, placeholder: 'us.anthropic.claude-haiku-4-5-20251001-v1:0' },
      { label: 'Temperature', type: 'number', placeholder: '0.7' },
      { label: 'Batch size', type: 'number', placeholder: '25 (Ollama) · 100 (Bedrock)' },
      { label: 'Agent answers', type: 'file', accept: '.xlsx' },
    ], [0, 0]));
  const hasFile = wf.add(ifNode('Answers file attached?', '={{ Object.keys($binary || {}).length > 0 }}', [240, 0]));
  const extract = wf.add(extractXlsx('Extract: agent answers sheet', 'Agent_answers', null, [480, -120]));
  const parse = wf.add(code('Parse agent answers', 'parse_agent_answers.js', [720, -120]));
  const readRuns = readTable(wf, 'runs', [960, 0]);
  const prepare = wf.add(code('Prepare run', 'prepare_run.js', [1200, 0]));
  const create = createRow(wf, 'runs', [1440, 0], { ref: readRuns.name });
  const turns = wf.add(code('Turn rows', 'turn_rows.js', [1680, 0]));
  const lp = wf.add(loop('200 at a time: agent_turns', 200, [1920, 0]));
  const bc = batchCreate(wf, 'agent_turns', [2160, 20]);

  wf.connect(form, hasFile);
  wf.connect(hasFile, extract, 0);
  wf.connect(hasFile, readRuns, 1);
  wf.chain(extract, parse, readRuns);
  wf.chain(readRuns, prepare, create, turns, lp);
  wf.connect(lp, bc, 1);
  wf.connect(bc, lp);
  tableNote(wf, [-660, 120]);
  return wf;
}
