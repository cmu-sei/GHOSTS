import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { runChecks } from "../checks.ts";
import { mkdtempSync, readdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { verdict, writeReport } from "../report.ts";
import type { Call, CheckResult, Fixture, Observed } from "../types.ts";

const fixture: Fixture = JSON.parse(readFileSync(new URL("../fixtures/harbor-blackout.json", import.meta.url), "utf8"));
const kind = JSON.parse(readFileSync(new URL("../../../kinds/crucible-tabletop.kind.json", import.meta.url), "utf8"));
const ALL = (kind.stages as { questions: { id: string }[] }[]).flatMap(s => s.questions.map(q => q.id));

/** A clean run: every question decided as the fixture intends, nothing refused. */
function observed(overrides: Partial<Observed> = {}): Observed {
  const decisions = ALL.map(id => ({
    question: id,
    who: fixture.delegate.includes(id) ? "proposed" : "stated",
    by: "dev",
    via: fixture.delegate.includes(id) || id in fixture.openingQuestions ? "agent" : "person",
    value: fixture.answers[id] ?? "chosen",
    ...(fixture.delegate.includes(id) ? { reason: "A sensible default." } : {}),
  }));
  return {
    model: "m",
    scenarioId: 1,
    calls: [],
    replies: [],
    prompts: [{ index: 0, text: fixture.opening, startedAt: 0, endedAt: 1 }],
    stalled: false,
    stoppedAtLimit: false,
    limitHits: [],
    doc: { decisions, scenario: { executiveSummary: "Heat cuts power at the port's data centre." } },
    metrics: { prompts: 1, wallMs: 1, inputTokens: 0, outputTokens: 0, medianFirstTokenMs: null },
    ...overrides,
  };
}

const call = (name: string, args: any, extra: Partial<Call> = {}): Call => ({ name, args, isError: false, prompt: 0, ...extra });
const check = (o: Observed, id: number) => runChecks(o, fixture, kind).find(c => c.id === id)!;

describe("runChecks", () => {
  it("passes all ten on a clean run", () => {
    expect(runChecks(observed(), fixture, kind).filter(c => !c.pass)).toEqual([]);
  });

  it("check 1 fails when a question is left unanswered or the run stalls", () => {
    const o = observed();
    o.doc.decisions.pop();

    expect(check(o, 1).pass).toBe(false);
    expect(check(observed({ stalled: true }), 1).pass).toBe(false);
  });

  it("check 2 fails when a question is asked twice", () => {
    const o = observed({ calls: [call("ask_user", { questionId: "world" }), call("ask_user", { questionId: "world" }, { prompt: 3 })] });

    expect(check(o, 2)).toMatchObject({ pass: false, detail: expect.stringContaining("world") });
  });

  it("check 2 counts refused re-asks of an answered question", () => {
    const refusedReask = (prompt: number) => call("ask_user", { questionId: "world" }, { isError: true, code: "ANSWERED", prompt });
    const o = observed({ calls: [call("ask_user", { questionId: "world" }), refusedReask(2), refusedReask(3)] });

    expect(check(o, 2)).toMatchObject({ pass: false, detail: expect.stringContaining("world") });
  });

  it("check 2 fails when a prompt hit the turn limit", () => {
    expect(check(observed({ limitHits: [5] }), 2)).toMatchObject({ pass: false, detail: expect.stringContaining("turn limit") });
  });

  it("check 3 fails when an opening question is asked, even if refused", () => {
    const o = observed({ calls: [call("ask_user", { questionId: "headcount" }, { isError: true, code: "ANSWERED" })] });

    expect(check(o, 3)).toMatchObject({ pass: false, detail: expect.stringContaining("headcount") });
  });

  it.each([
    [4, "ALREADY_ASKED"],
    [5, "NO_RECOMMENDED"],
    [6, "FUTURE_STAGE"],
  ])("check %i fails on a %s refusal", (id, code) => {
    const o = observed({ calls: [call("ask_user", { questionId: "x" }, { isError: true, code })] });

    expect(check(o, id).pass).toBe(false);
  });

  it("check 7 fails when a delegated question ends stated, or proposed without a reason", () => {
    const stated = observed();
    stated.doc.decisions.find((d: any) => d.question === "framework").who = "stated";
    const reasonless = observed();
    delete reasonless.doc.decisions.find((d: any) => d.question === "redHerring").reason;

    expect(check(stated, 7).pass).toBe(false);
    expect(check(reasonless, 7).pass).toBe(false);
  });

  it("check 8 passes a stated answer quoted from the opening", () => {
    const o = observed({ calls: [call("record_answer", { questionId: "headcount", value: "20 people, four teams of five", who: "stated", quote: "20 port and logistics  operations leaders, in four teams of five" })] });

    expect(check(o, 8).pass).toBe(true);
  });

  it("check 8 fails when a stated quote isn't in the opening message", () => {
    const o = observed({ calls: [call("record_answer", { questionId: "headcount", value: "20, teams of five", who: "stated", quote: "twenty people" })] });

    expect(check(o, 8)).toMatchObject({ pass: false, detail: expect.stringContaining("quote") });
  });

  it("check 8 fails when the value lacks the fixture's fragments", () => {
    const o = observed({ calls: [call("record_answer", { questionId: "timeSlot", value: "Morning", who: "stated", quote: "It runs on 4 March 2027" })] });

    expect(check(o, 8).pass).toBe(false);
  });

  it("check 8 fails when a non-opening question is recorded as stated", () => {
    const o = observed({ calls: [call("record_answer", { questionId: "world", value: "Fictional world", who: "stated", quote: "keep everything plain" })] });

    expect(check(o, 8)).toMatchObject({ pass: false, detail: expect.stringContaining("world") });
  });

  it("check 9 ignores 'recorded' in a prompt that recorded something, and flags it in one that didn't", () => {
    const fine = observed({
      calls: [call("record_answer", { questionId: "world", who: "proposed", reason: "r" }, { prompt: 2 })],
      prompts: [{ index: 0, text: fixture.opening, startedAt: 0, endedAt: 1 }, { index: 1, text: "[continue]", startedAt: 1, endedAt: 2 }, { index: 2, text: "[delegated world]", startedAt: 2, endedAt: 3 }],
      replies: [{ prompt: 2, text: "I've recorded a fictional world." }],
    });
    const card = observed({
      prompts: [{ index: 0, text: fixture.opening, startedAt: 0, endedAt: 1 }, { index: 1, text: '[answered world: "Fictional world"]', startedAt: 1, endedAt: 2 }],
      replies: [{ prompt: 1, text: "Noted." }],
    });
    const claim = observed({
      prompts: [{ index: 0, text: fixture.opening, startedAt: 0, endedAt: 1 }, { index: 1, text: "[continue]", startedAt: 1, endedAt: 2 }],
      replies: [{ prompt: 1, text: "Your settings are saved." }],
    });

    expect(check(fine, 9).pass).toBe(true);
    expect(check(card, 9).pass).toBe(true);
    expect(check(claim, 9).pass).toBe(false);
  });

  it("check 9 doesn't count a negated claim", () => {
    const o = observed({
      prompts: [{ index: 0, text: fixture.opening, startedAt: 0, endedAt: 1 }, { index: 1, text: "[continue]", startedAt: 1, endedAt: 2 }],
      replies: [{ prompt: 1, text: "Nothing has been saved for redHerring yet. Shall I propose one?" }],
    });

    expect(check(o, 9).pass).toBe(true);
  });

  it("check 10 matches fragments at word starts, so 'support' isn't 'port'", () => {
    const o = observed();
    o.doc.scenario.executiveSummary = "Heat cuts power, and support staff report delays.";

    expect(check(o, 10)).toMatchObject({ pass: false, detail: expect.stringContaining('"port"') });
  });

  it("check 10 fails without the summary, without a required fragment, or with a banned one", () => {
    const none = observed();
    delete none.doc.scenario;
    const missing = observed();
    missing.doc.scenario.executiveSummary = "The port loses power.";
    const banned = observed();
    banned.doc.scenario.executiveSummary = "Heat and a hacker cut power at the port.";

    expect(check(none, 10).pass).toBe(false);
    expect(check(missing, 10)).toMatchObject({ pass: false, detail: expect.stringContaining("heat") });
    expect(check(banned, 10)).toMatchObject({ pass: false, detail: expect.stringContaining("hacker") });
  });
});

describe("verdict", () => {
  const run = (failing: number[] = []): CheckResult[] =>
    Array.from({ length: 10 }, (_, i) => ({ id: i + 1, name: `c${i + 1}`, pass: !failing.includes(i + 1), detail: "" }));

  it("passes when gemma passes all checks in two of three runs", () => {
    const v = verdict({ gemma: [run(), run([3]), run()], sonnet: [run(), run(), run()] }, "gemma", "sonnet");

    expect(v.pass).toBe(true);
  });

  it("fails when gemma passes all checks in only one of three runs", () => {
    const v = verdict({ gemma: [run(), run([3]), run([5])], sonnet: [run(), run(), run()] }, "gemma", "sonnet");

    expect(v.pass).toBe(false);
    expect(v.reasons.join(" ")).toContain("1 of 3");
  });

  it("fails when sonnet passes a check gemma fails in all three runs", () => {
    const v = verdict({ gemma: [run([8]), run([8]), run([8])], sonnet: [run([8]), run(), run()] }, "gemma", "sonnet");

    expect(v.pass).toBe(false);
    expect(v.reasons.join(" ")).toContain("c8");
  });

  it("doesn't hold gemma to a check sonnet fails too", () => {
    const v = verdict({ gemma: [run(), run(), run([8])], sonnet: [run([8]), run([8]), run([8])] }, "gemma", "sonnet");

    expect(v.pass).toBe(true);
  });
});

describe("writeReport", () => {
  it("rewrites one results file per invocation, so every finished run is kept", () => {
    const dir = mkdtempSync(join(tmpdir(), "sb-report-"));
    const run = [{ id: 1, name: "coverage", pass: true, detail: "" }];
    const metrics = { prompts: 1, wallMs: 1, inputTokens: 0, outputTokens: 0, medianFirstTokenMs: null };

    writeReport(dir, { gemma: [run] }, { gemma: [metrics] }, "gemma", "sonnet", "stamp-1");
    writeReport(dir, { gemma: [run, run] }, { gemma: [metrics, metrics] }, "gemma", "sonnet", "stamp-1");

    expect(readdirSync(dir).sort()).toEqual(["report.md", "results-stamp-1.json"]);
  });
});
