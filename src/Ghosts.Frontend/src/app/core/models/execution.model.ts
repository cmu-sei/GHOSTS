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

export interface TerrainManifestHost {
  name: string;
  segment: string;
  rangeIp: string;
  managementIp: string;
  osFamily: string;
  osVersion: string;
  role: string;
  proxmoxNode: string;
  vmid: number;
  ghostsClient: boolean;
  participantWorkstation: boolean;
}

export interface TerrainSite {
  name: string;
  kind: string;
  url: string;
}

export interface TerrainManifest {
  instanceId: string;
  baseId: string;
  enclave: string;
  directoryDomain: string;
  hosts: TerrainManifestHost[];
  sites: TerrainSite[];
}

/** The enclave the terrain provider (GraySpace) builds for a run. */
export interface ExecutionTerrain {
  executionId: number;
  provider?: string;
  baseId?: string;
  enclave?: string;
  instanceId?: string;
  status?: string;
  progress: number;
  log: string[];
  manifest?: TerrainManifest;
  error?: string;
  updatedAt?: string;
}
