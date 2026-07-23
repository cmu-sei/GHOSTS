import { Component, ChangeDetectionStrategy, inject, signal, OnDestroy } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { DecimalPipe } from '@angular/common';
import { Subscription } from 'rxjs';
import { EventFormComponent, PredictionSubmitEvent } from '../../../shared/components/event-form/event-form.component';
import { GlassCardComponent } from '../../../shared/components/glass-card/glass-card.component';
import { ScenarioWsService, WsConnectionState } from '../../../core/services/scenario-ws.service';
import { LeaderService } from '../../../core/services/leader.service';
import { LeaderProfile } from '../../../core/models/leader.model';
import { GeopoliticalEvent } from '../../../core/models/event.model';
import { LeaderResponse, PopulationResponse } from '../../../core/models/response.model';

interface TimelineEntry {
  id: string;
  timestamp: Date;
  event: GeopoliticalEvent;
  leaderResponse?: LeaderResponse;
  populationResponse?: PopulationResponse;
}

@Component({
  selector: 'app-scenario-session',
  standalone: true,
  imports: [
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, DecimalPipe,
    EventFormComponent, GlassCardComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scenario-session.component.html',
  styleUrl: './scenario-session.component.scss'
})
export class ScenarioSessionComponent implements OnDestroy {
  private readonly scenarioWsService = inject(ScenarioWsService);
  private readonly leaderService = inject(LeaderService);
  private readonly subscriptions: Subscription[] = [];

  showHelp = signal(false);
  leaders = signal<LeaderProfile[]>([]);
  scenarioId = signal('');
  selectedLeaderId = signal('');
  country = signal('');
  connectionState = signal<WsConnectionState>('disconnected');
  timeline = signal<TimelineEntry[]>([]);

  constructor() {
    this.leaderService.getAll().subscribe(leaders => this.leaders.set(leaders));

    this.subscriptions.push(
      this.scenarioWsService.state$.subscribe(state => this.connectionState.set(state)),

      this.scenarioWsService.leaderResponse$.subscribe(response => {
        this.timeline.update(entries => {
          const updated = [...entries];
          const entry = updated.find(e => e.id === response.event_id);
          if (entry) {
            entry.leaderResponse = response;
          }
          return updated;
        });
      }),

      this.scenarioWsService.populationResponse$.subscribe(response => {
        this.timeline.update(entries => {
          const updated = [...entries];
          const entry = updated.find(e => e.id === response.event_id);
          if (entry) {
            entry.populationResponse = response;
          }
          return updated;
        });
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(s => s.unsubscribe());
    this.scenarioWsService.disconnect();
  }

  connect(): void {
    const id = this.scenarioId();
    if (!id) return;
    this.scenarioWsService.connect(id);
  }

  disconnect(): void {
    this.scenarioWsService.disconnect();
  }

  onSendEvent(submission: PredictionSubmitEvent): void {
    const event = submission.event;
    const eventId = crypto.randomUUID();
    event.id = eventId;
    event.scenario_id = this.scenarioId();

    if (submission.targetLeaderId) {
      this.selectedLeaderId.set(submission.targetLeaderId);
    }

    const entry: TimelineEntry = {
      id: eventId,
      timestamp: new Date(),
      event
    };

    this.timeline.update(entries => [entry, ...entries]);

    this.scenarioWsService.sendEvent(event, this.selectedLeaderId(), this.country());
  }

  getSeverityColor(severity: number): string {
    if (severity < 0.33) return '#22c55e';
    if (severity < 0.66) return '#f59e0b';
    return '#ef4444';
  }
}
