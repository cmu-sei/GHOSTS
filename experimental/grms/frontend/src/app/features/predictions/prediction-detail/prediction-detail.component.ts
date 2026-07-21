import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe, SlicePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatTableModule } from '@angular/material/table';
import { GlassCardComponent } from '../../../shared/components/glass-card/glass-card.component';
import { GaugeComponent } from '../../../shared/components/gauge/gauge.component';
import { PredictionHistoryService } from '../../../core/services/prediction-history.service';
import { SavedPrediction } from '../../../core/models/prediction-history.model';

@Component({
  selector: 'app-prediction-detail',
  standalone: true,
  imports: [
    DatePipe, DecimalPipe, SlicePipe,
    MatButtonModule, MatIconModule, MatChipsModule, MatExpansionModule, MatTableModule,
    GlassCardComponent, GaugeComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './prediction-detail.component.html',
  styleUrl: './prediction-detail.component.scss'
})
export class PredictionDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly historyService = inject(PredictionHistoryService);

  prediction = signal<SavedPrediction | null>(null);
  loading = signal(true);

  displayedColumns = ['segment_name', 'support', 'oppose', 'amplification', 'protest_propensity'];

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.historyService.getById(id).subscribe({
        next: (p) => {
          this.prediction.set(p);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        }
      });
    } else {
      this.loading.set(false);
    }
  }

  goBack(): void {
    this.router.navigate(['/predictions']);
  }

  getTypeIcon(type: string): string {
    const icons: Record<string, string> = {
      leader: 'person',
      population: 'groups',
      combined: 'hub',
    };
    return icons[type] || 'analytics';
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
