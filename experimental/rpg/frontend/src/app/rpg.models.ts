// Mirrors the FastAPI Frame/Beat/AAR/HUD payloads for Kriegspiel Mode.

export interface Beat {
  turn: number;
  speaker: string; // Judge | Signals | Control | Player
  text: string;
}

export interface Objective {
  id: number;
  name: string;
  status: string; // Active | Achieved | Failed
  met: boolean;
}

export interface Hud {
  scenario: string;
  role: string;
  mandate: string;
  objectives: Objective[];
  indicators: string[];
  turn: number;
  minutesLeft: number;
  windowMinutes: number;
  clockLabel: string;
  opforName: string;
  opforProgress: number;
  opforThreshold: number;
  threats: string[];
  // Present only when fog is 'off' (training/debug).
  flags?: string[];
  facts?: Record<string, string>;
}

export interface Aar {
  outcome: string; // WIN | LOSS | INCOMPLETE
  grade: string;
  score: number;
  objectives_met: number;
  objectives_total: number;
  minutes_spent: number;
  window_minutes: number;
  opfor_progress: number;
  opfor_threshold: number;
  highlights: string[];
}

export interface Frame {
  beats: Beat[];
  awaiting_player: boolean;
  band: string | null; // odds band the judge assigned this turn
  critique: string;
  tier: string | null; // resolved outcome tier
  notices: string[];
  is_complete: boolean;
  aar: Aar | null;
  hud: Hud;
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
  objectives: number;
}

export interface FixtureList {
  fixtures: FixtureSummary[];
}
