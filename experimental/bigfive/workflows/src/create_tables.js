// Create Tables: sign in to Baserow as a user (database tokens cannot create tables), create the eight
// study tables that are missing in the given database, add missing columns to existing ones, and refuse
// on a column whose type differs. Output: one item per table with its id and what was done.
const form = $input.first().json;
const field = (name) => {
  const want = name.toLowerCase().replace(/[^a-z0-9]/g, '');
  for (const k of Object.keys(form)) if (k.toLowerCase().replace(/[^a-z0-9]/g, '').startsWith(want)) return form[k];
  return undefined;
};
const baseUrl = String(field('Baserow URL') || '').trim().replace(/\/+$/, '');
const email = String(field('Email') || '').trim();
const password = String(field('Password') || '');
const databaseId = Number(field('Database ID'));
if (!baseUrl || !email || !password) throw new Error('Baserow URL, Email and Password are required');
if (!Number.isInteger(databaseId)) throw new Error(`Database ID must be a number, got "${field('Database ID')}"`);

const text = { type: 'text' };
const long = { type: 'long_text' };
const bool = { type: 'boolean' };
const date = { type: 'date', date_include_time: true, date_format: 'ISO' };
const int = { type: 'number', number_decimal_places: 0, number_negative: false };
const num = { type: 'number', number_decimal_places: 3, number_negative: true };
// p-values need more precision than 3 decimals; Baserow rejects a value with more decimals than the field allows.
const pval = { type: 'number', number_decimal_places: 6, number_negative: false };
const range = (prefix, n) => Object.fromEntries(Array.from({ length: n }, (_, i) => [`${prefix}${i + 1}`, int]));
const FACETS = ['extraversion', 'agreeableness', 'conscientiousness', 'emotional_stability', 'openness'];
const perFacet = (prefixes, type) => Object.fromEntries(FACETS.flatMap((f) => prefixes.map((p) => [p ? `${p}_${f}` : f, type])));
const perFacetSuffix = (suffixes) => Object.fromEntries(FACETS.flatMap((f) => suffixes.map((s) => [`${f}_${s}`, num])));

// `key` is the primary field of every table and is created with the table.
const TABLES = {
  participants: { participant_id: int, age: int, gender: text, ethnicity: text, persona: long, has_mini_markers: bool },
  items: { instrument: text, item_key: text, position: int, text, facet: text, reverse: bool },
  human_bfi2: { participant_id: int, ...range('bfi', 60) },
  human_mini_markers: { participant_id: int, ...range('tda', 40) },
  runs: {
    run_id: text, source: text, provider: text, model: text, temperature: num, prompt_version: text, batch_size: int, status: text,
    participants_total: int, participants_done: int, participants_failed: int,
    started_at: date, last_batch_at: date, finished_at: date, locked_at: date, locked_by: text, analyzed_at: date,
  },
  agent_turns: {
    run_id: text, participant_id: int, provider: text, model: text, prompt_version: text, raw_output: long,
    parsed_count: int, ok: bool, error: long, duration_ms: int, answered_at: date, ...range('tda', 40),
  },
  scores: { subject: text, run_id: text, participant_id: int, ...perFacet([''], num) },
  comparisons: { run_id: text, participant_id: int, ...perFacet(['agent', 'human', 'diff'], num), mean_abs_diff: num },
  run_summaries: {
    run_id: text, model: text, provider: text, n: int, mean_abs_diff: num,
    ...perFacetSuffix(['shift', 'mae', 'r', 'r_ci_low', 'r_ci_high', 'baseline_r']),
    ...Object.fromEntries(FACETS.flatMap((f) => ['shift_p', 'shift_p_holm', 'steiger_p'].map((s) => [`${f}_${s}`, pval]))),
    analyzed_at: date,
  },
};

const call = async (method, path, body, token) => this.helpers.httpRequest({
  method,
  url: `${baseUrl}${path}`,
  headers: token ? { Authorization: `JWT ${token}` } : {},
  body,
  json: true,
});

let token;
try {
  token = (await call('POST', '/api/user/token-auth/', { email, password })).access_token;
} catch (e) {
  throw new Error(`Baserow sign-in failed for ${email} at ${baseUrl}: ${e.message}`);
}
const existingTables = await call('GET', `/api/database/tables/database/${databaseId}/`, undefined, token);
const byName = new Map(existingTables.map((t) => [t.name, t]));

const results = [];
const mismatches = [];
for (const [name, fields] of Object.entries(TABLES)) {
  let table = byName.get(name);
  let created = false;
  if (!table) {
    table = await call('POST', `/api/database/tables/database/${databaseId}/`, { name, data: [['key']], first_row_header: true }, token);
    created = true;
  }
  const existingFields = await call('GET', `/api/database/fields/table/${table.id}/`, undefined, token);
  const have = new Map(existingFields.map((f) => [f.name, f]));
  const primary = existingFields.find((f) => f.primary);
  if (!primary || primary.name !== 'key' || primary.type !== 'text') mismatches.push(`${name}: primary field must be a text field named "key"`);
  const added = [];
  const adjusted = [];
  for (const [fieldName, def] of Object.entries(fields)) {
    const cur = have.get(fieldName);
    if (!cur) {
      await call('POST', `/api/database/fields/table/${table.id}/`, { name: fieldName, ...def }, token);
      added.push(fieldName);
    } else if (cur.type !== def.type) {
      mismatches.push(`${name}.${fieldName}: expected ${def.type}, found ${cur.type}`);
    } else if (def.type === 'number' && (cur.number_decimal_places !== def.number_decimal_places || cur.number_negative !== def.number_negative)) {
      // Safe to change in place: Baserow rounds or pads existing values.
      await call('PATCH', `/api/database/fields/${cur.id}/`, { number_decimal_places: def.number_decimal_places, number_negative: def.number_negative }, token);
      adjusted.push(fieldName);
    }
  }
  results.push({ json: { table: name, table_id: table.id, created, fields_added: added.length, fields_adjusted: adjusted.length, fields_total: Object.keys(fields).length + 1 } });
}
if (mismatches.length) throw new Error(`Column type mismatch (nothing was deleted; rename or fix these and run again): ${mismatches.join('; ')}`);
return results;
