import { GeopoliticalEvent } from './event.model';
import { LeaderResponse, PopulationResponse } from './response.model';

export interface KnownOutcome {
  escalation_risk?: number;
  action_types: string[];
  tone?: string;
  description: string;
}

export interface ScoreDimension {
  dimension: string;
  predicted: number | string | string[];
  actual: number | string | string[];
  score: number;
  note: string;
}

export interface PredictionScore {
  overall_score: number;
  escalation_score?: number;
  action_type_score?: number;
  tone_score?: number;
  dimensions: ScoreDimension[];
  scored_at: string;
}

export interface SavedPrediction {
  id: string;
  created_at: string;
  historical_date?: string;
  label: string;
  prediction_type: 'leader' | 'population' | 'combined';
  event: GeopoliticalEvent;
  leader_id?: string;
  leader_name?: string;
  country?: string;
  llm_model?: string;
  leader_response?: LeaderResponse;
  population_response?: PopulationResponse;
  cascade_responses: LeaderResponse[];
  known_outcome?: KnownOutcome;
  score?: PredictionScore;
}
