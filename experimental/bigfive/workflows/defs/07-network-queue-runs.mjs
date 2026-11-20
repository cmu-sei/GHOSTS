import { Workflow, formTrigger, code, sticky, loop, readTable, batchCreate, tableNote } from '../lib/n8n.mjs';

export default function build() {
  const wf = new Workflow('Network 2 - Queue Runs', 'networkQueueRuns');
  wf.add(sticky('About', `## Network 2 - Queue Runs
Queues the runs of one model for the network experiments. "Network 3 - Run Simulation" works through the queue.

- **Label**: a short name for this model in the report, e.g. \`haiku\`. One label = one model under study.
- **Experiment**: all three, or one of them.
- **Repeats** (default 5): propagation gets that many LLM runs plus 2 no-seed control runs; structure that many; dynamics that many rounded up to an even number, alternating which claim starts at the hub.
- **Days** (default 2): 8 ticks per day.
- **Judge**: the model that labels messages. Use the same judge for every label so the models are measured the same way.

The rule arms (share with probability p, friend of a friend, random ties, Bayes, DeGroot) need no model and are computed by "Network 4 - Analyze and Report".`, [-660, -300], 560, 420));
  const form = wf.add(formTrigger('Queue runs form', 'network-queue-runs', 'Network · Queue Runs',
    'Queues the simulation runs of one model. Use the same judge model for every label.', [
      { label: 'Label', type: 'text', required: true, placeholder: 'haiku' },
      { label: 'Experiment', type: 'dropdown', required: true, options: ['all', 'propagation', 'structure', 'dynamics'] },
      { label: 'Provider', type: 'dropdown', required: true, options: ['bedrock', 'ollama'] },
      { label: 'Model', type: 'text', required: true, placeholder: 'us.anthropic.claude-haiku-4-5-20251001-v1:0' },
      { label: 'Temperature', type: 'number', placeholder: 'empty = the model default' },
      { label: 'Repeats', type: 'number', placeholder: '5' },
      { label: 'Days', type: 'number', placeholder: '2' },
      { label: 'Judge provider', type: 'dropdown', required: true, options: ['bedrock', 'ollama'] },
      { label: 'Judge model', type: 'text', required: true, placeholder: 'us.anthropic.claude-haiku-4-5-20251001-v1:0' },
    ], [0, 0]));
  const readRuns = readTable(wf, 'sim_runs', [240, 0]);
  const plan = wf.add(code('Plan runs', 'net_queue.js', [480, 0]));
  const lp = wf.add(loop('200 at a time: sim_runs', 200, [720, 0]));
  const bc = batchCreate(wf, 'sim_runs', [960, 20], { ref: readRuns.name });
  wf.chain(form, readRuns, plan, lp);
  wf.connect(lp, bc, 1);
  wf.connect(bc, lp);
  tableNote(wf, [-660, 160]);
  return wf;
}
