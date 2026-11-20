import { Workflow, node, manualTrigger, schedule, code, sticky, ifNode, loop, readTable, batchCreate, updateRow, tableNote } from '../lib/n8n.mjs';

const BEDROCK_REGION = 'us-east-1';
const OLLAMA_URL = 'http://host.docker.internal:11434'; // as seen from the n8n container

export default function build() {
  const wf = new Workflow('Network 3 - Run Simulation', 'networkRunSimulation');
  wf.add(sticky('About', `## Network 3 - Run Simulation
Simulates one queued run from start to finish: every tick, every agent reads its inbox and decides whom to message (25 model calls at once; failed calls are asked again twice), then the interviews, then the blind judge.

- Picks the oldest \`queued\` run in sim_runs and locks it. Runs go one at a time per provider; a run whose execution died is started again after 30 minutes.
- The loop **Build prompts → Ask → Apply replies** runs once per phase (a tick, a belief checkpoint, an interview round, the judge). The sim_runs row shows the phase it is in.
- Nothing is written until the run is over: then its messages, turns and ties go to sim_messages, sim_turns and sim_ties, and the run is set to \`done\`.

Press **Execute workflow** for one run, or **activate** it: the schedule starts the next queued run every minute.

Credentials: Baserow Token on every Baserow node; AWS on Ask Bedrock. The Bedrock region (${BEDROCK_REGION}) and the Ollama URL (${OLLAMA_URL}) are set at the top of this workflow's definition.`, [-700, -420], 600, 460));

  const manual = wf.add(manualTrigger('Manual Trigger', [0, -100]));
  const every = wf.add(schedule('Every minute', '* * * * *', [0, 100]));
  const readRuns = readTable(wf, 'sim_runs', [240, 0]);
  const pick = wf.add(code('Pick run', 'net_pick_run.js', [480, 0]));
  const lock = updateRow(wf, 'sim_runs', [720, 0], { name: 'Update: sim_runs (lock)', ref: readRuns.name, rowId: '={{ $json.run.id }}', ignore: 'run' });
  const readCast = readTable(wf, 'sim_cast', [960, 0]);
  const readParticipants = readTable(wf, 'participants', [1200, 0]);
  const readNetworks = readTable(wf, 'sim_networks', [1440, 0]);
  const start = wf.add(code('Start run', 'net_start_run.js', [1680, 0]));
  const prompts = wf.add(code('Build prompts', 'net_build_prompts.js', [1920, 0]));
  const isBedrock = wf.add(ifNode('Bedrock?', "={{ $json.provider === 'bedrock' }}", [2160, 0]));
  // Bedrock is called through its Converse API directly: newer Claude models refuse `temperature` and return
  // reasoning parts that the LLM chain node cannot read.
  const askBedrock = wf.add(node('n8n-nodes-base.httpRequest', 4.2, 'Ask Bedrock', {
    method: 'POST',
    url: `=https://bedrock-runtime.${BEDROCK_REGION}.amazonaws.com/model/{{ encodeURIComponent($json.model) }}/converse`,
    authentication: 'predefinedCredentialType', nodeCredentialType: 'aws',
    sendBody: true, specifyBody: 'json', jsonBody: '={{ JSON.stringify($json.body) }}',
    options: { batching: { batch: { batchSize: 25, batchInterval: 1000 } }, timeout: 180000 },
  }, [2400, -100], { onError: 'continueRegularOutput' }));
  // Ollama is called directly too, with thinking off: a local model that thinks before every reply takes
  // about ten times as long, and a run is thousands of calls. Requests queue up inside Ollama, hence the long timeout.
  const askOllama = wf.add(node('n8n-nodes-base.httpRequest', 4.2, 'Ask Ollama', {
    method: 'POST', url: `${OLLAMA_URL}/api/chat`,
    sendBody: true, specifyBody: 'json', jsonBody: '={{ JSON.stringify($json.body) }}',
    options: { timeout: 1800000 },
  }, [2400, 140], { onError: 'continueRegularOutput' }));
  const apply = wf.add(code('Apply replies', 'net_apply_replies.js', [2740, 0]));
  const progress = updateRow(wf, 'sim_runs', [2980, -200], { name: 'Update: sim_runs (progress)', ref: readRuns.name, ignore: 'id,state,done' });
  const done = wf.add(ifNode('Done?', '={{ $json.done }}', [2980, 0]));
  const finish = wf.add(code('Finish run', 'net_finish_run.js', [3220, -20]));

  wf.connect(manual, readRuns);
  wf.connect(every, readRuns);
  wf.chain(readRuns, pick, lock, readCast, readParticipants, readNetworks, start, prompts, isBedrock);
  wf.connect(isBedrock, askBedrock, 0);
  wf.connect(isBedrock, askOllama, 1);
  wf.connect(askBedrock, apply);
  wf.connect(askOllama, apply);
  wf.connect(apply, progress);
  wf.connect(apply, done);
  wf.connect(done, finish, 0);
  wf.connect(done, prompts, 1);

  ['sim_messages', 'sim_turns', 'sim_ties'].forEach((table, i) => {
    const y = -20 + i * 200;
    const rows = wf.add(code(`Rows: ${table}`, 'net_rows.js', [3460, y], { __TABLE__: table }));
    const lp = wf.add(loop(`200 at a time: ${table}`, 200, [3700, y]));
    const bc = batchCreate(wf, table, [3940, y + 20]);
    wf.chain(finish, rows, lp);
    wf.connect(lp, bc, 1);
    wf.connect(bc, lp);
  });
  const runRow = wf.add(code('Rows: run', 'net_rows.js', [3460, 580], { __TABLE__: 'run' }));
  const update = updateRow(wf, 'sim_runs', [3700, 580], { name: 'Update: sim_runs (done)', ref: readRuns.name });
  wf.chain(finish, runRow, update);
  tableNote(wf, [-700, 80]);
  return wf;
}
