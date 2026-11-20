// Pick the run this execution simulates. Runs go one at a time per provider (a local Ollama run and a
// Bedrock run can go side by side): a provider with a freshly locked run is busy. A run whose lock went stale (its execution died) is started again from scratch;
// rows carry the execution id, so whatever the dead attempt wrote is ignored by the analysis.
// Output: the lock update body for sim_runs plus the run under `run`; no item when there is nothing to do.
const LOCK_MINUTES = 30;
const runs = $input.all().map((i) => i.json).filter((r) => r && r.run_id);
const now = new Date();
const fresh = (r) => r.locked_at && now.getTime() - Date.parse(r.locked_at) < LOCK_MINUTES * 60 * 1000;
const running = runs.filter((r) => r.status === 'running');
const busy = new Set(running.filter(fresh).map((r) => r.provider));
const run = running.find((r) => !fresh(r)) || runs.filter((r) => r.status === 'queued' && !busy.has(r.provider)).sort((a, b) => a.id - b.id)[0];
if (!run) return [];
for (const k of ['provider', 'judge_provider']) if (!['ollama', 'bedrock'].includes(run[k])) throw new Error(`run ${run.run_id}: ${k} must be ollama or bedrock, got "${run[k]}"`);

const iso = now.toISOString().replace(/\.\d{3}Z$/, '+00:00');
return [{
  json: {
    run: { ...run, repeat: Number(run.repeat), ticks: Number(run.ticks), temperature: run.temperature === null || run.temperature === '' ? null : Number(run.temperature) },
    status: 'running', progress: 'starting', locked_at: iso, locked_by: $execution.id, started_at: iso,
  },
}];
