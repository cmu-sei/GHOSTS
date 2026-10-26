import { Workflow, node, manualTrigger, schedule, code, sticky, ifNode, loop, readTable, createRow, updateRow, tableNote } from '../lib/n8n.mjs';

export default function build() {
  const wf = new Workflow('Big Five 4 - Run Agents', 'bigFiveRunAgents');
  wf.add(sticky('About', `## Big Five 4 - Run Agents
Answers the 40 Mini-Markers for one batch of participants of the current run, as the persona of each participant.

- Picks the \`running\` run, else the oldest \`queued\` one, and locks it (\`locked_at\`/\`locked_by\`; a lock older than 30 min is abandoned).
- Asks the model for \`batch_size\` participants that have no agent_turns row yet; prompt **mm-v1** (Build prompt).
- Writes one agent_turns row per participant: raw reply, parse result, and tda1..tda40 when all 40 came back clean.
- Refreshes the lock after every participant; when nobody is left sets the run to \`done\`.

Press **Execute workflow** for one batch, or **activate** for a batch every 10 minutes. Set a run's status to \`paused\` to stop after the current batch. To re-ask failed participants, delete their agent_turns rows and set the run back to \`queued\`.

Credentials: Baserow Token on every Baserow node; AWS on the Bedrock model; Ollama (base URL reachable from the n8n container, Aspire: http://host.ollama:11434) on the Ollama model.`, [-700, -420], 600, 460));

  const manual = wf.add(manualTrigger('Manual Trigger', [0, -100]));
  const every = wf.add(schedule('Every 10 minutes', '*/10 * * * *', [0, 100]));
  const config = wf.add(code('Configuration', 'configuration.js', [240, 0]));
  const readRuns = readTable(wf, 'runs', [480, 0]);
  const pick = wf.add(code('Pick run', 'pick_run.js', [720, 0]));
  const lock = updateRow(wf, 'runs', [960, 0], { name: 'Update: runs (lock)', ref: readRuns.name, rowId: '={{ $json.run.id }}', ignore: 'run' });
  const readParticipants = readTable(wf, 'participants', [1200, 0]);
  const readTurns = readTable(wf, 'agent_turns', [1440, 0]);
  const prepare = wf.add(code('Prepare batch', 'prepare_batch.js', [1680, 0]));
  const noWork = wf.add(ifNode('No work?', '={{ $json.noWork }}', [1920, 0]));
  const lp = wf.add(loop('Loop Over Items', 1, [2160, 100]));
  const prompt = wf.add(code('Build prompt', 'build_prompt.js', [2400, 200]));
  const isBedrock = wf.add(ifNode('Bedrock?', "={{ $json.provider === 'bedrock' }}", [2640, 200]));
  const askBedrock = wf.add(node('@n8n/n8n-nodes-langchain.chainLlm', 1.7, 'Ask Bedrock', { promptType: 'define', text: '={{ $json.prompt }}', hasOutputParser: false, batching: {} }, [2880, 100]));
  const askOllama = wf.add(node('@n8n/n8n-nodes-langchain.chainLlm', 1.7, 'Ask Ollama', { promptType: 'define', text: '={{ $json.prompt }}', hasOutputParser: false, batching: {} }, [2880, 320]));
  const bedrockModel = wf.add(node('@n8n/n8n-nodes-langchain.lmChatAwsBedrock', 1.2, 'AWS Bedrock Chat Model', {
    model: "={{ $('Build prompt').first().json.model }}",
    options: { temperature: "={{ $('Build prompt').first().json.temperature ?? 0.7 }}", maxTokensToSample: 1500, timeout: 180000, maxRetries: 2 },
  }, [2900, 260]));
  const ollamaModel = wf.add(node('@n8n/n8n-nodes-langchain.lmChatOllama', 1, 'Ollama Chat Model', {
    model: "={{ $('Build prompt').first().json.model }}",
    options: { temperature: "={{ $('Build prompt').first().json.temperature ?? 0.7 }}" },
  }, [2900, 480]));
  const parse = wf.add(code('Parse answer', 'parse_answer.js', [3220, 200], {}, { onError: 'continueRegularOutput' }));
  const writeTurn = createRow(wf, 'agent_turns', [3460, 200], { ref: readTurns.name });
  const heartbeat = wf.add(code('Heartbeat', 'heartbeat.js', [3700, 200]));
  const touch = updateRow(wf, 'runs', [3940, 200], { name: 'Update: runs (heartbeat)', ref: readRuns.name });
  const recount = readTable(wf, 'agent_turns', [2400, -200], { name: 'Read: agent_turns (recount)', ref: readTurns.name });
  const recheck = readTable(wf, 'runs', [2640, -200], { name: 'Read: runs (recheck)', ref: readRuns.name });
  const finish = wf.add(code('Finish run', 'finish_run.js', [2880, -200]));
  const update = updateRow(wf, 'runs', [3120, -200], { name: 'Update: runs (finish)', ref: readRuns.name });

  wf.connect(manual, config);
  wf.connect(every, config);
  wf.chain(config, readRuns, pick, lock, readParticipants, readTurns, prepare, noWork);
  wf.connect(noWork, recount, 0);
  wf.connect(noWork, lp, 1);
  wf.connect(lp, recount, 0);
  wf.connect(lp, prompt, 1);
  wf.connect(prompt, isBedrock);
  wf.connect(isBedrock, askBedrock, 0);
  wf.connect(isBedrock, askOllama, 1);
  wf.connect(bedrockModel, askBedrock, 0, 'ai_languageModel');
  wf.connect(ollamaModel, askOllama, 0, 'ai_languageModel');
  wf.connect(askBedrock, parse);
  wf.connect(askOllama, parse);
  wf.chain(parse, writeTurn, heartbeat, touch, lp);
  wf.chain(recount, recheck, finish, update);
  for (const n of [askBedrock, askOllama]) n.onError = 'continueRegularOutput';
  tableNote(wf, [-700, 80]);
  return wf;
}
