#!/usr/bin/env node
// Scenario-document tooling for Step 1: validate, canonicalize, convert the RPG fixtures,
// and check that a conversion kept every value from its source.
//
//   node scenario-doc.mjs validate <doc.json>...
//   node scenario-doc.mjs crosscheck <doc.json>...        (ajv's verdict and failing paths, as JSON)
//   node scenario-doc.mjs canonicalize <doc.json>            (rewrites the file in canonical form)
//   node scenario-doc.mjs convert <fixture.json> <out.json>  (either fixture shape → document)
//   node scenario-doc.mjs coverage <fixture.json> <doc.json> (every source value appears in the doc)
//   node scenario-doc.mjs diff <a.json> <b.json>             (path-level differences)
//
// Needs ajv and ajv-formats on the module path.

import { readFileSync, writeFileSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const here = dirname(fileURLToPath(import.meta.url));
const SCHEMA_PATH = join(here, '..', 'v1', 'scenario-document.schema.json');
const schema = JSON.parse(readFileSync(SCHEMA_PATH, 'utf8'));

// ───────────────────────── validate ─────────────────────────

function compile() {
  const Ajv = require('ajv/dist/2020').default;
  const addFormats = require('ajv-formats').default;
  // strictRequired is off because vulnerabilities[] says "cve or description" with anyOf/required,
  // which strict mode misreads as an undefined property.
  const ajv = new Ajv({ allErrors: true, strict: true, strictRequired: false, allowUnionTypes: true });
  addFormats(ajv);
  return ajv.compile(schema);
}

function validate(files) {
  const check = compile();
  let failed = 0;
  for (const f of files) {
    const doc = JSON.parse(readFileSync(f, 'utf8'));
    if (check(doc)) {
      console.log(`ok    ${f}`);
    } else {
      failed++;
      console.log(`FAIL  ${f}`);
      for (const e of check.errors) console.log(`      ${e.instancePath || '/'} ${e.message}${e.params?.allowedValues ? ' ' + JSON.stringify(e.params.allowedValues) : ''}`);
    }
  }
  return failed;
}

// ─────────────────────── crosscheck ─────────────────────────
// The failing instance locations ajv reports, per file, as a fixture the .NET tier-1 validator is
// held to. The API image has no node, so the .NET validator is the one that runs in production and
// this is the only thing that keeps the two from drifting apart.
//
// One normalization: ajv reports an additionalProperties error at the *parent* and names the
// offending key in params; the pointer to the offending value is the parent plus that key, which is
// what a finding should carry and what the .NET validator reports.

const pointer = e =>
  (e.keyword === 'additionalProperties'
    ? `${e.instancePath}/${e.params.additionalProperty}`
    : e.instancePath) || '/';

function crosscheck(files) {
  const check = compile();
  const out = {};
  for (const f of files) {
    const valid = check(JSON.parse(readFileSync(f, 'utf8')));
    out[basename(f)] = {
      valid,
      paths: valid ? [] : [...new Set(check.errors.map(pointer))].sort(),
    };
  }
  console.log(JSON.stringify(out, null, 2));
  return 0;
}

// ─────────────────────── canonicalize ───────────────────────
// One serialization per scenario: keys in schema order, free-key objects sorted,
// set-like arrays sorted, empty optional values omitted, values equal to their schema
// default omitted, 2-space indent, trailing newline.

const SET_ARRAYS = new Set(['flags', 'setFlags', 'techniques', 'objectives']);

function resolve(node) {
  if (node && node.$ref) {
    const path = node.$ref.replace(/^#\//, '').split('/');
    let t = schema;
    for (const p of path) t = t[p];
    return { ...t, ...node, $ref: undefined };
  }
  return node;
}

function canonical(value, node, key) {
  node = resolve(node);
  if (Array.isArray(value)) {
    const itemNode = node?.items;
    let out = value.map(v => canonical(v, itemNode, key));
    if (SET_ARRAYS.has(key) && out.every(v => typeof v !== 'object')) out = [...out].sort();
    return out;
  }
  if (value && typeof value === 'object') {
    const props = node?.properties || {};
    const ordered = Object.keys(props).filter(k => k in value);
    const free = Object.keys(value).filter(k => !(k in props)).sort();
    const out = {};
    for (const k of [...ordered, ...free]) {
      const child = resolve(props[k] || node?.additionalProperties);
      const v = canonical(value[k], child, k);
      const required = (node?.required || []).includes(k);
      if (!required && isEmpty(v)) continue;
      // A value equal to its schema default says nothing the default does not, so the
      // document that omits it and the document that states it are the same document.
      if (!required && child && 'default' in child && v === child.default) continue;
      out[k] = v;
    }
    return out;
  }
  return value;
}

function isEmpty(v) {
  return v === undefined || v === null || v === '' ||
    (Array.isArray(v) && v.length === 0) ||
    (v && typeof v === 'object' && Object.keys(v).length === 0);
}

function serialize(doc) {
  return JSON.stringify(canonical(doc, schema, null), null, 2) + '\n';
}

// ───────────────────────── convert ──────────────────────────

const slugify = s => s.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 79) || 'x';
const OWNER = { 'White Cell': 'white-cell', 'Red Team': 'red-team', 'Blue Team': 'blue-team', 'Green Cell': 'green-cell', None: 'unassigned', '': 'unassigned' };
const owner = s => OWNER[s] ?? (OWNER[s?.trim()] ?? 'unassigned');
const isTechnique = s => /^T[0-9]{4}(\.[0-9]{3})?$/.test(s);
const isCve = s => /^CVE-[0-9]{4}-[0-9]{4,}$/.test(s);
const isOffset = s => /^T\+(?=\d)(\d+d)?(\d+h)?(\d+m)?$/.test(s);

function minutesToDuration(min) {
  min = Math.round(min);
  const d = Math.floor(min / 1440), h = Math.floor((min % 1440) / 60), m = min % 60;
  return (d ? `${d}d` : '') + (h ? `${h}h` : '') + (m || (!d && !h) ? `${m}m` : '');
}

const notes = [];
const note = s => notes.push(s);

// The document says what the fixture says. GHOSTS stores one duration and stores it in whole hours
// (game_mechanics.duration_hours is an integer), so a sub-hour exercise cannot be imported until that
// column holds minutes — but bending the document to the column would make the example an argument
// that integer hours is enough. So the minutes survive here and the note is a refusal, not a change:
// an import of this document reports TIME_DURATION_NOT_STORABLE and writes nothing.
function faithfulDuration(minutes, slug, source) {
  if (minutes % 60 !== 0) {
    note(`${slug}: ${source} is ${minutes} minutes; the document says so, and GHOSTS cannot store it ` +
      `(duration_hours is an integer), so an import refuses with TIME_DURATION_NOT_STORABLE`);
  }
  return minutesToDuration(minutes);
}

function convert(fixture, slug) {
  const s = fixture.scenario;
  return s.scenarioParameters ? convertApiShape(fixture, slug) : convertKriegspielShape(fixture, slug);
}

// Shape A: the three-GETs export of the live API (scenario + graph + objectives).
function convertApiShape(fx, slug) {
  const s = fx.scenario, p = s.scenarioParameters, te = s.technicalEnvironment, gm = s.gameMechanics, tl = s.timeline;
  const doc = { schemaVersion: '1.1.0', slug, name: s.name, description: s.description };
  if (fx.catalog) doc.catalog = fx.catalog;
  doc.context = { political: p.politicalContext };
  doc.audience = { role: p.playerRole, rulesOfEngagement: p.rulesOfEngagement };
  doc.sides = p.nations.map(n => ({ name: n.name, alignment: n.alignment }));
  doc.adversaries = p.threatActors.map(a => {
    const techniques = a.ttps.filter(isTechnique), capabilities = a.ttps.filter(t => !isTechnique(t));
    if (capabilities.length) note(`${slug}: threat actor "${a.name}": ${capabilities.length} of ${a.ttps.length} ttps are not ATT&CK ids → capabilities[]`);
    return { id: slugify(a.name), name: a.name, type: a.type, capability: a.capability, techniques, capabilities };
  });
  doc.terrain = {
    summary: { topology: te.networkTopology, services: te.services, assets: te.assets },
    defenses: (te.defenses || []).map(d => ({ name: d })),
    vulnerabilities: (te.vulnerabilities || []).map(v => {
      if (!isCve(v.cve)) note(`${slug}: vulnerability on "${v.asset}": "${v.cve}" is not a CVE id → description`);
      return isCve(v.cve) ? { asset: v.asset, cve: v.cve, severity: v.severity } : { asset: v.asset, description: v.cve, severity: v.severity };
    }),
  };
  doc.population = { pools: p.userPools.map(u => ({ role: u.role, count: u.count })) };
  const rop = {
    pacing: gm.timelineType,
    duration: faithfulDuration(Math.round(gm.durationHours * 60), slug, `gameMechanics.durationHours=${gm.durationHours}`),
    adjudication: gm.adjudicationType,
    telemetry: gm.telemetry && { logs: gm.telemetry.collectLogs, network: gm.telemetry.collectNetwork, endpoint: gm.telemetry.collectEndpoint, chat: gm.telemetry.collectChat },
    escalationLadder: { summary: gm.escalationLadder },
    branching: { summary: gm.branchingLogic },
  };
  if (gm.windowLabel) rop.clock = { label: gm.windowLabel };
  if (gm.containmentDeadlineMinutes != null) {
    rop.deadline = { at: `T+${gm.containmentDeadlineMinutes}m`, label: gm.deadlineLabel, decisiveAction: gm.decisiveActionLabel, warning: gm.deadlineWarning, failureMessage: gm.deadlineFailureMessage };
  }
  if (tl.exerciseDuration != null && tl.exerciseDuration !== gm.durationHours * 60 && tl.exerciseDuration !== gm.durationHours) {
    note(`${slug}: timeline.exerciseDuration=${tl.exerciseDuration} disagrees with gameMechanics.durationHours=${gm.durationHours}; the document keeps one duration (from durationHours)`);
  }
  doc.rulesOfPlay = rop;
  doc.assessment = {
    purpose: p.objectives,
    victoryConditions: p.victoryConditions,
    performanceMetrics: gm.performanceMetrics,
    objectives: (fx.objectives || []).map(o => ({
      id: o.id, parentId: o.parentId ?? undefined, name: o.name, description: o.description, type: o.type,
      priority: o.priority, successCriteria: o.successCriteria, assigned: o.assigned ? owner(o.assigned) : undefined,
    })),
  };
  const events = [];
  for (const [i, inj] of (p.injects || []).entries()) {
    events.push({ id: `inject-${i + 1}`, at: inj.trigger, owner: 'white-cell', title: inj.title });
  }
  for (const e of tl.events) {
    const ev = { id: `event-${e.number}`, owner: owner(e.assigned), description: e.description };
    if (isOffset(e.time)) ev.at = e.time; else { ev.displayTime = e.time; }
    if (e.schedule) ev.schedule = e.schedule;
    if (e.triggerCondition) ev.when = e.triggerCondition;
    if (e.objectiveIds?.length) ev.objectives = e.objectiveIds;
    ev.execution = { mode: (e.executionType || 'manual').toLowerCase(), workflowRef: e.workflowId ?? undefined };
    events.push(ev);
  }
  doc.timeline = { events };
  if (p.workflowBindings) doc.workflows = p.workflowBindings.map(w => ({ ref: w.workflowRef, displayName: w.displayName, cron: w.cron, enabled: w.enabled }));
  // Graph: entity ids become slugs; edges follow.
  const idMap = new Map(), used = new Set();
  doc.entities = (fx.graph?.nodes || []).map(n => {
    let id = slugify(n.name); while (used.has(id)) id += '-2'; used.add(id); idMap.set(n.id, id);
    const props = typeof n.properties === 'string' ? JSON.parse(n.properties || '{}') : (n.properties || {});
    const prov = {};
    if (n.origin) prov.origin = n.origin.toLowerCase();
    if (n.confidence != null) prov.confidence = n.confidence;
    if (n.isReviewed != null) prov.reviewed = n.isReviewed;
    return { id, name: n.name, type: n.entityType, description: n.description, properties: props, externalId: n.externalId || undefined, provenance: prov };
  });
  doc.edges = (fx.graph?.edges || []).map(e => {
    const prov = {};
    if (e.origin) prov.origin = e.origin.toLowerCase();
    if (e.confidence != null) prov.confidence = e.confidence;
    if (e.isReviewed != null) prov.reviewed = e.isReviewed;
    return { from: idMap.get(e.sourceEntityId), to: idMap.get(e.targetEntityId), type: e.edgeType, label: e.label, weight: e.weight ?? undefined, provenance: prov };
  });
  return doc;
}

// Shape B: the current kriegspiel bundle (situation, player, opfor, world, triggers, clock, fog).
function convertKriegspielShape(fx, slug) {
  const s = fx.scenario;
  const doc = { schemaVersion: '1.1.0', slug, name: s.name, description: s.description };
  if (fx.catalog) doc.catalog = fx.catalog;
  doc.context = { situation: s.situation };
  doc.audience = { role: s.player?.role, mandate: s.player?.mandate, rulesOfEngagement: s.player?.roe };
  if (s.opfor) {
    doc.adversaries = [{
      id: slugify(s.opfor.name), name: s.opfor.name, objective: s.opfor.objective, winThreshold: s.opfor.winThreshold,
      playbook: (s.opfor.playbook || []).map(m => ({
        id: m.id, domain: m.domain, description: m.description, preconditions: m.preconds,
        effects: { setFlags: m.setFlags, setFacts: m.setFacts }, progress: m.progress, indicators: m.indicators,
      })),
    }];
  }
  const w = s.world || {};
  doc.terrain = {
    hosts: (w.assets || []).map(a => { const m = /^(.*?)\s*\((.*)\)$/.exec(a); return m ? { name: m[1], description: m[2] } : { name: a }; }),
    informationEnvironment: w.narrativeEnv,
  };
  doc.startingConditions = { flags: w.flags, facts: w.facts };
  doc.rulesOfPlay = {
    duration: s.clock ? faithfulDuration(s.clock.windowMinutes, slug, `clock.windowMinutes=${s.clock.windowMinutes}`) : undefined,
    clock: s.clock && { tickMinutes: s.clock.tickMinutes, label: s.clock.label },
    fog: s.fog?.default,
  };
  doc.assessment = {
    objectives: (s.objectives || []).map(o => ({ id: o.id, name: o.name, description: o.description, priority: o.priority, successCriteria: o.successCriteria, metWhen: o.metWhen })),
  };
  doc.timeline = {
    events: (s.triggers || []).map(t => ({
      id: t.id, when: t.when, owner: 'white-cell', description: t.inject,
      effects: { setFlags: t.setFlags, setFacts: t.setFacts }, indicators: t.indicators,
    })),
  };
  return doc;
}

// ───────────────────────── coverage ─────────────────────────
// Every leaf value in the fixture must appear somewhere in the document, except the
// fields the document drops on purpose. Loose (string containment), but it catches a
// dropped paragraph or a mis-mapped number, which is what a conversion gets wrong.

const DROPPED_KEYS = new Set([
  '_comment', 'id', 'createdAt', 'updatedAt', 'status', 'score', 'scenarioId', 'parentId', 'sortOrder',
  'builderStatus', 'triggerKind', 'schedule', 'workflowId', 'children', 'sourceId', 'npcId', 'isReviewed',
  'confidence', 'weight', 'origin', 'sourceEntityId', 'targetEntityId', 'exerciseDuration', 'durationHours',
  'containmentDeadlineMinutes', 'windowMinutes', 'number', 'executionType', 'listed',
]);

function leaves(v, key, out) {
  if (DROPPED_KEYS.has(key)) return;
  if (Array.isArray(v)) { v.forEach(x => leaves(x, key, out)); return; }
  if (v && typeof v === 'object') { for (const [k, x] of Object.entries(v)) leaves(x, k, out); return; }
  if (typeof v === 'boolean' || v === null || v === '') return;
  if (key === 'properties' && typeof v === 'string') { try { leaves(JSON.parse(v), key, out); return; } catch { /* keep as string */ } }
  // Two deliberate normalizations: cell names become the owner enum, and an RPG asset
  // "NAME (description)" is split into a host name and a description.
  if (key === 'assigned') { out.push({ key, value: owner(v) }); return; }
  if (key === 'assets') { const m = /^(.*?)\s*\((.*)\)$/.exec(v); if (m) { out.push({ key, value: m[1] }, { key, value: m[2] }); return; } }
  out.push({ key, value: String(v) });
}

// Canonical form omits a value equal to its schema default, so coverage reads the document
// the way any reader must: with the declared defaults filled in on the objects that are present.
function withDefaults(value, node) {
  node = resolve(node);
  if (Array.isArray(value)) return value.map(v => withDefaults(v, node?.items));
  if (!value || typeof value !== 'object') return value;
  const props = node?.properties || {};
  const out = {};
  for (const [k, v] of Object.entries(value)) out[k] = withDefaults(v, props[k] || node?.additionalProperties);
  for (const [k, p] of Object.entries(props)) {
    const child = resolve(p);
    if (!(k in out) && child && 'default' in child) out[k] = child.default;
  }
  return out;
}

function coverage(fixtureFile, docFile) {
  const fx = JSON.parse(readFileSync(fixtureFile, 'utf8'));
  const text = readFileSync(docFile, 'utf8');
  const hay = JSON.stringify(withDefaults(JSON.parse(text), schema)); // escaped the same way as the values will be
  const want = []; leaves(fx, null, want);
  const missing = want.filter(({ value }) => !hay.includes(JSON.stringify(value).slice(1, -1)) && !hay.includes(value));
  for (const m of missing) console.log(`MISSING ${m.key}: ${m.value.slice(0, 80)}`);
  console.log(`${missing.length ? 'FAIL' : 'ok'}    coverage ${basename(fixtureFile)} → ${basename(docFile)}: ${want.length - missing.length}/${want.length} source values present`);
  return missing.length;
}

// ─────────────────────────── main ───────────────────────────

// Path-level differences between two documents: what the first has and the second does not,
// what the second added, and where both hold a value but not the same one. The loss report of
// a round trip, where a byte diff of two 300-line documents says little.

const flatten = (v, path, out) => {
  if (Array.isArray(v)) { v.forEach((x, i) => flatten(x, `${path}[${i}]`, out)); return out; }
  if (v && typeof v === 'object') { for (const [k, x] of Object.entries(v)) flatten(x, path ? `${path}.${k}` : k, out); return out; }
  out.set(path, v);
  return out;
};

const short = v => { const s = String(v); return s.length > 72 ? s.slice(0, 69) + '...' : s; };

function docDiff(aFile, bFile) {
  const a = flatten(JSON.parse(readFileSync(aFile, 'utf8')), '', new Map());
  const b = flatten(JSON.parse(readFileSync(bFile, 'utf8')), '', new Map());
  const lines = [];
  for (const [k, v] of a) if (!b.has(k)) lines.push(`- ${k} = ${short(v)}`);
  for (const [k, v] of b) if (!a.has(k)) lines.push(`+ ${k} = ${short(v)}`);
  for (const [k, v] of a) if (b.has(k) && String(v) !== String(b.get(k))) lines.push(`~ ${k}: ${short(v)} -> ${short(b.get(k))}`);
  for (const l of lines) console.log(l);
  return lines.length;
}

// ─────────────────────────── render ───────────────────────────
// The document as an exercise plan a reviewer works through section by section. This is the review
// only a person can perform: whether the consequences of the intent are a faithful reading of the
// intent, which is the call left once tiers 1 to 4 have run. The register to aim for is the design's
// §4a — a beat is "fires on the clock; sets foothold-billing; observable in the mail gateway logs;
// expected blue response: a user report and a gateway block", not a field dump.
//
// It is a view and never a source. It is regenerated from the document, so it cannot drift from what
// the loader will read, and nothing here is authored by hand.

const cell = v => String(v ?? '').replace(/\|/g, '\\|').replace(/\n+/g, ' ').trim() || '—';
const list = v => (Array.isArray(v) ? v : v == null ? [] : [v]);
const commas = v => list(v).map(String).join(', ');
const em = v => (v ? `*${v}*` : '');

// A table, or nothing at all when there are no rows: an empty table is a question a reviewer has to
// stop and answer ("is this blank because there is nothing, or because something failed?").
function table(out, headers, rows) {
  if (!rows.length) return;
  out.push(`| ${headers.join(' | ')} |`, `|${headers.map(() => '---|').join('')}`);
  for (const r of rows) out.push(`| ${r.map(cell).join(' | ')} |`);
  out.push('');
}

function heading(out, level, text) { out.push(`${'#'.repeat(level)} ${text}`, ''); }
function para(out, text) { if (text) out.push(String(text), ''); }

// When an event happens, in the words the document used. A condition is not a time and must not be
// printed as one — "when the foothold is held" is a different claim from "at 09:40".
function timing(e) {
  const parts = [];
  if (e.displayTime) parts.push(e.displayTime + (e.at ? ` (${e.at})` : ''));
  else if (e.at) parts.push(e.at);
  if (e.schedule) parts.push(`on schedule \`${e.schedule}\``);
  if (e.when) parts.push(`when \`${e.when}\``);
  return parts.join(', ') || 'unscheduled';
}

function renderTimeline(out, doc) {
  const events = list(doc.timeline?.events);
  if (!events.length) return;
  heading(out, 2, 'The timeline');
  para(out, `${events.length} beat${events.length === 1 ? '' : 's'}. Every one names who owns it, what the ` +
    'audience is expected to do, and what they would have to see in order to do it.');

  table(out, ['#', 'When', 'Owner', 'Beat', 'Expected response', 'Indicators'],
    events.map((e, i) => [i + 1, timing(e), e.owner || 'unassigned',
      e.title || e.description || e.id, e.expectedResponse, commas(e.indicators)]));

  // Then each beat in prose, because the fields a table cannot hold are the ones that decide whether
  // the beat is a faithful reading of the intent: what it sets, what it is graded against, and how it
  // is executed.
  heading(out, 3, 'Beat by beat');
  const sentence = s => String(s).trim().replace(/\.$/, '');
  events.forEach((e, i) => {
    // The mechanics read as one semicolon-joined clause, which is §4a's register: "Fires on the clock;
    // sets foothold-billing; observable in the mail gateway logs and one workstation process tree."
    const how = [];
    if (e.when) how.push(`fires when \`${e.when}\` holds`);
    else if (e.schedule) how.push(`fires on the schedule \`${e.schedule}\``);
    else how.push('fires on the clock');
    if (e.effects?.setFlags?.length) how.push(`sets ${list(e.effects.setFlags).map(em).join(', ')}`);
    if (e.effects?.setFacts) how.push(`records ${Object.entries(e.effects.setFacts).map(([k, v]) => `${k} = ${v}`).join(', ')}`);
    if (e.indicators?.length) how.push(`observable in ${commas(e.indicators)}`);
    if (e.objectives?.length) how.push(`graded against objective ${commas(e.objectives)}`);
    if (e.execution?.mode === 'workflow') how.push(`run by workflow \`${e.execution.workflowRef}\``);

    const parts = [`**${i + 1}. ${e.title || e.id} — ${timing(e)}**, ${e.owner || 'unassigned'}.`];
    if (e.description) parts.push(`${sentence(e.description)}.`);
    const mechanics = how.map(sentence).join('; ');
    parts.push(`${mechanics[0].toUpperCase()}${mechanics.slice(1)}.`);
    if (e.expectedResponse) parts.push(`**Expected response:** ${sentence(e.expectedResponse)}.`);
    out.push(parts.join(' '), '');
  });
}

function renderAdversaries(out, doc) {
  const adversaries = list(doc.adversaries);
  if (!adversaries.length) return;
  heading(out, 2, 'The adversary');
  for (const a of adversaries) {
    heading(out, 3, `${a.name || a.id} — ${a.type || 'unspecified'}, capability ${a.capability ?? '?'}/5`);
    para(out, a.objective && `**What it is trying to do.** ${a.objective}`);
    para(out, a.winThreshold != null && `**It wins at** progress ${a.winThreshold}.`);
    if (a.techniques?.length) para(out, `**Techniques.** ${commas(a.techniques)} (ATT&CK ids; the validator resolves each one).`);
    if (a.capabilities?.length) para(out, `**Capabilities in plain words.** ${commas(a.capabilities)}`);
    table(out, ['Move', 'Domain', 'Techniques', 'Available when', 'Sets', 'Progress', 'Indicators'],
      list(a.playbook).map(m => [m.description || m.id, m.domain, commas(m.techniques),
        m.preconditions ? `\`${m.preconditions}\`` : 'always', commas(m.effects?.setFlags), m.progress,
        commas(m.indicators)]));
  }
}

function renderTerrain(out, doc) {
  const t = doc.terrain;
  if (!t) return;
  heading(out, 2, 'The terrain');
  if (t.reference) para(out, `**Base.** The \`${t.reference.slice}\` slice from ${t.reference.provider}. ` +
    'Everything below is what this exercise adds to it or relies on from it.');
  if (t.summary?.topology) para(out, `**Topology.** ${t.summary.topology}`);
  if (t.summary?.assets) para(out, `**Assets.** ${t.summary.assets}`);
  if (t.summary?.services) para(out, `**Services.** ${t.summary.services}`);
  table(out, ['Segment', 'CIDR', 'What it is'], list(t.segments).map(s => [s.name, s.cidr, s.description]));
  table(out, ['Host', 'Segment', 'OS', 'Role', 'What it is'],
    list(t.hosts).map(h => [h.name, h.segment, h.os, h.role, h.description]));
  table(out, ['Service', 'On', 'What it is'], list(t.services).map(s => [s.name, commas(s.hosts), s.description]));
  table(out, ['Defense', 'Covers', 'What it does'],
    list(t.defenses).map(d => [d.name, commas(d.covers), d.description]));
  table(out, ['Weakness', 'On', 'Severity', 'What it is'],
    list(t.vulnerabilities).map(v => [v.cve || v.description, v.asset, v.severity, v.cve ? v.description : '']));
  if (t.informationEnvironment) {
    para(out, `**Information environment.** ${commas(t.informationEnvironment.platforms)}` +
      (t.informationEnvironment.audience ? ` — ${t.informationEnvironment.audience}` : ''));
  }
  const pools = list(doc.population?.pools);
  if (pools.length) {
    heading(out, 3, 'Who GHOSTS simulates');
    para(out, `${pools.reduce((n, p) => n + (p.count || 0), 0)} simulated people. Everyone else in the ` +
      'exercise is a person playing.');
    table(out, ['Pool', 'Count', 'Who they are'], pools.map(p => [p.role, p.count, p.description]));
  }
}

function render(docFile, ledgerFile) {
  const doc = JSON.parse(readFileSync(docFile, 'utf8'));
  const out = [];

  heading(out, 1, `${doc.name || doc.slug} — exercise plan`);
  para(out, `*Rendered from \`${basename(docFile)}\` (schema ${doc.schemaVersion}). A view, not a source: ` +
    'regenerate it after every edit. What gets signed is the plan; what gets loaded is the document.*');
  para(out, doc.description);

  if (doc.intent) { heading(out, 2, 'Intent'); para(out, doc.intent); }
  if (doc.context?.situation) { heading(out, 2, 'The situation the audience is given'); para(out, doc.context.situation); }
  if (doc.context?.political) { heading(out, 2, 'Background (White Cell)'); para(out, doc.context.political); }

  const a = doc.audience;
  if (a || doc.sides) {
    heading(out, 2, 'The audience');
    para(out, a?.role && `**Who.** ${a.role}${a.size ? `, ${a.size} of them` : ''}.`);
    para(out, a?.proficiency && `**Where they are starting from.** ${a.proficiency}`);
    para(out, a?.mandate && `**What they are responsible for.** ${a.mandate}`);
    para(out, a?.rulesOfEngagement && `**What they may and may not do.** ${a.rulesOfEngagement}`);
    table(out, ['Side', 'Alignment'], list(doc.sides).map(s => [s.name, s.alignment]));
  }

  const r = doc.rulesOfPlay;
  if (r) {
    heading(out, 2, 'How it is played');
    const rows = [
      ['Runs for', r.duration], ['Pacing', r.pacing], ['Adjudication', r.adjudication],
      ['Clock tick', r.clock?.tickMinutes && `${r.clock.tickMinutes} minutes${r.clock.label ? ` (${r.clock.label})` : ''}`],
      ['Deadline', r.deadline?.at && `${r.deadline.at}${r.deadline.label ? ` — ${r.deadline.label}` : ''}`],
      ['Fog of war', r.fog],
      ['Telemetry', Object.entries(r.telemetry || {}).filter(([, v]) => v).map(([k]) => k).join(', ')],
    ].filter(([, v]) => v);
    table(out, ['Rule of play', ''], rows);
    if (r.escalationLadder) {
      heading(out, 3, 'How far it can go');
      para(out, r.escalationLadder.summary);
      table(out, ['Rung', 'What has happened', 'Still recoverable'],
        list(r.escalationLadder.rungs).map(g => [g.name, g.description, g.recoverable ? 'yes' : '**no**']));
    }
    if (r.branching?.summary) { heading(out, 3, 'If play diverges'); para(out, r.branching.summary); }
  }

  const flags = list(doc.startingConditions?.flags);
  const facts = Object.entries(doc.startingConditions?.facts || {});
  if (flags.length || facts.length) {
    heading(out, 2, 'What is true when it starts');
    if (flags.length) para(out, flags.map(em).join(', '));
    table(out, ['Fact', 'Value'], facts.map(([k, v]) => [k, v]));
  }

  renderAdversaries(out, doc);
  renderTerrain(out, doc);
  renderTimeline(out, doc);

  const s = doc.assessment;
  if (s) {
    heading(out, 2, 'What counts as success');
    para(out, s.purpose && `**Why this exercise exists.** ${s.purpose}`);
    para(out, s.victoryConditions && `**Victory conditions.** ${s.victoryConditions}`);
    para(out, s.performanceMetrics && `**How it is measured.** ${s.performanceMetrics}`);
    table(out, ['Id', 'Objective', 'Type', 'Priority', 'Met when', 'Assigned'],
      list(s.objectives).map(o => [o.id, o.name + (o.parentId ? ` (under ${o.parentId})` : ''), o.type,
        o.priority, o.metWhen ? `\`${o.metWhen}\`` : o.successCriteria, o.assigned]));
  }

  table(out, ['Automation', 'Bound to'], list(doc.workflows).map(w => [w.name || w.id, w.ref]));

  const refs = list(doc.references);
  if (refs.length) {
    heading(out, 2, 'References');
    para(out, 'Every `[ref:id]` in the prose above points at one of these.');
    table(out, ['Id', 'Title', 'Where'], refs.map(x => [x.id, x.title, x.url || x.citation]));
  }

  // The ledger is the other half of the review: which mission judgments a person made and which a
  // model proposed. It is a separate file because it is not part of the document, and it is included
  // verbatim because paraphrasing a record of who decided what would defeat its purpose.
  if (ledgerFile) {
    heading(out, 2, 'Decisions ledger');
    out.push(readFileSync(ledgerFile, 'utf8').replace(/^#\s+/m, '### ').trimEnd(), '');
  }

  return out.join('\n').replace(/\n{3,}/g, '\n\n') + '\n';
}

// ─────────────────────────── main ───────────────────────────

const [cmd, ...args] = process.argv.slice(2);
let rc = 0;
switch (cmd) {
  case 'validate':
    rc = validate(args) ? 1 : 0; break;
  case 'canonicalize':
    for (const f of args) { const doc = JSON.parse(readFileSync(f, 'utf8')); writeFileSync(f, serialize(doc)); console.log(`wrote ${f}`); }
    break;
  case 'convert': {
    const [inFile, outFile] = args;
    const fx = JSON.parse(readFileSync(inFile, 'utf8'));
    const slug = slugify(basename(inFile).replace(/\.json$/, ''));
    const doc = convert(fx, slug);
    writeFileSync(outFile, serialize(doc));
    for (const n of notes) console.log(`note  ${n}`);
    console.log(`wrote ${outFile}`);
    break;
  }
  case 'crosscheck':
    rc = crosscheck(args); break;
  case 'coverage':
    rc = coverage(args[0], args[1]) ? 1 : 0; break;
  case 'diff':
    rc = docDiff(args[0], args[1]) ? 1 : 0; break;
  case 'render':
    process.stdout.write(render(args[0], args[1])); break;
  default:
    console.error('usage: scenario-doc.mjs validate|crosscheck|canonicalize|convert|coverage|diff|render ...\n' +
      '       render <document.scenario.json> [decisions.md]   exercise plan to stdout'); rc = 2;
}
process.exit(rc);
