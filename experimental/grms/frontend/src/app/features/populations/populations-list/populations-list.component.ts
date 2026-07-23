import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { GlassCardComponent } from '../../../shared/components/glass-card/glass-card.component';
import { PopulationService } from '../../../core/services/population.service';
import { PopulationProfile } from '../../../core/models/population.model';
import { PopulationCreateDialogComponent } from './population-create-dialog.component';
import { CountryFlagPipe } from '../../../shared/pipes/country-flag.pipe';

@Component({
  selector: 'app-populations-list',
  standalone: true,
  imports: [
    DecimalPipe,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatExpansionModule,
    MatDialogModule,
    GlassCardComponent,
    CountryFlagPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './populations-list.component.html',
  styleUrl: './populations-list.component.scss',
})
export class PopulationsListComponent implements OnInit {
  private readonly populationService = inject(PopulationService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  populations = signal<PopulationProfile[]>([]);
  searchQuery = signal('');
  loading = signal(true);
  error = signal<string | null>(null);

  filteredPopulations = computed(() => {
    const query = this.searchQuery().toLowerCase();
    if (!query) return this.populations();
    return this.populations().filter(p =>
      p.country.toLowerCase().includes(query) ||
      (p.period && p.period.toLowerCase().includes(query))
    );
  });

  populationsByPeriod = computed(() => {
    const groups: { period: string; label: string; populations: PopulationProfile[] }[] = [];
    const map = new Map<string, PopulationProfile[]>();
    for (const p of this.filteredPopulations()) {
      const period = p.period || 'current';
      if (!map.has(period)) map.set(period, []);
      map.get(period)!.push(p);
    }
    for (const [period, pops] of map) {
      groups.push({ period, label: period.charAt(0).toUpperCase() + period.slice(1), populations: pops });
    }
    groups.sort((a, b) => a.period === 'current' ? -1 : b.period === 'current' ? 1 : a.period.localeCompare(b.period));
    return groups;
  });

  ngOnInit(): void {
    this.populationService.getAll().subscribe({
      next: (populations) => {
        this.populations.set(populations);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load populations');
        this.loading.set(false);
      },
    });
  }

  viewDetail(country: string): void {
    this.router.navigate(['/populations', country]);
  }

  openCreateDialog(): void {
    const dialogRef = this.dialog.open(PopulationCreateDialogComponent, {
      width: '500px',
      panelClass: 'glass-dialog',
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.populations.update(list => [...list, result]);
      }
    });
  }
}
