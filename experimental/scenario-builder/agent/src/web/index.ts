// The agent in a browser, for the builder UI's side panel: the same session and tools the
// evaluation runs, calling the builder's API with the page's origin, and reaching models only
// through its pass-through.
//
// build.mjs bundles it for the browser. Nothing here may import a Node module, directly or
// through the core: the build fails if anything does.

import type { StreamFn } from "@earendil-works/pi-agent-core";
import { BuilderApi } from "../core/api.ts";
import type { ProposalView as CoreProposalView } from "../core/api.ts";
import { builderModel, listModels } from "../core/model.ts";
import type { ModelInfo as CoreModelInfo } from "../core/model.ts";
import { BuilderSession } from "../core/session.ts";
import type { SessionEvent } from "../core/session.ts";
import type { QuestionCard as CoreQuestionCard, ToolApi } from "../core/tools.ts";
import type { CreateWebAgent, ModelInfo, ProposalView, QuestionCard, WebAgent, WebAgentEvent, WebAgentOptions } from "./types.ts";

export type * from "./types.ts";

// types.ts is copied out as the bundle's declarations, so it can't import the core's types;
// these fail to compile when the core's stop fitting it.
const fits = <T>(_: T) => undefined;
fits<QuestionCard>(undefined as unknown as CoreQuestionCard);
fits<ProposalView>(undefined as unknown as CoreProposalView);
fits<ModelInfo>(undefined as unknown as CoreModelInfo);

/** What tests pass besides the public options: a fake model call, API and model list. */
interface TestOptions {
  streamFn?: StreamFn;
  api?: ToolApi;
  models?: ModelInfo[];
}

export const createWebAgent: CreateWebAgent = async (options: WebAgentOptions & TestOptions): Promise<WebAgent> => {
  const base = options.apiUrl.replace(/\/+$/, "");
  const models = options.models ?? (await listModels(base));
  if (!models.length) throw new Error("The Scenario Builder API offers no models. Check ModelServers in its settings.");
  let model = models.find(m => m.id === options.model) ?? models[0];

  const session = new BuilderSession({
    model: builderModel(base, model),
    api: options.api ?? new BuilderApi(base, { via: "agent" }),
    scenarioId: options.scenarioId,
    streamFn: options.streamFn,
  });

  const listeners = new Set<(event: WebAgentEvent) => void>();
  const emit = (event: WebAgentEvent) => listeners.forEach(l => l(event));
  let messages = 0;
  let current = "";
  session.subscribe(event => translate(event, emit, () => current, id => (current = id), () => `m-${++messages}`));

  return {
    models,
    get model() {
      return model;
    },
    setModel(id: string) {
      const next = models.find(m => m.id === id);
      if (!next) throw new Error(`No model '${id}'. Models: ${models.map(m => m.id).join(", ")}.`);
      model = next;
      session.agent.state.model = builderModel(base, next);
    },
    send: text => session.prompt(text),
    answered: (questionId, value) => session.answered(questionId, value),
    delegated: questionId => session.delegated(questionId),
    approved: stage => session.approved(stage),
    abort: () => session.abort(),
    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
};

function translate(
  event: SessionEvent,
  emit: (event: WebAgentEvent) => void,
  currentId: () => string,
  setId: (id: string) => void,
  nextId: () => string,
): void {
  if (event.type === "question") return emit({ type: "question", card: event.card });
  if (event.type === "proposal") return emit({ type: "proposal", proposal: event.proposal });
  if (event.type === "limit")
    return emit({ type: "error", message: `The agent stopped after ${event.turns} model calls without asking anything. Send a message to carry on.` });

  const e = event.event;
  switch (e.type) {
    case "agent_start":
      return emit({ type: "busy", busy: true });
    case "agent_end":
      return emit({ type: "busy", busy: false });
    case "message_start":
      if ((e.message as any).role === "assistant") setId(nextId());
      return;
    case "message_update": {
      const text = textOf(e.message);
      if (text) emit({ type: "text", id: currentId(), text, final: false });
      return;
    }
    case "message_end": {
      const m = e.message as any;
      if (m.role !== "assistant") return;
      if (m.stopReason === "error") return emit({ type: "error", message: m.errorMessage ?? "The model call failed." });
      const text = textOf(m);
      if (text) emit({ type: "text", id: currentId(), text, final: true });
      return;
    }
    case "tool_execution_end": {
      const result = e.result as { content?: { type: string; text?: string }[]; details?: { summary?: string } } | undefined;
      const error = e.isError ? (result?.content ?? []).map(c => c.text ?? "").join("") || "The tool failed." : undefined;
      return emit({ type: "tool", name: e.toolName, summary: result?.details?.summary ?? e.toolName, ...(error ? { error } : {}) });
    }
  }
}

function textOf(message: unknown): string {
  const m = message as { role?: string; content?: { type: string; text?: string }[] };
  if (m.role !== "assistant" || !Array.isArray(m.content)) return "";
  return m.content.filter(c => c.type === "text").map(c => c.text ?? "").join("");
}
