// After every participant: refresh the run's lock so a long batch is never mistaken for a dead one.
// Output: the update body for the runs table (Touch lock ignores `id` and uses it as the row id).
const run = $('Pick run').first().json.run;
const iso = new Date().toISOString().replace(/\.\d{3}Z$/, '+00:00');
return [{ json: { id: run.id, locked_at: iso, locked_by: $execution.id } }];
