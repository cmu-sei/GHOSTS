// The conversation: a Pi agent with the builder's tools, for one scenario. UI-agnostic; the
// browser panel and the evaluation each wrap one of these and react to its events.

import { Agent } from "@earendil-works/pi-agent-core";
import type { AgentEvent, StreamFn } from "@earendil-works/pi-agent-core";
import type { Model } from "@earendil-works/pi-ai";
import { streamSimple } from "@earendil-works/pi-ai/api/openai-completions";
import type { ProposalView } from "./api.ts";
import { systemPrompt } from "./prompt.ts";
import { prune } from "./prune.ts";
import { createTools } from "./tools.ts";
import type { QuestionCard, ToolApi } from "./tools.ts";

export type SessionEvent =
  | { type: "agent"; event: AgentEvent }
  | { type: "question"; card: QuestionCard }
  | { type: "proposal"; proposal: ProposalView }
  /** A prompt hit the turn limit: the model kept calling tools without asking or finishing. */
  | { type: "limit"; turns: number };

export interface SessionOptions {
  model: Model<"openai-completions">;
  api: ToolApi;
  scenarioId: number;
  systemPrompt?: string;
  /** Model calls one prompt may make; default 12. Nothing in an interview step needs more. */
  maxTurnsPerPrompt?: number;
  /** Replaces Pi's model call; tests pass the faux provider's. */
  streamFn?: StreamFn;
}

export class BuilderSession {
  readonly agent: Agent;
  private readonly listeners = new Set<(event: SessionEvent) => void>();
  private readonly turn = { asked: false };
  private readonly said: string[] = [];
  private turns = 0;

  constructor(options: SessionOptions) {
    const maxTurns = options.maxTurnsPerPrompt ?? 12;
    const tools = createTools({
      api: options.api,
      scenarioId: options.scenarioId,
      turn: this.turn,
      said: this.said,
      onQuestion: card => this.emit({ type: "question", card }),
      onProposal: proposal => this.emit({ type: "proposal", proposal }),
    });
    this.agent = new Agent({
      initialState: {
        systemPrompt: options.systemPrompt ?? systemPrompt(),
        model: options.model,
        tools,
        thinkingLevel: "off",
      },
      // The API's pass-through adds the real key; the model server never sees this one.
      getApiKey: () => "unused",
      // One call at a time keeps the transcript in the order the model asked.
      toolExecution: "sequential",
      // The window of the model in use now: the designer may switch models mid-conversation.
      transformContext: async messages => prune(messages, this.agent.state.model.contextWindow),
      finishTurn: turn => {
        // A question shown is the end of the turn: the designer answers next.
        if (turn.toolResults.some(r => r.toolName === "ask_user" && !r.isError)) return { action: "end" };
        // A model that keeps calling tools without asking or finishing is stopped here, not left to loop.
        if (++this.turns >= maxTurns) {
          this.emit({ type: "limit", turns: this.turns });
          return { action: "end" };
        }
        return undefined;
      },
      streamFn: options.streamFn ?? ((model, context, opts) => streamSimple(model as Model<"openai-completions">, context, opts)),
    });
    this.agent.subscribe(event => this.emit({ type: "agent", event }));
  }

  subscribe(listener: (event: SessionEvent) => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  /**
   * Sends a message and resolves when the agent's turn is over. Refused while a turn is running,
   * before anything is reset, so the running turn's one-question rule still holds.
   */
  async prompt(text: string): Promise<void> {
    if (this.agent.state.isStreaming) throw new Error("The agent is still answering. Wait for it to finish, then try again.");
    this.turn.asked = false;
    this.turns = 0;
    this.said.push(text);
    await this.agent.prompt(text);
  }

  /** The designer answered a card; the UI has already recorded it. */
  answered(questionId: string, value: unknown): Promise<void> {
    return this.prompt(`[answered ${questionId}: ${JSON.stringify(value)}]`);
  }

  /** The designer pressed "You decide" on a card. */
  delegated(questionId: string): Promise<void> {
    return this.prompt(`[delegated ${questionId}]`);
  }

  /** The owner approved a stage's gate. */
  approved(stage: number): Promise<void> {
    return this.prompt(`[approved stage ${stage}]`);
  }

  abort(): void {
    this.agent.abort();
  }

  private emit(event: SessionEvent): void {
    for (const listener of this.listeners) listener(event);
  }
}
