// The pass bar (spec §12) and the report: one table per model, its median metrics, the verdict.
//
// Pass: the local model passes all ten checks in at least two of three runs (two thirds,
// rounded up, for another count), and no check passes on the frontier model in any run while
// failing on the local model in every run. Time is reported; it doesn't decide.

import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import type { CheckResult, Metrics } from "./types.ts";

export type Results = Record<string, CheckResult[][]>;

export function verdict(results: Results, local: string, frontier: string): { pass: boolean; reasons: string[] } {
  const reasons: string[] = [];
  const localRuns = results[local] ?? [];
  const frontierRuns = results[frontier] ?? [];
  if (!localRuns.length) return { pass: false, reasons: [`no runs for ${local}`] };
  if (!frontierRuns.length) reasons.push(`no runs for ${frontier} to compare against`);

  const needed = Math.ceil((localRuns.length * 2) / 3);
  const clean = localRuns.filter(run => run.every(c => c.pass)).length;
  if (clean < needed) reasons.push(`${local} passed every check in ${clean} of ${localRuns.length} runs; it needs ${needed}`);

  for (const check of localRuns[0]) {
    const frontierPassed = frontierRuns.some(run => run.find(c => c.id === check.id)?.pass);
    const localFailedAll = localRuns.every(run => !run.find(c => c.id === check.id)?.pass);
    if (frontierPassed && localFailedAll)
      reasons.push(`check ${check.id} ${check.name}: ${frontier} passed it, ${local} failed it in every run`);
  }
  return { pass: reasons.length === 0, reasons };
}

const median = (values: number[]): number | null => {
  if (!values.length) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
};

const seconds = (ms: number | null) => (ms === null ? "–" : `${(ms / 1000).toFixed(1)} s`);

export function writeReport(
  dir: string,
  results: Results,
  metrics: Record<string, Metrics[]>,
  local: string,
  frontier: string,
  /** One per invocation: rewriting the same file after every run keeps each finished run. */
  stamp = new Date().toISOString().replace(/[:.]/g, "-"),
): { json: string; md: string; verdict: { pass: boolean; reasons: string[] } } {
  mkdirSync(dir, { recursive: true });
  const json = join(dir, `results-${stamp}.json`);
  const md = join(dir, "report.md");
  const v = verdict(results, local, frontier);
  writeFileSync(json, JSON.stringify({ local, frontier, verdict: v, results, metrics }, null, 2));

  const lines = [`# Scenario Builder evaluation, ${new Date().toISOString().slice(0, 16).replace("T", " ")} UTC`, ""];
  lines.push(`**Verdict: ${v.pass ? "PASS" : "FAIL"}** (${local} against ${frontier})`, "");
  for (const reason of v.reasons) lines.push(`- ${reason}`);
  if (v.reasons.length) lines.push("");

  for (const [model, runs] of Object.entries(results)) {
    const checks = runs[0] ?? [];
    lines.push(`## ${model}`, "");
    lines.push(`| run | ${checks.map(c => `${c.id} ${c.name}`).join(" | ")} |`);
    lines.push(`|---|${checks.map(() => "---").join("|")}|`);
    runs.forEach((run, i) => lines.push(`| ${i + 1} | ${run.map(c => (c.pass ? "✓" : `✗ ${c.detail.replace(/\|/g, "/")}`)).join(" | ")} |`));
    const m = metrics[model] ?? [];
    lines.push("");
    lines.push(`Median per run: ${median(m.map(x => x.prompts)) ?? "–"} prompts, ${seconds(median(m.map(x => x.wallMs)))} wall time, ` +
      `${median(m.map(x => x.inputTokens)) ?? "–"} input tokens, ${median(m.map(x => x.outputTokens)) ?? "–"} output tokens, ` +
      `${seconds(median(m.flatMap(x => (x.medianFirstTokenMs === null ? [] : [x.medianFirstTokenMs]))))} to the first token.`, "");
  }
  writeFileSync(md, lines.join("\n"));
  return { json, md, verdict: v };
}
