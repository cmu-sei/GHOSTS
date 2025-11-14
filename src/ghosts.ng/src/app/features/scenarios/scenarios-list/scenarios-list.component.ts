import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioService } from '../../../core/services';
import { Scenario } from '../../../core/models';
import { SearchBarComponent } from '../../../shared/components/search-bar/search-bar.component';

@Component({
  selector: 'app-scenarios-list',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    SearchBarComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scenarios-list.component.html',
  styleUrls: ['./scenarios-list.component.scss']
})
export class ScenariosListComponent implements OnInit {
  private readonly scenarioService = inject(ScenarioService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly scenarios = signal<Scenario[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly searchTerm = signal('');

  protected readonly filteredScenarios = computed(() => {
    const search = this.searchTerm().toLowerCase().trim();
    if (!search) {
      return this.scenarios();
    }
    return this.scenarios().filter(scenario =>
      scenario.name?.toLowerCase().includes(search) ||
      scenario.description?.toLowerCase().includes(search)
    );
  });

  ngOnInit(): void {
    this.loadScenarios();
  }

  protected onSearchChange(searchTerm: string): void {
    this.searchTerm.set(searchTerm);
  }

  protected loadScenarios(): void {
    this.loading.set(true);
    this.error.set(null);

    this.scenarioService.getScenarios().subscribe({
      next: (scenarios) => {
        this.scenarios.set(scenarios);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading scenarios', error);
        this.error.set('Failed to load scenarios. Make sure the API is running.');
        this.loading.set(false);
      }
    });
  }

  protected createNewScenario(): void {
    this.router.navigate(['/scenarios', 'new']);
  }

  protected editScenario(id: number): void {
    this.router.navigate(['/scenarios', id]);
  }

  protected deleteScenario(scenario: Scenario, event: Event): void {
    event.stopPropagation();

    if (confirm(`Are you sure you want to delete "${scenario.name}"?`)) {
      this.scenarioService.deleteScenario(scenario.id).subscribe({
        next: () => {
          this.snackBar.open('Scenario deleted successfully', 'Close', {
            duration: 3000
          });
          this.loadScenarios();
        },
        error: (error) => {
          console.error('Error deleting scenario', error);
          this.snackBar.open('Error deleting scenario', 'Close', {
            duration: 3000
          });
        }
      });
    }
  }

  protected executeScenario(scenario: Scenario, event: Event): void {
    event.stopPropagation();
    this.router.navigate(['/executions/new'], {
      queryParams: { scenarioId: scenario.id }
    });
  }

  protected formatDate(date: Date): string {
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }
}
