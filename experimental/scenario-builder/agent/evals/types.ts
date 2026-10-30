// What a run records, and what the checks read: the evaluation's shared shapes.

export interface Fixture {
  kind: string;
  /** The designer's first message, which answers the openingQuestions in plain prose. */
  opening: string;
  /** Each question the opening answers, with fragments its recorded value must contain. */
  openingQuestions: Record<string, string[]>;
  /** The answer the scripted person gives on each question's card. */
  answers: Record<string, string | string[]>;
  /** Questions the person answers with "You decide". */
  delegate: string[];
  summary: { required: string[]; banned: string[] };
}

export interface Call {
  name: string;
  args: any;
  isError: boolean;
  /** A refusal's code, from the tool result's details. */
  code?: string;
  /** Which prompt it happened in. */
  prompt: number;
}

export interface PromptRecord {
  index: number;
  text: string;
  startedAt: number;
  endedAt: number;
  /** Milliseconds from the prompt to the model's first streamed update. */
  firstTokenMs?: number;
}

export interface Metrics {
  prompts: number;
  wallMs: number;
  inputTokens: number;
  outputTokens: number;
  medianFirstTokenMs: number | null;
}

export interface Observed {
  model: string;
  scenarioId: number;
  calls: Call[];
  /** The text of each finished assistant message, with its prompt. */
  replies: { prompt: number; text: string }[];
  prompts: PromptRecord[];
  /** Three prompts in a row with no question, approval or end. */
  stalled: boolean;
  /** The run hit maxPrompts. */
  stoppedAtLimit: boolean;
  /** Prompts that hit the session's turn limit: the model kept calling tools without asking or finishing. */
  limitHits: number[];
  /** A model or API failure that ended the run. */
  error?: string;
  /** The pulled scenario document at the end. */
  doc: any;
  metrics: Metrics;
}

export interface CheckResult {
  id: number;
  name: string;
  pass: boolean;
  detail: string;
}
