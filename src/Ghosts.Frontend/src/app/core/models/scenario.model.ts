export interface Scenario {
  id: number;
  name: string;
  description: string;
  createdAt: Date;
  updatedAt: Date;
  scenarioParameters?: ScenarioParameters;
  technicalEnvironment?: TechnicalEnvironment;
  gameMechanics?: GameMechanics;
  timeline?: ScenarioTimeline;
  simulationMechanics?: GameMechanics; // alias for gameMechanics
  builderStatus?: string; // None, Sources, Extracted, Enriched, Compiled
  author?: string | null;
  publishedAt?: Date | null; // null: a draft, visible only to its author and not deployable
  extras?: ScenarioExtras | null;
}

/** The document a scenario was last imported from, and what an edit since changed in its rows (A5). */
export interface ApprovedScenarioDocument {
  scenarioId: number;
  approvedAt: string;
  contentHash: string;
  /** The approved document's JSON text. */
  document: string;
  edited: boolean;
  /** One line per changed path, such as "/name: Old → New"; empty when not edited. */
  changes: string[];
}

export interface ScenarioParameters {
  nations: Nation[];
  threatActors: ThreatActor[];
  injects: Inject[];
  userPools: UserPool[];
  objectives: string;
  politicalContext: string;
  rulesOfEngagement: string;
  victoryConditions: string;
  workflowBindings?: ScenarioWorkflowBinding[] | null; // absent on a new scenario: create seeds the defaults
}

/** An n8n animation workflow a run schedules, by its webhook path. */
export interface ScenarioWorkflowBinding {
  workflowRef: string;
  displayName: string;
  cron: string;
  enabled: boolean;
}

export interface Nation {
  name: string;
  alignment: string;
}

export interface ThreatActor {
  name: string;
  type: string;
  capability: number;
  ttps: string[];
  ttpsString?: string; // For editing purposes
  extras?: AdversaryExtras | null;
}

export interface Inject {
  trigger: string;
  title: string;
  extras?: EventExtras | null;
}

export interface UserPool {
  role: string;
  count: number;
  extras?: { description?: string } | null;
}

export interface TechnicalEnvironment {
  networkTopology: string;
  services: string;
  assets: string;
  platforms?: {
    websites?: string[];
    socialMedia?: string[];
    emailProviders?: string[];
    cloudServices?: string[];
    collaborationTools?: string[];
    [key: string]: string[] | undefined;
  };
  defenses: string[];
  vulnerabilities: Vulnerability[];
}

export interface Vulnerability {
  asset: string;
  cve: string;
  severity: string;
  extras?: { description?: string } | null; // when cve holds a CVE id
}

export interface GameMechanics {
  timelineType: string;
  durationHours: number;
  adjudicationType: string;
  escalationLadder: string;
  branchingLogic: string;
  telemetry: Telemetry;
  performanceMetrics: string;
}

export interface Telemetry {
  collectLogs: boolean;
  collectNetwork: boolean;
  collectEndpoint: boolean;
  collectChat: boolean;
}

export interface ScenarioTimeline {
  exerciseDuration: number;
  events: ScenarioTimelineEvent[];
}

export type TriggerKind = 'PointInTime' | 'Scheduled' | 'Triggered';

export type ExecutionType = 'manual' | 'workflow';

export interface ScenarioTimelineEvent {
  time: string;
  number: number;
  assigned: string;
  description: string;
  status: string;
  objectiveIds?: number[];
  triggerKind?: TriggerKind;
  schedule?: string;
  triggerCondition?: string;
  executionType?: ExecutionType;
  workflowId?: string;
  extras?: EventExtras | null;
}

export interface CreateScenario {
  name: string;
  description: string;
  scenarioParameters: ScenarioParameters;
  technicalEnvironment: TechnicalEnvironment;
  gameMechanics?: GameMechanics;
  simulationMechanics?: GameMechanics; // Alias for gameMechanics used in UI
  timeline: ScenarioTimeline;
  builderStatus?: string;
  extras?: ScenarioExtras | null;
}

// What a scenario document says that no column holds, kept in each row's extras in the document's own
// shape (schemas/scenario-document/v1): the key names are the document's.

export interface ScenarioExtras {
  slug?: string;
  intent?: string;
  catalog?: { listed?: boolean; sortOrder?: number; era?: string; theater?: string; estimatedMinutes?: number };
  context?: { situation?: string };
  audience?: { role?: string; size?: number; proficiency?: string; mandate?: string };
  terrain?: {
    reference?: { provider?: string; slice?: string };
    segments?: TerrainSegment[];
    hosts?: TerrainHost[];
    services?: TerrainService[];
    informationEnvironment?: { platforms?: string; audience?: string };
    defenses?: TerrainDefense[]; // names match technicalEnvironment.defenses
  };
  startingConditions?: { flags?: string[]; facts?: Record<string, string> };
  rulesOfPlay?: {
    clock?: { tickMinutes?: number; label?: string };
    deadline?: { at?: string; label?: string; decisiveAction?: string; warning?: string; failureMessage?: string };
    fog?: string;
    escalationLadder?: { rungs?: EscalationRung[] };
  };
  sources?: DocumentSource[];
  references?: DocumentReference[];
}

export interface TerrainSegment { name: string; cidr?: string; description?: string }
export interface TerrainHost { name: string; segment?: string; os?: string; role?: string; description?: string; services?: string[] }
export interface TerrainService { name: string; description?: string; hosts?: string[] }
export interface TerrainDefense { name: string; description?: string; covers?: string[] }
export interface EscalationRung { name: string; description?: string; recoverable?: boolean }
export interface DocumentSource { id: string; name: string; type?: string; uri?: string; mimeType?: string; note?: string }
export interface DocumentReference { id: string; title: string; uri?: string; locator?: string; note?: string }

export interface AdversaryExtras {
  id?: string;
  objective?: string;
  winThreshold?: number;
  playbook?: PlaybookMove[];
}

export interface PlaybookMove {
  id: string;
  domain?: string;
  description: string;
  techniques?: string[];
  preconditions?: string;
  progress?: number;
  effects?: EventEffects;
  indicators?: string[];
}

export interface EventEffects { setFlags?: string[]; setFacts?: Record<string, string> }

/** A timeline event's carries id, title, expectedResponse, effects, indicators; an inject's also the rest of the event. */
export interface EventExtras {
  id?: string;
  title?: string;
  owner?: string;
  objectives?: number[];
  schedule?: string;
  when?: string;
  execution?: { mode?: string; workflowRef?: string };
  expectedResponse?: string;
  effects?: EventEffects;
  indicators?: string[];
}

export interface ScenarioListItem {
  id: number;
  name: string;
}
