// Scenario Authoring Models — the conversation between the developer and the authoring agent.
// Mirrors Ghosts.Api.Infrastructure.Services.ScenarioAuthoringService's records and
// ScenarioAuthoringController's responses (api/scenario-authoring).

export interface AuthoringSession {
  id: string;
  model: string;
  scenarioId: number | null;
}

/** A model a new session may use (GET api/scenario-authoring/models). */
export interface AuthoringModelChoice {
  name: string;
  id: string;
}

export interface AuthoringModels {
  model: string;
  models: AuthoringModelChoice[];
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
 * D2, E5) — never the model's prose.
 */
export interface AuthoringTurnResult {
  turn: number;
  reply: string;
  gateReport: string;
  toolCalls: AuthoringToolCall[];
  validations: AuthoringDocumentStatus[];
  latestDocument: AuthoringDocumentStatus | null;
  canImport: boolean;
  failure: AuthoringFailure | null;
  statusLine: string | null;
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
}

export interface AuthoringSessionRecord {
  id: string;
  model: string;
  status: string;
  scenarioId: number | null;
  importedScenarioId: number | null;
  createdAt: string;
  updatedAt: string;
  running: boolean;
  turns: AuthoringTurnRecord[];
  latestDocument: AuthoringDocumentStatus | null;
  canImport: boolean;
  statusLine: string;
  gaps: string[];
  documents: AuthoringDocumentSummary[];
  messages: AuthoringMessageSummary[];
  tokens: AuthoringTokenTotals;
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
