// Simulation engine for the three network experiments (spliced into Code nodes via `// @include lib/sim.js`;
// needs lib/graph.js). One run is a list of phases. Each phase is one round of model calls:
//   simStart(run, cast, network) -> state
//   simPrompts(state)            -> the calls of the current phase (and remembers what each call is for)
//   simApply(state, replies)     -> folds the replies into the state and moves to the next phase
//   simRows(state, execution)    -> the Baserow rows of the finished run
// Ticks are synchronous: at tick t every agent sees the messages sent before t; what it sends arrives at t+1.
const SIM_SLOTS = ['8 am', '10 am', 'noon', '2 pm', '4 pm', '6 pm', '8 pm', '10 pm'];
const SIM_FAKE_NAMES = ['Marlow', 'Tatum', 'Lennox', 'Shiloh', 'Arden'];
const SIM_OUTSIDER = 'Riley'; // the host in the dynamics experiment; not in the network, so nobody can ask them
const SIM_VENUE = 'Saturday at 7 pm at the Rosewood Park pavilion';
const SIM_JUDGE_CHUNK = 20;

const simTime = (tick) => `Day ${Math.floor((tick - 1) / SIM_SLOTS.length) + 1}, ${SIM_SLOTS[(tick - 1) % SIM_SLOTS.length]}`;

function simPhases(experiment, ticks) {
  const phases = [];
  if (experiment === 'structure') phases.push({ kind: 'names', tick: 0 });
  if (experiment === 'dynamics') phases.push({ kind: 'belief', tick: 0 });
  for (let t = 1; t <= ticks; t++) {
    phases.push({ kind: 'act', tick: t });
    if (experiment === 'dynamics') phases.push({ kind: 'belief', tick: t });
  }
  if (experiment === 'propagation') phases.push({ kind: 'interview', tick: ticks });
  if (experiment === 'structure') phases.push({ kind: 'names', tick: ticks });
  if (experiment !== 'structure') phases.push({ kind: 'judge', tick: ticks });
  return phases;
}

// cast: [{ agent (1..n), name, participant_id, persona }] ordered by agent; node i of the network is agent i+1.
function simStart(run, cast, network) {
  const roles = network.roles;
  const agents = cast.map((c) => ({ agent: c.agent, name: c.name, participant_id: c.participant_id, persona: c.persona, notes: [], heard: [] }));
  const state = {
    run, agents, roles,
    edges: network.edges.map((e) => [...e]),
    phases: simPhases(run.experiment, run.ticks), phase: 0,
    messages: [], turns: [], ties: [], requests: [], meta: [], calls: 0, failures: 0,
  };
  if (run.experiment === 'dynamics') {
    for (const a of agents) a.notes.push(`${SIM_OUTSIDER} mentioned maybe having a birthday party this Saturday, but it wasn't settled.`);
    const [on, off] = run.condition === 'on_at_hub' ? [roles.hub, roles.peripheral] : [roles.peripheral, roles.hub];
    agents[on].notes.push(`I just talked to ${SIM_OUTSIDER}: the birthday party this Saturday is definitely on.`);
    agents[off].notes.push(`I just heard from ${SIM_OUTSIDER}: the birthday party this Saturday is cancelled.`);
    state.event = `${SIM_OUTSIDER}'s birthday party this Saturday`;
  } else {
    const host = agents[roles.seed];
    if (run.arm !== 'control') host.notes.push(`I'm throwing my birthday party this ${SIM_VENUE}. I want everyone to come, so I should invite people and ask them to pass it on.`);
    state.event = `${host.name}'s birthday party (this ${SIM_VENUE})`;
  }
  return state;
}

const simNeighbors = (state, i) => state.edges.filter((e) => e.includes(i)).map((e) => (e[0] === i ? e[1] : e[0])).sort((a, b) => a - b);

const simIdentity = (a) => `You are ${a.name}. Stay in character as the person described below.

About you. These are your own answers to a personality questionnaire, where 1 = disagree strongly and 5 = agree strongly:
${a.persona}`;

// What agent i remembers: its notes, inbox and outbox up to and including tick `upto`.
function simMemory(state, i, upto) {
  const a = state.agents[i];
  const inbox = state.messages.filter((m) => m.to === i && m.tick <= upto).map((m) => `[${simTime(m.tick)}] From ${state.agents[m.from].name}: ${m.text}`);
  const outbox = state.messages.filter((m) => m.from === i && m.tick <= upto).map((m) => `[${simTime(m.tick)}] To ${state.agents[m.to].name}: ${m.text}`);
  return `${a.notes.length ? `Your notes:\n${a.notes.map((n) => `- ${n}`).join('\n')}\n\n` : ''}Messages you received (oldest first):
${inbox.join('\n') || '(none)'}

Messages you sent (oldest first):
${outbox.join('\n') || '(none)'}`;
}

function simActPrompt(state, i, tick) {
  const a = state.agents[i];
  const names = (list) => list.map((j) => state.agents[j].name).join(', ') || '(nobody)';
  const structure = state.run.experiment === 'structure';
  const waiting = state.requests.filter((r) => r.to === i).map((r) => r.from);
  return `${simIdentity(a)}

It is ${simTime(tick)}. You use a direct-messaging app.
Your connections: ${names(simNeighbors(state, i))}.${structure ? `
People you have heard of but are not connected with: ${names(a.heard)}.
Connection requests waiting for your answer: ${names(waiting)}.` : ''}

${simMemory(state, i, tick - 1)}

Decide what to do right now. You can send direct messages to one or more of your connections, or stay quiet.${structure ? ' You can also ask to connect with someone you have heard of, and accept or decline each waiting request.' : ''}

Reply with only a JSON object and nothing else:
{"messages": [{"to": "<name of a connection>", "text": "<your message>"}]${structure ? ', "connect": ["<name>"], "accept": ["<name>"], "decline": ["<name>"]' : ''}}
Use an empty list for anything you do not do.`;
}

const simInterviewPrompt = (state, i, question) => `${simIdentity(state.agents[i])}

This is everything you remember:

${simMemory(state, i, state.run.ticks)}

${question}`;

function simJudgePrompt(state, units) {
  const list = units.map((u, n) => `${n + 1}. ${JSON.stringify(u.text)}`).join('\n');
  if (state.run.experiment === 'dynamics') {
    return `You are labelling short texts for a study. The topic: ${state.event}.
For each numbered text, give its stance on whether the party will happen:
- "on": says or implies the party is happening
- "off": says or implies the party is cancelled or not happening
- "unsure": mentions the party without taking a side, or asks about it
- "none": does not mention the party

Texts:
${list}

Reply with only a JSON array, one entry per text, in order: [{"n": 1, "stance": "on"}]`;
  }
  return `You are labelling short texts for a study. The event of interest: ${state.event}.
For each numbered text, decide whether it tells the reader about this event: that the party is happening, an invitation to it, or any of its details. A text that mentions the host without the party does not count.

Texts:
${list}

Reply with only a JSON array, one entry per text, in order: [{"n": 1, "carries": true}]`;
}

// The model calls of the current phase: [{ prompt, provider, model, temperature }]. Remembers in
// state.meta what each call is for, so simApply only needs the reply texts in the same order.
function simPrompts(state) {
  const { run, agents } = state;
  const ph = state.phases[state.phase];
  const all = agents.map((_, i) => i);
  const subject = { provider: run.provider, model: run.model, temperature: run.temperature };
  let calls = [];
  if (ph.kind === 'act') {
    calls = all.map((i) => ({ meta: { i }, prompt: simActPrompt(state, i, ph.tick) }));
  } else if (ph.kind === 'belief') {
    calls = all.map((i) => ({
      meta: { i },
      prompt: `${simIdentity(agents[i])}

It is ${ph.tick ? simTime(ph.tick) : 'Day 1, before 8 am'}. This is everything you know:

${simMemory(state, i, ph.tick)}

How likely is it that ${state.event} will happen? Reply with a single whole number from 0 (certainly not happening) to 100 (certainly happening), and nothing else.`,
    }));
  } else if (ph.kind === 'names') {
    calls = all.map((i) => {
      const list = [...agents.filter((_, j) => j !== i).map((a) => a.name), ...SIM_FAKE_NAMES].sort();
      return { meta: { i }, prompt: simInterviewPrompt(state, i, `An interviewer asks: "Which of these people do you know?"\n${list.join(', ')}\n\nReply with only a JSON array of the names you know, and nothing else. Use [] if you know none of them.`) };
    });
  } else if (ph.kind === 'interview') {
    const host = agents[state.roles.seed].name;
    const decoy = agents[state.roles.decoy].name;
    const yesNo = (what) => `An interviewer asks: "Have you heard about ${what}?"\n\nStart your reply with YES or NO, then say in one sentence what you know about it.`;
    for (const i of all) {
      calls.push({ meta: { i, question: 'open' }, prompt: simInterviewPrompt(state, i, 'An interviewer asks: "Is there anything coming up that you know about, any events or plans?"\n\nAnswer in one or two sentences.') });
      calls.push({ meta: { i, question: 'cued' }, prompt: simInterviewPrompt(state, i, yesNo(`${host}'s birthday party`)) });
      calls.push({ meta: { i, question: 'decoy' }, prompt: simInterviewPrompt(state, i, yesNo(`${decoy}'s housewarming`)) });
    }
  } else if (ph.kind === 'judge') {
    // The judge sees only texts: no arm, no sender, no run. Messages first, then the open-recall answers.
    const units = [
      ...state.messages.map((m, n) => ({ message: n, text: m.text })),
      ...state.turns.map((t, n) => ({ turn: n, text: t.raw_output, t })).filter((u) => u.t.question === 'open' && u.t.ok).map(({ t, ...u }) => u),
    ];
    for (let c = 0; c < units.length; c += SIM_JUDGE_CHUNK) {
      const chunk = units.slice(c, c + SIM_JUDGE_CHUNK);
      calls.push({ meta: { units: chunk.map(({ text, ...u }) => u) }, prompt: simJudgePrompt(state, chunk), provider: run.judge_provider, model: run.judge_model, temperature: 0 });
    }
  }
  state.meta = calls.map((c) => c.meta);
  return calls.map(({ meta, ...c }) => ({ ...subject, ...c }));
}

// The first {...} or [...] in a reply, parsed; null when there is none.
function simJson(text, open) {
  const close = open === '{' ? '}' : ']';
  const start = text.indexOf(open);
  const end = text.lastIndexOf(close);
  if (start < 0 || end <= start) return null;
  try { return JSON.parse(text.slice(start, end + 1)); } catch (e) { return null; }
}

// replies: one entry per call of simPrompts, in order: the reply text, or '' when the call failed.
function simApply(state, replies) {
  const { run, agents } = state;
  const ph = state.phases[state.phase];
  const byName = new Map(agents.map((a, i) => [a.name.toLowerCase(), i]));
  const idx = (name) => byName.get(String(name ?? '').trim().toLowerCase());
  const nameList = (v) => (Array.isArray(v) ? v.map(idx).filter((j) => j !== undefined) : []);
  const turn = (i, text, fields) => {
    const t = { tick: ph.tick, phase: ph.kind, agent: i, raw_output: String(text).slice(0, 4000), ok: true, error: '', ...fields };
    if (!t.ok) state.failures++;
    state.turns.push(t);
    return t;
  };
  state.calls += replies.length;

  if (ph.kind === 'act') {
    const sent = [];
    const asked = [];
    const accepted = [];
    replies.forEach((text, n) => {
      const { i } = state.meta[n];
      const reply = text ? simJson(text, '{') : null;
      if (!reply) { turn(i, text, { ok: false, error: text ? 'no JSON object in the reply' : 'empty reply', sent: 0 }); return; }
      const nbrs = new Set(simNeighbors(state, i));
      let invalid = 0;
      let count = 0;
      for (const m of Array.isArray(reply.messages) ? reply.messages : []) {
        const to = idx(m && m.to);
        const body = String((m && m.text) ?? '').trim().slice(0, 1000);
        if (to === undefined || !nbrs.has(to) || !body) { invalid++; continue; }
        sent.push({ tick: ph.tick, from: i, to, text: body });
        count++;
      }
      const fields = { sent: count, invalid };
      if (run.experiment === 'structure') {
        const want = nameList(reply.connect).filter((j) => j !== i && !nbrs.has(j) && agents[i].heard.includes(j));
        for (const j of want) asked.push({ from: i, to: j });
        const waiting = new Set(state.requests.filter((r) => r.to === i).map((r) => r.from));
        const yes = nameList(reply.accept).filter((j) => waiting.has(j));
        for (const j of yes) accepted.push({ from: j, to: i });
        fields.detail = JSON.stringify({ connect: want.map((j) => agents[j].name), accept: yes.map((j) => agents[j].name), decline: nameList(reply.decline).filter((j) => waiting.has(j)).map((j) => agents[j].name) });
      }
      turn(i, text, fields);
    });
    // Deliver. A tie is written only when both sides agreed: an accepted request, or two agents asking each other.
    for (const m of sent) state.messages.push({ seq: state.messages.length + 1, ...m });
    const have = new Set(state.edges.map(([a, b]) => edgeKey(a, b)));
    const tie = (from, to) => {
      if (have.has(edgeKey(from, to))) return;
      have.add(edgeKey(from, to));
      state.edges.push(from < to ? [from, to] : [to, from]);
      state.ties.push({ tick: ph.tick, from, to });
    };
    for (const r of accepted) tie(r.from, r.to);
    for (const r of asked) if (asked.some((o) => o.from === r.to && o.to === r.from)) tie(r.from, r.to);
    // Requests live for one tick: the ones just made are shown to their targets on the next tick.
    state.requests = asked.filter((r) => !have.has(edgeKey(r.from, r.to)));
    if (run.experiment === 'structure') {
      // An agent can only meet someone whose name appeared in its inbox (or who asked to connect).
      const hear = (i, j) => { if (j !== i && !agents[i].heard.includes(j)) agents[i].heard.push(j); };
      for (const m of sent) agents.forEach((a, j) => { if (new RegExp(`\\b${a.name}\\b`).test(m.text)) hear(m.to, j); });
      for (const r of state.requests) hear(r.to, r.from);
      for (const a of agents) a.heard = a.heard.filter((j) => !have.has(edgeKey(a.agent - 1, j))).sort((x, y) => x - y);
    }
  } else if (ph.kind === 'belief') {
    replies.forEach((text, n) => {
      const m = String(text).match(/\b(100|\d{1,2})\b/);
      turn(state.meta[n].i, text, m ? { belief: Number(m[1]) } : { ok: false, error: text ? 'no number 0-100 in the reply' : 'empty reply' });
    });
  } else if (ph.kind === 'names') {
    replies.forEach((text, n) => {
      const list = text ? simJson(text, '[') : null;
      const question = ph.tick === 0 ? 'names_start' : 'names_end';
      if (!Array.isArray(list)) { turn(state.meta[n].i, text, { question, ok: false, error: text ? 'no JSON array in the reply' : 'empty reply' }); return; }
      const said = list.map((s) => String(s).trim().toLowerCase());
      turn(state.meta[n].i, text, {
        question,
        names: JSON.stringify(agents.filter((a) => said.includes(a.name.toLowerCase())).map((a) => a.name)),
        fakes: SIM_FAKE_NAMES.filter((f) => said.includes(f.toLowerCase())).length,
      });
    });
  } else if (ph.kind === 'interview') {
    replies.forEach((text, n) => {
      const { i, question } = state.meta[n];
      if (!text) { turn(i, text, { question, ok: false, error: 'empty reply' }); return; }
      if (question === 'open') { turn(i, text, { question }); return; }
      const m = String(text).match(/^\W*(yes|no)\b/i);
      turn(i, text, m ? { question, answer: m[1].toLowerCase() } : { question, ok: false, error: 'reply does not start with YES or NO' });
    });
  } else if (ph.kind === 'judge') {
    replies.forEach((text, n) => {
      const { units } = state.meta[n];
      const list = text ? simJson(text, '[') : null;
      const ok = Array.isArray(list) && list.length === units.length;
      turn(null, text, ok ? {} : { ok: false, error: text ? `expected ${units.length} labels` : 'empty reply' });
      if (!ok) return;
      units.forEach((u, k) => {
        const label = list[k] || {};
        if (run.experiment === 'dynamics') {
          if (['on', 'off', 'unsure', 'none'].includes(label.stance)) state.messages[u.message].stance = label.stance;
        } else if (typeof label.carries === 'boolean') {
          const value = label.carries ? 'yes' : 'no';
          if (u.message !== undefined) state.messages[u.message].carries_event = value; else state.turns[u.turn].judge = value;
        }
      });
    });
  }
  state.meta = [];
  state.phase++;
  return state;
}

const simDone = (state) => state.phase >= state.phases.length;
const simProgress = (state) => {
  if (simDone(state)) return 'done';
  const ph = state.phases[state.phase];
  return `${ph.kind} ${ph.tick}/${state.run.ticks}`;
};

// Baserow rows of a finished run. `execution` marks the attempt, so rows of an abandoned attempt can be told apart.
function simRows(state, execution) {
  const { run, agents } = state;
  const name = (i) => (i === null || i === undefined ? '' : agents[i].name);
  const num = (i) => (i === null || i === undefined ? null : agents[i].agent);
  return {
    sim_messages: state.messages.map((m) => ({
      key: `${run.run_id}:m${m.seq}`, run_id: run.run_id, execution, seq: m.seq, tick: m.tick,
      sender: name(m.from), sender_agent: num(m.from), recipient: name(m.to), recipient_agent: num(m.to),
      text: m.text, carries_event: m.carries_event || '', stance: m.stance || '',
    })),
    sim_turns: state.turns.map((t, n) => ({
      key: `${run.run_id}:t${n + 1}`, run_id: run.run_id, execution, tick: t.tick, phase: t.phase, question: t.question || '',
      agent: name(t.agent), agent_number: num(t.agent), participant_id: t.agent === null ? null : agents[t.agent].participant_id,
      ok: t.ok, error: t.error, raw_output: t.raw_output, sent: t.sent ?? null, invalid: t.invalid ?? null, detail: t.detail || '',
      belief: t.belief ?? null, answer: t.answer || '', judge: t.judge || '', names: t.names || '', fakes: t.fakes ?? null,
    })),
    sim_ties: state.ties.map((t) => ({
      key: `${run.run_id}:${edgeKey(num(t.from), num(t.to))}`, run_id: run.run_id, execution, tick: t.tick,
      requester: name(t.from), requester_agent: num(t.from), accepter: name(t.to), accepter_agent: num(t.to),
    })),
  };
}
