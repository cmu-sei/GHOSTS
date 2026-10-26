// Pick the run this execution works on: a running run first, else the oldest queued one.
// Output: one item with the lock update body for the runs table (locked_at + locked_by = this execution),
// plus the run itself under `run` (ignored by the Baserow node via Inputs to Ignore). No item when
// there is nothing to do.
const config = $('Configuration').first().json;
const runs = $input.all().map((i) => i.json).filter((r) => r && r.run_id);
const byAge = (a, b) => String(a.started_at || '').localeCompare(String(b.started_at || '')) || a.id - b.id;
const running = runs.filter((r) => r.status === 'running').sort(byAge);
const queued = runs.filter((r) => r.status === 'queued').sort((a, b) => a.id - b.id);
const run = running[0] || queued[0];
if (!run) return [];

const now = new Date();
const lockMs = (Number(config.lockMinutes) || 30) * 60 * 1000;
if (run.locked_at && now.getTime() - Date.parse(run.locked_at) < lockMs) {
  // Another execution is still working on this run; leave it alone.
  return [];
}

const provider = String(run.provider || '').trim().toLowerCase();
if (!['ollama', 'bedrock'].includes(provider)) throw new Error(`run ${run.run_id}: provider must be ollama or bedrock, got "${run.provider}"`);
if (!String(run.model || '').trim()) throw new Error(`run ${run.run_id}: model is required`);
if (run.prompt_version && run.prompt_version !== config.promptVersion) {
  throw new Error(`run ${run.run_id}: prompt_version "${run.prompt_version}" does not match this workflow's prompt "${config.promptVersion}"`);
}
const batchSize = Math.max(1, Math.floor(Number(run.batch_size) || 25));
const temperature = run.temperature === '' || run.temperature === null || run.temperature === undefined ? null : Number(run.temperature);

const iso = now.toISOString().replace(/\.\d{3}Z$/, '+00:00');
return [{
  json: {
    run: { ...run, provider, model: String(run.model).trim(), batch_size: batchSize, temperature, prompt_version: config.promptVersion },
    status: 'running',
    locked_at: iso,
    locked_by: $execution.id,
    started_at: run.started_at || iso,
    prompt_version: config.promptVersion,
    batch_size: batchSize,
  },
}];
