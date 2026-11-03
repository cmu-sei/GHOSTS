// Run: node --test configuration/n8n-bootstrap/scripts/
// The provisioner imports workflows once; sync keeps an installed n8n current with the shipped files on every boot.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { syncWorkflows, fileHash } from './sync-workflows.mjs';

const shipped = {
  id: 'wf-belief', name: 'GHOSTS Belief',
  nodes: [{ name: 'Get NPC List', type: 'n8n-nodes-base.httpRequest', parameters: { url: 'http://api/npcs/list?executionId=1' } }],
  connections: { Webhook: { main: [[{ node: 'Get NPC List', type: 'main', index: 0 }]] } },
  settings: { executionOrder: 'v1', availableInMCP: false }
};

function setup(remote, state = {}) {
  const dir = mkdtempSync(join(tmpdir(), 'wf-'));
  const text = JSON.stringify(shipped, null, 2);
  writeFileSync(join(dir, 'GHOSTS Belief.json'), text);
  const statePath = join(dir, 'state.json');
  if (Object.keys(state).length) writeFileSync(statePath, JSON.stringify(state));
  const calls = [];
  const fetch = async (url, init = {}) => {
    calls.push({ url, method: init.method || 'GET', body: init.body && JSON.parse(init.body), key: init.headers?.['X-N8N-API-KEY'] });
    if ((init.method || 'GET') === 'GET') {
      return remote ? { ok: true, status: 200, json: async () => remote } : { ok: false, status: 404, json: async () => ({}) };
    }
    return { ok: true, status: 200, json: async () => ({}) };
  };
  return { dir, statePath, calls, fetch, text };
}

const run = (s) => syncWorkflows({ dir: s.dir, statePath: s.statePath, apiUrl: 'http://n8n:5678/api/v1', apiKey: 'k', fetch: s.fetch, log: () => {} });

test('a workflow that differs from its shipped file is replaced with it and the file is recorded', async () => {
  const stale = { ...shipped, nodes: [{ ...shipped.nodes[0], parameters: { url: 'http://api/npcs/list' } }] };
  const s = setup(stale);

  const result = await run(s);

  assert.deepEqual(result.updated, ['GHOSTS Belief']);
  const put = s.calls.find(c => c.method === 'PUT');
  assert.equal(put.url, 'http://n8n:5678/api/v1/workflows/wf-belief');
  assert.equal(put.key, 'k');
  assert.deepEqual(put.body.nodes, shipped.nodes);
  assert.deepEqual(put.body.connections, shipped.connections);
  assert.deepEqual(put.body.settings, { executionOrder: 'v1' });
  assert.equal(JSON.parse(readFileSync(s.statePath, 'utf8'))['GHOSTS Belief.json'], fileHash(s.text));
});

test('a file already synced at this content is not fetched or replaced', async () => {
  const text = JSON.stringify(shipped, null, 2);
  const s = setup({ ...shipped }, { 'GHOSTS Belief.json': fileHash(text) });

  const result = await run(s);

  assert.deepEqual(result.updated, []);
  assert.equal(s.calls.length, 0);
});

test('a workflow that already matches its file is recorded without a replace', async () => {
  const live = { ...shipped, nodes: shipped.nodes.map(n => ({ ...n, id: 'n8n-adds-ids', position: [0, 0], typeVersion: 4 })) };
  const s = setup(live);

  const result = await run(s);

  assert.deepEqual(result.updated, []);
  assert.equal(s.calls.filter(c => c.method === 'PUT').length, 0);
  assert.equal(JSON.parse(readFileSync(s.statePath, 'utf8'))['GHOSTS Belief.json'], fileHash(s.text));
});

test('a workflow n8n does not have is left for the import and not recorded', async () => {
  const s = setup(null);

  const result = await run(s);

  assert.deepEqual(result.missing, ['GHOSTS Belief']);
  assert.equal(s.calls.filter(c => c.method === 'PUT').length, 0);
  assert.ok(!existsSync(s.statePath) || !JSON.parse(readFileSync(s.statePath, 'utf8'))['GHOSTS Belief.json']);
});
