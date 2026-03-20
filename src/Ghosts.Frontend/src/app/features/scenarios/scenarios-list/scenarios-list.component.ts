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

  protected exportScenario(scenario: Scenario, event: Event): void {
    event.stopPropagation();

    const markdown = this.buildScenarioMarkdown(scenario);
    const blob = new Blob([markdown], { type: 'text/markdown;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.slugify(scenario.name)}.md`;
    link.click();
    URL.revokeObjectURL(url);
    link.remove();
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

  private buildScenarioMarkdown(scenario: Scenario): string {
    const lines: string[] = [];
    lines.push(`# ${scenario.name}`);
    lines.push('');
    lines.push(scenario.description || 'No description provided.');
    lines.push('');
    lines.push(`- **Updated:** ${this.formatDate(scenario.updatedAt)}`);
    lines.push('');

    if (scenario.scenarioParameters) {
      lines.push('## Scenario Parameters');
      lines.push('');
      const { nations = [], threatActors = [], injects = [], userPools = [] } = scenario.scenarioParameters;

      if (nations.length) {
        lines.push('### Nations');
        nations.forEach(nation => lines.push(`- ${nation.name} (${nation.alignment})`));
        lines.push('');
      }

      if (threatActors.length) {
        lines.push('### Threat Actors');
        threatActors.forEach(actor => {
          const ttps = actor.ttps?.length ? ` | TTPs: ${actor.ttps.join(', ')}` : '';
          lines.push(`- ${actor.name} (${actor.type}) - Capability ${actor.capability}${ttps}`);
        });
        lines.push('');
      }

      if (injects.length) {
        lines.push('### Injects');
        injects.forEach(inject => lines.push(`- **${inject.trigger}**: ${inject.title}`));
        lines.push('');
      }

      if (userPools.length) {
        lines.push('### User Pools');
        userPools.forEach(pool => lines.push(`- ${pool.role}: ${pool.count}`));
        lines.push('');
      }
    }

    if (scenario.simulationMechanics) {
      const sim = scenario.simulationMechanics;
      lines.push('## Simulation Mechanics');
      lines.push('');
      lines.push(`- Timeline: ${sim.timelineType}`);
      lines.push(`- Duration: ${sim.durationHours} hours`);
      lines.push(`- Adjudication: ${sim.adjudicationType}`);
      if (sim.escalationLadder) {
        lines.push(`- Escalation Ladder: ${sim.escalationLadder}`);
      }
      if (sim.branchingLogic) {
        lines.push(`- Branching Logic: ${sim.branchingLogic}`);
      }
      if (sim.performanceMetrics) {
        lines.push(`- Performance Metrics: ${sim.performanceMetrics}`);
      }
      lines.push('');
    }

    if (scenario.timeline?.events?.length) {
      lines.push('## Timeline Events');
      lines.push('');
      scenario.timeline.events.forEach(event => {
        lines.push(`- **${event.time}** (${event.assigned}) - ${event.description} [${event.status}]`);
      });
      lines.push('');
    }

    return lines.join('\n').trim() + '\n';
  }

  private slugify(value: string): string {
    return value?.toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
      .replace(/-{2,}/g, '-') || 'scenario';
  }
}
