// Add Run: one agent_turns row per uploaded answer. Nothing when the run was queued without a file.
if (!$('Parse agent answers').isExecuted) return [];
const run = $('Prepare run').first().json;
const { answers } = $('Parse agent answers').first().json;
const iso = new Date().toISOString().replace(/\.\d{3}Z$/, '+00:00');
return answers.map((a) => ({
  json: {
    key: `${run.run_id}:${a.participant_id}`,
    run_id: run.run_id,
    participant_id: a.participant_id,
    provider: run.provider,
    model: run.model,
    prompt_version: run.prompt_version,
    raw_output: '',
    parsed_count: a.parsed_count,
    ok: a.ok,
    error: a.error,
    duration_ms: 0,
    answered_at: iso,
    ...a.values,
  },
}));
