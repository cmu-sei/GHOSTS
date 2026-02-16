import { Component, signal, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSliderModule } from '@angular/material/slider';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { Scenario } from '../../core/models';
import { ScenarioService } from '../../core/services';
import { ExecutionService } from '../../core/services/execution.service';

export interface ExecutionConfig {
  name: string;
  description: string;
  timeScaleMultiplier: number;
  activityProfiles: {
    heavyUser: number;
    regularUser: number;
    casualUser: number;
    lurker: number;
  };
  engagementAffinityWeight: number;
  viralThresholdMultiplier: number;
  topicAlignmentWeight: number;
  feedSize: number;
  randomSeed: number | null;
  autoStart: boolean;
}

@Component({
  selector: 'app-execution-config',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSliderModule,
    MatCheckboxModule,
    MatTooltipModule,
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

  protected config = signal<ExecutionConfig>({
    name: '',
    description: '',
    timeScaleMultiplier: 1.0,
    activityProfiles: {
      heavyUser: 20,
      regularUser: 50,
      casualUser: 25,
      lurker: 5,
    },
    engagementAffinityWeight: 0.5,
    viralThresholdMultiplier: 1.0,
    topicAlignmentWeight: 0.7,
    feedSize: 30,
    randomSeed: null,
    autoStart: false,
  });

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
        // Set default name based on scenario
        this.config.update((c) => ({
          ...c,
          name: `${scenario.name} - Run 1`,
          description: `Execution of ${scenario.name}`,
        }));
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading scenario:', error);
        this.snackBar.open('Failed to load scenario', 'Close', { duration: 3000 });
        this.router.navigate(['/scenarios']);
      },
    });
  }

  protected get activityProfileTotal(): number {
    const profiles = this.config().activityProfiles;
    return (
      profiles.heavyUser +
      profiles.regularUser +
      profiles.casualUser +
      profiles.lurker
    );
  }

  protected get activityProfileValid(): boolean {
    return Math.abs(this.activityProfileTotal - 100) < 0.01;
  }

  protected normalizeActivityProfiles(): void {
    const total = this.activityProfileTotal;
    if (total === 0) return;

    this.config.update((c) => ({
      ...c,
      activityProfiles: {
        heavyUser: (c.activityProfiles.heavyUser / total) * 100,
        regularUser: (c.activityProfiles.regularUser / total) * 100,
        casualUser: (c.activityProfiles.casualUser / total) * 100,
        lurker: (c.activityProfiles.lurker / total) * 100,
      },
    }));
  }

  protected resetToDefaults(): void {
    this.config.update((c) => ({
      ...c,
      timeScaleMultiplier: 1.0,
      activityProfiles: {
        heavyUser: 20,
        regularUser: 50,
        casualUser: 25,
        lurker: 5,
      },
      engagementAffinityWeight: 0.5,
      viralThresholdMultiplier: 1.0,
      topicAlignmentWeight: 0.7,
      feedSize: 30,
      randomSeed: null,
    }));
  }

  protected generateRandomSeed(): void {
    this.config.update((c) => ({
      ...c,
      randomSeed: Math.floor(Math.random() * 1000000),
    }));
  }

  protected onCancel(): void {
    this.router.navigate(['/scenarios']);
  }

  protected onExecute(): void {
    if (!this.activityProfileValid) {
      this.snackBar.open('Activity profiles must total 100%', 'Close', { duration: 3000 });
      return;
    }

    if (!this.config().name.trim()) {
      this.snackBar.open('Execution name is required', 'Close', { duration: 3000 });
      return;
    }

    const scenario = this.scenario();
    if (!scenario) return;

    this.submitting.set(true);

    const config = this.config();
    const parameterOverrides = {
      timeScaleMultiplier: config.timeScaleMultiplier,
      activityProfiles: config.activityProfiles,
      engagementAffinityWeight: config.engagementAffinityWeight,
      viralThresholdMultiplier: config.viralThresholdMultiplier,
      topicAlignmentWeight: config.topicAlignmentWeight,
      feedSize: config.feedSize,
      randomSeed: config.randomSeed,
    };

    const configuration = {
      autoStart: config.autoStart,
    };

    this.executionService
      .createExecution({
        scenarioId: scenario.id,
        name: config.name,
        description: config.description,
        parameterOverrides: JSON.stringify(parameterOverrides),
        configuration: JSON.stringify(configuration),
      })
      .subscribe({
        next: (execution) => {
          this.snackBar.open('Execution created successfully', 'Close', {
            duration: 3000,
          });

          // Auto-start if requested
          if (config.autoStart) {
            this.executionService.startExecution(execution.id).subscribe({
              next: () => {
                this.snackBar.open('Execution started', 'Close', {
                  duration: 2000,
                });
                this.router.navigate(['/executions', execution.id]);
              },
              error: (error) => {
                console.error('Error starting execution', error);
                this.snackBar.open('Execution created but failed to start', 'Close', {
                  duration: 3000,
                });
                this.router.navigate(['/executions', execution.id]);
              },
            });
          } else {
            this.router.navigate(['/executions', execution.id]);
          }
        },
        error: (error) => {
          console.error('Error creating execution', error);
          this.snackBar.open('Error creating execution', 'Close', {
            duration: 3000,
          });
          this.submitting.set(false);
        },
      });
  }

  protected formatPercentage(value: number): string {
    return `${value.toFixed(1)}%`;
  }

  protected formatMultiplier(value: number): string {
    return `${value.toFixed(2)}x`;
  }

  protected formatWeight(value: number): string {
    return value.toFixed(2);
  }
}
