import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { N8nWorkflowService } from '../../../core/services';
import { N8nWorkflow } from '../../../core/models';

const CRON_EXPRESSION = /^(@(yearly|monthly|weekly|daily|hourly|reboot))|((\S+\s+){4}\S+)$/i;

@Component({
  selector: 'app-n8n-workflow-runner',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatSelectModule,
    MatFormFieldModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatInputModule,
    DatePipe
  ],
  templateUrl: './n8n-workflow-runner.component.html',
  styleUrls: ['./n8n-workflow-runner.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class N8nWorkflowRunnerComponent implements OnInit {
  private readonly workflowService = inject(N8nWorkflowService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly workflows = signal<N8nWorkflow[]>([]);
  protected readonly loadingWorkflows = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly lastUpdated = signal<Date | null>(null);
  protected readonly executing = signal(false);

  protected readonly runForm = this.fb.group({
    workflowId: [null as string | number | null, Validators.required],
    cronSchedule: [
      '*/5 * * * *',
      [Validators.required, Validators.pattern(CRON_EXPRESSION)]
    ]
  });

  ngOnInit(): void {
    this.loadWorkflows();
  }

  protected loadWorkflows(forceRefresh = false): void {
    this.loadingWorkflows.set(true);
    this.loadError.set(null);

    this.workflowService.getActiveWorkflows(forceRefresh).subscribe({
      next: (workflows) => {
        this.workflows.set(workflows);
        this.lastUpdated.set(new Date());

        if (!this.runForm.value.workflowId && workflows.length > 0) {
          this.runForm.patchValue({ workflowId: workflows[0].id });
        }

        this.loadingWorkflows.set(false);
      },
      error: (err) => {
        this.loadError.set(err?.message || 'Failed to load workflows');
        this.loadingWorkflows.set(false);
      }
    });
  }

  protected refreshWorkflows(): void {
    this.loadWorkflows(true);
  }

  protected runWorkflow(): void {
    if (this.runForm.invalid) {
      this.snackBar.open('Workflow and schedule are required', 'Close', { duration: 3000 });
      return;
    }

    const workflowId = this.runForm.value.workflowId;
    const cronSchedule = this.runForm.value.cronSchedule?.trim();

    if (!workflowId) {
      this.snackBar.open('Invalid workflow selected', 'Close', { duration: 3000 });
      return;
    }

    if (!cronSchedule) {
      this.snackBar.open('Please provide a cron schedule', 'Close', { duration: 3000 });
      return;
    }

    this.executing.set(true);

    this.workflowService.runWorkflow(workflowId, cronSchedule).subscribe({
      next: (response) => {
        const message = response.executionId
          ? `Workflow schedule ${response.executionId} saved`
          : 'Workflow schedule saved';

        this.snackBar.open(message, 'Close', { duration: 4000 });
      },
      error: (err) => {
        this.snackBar.open(
          err?.message || 'Failed to trigger workflow',
          'Close',
          { duration: 5000 }
        );
        this.executing.set(false);
      },
      complete: () => {
        this.executing.set(false);
      }
    });
  }
}
