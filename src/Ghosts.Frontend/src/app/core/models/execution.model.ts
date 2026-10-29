export interface Execution {
  id: number;
  scenarioId: number;
  scenarioName: string;
  name: string;
  description: string;
  status: ExecutionStatus;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  parameterOverrides: string;
  configuration: string;
  metrics: string;
  errorDetails: string;
}

export type ExecutionStatus =
  | 'Created'
  | 'Running'
  | 'Paused'
  | 'Completed'
  | 'Failed'
  | 'Cancelled';

export interface ExecutionSummary {
  id: number;
  name: string;
  status: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  durationSeconds?: number;
  scenarioName: string;
  eventCount: number;
  snapshotCount: number;
}

export interface ExecutionEvent {
  id: number;
  timestamp: string;
  eventType: string;
  description: string;
  data: string;
  severity: string;
}

export interface ExecutionMetricSnapshot {
  id: number;
  timestamp: string;
  elapsedSeconds: number;
  metrics: string;
}

export interface CreateExecutionRequest {
  scenarioId: number;
  name?: string;
  description?: string;
  parameterOverrides?: string;
  configuration?: string;
}

export interface UpdateExecutionRequest {
  name?: string;
  description?: string;
  parameterOverrides?: string;
  configuration?: string;
}

export interface CreateExecutionEventRequest {
  eventType: string;
  description: string;
  data?: string;
  severity?: string;
}

export type TriggerKind = 'PointInTime' | 'Scheduled' | 'Triggered';

export interface ExecutionTimelineItem {
  id: number;
  executionId: number;
  sourceTimelineEventId?: number;
  time: string;
  number: number;
  assigned: string;
  description: string;
  status: TimelineItemStatus;
  automationKind: 'Manual' | 'Workflow' | 'MachineUpdate';
  workflowId?: string;
  completedBy?: string;
  notes?: string;
  resultData: string;
  createdAt: string;
  completedAt?: string;
  triggerKind: TriggerKind;
  schedule?: string;
  triggerCondition?: string;
  lastFiredAt?: string;
  fireCount: number;
  extras?: string;
}

export type TimelineItemStatus =
  | 'Pending'
  | 'Ready'
  | 'Queued'
  | 'Deployed'
  | 'Completed'
  | 'Failed'
  | 'Skipped';

export interface CompleteTimelineItemRequest {
  status: 'Completed' | 'Failed' | 'Skipped';
  notes?: string;
  completedBy?: string;
}

/** One answer a decision point offers, from the item's extras. */
export interface DecisionChoice {
  id: string;
  label?: string;
}

export interface ObjectiveStatus {
  id: number;
  name: string;
  metWhen?: string;
  met: boolean;
  metAt?: string;
}

/** White Cell's judgment of whether participants gave an item's expected response. */
export interface ResponseMark {
  itemId: number;
  itemNumber: number;
  expected: string;
  met: boolean;
  notes?: string;
  by: string;
  at: string;
}

export interface RecordedDecision {
  itemId: number;
  itemNumber: number;
  choiceId: string;
  label: string;
  by: string;
  at: string;
}

/** A run's ground truth: the exercise clock, flags and facts, objectives and decisions. */
export interface ExecutionState {
  executionId: number;
  clockMinutes: number;
  flags: string[];
  facts: Record<string, string>;
  objectives: ObjectiveStatus[];
  decisions: RecordedDecision[];
  responses: ResponseMark[];
}

export interface SetFlagsRequest {
  set: string[];
  clear: string[];
  by: string;
}

export interface RecordDecisionRequest {
  choiceId: string;
  by: string;
}

export interface MarkResponseRequest {
  met: boolean;
  by: string;
}

/** Which host an NPC sits at; machineId is set once a client with that host name registers. */
export interface NpcBinding {
  npcId: string;
  name: string;
  team: string;
  hostName?: string;
  machineId?: string;
}

export interface BindingResult {
  bound: number;
  unbound: number;
  hosts: number;
}

/** A host GraySpace built. hostname is what it (and a GHOSTS client on it) reports; labels carry ghosts.pool and ghosts.console. */
export interface TerrainManifestHost {
  name: string;
  hostname: string;
  site?: string;
  segment?: string;
  ip: string;
  publicIp?: string;
  mgmtIp?: string;
  fqdns: string[];
  roles: string[];
  labels: Record<string, string>;
  image: string;
  status: string;
}

export interface TerrainManifestSite {
  name: string;
  router: string;
  isp: string;
  location?: string;
  wanIp: string;
  edge: string;
  segments: { name: string; cidr: string; gateway: string }[];
}

export interface TerrainManifest {
  sites: TerrainManifestSite[];
  hosts: TerrainManifestHost[];
  dns: { name: string; type: string; data: string }[];
  certificates: { host: string; names: string[]; ca: string }[];
}

/** A problem GraySpace found in the enclave; an error blocks the build. */
export interface TerrainFinding {
  code: string;
  severity: 'error' | 'warning';
  path: string;
  message: string;
}

/** The enclave GraySpace builds for a run (Terrain API 1.3.0). baseId is the deployed base version. */
export interface ExecutionTerrain {
  executionId: number;
  provider?: string;
  baseId?: string;
  enclave?: string;
  version?: string;
  instanceId?: string;
  jobId?: string;
  stage?: string;
  status?: string;
  progress: number;
  log: string[];
  manifest?: TerrainManifest;
  findings?: TerrainFinding[];
  notes?: string[];
  error?: string;
  updatedAt?: string;
}

/** GraySpace's map layer: routers, links, and enclave sites, internet hosts and attachments, each with a stable id and a kind. */
export interface TerrainMapLayer {
  type: 'FeatureCollection';
  features: { type: 'Feature'; id: string; geometry: any; properties: Record<string, any> }[];
}

/** A GraySpace /api/v1/events message, relayed by GHOSTS as TerrainEvent. */
export interface TerrainEvent {
  type: string;
  id?: string;
  kind?: string;
  target?: string;
  state?: string;
  hostname?: string;
  status?: string;
  properties?: Record<string, unknown>;
  timestamp?: string;
}
