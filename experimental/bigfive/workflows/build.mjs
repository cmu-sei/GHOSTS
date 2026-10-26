#!/usr/bin/env node
// Build the n8n workflow JSON files from defs/*.mjs and src/*.js.
//   node workflows/build.mjs            write workflows/Big Five N - *.json
//   node workflows/build.mjs --check    exit 1 if any JSON differs from what would be written
import { readdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const check = process.argv.includes('--check');
let stale = 0;
for (const file of readdirSync(join(here, 'defs')).filter((f) => f.endsWith('.mjs')).sort()) {
  const { default: build } = await import(pathToFileURL(join(here, 'defs', file)).href);
  const wf = build();
  const out = join(here, `${wf.name}.json`);
  const text = JSON.stringify(wf.toJSON(), null, 2) + '\n';
  if (check) {
    const same = existsSync(out) && readFileSync(out, 'utf8') === text;
    console.log(`${same ? 'ok     ' : 'STALE  '} ${out}`);
    if (!same) stale++;
  } else {
    writeFileSync(out, text);
    console.log(`wrote  ${out}  (${wf.nodes.length} nodes)`);
  }
}
if (stale) process.exit(1);
