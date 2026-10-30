// Makes sure the agent's browser bundle is built before the UI builds, serves or tests
// (package.json's pre* scripts). It is ../agent's dist/web/agent.js, which tsconfig maps as
// @scenario-builder/agent-web, as jeffort-ui's scripts/agent-web.mjs does.
//
// With the agent's packages installed it rebuilds the bundle, so it always matches the agent's
// source. Without them it only checks the bundle is there.

import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ui = join(dirname(fileURLToPath(import.meta.url)), '..');
const agent = join(ui, '..', 'agent');
const bundle = join(agent, 'dist', 'web', 'agent.js');

if (existsSync(join(agent, 'node_modules', 'esbuild'))) {
  execFileSync(process.execPath, ['build.mjs'], { cwd: agent, stdio: 'inherit' });
} else if (!existsSync(bundle) || !existsSync(join(agent, 'dist', 'web', 'agent.d.ts'))) {
  console.error(`The agent panel needs the agent's browser bundle, ${bundle}, and the agent's packages aren't installed to build it:
  cd ${agent} && npm ci && npm run build:web`);
  process.exit(1);
}
