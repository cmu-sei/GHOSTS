// Add Run: validate the form and produce the `runs` row. Input: every existing runs row (Read: runs).
// With an uploaded answers file (Parse agent answers ran) the run is stored finished, source "import";
// otherwise it is queued for Run Agents.
const form = $('Add run form').first().json;
const existing = $input.all().map((i) => i.json).filter((r) => r && r.run_id);
const imported = $('Parse agent answers').isExecuted ? $('Parse agent answers').first().json : null;

const field = (name) => {
  const want = name.toLowerCase().replace(/[^a-z0-9]/g, '');
  for (const k of Object.keys(form)) if (k.toLowerCase().replace(/[^a-z0-9]/g, '').startsWith(want)) return form[k];
  return undefined;
};
const run_id = String(field('Run ID') ?? '').trim();
const provider = String(field('Provider') ?? '').trim().toLowerCase();
const model = String(field('Model') ?? '').trim();
const temperature = field('Temperature') === '' || field('Temperature') === undefined || field('Temperature') === null ? 0.7 : Number(field('Temperature'));
const batch_size = Math.max(1, Math.floor(Number(field('Batch size')) || 25));

if (!run_id) throw new Error('Run ID is required');
if (!/^[A-Za-z0-9._-]+$/.test(run_id)) throw new Error(`Run ID "${run_id}" may only contain letters, digits, ".", "_" and "-"`);
if (existing.some((r) => String(r.run_id) === run_id)) throw new Error(`Run "${run_id}" already exists; pick another Run ID`);
if (!['ollama', 'bedrock'].includes(provider)) throw new Error(`Provider must be ollama or bedrock, got "${provider}"`);
if (!model) throw new Error('Model is required');
if (!Number.isFinite(temperature)) throw new Error(`Temperature must be a number, got "${field('Temperature')}"`);

const iso = new Date().toISOString().replace(/\.\d{3}Z$/, '+00:00');
const row = {
  key: run_id,
  run_id,
  source: imported ? 'import' : 'llm',
  provider,
  model,
  temperature,
  prompt_version: imported ? '' : 'mm-v1',
  batch_size,
  status: imported ? 'done' : 'queued',
  participants_total: imported ? imported.count : 0,
  participants_done: imported ? imported.ok : 0,
  participants_failed: imported ? imported.count - imported.ok : 0,
  // Date fields take an ISO timestamp or null; Baserow rejects ''.
  started_at: imported ? iso : null,
  last_batch_at: imported ? iso : null,
  finished_at: imported ? iso : null,
  locked_at: null,
  locked_by: '',
  analyzed_at: null,
};
return [{ json: row }];
