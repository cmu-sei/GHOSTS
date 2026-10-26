// Turn the model's reply into one agent_turns row: the bookkeeping columns plus tda1..tda40 when the
// reply parsed cleanly. ok is true only when every adjective appears exactly once with an integer 1-9.
const ctx = $('Build prompt').first().json;
const reply = $input.first().json || {};
const text = typeof reply.text === 'string' ? reply.text : typeof reply.output === 'string' ? reply.output : '';
const now = new Date();
const iso = now.toISOString().replace(/\.\d{3}Z$/, '+00:00');

const byAdjective = new Map(ctx.adjectives.map((a, i) => [a.toLowerCase(), { item_key: `tda${i + 1}`, text: a }]));
const found = new Map();
const problems = [];

if (!text) {
  problems.push(`llm: ${reply.error || 'empty reply'}`);
} else {
  const linePattern = /^\s*(?:[-*•]\s*)?(?:\d{1,2}\s*[.)]\s*)?[*_]*([A-Za-z][A-Za-z\- ]*?)[*_]*\s*(?:[:\-–—]|\s)\s*[*_]*(\d{1,2})[*_]*\s*[.)]?\s*$/;
  for (const raw of text.split(/\r?\n/)) {
    const m = raw.match(linePattern);
    if (!m) continue;
    const entry = byAdjective.get(m[1].trim().toLowerCase());
    if (!entry) continue;
    const value = Number(m[2]);
    if (found.has(entry.item_key)) {
      problems.push(`duplicate: ${entry.text}`);
      continue;
    }
    if (!Number.isInteger(value) || value < 1 || value > 9) {
      problems.push(`out of range: ${entry.text} = ${value}`);
      continue;
    }
    found.set(entry.item_key, value);
  }
  const missing = ctx.adjectives.filter((a) => !found.has(byAdjective.get(a.toLowerCase()).item_key));
  if (missing.length) problems.push(`missing: ${missing.join(', ')}`);
}

const ok = problems.length === 0 && found.size === ctx.adjectives.length;
const values = ok ? Object.fromEntries(found) : {};

return [{
  json: {
    key: `${ctx.run_id}:${ctx.participant_id}`,
    run_id: ctx.run_id,
    participant_id: ctx.participant_id,
    provider: ctx.provider,
    model: ctx.model,
    prompt_version: ctx.prompt_version,
    raw_output: text,
    parsed_count: found.size,
    ok,
    error: problems.join('; ').slice(0, 2000),
    duration_ms: Math.max(0, now.getTime() - Number(ctx.startedAt || now.getTime())),
    answered_at: iso,
    ...values,
  },
}];
