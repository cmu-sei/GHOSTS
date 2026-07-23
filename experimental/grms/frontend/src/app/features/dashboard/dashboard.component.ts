import { Component, ChangeDetectionStrategy, signal, computed, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { GlassCardComponent } from '../../shared/components/glass-card/glass-card.component';
import { HealthService } from '../../core/services/health.service';
import { LeaderService } from '../../core/services/leader.service';
import { PopulationService } from '../../core/services/population.service';
import { PredictionHistoryService } from '../../core/services/prediction-history.service';
import { HealthStatus } from '../../core/models/health.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, MatIconModule, MatButtonModule, GlassCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly healthService = inject(HealthService);
  private readonly leaderService = inject(LeaderService);
  private readonly populationService = inject(PopulationService);
  private readonly predictionHistoryService = inject(PredictionHistoryService);

  leaderCount = signal(0);
  populationCount = signal(0);
  predictionsRun = signal(0);
  systemStatus = signal<'healthy' | 'unhealthy'>('unhealthy');
  llmBackend = signal('—');
  llmModel = signal('—');
  currentDateTime = signal(new Date().toLocaleString());

  ngOnInit(): void {
    this.healthService.getHealth().subscribe({
      next: (health: HealthStatus) => {
        this.systemStatus.set(health.status === 'healthy' ? 'healthy' : 'unhealthy');
        this.llmBackend.set(health.llm_backend);
        this.llmModel.set(health.llm_model);
        this.populationCount.set(health.populations_loaded);
        this.leaderCount.set(health.leaders_loaded);
      }
    });

    this.leaderService.getAll().subscribe({
      next: (leaders) => {
        this.leaderCount.set(leaders.length);
      }
    });

    this.populationService.getAll().subscribe({
      next: (populations) => {
        this.populationCount.set(populations.length);
      }
    });

    this.predictionHistoryService.getAll().subscribe({
      next: (predictions) => {
        this.predictionsRun.set(predictions.length);
      }
    });

    setInterval(() => {
      this.currentDateTime.set(new Date().toLocaleString());
    }, 1000);
  }
}
