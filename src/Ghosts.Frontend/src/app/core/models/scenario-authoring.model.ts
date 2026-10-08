// Scenario Authoring Models — the conversation between the developer and the authoring agent.
// Mirrors Ghosts.Api.Infrastructure.Services.ScenarioAuthoringService's records and
// ScenarioAuthoringController's responses (api/scenario-authoring).

export interface AuthoringSession {
  id: string;
  model: string;
  /** The model's effort level for the whole session (H3), or null for the model's default. */
  effort: string | null;
  scenarioId: number | null;
}

/** A model a new session may use (GET api/scenario-authoring/models). */
export interface AuthoringModelChoice {
  name: string;
  id: string;
  /** The effort levels the model takes; empty when it takes none. */
  efforts: string[];
}

export interface AuthoringSessionSummary {
  id: string;
  model: string;
  turns: number;
  importedScenarioId: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface AuthoringToolCall {
  name: string;
  ok: boolean;
}

export interface AuthoringDocumentStatus {
  hash: string;
  turn: number;
  errors: number;
  warnings: number;
  shown: boolean;
  importedScenarioId: number | null;
}

/** Cause is "model error", "timeout" or "empty reply" (B5). */
export interface AuthoringFailure {
  cause: string;
  notice: string;
  details: string;
}

/**
 * One turn's result. On a failure, reply is empty and failure names the cause; the gate report still
 * lists what the turn did. statusLine, attention, changes and gaps are the server's own lines (B2, B3,
 * D2, E5) — never the model's prose. canImport says the import rule holds; importedBefore says the
 * import will ask first (A4).
 */
export interface AuthoringTurnResult {
  turn: number;
  reply: string;
  gateReport: string;
  toolCalls: AuthoringToolCall[];
  validations: AuthoringDocumentStatus[];
  latestDocument: AuthoringDocumentStatus | null;
  canImport: boolean;
  importedBefore: boolean;
  failure: AuthoringFailure | null;
  /** The server's status line for this turn (B2); the record's property is "status". */
  status: string | null;
  attention: string[] | null;
  changes: string[] | null;
  gaps: string[] | null;
  model: string | null;
}

export interface AuthoringTurnRecord {
  turn: number;
  message: string;
  startedAt: string;
  endedAt: string | null;
  interrupted: boolean;
  result: AuthoringTurnResult | null;
}

export interface AuthoringDocumentSummary {
  turn: number;
  hash: string;
  baseHash: string | null;
  errors: number;
  warnings: number;
  dryRun: boolean;
  shown: boolean;
  importedScenarioId: number | null;
  createdAt: string;
}

export interface AuthoringMessageSummary {
  turn: number;
  sequence: number;
  role: string;
  inHistory: boolean;
  inputTokens: number | null;
  outputTokens: number | null;
  cacheReadTokens: number | null;
  cacheWriteTokens: number | null;
  stopReason: string | null;
  error: string | null;
  startedAt: string | null;
  endedAt: string | null;
}

export interface AuthoringTokenTotals {
  modelCalls: number;
  input: number;
  output: number;
  cacheRead: number;
  cacheWrite: number;
  /** What the session cost so far at the model's configured list prices; null when none are configured. */
  estimatedCostUsd: number | null;
}

/** One of ELICITATION.md's fourteen questions: stated or proposed per the ledger, drafted when only the document answers it, else open. */
export interface AuthoringReadinessQuestion {
  tag: string;
  group: string;
  label: string;
  question: string;
  status: 'stated' | 'proposed' | 'drafted' | 'open';
  value: string | null;
}

/** A finding in the prompt's pile: mechanical, judgment (asks names the question it implies), or accepted. */
export interface AuthoringReadinessFinding {
  severity: 'error' | 'warning' | 'info';
  code: string;
  path: string;
  kind: 'mechanical' | 'judgment' | 'accepted';
  asks: string | null;
}

/** The dashboard beside the conversation, computed by the server from the latest document and reply. */
export interface AuthoringReadiness {
  /** How many documents the session has validated. */
  draft: number;
  groups: { key: string; label: string }[];
  questions: AuthoringReadinessQuestion[];
  answered: number;
  open: number;
  ledger: { stated: number; proposed: number; open: number };
  findings: { errors: number; warnings: number; info: number; items: AuthoringReadinessFinding[] };
  coverage: { label: string; count: number }[];
  clock: { duration: string | null; pacing: string | null; deadline: string | null; events: string | null };
  sources: { name: string; cited: number }[];
  gate: { ready: boolean; steps: { label: string; done: boolean }[] };
}

export interface AuthoringSessionRecord {
  id: string;
  model: string;
  effort: string | null;
  status: string;
  scenarioId: number | null;
  importedScenarioId: number | null;
  createdAt: string;
  updatedAt: string;
  running: boolean;
  turns: AuthoringTurnRecord[];
  latestDocument: AuthoringDocumentStatus | null;
  canImport: boolean;
  importedBefore: boolean;
  statusLine: string;
  gaps: string[];
  documents: AuthoringDocumentSummary[];
  messages: AuthoringMessageSummary[];
  tokens: AuthoringTokenTotals;
  readiness: AuthoringReadiness;
}

export interface AuthoringScenarioFinding {
  tier: number;
  severity: string;
  code: string;
  path: string;
  message: string;
  hint: string | null;
}

export interface AuthoringDocumentDetail {
  turn: number;
  hash: string;
  baseHash: string | null;
  errors: number;
  warnings: number;
  shown: boolean;
  importedScenarioId: number | null;
  findings: AuthoringScenarioFinding[];
  changes: string[];
  gaps: string[];
  document: string;
}

export interface AuthoringChunk {
  chunkId: number;
  sourceId: number;
  source: string | null;
  index: number;
  text: string;
}

export interface StartAuthoringSessionRequest {
  scenarioId?: number | null;
  model?: string;
  effort?: string | null;
}

export interface AuthoringTurnRequest {
  message: string;
}

export interface AuthoringImportRequest {
  hash: string;
  again?: boolean;
  replace?: boolean;
}

export interface AuthoringImportResult {
  imported: boolean;
  scenarioId: number | null;
  hash: string;
  reason: string | null;
  needsConfirmation: boolean;
  confirm: string | null;
  report: string;
}

/** Pushed over the Scenario Builder's hub ("authoringProgress") while a turn runs (section 10). */
export interface AuthoringProgressEvent {
  sessionId: string;
  turn: number | null;
  kind: 'turn-started' | 'model-reply' | 'tool-call' | 'turn-ended';
  detail: Record<string, unknown> | null;
  at: string;
}
