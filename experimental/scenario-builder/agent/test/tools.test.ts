import { beforeEach, describe, expect, it } from "vitest";
import type { AgentTool } from "@earendil-works/pi-agent-core";
import { ApiError } from "../src/core/api.ts";
import type { ProposalView } from "../src/core/api.ts";
import { createTools } from "../src/core/tools.ts";
import type { QuestionCard, ToolContext } from "../src/core/tools.ts";
import { FakeApi } from "./fake-api.ts";

let api: FakeApi;
let ctx: ToolContext;
let cards: QuestionCard[];
let proposals: ProposalView[];

const run = (name: string, params: Record<string, unknown>) => {
  const tool = createTools(ctx).find(t => t.name === name) as AgentTool<any>;
  return tool.execute("call-1", params);
};
const text = (r: { content: { type: string; text?: string }[] }) => r.content.map(c => c.text ?? "").join("");
const askA = { questionId: "a", question: "What is A?", recommended: { answer: "A1", why: "It fits." }, alternatives: ["A2", "A3"] };

beforeEach(() => {
  api = new FakeApi();
  cards = [];
  proposals = [];
  ctx = { api, scenarioId: 1, turn: { asked: false }, said: [], onQuestion: c => cards.push(c), onProposal: p => proposals.push(p) };
});

describe("ask_user", () => {
  it("shows the card and marks the turn", async () => {
    const result = await run("ask_user", askA);

    expect(result.isError).toBeFalsy();
    expect(text(result)).toContain("Question shown");
    expect(ctx.turn.asked).toBe(true);
    expect(cards).toEqual([{ ...askA, answer: "text" }]);
  });

  it("refuses a second question in the same turn", async () => {
    await run("ask_user", askA);

    const second = await run("ask_user", { questionId: "b", question: "Pick one", alternatives: ["x", "y"] });

    expect(second.isError).toBe(true);
    expect(second.details.code).toBe("ALREADY_ASKED");
    expect(cards).toHaveLength(1);
  });

  it("refuses a future-stage question and names the open ones", async () => {
    const result = await run("ask_user", { questionId: "c", question: "List some", alternatives: [] });

    expect(result.details.code).toBe("FUTURE_STAGE");
    expect(text(result)).toContain("Open now: a, b");
  });

  it("refuses an answered question", async () => {
    await api.putDecision(1, "a", { value: "A1", who: "stated", quote: "A1" });

    const result = await run("ask_user", askA);

    expect(result.details.code).toBe("ANSWERED");
    expect(text(result)).toContain('"A1"');
  });

  it("refuses when the recommended answer is left out", async () => {
    const result = await run("ask_user", { questionId: "a", question: "What is A?", alternatives: ["A2"] });

    expect(result.details.code).toBe("NO_RECOMMENDED");
    expect(text(result)).not.toContain("sources");
    expect(text(result)).toContain("A1");
    expect(ctx.turn.asked).toBe(false);
  });

  it("refuses a question the kind doesn't have", async () => {
    const result = await run("ask_user", { questionId: "zzz", question: "?", alternatives: [] });

    expect(result.details.code).toBe("UNKNOWN_QUESTION");
  });

  it("fetches the kind again after a failed fetch", async () => {
    // One tool set, as a session keeps it: the kind is cached across calls.
    const ask = createTools(ctx).find(t => t.name === "ask_user") as AgentTool<any>;
    api.failKind = new ApiError(0, "UNREACHABLE", "Can't reach the Scenario Builder API.");
    const failed = await ask.execute("call-1", askA);
    expect(failed.details.code).toBe("UNREACHABLE");

    const retried = await ask.execute("call-2", askA);

    expect(retried.isError).toBeFalsy();
    expect(cards).toHaveLength(1);
  });

  it("carries a choice's options onto the card", async () => {
    await run("ask_user", { questionId: "b", question: "Pick one", alternatives: ["y"] });

    expect(cards[0]).toMatchObject({ answer: "choice", options: ["x", "y"] });
  });
});

describe("record_answer", () => {
  it("needs a quote for a stated answer", async () => {
    const result = await run("record_answer", { questionId: "a", value: "A1", who: "stated", reason: "" });

    expect(result.details.code).toBe("QUOTE_REQUIRED");
    expect(api.answers.size).toBe(0);
  });

  it("needs a reason for a proposed answer", async () => {
    const result = await run("record_answer", { questionId: "a", value: "A1", who: "proposed" });

    expect(result.details.code).toBe("REASON_REQUIRED");
  });

  it("records a proposed answer with its reason", async () => {
    const result = await run("record_answer", { questionId: "b", value: "x", who: "proposed", reason: "Simplest." });

    expect(result.isError).toBeFalsy();
    expect(api.answers.get("b")).toMatchObject({ value: "x", who: "proposed", reason: "Simplest." });
  });

  it("passes API errors through with their code", async () => {
    api.fail = new ApiError(409, "STATED", "'a' was stated by dev. Use propose_change to suggest a different value.");

    const result = await run("record_answer", { questionId: "a", value: "A2", who: "proposed", reason: "Better" });

    expect(result.isError).toBe(true);
    expect(result.details.code).toBe("STATED");
    expect(text(result)).toContain("propose_change");
  });
});

describe("delegation", () => {
  it("record_answers records a whole stage as proposed, each with its reason", async () => {
    const result = await run("record_answers", { answers: [
      { questionId: "a", value: "A1", reason: "The kind recommends it." },
      { questionId: "b", value: "x", reason: "Simplest." },
    ] });

    expect(result.isError).toBeFalsy();
    expect([...api.answers.values()].map(a => [a.id, a.who, a.reason])).toEqual([["a", "proposed", "The kind recommends it."], ["b", "proposed", "Simplest."]]);
    expect(text(result)).toContain("Recorded 2");
  });

  it("record_answers reports what the API refused and keeps the rest", async () => {
    await api.putDecision(1, "a", { value: "A1", who: "stated", quote: "A1" });

    const result = await run("record_answers", { answers: [
      { questionId: "a", value: "A2", reason: "r" },
      { questionId: "b", value: "y", reason: "r" },
    ] });

    expect(result.isError).toBeFalsy();
    expect(api.answers.get("b")?.value).toBe("y");
    expect(text(result)).toContain("a: STATED");
  });

  it("record_answers needs a reason for every answer", async () => {
    const result = await run("record_answers", { answers: [{ questionId: "a", value: "A1", reason: " " }] });

    expect(result.details.code).toBe("REASON_REQUIRED");
    expect(api.answers.size).toBe(0);
  });

  it("approve_stage approves the open stage while delegated, and passes the refusal through when not", async () => {
    const refused = await run("approve_stage", {});
    expect(refused.details.code).toBe("AGENT_CANNOT_DECIDE");

    api.delegated = true;
    const result = await run("approve_stage", {});

    expect(result.isError).toBeFalsy();
    expect(api.approved).toEqual([0]);
  });

  it("ask_user refuses while delegated: the designer handed the decisions over", async () => {
    api.delegated = true;

    const result = await run("ask_user", askA);

    expect(result.details.code).toBe("DELEGATED");
    expect(text(result)).toContain("record_answers");
    expect(cards).toHaveLength(0);
  });
});

describe("get_progress with sources", () => {
  it("puts the passages for the open questions under them, once each", async () => {
    api.sources.push({ id: 3, name: "Brief", text: "Pick the answer: the brief says y." }, { id: 4, name: "Notes", text: "Unrelated weather." });

    const result = await run("get_progress", {});

    expect(text(result)).toContain("From the sources. When a passage answers a question, recommend what it says, not the kind's suggestion, and name the source in your reason:");
    expect(text(result)).toContain("For b: [Brief, passage 1 of 1]\nPick the answer: the brief says y.");
    expect(text(result).split("[Brief, passage 1 of 1]")).toHaveLength(2);
    expect(text(result)).not.toContain("[Notes,");
  });

  it("adds nothing when no source matches an open question", async () => {
    api.sources.push({ id: 4, name: "Notes", text: "Unrelated weather." });

    expect(text(await run("get_progress", {}))).not.toContain("From the sources");
  });
});

describe("record_answer as stated", () => {
  it("records it when the quote is the designer's own words, whatever the case and spacing", async () => {
    ctx.said.push("hello", "Our people are the  Port Authority team.");

    const result = await run("record_answer", { questionId: "a", value: "Port Authority team", who: "stated", quote: "the port authority team" });

    expect(result.isError).toBeFalsy();
    expect(api.answers.get("a")?.who).toBe("stated");
  });

  it("refuses a quote the designer never wrote, such as a source's words", async () => {
    ctx.said.push("hello");
    api.sources.push({ id: 3, name: "Brief", text: "The participants are the port team." });

    const result = await run("record_answer", { questionId: "a", value: "port team", who: "stated", quote: "The participants are the port team." });

    expect(result.details.code).toBe("QUOTE_NOT_THEIRS");
    expect(text(result)).toContain("record it as proposed, with the source in the reason");
    expect(api.answers.has("a")).toBe(false);
  });
});

describe("ask_user with sources", () => {
  it("asks for the sources' answer before the kind's when a card has no recommendation", async () => {
    api.sources.push({ id: 3, name: "Brief", text: "A is A7." });

    const result = await run("ask_user", { questionId: "a", question: "What is A?", alternatives: ["A2"] });

    expect(result.details.code).toBe("NO_RECOMMENDED");
    expect(text(result)).toContain("Recommend what the sources say if they answer it; otherwise the kind's suggestion, A1.");
  });
});

describe("search_sources", () => {
  it("returns the passages, each under its source", async () => {
    api.sources.push({ id: 3, name: "Port notice", text: "The cranes stopped at 14:10." });

    const result = await run("search_sources", { query: "cranes stopped" });

    expect(result.isError).toBeFalsy();
    expect(text(result)).toContain("[Port notice, passage 1 of 1]\nThe cranes stopped at 14:10.");
  });

  it("says when nothing matches, so the model doesn't invent a source", async () => {
    api.sources.push({ id: 3, name: "Port notice", text: "The cranes stopped." });

    const result = await run("search_sources", { query: "ransomware" });

    expect(result.isError).toBeFalsy();
    expect(text(result)).toContain("No passage in the sources matches");
  });

  it("refuses an empty query", async () => {
    const result = await run("search_sources", { query: "  " });

    expect(result.details.code).toBe("QUERY_EMPTY");
  });
});

describe("the other tools", () => {
  it("get_progress returns the formatted progress", async () => {
    const result = await run("get_progress", {});

    expect(text(result)).toContain("- a [text]: What is A?");
  });

  it("propose_change hands the proposal to the session", async () => {
    const result = await run("propose_change", { questionId: "a", value: "A2", reason: "Clearer." });

    expect(proposals).toHaveLength(1);
    expect(text(result)).toContain("P-1");
    expect(text(result)).toContain("waiting for the owner");
  });

  it("draft writes the field", async () => {
    await run("draft", { field: "executiveSummary", text: "Summary." });

    expect(api.drafts.get("executiveSummary")).toBe("Summary.");
  });

  it("advance_stage reports the stage now open", async () => {
    const result = await run("advance_stage", {});

    expect(text(result)).toContain("Stage 1 is open");
  });

  it("offers no tool beyond the nine", () => {
    expect(createTools(ctx).map(t => t.name).sort()).toEqual(
      ["advance_stage", "approve_stage", "ask_user", "draft", "get_progress", "propose_change", "record_answer", "record_answers", "search_sources"]);
  });
});
