import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { DecimalPipe, SlicePipe } from '@angular/common';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { EventFormComponent, PredictionSubmitEvent } from '../../../shared/components/event-form/event-form.component';
import { GaugeComponent } from '../../../shared/components/gauge/gauge.component';
import { GlassCardComponent } from '../../../shared/components/glass-card/glass-card.component';
import { PredictionService } from '../../../core/services/prediction.service';
import { PredictionHistoryService } from '../../../core/services/prediction-history.service';
import { LeaderService } from '../../../core/services/leader.service';
import { LeaderProfile } from '../../../core/models/leader.model';
import { GeopoliticalEvent } from '../../../core/models/event.model';
import { LeaderResponse } from '../../../core/models/response.model';
import { SavedPrediction } from '../../../core/models/prediction-history.model';

interface AllianceCascadeEntry {
  leader: LeaderProfile;
  relationship: 'ally' | 'adversary';
  result: LeaderResponse | null;
  loading: boolean;
}

@Component({
  selector: 'app-leader-prediction',
  standalone: true,
  imports: [
    DecimalPipe, SlicePipe, MatSelectModule, MatFormFieldModule, MatExpansionModule,
    MatChipsModule, MatIconModule, MatButtonModule, MatTooltipModule,
    EventFormComponent, GaugeComponent, GlassCardComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './leader-prediction.component.html',
  styleUrl: './leader-prediction.component.scss'
})
export class LeaderPredictionComponent {
  private readonly predictionService = inject(PredictionService);
  private readonly historyService = inject(PredictionHistoryService);
  private readonly leaderService = inject(LeaderService);

  leaders = signal<LeaderProfile[]>([]);
  result = signal<LeaderResponse | null>(null);
  loading = signal(false);
  saved = signal(false);
  lastEvent = signal<GeopoliticalEvent | null>(null);
  targetLeader = signal<LeaderProfile | null>(null);
  cascadeEntries = signal<AllianceCascadeEntry[]>([]);

  constructor() {
    this.leaderService.getAll().subscribe(leaders => this.leaders.set(leaders));
  }

  onPredict(submission: PredictionSubmitEvent): void {
    const { event, targetLeaderId } = submission;
    if (!targetLeaderId) return;

    const target = this.leaders().find(l => l.id === targetLeaderId);
    this.targetLeader.set(target ?? null);
    this.loading.set(true);
    this.result.set(null);
    this.saved.set(false);
    this.lastEvent.set(event);
    this.cascadeEntries.set([]);

    this.predictionService.predictLeader(targetLeaderId, event).subscribe({
      next: (response) => {
        this.result.set(response);
        this.loading.set(false);
        this.buildCascadeEntries(event);
        this.savePrediction(event, response);
      },
      error: () => this.loading.set(false)
    });
  }

  private savePrediction(event: GeopoliticalEvent, response: LeaderResponse): void {
    const target = this.targetLeader();
    const prediction: SavedPrediction = {
      id: crypto.randomUUID(),
      created_at: new Date().toISOString(),
      label: event.description,
      prediction_type: 'leader',
      event,
      leader_id: target?.id,
      leader_name: target?.name,
      country: target?.country,
      leader_response: response,
      cascade_responses: [],
    };
    this.historyService.save(prediction).subscribe(() => this.saved.set(true));
  }

  private buildCascadeEntries(event: GeopoliticalEvent): void {
    const target = this.targetLeader();
    const targetCountry = event.structured.target;
    const actorCountry = event.structured.actor;
    const targetPeriod = target?.period || 'current';
    const entries: AllianceCascadeEntry[] = [];

    for (const leader of this.leaders()) {
      if (leader.period !== targetPeriod) continue;
      if (leader.country.toLowerCase() === targetCountry.toLowerCase()) continue;
      if (leader.country.toLowerCase() === actorCountry.toLowerCase()) continue;

      const isAllyOfTarget = leader.ideology.key_alliances
        .some(a => a.toLowerCase() === targetCountry.toLowerCase());
      const isAdversaryOfActor = leader.ideology.adversaries
        .some(a => a.toLowerCase() === actorCountry.toLowerCase());

      if (isAllyOfTarget) {
        entries.push({ leader, relationship: 'ally', result: null, loading: false });
      } else if (isAdversaryOfActor) {
        entries.push({ leader, relationship: 'adversary', result: null, loading: false });
      }
    }

    this.cascadeEntries.set(entries);
  }

  predictCascade(entry: AllianceCascadeEntry): void {
    const event = this.lastEvent();
    if (!event) return;

    const entries = this.cascadeEntries();
    const idx = entries.indexOf(entry);
    if (idx === -1) return;

    const updated = [...entries];
    updated[idx] = { ...entry, loading: true };
    this.cascadeEntries.set(updated);

    this.predictionService.predictLeader(entry.leader.id, event).subscribe({
      next: (response) => {
        const current = [...this.cascadeEntries()];
        const i = current.findIndex(e => e.leader.id === entry.leader.id);
        if (i !== -1) {
          current[i] = { ...current[i], result: response, loading: false };
          this.cascadeEntries.set(current);
        }
      },
      error: () => {
        const current = [...this.cascadeEntries()];
        const i = current.findIndex(e => e.leader.id === entry.leader.id);
        if (i !== -1) {
          current[i] = { ...current[i], loading: false };
          this.cascadeEntries.set(current);
        }
      }
    });
  }

  predictAllCascade(): void {
    for (const entry of this.cascadeEntries()) {
      if (!entry.result && !entry.loading) {
        this.predictCascade(entry);
      }
    }
  }

  getActionIcon(actionType: string): string {
    const icons: Record<string, string> = {
      military: 'military_tech',
      diplomatic: 'handshake',
      economic: 'payments',
      cyber: 'security',
      information: 'campaign',
    };
    return icons[actionType] || 'bolt';
  }

  getToneColor(tone: string): string {
    const colors: Record<string, string> = {
      aggressive: '#ef4444',
      conciliatory: '#22c55e',
      defiant: '#f59e0b',
      measured: '#38bdf8',
      threatening: '#ef4444',
    };
    return colors[tone] || '#94a3b8';
  }
}
