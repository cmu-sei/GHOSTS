#!/usr/bin/env node
// Builds corpus/attack-index.json and corpus/attack-groups.json: every ATT&CK technique id and every
// ATT&CK intrusion-set (group) id the validator and the lookup tools will resolve, with names,
// domains, and whether MITRE has revoked or deprecated each. Nothing else — the document holds ids
// and the standard holds the content, so both indexes are resolvers, not copies.
//
//   node build-attack-index.mjs                 (downloads the three bundles, writes both indexes)
//   node build-attack-index.mjs /path/to/dir    (reads <domain>-attack.json from a local dir)
//
// The MITRE bundles are ~30-50 MB each and are never committed. What is committed is this script,
// the two indexes, and the commit the bundles were read at, recorded in each index's builtFrom block.

import { writeFileSync, readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const TECHNIQUES_OUT = join(here, 'attack-index.json');
const GROUPS_OUT = join(here, 'attack-groups.json');
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
const groups = new Map();
const techniqueBundles = [];
const groupBundles = [];

for (const domain of DOMAINS) {
  const b = await bundle(domain);

  let techniqueCount = 0;
  let groupCount = 0;
  for (const obj of b.objects || []) {
    if (obj.type === 'attack-pattern') {
      const id = attackId(obj);
      if (!id || !/^T[0-9]{4}(\.[0-9]{3})?$/.test(id)) continue;
      const entry = techniques.get(id) || { id, name: obj.name, domains: [] };
      if (!entry.domains.includes(domain)) entry.domains.push(domain);
      // Revoked or deprecated anywhere is reported: the id is not one to author against.
      if (obj.revoked === true) entry.revoked = true;
      if (obj.x_mitre_deprecated === true) entry.deprecated = true;
      techniques.set(id, entry);
      techniqueCount++;
    } else if (obj.type === 'intrusion-set') {
      const id = attackId(obj);
      if (!id || !/^G[0-9]{4}$/.test(id)) continue;
      const entry = groups.get(id) || { id, name: obj.name, aliases: obj.aliases || [], domains: [] };
      if (!entry.domains.includes(domain)) entry.domains.push(domain);
      if (obj.revoked === true) entry.revoked = true;
      if (obj.x_mitre_deprecated === true) entry.deprecated = true;
      groups.set(id, entry);
      groupCount++;
    }
  }
  techniqueBundles.push({ domain, file: `${domain}-attack/${domain}-attack.json`, attackPatterns: techniqueCount });
  groupBundles.push({ domain, file: `${domain}-attack/${domain}-attack.json`, intrusionSets: groupCount });
}

const builtAt = new Date().toISOString().slice(0, 10);
const builtFromCommit = await commit();

const orderedTechniques = [...techniques.values()].sort((a, b) => a.id.localeCompare(b.id));
const techniqueIndex = {
  builtFrom: { repo: REPO, ref: REF, commit: builtFromCommit, builtAt, bundles: techniqueBundles },
  count: orderedTechniques.length,
  revoked: orderedTechniques.filter(t => t.revoked).length,
  deprecated: orderedTechniques.filter(t => t.deprecated).length,
  techniques: orderedTechniques,
};

// One technique per line: a 1,000-entry index that a reviewer can diff.
const techniqueBody = orderedTechniques.map(t => '    ' + JSON.stringify(t)).join(',\n');
const techniqueHead = JSON.stringify({ ...techniqueIndex, techniques: undefined }, null, 2).replace(/\n\}$/, '');
writeFileSync(TECHNIQUES_OUT, `${techniqueHead},\n  "techniques": [\n${techniqueBody}\n  ]\n}\n`);
console.error(`wrote ${TECHNIQUES_OUT}: ${orderedTechniques.length} techniques (${techniqueIndex.revoked} revoked, ${techniqueIndex.deprecated} deprecated)`);

const orderedGroups = [...groups.values()].sort((a, b) => a.id.localeCompare(b.id));
const groupIndex = {
  builtFrom: { repo: REPO, ref: REF, commit: builtFromCommit, builtAt, bundles: groupBundles },
  count: orderedGroups.length,
  revoked: orderedGroups.filter(g => g.revoked).length,
  deprecated: orderedGroups.filter(g => g.deprecated).length,
  groups: orderedGroups,
};

// One group per line, same reasoning as the technique index.
const groupBody = orderedGroups.map(g => '    ' + JSON.stringify(g)).join(',\n');
const groupHead = JSON.stringify({ ...groupIndex, groups: undefined }, null, 2).replace(/\n\}$/, '');
writeFileSync(GROUPS_OUT, `${groupHead},\n  "groups": [\n${groupBody}\n  ]\n}\n`);
console.error(`wrote ${GROUPS_OUT}: ${orderedGroups.length} groups (${groupIndex.revoked} revoked, ${groupIndex.deprecated} deprecated)`);
