import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { EventFormComponent, PredictionSubmitEvent } from '../../../shared/components/event-form/event-form.component';
import { GaugeComponent } from '../../../shared/components/gauge/gauge.component';
import { GlassCardComponent } from '../../../shared/components/glass-card/glass-card.component';
import { PredictionService } from '../../../core/services/prediction.service';
import { LeaderService } from '../../../core/services/leader.service';
import { LeaderProfile } from '../../../core/models/leader.model';
import { CombinedResponse } from '../../../core/models/response.model';

@Component({
  selector: 'app-combined-prediction',
  standalone: true,
  imports: [
    DecimalPipe, MatSelectModule, MatFormFieldModule, MatInputModule,
    MatIconModule, EventFormComponent, GaugeComponent, GlassCardComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './combined-prediction.component.html',
  styleUrl: './combined-prediction.component.scss'
})
export class CombinedPredictionComponent {
  private readonly predictionService = inject(PredictionService);
  private readonly leaderService = inject(LeaderService);

  leaders = signal<LeaderProfile[]>([]);
  country = signal('');
  result = signal<CombinedResponse | null>(null);
  loading = signal(false);

  constructor() {
    this.leaderService.getAll().subscribe(leaders => this.leaders.set(leaders));
  }

  onPredict(submission: PredictionSubmitEvent): void {
    const { event, targetLeaderId } = submission;
    const countryVal = this.country();
    if (!targetLeaderId || !countryVal) return;

    this.loading.set(true);
    this.result.set(null);

    this.predictionService.predictCombined(targetLeaderId, countryVal, event).subscribe({
      next: (response) => {
        this.result.set(response);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
