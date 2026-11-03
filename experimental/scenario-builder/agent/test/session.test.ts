import { describe, expect, it } from "vitest";
import type { AgentMessage } from "@earendil-works/pi-agent-core";
import { fauxAssistantMessage, fauxToolCall } from "@earendil-works/pi-ai";
import { builderModel } from "../src/core/model.ts";
import { prune } from "../src/core/prune.ts";
import { BuilderSession } from "../src/core/session.ts";
import type { SessionEvent } from "../src/core/session.ts";
import { FakeApi } from "./fake-api.ts";
import { fauxModel } from "./faux.ts";

function session() {
  const { faux, stream } = fauxModel();
  const api = new FakeApi();
  const s = new BuilderSession({
    model: builderModel("http://builder", { id: "m", contextWindow: 32768 }),
    api,
    scenarioId: 1,
    streamFn: stream,
  });
  const events: SessionEvent[] = [];
  s.subscribe(e => events.push(e));
  return { faux, api, s, events };
}

describe("BuilderSession", () => {
  it("ends the turn after a successful ask_user", async () => {
    const { faux, s, events } = session();
    faux.setResponses([
      fauxAssistantMessage([fauxToolCall("get_progress", {})], { stopReason: "toolUse" }),
      fauxAssistantMessage([fauxToolCall("ask_user", {
        questionId: "a", question: "What is A?", recommended: { answer: "A1", why: "It fits." }, alternatives: ["A2"],
      })], { stopReason: "toolUse" }),
      fauxAssistantMessage("This reply must never be asked for."),
    ]);

    await s.prompt("Let's start.");

    expect(events.filter(e => e.type === "question")).toHaveLength(1);
    expect(faux.getPendingResponseCount()).toBe(1);
  });

  it("keeps going after a refused ask_user", async () => {
    const { faux, s, events } = session();
    faux.setResponses([
      fauxAssistantMessage([fauxToolCall("ask_user", { questionId: "a", question: "What is A?", alternatives: [] })], { stopReason: "toolUse" }),
      fauxAssistantMessage("I'll include the recommendation next time."),
    ]);

    await s.prompt("Go.");

    expect(events.filter(e => e.type === "question")).toHaveLength(0);
    expect(faux.getPendingResponseCount()).toBe(0);
  });

  it("starts each prompt with a fresh turn", async () => {
    const { faux, s, events } = session();
    const ask = (id: string, extra: object) => fauxAssistantMessage([fauxToolCall("ask_user", { questionId: id, alternatives: [], ...extra })], { stopReason: "toolUse" });
    faux.setResponses([ask("a", { question: "A?", recommended: { answer: "A1", why: "w" } }), ask("b", { question: "B?" })]);

    await s.prompt("First.");
    await s.answered("a", "A1");

    expect(events.filter(e => e.type === "question").map(e => (e as any).card.questionId)).toEqual(["a", "b"]);
  });

  it("ends a prompt that loops on tool calls at the turn limit, and says so", async () => {
    const { faux, s, events } = session();
    faux.setResponses(Array.from({ length: 30 }, () => fauxAssistantMessage([fauxToolCall("get_progress", {})], { stopReason: "toolUse" })));

    await s.prompt("Go.");

    expect(events.filter(e => e.type === "limit")).toEqual([{ type: "limit", turns: 12 }]);
    expect(faux.getPendingResponseCount()).toBe(30 - 12);
  });

  it("refuses a prompt while a turn is running, leaving that turn's state alone", async () => {
    const { faux, s, events } = session();
    let release!: () => void;
    const gate = new Promise<void>(r => (release = r));
    faux.setResponses([
      async () => {
        await gate;
        return fauxAssistantMessage([fauxToolCall("ask_user", { questionId: "a", question: "A?", recommended: { answer: "A1", why: "w" }, alternatives: [] })], { stopReason: "toolUse" });
      },
      fauxAssistantMessage([fauxToolCall("ask_user", { questionId: "b", question: "B?", alternatives: [] })], { stopReason: "toolUse" }),
    ]);

    const running = s.prompt("First.");
    await expect(s.delegated("b")).rejects.toThrow(/still answering/);
    release();
    await running;

    expect(events.filter(e => e.type === "question").map(e => (e as any).card.questionId)).toEqual(["a"]);
  });

  it("prunes to the window of the model in use now, after a switch", async () => {
    const { faux, s } = session();
    const big = "x".repeat(4000);
    s.agent.state.messages = Array.from({ length: 12 }, (_, i) =>
      ({ role: "toolResult", toolCallId: `t${i}`, toolName: "get_progress", content: [{ type: "text", text: big }], isError: false, timestamp: 0 }) as any);
    s.agent.state.model = builderModel("http://builder", { id: "small", contextWindow: 4000 });
    let seen = "";
    faux.setResponses([(context: any) => {
      seen = JSON.stringify(context.messages);
      return fauxAssistantMessage("Ok.");
    }]);

    await s.prompt("Next.");

    expect(seen).toContain("[older result elided]");
  });

  it("answered() sends the bracketed note", async () => {
    const { faux, s } = session();
    let last = "";
    faux.setResponses([(context: any) => {
      last = JSON.stringify(context.messages.at(-1).content);
      return fauxAssistantMessage("Noted.");
    }]);

    await s.answered("a", "A1");

    expect(last).toContain('[answered a: \\"A1\\"]');
  });

  it("delegated() and approved() send their notes", async () => {
    const { faux, s } = session();
    const seen: string[] = [];
    const capture = (context: any) => {
      seen.push(JSON.stringify(context.messages.at(-1).content));
      return fauxAssistantMessage("Ok.");
    };
    faux.setResponses([capture, capture]);

    await s.delegated("b");
    await s.approved(0);

    expect(seen[0]).toContain("[delegated b]");
    expect(seen[1]).toContain("[approved stage 0]");
  });
});

describe("prune", () => {
  const result = (text: string): AgentMessage =>
    ({ role: "toolResult", toolCallId: "x", toolName: "get_progress", content: [{ type: "text", text }], isError: false, timestamp: 0 }) as AgentMessage;

  it("leaves a conversation under 60% of the window alone", () => {
    const messages = [result("a".repeat(100))];

    expect(prune(messages, 1000)).toBe(messages);
  });

  it("elides old tool results, never the last six messages", () => {
    const messages = Array.from({ length: 10 }, () => result("a".repeat(400)));

    const pruned = prune(messages, 1000);

    expect((pruned[0] as any).content[0].text).toBe("[older result elided]");
    expect(pruned.slice(-6)).toEqual(messages.slice(-6));
  });
});
