export interface VerbalResponse {
  statement: string;
  tone: string;
  audience: string;
}

export interface LeaderAction {
  action_type: string;
  description: string;
  timeline: string;
  likelihood: number;
  reversibility: string;
}

export interface ResponseReasoning {
  personality_factors: string[];
  historical_precedents: string[];
  contextual_drivers: string[];
  constraints: string[];
  confidence: number;
}

export interface LeaderResponse {
  event_id: string;
  leader_id: string;
  timestamp: string;
  verbal_response: VerbalResponse;
  actions: LeaderAction[];
  reasoning: ResponseReasoning;
  escalation_risk: number;
  de_escalation_openings: string[];
}

export interface PopulationOverall {
  support_leader: number;
  oppose_leader: number;
  neutral: number;
  protest_likelihood: number;
  compliance_likelihood: number;
}

export interface SegmentResponse {
  segment_name: string;
  support: number;
  oppose: number;
  neutral: number;
  amplification: number;
  protest_propensity: number;
  key_concerns: string[];
}

export interface InformationSpread {
  viral_probability: number;
  dominant_narrative: string;
  counter_narratives: string[];
  time_to_saturation_hours: number;
}

export interface PopulationTrajectory {
  support_7d: number;
  support_30d: number;
  fatigue_onset_days: number;
}

export interface PopulationResponse {
  event_id: string;
  country: string;
  timestamp: string;
  overall: PopulationOverall;
  segment_responses: SegmentResponse[];
  information_spread: InformationSpread;
  trajectory: PopulationTrajectory;
}

export interface CombinedResponse {
  leader: LeaderResponse;
  population: PopulationResponse;
}
