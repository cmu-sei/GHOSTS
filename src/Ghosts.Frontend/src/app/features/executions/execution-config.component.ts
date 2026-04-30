import { Component, signal, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { Scenario } from '../../core/models';
import { ScenarioService } from '../../core/services';
import { ExecutionService } from '../../core/services/execution.service';

@Component({
  selector: 'app-execution-config',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './execution-config.component.html',
  styleUrls: ['./execution-config.component.scss'],
})
export class ExecutionConfigComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly scenarioService = inject(ScenarioService);
  private readonly executionService = inject(ExecutionService);
  private readonly snackBar = inject(MatSnackBar);

  protected scenario = signal<Scenario | null>(null);
  protected loading = signal(true);
  protected submitting = signal(false);

  protected name = signal('');
  protected description = signal('');
  protected autoStart = signal(false);

  ngOnInit(): void {
    const scenarioId = this.route.snapshot.queryParamMap.get('scenarioId');
    if (!scenarioId) {
      this.snackBar.open('Scenario ID is required', 'Close', { duration: 3000 });
      this.router.navigate(['/scenarios']);
      return;
    }

    this.loadScenario(Number(scenarioId));
  }

  private loadScenario(scenarioId: number): void {
    this.loading.set(true);
    this.scenarioService.getScenario(scenarioId).subscribe({
      next: (scenario) => {
        this.scenario.set(scenario);
        this.name.set(`${scenario.name} - Run 1`);
        this.description.set(`Execution of ${scenario.name}`);
        this.loading.set(false);
      },
      error: () => {
        this.snackBar.open('Failed to load scenario', 'Close', { duration: 3000 });
        this.router.navigate(['/scenarios']);
      },
    });
  }

  protected onCancel(): void {
    this.router.navigate(['/scenarios']);
  }

  protected onExecute(): void {
    if (!this.name().trim()) {
      this.snackBar.open('Execution name is required', 'Close', { duration: 3000 });
      return;
    }

    const scenario = this.scenario();
    if (!scenario) return;

    this.submitting.set(true);

    this.executionService
      .createExecution({
        scenarioId: scenario.id,
        name: this.name(),
        description: this.description(),
        configuration: JSON.stringify({ autoStart: this.autoStart() }),
      })
      .subscribe({
        next: (execution) => {
          this.snackBar.open('Execution created successfully', 'Close', { duration: 3000 });

          if (this.autoStart()) {
            this.executionService.startExecution(execution.id).subscribe({
              next: () => {
                this.snackBar.open('Execution started', 'Close', { duration: 2000 });
                this.router.navigate(['/executions', execution.id]);
              },
              error: () => {
                this.snackBar.open('Execution created but failed to start', 'Close', { duration: 3000 });
                this.router.navigate(['/executions', execution.id]);
              },
            });
          } else {
            this.router.navigate(['/executions', execution.id]);
          }
        },
        error: () => {
          this.snackBar.open('Error creating execution', 'Close', { duration: 3000 });
          this.submitting.set(false);
        },
      });
  }
}
