// Analysis of the three network experiments (spliced in via `// @include lib/netstats.js`; needs
// lib/graph.js, lib/stats.js and FACETS from lib/scoring.js). The rule arms need no model, so they are
// simulated here, many times, from the same saved network the LLM arm ran on.
const NET_SIMS = 1000;
const quantile = (sorted, q) => sorted[Math.min(sorted.length - 1, Math.max(0, Math.round(q * (sorted.length - 1))))];
const sd = (xs) => { const m = mean(xs); return xs.length ? Math.sqrt(mean(xs.map((x) => (x - m) ** 2))) : null; };
const rmse = (as, bs) => Math.sqrt(mean(as.map((a, i) => (a - bs[i]) ** 2)));
// Mean and 5-95% band of a list of equally long series, position by position.
function band(series) {
  const len = series[0].length;
  const cols = Array.from({ length: len }, (_, t) => series.map((s) => s[t]).sort((a, b) => a - b));
  return { mean: cols.map(mean), low: cols.map((c) => quantile(c, 0.05)), high: cols.map((c) => quantile(c, 0.95)) };
}
const meanSeries = (series) => series[0].map((_, t) => mean(series.map((s) => s[t]).filter((v) => v !== null)));

// ---- 1. Propagation ---------------------------------------------------------------------------
// Rule arm: every tick, each agent that knows the event tells one random connection with probability p.
// Returns the tick at which each agent learned it (0 for the seed, Infinity for never).
function ruleSpread(adj, seed, p, ticks, rand) {
  const at = Array(adj.length).fill(Infinity);
  at[seed] = 0;
  for (let t = 1; t <= ticks; t++) {
    const told = [];
    for (let i = 0; i < adj.length; i++) if (at[i] < t && rand() < p) told.push(pick([...adj[i]], rand));
    for (const j of told) if (at[j] === Infinity) at[j] = t;
  }
  return at;
}
// Share of agents that know the event after each tick, t = 0..ticks.
const spreadCurve = (at, ticks) => Array.from({ length: ticks + 1 }, (_, t) => at.filter((x) => x <= t).length / at.length);

// Who was exposed, when, and by whom, from the judged message log of one run.
function exposure(run, messages, n, seed) {
  const at = Array(n).fill(Infinity);
  const by = Array(n).fill(null);
  if (run.arm !== 'control') at[seed] = 0;
  for (const m of messages) {
    if (m.carries_event !== 'yes') continue;
    const to = m.recipient_agent - 1;
    if (m.tick < at[to]) { at[to] = m.tick; by[to] = m.sender_agent - 1; }
  }
  return { at, by };
}

function analyzePropagation(runs, data, network) {
  const n = network.nodes;
  const { seed, decoy } = network.roles;
  const adj = adjacency(n, network.edges);
  const ticks = runs[0].ticks;
  const perRun = runs.map((run) => {
    const { messages, turns } = data[run.run_id];
    const { at, by } = exposure(run, messages, n, seed);
    // How often an agent that already knows passes it on, and to how many people at once.
    let eligible = 0;
    let shared = 0;
    let recipients = 0;
    for (let t = 1; t <= ticks; t++) {
      for (let i = 0; i < n; i++) {
        if (!(at[i] < t)) continue;
        eligible++;
        const to = new Set(messages.filter((m) => m.tick === t && m.sender_agent - 1 === i && m.carries_event === 'yes').map((m) => m.recipient_agent));
        if (to.size) { shared++; recipients += to.size; }
      }
    }
    const cells = { exposed_yes: 0, exposed_no: 0, unexposed_yes: 0, unexposed_no: 0 };
    let decoyYes = 0;
    let decoyAsked = 0;
    let openYes = 0;
    let openExposed = 0;
    for (const t of turns) {
      if (t.phase !== 'interview' || !t.ok) continue;
      const i = t.agent_number - 1;
      if (t.question === 'cued' && i !== seed) cells[`${at[i] < Infinity ? 'exposed' : 'unexposed'}_${t.answer}`]++;
      if (t.question === 'decoy' && i !== decoy) { decoyAsked++; if (t.answer === 'yes') decoyYes++; }
      if (t.question === 'open' && i !== seed && at[i] < Infinity) { openExposed++; if (t.judge === 'yes') openYes++; }
    }
    return {
      run, at, by, curve: spreadCurve(at, ticks), eligible, shared, recipients, cells, decoyYes, decoyAsked, openYes, openExposed,
      messages: messages.length, carrying: messages.filter((m) => m.carries_event === 'yes').length,
    };
  });
  const llm = perRun.filter((r) => r.run.arm === 'llm');
  const control = perRun.filter((r) => r.run.arm === 'control');
  if (!llm.length) return null;
  const sum = (list, f) => list.reduce((a, r) => a + f(r), 0);

  // Fit p on the odd repeats, check on the even ones (with a single run, both are that run).
  const train = llm.filter((r) => r.run.repeat % 2 === 1);
  const test = llm.filter((r) => r.run.repeat % 2 === 0);
  const fitOn = train.length ? train : llm;
  const checkOn = test.length ? test : llm;
  const target = meanSeries(fitOn.map((r) => r.curve));
  const simulate = (p, sims, seedNo) => { const rand = rng(seedNo); return Array.from({ length: sims }, () => spreadCurve(ruleSpread(adj, seed, p, ticks, rand), ticks)); };
  let best = { p: 0, err: Infinity };
  for (let k = 1; k <= 50; k++) {
    const p = k / 50;
    const err = rmse(meanSeries(simulate(p, 300, 1)), target);
    if (err < best.err) best = { p, err };
  }
  const rule = band(simulate(best.p, NET_SIMS, 2));
  const held = meanSeries(checkOn.map((r) => r.curve));
  const cells = Object.fromEntries(Object.keys(llm[0].cells).map((k) => [k, sum(llm, (r) => r.cells[k])]));
  return {
    n, ticks, seed, decoy, edges: network.edges, runs: perRun,
    llmCurve: meanSeries(llm.map((r) => r.curve)), llmCurves: llm.map((r) => r.curve),
    controlCurve: control.length ? meanSeries(control.map((r) => r.curve)) : null,
    p_behavior: sum(llm, (r) => r.shared) / Math.max(1, sum(llm, (r) => r.eligible)),
    fanout: sum(llm, (r) => r.recipients) / Math.max(1, sum(llm, (r) => r.shared)),
    p_fit: best.p, rule, held, split: train.length > 0 && test.length > 0,
    held_rmse: rmse(held, rule.mean),
    held_inside: held.filter((v, t) => v >= rule.low[t] && v <= rule.high[t]).length / held.length,
    final_exposed: mean(llm.map((r) => r.curve[ticks])), rule_final: rule.mean[ticks],
    cells,
    recall: cells.exposed_yes / Math.max(1, cells.exposed_yes + cells.exposed_no),
    hallucinated: cells.unexposed_yes / Math.max(1, cells.unexposed_yes + cells.unexposed_no),
    unexposed: cells.unexposed_yes + cells.unexposed_no,
    open_recall: sum(llm, (r) => r.openYes) / Math.max(1, sum(llm, (r) => r.openExposed)),
    decoy_yes: sum(llm, (r) => r.decoyYes) / Math.max(1, sum(llm, (r) => r.decoyAsked)),
    control: control.length ? {
      runs: control.length,
      cued_yes: sum(control, (r) => r.cells.unexposed_yes + r.cells.exposed_yes) / Math.max(1, sum(control, (r) => Object.values(r.cells).reduce((a, b) => a + b, 0))),
      judged_carrying: sum(control, (r) => r.carrying) / Math.max(1, sum(control, (r) => r.messages)),
    } : null,
  };
}

// ---- 2. Structure -----------------------------------------------------------------------------
const hasCommon = (adj, a, b) => [...adj[a]].some((x) => adj[b].has(x));
const graphMetrics = (n, edges) => ({ density: density(n, edges), clustering: clustering(n, edges), degree_sd: degreeSd(n, edges) });

// Rule arms. 'fof': every tick each agent connects to a random friend of a friend with probability q.
// 'random': the same, to any non-connection. Returns the end graph and the share of new ties that closed a triangle.
function ruleTies(n, start, q, ticks, rand, mode) {
  const adj = adjacency(n, start);
  const edges = start.map((e) => [...e]);
  let closing = 0;
  for (let t = 1; t <= ticks; t++) {
    for (let i = 0; i < n; i++) {
      if (rand() >= q) continue;
      const options = [];
      for (let j = 0; j < n; j++) if (j !== i && !adj[i].has(j) && (mode === 'random' || hasCommon(adj, i, j))) options.push(j);
      if (!options.length) continue;
      const j = pick(options, rand);
      if (hasCommon(adj, i, j)) closing++;
      adj[i].add(j); adj[j].add(i); edges.push([Math.min(i, j), Math.max(i, j)]);
    }
  }
  const added = edges.length - start.length;
  return { edges, added, closure: added ? closing / added : null };
}

function analyzeStructure(runs, data, network) {
  const n = network.nodes;
  const ticks = runs[0].ticks;
  const start = network.edges;
  const perRun = runs.filter((r) => r.arm === 'llm').map((run) => {
    const { turns, ties } = data[run.run_id];
    const adj = adjacency(n, start);
    const edges = start.map((e) => [...e]);
    let closing = 0;
    for (const t of [...ties].sort((a, b) => a.tick - b.tick)) {
      const a = t.requester_agent - 1;
      const b = t.accepter_agent - 1;
      if (hasCommon(adj, a, b)) closing++;
      adj[a].add(b); adj[b].add(a); edges.push([Math.min(a, b), Math.max(a, b)]);
    }
    // Perceived tie: both agents name each other in the interview.
    const perceived = (question) => {
      const knows = Array.from({ length: n }, () => new Set());
      const answers = turns.filter((t) => t.question === question && t.ok);
      for (const t of answers) for (const name of JSON.parse(t.names || '[]')) { const j = data.byName.get(name); if (j !== undefined) knows[t.agent_number - 1].add(j); }
      const list = [];
      for (let a = 0; a < n; a++) for (let b = a + 1; b < n; b++) if (knows[a].has(b) && knows[b].has(a)) list.push([a, b]);
      return { edges: list, fakes: mean(answers.map((t) => t.fakes || 0)) };
    };
    const before = perceived('names_start');
    const after = perceived('names_end');
    const actual = new Set(edges.map(([a, b]) => edgeKey(a, b)));
    const both = after.edges.filter(([a, b]) => actual.has(edgeKey(a, b))).length;
    let requests = 0;
    let declined = 0;
    for (const t of turns) if (t.phase === 'act' && t.detail) { const d = JSON.parse(t.detail); requests += d.connect.length; declined += d.decline.length; }
    return {
      run, edges, perceivedEdges: after.edges, added: ties.length, requests, declined, closure: ties.length ? closing / ties.length : null,
      start: graphMetrics(n, start), end: graphMetrics(n, edges),
      perceived_start: density(n, before.edges), perceived_end: density(n, after.edges), fakes_start: before.fakes, fakes_end: after.fakes,
      precision: after.edges.length ? both / after.edges.length : null, recall: both / edges.length,
      jaccard: both / (after.edges.length + edges.length - both),
    };
  });
  if (!perRun.length) return null;
  const added = mean(perRun.map((r) => r.added));
  // Give each rule the q that adds as many ties as the LLM arm did, so density matches by construction
  // and only the shape of the network can differ.
  const rules = {};
  ['fof', 'random'].forEach((mode, m) => {
    const sims = (q, count, seedNo) => { const rand = rng(seedNo); return Array.from({ length: count }, () => ruleTies(n, start, q, ticks, rand, mode)); };
    let best = { q: 0, err: Infinity };
    for (let k = 0; k <= 100; k++) {
      const q = k / 200;
      const err = Math.abs(mean(sims(q, 100, 10 + m).map((s) => s.added)) - added);
      if (err < best.err) best = { q, err };
    }
    const out = sims(best.q, NET_SIMS, 20 + m);
    const stat = (f) => { const xs = out.map(f).filter((v) => v !== null).sort((a, b) => a - b); return xs.length ? { mean: mean(xs), low: quantile(xs, 0.05), high: quantile(xs, 0.95) } : null; };
    rules[mode] = {
      q: best.q, added: stat((s) => s.added), closure: stat((s) => s.closure),
      density: stat((s) => density(n, s.edges)), clustering: stat((s) => clustering(n, s.edges)), degree_sd: stat((s) => degreeSd(n, s.edges)),
    };
  });
  const m = (f) => mean(perRun.map(f).filter((v) => v !== null));
  return {
    n, ticks, startEdges: start, runs: perRun, rules,
    start: graphMetrics(n, start),
    llm: {
      added, requests: m((r) => r.requests), declined: m((r) => r.declined), closure: m((r) => r.closure),
      density: m((r) => r.end.density), clustering: m((r) => r.end.clustering), degree_sd: m((r) => r.end.degree_sd),
      perceived_start: m((r) => r.perceived_start), perceived_end: m((r) => r.perceived_end),
      fakes_start: m((r) => r.fakes_start), fakes_end: m((r) => r.fakes_end),
      precision: m((r) => r.precision), recall: m((r) => r.recall), jaccard: m((r) => r.jaccard),
    },
  };
}

// ---- 3. Social dynamics -----------------------------------------------------------------------
// GHOSTS today (EvidenceProcessorService + Bayes.cs): a message whose text contains one of a hypothesis's
// keywords moves the belief of its author and its reader, prior 0.5, likelihood 0.6, every time. Here the
// hypothesis is "the party is on"; a message with a cancellation keyword is the same step the other way.
const BAYES_LIKELIHOOD = 0.6;
const BAYES_KEYWORDS = {
  on: ['definitely on', 'still on', 'is on', 'is happening', 'confirmed', 'going ahead'],
  off: ['cancelled', 'canceled', 'called off', 'not happening', 'is off'],
};
const bayesStep = (prior, side) => { const l = side === 'on' ? BAYES_LIKELIHOOD : 1 - BAYES_LIKELIHOOD; return (l * prior) / (l * prior + (1 - l) * (1 - prior)); };
const bayesMatches = (text) => Object.keys(BAYES_KEYWORDS).filter((h) => BAYES_KEYWORDS[h].some((kw) => text.toLowerCase().includes(kw)));

function analyzeDynamics(runs, data, network) {
  const n = network.nodes;
  const ticks = runs[0].ticks;
  const { hub, peripheral } = network.roles;
  const perRun = runs.filter((r) => r.arm === 'llm').map((run) => {
    const { messages, turns } = data[run.run_id];
    const [on, off] = run.condition === 'on_at_hub' ? [hub, peripheral] : [peripheral, hub];
    // LLM arm: the agent's own answer at every checkpoint, 0-100.
    const llm = Array.from({ length: n }, () => Array(ticks + 1).fill(null));
    for (const t of turns) if (t.phase === 'belief' && t.ok) llm[t.agent_number - 1][t.tick] = t.belief;

    // Fixed Bayes: everyone starts at 0.5; the seeds read their own note first.
    const h = Array(n).fill(0.5);
    h[on] = bayesStep(h[on], 'on');
    h[off] = bayesStep(h[off], 'off');
    const pOn = () => h.map((x) => 100 * x);
    const bayes = [pOn()];
    // DeGroot: start from what the agents said at tick 0; each tick an agent moves to the average of its own
    // belief and the beliefs of everyone who messaged it about the party that tick.
    let b = llm.map((row) => row[0] ?? 50);
    const degroot = [b];
    for (let t = 1; t <= ticks; t++) {
      const now = messages.filter((m) => m.tick === t);
      for (const m of now) for (const k of bayesMatches(m.text)) for (const i of [m.sender_agent - 1, m.recipient_agent - 1]) h[i] = bayesStep(h[i], k);
      bayes.push(pOn());
      b = b.map((own, i) => {
        const senders = [...new Set(now.filter((m) => m.recipient_agent - 1 === i && ['on', 'off', 'unsure'].includes(m.stance)).map((m) => m.sender_agent - 1))];
        return (own + senders.reduce((a, j) => a + b[j], 0)) / (1 + senders.length);
      });
      degroot.push(b);
    }
    const byAgent = (series) => Array.from({ length: n }, (_, i) => series.map((row) => row[i]));
    const rules = { bayes: byAgent(bayes), degroot: byAgent(degroot) };
    const fit = (rule) => {
      const xs = [];
      const ys = [];
      for (let i = 0; i < n; i++) for (let t = 1; t <= ticks; t++) if (llm[i][t] !== null) { xs.push(llm[i][t]); ys.push(rule[i][t]); }
      return { rmse: xs.length ? rmse(xs, ys) : null, r: xs.length > 2 ? pearson(xs, ys) : null };
    };
    const final = llm.map((row) => row[ticks]).filter((v) => v !== null);
    return {
      run, on, off, llm, rules,
      mean: { llm: meanSeries(llm), bayes: meanSeries(rules.bayes), degroot: meanSeries(rules.degroot) },
      fit: { bayes: fit(rules.bayes), degroot: fit(rules.degroot) },
      final_mean: mean(final), final_sd: sd(final),
      final_on: final.filter((v) => v > 50).length / Math.max(1, final.length), final_off: final.filter((v) => v < 50).length / Math.max(1, final.length),
      stances: Object.fromEntries(['on', 'off', 'unsure', 'none'].map((s) => [s, messages.filter((m) => m.stance === s).length])),
    };
  });
  if (!perRun.length) return null;
  const m = (list, f) => mean(list.map(f).filter((v) => v !== null && v !== undefined));
  const conditions = Object.fromEntries(['on_at_hub', 'off_at_hub'].map((c) => {
    const list = perRun.filter((r) => r.run.condition === c);
    return [c, list.length ? {
      runs: list.length,
      mean: { llm: meanSeries(list.map((r) => r.mean.llm)), bayes: meanSeries(list.map((r) => r.mean.bayes)), degroot: meanSeries(list.map((r) => r.mean.degroot)) },
      final_mean: m(list, (r) => r.final_mean), final_sd: m(list, (r) => r.final_sd), final_on: m(list, (r) => r.final_on), final_off: m(list, (r) => r.final_off),
    } : null];
  }));
  const gap = conditions.on_at_hub && conditions.off_at_hub ? conditions.on_at_hub.final_mean - conditions.off_at_hub.final_mean : null;
  return {
    n, ticks, hub, peripheral, edges: network.edges, runs: perRun, conditions, position_gap: gap,
    final_mean: m(perRun, (r) => r.final_mean), final_sd: m(perRun, (r) => r.final_sd),
    fit: { bayes: { rmse: m(perRun, (r) => r.fit.bayes.rmse), r: m(perRun, (r) => r.fit.bayes.r) }, degroot: { rmse: m(perRun, (r) => r.fit.degroot.rmse), r: m(perRun, (r) => r.fit.degroot.r) } },
  };
}

// ---- Traits -----------------------------------------------------------------------------------
// Does a person's own trait score predict how their agent behaves? Per cast member: the share of
// ticks in which the agent sent at least one message, over every run of this label.
function analyzeTraits(runs, data, cast) {
  const rows = cast.map((c) => {
    let turns = 0;
    let active = 0;
    let sent = 0;
    for (const run of runs) for (const t of data[run.run_id].turns) if (t.phase === 'act' && t.ok && t.agent_number === c.agent) { turns++; sent += t.sent; if (t.sent > 0) active++; }
    return { agent: c.agent, name: c.name, participant_id: c.participant_id, ...Object.fromEntries(FACETS.map((f) => [f, c[f]])), turns, active_rate: turns ? active / turns : null, messages_per_tick: turns ? sent / turns : null };
  });
  const have = rows.filter((r) => r.active_rate !== null);
  const facets = Object.fromEntries(FACETS.map((f) => {
    const r = pearson(have.map((x) => x[f]), have.map((x) => x.active_rate));
    const [low, high] = fisherCI(r, have.length);
    return [f, { r, low, high }];
  }));
  return { rows, facets, n: have.length };
}

// ---- Everything, per label (one label = one model under study) --------------------------------
// tables: typed rows of sim_runs (done), sim_cast, sim_networks, sim_messages, sim_turns, sim_ties.
function netAnalyze(tables) {
  const cast = [...tables.cast].sort((a, b) => a.agent - b.agent);
  const networks = Object.fromEntries(tables.networks.map((w) => [w.key, { ...w, edges: JSON.parse(w.edges), roles: JSON.parse(w.roles) }]));
  const data = { byName: new Map(cast.map((c) => [c.name, c.agent - 1])) };
  for (const run of tables.runs) {
    const mine = (rows) => rows.filter((r) => r.run_id === run.run_id && r.execution === run.execution);
    data[run.run_id] = { messages: mine(tables.messages).sort((a, b) => a.seq - b.seq), turns: mine(tables.turns), ties: mine(tables.ties) };
  }
  const labels = [...new Set(tables.runs.map((r) => r.label))].sort();
  return labels.map((label) => {
    const runs = tables.runs.filter((r) => r.label === label).sort((a, b) => a.repeat - b.repeat);
    const of = (experiment) => runs.filter((r) => r.experiment === experiment);
    const part = (experiment, fn) => (of(experiment).length && networks[experiment] ? fn(of(experiment), data, networks[experiment]) : null);
    return {
      label, model: runs[0].model, provider: runs[0].provider, judge_model: runs[0].judge_model, runs,
      calls: runs.reduce((a, r) => a + (r.calls || 0), 0), failures: runs.reduce((a, r) => a + (r.failures || 0), 0),
      messages: runs.reduce((a, r) => a + data[r.run_id].messages.length, 0),
      propagation: part('propagation', analyzePropagation),
      structure: part('structure', analyzeStructure),
      dynamics: part('dynamics', analyzeDynamics),
      traits: analyzeTraits(runs.filter((r) => r.arm === 'llm'), data, cast),
    };
  });
}
