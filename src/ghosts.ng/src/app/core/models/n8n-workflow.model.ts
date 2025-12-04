export interface N8nWorkflow {
  id: string | number;
  name: string;
  active: boolean;
  createdAt?: string;
  updatedAt?: string;
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
