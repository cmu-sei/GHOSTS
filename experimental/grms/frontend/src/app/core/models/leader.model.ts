export interface LeaderPersonality {
  risk_tolerance: number;
  authoritarianism: number;
  nationalism: number;
  pragmatism: number;
  aggression: number;
  populism: number;
  transparency: number;
  religiosity: number;
}

export interface LeaderIdeology {
  economic_system: string;
  geopolitical_orientation: string;
  key_alliances: string[];
  adversaries: string[];
  red_lines: string[];
  core_narratives: string[];
}

export interface HistoricalDecision {
  event_description: string;
  decision_taken: string;
  outcome: string;
  date: string;
  context_factors: string[];
}

export interface LeaderCulturalContext {
  decision_making_style: string;
  information_sources: string[];
  domestic_power_base: string;
  succession_concerns: boolean;
  historical_analogies: string[];
}

export interface EconomicConditions {
  gdp_growth: number;
  unemployment: number;
  inflation: number;
  sanctions_pressure: number;
}

export interface MilitaryPosture {
  readiness_level: string;
  ongoing_operations: string[];
  force_disposition: string;
}

export interface DomesticPressures {
  election_proximity_days: number | null;
  opposition_strength: number;
  media_sentiment: number;
  protest_level: string;
}

export interface LeaderDecisionContext {
  approval_rating: number;
  economic_conditions: EconomicConditions;
  military_posture: MilitaryPosture;
  domestic_pressures: DomesticPressures;
  ongoing_conflicts: string[];
  recent_events: string[];
}

export interface LeaderProfile {
  id: string;
  name: string;
  country: string;
  title: string;
  period: string;
  personality: LeaderPersonality;
  ideology: LeaderIdeology;
  decision_history: HistoricalDecision[];
  cultural_context: LeaderCulturalContext;
  decision_context: LeaderDecisionContext;
}

export const PERSONALITY_DIMENSIONS: { key: keyof LeaderPersonality; low: string; high: string }[] = [
  { key: 'risk_tolerance', low: 'Cautious', high: 'Reckless' },
  { key: 'authoritarianism', low: 'Democratic', high: 'Autocratic' },
  { key: 'nationalism', low: 'Internationalist', high: 'Nationalist' },
  { key: 'pragmatism', low: 'Ideological', high: 'Pragmatic' },
  { key: 'aggression', low: 'Dovish', high: 'Hawkish' },
  { key: 'populism', low: 'Elitist', high: 'Populist' },
  { key: 'transparency', low: 'Secretive', high: 'Transparent' },
  { key: 'religiosity', low: 'Secular', high: 'Theocratic' },
];
