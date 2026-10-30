// The Scenario Builder API's shapes, as it serializes them (camelCase, enums as strings).
// agent/src/core/api.ts mirrors the same ones for the agent.

export type AnswerType = 'text' | 'choice' | 'list';
export type Who = 'stated' | 'proposed';
export type Via = 'person' | 'agent';
export type AnswerValue = string | string[];

export interface KindSummary {
  id: string;
  version: string;
  name: string;
}

export interface KindQuestion {
  id: string;
  /** A short name for lists, such as "Time slot". */
  label?: string;
  text: string;
  why: string;
  recommended?: string;
  answer: AnswerType;
  options?: string[];
  path: string;
}

export interface KindStage {
  number: number;
  title: string;
  questions: KindQuestion[];
  gate: { approval?: string };
}

export interface Kind extends KindSummary {
  stages: KindStage[];
  drafts: { field: string; dueAfterStage: number; path: string }[];
}

export interface ScenarioSummary {
  id: number;
  slug: string;
  name: string;
  kindId: string;
  kindVersion: string;
  owner: string;
  stage: number;
  created: string;
  updated: string;
  /** Hidden from the list unless Show archived is on. */
  archived: boolean;
}

export type SourceKind = 'text' | 'url' | 'file';

/** Context the person gave for a scenario: pasted text, a fetched page, or a document's text. */
export interface SourceView {
  id: number;
  kind: SourceKind;
  name: string;
  /** The URL or the file's name; none for pasted text. */
  origin?: string;
  characters: number;
  addedBy: string;
  added: string;
}

export interface QuestionView {
  id: string;
  stage: number;
  text: string;
  why: string;
  recommended?: string;
  answer: AnswerType;
  options?: string[];
}

export interface AnsweredView {
  id: string;
  stage: number;
  value: AnswerValue;
  who: Who;
  by: string;
  via: Via;
  reason?: string;
  time: string;
}

export interface Finding {
  severity: 'stop' | 'warning';
  code: string;
  message: string;
  /** The stage it is about: the open one, or a closed one whose approval went stale. */
  stage: number;
}

export interface Gate {
  passes: boolean;
  approval?: string;
  approved: boolean;
  findings: Finding[];
}

export interface Progress {
  kindId: string;
  stage: number;
  stageTitle: string;
  lastStage: boolean;
  gate: Gate;
  open: QuestionView[];
  answered: AnsweredView[];
  draftsDue: string[];
  /** The owner handed every remaining decision to the agent, gates included. */
  delegated: boolean;
}

export interface ProposalView {
  id: number;
  questionId: string;
  before: unknown;
  after: unknown;
  reason: string;
  proposedBy: string;
  status: 'pending' | 'applied' | 'discarded';
}

export interface LogEntry {
  time: string;
  type: 'answer' | 'draft' | 'proposal' | 'approval';
  target: string;
  before?: unknown;
  after?: unknown;
  by: string;
  via: Via;
  reason?: string;
}

export interface ApiError {
  code: string;
  message: string;
}

export type Audience = 'participants' | 'facilitators' | 'designers';
export type TemplateFormat = 'html' | 'markdown';

/** A document a scenario can be made into: one template of a published library version. */
export interface DocumentView {
  library: string;
  libraryName: string;
  version: number;
  template: string;
  name: string;
  audience: Audience;
  format: TemplateFormat;
}

export interface Me {
  user: string;
  admin: boolean;
}

/** One document's Liquid source in a library. */
export interface TemplateSpec {
  id: string;
  name: string;
  audience: Audience;
  format: TemplateFormat;
  body: string;
}

export interface LibrarySummary {
  id: number;
  slug: string;
  name: string;
  kinds: string[];
  latestVersion?: number;
  draftChanged: boolean;
}

export interface VersionView {
  number: number;
  publishedBy: string;
  published: string;
}

export interface LibraryDetail extends LibrarySummary {
  description?: string;
  templates: TemplateSpec[];
  versions: VersionView[];
}

/** What the library page saves: the whole draft. */
export interface LibraryDraft {
  name: string;
  description?: string;
  kinds: string[];
  templates: TemplateSpec[];
}

export interface PreviewRequest {
  scenarioId: number;
  libraryId: number;
  name: string;
  audience: Audience;
  format: TemplateFormat;
  body: string;
}

/** An app a scenario can be sent to. Send waits until an admin connects it by its API's address. */
export interface PluginView {
  id: string;
  name: string;
  description: string;
  connected: boolean;
  url?: string;
  connectedBy?: string;
  since?: string;
}
