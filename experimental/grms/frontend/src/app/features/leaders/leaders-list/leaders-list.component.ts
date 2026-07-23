import { Component, ChangeDetectionStrategy, signal, computed, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFabButton } from '@angular/material/button';
import { FormsModule } from '@angular/forms';
import { GlassCardComponent } from '../../../shared/components/glass-card/glass-card.component';
import { RadarChartComponent, RadarDataPoint } from '../../../shared/components/radar-chart/radar-chart.component';
import { LeaderService } from '../../../core/services/leader.service';
import { LeaderProfile, PERSONALITY_DIMENSIONS } from '../../../core/models/leader.model';
import { CountryFlagPipe } from '../../../shared/pipes/country-flag.pipe';

@Component({
  selector: 'app-leaders-list',
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatExpansionModule,
    GlassCardComponent,
    RadarChartComponent,
    CountryFlagPipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './leaders-list.component.html',
  styleUrl: './leaders-list.component.scss'
})
export class LeadersListComponent implements OnInit {
  private readonly leaderService = inject(LeaderService);

  leaders = signal<LeaderProfile[]>([]);
  loading = signal(true);
  searchQuery = signal('');

  filteredLeaders = computed(() => {
    const query = this.searchQuery().toLowerCase();
    if (!query) return this.leaders();
    return this.leaders().filter(l =>
      l.name.toLowerCase().includes(query) ||
      l.country.toLowerCase().includes(query) ||
      l.title.toLowerCase().includes(query) ||
      (l.period && l.period.toLowerCase().includes(query))
    );
  });

  leadersByPeriod = computed(() => {
    const groups: { period: string; label: string; leaders: LeaderProfile[] }[] = [];
    const map = new Map<string, LeaderProfile[]>();
    for (const l of this.filteredLeaders()) {
      const period = l.period || 'current';
      if (!map.has(period)) map.set(period, []);
      map.get(period)!.push(l);
    }
    for (const [period, leaders] of map) {
      groups.push({ period, label: period.charAt(0).toUpperCase() + period.slice(1), leaders });
    }
    groups.sort((a, b) => a.period === 'current' ? -1 : b.period === 'current' ? 1 : a.period.localeCompare(b.period));
    return groups;
  });

  ngOnInit(): void {
    this.leaderService.getAll().subscribe({
      next: (leaders) => {
        this.leaders.set(leaders);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  getRadarData(leader: LeaderProfile): RadarDataPoint[] {
    return PERSONALITY_DIMENSIONS.map(dim => ({
      axis: dim.high,
      value: leader.personality[dim.key]
    }));
  }

  onSearchChange(value: string): void {
    this.searchQuery.set(value);
  }

  getPeriodClass(period: string): string {
    if (!period || period === 'current') return 'period-current';
    return 'period-historical';
  }
}
