// Decide which participants this execution asks. Input: every agent_turns row (Read: agent_turns).
// Output: up to batch_size items, one per participant still unanswered for the picked run,
// or a single { noWork: true } item when the run has nobody left.
const run = $('Pick run').first().json.run;
const participants = $('Read: participants').all().map((i) => i.json);
const turns = $input.all().map((i) => i.json).filter((t) => t && t.run_id === run.run_id);
const answered = new Set(turns.map((t) => String(t.participant_id)));

const todo = participants
  .filter((p) => p && p.persona && !answered.has(String(p.participant_id)))
  .sort((a, b) => Number(a.participant_id) - Number(b.participant_id));

if (todo.length === 0) return [{ json: { noWork: true, remaining: 0, run_id: run.run_id } }];

return todo.slice(0, run.batch_size).map((p) => ({
  json: {
    noWork: false,
    remaining: todo.length,
    run_id: run.run_id,
    provider: run.provider,
    model: run.model,
    temperature: run.temperature,
    prompt_version: run.prompt_version,
    participant_id: Number(p.participant_id),
    persona: p.persona,
  },
}));
