// After the batch: recount from the re-read agent_turns and produce the runs-table update body.
// Input: the re-read runs rows (so a status change made during the batch, e.g. paused, is respected).
// The run is done when every participant with a persona has a turn (ok or failed); otherwise it stays
// running and the schedule trigger picks it up again. The lock is released only if this execution
// still owns it; another execution's lock is left untouched.
const run = $('Pick run').first().json.run;
const participants = $('Read: participants').all().map((i) => i.json).filter((p) => p && p.persona);
const turns = $('Read: agent_turns (recount)').all().map((i) => i.json).filter((t) => t && t.run_id === run.run_id);
const current = $input.all().map((i) => i.json).find((r) => r && r.id === run.id) || run;

const latest = new Map();
for (const t of turns) {
  const pid = String(t.participant_id);
  const prev = latest.get(pid);
  if (!prev || String(t.answered_at || '') >= String(prev.answered_at || '')) latest.set(pid, t);
}
const done = [...latest.values()].filter((t) => t.ok === true).length;
const failed = latest.size - done;
const remaining = participants.filter((p) => !latest.has(String(p.participant_id))).length;
const iso = new Date().toISOString().replace(/\.\d{3}Z$/, '+00:00');

let status = remaining === 0 ? 'done' : 'running';
if (current.status === 'paused') status = 'paused';

const body = {
  id: run.id,
  status,
  participants_total: participants.length,
  participants_done: done,
  participants_failed: failed,
  last_batch_at: iso,
  finished_at: status === 'done' ? iso : null,
};
if (!current.locked_by || current.locked_by === $execution.id) {
  body.locked_at = null;
  body.locked_by = '';
}
return [{ json: body }];
