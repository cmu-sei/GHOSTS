export interface Objective {
  id: number;
  parentId: number | null;
  scenarioId: number | null;
  name: string;
  description: string;
  type: ObjectiveType;
  status: ObjectiveStatus;
  score: TaskScore;
  priority: number;
  successCriteria: string;
  assigned: string;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
  children: Objective[];
}

export type ObjectiveType = 'MET' | 'JMET' | 'Rehearsal' | 'Onboarding' | 'ToolTraining';
export type ObjectiveStatus = 'Draft' | 'Active' | 'Achieved' | 'PartiallyMet' | 'NotMet';
export type TaskScore = 'T' | 'P' | 'U';

export interface CreateObjective {
  parentId?: number | null;
  scenarioId?: number | null;
  name: string;
  description: string;
  type: string;
  priority: number;
  successCriteria: string;
  assigned: string;
}

export interface UpdateObjective {
  name: string;
  description: string;
  type: string;
  status: string;
  score: string;
  priority: number;
  successCriteria: string;
  assigned: string;
  sortOrder: number;
}

export const OBJECTIVE_TYPES: { value: ObjectiveType; label: string; description: string }[] = [
  { value: 'MET', label: 'MET', description: 'Mission Essential Task' },
  { value: 'JMET', label: 'JMET', description: 'Joint Mission Essential Task' },
  { value: 'Rehearsal', label: 'Rehearsal', description: 'Mission rehearsal or dry run' },
  { value: 'Onboarding', label: 'Onboarding', description: 'New operator onboarding' },
  { value: 'ToolTraining', label: 'Tool Training', description: 'Learning a new tool or platform' }
];

export const OBJECTIVE_STATUSES: { value: ObjectiveStatus; label: string; color: string }[] = [
  { value: 'Draft', label: 'Draft', color: '#9e9e9e' },
  { value: 'Active', label: 'Active', color: '#1976d2' },
  { value: 'Achieved', label: 'Achieved', color: '#2e7d32' },
  { value: 'PartiallyMet', label: 'Partially Met', color: '#f57c00' },
  { value: 'NotMet', label: 'Not Met', color: '#c62828' }
];

export const TASK_SCORES: { value: TaskScore; label: string; color: string }[] = [
  { value: 'T', label: 'Trained', color: '#2e7d32' },
  { value: 'P', label: 'Practiced', color: '#f57c00' },
  { value: 'U', label: 'Untrained', color: '#c62828' }
];
