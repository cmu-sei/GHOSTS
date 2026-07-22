// Mirrors the FastAPI Frame/Beat/AAR/HUD payloads (snake_case as the API returns).

export interface Beat {
  step_number: number;
  cell: string;
  time: string;
  text: string;
}

export interface Objective {
  id: number;
  name: string;
  met: boolean;
}

export interface Hud {
  scenario: string;
  step: number | null;
  time: string | null;
  role: string;
  objectives: Objective[];
  flags: string[];
  knowledge: string[];
  umpireFindings: string[];
  assumptions: string[];
  constraints: string[];
  threats: string[];
  openTasks: number;
  shownTasks: number;
  queuedTasks: number;
  minutesLeft: number;
  lunchMinutes: number;
  windowLabel: string;
  containmentFuseMinutes: number | null;
  containmentFuseMinutesLeft: number | null;
  containmentContained: boolean;
  deadlineLabel: string;
}

export interface TaskAction {
  label: string;
  intent: string; // investigate | contain | notify | wait
}

export interface Task {
  step: number;
  time: string;
  prompt: string;
  actions: TaskAction[];
}

export interface Aar {
  outcome: string; // WIN | LOSS | INCOMPLETE
  grade: string;
  score: number;
  objectives_met: number;
  objectives_total: number;
  minutes_spent: number;
  lunch_minutes: number;
  made_lunch: boolean;
  highlights: string[];
}

export interface Frame {
  beats: Beat[];
  tasks: Task[];
  awaiting_player: boolean;
  notices: string[];
  is_complete: boolean;
  aar: Aar | null;
  hud: Hud;
  can_table: boolean;
}

export interface GameResponse {
  gameId: string;
  frame: Frame;
}

export interface FixtureSummary {
  fixture: string;
  sortOrder: number;
  name: string;
  description: string;
  era: string;
  theater: string;
  estimatedMinutes: number;
  events: number;
  objectives: number;
}

export interface FixtureList {
  fixtures: FixtureSummary[];
}
