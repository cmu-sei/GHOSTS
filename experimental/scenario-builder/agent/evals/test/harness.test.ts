// The harness end to end: the real API on a scratch database, and the agent driven by the fake
// model (plumbing) or by a faux model that never asks anything (the stall guard).

import { execFileSync, spawn } from "node:child_process";
import type { ChildProcess } from "node:child_process";
import { readFileSync, rmSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { fauxAssistantMessage } from "@earendil-works/pi-ai";
import { fauxModel } from "../../test/faux.ts";
import { startFakeModel } from "../fake-model.ts";
import { runOnce } from "../harness.ts";
import type { Fixture } from "../types.ts";

const API = "http://localhost:5281";
const apiDir = fileURLToPath(new URL("../../../api/", import.meta.url));
const db = `/tmp/sb-eval-${process.pid}.db`;
const fixture: Fixture = JSON.parse(readFileSync(new URL("../fixtures/harbor-blackout.json", import.meta.url), "utf8"));

let api: ChildProcess | undefined;
let fake: { close(): Promise<void> } | undefined;

async function waitFor(url: string, ms: number): Promise<void> {
  const until = Date.now() + ms;
  while (Date.now() < until) {
    try {
      if ((await fetch(url)).ok) return;
    } catch {
      // not up yet
    }
    await new Promise(r => setTimeout(r, 500));
  }
  throw new Error(`${url} didn't come up in ${ms / 1000} s`);
}

beforeAll(async () => {
  execFileSync("dotnet", ["build", "ScenarioBuilder.slnx", "-v", "q", "-nologo"], { cwd: apiDir, stdio: "ignore" });
  api = spawn("dotnet", ["run", "--no-build", "--no-launch-profile"], {
    cwd: `${apiDir}ScenarioBuilder.Api`,
    env: { ...process.env, ASPNETCORE_URLS: API, ConnectionStrings__Builder: `Data Source=${db}` },
    stdio: "ignore",
  });
  await waitFor(`${API}/api/kinds`, 60_000);
  fake = await startFakeModel(5299);
}, 240_000);

afterAll(async () => {
  api?.kill();
  await fake?.close();
  rmSync(db, { force: true });
});

describe("runOnce", () => {
  it("plumbing: the fake model reaches stage 1, and the run records calls and timings", async () => {
    const o = await runOnce({ apiUrl: API, model: { id: "fake", contextWindow: 32768 }, fixture, maxPrompts: 40 });

    expect(o.error).toBeUndefined();
    expect(o.calls.map(c => c.name)).toEqual(expect.arrayContaining(["get_progress", "ask_user", "advance_stage"]));
    expect(o.doc.stage).toBeGreaterThanOrEqual(1);
    expect(o.prompts[0].text).toBe(fixture.opening);
    expect(o.prompts.some(p => p.text === "[approved stage 0]")).toBe(true);
    expect(o.metrics.prompts).toBe(o.prompts.length);
    expect(o.metrics.medianFirstTokenMs).not.toBeNull();
  }, 120_000);

  it("harness stops after three stalls", async () => {
    const { faux, stream } = fauxModel();
    faux.setResponses(Array.from({ length: 10 }, () => fauxAssistantMessage("ok")));

    const o = await runOnce({ apiUrl: API, model: { id: "fake", contextWindow: 32768 }, fixture, streamFn: stream });

    expect(o.stalled).toBe(true);
    expect(o.prompts.length).toBeLessThanOrEqual(4);
    expect(o.prompts.slice(1).every(p => p.text === "[continue]")).toBe(true);
  }, 60_000);
});
