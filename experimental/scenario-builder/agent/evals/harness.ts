// One evaluation run: a fresh scenario, the agent driven from Node on the same core the browser
// runs, and a scripted person answering from the fixture the way the UI would: a card answer is
// recorded as the person and then told to the agent; "You decide" is only told; an approval is
// given once it's all the gate waits for.

import type { StreamFn } from "@earendil-works/pi-agent-core";
import { BuilderApi } from "../src/core/api.ts";
import { builderModel } from "../src/core/model.ts";
import type { ModelInfo } from "../src/core/model.ts";
import { BuilderSession } from "../src/core/session.ts";
import type { Call, Fixture, Observed, PromptRecord } from "./types.ts";

export interface RunOptions {
  apiUrl: string;
  model: ModelInfo;
  fixture: Fixture;
  /** Ends the run here; default 150. */
  maxPrompts?: number;
  /** Prompts in a row with nothing to act on before the run counts as stalled; default 3. */
  maxStalls?: number;
  /** Replaces the model call; tests pass a faux one. */
  streamFn?: StreamFn;
  /** Hears each prompt as it finishes, for progress output. */
  onPrompt?: (prompt: PromptRecord) => void;
}

export async function runOnce(opts: RunOptions): Promise<Observed> {
  const maxPrompts = opts.maxPrompts ?? 150;
  const maxStalls = opts.maxStalls ?? 3;
  const person = new BuilderApi(opts.apiUrl);
  const { id } = await person.createScenario(`eval ${opts.model.id} ${new Date().toISOString()}`, opts.fixture.kind);
  const session = new BuilderSession({
    model: builderModel(opts.apiUrl, opts.model),
    api: new BuilderApi(opts.apiUrl, { via: "agent" }),
    scenarioId: id,
    streamFn: opts.streamFn,
  });

  const calls: Call[] = [];
  const replies: Observed["replies"] = [];
  const prompts: PromptRecord[] = [];
  const argsById = new Map<string, unknown>();
  let asked: string | null = null;
  const limitHits: number[] = [];
  let inputTokens = 0;
  let outputTokens = 0;
  let error: string | undefined;
  const current = () => prompts.length - 1;

  session.subscribe(e => {
    if (e.type === "question") {
      asked = e.card.questionId;
      return;
    }
    if (e.type === "limit") {
      limitHits.push(current());
      return;
    }
    if (e.type !== "agent") return;
    const ev = e.event;
    switch (ev.type) {
      case "message_update": {
        const p = prompts[current()];
        if (p && p.firstTokenMs === undefined) p.firstTokenMs = Date.now() - p.startedAt;
        return;
      }
      case "message_end": {
        const m = ev.message as any;
        if (m.role !== "assistant") return;
        inputTokens += m.usage?.input ?? 0;
        outputTokens += m.usage?.output ?? 0;
        if (m.stopReason === "error") error = m.errorMessage ?? "The model call failed.";
        const text = (m.content ?? []).filter((c: any) => c.type === "text").map((c: any) => c.text).join("");
        if (text.trim()) replies.push({ prompt: current(), text });
        return;
      }
      case "tool_execution_start":
        argsById.set(ev.toolCallId, ev.args);
        return;
      case "tool_execution_end":
        calls.push({ name: ev.toolName, args: argsById.get(ev.toolCallId), isError: ev.isError, code: ev.result?.details?.code, prompt: current() });
        return;
    }
  });

  const send = async (text: string, call: () => Promise<void>) => {
    prompts.push({ index: prompts.length, text, startedAt: Date.now(), endedAt: 0 });
    asked = null;
    try {
      await call();
    } catch (e) {
      error = (e as Error).message;
    }
    prompts[current()].endedAt = Date.now();
    opts.onPrompt?.(prompts[current()]);
  };

  const started = Date.now();
  let stalled = false;
  let finished = false;
  let stalls = 0;
  await send(opts.fixture.opening, () => session.prompt(opts.fixture.opening));

  try {
    while (!error && prompts.length < maxPrompts) {
      const q: string | null = asked;
      if (q) {
        stalls = 0;
        if (opts.fixture.delegate.includes(q)) {
          await send(`[delegated ${q}]`, () => session.delegated(q));
          continue;
        }
        const value = opts.fixture.answers[q];
        if (value === undefined) {
          error = `The fixture has no answer for '${q}'.`;
          break;
        }
        await person.putDecision(id, q, { value });
        await send(`[answered ${q}: ${JSON.stringify(value)}]`, () => session.answered(q, value));
        continue;
      }

      const progress = await person.progress(id);
      if (progress.lastStage && progress.gate.passes) {
        finished = true;
        break;
      }
      const findings = progress.gate.findings;
      if (progress.gate.approval && !progress.gate.approved && findings.length && findings.every(f => f.code.startsWith("APPROVAL_"))) {
        stalls = 0;
        await person.approve(id, progress.stage);
        await send(`[approved stage ${progress.stage}]`, () => session.approved(progress.stage));
        continue;
      }
      if (++stalls >= maxStalls) {
        stalled = true;
        break;
      }
      await send("[continue]", () => session.prompt("[continue]"));
    }
  } catch (e) {
    error = (e as Error).message;
  }

  let doc: any = null;
  try {
    doc = await person.pull(id);
  } catch (e) {
    error ??= (e as Error).message;
  }
  const firstTokens = prompts.flatMap(p => (p.firstTokenMs === undefined ? [] : [p.firstTokenMs])).sort((a, b) => a - b);
  return {
    model: opts.model.id,
    scenarioId: id,
    calls,
    replies,
    prompts,
    stalled,
    stoppedAtLimit: !finished && !stalled && !error && prompts.length >= maxPrompts,
    limitHits,
    error,
    doc,
    metrics: {
      prompts: prompts.length,
      wallMs: Date.now() - started,
      inputTokens,
      outputTokens,
      medianFirstTokenMs: firstTokens.length ? firstTokens[Math.floor(firstTokens.length / 2)] : null,
    },
  };
}
