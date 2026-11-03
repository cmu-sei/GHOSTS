// Keeps an installed n8n's GHOSTS workflows current with the files shipped in /bootstrap/workflows.
//
// The provisioner imports workflows once, on first boot. Without this, a fixed or changed workflow file never
// reaches an n8n that already exists. On every boot, for each shipped file with an id:
//   - if the file's content was already synced (its hash is in the state file), nothing happens;
//   - if n8n's copy differs from the file, n8n's copy is replaced through the public API (PUT /workflows/{id});
//   - either way the file's hash is recorded, so a user's later edits in n8n are kept until the file changes again.
// A workflow n8n does not have yet is left to the import step.
//
// Usage (inside the n8n container): node sync-workflows.mjs
//   WORKFLOW_DIR (default /bootstrap/workflows), N8N_HOST (default n8n),
//   N8N_API_KEY or /home/node/.n8n/.api_key, SYNC_STATE (default /home/node/.n8n/.bootstrap_workflow_hashes.json)
import { createHash } from 'node:crypto';
import { readdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { join, basename } from 'node:path';
import { pathToFileURL } from 'node:url';

export function fileHash(text) {
  return createHash('sha256').update(text).digest('hex');
}

/** What the file decides: each node's type and parameters by name, and the connections. n8n adds ids and positions. */
function shape(wf) {
  const nodes = Object.fromEntries((wf.nodes || [])
    .map(n => [n.name, { type: n.type, parameters: n.parameters ?? {} }])
    .sort(([a], [b]) => a.localeCompare(b)));
  return JSON.stringify({ nodes, connections: wf.connections ?? {} });
}

export async function syncWorkflows({ dir, statePath, apiUrl, apiKey, fetch = globalThis.fetch, log = console.log }) {
  const state = existsSync(statePath) ? JSON.parse(readFileSync(statePath, 'utf8')) : {};
  const result = { updated: [], unchanged: [], missing: [], failed: [] };
  const headers = { 'X-N8N-API-KEY': apiKey, accept: 'application/json', 'Content-Type': 'application/json' };

  for (const file of readdirSync(dir).filter(f => f.endsWith('.json')).sort()) {
    const text = readFileSync(join(dir, file), 'utf8');
    const wf = JSON.parse(text);
    const name = wf.name || basename(file, '.json');
    const hash = fileHash(text);
    if (!wf.id || state[file] === hash) continue;

    const get = await fetch(`${apiUrl}/workflows/${wf.id}`, { headers });
    if (get.status === 404) { result.missing.push(name); continue; }
    if (!get.ok) { result.failed.push(name); log(`  sync: could not read ${name} (${get.status})`); continue; }

    const live = await get.json();
    if (shape(live) === shape(wf)) {
      result.unchanged.push(name);
      state[file] = hash;
      continue;
    }

    const body = JSON.stringify({
      name: wf.name,
      nodes: wf.nodes,
      connections: wf.connections,
      settings: { executionOrder: wf.settings?.executionOrder ?? 'v1' }
    });
    const put = await fetch(`${apiUrl}/workflows/${wf.id}`, { method: 'PUT', headers, body });
    if (put.ok) {
      result.updated.push(name);
      state[file] = hash;
      log(`  sync: updated ${name} from its shipped file`);
    } else {
      result.failed.push(name);
      log(`  sync: FAILED to update ${name} (${put.status})`);
    }
  }

  writeFileSync(statePath, JSON.stringify(state, null, 2));
  return result;
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const keyFile = '/home/node/.n8n/.api_key';
  const apiKey = process.env.N8N_API_KEY || (existsSync(keyFile) ? readFileSync(keyFile, 'utf8').trim() : '');
  if (!apiKey) {
    console.log('[sync] No n8n API key yet; skipping workflow sync.');
    process.exit(0);
  }
  const result = await syncWorkflows({
    dir: process.env.WORKFLOW_DIR || '/bootstrap/workflows',
    statePath: process.env.SYNC_STATE || '/home/node/.n8n/.bootstrap_workflow_hashes.json',
    apiUrl: `http://${process.env.N8N_HOST || 'n8n'}:5678/api/v1`,
    apiKey
  });
  console.log(`[sync] ${result.updated.length} updated, ${result.unchanged.length} already current, ${result.missing.length} not imported yet, ${result.failed.length} failed.`);
}
