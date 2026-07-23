import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { LeaderPredictionComponent } from '../leader-prediction/leader-prediction.component';
import { PopulationPredictionComponent } from '../population-prediction/population-prediction.component';
import { CombinedPredictionComponent } from '../combined-prediction/combined-prediction.component';

@Component({
  selector: 'app-predictions-shell',
  standalone: true,
  imports: [
    MatTabsModule, MatButtonModule, MatIconModule,
    LeaderPredictionComponent, PopulationPredictionComponent, CombinedPredictionComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './predictions-shell.component.html',
  styleUrl: './predictions-shell.component.scss'
})
export class PredictionsShellComponent {
  private readonly router = inject(Router);

  goBack(): void {
    this.router.navigate(['/predictions']);
  }
}
