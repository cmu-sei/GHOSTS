// Add Run: read an uploaded sheet of agent answers (Participant ID, tda1..tda40) into one item
// { answers: [{ participant_id, values: { tda1.. }, ok, parsed_count, error }] }.
const rows = $input.all().map((i) => i.json);
const col = (row, name) => {
  const want = name.toLowerCase().replace(/[\s_]/g, '');
  for (const k of Object.keys(row)) if (k.toLowerCase().replace(/[\s_]/g, '') === want) return row[k];
  return undefined;
};
const answers = [];
for (const row of rows) {
  const raw = col(row, 'Participant ID') ?? col(row, 'participant_id') ?? col(row, 'pid');
  const pid = Number(raw);
  if (raw === null || raw === undefined || raw === '' || !Number.isInteger(pid)) continue;
  const values = {};
  const problems = [];
  for (let i = 1; i <= 40; i++) {
    const v = col(row, `tda${i}`);
    if (v === null || v === undefined || v === '') continue;
    const n = Number(v);
    if (!Number.isInteger(n) || n < 1 || n > 9) { problems.push(`out of range: tda${i} = ${v}`); continue; }
    values[`tda${i}`] = n;
  }
  const parsed_count = Object.keys(values).length;
  if (parsed_count === 0) continue;
  const missing = [];
  for (let i = 1; i <= 40; i++) if (values[`tda${i}`] === undefined) missing.push(`tda${i}`);
  if (missing.length) problems.push(`missing: ${missing.join(', ')}`);
  answers.push({ participant_id: pid, values, ok: problems.length === 0, parsed_count, error: problems.join('; ').slice(0, 2000) });
}
if (!answers.length) throw new Error('The agent answers file has no rows with a Participant ID and tda1..tda40 columns');
return [{ json: { answers, count: answers.length, ok: answers.filter((a) => a.ok).length } }];
