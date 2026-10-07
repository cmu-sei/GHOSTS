import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatMenuModule } from '@angular/material/menu';
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
    MatMenuModule,
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

  protected createNewScenarioManual(): void {
    this.router.navigate(['/scenarios', 'new']);
  }

  protected createNewScenario(): void {
    const name = `Scenario ${new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}`;

    this.scenarioService.createScenario({
      name,
      description: '',
      scenarioParameters: { nations: [], threatActors: [], injects: [], userPools: [], objectives: '', politicalContext: '', rulesOfEngagement: '', victoryConditions: '' },
      technicalEnvironment: { networkTopology: '', services: '', assets: '', defenses: [], vulnerabilities: [] },
      timeline: { exerciseDuration: 8, events: [] }
    }).subscribe({
      next: (scenario) => this.router.navigate(['/scenarios', scenario.id, 'builder']),
      error: () => this.snackBar.open('Failed to create scenario', 'Close', { duration: 3000 })
    });
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

  protected openBuilder(id: number, event: Event): void {
    event.stopPropagation();
    this.router.navigate(['/scenarios', id, 'builder']);
  }

  protected executeScenario(scenario: Scenario, event: Event): void {
    event.stopPropagation();
    this.router.navigate(['/executions/new'], {
      queryParams: { scenarioId: scenario.id }
    });
  }

  protected publishScenario(scenario: Scenario, event: Event): void {
    event.stopPropagation();
    this.scenarioService.publishScenario(scenario.id).subscribe({
      next: () => {
        this.snackBar.open(`${scenario.name} is published.`, 'Close', { duration: 3000 });
        this.loadScenarios();
      },
      error: (error) => {
        const message = error?.error?.error ?? 'Error publishing scenario';
        this.snackBar.open(message, 'Close', { duration: 5000 });
      }
    });
  }

  /** Downloads the scenario as its scenario document, the form the validator checks and the import reads. */
  protected exportScenario(scenario: Scenario, event: Event): void {
    event.stopPropagation();
    this.download(this.scenarioService.getScenarioDocument(scenario.id), `${this.slugify(scenario.name)}.scenario.json`, 'application/json');
  }

  /** Downloads the scenario as an exercise plan: the document rendered as Markdown by the server. */
  protected exportPlan(scenario: Scenario, event: Event): void {
    event.stopPropagation();
    this.download(this.scenarioService.getScenarioPlan(scenario.id), `${this.slugify(scenario.name)}.plan.md`, 'text/markdown');
  }

  private download(text$: Observable<string>, filename: string, type: string): void {
    text$.subscribe({
      next: (text) => {
        const url = URL.createObjectURL(new Blob([text], { type }));
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.snackBar.open(`${filename} could not be exported.`, 'Close', { duration: 5000 })
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

  private slugify(value: string): string {
    return value?.toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
      .replace(/-{2,}/g, '-') || 'scenario';
  }
}
