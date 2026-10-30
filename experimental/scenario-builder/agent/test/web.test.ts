import { describe, expect, it } from "vitest";
import { fauxAssistantMessage, fauxToolCall } from "@earendil-works/pi-ai";
import { createWebAgent } from "../src/web/index.ts";
import type { WebAgentEvent } from "../src/web/index.ts";
import { FakeApi } from "./fake-api.ts";
import { fauxModel } from "./faux.ts";

const MODELS = [{ id: "fake", contextWindow: 32768 }, { id: "other", contextWindow: 8192 }];

async function agent(model?: string) {
  const { faux, stream } = fauxModel();
  const web = await createWebAgent({ apiUrl: "http://builder/", scenarioId: 1, model, streamFn: stream, api: new FakeApi(), models: MODELS } as any);
  const events: WebAgentEvent[] = [];
  web.subscribe(e => events.push(e));
  return { faux, web, events };
}

describe("createWebAgent", () => {
  it("maps a reply that asks a question to busy, tool, question and idle events", async () => {
    const { faux, web, events } = await agent();
    faux.setResponses([
      fauxAssistantMessage([fauxToolCall("ask_user", {
        questionId: "a", question: "What is A?", recommended: { answer: "A1", why: "It fits." }, alternatives: ["A2"],
      })], { stopReason: "toolUse" }),
    ]);

    await web.send("Start.");

    expect(events.map(e => e.type)).toEqual(expect.arrayContaining(["busy", "tool", "question"]));
    expect(events[0]).toEqual({ type: "busy", busy: true });
    expect(events.at(-1)).toEqual({ type: "busy", busy: false });
    expect(events.find(e => e.type === "question")).toMatchObject({ card: { questionId: "a", answer: "text" } });
    expect(events.find(e => e.type === "tool")).toMatchObject({ name: "ask_user", summary: "asked a" });
  });

  it("streams text and marks the message final", async () => {
    const { faux, web, events } = await agent();
    faux.setResponses([fauxAssistantMessage("Hello there.")]);

    await web.send("Hi.");

    const texts = events.filter(e => e.type === "text") as Extract<WebAgentEvent, { type: "text" }>[];
    expect(texts.at(-1)).toMatchObject({ text: "Hello there.", final: true });
    expect(new Set(texts.map(t => t.id)).size).toBe(1);
  });

  it("reports a refused tool call with its error", async () => {
    const { faux, web, events } = await agent();
    faux.setResponses([
      fauxAssistantMessage([fauxToolCall("ask_user", { questionId: "c", question: "C?", alternatives: [] })], { stopReason: "toolUse" }),
      fauxAssistantMessage("Sorry."),
    ]);

    await web.send("Go.");

    expect(events.find(e => e.type === "tool")).toMatchObject({ name: "ask_user", summary: "FUTURE_STAGE", error: expect.stringContaining("stage 1") });
  });

  it("tells the designer when a reply hits the turn limit", async () => {
    const { faux, web, events } = await agent();
    faux.setResponses(Array.from({ length: 12 }, () => fauxAssistantMessage([fauxToolCall("get_progress", {})], { stopReason: "toolUse" })));

    await web.send("Go.");

    expect(events.find(e => e.type === "error")).toMatchObject({ message: expect.stringContaining("12 model calls") });
  });

  it("picks the asked-for model, or the first, and can switch", async () => {
    expect((await agent("other")).web.model.id).toBe("other");
    const { web } = await agent("missing");
    expect(web.model.id).toBe("fake");

    web.setModel("other");

    expect(web.model.id).toBe("other");
    expect(web.models.map(m => m.id)).toEqual(["fake", "other"]);
  });
});
