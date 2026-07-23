import { Component, ChangeDetectionStrategy, signal, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { GlassCardComponent } from '../../../shared/components/glass-card/glass-card.component';
import { RadarChartComponent, RadarDataPoint } from '../../../shared/components/radar-chart/radar-chart.component';
import { DimensionSliderComponent } from '../../../shared/components/dimension-slider/dimension-slider.component';
import { LeaderService } from '../../../core/services/leader.service';
import { LeaderPersonality, LeaderProfile, PERSONALITY_DIMENSIONS } from '../../../core/models/leader.model';
import { CountryFlagPipe } from '../../../shared/pipes/country-flag.pipe';

@Component({
  selector: 'app-leader-detail',
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatFormFieldModule,
    MatInputModule,
    GlassCardComponent,
    RadarChartComponent,
    DimensionSliderComponent,
    CountryFlagPipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './leader-detail.component.html',
  styleUrl: './leader-detail.component.scss'
})
export class LeaderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly leaderService = inject(LeaderService);

  leader = signal<LeaderProfile | null>(null);
  loading = signal(true);
  isNew = signal(false);
  dimensions = PERSONALITY_DIMENSIONS;

  newName = '';
  newCountry = '';
  newTitle = '';
  newPersonality: Record<string, number> = {
    risk_tolerance: 0,
    authoritarianism: 0,
    nationalism: 0,
    pragmatism: 0,
    aggression: 0,
    populism: 0,
    transparency: 0,
    religiosity: 0,
  };

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.leaderService.getById(id).subscribe({
        next: (leader) => {
          this.leader.set(leader);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        }
      });
    } else {
      this.isNew.set(true);
      this.loading.set(false);
    }
  }

  getRadarData(): RadarDataPoint[] {
    const l = this.leader();
    if (!l) return [];
    return PERSONALITY_DIMENSIONS.map(dim => ({
      axis: dim.high,
      value: l.personality[dim.key]
    }));
  }

  navigateToPredictions(): void {
    this.router.navigate(['/predictions']);
  }

  createLeader(): void {
    if (!this.newName || !this.newCountry) return;

    this.leaderService.create({
      name: this.newName,
      country: this.newCountry,
      title: this.newTitle,
      personality: this.newPersonality as unknown as LeaderPersonality,
    }).subscribe({
      next: (created) => {
        this.router.navigate(['/leaders', created.id]);
      }
    });
  }
}
