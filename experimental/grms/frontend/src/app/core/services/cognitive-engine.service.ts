import { Injectable } from '@angular/core';

export interface CognitiveIdentity {
  role: string;
  decision_style: string; // 'aggressive' | 'defensive' | 'balanced' | 'opportunistic'
  power_base: string;
}

export interface CognitivePersonality {
  risk_tolerance: number; // -1 to 1
  authoritarianism: number;
  nationalism: number;
  pragmatism: number;
  aggression: number;
  populism: number;
  transparency: number;
  religiosity: number;
}

export interface CognitiveBeliefs {
  threat_is_real: number; // 0-1 probability
  military_effective: number;
  diplomacy_effective: number;
  economy_stable: number;
  allies_reliable: number;
}

export interface CognitiveMotivations {
  power: number; // -2 to 2
  security: number;
  status: number;
  independence: number;
  honor: number;
  order: number;
  vengeance: number;
  idealism: number;
}

export interface CognitiveContext {
  approval_rating: number; // 0-100
  military_readiness: number; // 0-1
  economic_strength: number; // 0-1
  alliance_support: number; // 0-1
  red_line_triggered: boolean;
  time_pressure: number; // 0-1
}

export interface CognitiveHistory {
  past_decisions: { action: string; outcome_positive: boolean }[];
}

export interface CognitiveConfig {
  identity: CognitiveIdentity;
  personality: CognitivePersonality;
  beliefs: CognitiveBeliefs;
  motivations: CognitiveMotivations;
  context: CognitiveContext;
  history: CognitiveHistory;
  layer_weights: number[]; // 6 weights, should sum close to 1
}

export interface ActionScore {
  action: string;
  total_score: number;
  layer_contributions: number[]; // per-layer score
  confidence: number;
}

export interface DecisionResult {
  actions: ActionScore[];
  selected_action: string;
  confidence: number;
  interaction_effects: { name: string; magnitude: number }[];
}

export interface SensitivityPoint {
  parameter_value: number;
  action_scores: { action: string; score: number }[];
}

const ACTIONS = ['military_strike', 'diplomatic_protest', 'economic_sanctions', 'covert_action', 'no_response'] as const;

@Injectable({ providedIn: 'root' })
export class CognitiveEngineService {

  computeDecision(config: CognitiveConfig): DecisionResult {
    const layerScores: number[][] = [
      this.scoreIdentityLayer(config),
      this.scorePersonalityLayer(config),
      this.scoreBeliefsLayer(config),
      this.scoreMotivationsLayer(config),
      this.scoreContextLayer(config),
      this.scoreHistoryLayer(config),
    ];

    const interactions = this.computeInteractions(config);

    const actions: ActionScore[] = ACTIONS.map((action, ai) => {
      let total = 0;
      const contributions: number[] = [];
      for (let li = 0; li < 6; li++) {
        const weighted = layerScores[li][ai] * config.layer_weights[li];
        contributions.push(weighted);
        total += weighted;
      }
      // Apply interaction effects
      for (const effect of interactions) {
        total += effect.magnitude * this.interactionActionBias(action, effect.name);
      }
      return { action, total_score: total, layer_contributions: contributions, confidence: 0 };
    });

    // Normalize scores to 0-1 range for display
    const maxScore = Math.max(...actions.map(a => a.total_score));
    const minScore = Math.min(...actions.map(a => a.total_score));
    const range = maxScore - minScore || 1;
    actions.forEach(a => {
      a.total_score = (a.total_score - minScore) / range;
    });

    // Sort descending
    actions.sort((a, b) => b.total_score - a.total_score);

    // Compute confidence as gap between top and second
    const gap = actions.length > 1 ? actions[0].total_score - actions[1].total_score : 1;
    const confidence = Math.min(gap * 2, 1);
    actions.forEach(a => a.confidence = a.total_score);

    return {
      actions,
      selected_action: actions[0].action,
      confidence,
      interaction_effects: interactions,
    };
  }

  runSensitivityAnalysis(config: CognitiveConfig, parameterPath: string, range: number[]): SensitivityPoint[] {
    return range.map(value => {
      const modified = this.setNestedValue(structuredClone(config), parameterPath, value);
      const result = this.computeDecision(modified);
      return {
        parameter_value: value,
        action_scores: result.actions.map(a => ({ action: a.action, score: a.total_score })),
      };
    });
  }

  private scoreIdentityLayer(config: CognitiveConfig): number[] {
    const { decision_style } = config.identity;
    // [military_strike, diplomatic_protest, economic_sanctions, covert_action, no_response]
    const styleBiases: Record<string, number[]> = {
      aggressive: [0.9, 0.2, 0.5, 0.7, 0.1],
      defensive: [0.3, 0.6, 0.7, 0.4, 0.8],
      balanced: [0.5, 0.5, 0.5, 0.5, 0.5],
      opportunistic: [0.6, 0.3, 0.6, 0.9, 0.3],
    };
    return styleBiases[decision_style] ?? styleBiases['balanced'];
  }

  private scorePersonalityLayer(config: CognitiveConfig): number[] {
    const p = config.personality;
    return [
      this.clamp01((p.aggression + 1) / 2 * 0.6 + (p.risk_tolerance + 1) / 2 * 0.4),
      this.clamp01((p.pragmatism + 1) / 2 * 0.5 + (1 - (p.aggression + 1) / 2) * 0.5),
      this.clamp01((p.pragmatism + 1) / 2 * 0.4 + (p.nationalism + 1) / 2 * 0.3 + 0.3),
      this.clamp01((p.risk_tolerance + 1) / 2 * 0.5 + (1 - (p.transparency + 1) / 2) * 0.5),
      this.clamp01((1 - (p.aggression + 1) / 2) * 0.5 + (1 - (p.risk_tolerance + 1) / 2) * 0.5),
    ];
  }

  private scoreBeliefsLayer(config: CognitiveConfig): number[] {
    const b = config.beliefs;
    return [
      this.clamp01(b.threat_is_real * 0.5 + b.military_effective * 0.5),
      this.clamp01(b.diplomacy_effective * 0.7 + b.allies_reliable * 0.3),
      this.clamp01(b.threat_is_real * 0.3 + b.economy_stable * 0.4 + (1 - b.military_effective) * 0.3),
      this.clamp01(b.threat_is_real * 0.4 + (1 - b.diplomacy_effective) * 0.3 + b.military_effective * 0.3),
      this.clamp01((1 - b.threat_is_real) * 0.7 + b.economy_stable * 0.3),
    ];
  }

  private scoreMotivationsLayer(config: CognitiveConfig): number[] {
    const m = config.motivations;
    const norm = (v: number) => (v + 2) / 4; // -2..2 -> 0..1
    return [
      this.clamp01(norm(m.power) * 0.3 + norm(m.vengeance) * 0.3 + norm(m.honor) * 0.2 + norm(m.security) * 0.2),
      this.clamp01(norm(m.idealism) * 0.4 + norm(m.order) * 0.3 + norm(m.status) * 0.3),
      this.clamp01(norm(m.power) * 0.3 + norm(m.independence) * 0.3 + norm(m.order) * 0.4),
      this.clamp01(norm(m.vengeance) * 0.3 + norm(m.power) * 0.3 + norm(m.independence) * 0.4),
      this.clamp01(norm(m.idealism) * 0.3 + (1 - norm(m.vengeance)) * 0.4 + (1 - norm(m.power)) * 0.3),
    ];
  }

  private scoreContextLayer(config: CognitiveConfig): number[] {
    const c = config.context;
    const approvalNorm = c.approval_rating / 100;
    const redLineBoost = c.red_line_triggered ? 0.4 : 0;
    return [
      this.clamp01(c.military_readiness * 0.4 + redLineBoost + approvalNorm * 0.2),
      this.clamp01(c.alliance_support * 0.4 + (1 - c.time_pressure) * 0.3 + 0.3),
      this.clamp01(c.economic_strength * 0.5 + c.alliance_support * 0.3 + 0.2),
      this.clamp01(c.time_pressure * 0.3 + (1 - approvalNorm) * 0.3 + c.military_readiness * 0.4),
      this.clamp01((1 - c.time_pressure) * 0.4 + (1 - redLineBoost) * 0.3 + c.economic_strength * 0.3),
    ];
  }

  private scoreHistoryLayer(config: CognitiveConfig): number[] {
    const history = config.history.past_decisions;
    if (history.length === 0) return [0.5, 0.5, 0.5, 0.5, 0.5];

    const actionCounts: Record<string, { total: number; positive: number }> = {};
    for (const d of history) {
      if (!actionCounts[d.action]) actionCounts[d.action] = { total: 0, positive: 0 };
      actionCounts[d.action].total++;
      if (d.outcome_positive) actionCounts[d.action].positive++;
    }

    return ACTIONS.map(action => {
      const counts = actionCounts[action];
      if (!counts) return 0.4;
      const successRate = counts.positive / counts.total;
      const frequency = Math.min(counts.total / history.length, 1);
      return this.clamp01(successRate * 0.6 + frequency * 0.4);
    });
  }

  private computeInteractions(config: CognitiveConfig): { name: string; magnitude: number }[] {
    const effects: { name: string; magnitude: number }[] = [];

    // High aggression + red line triggered
    if (config.personality.aggression > 0.5 && config.context.red_line_triggered) {
      effects.push({ name: 'Aggression + Red Line', magnitude: 0.3 });
    }

    // High pragmatism + low threat
    if (config.personality.pragmatism > 0.5 && config.beliefs.threat_is_real < 0.3) {
      effects.push({ name: 'Pragmatism + Low Threat', magnitude: -0.2 });
    }

    // High time pressure + low approval
    if (config.context.time_pressure > 0.7 && config.context.approval_rating < 30) {
      effects.push({ name: 'Time Pressure + Low Approval', magnitude: 0.15 });
    }

    // Strong alliances + diplomacy belief
    if (config.beliefs.allies_reliable > 0.7 && config.beliefs.diplomacy_effective > 0.7) {
      effects.push({ name: 'Alliance + Diplomacy Synergy', magnitude: 0.2 });
    }

    // Vengeance + honor motivation
    if (config.motivations.vengeance > 1 && config.motivations.honor > 1) {
      effects.push({ name: 'Vengeance + Honor', magnitude: 0.25 });
    }

    return effects;
  }

  private interactionActionBias(action: string, effectName: string): number {
    const biases: Record<string, Record<string, number>> = {
      'Aggression + Red Line': { military_strike: 0.8, covert_action: 0.3, diplomatic_protest: -0.3, no_response: -0.8 },
      'Pragmatism + Low Threat': { no_response: 0.6, diplomatic_protest: 0.4, military_strike: -0.6 },
      'Time Pressure + Low Approval': { military_strike: 0.5, covert_action: 0.4, no_response: -0.5 },
      'Alliance + Diplomacy Synergy': { diplomatic_protest: 0.7, economic_sanctions: 0.5, military_strike: -0.3 },
      'Vengeance + Honor': { military_strike: 0.6, covert_action: 0.4, no_response: -0.7 },
    };
    return biases[effectName]?.[action] ?? 0;
  }

  private clamp01(v: number): number {
    return Math.max(0, Math.min(1, v));
  }

  private setNestedValue(obj: any, path: string, value: number): any {
    const keys = path.split('.');
    let current = obj;
    for (let i = 0; i < keys.length - 1; i++) {
      current = current[keys[i]];
    }
    current[keys[keys.length - 1]] = value;
    return obj;
  }
}
