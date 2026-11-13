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
  extras?: { metWhen?: string } | null; // what the scenario document says no column holds
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
  extras?: { metWhen?: string } | null;
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
  extras?: { metWhen?: string } | null;
}

export const OBJECTIVE_TYPES: { value: ObjectiveType; label: string; description: string }[] = [
  { value: 'MET', label: 'MET', description: 'Mission Essential Task' },
  { value: 'JMET', label: 'JMET', description: 'Joint Mission Essential Task' },
  { value: 'Rehearsal', label: 'Rehearsal', description: 'Mission rehearsal or dry run' },
  { value: 'Onboarding', label: 'Onboarding', description: 'New operator onboarding' },
  { value: 'ToolTraining', label: 'Tool Training', description: 'Learning a new tool or platform' }
];

export const OBJECTIVE_STATUSES: { value: ObjectiveStatus; label: string; color: string }[] = [
  { value: 'Draft', label: 'Draft', color: 'var(--gh-muted)' },
  { value: 'Active', label: 'Active', color: 'var(--gh-accent)' },
  { value: 'Achieved', label: 'Achieved', color: 'var(--gh-ok)' },
  { value: 'PartiallyMet', label: 'Partially Met', color: 'var(--gh-warn)' },
  { value: 'NotMet', label: 'Not Met', color: 'var(--gh-bad)' }
];

export const TASK_SCORES: { value: TaskScore; label: string; color: string }[] = [
  { value: 'T', label: 'Trained', color: 'var(--gh-ok)' },
  { value: 'P', label: 'Practiced', color: 'var(--gh-warn)' },
  { value: 'U', label: 'Untrained', color: 'var(--gh-bad)' }
];
