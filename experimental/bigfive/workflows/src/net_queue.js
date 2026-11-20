// Queue Runs: validate the form and produce one sim_runs row per run. Input: the existing sim_runs rows.
// Per label: propagation = `repeats` LLM runs + 2 no-seed control runs; structure = `repeats` LLM runs;
// dynamics = `repeats` rounded up to an even number, alternating which claim starts at the hub.
const form = $('Queue runs form').first().json;
const existing = new Set($input.all().map((i) => i.json).filter((r) => r && r.run_id).map((r) => String(r.run_id)));
const text = (k) => String(form[k] ?? '').trim();
const label = text('Label');
const provider = text('Provider');
const model = text('Model');
// No temperature = the model's own default; some models refuse the parameter.
const temperature = text('Temperature') === '' ? null : Number(text('Temperature'));
const repeats = Math.floor(Number(text('Repeats'))) || 5;
const days = Math.floor(Number(text('Days'))) || 2;
const judge_provider = text('Judge provider');
const judge_model = text('Judge model');
if (!/^[A-Za-z0-9._-]+$/.test(label)) throw new Error(`Label "${label}" may only contain letters, digits, ".", "_" and "-"`);
if (!model || !judge_model) throw new Error('Model and Judge model are required');
if (temperature !== null && !Number.isFinite(temperature)) throw new Error(`Temperature must be a number, got "${text('Temperature')}"`);

const wanted = text('Experiment') === 'all' ? ['propagation', 'structure', 'dynamics'] : [text('Experiment')];
const plan = [];
for (const experiment of wanted) {
  if (experiment === 'propagation') {
    for (let r = 1; r <= repeats; r++) plan.push({ experiment, arm: 'llm', condition: '', repeat: r });
    for (let r = 1; r <= Math.min(2, repeats); r++) plan.push({ experiment, arm: 'control', condition: '', repeat: r });
  } else if (experiment === 'structure') {
    for (let r = 1; r <= repeats; r++) plan.push({ experiment, arm: 'llm', condition: '', repeat: r });
  } else if (experiment === 'dynamics') {
    for (let r = 1; r <= 2 * Math.ceil(repeats / 2); r++) plan.push({ experiment, arm: 'llm', condition: r % 2 ? 'on_at_hub' : 'off_at_hub', repeat: r });
  }
}
const rows = plan.map((p) => {
  const run_id = `${label}-${p.experiment}-${p.arm}-${p.repeat}`;
  if (existing.has(run_id)) throw new Error(`Run "${run_id}" already exists; use another Label`);
  return {
    key: run_id, run_id, label, ...p, provider, model, temperature, judge_provider, judge_model, ticks: days * 8,
    // Date fields take an ISO timestamp or null; Baserow rejects ''.
    status: 'queued', progress: '', calls: 0, failures: 0, messages: 0, execution: '', started_at: null, finished_at: null, locked_at: null, locked_by: '',
  };
});
return rows.map((json) => ({ json }));
