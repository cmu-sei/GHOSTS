import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { DecimalPipe } from '@angular/common';
import { EventFormComponent, PredictionSubmitEvent } from '../../../shared/components/event-form/event-form.component';
import { GaugeComponent } from '../../../shared/components/gauge/gauge.component';
import { GlassCardComponent } from '../../../shared/components/glass-card/glass-card.component';
import { PredictionService } from '../../../core/services/prediction.service';
import { PopulationResponse } from '../../../core/models/response.model';

@Component({
  selector: 'app-population-prediction',
  standalone: true,
  imports: [
    MatFormFieldModule, MatInputModule, MatTableModule, MatIconModule,
    DecimalPipe, EventFormComponent, GaugeComponent, GlassCardComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './population-prediction.component.html',
  styleUrl: './population-prediction.component.scss'
})
export class PopulationPredictionComponent {
  private readonly predictionService = inject(PredictionService);

  country = signal('');
  result = signal<PopulationResponse | null>(null);
  loading = signal(false);

  displayedColumns = ['segment_name', 'support', 'oppose', 'amplification', 'protest_propensity'];

  onPredict(submission: PredictionSubmitEvent): void {
    const country = this.country();
    if (!country) return;

    this.loading.set(true);
    this.result.set(null);

    this.predictionService.predictPopulation(country, submission.event).subscribe({
      next: (response) => {
        this.result.set(response);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
