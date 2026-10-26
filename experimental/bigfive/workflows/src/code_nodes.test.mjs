// Tests for the n8n Code-node scripts and the shared libs. Each script is the exact jsCode of one node
// (includes spliced, placeholders replaced, as build.mjs does); here it runs against fake $input / $() objects.
// Run from experimental/bigfive:  node --test "workflows/src/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadScript } from '../lib/n8n.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;

// Run a script. nodes: { name: [json, ...] | { items: [...], executed: false } }.
async function run(script, { input = [], nodes = {}, binary = {}, executionId = 'exec-1', replacements = {}, helpers = {} } = {}) {
  const items = input.map((json) => ({ json }));
  if (items[0] && Object.keys(binary).length) items[0].binary = binary;
  const $execution = { id: executionId };
  const $input = { all: () => items, first: () => items[0], last: () => items[items.length - 1] };
  const $ = (name) => {
    if (!(name in nodes)) throw new Error(`test: node "${name}" not provided`);
    const spec = Array.isArray(nodes[name]) ? { items: nodes[name], executed: true } : nodes[name];
    const out = (spec.items || []).map((json) => ({ json }));
    if (spec.binary && out[0]) out[0].binary = spec.binary;
    return {
      isExecuted: spec.executed !== false,
      all: () => { if (spec.executed === false) throw new Error(`test: node "${name}" did not execute`); return out; },
      first: () => { if (spec.executed === false) throw new Error(`test: node "${name}" did not execute`); return out[0]; },
      last: () => out[out.length - 1],
      item: out[0],
      params: spec.params || {},
    };
  };
  const fn = new AsyncFunction('$input', '$', '$execution', loadScript(script, replacements));
  return fn.call({ helpers }, $input, $, $execution);
}

// Expose the functions of a lib file for direct testing.
function lib(file) {
  const names = [...readFileSync(join(here, file), 'utf8').matchAll(/^(?:function|const) ([A-Za-z_]\w*)/gm)].map((m) => m[1]);
  return new Function(`${readFileSync(join(here, file), 'utf8')}\nreturn { ${names.join(', ')} };`)();
}

const ADJECTIVES = ['Bashful', 'Bold', 'Careless', 'Cold', 'Complex', 'Cooperative', 'Creative', 'Deep', 'Disorganized', 'Efficient',
  'Energetic', 'Envious', 'Extraverted', 'Fretful', 'Harsh', 'Imaginative', 'Inefficient', 'Intellectual', 'Jealous', 'Kind',
  'Moody', 'Organized', 'Philosophical', 'Practical', 'Quiet', 'Relaxed', 'Rude', 'Shy', 'Sloppy', 'Sympathetic',
  'Systematic', 'Talkative', 'Temperamental', 'Touchy', 'Uncreative', 'Unenvious', 'Unintellectual', 'Unsympathetic', 'Warm', 'Withdrawn'];
const answer = (lines) => lines.join('\n');
const CONFIG = { lockMinutes: 30, promptVersion: 'mm-v1' };

// Fake spreadsheets: 3 participants, participant 3 without Mini-Markers.
const bfiSheet = [1, 2, 3].map((pid) => ({ 'Participant ID': pid, age: 20 + pid, Gender: 'F', ethnicity: 'x', ...Object.fromEntries(Array.from({ length: 60 }, (_, i) => [`bfi${i + 1}`, ((i + pid) % 5) + 1])) }));
const lookupSheet = [{ 'BPI-2 Questions': 'BPI-2 Questions', __EMPTY: null }, ...Array.from({ length: 60 }, (_, i) => ({ 'BPI-2 Questions': `bfi${i + 1}`, __EMPTY: `Item ${i + 1}` }))];
const mmSheet = [1, 2, 3].map((pid) => ({ 'Participant ID': pid, ...(pid === 3 ? {} : Object.fromEntries(Array.from({ length: 40 }, (_, i) => [`tda${i + 1}`, ((i * pid) % 9) + 1]))) }));
const importNodes = { 'Extract: Human Responses sheet': bfiSheet, 'Extract: bfi lookup sheet': lookupSheet, 'Extract: Mini-Marker sheet': mmSheet };

// ---- libs --------------------------------------------------------------------------------------
test('scoring: reverse items, BFI-2 emotional stability, missing facets', () => {
  const { facetScores, hasAllFacets } = lib('lib/scoring.js');
  const items = [
    { instrument: 'mini_markers', item_key: 'tda1', facet: 'extraversion', reverse: 'true' },
    { instrument: 'mini_markers', item_key: 'tda2', facet: 'extraversion', reverse: false },
    { instrument: 'bfi2', item_key: 'bfi4', facet: 'negative_emotionality', reverse: true },
    { instrument: 'bfi2', item_key: 'bfi9', facet: 'negative_emotionality', reverse: false },
  ];
  const mm = facetScores(items, 'mini_markers', { tda1: '2', tda2: 6 });
  assert.equal(mm.extraversion, (8 + 6) / 2);
  assert.equal(mm.openness, null);
  assert.equal(hasAllFacets(mm), false);
  const bfi = facetScores(items, 'bfi2', { bfi4: 5, bfi9: 1 });
  assert.equal(bfi.emotional_stability, 6 - (1 + 1) / 2);
});

test('scoring: reproduces the shipped human facet scores of run-001.csv when the data is present', () => {
  const comps = join(here, '..', '..', 'out', 'run-001.csv');
  const responses = join(here, '..', '..', 'out', 'human_responses.csv');
  const itemsCsv = join(here, '..', '..', 'out', 'items.csv');
  if (!existsSync(comps) || !existsSync(responses) || !existsSync(itemsCsv)) return;
  const { facetScores } = lib('lib/scoring.js');
  const parse = (p) => { const [h, ...rows] = readFileSync(p, 'utf8').trim().split(/\r?\n/); const cols = h.split(','); return rows.map((r) => Object.fromEntries(r.split(',').map((v, i) => [cols[i], v]))); };
  const items = parse(itemsCsv).filter((i) => i.instrument === 'mini_markers');
  const human = {};
  for (const r of parse(responses)) if (r.instrument === 'mini_markers') (human[r.participant_id] = human[r.participant_id] || {})[r.item_key] = Number(r.value);
  for (const c of parse(comps).slice(0, 25)) {
    const s = facetScores(items, 'mini_markers', human[c.participant_id]);
    for (const f of ['extraversion', 'agreeableness', 'conscientiousness', 'emotional_stability', 'openness']) assert.ok(Math.abs(s[f] - Number(c[`human_${f}`])) < 1e-9, `${c.participant_id} ${f}`);
  }
});

test('stats: pearson, Fisher CI, t p-value, Holm, Steiger', () => {
  const { pearson, fisherCI, tTwoSidedP, pairedT, holm, steiger, normalCdf } = lib('lib/stats.js');
  assert.ok(Math.abs(pearson([1, 2, 3, 4, 5], [2, 4, 6, 8, 10]) - 1) < 1e-12);
  assert.ok(Math.abs(pearson([1, 2, 3, 4, 5], [1, 3, 2, 5, 4]) - 0.8) < 1e-12);
  const [lo, hi] = fisherCI(0.5, 103); // z = .5493 ± 1.96/10 → tanh(.3533)=.3393, tanh(.7453)=.6324
  assert.ok(Math.abs(lo - 0.3393) < 1e-3 && Math.abs(hi - 0.6324) < 1e-3, `${lo} ${hi}`);
  assert.ok(Math.abs(tTwoSidedP(2.0, 10) - 0.07339) < 1e-4);
  assert.ok(Math.abs(tTwoSidedP(2.228, 10) - 0.05) < 1e-3);
  assert.ok(Math.abs(normalCdf(1.959964) - 0.975) < 1e-6);
  const t = pairedT([2, 4, 6, 8, 11], [1, 2, 3, 4, 5]);
  assert.ok(Math.abs(t.mean - 3.2) < 1e-12 && t.df === 4 && t.p > 0.01 && t.p < 0.03, JSON.stringify(t)); // t = 3.72, df 4 → p ≈ .020
  assert.deepEqual(holm([0.01, 0.04, 0.03, null]).map((p) => (p === null ? null : Number(p.toFixed(3)))), [0.03, 0.06, 0.06, null]);
  const st = steiger(0.8, 0.5, 0.4, 100);
  assert.ok(st.z > 0 && st.p < 0.01);
  assert.equal(steiger(0.5, 0.5, 0.3, 100).z, 0);
});

// ---- Import Human Data ---------------------------------------------------------------------------
test('normalize_humans: participants, personas, items, wide tables, human scores', async () => {
  const [{ json }] = await run('normalize_humans.js', { nodes: importNodes });
  assert.equal(json.participants.length, 3);
  assert.equal(json.participants[2].has_mini_markers, false);
  assert.ok(json.participants[0].persona.startsWith('Item 1: 2; Item 2: 3;'));
  assert.ok(json.participants[0].persona.endsWith('.'));
  assert.equal(json.items.length, 100);
  assert.equal(json.items.filter((i) => i.instrument === 'mini_markers' && i.reverse).length, 20);
  assert.deepEqual(json.items.filter((i) => i.instrument === 'mini_markers').map((i) => i.text), ADJECTIVES);
  assert.equal(json.human_bfi2.length, 3);
  assert.equal(json.human_bfi2[0].bfi60, ((59 + 1) % 5) + 1);
  assert.equal(json.human_mini_markers.length, 2);
  assert.equal(json.scores.filter((s) => s.subject === 'human_bfi2').length, 3);
  assert.equal(json.scores.filter((s) => s.subject === 'human_mini_markers').length, 2);
  assert.equal(json.scores[0].key, 'human_bfi2::1');
  assert.equal(json.scores[0].run_id, '');
});

test('normalize_humans: refuses when the lookup sheet misses an item', async () => {
  await assert.rejects(run('normalize_humans.js', { nodes: { ...importNodes, 'Extract: bfi lookup sheet': lookupSheet.slice(0, 30) } }), /missing the text for: bfi30/);
});

test('upsert_split: new vs changed rows, loose value comparison', async () => {
  const staged = [{ key: '1', participant_id: 1, age: 27, has_mini_markers: true }, { key: '2', participant_id: 2, age: 30, has_mini_markers: false }, { key: '3', participant_id: 3, age: null, has_mini_markers: true }];
  const existing = [{ id: 11, key: '1', participant_id: '1', age: '27', has_mini_markers: true }, { id: 12, key: '2', participant_id: '2', age: '31', has_mini_markers: false }];
  const nodes = { Normalize: [{ participants: staged }], 'Read: participants': existing };
  const repl = { __SOURCE__: 'Normalize', __TABLE__: 'participants' };
  const fresh = await run('upsert_split.js', { nodes, replacements: { ...repl, __MODE__: 'new' } });
  assert.deepEqual(fresh.map((i) => i.json.key), ['3']);
  const changed = await run('upsert_split.js', { nodes, replacements: { ...repl, __MODE__: 'changed' } });
  assert.deepEqual(changed.map((i) => i.json), [{ id: 12, key: '2', participant_id: 2, age: 30, has_mini_markers: false }]);
});

test('upsert_split: an empty table (alwaysOutputData placeholder item) creates everything', async () => {
  const nodes = { Normalize: [{ items: [{ key: 'a' }, { key: 'b' }] }], 'Read: items': [{}] };
  const fresh = await run('upsert_split.js', { nodes, replacements: { __SOURCE__: 'Normalize', __TABLE__: 'items', __MODE__: 'new' } });
  assert.equal(fresh.length, 2);
});

// ---- Add Run -------------------------------------------------------------------------------------
const answersSheet = [1, 2].map((pid) => ({ 'Participant ID': pid, ...Object.fromEntries(Array.from({ length: 40 }, (_, i) => [`tda${i + 1}`, ((i + pid) % 9) + 1])) }));

test('parse_agent_answers: good rows, missing and out-of-range values', async () => {
  const bad = { ...answersSheet[1], 'Participant ID': 3, tda40: 12 };
  delete bad.tda1;
  const [{ json }] = await run('parse_agent_answers.js', { input: [...answersSheet, bad, { 'Participant ID': '', tda1: 1 }] });
  assert.equal(json.count, 3);
  assert.equal(json.ok, 2);
  assert.equal(json.answers[2].parsed_count, 38);
  assert.match(json.answers[2].error, /out of range: tda40 = 12; missing: tda1, tda40/);
});

test('prepare_run: queues an LLM run with defaults', async () => {
  const form = { 'Run ID': 'run-002', Provider: 'bedrock', Model: 'm', Temperature: '', 'Batch size': '' };
  const [{ json }] = await run('prepare_run.js', { input: [{}], nodes: { 'Add run form': [form], 'Parse agent answers': { executed: false } } });
  assert.equal(json.status, 'queued');
  assert.equal(json.source, 'llm');
  assert.equal(json.temperature, 0.7);
  assert.equal(json.batch_size, 25);
  assert.equal(json.prompt_version, 'mm-v1');
  assert.equal(json.key, 'run-002');
  assert.equal(json.finished_at, null); // Baserow date fields reject ''
  assert.equal(json.locked_at, null);
});

test('prepare_run: imported answers make a finished run; duplicates and bad providers are refused', async () => {
  const form = { 'Run ID': 'run-x', Provider: 'ollama', Model: 'llama3:70b', Temperature: 0.2, 'Batch size': 10 };
  const [{ json }] = await run('prepare_run.js', { input: [{ id: 1, run_id: 'run-001' }], nodes: { 'Add run form': [form], 'Parse agent answers': [{ count: 3, ok: 2, answers: [] }] } });
  assert.equal(json.status, 'done');
  assert.equal(json.source, 'import');
  assert.equal(json.participants_total, 3);
  assert.equal(json.participants_failed, 1);
  assert.ok(json.finished_at);
  await assert.rejects(run('prepare_run.js', { input: [{ id: 1, run_id: 'run-x' }], nodes: { 'Add run form': [form], 'Parse agent answers': { executed: false } } }), /already exists/);
  await assert.rejects(run('prepare_run.js', { input: [{}], nodes: { 'Add run form': [{ ...form, Provider: 'openai' }], 'Parse agent answers': { executed: false } } }), /Provider must be/);
});

test('turn_rows: one agent_turns row per answer with tda columns; none when queued', async () => {
  const [{ json: parsed }] = await run('parse_agent_answers.js', { input: answersSheet });
  const rows = await run('turn_rows.js', { nodes: { 'Parse agent answers': [parsed], 'Prepare run': [{ run_id: 'run-x', provider: 'ollama', model: 'm', prompt_version: '' }] } });
  assert.equal(rows.length, 2);
  assert.equal(rows[0].json.key, 'run-x:1');
  assert.equal(rows[0].json.tda1, 2);
  assert.equal(rows[0].json.ok, true);
  assert.deepEqual(await run('turn_rows.js', { nodes: { 'Parse agent answers': { executed: false }, 'Prepare run': [{}] } }), []);
});

// ---- Run Agents ----------------------------------------------------------------------------------
test('pick_run: picks the running run over queued and emits the lock body', async () => {
  const runs = [
    { id: 1, run_id: 'run-001', status: 'done', provider: 'bedrock', model: 'a' },
    { id: 2, run_id: 'run-002', status: 'queued', provider: 'ollama', model: 'b', batch_size: '25', temperature: '0.7' },
    { id: 3, run_id: 'run-003', status: 'running', provider: 'bedrock', model: 'c', started_at: '2026-10-20T10:00:00+00:00', locked_at: '' },
  ];
  const out = await run('pick_run.js', { input: runs, nodes: { Configuration: [CONFIG] } });
  assert.equal(out.length, 1);
  assert.equal(out[0].json.run.run_id, 'run-003');
  assert.equal(out[0].json.locked_by, 'exec-1');
  assert.equal(out[0].json.status, 'running');
});

test('pick_run: a fresh lock means nothing to do; a stale one is taken over', async () => {
  const fresh = { id: 2, run_id: 'r', status: 'running', provider: 'ollama', model: 'm', locked_at: new Date().toISOString(), locked_by: 'other' };
  assert.deepEqual(await run('pick_run.js', { input: [fresh], nodes: { Configuration: [CONFIG] } }), []);
  const stale = { ...fresh, locked_at: new Date(Date.now() - 31 * 60000).toISOString() };
  assert.equal((await run('pick_run.js', { input: [stale], nodes: { Configuration: [CONFIG] } })).length, 1);
});

test('pick_run: refuses a run whose prompt_version is not this workflow\'s', async () => {
  const r = { id: 1, run_id: 'r', status: 'queued', provider: 'ollama', model: 'm', prompt_version: 'mm-v0' };
  await assert.rejects(run('pick_run.js', { input: [r], nodes: { Configuration: [CONFIG] } }), /prompt_version/);
});

test('prepare_batch: unanswered participants with a persona, up to batch_size', async () => {
  const run_ = { run_id: 'r', provider: 'ollama', model: 'm', temperature: 0.7, prompt_version: 'mm-v1', batch_size: 2 };
  const participants = [{ participant_id: 3, persona: 'p3' }, { participant_id: 1, persona: 'p1' }, { participant_id: 2, persona: '' }, { participant_id: 4, persona: 'p4' }];
  const turns = [{ run_id: 'r', participant_id: '1' }, { run_id: 'other', participant_id: '3' }];
  const out = await run('prepare_batch.js', { input: turns, nodes: { 'Pick run': [{ run: run_ }], 'Read: participants': participants } });
  assert.deepEqual(out.map((i) => i.json.participant_id), [3, 4]);
  assert.equal(out[0].json.remaining, 2);
  const none = await run('prepare_batch.js', { input: [{ run_id: 'r', participant_id: 3 }, { run_id: 'r', participant_id: 4 }, ...turns], nodes: { 'Pick run': [{ run: run_ }], 'Read: participants': participants } });
  assert.equal(none[0].json.noWork, true);
});

test('build_prompt: same adjectives as normalize_humans, prompt carries persona and format', async () => {
  const [{ json }] = await run('build_prompt.js', { input: [{ participant_id: 1, persona: 'Is talkative: 5.' }] });
  assert.deepEqual(json.adjectives, ADJECTIVES);
  assert.match(json.prompt, /Is talkative: 5\./);
  assert.match(json.prompt, /40\. Withdrawn: <score>/);
  const normalizeSrc = readFileSync(join(here, 'normalize_humans.js'), 'utf8');
  assert.ok(normalizeSrc.includes(`'${ADJECTIVES.join("', '")}'`.slice(0, 60)));
});

test('parse_answer: a clean reply becomes an ok turn with 40 tda columns', async () => {
  const ctx = { run_id: 'r', participant_id: 7, provider: 'bedrock', model: 'm', prompt_version: 'mm-v1', adjectives: ADJECTIVES, startedAt: Date.now() - 1200 };
  const text = answer(ADJECTIVES.map((a, i) => `${i + 1}. ${a}: ${(i % 9) + 1}`));
  const [{ json }] = await run('parse_answer.js', { input: [{ text }], nodes: { 'Build prompt': [ctx] } });
  assert.equal(json.ok, true);
  assert.equal(json.parsed_count, 40);
  assert.equal(json.tda1, 1);
  assert.equal(json.tda40, 4);
  assert.equal(json.key, 'r:7');
  assert.ok(json.duration_ms >= 1000);
});

test('parse_answer: duplicates, gaps and model errors are kept as failed turns without tda columns', async () => {
  const ctx = { run_id: 'r', participant_id: 7, adjectives: ADJECTIVES };
  const text = answer(['Bashful: 3', 'Bashful: 4', ...ADJECTIVES.slice(2).map((a) => `${a} - 5`)]);
  const [{ json }] = await run('parse_answer.js', { input: [{ text }], nodes: { 'Build prompt': [ctx] } });
  assert.equal(json.ok, false);
  assert.match(json.error, /duplicate: Bashful/);
  assert.match(json.error, /missing: Bold/);
  assert.equal(json.tda1, undefined);
  const [{ json: err }] = await run('parse_answer.js', { input: [{ error: 'boom' }], nodes: { 'Build prompt': [ctx] } });
  assert.match(err.error, /llm: boom/);
});

test('finish_run: done when everyone has a turn, releases only its own lock, respects paused', async () => {
  const run_ = { id: 9, run_id: 'r', status: 'running' };
  const participants = [{ participant_id: 1, persona: 'a' }, { participant_id: 2, persona: 'b' }, { participant_id: 3, persona: '' }];
  const turns = [{ run_id: 'r', participant_id: 1, ok: true, answered_at: '1' }, { run_id: 'r', participant_id: 2, ok: false, answered_at: '1' }, { run_id: 'r', participant_id: 2, ok: true, answered_at: '2' }];
  const nodes = { 'Pick run': [{ run: run_ }], 'Read: participants': participants, 'Read: agent_turns (recount)': turns };
  const [{ json }] = await run('finish_run.js', { input: [{ ...run_, locked_by: 'exec-1' }], nodes });
  assert.equal(json.status, 'done');
  assert.equal(json.participants_done, 2);
  assert.equal(json.participants_failed, 0);
  assert.equal(json.locked_by, '');
  assert.equal(json.locked_at, null);
  const [{ json: other }] = await run('finish_run.js', { input: [{ ...run_, status: 'paused', locked_by: 'exec-2' }], nodes });
  assert.equal(other.status, 'paused');
  assert.equal('locked_by' in other, false);
});

// ---- Analyze and Report --------------------------------------------------------------------------
async function analysisFixture() {
  const [{ json: norm }] = await run('normalize_humans.js', { nodes: importNodes });
  const items = norm.items.map((it, i) => ({ id: i + 1, ...it }));
  const scores = norm.scores.map((s, i) => ({ id: i + 1, ...s, ...Object.fromEntries(['extraversion', 'agreeableness', 'conscientiousness', 'emotional_stability', 'openness'].map((f) => [f, String(s[f])])) }));
  // Agent answers: the human's own Mini-Markers for participant 1, shifted by +1 (capped) for participant 2.
  const turns = norm.human_mini_markers.map((h, i) => ({ id: i + 1, run_id: 'run-t', participant_id: String(h.participant_id), ok: true, answered_at: '2026-10-23T00:00:00+00:00', ...Object.fromEntries(Array.from({ length: 40 }, (_, k) => [`tda${k + 1}`, String(Math.min(9, h[`tda${k + 1}`] + (h.participant_id === 2 ? 1 : 0)))])) }));
  const runs = [{ id: 1, run_id: 'run-t', status: 'done', model: 'us.anthropic.claude-haiku-4-5-20251001-v1:0', provider: 'bedrock', participants_done: '2', participants_failed: '1' }];
  return { items, scores, turns, runs };
}

test('analyze_runs: scores, comparisons and a summary for the selected run', async () => {
  const { items, scores, turns, runs } = await analysisFixture();
  const nodes = { 'Analyze form': [{ 'Run ID': '' }], 'Read: runs': runs, 'Read: items': items, 'Read: scores': scores, 'Read: agent_turns': turns, 'Read: run_summaries': [{}] };
  const [{ json }] = await run('analyze_runs.js', { nodes });
  assert.deepEqual(json.analyzed, ['run-t']);
  assert.equal(json.scores.length, 2);
  assert.equal(json.comparisons.length, 2);
  const c1 = json.comparisons.find((c) => c.participant_id === 1);
  assert.equal(c1.mean_abs_diff, 0);
  assert.equal(c1.key, 'run-t:1');
  const s = json.run_summaries[0];
  assert.equal(s.n, 2);
  assert.ok(s.mean_abs_diff > 0);
  assert.ok('extraversion_shift_p_holm' in s && 'openness_steiger_p' in s && 'agreeableness_baseline_r' in s);
  assert.deepEqual(json.runs, [{ id: 1, analyzed_at: s.analyzed_at }]);
  await assert.rejects(run('analyze_runs.js', { nodes: { ...nodes, 'Analyze form': [{ 'Run ID': 'nope' }] } }), /does not exist/);
  const [{ json: none }] = await run('analyze_runs.js', { nodes: { ...nodes, 'Read: run_summaries': [{ id: 1, key: 'run-t', run_id: 'run-t', analyzed_at: '2026-10-23T12:00:00Z' }] } });
  assert.deepEqual(none.analyzed, []);
  // A summary older than the run's finish is stale and gets redone.
  const staleRuns = [{ ...runs[0], finished_at: '2026-10-23T13:00:00Z' }];
  const [{ json: redo }] = await run('analyze_runs.js', { nodes: { ...nodes, 'Read: runs': staleRuns, 'Read: run_summaries': [{ id: 1, key: 'run-t', run_id: 'run-t', analyzed_at: '2026-10-23T12:00:00Z' }] } });
  assert.deepEqual(redo.analyzed, ['run-t']);
});

test('render_reports: per-run html+csv and all-runs html+csv with the expected content', async () => {
  const { items, scores, turns, runs } = await analysisFixture();
  const nodes = { 'Analyze form': [{ 'Run ID': 'run-t' }], 'Read: runs': runs, 'Read: items': items, 'Read: scores': scores, 'Read: agent_turns': turns, 'Read: run_summaries': [{}], 'Read: comparisons': [{}] };
  const [{ json: analysis }] = await run('analyze_runs.js', { nodes });
  const files = await run('render_reports.js', { nodes: { ...nodes, Analyze: [analysis] } });
  assert.deepEqual(files.map((f) => f.json.fileName), ['run-t.html', 'run-t.csv', 'all-runs.html', 'all-runs.csv']);
  const html = files[0].json.content;
  assert.match(html, /<em>claude-haiku-4-5<\/em>/);
  assert.match(html, /FIG\. 3/);
  assert.match(html, /2 of 3 turns parsed cleanly; 1 failed/);
  assert.ok(!/undefined|NaN/.test(html), 'no undefined/NaN in run report');
  assert.match(files[1].json.content, /^run_id,participant_id,agent_extraversion/);
  assert.equal(files[1].json.content.trim().split('\n').length, 3);
  assert.match(files[2].json.content, /is the only run so far/);
  assert.ok(!/undefined|NaN/.test(files[2].json.content), 'no undefined/NaN in all-runs report');
  assert.match(files[3].json.content, /^run_id,model,provider,n,mean_abs_diff/);
});

test('run_stamps: one item per analyzed run', async () => {
  assert.deepEqual((await run('run_stamps.js', { nodes: { Analyze: [{ runs: [{ id: 1, analyzed_at: 'x' }] }] } })).map((i) => i.json), [{ id: 1, analyzed_at: 'x' }]);
});

// ---- Create Tables -------------------------------------------------------------------------------
test('create_tables: signs in, creates missing tables and fields, reports mismatches', async () => {
  const calls = [];
  const fields = { 7: [{ name: 'key', type: 'text', primary: true }, { id: 70, name: 'participant_id', type: 'text' }, { id: 71, name: 'age', type: 'number', number_decimal_places: 2, number_negative: false }] };
  const helpers = {
    httpRequest: async (o) => {
      calls.push(`${o.method} ${o.url.replace('http://baserow', '')}`);
      if (o.url.endsWith('/token-auth/')) return { access_token: 'jwt' };
      if (/tables\/database\/5\/$/.test(o.url) && o.method === 'GET') return [{ id: 7, name: 'participants' }];
      if (/tables\/database\/5\/$/.test(o.url) && o.method === 'POST') return { id: 100 + calls.length, name: o.body.name };
      if (/fields\/table\/(\d+)\/$/.test(o.url) && o.method === 'GET') return fields[o.url.match(/table\/(\d+)\//)[1]] || [{ name: 'key', type: 'text', primary: true }];
      if (/fields\/table\/(\d+)\/$/.test(o.url) && o.method === 'POST') return { id: 1, ...o.body };
      if (/fields\/\d+\/$/.test(o.url) && o.method === 'PATCH') return { id: 1, ...o.body };
      throw new Error(`unexpected ${o.method} ${o.url}`);
    },
  };
  const form = { 'Baserow URL': 'http://baserow/', Email: 'e', Password: 'p', 'Database ID': '5' };
  await assert.rejects(run('create_tables.js', { input: [form], helpers }), /participants\.participant_id: expected number, found text/);
  fields[7][1] = { id: 70, name: 'participant_id', type: 'number', number_decimal_places: 0, number_negative: false };
  calls.length = 0;
  const out = await run('create_tables.js', { input: [form], helpers });
  assert.equal(out.length, 9);
  assert.equal(out[0].json.table, 'participants');
  assert.equal(out[0].json.created, false);
  assert.equal(out[0].json.fields_added, 4);
  assert.equal(out[0].json.fields_adjusted, 1); // age: 2 decimals -> 0
  assert.ok(calls.includes('PATCH /api/database/fields/71/'));
  assert.ok(out.slice(1).every((i) => i.json.created));
  assert.ok(calls.filter((c) => c.startsWith('POST /api/database/tables/database/5/')).length === 8);
  assert.ok(calls.some((c) => c === 'POST /api/database/fields/table/7/'));
});
