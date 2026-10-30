// The evaluation: each model in turn, a run at a time (never in parallel on one machine), the
// ten checks on each run, then the report and the verdict.
//
//   node evals/run.ts --api http://localhost:5280 --models gemma4:26b,sonnet-5.5 --repeat 3
//   node evals/run.ts --models fake --repeat 1          # plumbing, with node evals/fake-model.ts up
//
// Options: --fixture <file>, --out <dir>, --local <model>, --frontier <model> (default: the
// first and second of --models), --max-prompts <n>.

import { readFileSync } from "node:fs";
import { parseArgs } from "node:util";
import { listModels } from "../src/core/model.ts";
import { runChecks } from "./checks.ts";
import { runOnce } from "./harness.ts";
import { writeReport } from "./report.ts";
import type { Results } from "./report.ts";
import type { Fixture, Metrics } from "./types.ts";

const { values } = parseArgs({
  options: {
    api: { type: "string", default: "http://localhost:5280" },
    models: { type: "string", default: "gemma4:26b,sonnet-5.5" },
    repeat: { type: "string", default: "3" },
    fixture: { type: "string", default: "evals/fixtures/harbor-blackout.json" },
    out: { type: "string", default: "evals/results" },
    local: { type: "string" },
    frontier: { type: "string" },
    "max-prompts": { type: "string", default: "150" },
  },
});

const api = values.api!.replace(/\/+$/, "");
const fixture: Fixture = JSON.parse(readFileSync(values.fixture!, "utf8"));
const kind = await (await fetch(`${api}/api/kinds/${fixture.kind}`)).json();
const offered = await listModels(api);
const names = values.models!.split(",").map(s => s.trim()).filter(Boolean);
const local = values.local ?? names[0];
const frontier = values.frontier ?? names[1] ?? names[0];
const repeat = Number(values.repeat);

const results: Results = {};
const metrics: Record<string, Metrics[]> = {};
const stamp = new Date().toISOString().replace(/[:.]/g, "-");
for (const name of names) {
  const model = offered.find(m => m.id === name);
  if (!model) throw new Error(`The API offers no model '${name}'. It offers: ${offered.map(m => m.id).join(", ")}.`);
  results[name] = [];
  metrics[name] = [];
  for (let run = 1; run <= repeat; run++) {
    const tag = `[${name} run ${run}/${repeat}]`;
    const o = await runOnce({
      apiUrl: api,
      model,
      fixture,
      maxPrompts: Number(values["max-prompts"]),
      onPrompt: p => console.error(`${tag} prompt ${p.index + 1} ${((p.endedAt - p.startedAt) / 1000).toFixed(1)} s: ${p.text.slice(0, 70)}`),
    });
    const checks = runChecks(o, fixture, kind);
    results[name].push(checks);
    metrics[name].push(o.metrics);
    const failed = checks.filter(c => !c.pass);
    console.log(`${tag} scenario ${o.scenarioId}: ${checks.length - failed.length}/${checks.length} checks, ${o.metrics.prompts} prompts, ` +
      `${(o.metrics.wallMs / 1000).toFixed(0)} s${failed.length ? ` — failed: ${failed.map(c => `${c.id} ${c.name} (${c.detail})`).join("; ")}` : ""}`);
    // After every run, so stopping a long evaluation keeps what finished.
    writeReport(values.out!, results, metrics, local, frontier, stamp);
  }
}

const report = writeReport(values.out!, results, metrics, local, frontier, stamp);
console.log(`\n${report.verdict.pass ? "PASS" : "FAIL"}${report.verdict.reasons.length ? `: ${report.verdict.reasons.join("; ")}` : ""}`);
console.log(`Report: ${report.md}\nDetails: ${report.json}`);
