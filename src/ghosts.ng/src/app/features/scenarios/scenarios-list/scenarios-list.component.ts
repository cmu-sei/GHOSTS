import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { ScenarioService } from '../../../core/services';
import { Scenario } from '../../../core/models';

@Component({
  selector: 'app-scenarios-list',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
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

  ngOnInit(): void {
    this.loadScenarios();
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

  protected startScenario(scenario: Scenario, event: Event): void {
    event.stopPropagation();

    if (scenario.startedAt) {
      this.snackBar.open('This scenario has already been started', 'Close', {
        duration: 3000
      });
      return;
    }

    if (confirm(`Are you sure you want to start "${scenario.name}"?`)) {
      this.scenarioService.startScenario(scenario.id).subscribe({
        next: (updatedScenario) => {
          this.snackBar.open('Scenario started successfully', 'Close', {
            duration: 3000
          });
          this.loadScenarios();
        },
        error: (error) => {
          console.error('Error starting scenario', error);
          this.snackBar.open('Error starting scenario', 'Close', {
            duration: 3000
          });
        }
      });
    }
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
