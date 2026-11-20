// Tests for the network experiments: the engine (lib/sim.js) driven by a scripted stand-in for the model,
// the analysis (lib/netstats.js) on what it produced, and the Code nodes around them.
// Run from experimental/bigfive:  node --test "workflows/src/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadScript } from '../lib/n8n.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
const LIBS = ['scoring', 'stats', 'graph', 'sim', 'netstats', 'report', 'net_report'];
const src = LIBS.map((f) => readFileSync(join(here, 'lib', `${f}.js`), 'utf8')).join('\n');
const names = [...src.matchAll(/^(?:function|const) ([A-Za-z_]\w*)/gm)].map((m) => m[1]);
export const L = new Function(`${src}\nreturn { ${names.join(', ')} };`)();

async function run(script, { input = [], nodes = {}, replacements = {} } = {}) {
  const items = input.map((json) => ({ json }));
  const $ = (name) => {
    if (!(name in nodes)) throw new Error(`test: node "${name}" not provided`);
    const out = nodes[name].map((json) => ({ json }));
    return { all: () => out, first: () => out[0] };
  };
  const fn = new AsyncFunction('$input', '$', '$execution', loadScript(script, replacements));
  return fn.call({}, { all: () => items, first: () => items[0] }, $, { id: 'exec-1' });
}

const NAMES = ['Alex', 'Avery', 'Blake', 'Casey', 'Charlie', 'Dakota', 'Devin', 'Elliot', 'Emerson', 'Finley', 'Harper', 'Hayden', 'Jamie',
  'Jordan', 'Kai', 'Logan', 'Morgan', 'Parker', 'Quinn', 'Reese', 'Robin', 'Rowan', 'Shay', 'Skyler', 'Taylor'];
export const CAST = NAMES.map((name, i) => ({
  key: `a${i + 1}`, agent: i + 1, name, participant_id: 100 + i, persona: 'Is outgoing: 3.',
  extraversion: 1 + (i % 5), agreeableness: 3, conscientiousness: 2 + (i % 3), emotional_stability: 3, openness: 4,
}));
export const NETWORKS = [
  { key: 'propagation', nodes: 25, edges: L.smallWorld(25, 4, 0.1, L.rng(2)), roles: { seed: 3, decoy: 15 } },
  { key: 'structure', nodes: 25, edges: L.smallWorld(25, 2, 0.1, L.rng(3)), roles: { seed: 5 } },
  { key: 'dynamics', nodes: 25, edges: L.prefAttach(25, 2, L.rng(4)), roles: { hub: 0, peripheral: 20 } },
];

// A scripted stand-in for the model: it passes on anything about a party, introduces its connections to
// each other, asks to meet people it has heard of, and believes the last thing it was told.
export function fakeModel(rand, share = 0.5) {
  return (prompt) => {
    const line = (label) => (prompt.match(new RegExp(`${label}: (.*)\\.`)) || [])[1] || '';
    const list = (label) => line(label).split(', ').filter((s) => s && s !== '(nobody)');
    if (prompt.startsWith('You are labelling')) {
      const texts = [...prompt.matchAll(/^\d+\. (".*")$/gm)].map((m) => JSON.parse(m[1]));
      if (prompt.includes('"stance"')) return JSON.stringify(texts.map((t, n) => ({ n: n + 1, stance: /cancelled/.test(t) ? 'off' : /is on/.test(t) ? 'on' : 'none' })));
      return JSON.stringify(texts.map((t, n) => ({ n: n + 1, carries: /party/.test(t) })));
    }
    const end = prompt.indexOf('An interviewer asks');
    const memory = prompt.slice(prompt.indexOf('About you'), end > 0 ? end : prompt.lastIndexOf('\n\n'));
    const knows = /party/.test(memory);
    if (prompt.includes('How likely is it')) return /cancelled/.test(memory.slice(memory.lastIndexOf('From'))) ? '10' : /definitely on|is on/.test(memory) ? '90' : '50';
    if (prompt.includes('Which of these people')) return JSON.stringify([...new Set([...memory.matchAll(/(?:From|To) (\w+):/g)].map((m) => m[1])), 'Marlow']);
    if (prompt.includes('Have you heard about')) return /housewarming/.test(prompt) ? 'NO, nothing.' : knows ? 'YES, it is on Saturday.' : 'NO, I have not.';
    if (prompt.includes('anything coming up')) return knows ? 'There is a birthday party on Saturday.' : 'Nothing that I know of.';
    const friends = list('Your connections');
    const out = { messages: [], connect: list('People you have heard of but are not connected with').slice(0, 1), accept: list('Connection requests waiting for your answer'), decline: [] };
    if (knows && rand() < share && friends.length) {
      const to = friends[Math.floor(rand() * friends.length)];
      const other = friends[Math.floor(rand() * friends.length)];
      out.messages.push({ to, text: /cancelled/.test(memory) ? 'I heard the party is cancelled.' : `The party is on Saturday, tell ${other}!` });
    }
    out.messages.push({ to: 'Nobody', text: 'dropped' });
    return JSON.stringify(out);
  };
}

export function simulate(run, model) {
  const network = NETWORKS.find((w) => w.key === run.experiment);
  const state = L.simStart(run, CAST, network);
  while (!L.simDone(state)) L.simApply(state, L.simPrompts(state).map((c) => model(c.prompt)));
  return state;
}

// The tables of a small finished study under one label, as the analysis reads them.
export function study(label, share, ticks = 8) {
  const base = { label, provider: 'bedrock', model: `model-${label}`, judge_provider: 'bedrock', judge_model: 'judge', temperature: 0.7, ticks };
  const plan = [
    ...[1, 2, 3, 4].map((repeat) => ({ experiment: 'propagation', arm: 'llm', condition: '', repeat })),
    { experiment: 'propagation', arm: 'control', condition: '', repeat: 1 },
    ...[1, 2].map((repeat) => ({ experiment: 'structure', arm: 'llm', condition: '', repeat })),
    ...[1, 2].map((repeat) => ({ experiment: 'dynamics', arm: 'llm', condition: repeat % 2 ? 'on_at_hub' : 'off_at_hub', repeat })),
  ];
  const tables = { runs: [], messages: [], turns: [], ties: [] };
  plan.forEach((p, n) => {
    const run = { ...base, ...p, run_id: `${label}-${p.experiment}-${p.arm}-${p.repeat}`, execution: 'exec-1' };
    const state = simulate(run, fakeModel(L.rng(n + 1 + share * 100), share));
    const rows = L.simRows(state, 'exec-1');
    tables.runs.push({ ...run, calls: state.calls, failures: state.failures });
    tables.messages.push(...rows.sim_messages);
    tables.turns.push(...rows.sim_turns);
    tables.ties.push(...rows.sim_ties);
  });
  return tables;
}
export const networkRows = () => NETWORKS.map((w) => ({ ...w, edges: JSON.stringify(w.edges), roles: JSON.stringify(w.roles) }));

test('graphs: generators are reproducible, connected, and measured correctly', () => {
  assert.deepEqual(L.smallWorld(25, 4, 0.1, L.rng(7)), L.smallWorld(25, 4, 0.1, L.rng(7)));
  assert.equal(L.smallWorld(25, 4, 0.1, L.rng(7)).length, 50);
  assert.ok(L.isConnected(25, L.smallWorld(25, 2, 0.1, L.rng(7))));
  assert.equal(L.prefAttach(25, 2, L.rng(7)).length, 3 + 22 * 2);
  const triangle = [[0, 1], [0, 2], [1, 2]];
  assert.equal(L.density(3, triangle), 1);
  assert.equal(L.clustering(3, triangle), 1);
  assert.equal(L.clustering(3, [[0, 1], [1, 2]]), 0);
  assert.deepEqual(L.distances(4, [[0, 1], [1, 2]], 0), [0, 1, 2, Infinity]);
});

test('propagation: only connections get messages, exposure follows the log, interviews are scored', () => {
  const run = { run_id: 'r', experiment: 'propagation', arm: 'llm', repeat: 1, ticks: 8, provider: 'bedrock', model: 'm', judge_provider: 'bedrock', judge_model: 'j', temperature: 0.7 };
  const state = simulate(run, fakeModel(L.rng(1), 0.6));
  assert.equal(state.phases.length, 8 + 2);
  assert.ok(state.messages.length > 5);
  for (const m of state.messages) assert.ok(L.simNeighbors(state, m.from).includes(m.to));
  assert.ok(state.messages.every((m) => m.carries_event === 'yes'));
  assert.equal(state.turns.filter((t) => t.phase === 'act').every((t) => t.invalid === 1), true);
  // Tick 1: only the seed knows, so only the seed can have sent anything.
  assert.ok(state.messages.filter((m) => m.tick === 1).every((m) => m.from === 3));
  const control = simulate({ ...run, arm: 'control' }, fakeModel(L.rng(1), 0.6));
  assert.equal(control.messages.length, 0);
  assert.equal(control.turns.filter((t) => t.question === 'cued' && t.answer === 'yes').length, 0);
  // The judge prompt never names the arm.
  assert.ok(!/control|llm/.test(L.simJudgePrompt(state, [{ text: 'hi' }])));
});

test('propagation: a failed call is a quiet turn and is counted', () => {
  const run = { run_id: 'r', experiment: 'propagation', arm: 'llm', repeat: 1, ticks: 1, provider: 'bedrock', model: 'm', judge_provider: 'bedrock', judge_model: 'j' };
  const state = L.simStart(run, CAST, NETWORKS[0]);
  L.simApply(state, L.simPrompts(state).map((_, n) => (n === 0 ? '' : n === 1 ? 'I would rather not.' : '{"messages": []}')));
  assert.equal(state.failures, 2);
  assert.equal(state.messages.length, 0);
});

test('structure: a tie needs a name seen in the inbox and both sides', () => {
  const run = { run_id: 'r', experiment: 'structure', arm: 'llm', repeat: 1, ticks: 8, provider: 'bedrock', model: 'm', judge_provider: 'bedrock', judge_model: 'j' };
  const state = simulate(run, fakeModel(L.rng(5), 0.8));
  assert.ok(state.ties.length > 0);
  assert.equal(state.edges.length, NETWORKS[1].edges.length + state.ties.length);
  assert.equal(new Set(state.edges.map(([a, b]) => L.edgeKey(a, b))).size, state.edges.length);
  for (const t of state.ties) {
    const before = state.messages.filter((m) => m.tick < t.tick && m.to === t.from && m.text.includes(CAST[t.to].name));
    assert.ok(before.length > 0, 'the requester had seen the name');
  }
  assert.equal(state.turns.filter((t) => t.question === 'names_start').length, 25);
  assert.equal(state.turns.filter((t) => t.question === 'names_end' && t.fakes === 1).length, 25);
});

test('dynamics: beliefs are asked at every checkpoint and never fed back', () => {
  const run = { run_id: 'r', experiment: 'dynamics', arm: 'llm', condition: 'on_at_hub', repeat: 1, ticks: 4, provider: 'bedrock', model: 'm', judge_provider: 'bedrock', judge_model: 'j' };
  const state = L.simStart(run, CAST, NETWORKS[2]);
  assert.match(state.agents[0].notes[1], /definitely on/);
  assert.match(state.agents[20].notes[1], /cancelled/);
  const model = fakeModel(L.rng(9), 0.9);
  while (!L.simDone(state)) {
    const calls = L.simPrompts(state);
    for (const c of calls) assert.ok(!/How likely[\s\S]*How likely/.test(c.prompt) && !/\b(90|10|50)\b\s*$/.test(c.prompt));
    L.simApply(state, calls.map((c) => model(c.prompt)));
  }
  assert.equal(state.turns.filter((t) => t.phase === 'belief').length, 25 * 5);
  assert.ok(state.messages.some((m) => m.stance === 'on'));
});

test('analysis: rule arms, interview cells and belief rules from a small study', () => {
  const tables = { ...study('fast', 0.7), cast: CAST, networks: networkRows() };
  const [a] = L.netAnalyze(tables);
  const p = a.propagation;
  assert.equal(p.llmCurve[0], 1 / 25);
  assert.ok(p.llmCurve[8] > p.llmCurve[1]);
  assert.ok(p.p_fit > 0 && p.p_fit <= 1);
  assert.ok(Math.abs(p.p_behavior - 0.7) < 0.15, `behavioural p ${p.p_behavior}`);
  assert.equal(p.fanout, 1);
  assert.equal(p.recall, 1);
  assert.equal(p.hallucinated, 0);
  assert.equal(p.control.cued_yes, 0);
  assert.ok(p.rule.low[8] <= p.rule.mean[8] && p.rule.mean[8] <= p.rule.high[8]);
  const s = a.structure;
  assert.ok(s.llm.added > 0);
  assert.ok(s.llm.density > s.start.density);
  assert.ok(Math.abs(s.rules.fof.added.mean - s.llm.added) < 2);
  assert.equal(s.rules.fof.closure.mean, 1);
  assert.ok(s.rules.random.closure.mean < 0.5);
  const d = a.dynamics;
  assert.equal(d.runs.length, 2);
  assert.equal(d.runs[0].rules.bayes[d.runs[0].on][0], 60);
  assert.ok(d.fit.degroot.rmse >= 0);
  assert.equal(a.traits.rows.length, 25);
});

test('rule spread: p = 0 never spreads, p = 1 on a pair spreads at once', () => {
  assert.deepEqual(L.ruleSpread(L.adjacency(3, [[0, 1], [1, 2]]), 0, 0, 5, L.rng(1)), [0, Infinity, Infinity]);
  assert.deepEqual(L.ruleSpread(L.adjacency(2, [[0, 1]]), 0, 1, 5, L.rng(1)), [0, 1]);
});

test('Code nodes: queue plans the runs, pick takes one at a time', async () => {
  const form = { Label: 'haiku', Experiment: 'all', Provider: 'bedrock', Model: 'm', Temperature: '', Repeats: 5, Days: '', 'Judge provider': 'bedrock', 'Judge model': 'j' };
  const rows = (await run('net_queue.js', { input: [{}], nodes: { 'Queue runs form': [form] } })).map((i) => i.json);
  assert.equal(rows.length, 5 + 2 + 5 + 6);
  assert.equal(rows[0].ticks, 16);
  assert.equal(rows[0].temperature, null);
  assert.deepEqual(rows.filter((r) => r.experiment === 'dynamics').map((r) => r.condition).slice(0, 2), ['on_at_hub', 'off_at_hub']);
  await assert.rejects(run('net_queue.js', { input: [{ run_id: 'haiku-structure-llm-1' }], nodes: { 'Queue runs form': [form] } }), /already exists/);

  const queued = rows.map((r, i) => ({ ...r, id: i + 1 }));
  const [picked] = await run('net_pick_run.js', { input: queued });
  assert.equal(picked.json.run.run_id, 'haiku-propagation-llm-1');
  const busy = [{ ...queued[0], status: 'running', locked_at: new Date().toISOString() }, ...queued.slice(1)];
  assert.deepEqual(await run('net_pick_run.js', { input: busy }), []);
  const local = { ...queued[1], id: 99, provider: 'ollama' };
  assert.equal((await run('net_pick_run.js', { input: [...busy, local] }))[0].json.run.id, 99);
  const stale = [{ ...queued[0], status: 'running', locked_at: '2020-01-01T00:00:00+00:00' }, ...queued.slice(1)];
  assert.equal((await run('net_pick_run.js', { input: stale }))[0].json.run.run_id, 'haiku-propagation-llm-1');
});

test('Code nodes: set up picks 25 people spanning the traits, the same every time', async () => {
  const rand = L.rng(3);
  const participants = Array.from({ length: 80 }, (_, i) => ({ id: i, participant_id: i + 1, persona: 'x', has_mini_markers: true }));
  const scores = participants.map((p) => ({ id: p.id, subject: 'human_bfi2', participant_id: p.participant_id, ...Object.fromEntries(L.FACETS.map((f) => [f, String(1 + 4 * rand())])) }));
  const go = () => run('net_setup.js', { nodes: { 'Set up form': [{ Seed: 1 }], 'Read: participants': participants, 'Read: scores': scores } });
  const [{ json }] = await go();
  assert.equal(json.sim_cast.length, 25);
  assert.equal(new Set(json.sim_cast.map((c) => c.participant_id)).size, 25);
  assert.deepEqual((await go())[0].json, json);
  const spreadOf = (list) => Math.max(...list.map((c) => Number(c.extraversion))) - Math.min(...list.map((c) => Number(c.extraversion)));
  assert.ok(spreadOf(json.sim_cast) > 3);
  const roles = Object.fromEntries(json.sim_networks.map((w) => [w.key, JSON.parse(w.roles)]));
  assert.notEqual(roles.propagation.seed, roles.propagation.decoy);
  assert.notEqual(roles.dynamics.hub, roles.dynamics.peripheral);
});

test('Code nodes: the run loop carries the state from Build prompts to Apply replies to Finish', async () => {
  const runRow = { id: 7, run_id: 'x', experiment: 'propagation', arm: 'llm', repeat: 1, ticks: 1, provider: 'bedrock', model: 'm', judge_provider: 'bedrock', judge_model: 'j', temperature: 0.7 };
  const nodes = { 'Pick run': [{ run: runRow }], 'Read: participants': CAST.map((c, i) => ({ id: i, participant_id: c.participant_id, persona: c.persona })), 'Read: sim_cast': CAST.map((c, i) => ({ id: i, ...c })), 'Read: sim_networks': networkRows().map((w, i) => ({ id: i, ...w })) };
  let [item] = await run('net_start_run.js', { nodes });
  const model = fakeModel(L.rng(1), 1);
  for (let guard = 0; guard < 10 && !item.json.done; guard++) {
    const built = await run('net_build_prompts.js', { input: [item.json] });
    assert.equal(built[0].json.model, guard === 2 ? 'j' : 'm');
    assert.equal(built.length, [25, 75, 2][guard]);
    assert.equal(built[0].json.body.think, undefined);
    // Bedrock-shaped replies with a reasoning part; in the first phase one call fails once and is asked again.
    const reply = (b) => ({ output: { message: { content: [{ reasoningContent: {} }, { text: model(b.json.prompt) }] } } });
    const flaky = guard === 0;
    [item] = await run('net_apply_replies.js', { input: built.map((b, k) => (flaky && k === 3 ? { error: 'throttled' } : reply(b))), nodes: { 'Build prompts': built.map((b) => b.json) } });
    if (flaky) {
      assert.equal(item.json.progress, 'act 1/1');
      const again = await run('net_build_prompts.js', { input: [item.json] });
      assert.deepEqual(again.map((b) => b.json.n), [3]);
      assert.equal(again[0].json.body.inferenceConfig.temperature, 0.7);
      // The retry comes back Ollama-shaped.
      [item] = await run('net_apply_replies.js', { input: again.map((b) => ({ message: { content: model(b.json.prompt) } })), nodes: { 'Build prompts': again.map((b) => b.json) } });
    }
  }
  assert.equal(item.json.done, true);
  assert.equal(item.json.id, 7);
  const [finished] = await run('net_finish_run.js', { input: [item.json] });
  assert.equal(finished.json.run.status, 'done');
  assert.equal(finished.json.sim_turns.length, 25 + 75 + 2);
  assert.ok(finished.json.sim_messages.every((m) => m.execution === 'exec-1'));
  const rows = await run('net_rows.js', { nodes: { 'Finish run': [finished.json] }, replacements: { __TABLE__: 'run' } });
  assert.equal(rows[0].json.id, 7);
});

test('report: renders every section for two models and writes the three files', async () => {
  const a = study('fast', 0.7);
  const b = study('slow', 0.25);
  const id = (list) => list.map((r, i) => ({ id: i + 1, ...r }));
  const stringly = (list) => id(list).map((r) => Object.fromEntries(Object.entries(r).map(([k, v]) => [k, typeof v === 'number' && k !== 'id' ? String(v) : v])));
  const nodes = {
    'Read: sim_runs': stringly([...a.runs, ...b.runs].map((r) => ({ ...r, status: 'done' }))),
    'Read: sim_cast': stringly(CAST), 'Read: sim_networks': id(networkRows()),
    'Read: sim_messages': stringly([...a.messages, ...b.messages, { ...a.messages[0], execution: 'dead-attempt', carries_event: 'no' }]),
    'Read: sim_turns': stringly([...a.turns, ...b.turns]), 'Read: sim_ties': stringly([...a.ties, ...b.ties]),
  };
  const files = Object.fromEntries((await run('net_analyze.js', { nodes })).map((i) => [i.json.fileName, i.json.content]));
  assert.deepEqual(Object.keys(files), ['network-study.html', 'network-runs.csv', 'network-agents.csv']);
  const html = files['network-study.html'];
  for (const s of ['1 · Propagation', '2 · Structure', '3 · Social dynamics', '4 · The person behind the agent', 'FIG. 7']) assert.ok(html.includes(s), s);
  assert.ok(!/NaN|undefined|Infinity/.test(html), (html.match(/.{40}(NaN|undefined|Infinity).{40}/) || [])[0]);
  assert.equal(files['network-runs.csv'].trim().split('\n').length, 1 + 18);
  assert.equal(files['network-agents.csv'].trim().split('\n').length, 1 + 50);
  if (process.env.NETWORK_REPORT_OUT) (await import('node:fs')).writeFileSync(process.env.NETWORK_REPORT_OUT, html);
});
