// The Scenario Builder API's shapes, as it serializes them (camelCase, enums as strings).
// agent/src/core/api.ts mirrors the same ones for the agent.

export type AnswerType = 'text' | 'choice' | 'list';
export type Who = 'stated' | 'proposed';
export type Via = 'person' | 'agent';
export type AnswerValue = string | string[];

export interface ScenarioTypeSummary {
  id: string;
  version: string;
  name: string;
}

export interface ScenarioTypeQuestion {
  id: string;
  /** A short name for lists, such as "Time slot". */
  label?: string;
  text: string;
  why: string;
  recommended?: string;
  answer: AnswerType;
  options?: string[];
  path: string;
  idPrefix?: string;
  visibility?: string;
  default?: string;
}

export interface ScenarioTypeStage {
  number: number;
  title: string;
  questions: ScenarioTypeQuestion[];
  /** What the stage settles; the model drafts its questions from it. */
  purpose?: string;
}

export interface ScenarioType extends ScenarioTypeSummary {
  stages: ScenarioTypeStage[];
  drafts: { field: string; dueAfterStage: number; path: string; visibility?: string }[];
  /** Its scenarios have a Topology page, where the range is built and deployed. */
  topology?: boolean;
  /** What kind of exercise it is. */
  description?: string;
}

/**
 * What updating a scenario to the latest version of its scenario type would do. From and to are the same when it's
 * already on the latest. Only the owner can apply it.
 */
export interface ScenarioTypeUpdatePlan {
  from: string;
  to: string;
  carried: { id: string; label: string; fromStage: number; toStage: number }[];
  dropped: { id: string; label: string; why: string }[];
  added: { id: string; label: string; stage: number }[];
  draftsDropped: string[];
  stageFrom: number;
  stageTo: number;
  owner: string;
}

/** A scenario type in Admin: its latest published version, if any, and how many scenarios are on it. */
export interface ScenarioTypeAdminSummary {
  id: string;
  name: string;
  latestVersion?: string;
  draftChanged: boolean;
  scenarios: number;
  retired: boolean;
}

export interface ScenarioTypeVersionView {
  version: string;
  publishedBy: string;
  published: string;
  scenarios: number;
}

/** A scenario type's draft, which admins edit, and what's published of it. */
export interface ScenarioTypeDetail {
  id: string;
  scenarioType: ScenarioType;
  basedOn?: string;
  retired: boolean;
  latestVersion?: string;
  draftChanged: boolean;
  scenarios: number;
  versions: ScenarioTypeVersionView[];
}

export interface ScenarioSummary {
  id: number;
  slug: string;
  name: string;
  scenarioTypeId: string;
  scenarioTypeVersion: string;
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
  /** The stage it is about. */
  stage: number;
}

export interface Gate {
  passes: boolean;
  findings: Finding[];
}

export interface Progress {
  scenarioTypeId: string;
  /** The scenario type version the scenario was made on, which it runs on. */
  scenarioTypeVersion: string;
  stage: number;
  stageTitle: string;
  lastStage: boolean;
  gate: Gate;
  open: QuestionView[];
  answered: AnsweredView[];
  draftsDue: string[];
  /** The owner handed every remaining decision to the agent. */
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
  type: 'answer' | 'draft' | 'proposal' | 'delegation' | 'update';
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
  scenarioTypes: string[];
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
  scenarioTypes: string[];
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
