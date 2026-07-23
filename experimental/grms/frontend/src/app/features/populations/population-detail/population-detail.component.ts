import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DecimalPipe } from '@angular/common';
import { GlassCardComponent } from '../../../shared/components/glass-card/glass-card.component';
import { PopulationService } from '../../../core/services/population.service';
import { PopulationProfile, PopulationSegment } from '../../../core/models/population.model';
import { CountryFlagPipe } from '../../../shared/pipes/country-flag.pipe';

@Component({
  selector: 'app-population-detail',
  standalone: true,
  imports: [
    MatExpansionModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    DecimalPipe,
    GlassCardComponent,
    CountryFlagPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './population-detail.component.html',
  styleUrl: './population-detail.component.scss',
})
export class PopulationDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly populationService = inject(PopulationService);

  population = signal<PopulationProfile | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);

  segmentColors = ['#38bdf8', '#22c55e', '#f59e0b', '#a78bfa', '#ef4444', '#06b6d4', '#f472b6', '#84cc16'];

  ngOnInit(): void {
    const country = this.route.snapshot.paramMap.get('country');
    if (!country) {
      this.router.navigate(['/populations']);
      return;
    }

    this.populationService.getByCountry(country).subscribe({
      next: (profile) => {
        this.population.set(profile);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.status === 404 ? 'Population profile not found' : 'Failed to load data');
        this.loading.set(false);
      },
    });
  }

  getSegmentColor(index: number): string {
    return this.segmentColors[index % this.segmentColors.length];
  }

  goToPredictions(): void {
    this.router.navigate(['/predictions']);
  }

  goBack(): void {
    this.router.navigate(['/populations']);
  }
}
