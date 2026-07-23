import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
import { DecimalPipe, TitleCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DimensionSliderComponent } from '../../shared/components/dimension-slider/dimension-slider.component';
import { GlassCardComponent } from '../../shared/components/glass-card/glass-card.component';
import { GaugeComponent } from '../../shared/components/gauge/gauge.component';
import {
  CognitiveEngineService,
  CognitiveConfig,
  CognitiveIdentity,
  CognitivePersonality,
  CognitiveBeliefs,
  CognitiveMotivations,
  CognitiveContext,
  CognitiveHistory,
  DecisionResult,
  SensitivityPoint,
} from '../../core/services/cognitive-engine.service';

@Component({
  selector: 'app-cognitive-lab',
  standalone: true,
  imports: [
    DecimalPipe,
    TitleCasePipe,
    FormsModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSliderModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    DimensionSliderComponent,
    GlassCardComponent,
    GaugeComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cognitive-lab.component.html',
  styleUrl: './cognitive-lab.component.scss',
})
export class CognitiveLabComponent {
  private readonly engine = inject(CognitiveEngineService);
  readonly Math = Math;

  showHelp = signal(false);

  // Layer editor state
  identity = signal<CognitiveIdentity>({
    role: 'Head of State',
    decision_style: 'balanced',
    power_base: 'Military-Industrial',
  });

  personality = signal<CognitivePersonality>({
    risk_tolerance: 0,
    authoritarianism: 0,
    nationalism: 0,
    pragmatism: 0,
    aggression: 0,
    populism: 0,
    transparency: 0,
    religiosity: 0,
  });

  beliefs = signal<CognitiveBeliefs>({
    threat_is_real: 0.5,
    military_effective: 0.5,
    diplomacy_effective: 0.5,
    economy_stable: 0.5,
    allies_reliable: 0.5,
  });

  motivations = signal<CognitiveMotivations>({
    power: 0,
    security: 0,
    status: 0,
    independence: 0,
    honor: 0,
    order: 0,
    vengeance: 0,
    idealism: 0,
  });

  context = signal<CognitiveContext>({
    approval_rating: 50,
    military_readiness: 0.5,
    economic_strength: 0.5,
    alliance_support: 0.5,
    red_line_triggered: false,
    time_pressure: 0.3,
  });

  history = signal<CognitiveHistory>({ past_decisions: [] });

  layerWeights = signal<number[]>([0.15, 0.2, 0.2, 0.2, 0.15, 0.1]);

  // Sensitivity analysis
  sensitivityParam = signal<string>('personality.aggression');
  sensitivityParams = [
    { label: 'Aggression', path: 'personality.aggression' },
    { label: 'Risk Tolerance', path: 'personality.risk_tolerance' },
    { label: 'Pragmatism', path: 'personality.pragmatism' },
    { label: 'Threat Real', path: 'beliefs.threat_is_real' },
    { label: 'Military Effective', path: 'beliefs.military_effective' },
    { label: 'Diplomacy Effective', path: 'beliefs.diplomacy_effective' },
    { label: 'Military Readiness', path: 'context.military_readiness' },
    { label: 'Time Pressure', path: 'context.time_pressure' },
    { label: 'Alliance Support', path: 'context.alliance_support' },
  ];

  decisionStyles = ['aggressive', 'defensive', 'balanced', 'opportunistic'];

  layerNames = ['Identity', 'Personality', 'Beliefs', 'Motivations', 'Context', 'History'];
  layerColors = ['#38bdf8', '#a78bfa', '#22c55e', '#f59e0b', '#ef4444', '#06b6d4'];

  config = computed<CognitiveConfig>(() => ({
    identity: this.identity(),
    personality: this.personality(),
    beliefs: this.beliefs(),
    motivations: this.motivations(),
    context: this.context(),
    history: this.history(),
    layer_weights: this.layerWeights(),
  }));

  result = computed<DecisionResult>(() => this.engine.computeDecision(this.config()));

  selectedAction = computed(() => this.result().selected_action);

  selectedActionScores = computed(() => {
    const r = this.result();
    return r.actions.find(a => a.action === r.selected_action);
  });

  sensitivityData = computed<SensitivityPoint[]>(() => {
    const path = this.sensitivityParam();
    const isMotivation = path.startsWith('motivations.');
    const range = isMotivation
      ? [-2, -1.5, -1, -0.5, 0, 0.5, 1, 1.5, 2]
      : path.includes('approval')
        ? [0, 12.5, 25, 37.5, 50, 62.5, 75, 87.5, 100]
        : [-1, -0.75, -0.5, -0.25, 0, 0.25, 0.5, 0.75, 1];
    return this.engine.runSensitivityAnalysis(this.config(), path, range);
  });

  // Action display helpers
  actionLabels: Record<string, string> = {
    military_strike: 'Military Strike',
    diplomatic_protest: 'Diplomatic Protest',
    economic_sanctions: 'Economic Sanctions',
    covert_action: 'Covert Action',
    no_response: 'No Response',
  };

  actionColors: Record<string, string> = {
    military_strike: '#ef4444',
    diplomatic_protest: '#38bdf8',
    economic_sanctions: '#f59e0b',
    covert_action: '#a78bfa',
    no_response: '#64748b',
  };

  // History management
  newHistoryAction = signal('military_strike');
  newHistoryOutcome = signal(true);

  addHistoryEntry(): void {
    const current = this.history();
    this.history.set({
      past_decisions: [
        ...current.past_decisions,
        { action: this.newHistoryAction(), outcome_positive: this.newHistoryOutcome() },
      ],
    });
  }

  removeHistoryEntry(index: number): void {
    const current = this.history();
    this.history.set({
      past_decisions: current.past_decisions.filter((_, i) => i !== index),
    });
  }

  // Identity updaters
  updateIdentityField(field: keyof CognitiveIdentity, value: string): void {
    this.identity.set({ ...this.identity(), [field]: value });
  }

  // Personality updater
  updatePersonality(field: keyof CognitivePersonality, value: number): void {
    this.personality.set({ ...this.personality(), [field]: value });
  }

  // Beliefs updater
  updateBeliefs(field: keyof CognitiveBeliefs, value: number): void {
    this.beliefs.set({ ...this.beliefs(), [field]: value });
  }

  // Motivations updater
  updateMotivations(field: keyof CognitiveMotivations, value: number): void {
    this.motivations.set({ ...this.motivations(), [field]: value });
  }

  // Context updaters
  updateContext(field: keyof CognitiveContext, value: any): void {
    this.context.set({ ...this.context(), [field]: value });
  }

  // Layer weights updater
  updateWeight(index: number, value: number): void {
    const weights = [...this.layerWeights()];
    weights[index] = value;
    this.layerWeights.set(weights);
  }

  normalizeWeights(): void {
    const weights = this.layerWeights();
    const sum = weights.reduce((a, b) => a + b, 0);
    if (sum === 0) return;
    this.layerWeights.set(weights.map(w => +(w / sum).toFixed(3)));
  }

  getMaxSensitivityScore(): number {
    const data = this.sensitivityData();
    let max = 0;
    for (const point of data) {
      for (const s of point.action_scores) {
        if (s.score > max) max = s.score;
      }
    }
    return max || 1;
  }
}
