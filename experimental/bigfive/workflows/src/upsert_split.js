// Upsert helper, one node per table and mode. build.mjs replaces __SOURCE__ (the node whose single
// output item holds the staged rows under __TABLE__), __TABLE__ (table name, also the "Read: <table>"
// node that fetched the existing rows) and __MODE__ ("new" or "changed").
//  new:     staged rows whose key is not in the table yet  -> Batch Create
//  changed: existing rows whose staged values differ, as { id, ...staged } -> Update
// Values are compared loosely (Baserow returns numbers as strings; '' and null are the same).
const staged = $('__SOURCE__').first().json['__TABLE__'] || [];
const existing = $('Read: __TABLE__').all().map((i) => i.json).filter((r) => r && r.id !== undefined && r.key !== undefined);
const byKey = new Map(existing.map((r) => [String(r.key), r]));

const norm = (v) => {
  if (v === null || v === undefined || v === '') return '';
  if (typeof v === 'boolean') return v ? 'true' : 'false';
  if (v === 'true' || v === 'false') return v;
  const n = Number(v);
  if (typeof v === 'number' || (typeof v === 'string' && v.trim() !== '' && Number.isFinite(n))) return String(Number(n.toFixed(6)));
  return String(v).trim();
};
const differs = (row, cur) => Object.keys(row).some((k) => norm(row[k]) !== norm(cur[k]));

const out = [];
for (const row of staged) {
  const cur = byKey.get(String(row.key));
  if ('__MODE__' === 'new' && !cur) out.push({ json: row });
  if ('__MODE__' === 'changed' && cur && differs(row, cur)) out.push({ json: { id: cur.id, ...row } });
}
return out;
