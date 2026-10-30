// The browser bundle's public types. build.mjs copies this file out as dist/web/agent.d.ts, so
// it imports nothing; index.ts checks at compile time that the core's types still fit it.

export interface ModelInfo {
  id: string;
  contextWindow: number;
}

export interface QuestionCard {
  questionId: string;
  question: string;
  recommended?: { answer: string; why: string };
  alternatives: string[];
  answer: "text" | "choice" | "list";
  options?: string[];
}

export interface ProposalView {
  id: number;
  questionId: string;
  before: unknown;
  after: unknown;
  reason: string;
  proposedBy: string;
  status: "pending" | "applied" | "discarded";
}

export interface WebAgentOptions {
  /** The builder's origin; /api is added. Model calls go through its /api/model pass-through. */
  apiUrl: string;
  scenarioId: number;
  /** A model id from the API's list; the first one when absent or unknown. */
  model?: string;
}

export type WebAgentEvent =
  /** The text of one assistant message so far; `final` once it is complete. */
  | { type: "text"; id: string; text: string; final: boolean }
  /** A tool ran. `error` is its refusal or failure, said to the model as is. */
  | { type: "tool"; name: string; summary: string; error?: string }
  | { type: "question"; card: QuestionCard }
  | { type: "proposal"; proposal: ProposalView }
  | { type: "busy"; busy: boolean }
  /** The model call failed: the server is down, the model is unknown, the context is too long. */
  | { type: "error"; message: string };

export interface WebAgent {
  readonly models: ModelInfo[];
  readonly model: ModelInfo;
  setModel(id: string): void;
  /** What the designer typed. Resolves when the agent's turn is over. */
  send(text: string): Promise<void>;
  /** The designer answered a card, which the page has already recorded. */
  answered(questionId: string, value: unknown): Promise<void>;
  delegated(questionId: string): Promise<void>;
  approved(stage: number): Promise<void>;
  abort(): void;
  subscribe(listener: (event: WebAgentEvent) => void): () => void;
}

export type CreateWebAgent = (options: WebAgentOptions) => Promise<WebAgent>;
