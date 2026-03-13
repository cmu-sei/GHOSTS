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
