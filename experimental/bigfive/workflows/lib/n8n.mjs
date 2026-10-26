// Helpers for describing n8n workflows in JavaScript. build.mjs turns each defs/*.mjs into JSON.
// Code-node scripts come from src/, with `// @include lib/x.js` lines spliced in and __PLACEHOLDERS__ replaced.
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
export const SRC = join(here, '..', 'src');

export function loadScript(file, replacements = {}) {
  let text = readFileSync(join(SRC, file), 'utf8');
  text = text.replace(/^\/\/ @include (.+)$/gm, (_, inc) => `// ---- ${inc.trim()} ----\n${readFileSync(join(SRC, inc.trim()), 'utf8').trim()}\n// ---- end ${inc.trim()} ----`);
  for (const [k, v] of Object.entries(replacements)) text = text.split(k).join(v);
  return text;
}

export class Workflow {
  constructor(name, id) {
    this.name = name;
    this.id = id;
    this.nodes = [];
    this.connections = {};
    this.picks = []; // [nodeName, tableName] for the sticky note
  }

  add(node) {
    if (this.nodes.some((n) => n.name === node.name)) throw new Error(`${this.name}: duplicate node name "${node.name}"`);
    this.nodes.push(node);
    return node;
  }

  connect(from, to, fromIndex = 0, type = 'main') {
    const f = typeof from === 'string' ? from : from.name;
    const t = typeof to === 'string' ? to : to.name;
    const c = (this.connections[f] = this.connections[f] || {});
    const list = (c[type] = c[type] || []);
    while (list.length <= fromIndex) list.push([]);
    list[fromIndex].push({ node: t, type, index: 0 });
  }

  chain(...nodes) {
    for (let i = 1; i < nodes.length; i++) this.connect(nodes[i - 1], nodes[i]);
    return nodes[nodes.length - 1];
  }

  toJSON() {
    for (const n of this.nodes) {
      if (!n.id) n.id = n.name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
    }
    const names = new Set(this.nodes.map((n) => n.name));
    for (const [from, c] of Object.entries(this.connections)) {
      if (!names.has(from)) throw new Error(`${this.name}: connection from unknown node "${from}"`);
      for (const outs of Object.values(c)) for (const list of outs) for (const l of list) if (!names.has(l.node)) throw new Error(`${this.name}: connection to unknown node "${l.node}"`);
    }
    return {
      name: this.name,
      id: this.id,
      nodes: this.nodes,
      connections: this.connections,
      settings: { executionOrder: 'v1', saveExecutionProgress: true, saveManualExecutions: true },
      active: false,
      pinData: {},
      meta: { templateCredsSetupCompleted: false },
    };
  }
}

export const node = (type, typeVersion, name, parameters, position, extra = {}) => ({ parameters, type, typeVersion, name, position, ...extra });

export const sticky = (name, content, position, width = 460, height = 300, color = 6) =>
  node('n8n-nodes-base.stickyNote', 1, name, { content, width, height, color }, position);

export const manualTrigger = (name, position) => node('n8n-nodes-base.manualTrigger', 1, name, {}, position);

export const schedule = (name, cron, position) =>
  node('n8n-nodes-base.scheduleTrigger', 1.2, name, { rule: { interval: [{ field: 'cronExpression', expression: cron }] } }, position);

// fields: [{ label, type: 'text'|'number'|'password'|'dropdown'|'file', required, placeholder, options:[..], accept }]
export const formTrigger = (name, path, title, description, fields, position) =>
  node('n8n-nodes-base.formTrigger', 2.2, name, {
    path,
    formTitle: title,
    formDescription: description,
    formFields: {
      values: fields.map((f) => {
        const v = { fieldLabel: f.label, fieldType: f.type || 'text' };
        if (f.required) v.requiredField = true;
        if (f.placeholder) v.placeholder = f.placeholder;
        if (f.type === 'dropdown') v.fieldOptions = { values: f.options.map((o) => ({ option: o })) };
        if (f.type === 'file') { v.multipleFiles = false; if (f.accept) v.acceptFileTypes = f.accept; }
        return v;
      }),
    },
    options: {},
  }, position, { webhookId: path });

export const code = (name, file, position, replacements = {}, extra = {}) =>
  node('n8n-nodes-base.code', 2, name, { jsCode: loadScript(file, replacements) }, position, extra);

export const ifNode = (name, leftValue, position) =>
  node('n8n-nodes-base.if', 2.2, name, {
    conditions: {
      options: { caseSensitive: true, leftValue: '', typeValidation: 'loose', version: 2 },
      conditions: [{ id: `${name}-cond`, leftValue, rightValue: '', operator: { type: 'boolean', operation: 'true', singleValue: true } }],
      combinator: 'and',
    },
    options: {},
  }, position);

export const loop = (name, batchSize, position) => node('n8n-nodes-base.splitInBatches', 3, name, { batchSize, options: {} }, position);

export const extractXlsx = (name, binaryProperty, sheetName, position) =>
  node('n8n-nodes-base.extractFromFile', 1, name, { operation: 'xlsx', binaryPropertyName: binaryProperty, options: { headerRow: true, ...(sheetName ? { sheetName } : {}) } }, position);

export const toTextFile = (name, position) =>
  node('n8n-nodes-base.convertToFile', 1.1, name, { operation: 'toText', sourceProperty: 'content', binaryPropertyName: 'data', options: { fileName: '={{ $json.fileName }}' } }, position);

export const writeFile = (name, fileNameExpr, position) =>
  node('n8n-nodes-base.readWriteFile', 1, name, { operation: 'write', fileName: fileNameExpr, dataPropertyName: 'data', options: {} }, position);

// --- Baserow ------------------------------------------------------------------------------------
// `pick` nodes have an empty database/table for the operator to choose after import. `ref` nodes point
// at the pick node for the same table by expression, so each table is chosen once per workflow.
const tableRef = (refNode) => (refNode
  ? { databaseId: `={{ $('${refNode}').params.databaseId }}`, tableId: `={{ $('${refNode}').params.tableId }}` }
  : { databaseId: '', tableId: '' });

const baserow = (name, params, position, extra = {}) =>
  node('n8n-nodes-base.baserow', 1.1, name, { authentication: 'databaseToken', resource: 'row', ...params }, position, extra);

export function readTable(wf, table, position, { name = `Read: ${table}`, ref = null } = {}) {
  if (!ref) wf.picks.push([name, table]);
  return wf.add(baserow(name, { operation: 'getAll', ...tableRef(ref), returnAll: true, additionalOptions: {} }, position, { executeOnce: true, alwaysOutputData: true }));
}

export const createRow = (wf, table, position, { name = `Create: ${table}`, ref, ignore = '' } = {}) =>
  wf.add(baserow(name, { operation: 'create', ...tableRef(ref), dataToSend: 'autoMapInputData', inputsToIgnore: ignore }, position));

export const batchCreate = (wf, table, position, { name = `Batch Create: ${table}`, ref, ignore = '' } = {}) => {
  if (!ref) wf.picks.push([name, table]);
  return wf.add(baserow(name, { operation: 'batchCreate', ...tableRef(ref), dataToSend: 'autoMapInputData', inputsToIgnore: ignore }, position));
};

export const updateRow = (wf, table, position, { name = `Update: ${table}`, ref, rowId = '={{ $json.id }}', ignore = 'id' } = {}) =>
  wf.add(baserow(name, { operation: 'update', ...tableRef(ref), rowId, dataToSend: 'autoMapInputData', inputsToIgnore: ignore }, position));

// Upsert block for one table: Read -> New rows -> Loop(200) -> Batch Create; Read -> Changed rows -> Update.
// `source` is the node whose first item holds the staged rows under `table`. The split nodes hang off
// `from` (default: the Read node; pass the source node when the Read happened before the source ran).
// Returns the Read node.
export function upsert(wf, table, source, [x, y], { read = null, from = null } = {}) {
  const readNode = read || readTable(wf, table, [x, y]);
  const origin = from || readNode;
  const fresh = wf.add(code(`New rows: ${table}`, 'upsert_split.js', [x + 220, y - 60], { __SOURCE__: source, __TABLE__: table, __MODE__: 'new' }));
  const lp = wf.add(loop(`200 at a time: ${table}`, 200, [x + 440, y - 60]));
  const bc = batchCreate(wf, table, [x + 660, y - 40], { ref: readNode.name });
  const changed = wf.add(code(`Changed rows: ${table}`, 'upsert_split.js', [x + 220, y + 100], { __SOURCE__: source, __TABLE__: table, __MODE__: 'changed' }));
  const up = updateRow(wf, table, [x + 440, y + 100], { ref: readNode.name });
  wf.connect(origin, fresh);
  wf.connect(fresh, lp);
  wf.connect(lp, bc, 1);
  wf.connect(bc, lp);
  wf.connect(origin, changed);
  wf.connect(changed, up);
  return readNode;
}

export function tableNote(wf, position, intro = '') {
  const lines = wf.picks.map(([n, t]) => `- **${n}** → table \`${t}\``);
  const refs = wf.nodes.filter((n) => n.type === 'n8n-nodes-base.baserow' && String(n.parameters.tableId).startsWith('={{')).map((n) => n.name);
  const content = `## Baserow tables to pick\n${intro ? intro + '\n\n' : ''}After importing, open each node below and choose the Big Five database and the named table from the dropdowns (and the Baserow Token credential):\n\n${lines.join('\n')}\n\n${refs.length ? `These nodes follow the pick above for the same table by expression; nothing to choose, only the credential:\n${refs.map((r) => `- ${r}`).join('\n')}` : ''}`;
  return wf.add(sticky('Tables', content, position, 520, 120 + 24 * (lines.length + refs.length), 4));
}
