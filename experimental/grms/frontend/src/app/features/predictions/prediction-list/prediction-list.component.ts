import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe, SlicePipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { GlassCardComponent } from '../../../shared/components/glass-card/glass-card.component';
import { PredictionHistoryService } from '../../../core/services/prediction-history.service';
import { SavedPrediction } from '../../../core/models/prediction-history.model';

@Component({
  selector: 'app-prediction-list',
  standalone: true,
  imports: [
    DatePipe, DecimalPipe, SlicePipe, MatButtonModule, MatIconModule,
    MatChipsModule, MatTooltipModule, GlassCardComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './prediction-list.component.html',
  styleUrl: './prediction-list.component.scss'
})
export class PredictionListComponent {
  private readonly historyService = inject(PredictionHistoryService);
  private readonly router = inject(Router);

  predictions = signal<SavedPrediction[]>([]);
  loading = signal(true);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.historyService.getAll().subscribe({
      next: (items) => {
        this.predictions.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  newPrediction(): void {
    this.router.navigate(['/predictions', 'new']);
  }

  viewPrediction(p: SavedPrediction): void {
    this.router.navigate(['/predictions', p.id]);
  }

  deletePrediction(p: SavedPrediction, event: Event): void {
    event.stopPropagation();
    this.historyService.delete(p.id).subscribe(() => {
      this.predictions.update(list => list.filter(x => x.id !== p.id));
    });
  }

  getTypeIcon(type: string): string {
    const icons: Record<string, string> = {
      leader: 'person',
      population: 'groups',
      combined: 'hub',
    };
    return icons[type] || 'analytics';
  }
}
