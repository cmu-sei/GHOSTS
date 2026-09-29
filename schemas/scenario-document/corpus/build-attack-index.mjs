#!/usr/bin/env node
// Builds corpus/attack-index.json: every ATT&CK technique id the validator will accept, with
// its name, its domains, and whether MITRE has revoked or deprecated it. Nothing else — the
// document holds ids and the standard holds the content, so the index is a resolver, not a copy.
//
//   node build-attack-index.mjs                 (downloads the three bundles, writes the index)
//   node build-attack-index.mjs /path/to/dir    (reads <domain>-attack.json from a local dir)
//
// The MITRE bundles are ~30 MB each and are never committed. What is committed is this script,
// the index, and the commit the bundles were read at, recorded in the index's builtFrom block.

import { writeFileSync, readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const OUT = join(here, 'attack-index.json');
const REPO = 'mitre-attack/attack-stix-data';
const REF = 'master';
const DOMAINS = ['enterprise', 'ics', 'mobile'];
const localDir = process.argv[2];

async function bundle(domain) {
  const file = `${domain}-attack/${domain}-attack.json`;
  if (localDir) {
    const path = join(localDir, `${domain}-attack.json`);
    if (!existsSync(path)) throw new Error(`missing ${path}`);
    console.error(`reading ${path}`);
    return JSON.parse(readFileSync(path, 'utf8'));
  }
  const url = `https://raw.githubusercontent.com/${REPO}/${REF}/${file}`;
  console.error(`fetching ${url}`);
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${url}: HTTP ${res.status}`);
  return res.json();
}

// The commit the bundles were read at, so a reviewer can rebuild exactly this index.
async function commit() {
  if (localDir) return null;
  const res = await fetch(`https://api.github.com/repos/${REPO}/commits/${REF}`, {
    headers: { accept: 'application/vnd.github+json' },
  });
  if (!res.ok) return null;
  const body = await res.json();
  return { sha: body.sha, date: body.commit?.committer?.date };
}

const attackId = obj =>
  (obj.external_references || []).find(r => r.source_name === 'mitre-attack')?.external_id || null;

const techniques = new Map();
const bundles = [];

for (const domain of DOMAINS) {
  const b = await bundle(domain);
  let count = 0;
  for (const obj of b.objects || []) {
    if (obj.type !== 'attack-pattern') continue;
    const id = attackId(obj);
    if (!id || !/^T[0-9]{4}(\.[0-9]{3})?$/.test(id)) continue;
    const entry = techniques.get(id) || { id, name: obj.name, domains: [] };
    if (!entry.domains.includes(domain)) entry.domains.push(domain);
    // Revoked or deprecated anywhere is reported: the id is not one to author against.
    if (obj.revoked === true) entry.revoked = true;
    if (obj.x_mitre_deprecated === true) entry.deprecated = true;
    techniques.set(id, entry);
    count++;
  }
  bundles.push({ domain, file: `${domain}-attack/${domain}-attack.json`, attackPatterns: count });
}

const ordered = [...techniques.values()].sort((a, b) => a.id.localeCompare(b.id));
const index = {
  builtFrom: { repo: REPO, ref: REF, commit: await commit(), builtAt: new Date().toISOString().slice(0, 10), bundles },
  count: ordered.length,
  revoked: ordered.filter(t => t.revoked).length,
  deprecated: ordered.filter(t => t.deprecated).length,
  techniques: ordered,
};

// One technique per line: a 1,000-entry index that a reviewer can diff.
const body = ordered.map(t => '    ' + JSON.stringify(t)).join(',\n');
const head = JSON.stringify({ ...index, techniques: undefined }, null, 2).replace(/\n\}$/, '');
writeFileSync(OUT, `${head},\n  "techniques": [\n${body}\n  ]\n}\n`);
console.error(`wrote ${OUT}: ${ordered.length} techniques (${index.revoked} revoked, ${index.deprecated} deprecated)`);
