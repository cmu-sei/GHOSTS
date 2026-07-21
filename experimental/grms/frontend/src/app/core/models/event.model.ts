export interface EventStructured {
  actor: string;
  target: string;
  action_category: string;
  reversibility: string;
  visibility: string;
}

export interface GeopoliticalEvent {
  id?: string;
  timestamp?: string;
  event_type: string;
  severity: number;
  description: string;
  structured: EventStructured;
  scenario_id?: string;
  execution_id?: number;
  preceding_events?: string[];
}

export const EVENT_TYPES = ['military_action', 'diplomatic', 'economic', 'cyber', 'information'] as const;
export const ACTION_CATEGORIES = ['military', 'diplomatic', 'economic', 'cyber', 'information'] as const;
export const REVERSIBILITY_OPTIONS = ['irreversible', 'escalatory', 'reversible'] as const;
export const VISIBILITY_OPTIONS = ['public', 'leaked', 'covert'] as const;
