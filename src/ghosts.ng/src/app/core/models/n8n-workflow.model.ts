export interface N8nWorkflow {
  id: string | number;
  name: string;
  active: boolean;
  createdAt?: string;
  updatedAt?: string;
  description?: string;
  isRunning?: boolean;
  webhookUrl?: string;
  n8nUrl?: string;
  url?: string;
  apiUrl?: string;
  parameters?: WorkflowParameters;
  nodes?: WorkflowNode[];
  triggerType?: WorkflowTriggerType;
}

export interface WorkflowParameters {
  path?: string;
  [key: string]: unknown;
}

export interface WorkflowNode {
  id?: string | number;
  name?: string;
  type?: string;
  parameters?: WorkflowParameters;
}

export interface N8nWorkflowListResponse {
  count?: number;
  data: N8nWorkflow[];
}

export interface N8nWorkflowRunResponse {
  success: boolean;
  executionId?: string | number;
  startedAt?: string;
}

export interface WorkflowControl {
  id: string;
  webhookUrl?: string;
  schedule?: string;
}

export type WorkflowTriggerType = 'webhook' | 'chat' | 'form';
