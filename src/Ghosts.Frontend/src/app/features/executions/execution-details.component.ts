import { Component, OnInit, signal, inject, OnDestroy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { ExecutionService } from '../../core/services/execution.service';
import { ExecutionHubService, ExecutionHubMessage } from '../../core/services/execution-hub.service';
import { N8nWorkflowService } from '../../core/services/n8n-workflow.service';
import {
  Execution,
  ExecutionEvent,
  ExecutionMetricSnapshot,
  ExecutionTimelineItem,
} from '../../core/models/execution.model';
import { N8nWorkflow } from '../../core/models';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-execution-details',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatSnackBarModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatChipsModule,
  ],
  templateUrl: './execution-details.component.html',
  styleUrls: ['./execution-details.component.scss'],
})
export class ExecutionDetailsComponent implements OnInit, OnDestroy {
  private readonly executionService = inject(ExecutionService);
  private readonly executionHub = inject(ExecutionHubService);
  private readonly n8nWorkflowService = inject(N8nWorkflowService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  execution = signal<Execution | null>(null);
  events = signal<ExecutionEvent[]>([]);
  metrics = signal<ExecutionMetricSnapshot[]>([]);
  timelineItems = signal<ExecutionTimelineItem[]>([]);
  workflows = signal<N8nWorkflow[]>([]);
  loading = signal(false);

  // Manual completion dialog state
  completionDialogOpen = signal(false);
  completionItem = signal<ExecutionTimelineItem | null>(null);
  completionStatus = signal<'Completed' | 'Failed' | 'Skipped'>('Completed');
  completionNotes = signal('');
  completionBy = signal('exercise-admin');

  private hubSub?: Subscription;
  private executionId = 0;

  protected itemCounts = computed(() => {
    const items = this.timelineItems();
    return {
      total: items.length,
      pending: items.filter(i => i.status === 'Pending').length,
      queued: items.filter(i => i.status === 'Queued' || i.status === 'Deployed').length,
      completed: items.filter(i => i.status === 'Completed').length,
      failed: items.filter(i => i.status === 'Failed').length,
      skipped: items.filter(i => i.status === 'Skipped').length,
      manual: items.filter(i => i.automationKind === 'Manual').length,
      workflow: items.filter(i => i.automationKind === 'Workflow').length,
      automated: items.filter(i => i.automationKind === 'MachineUpdate').length,
    };
  });

  ngOnInit(): void {
    this.executionId = Number(this.route.snapshot.paramMap.get('id'));
    if (this.executionId) {
      this.loadAll();
      this.connectWebSocket();
    }
    this.n8nWorkflowService.getActiveWorkflows().subscribe({
      next: (wfs) => this.workflows.set(wfs),
      error: () => this.workflows.set([]),
    });
  }

  ngOnDestroy(): void {
    this.hubSub?.unsubscribe();
    this.executionHub.disconnect();
  }

  private loadAll(): void {
    this.loadExecution(this.executionId);
    this.loadEvents(this.executionId);
    this.loadMetrics(this.executionId);
    this.loadTimelineItems(this.executionId);
  }

  private connectWebSocket(): void {
    this.executionHub.connect(this.executionId);
    this.hubSub = this.executionHub.updates$.subscribe((msg: ExecutionHubMessage) => {
      if (msg.updateType === 'StatusChange') {
        this.snackBar.open(`Execution status: ${msg.data.status}`, 'Close', { duration: 3000 });
        this.loadExecution(this.executionId);
        this.loadEvents(this.executionId);

        if (this.isTerminal(msg.data.status)) {
          this.executionHub.disconnect();
        }
      } else if (msg.updateType === 'TimelineItemUpdate') {
        this.snackBar.open(`Timeline item updated: ${msg.data.status}`, 'Close', { duration: 2000 });
        this.loadTimelineItems(this.executionId);
        this.loadEvents(this.executionId);
      }
    });
  }

  loadExecution(id: number): void {
    this.loading.set(true);
    this.executionService.getExecution(id).subscribe({
      next: (execution) => {
        this.execution.set(execution);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  loadEvents(id: number): void {
    this.executionService.getExecutionEvents(id, undefined, 100).subscribe({
      next: (events) => this.events.set(events),
    });
  }

  loadMetrics(id: number): void {
    this.executionService.getExecutionMetrics(id, 100).subscribe({
      next: (metrics) => this.metrics.set(metrics),
    });
  }

  loadTimelineItems(id: number): void {
    this.executionService.getTimelineItems(id).subscribe({
      next: (items) => this.timelineItems.set(items),
    });
  }

  startExecution(): void {
    const execution = this.execution();
    if (!execution) return;

    this.executionService.startExecution(execution.id).subscribe({
      next: (updated) => {
        this.execution.set(updated);
        this.loadTimelineItems(execution.id);
        this.loadEvents(execution.id);
        this.snackBar.open('Execution started', 'Close', { duration: 2000 });
      },
      error: (error) => {
        this.snackBar.open('Failed to start: ' + (error.error?.error || ''), 'Close', { duration: 3000 });
      },
    });
  }

  pauseExecution(): void {
    const execution = this.execution();
    if (!execution) return;

    this.executionService.pauseExecution(execution.id).subscribe({
      next: (updated) => {
        this.execution.set(updated);
        this.snackBar.open('Execution paused', 'Close', { duration: 2000 });
      },
      error: (error) => {
        this.snackBar.open('Failed to pause: ' + (error.error?.error || ''), 'Close', { duration: 3000 });
      },
    });
  }

  stopExecution(): void {
    const execution = this.execution();
    if (!execution) return;

    if (confirm('Are you sure you want to stop this execution?')) {
      this.executionService.stopExecution(execution.id).subscribe({
        next: (updated) => {
          this.execution.set(updated);
          this.snackBar.open('Execution stopped', 'Close', { duration: 2000 });
        },
        error: (error) => {
          this.snackBar.open('Failed to stop: ' + (error.error?.error || ''), 'Close', { duration: 3000 });
        },
      });
    }
  }

  cancelExecution(): void {
    const execution = this.execution();
    if (!execution) return;

    if (confirm('Are you sure you want to cancel this execution?')) {
      this.executionService.cancelExecution(execution.id).subscribe({
        next: (updated) => {
          this.execution.set(updated);
          this.snackBar.open('Execution cancelled', 'Close', { duration: 2000 });
        },
        error: (error) => {
          this.snackBar.open('Failed to cancel: ' + (error.error?.error || ''), 'Close', { duration: 3000 });
        },
      });
    }
  }

  deleteExecution(): void {
    const execution = this.execution();
    if (!execution) return;

    if (confirm(`Are you sure you want to delete execution "${execution.name}"?`)) {
      this.executionService.deleteExecution(execution.id).subscribe({
        next: () => this.router.navigate(['/executions']),
        error: (error) => {
          this.snackBar.open('Failed to delete: ' + (error.error?.error || ''), 'Close', { duration: 3000 });
        },
      });
    }
  }

  // Manual completion dialog
  openCompletionDialog(item: ExecutionTimelineItem): void {
    this.completionItem.set(item);
    this.completionStatus.set('Completed');
    this.completionNotes.set('');
    this.completionBy.set('exercise-admin');
    this.completionDialogOpen.set(true);
  }

  closeCompletionDialog(): void {
    this.completionDialogOpen.set(false);
    this.completionItem.set(null);
  }

  submitCompletion(): void {
    const item = this.completionItem();
    const execution = this.execution();
    if (!item || !execution) return;

    this.executionService
      .completeTimelineItem(execution.id, item.id, {
        status: this.completionStatus(),
        notes: this.completionNotes() || undefined,
        completedBy: this.completionBy() || 'exercise-admin',
      })
      .subscribe({
        next: () => {
          this.snackBar.open(`Item #${item.number} marked ${this.completionStatus()}`, 'Close', { duration: 2000 });
          this.closeCompletionDialog();
          this.loadTimelineItems(execution.id);
          this.loadEvents(execution.id);
          this.loadExecution(execution.id);
        },
        error: (error) => {
          this.snackBar.open('Failed: ' + (error.error?.error || ''), 'Close', { duration: 3000 });
        },
      });
  }

  // Helpers
  formatDate(dateString?: string): string {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString();
  }

  formatDuration(): string {
    const execution = this.execution();
    if (!execution || !execution.startedAt) return 'N/A';

    const start = new Date(execution.startedAt).getTime();
    const end = execution.completedAt
      ? new Date(execution.completedAt).getTime()
      : Date.now();
    const seconds = Math.floor((end - start) / 1000);

    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;

    if (hours > 0) return `${hours}h ${minutes}m ${secs}s`;
    if (minutes > 0) return `${minutes}m ${secs}s`;
    return `${secs}s`;
  }

  formatJson(jsonString: string): string {
    try {
      return JSON.stringify(JSON.parse(jsonString), null, 2);
    } catch {
      return jsonString;
    }
  }

  parseJson(jsonString: string): any {
    try {
      return JSON.parse(jsonString);
    } catch {
      return {};
    }
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Running': return 'badge-success';
      case 'Paused': return 'badge-warning';
      case 'Completed': return 'badge-info';
      case 'Failed': return 'badge-danger';
      case 'Cancelled': return 'badge-secondary';
      default: return 'badge-light';
    }
  }

  getItemStatusClass(status: string): string {
    switch (status) {
      case 'Completed': return 'item-completed';
      case 'Failed': return 'item-failed';
      case 'Skipped': return 'item-skipped';
      case 'Deployed': return 'item-deployed';
      case 'Queued': return 'item-queued';
      default: return 'item-pending';
    }
  }

  getSeverityClass(severity: string): string {
    switch (severity.toLowerCase()) {
      case 'error': return 'bg-danger text-white';
      case 'warning': return 'bg-warning text-dark';
      case 'info': return 'bg-info text-white';
      default: return 'bg-secondary text-white';
    }
  }

  canStart(status: string): boolean {
    return status === 'Created' || status === 'Paused';
  }

  canPause(status: string): boolean {
    return status === 'Running';
  }

  canStop(status: string): boolean {
    return status === 'Running' || status === 'Paused';
  }

  canDelete(status: string): boolean {
    return status === 'Created' || status === 'Completed' || status === 'Failed' || status === 'Cancelled';
  }

  isTerminal(status: string): boolean {
    return status === 'Completed' || status === 'Failed' || status === 'Cancelled';
  }

  canCompleteItem(item: ExecutionTimelineItem): boolean {
    return !['Completed', 'Failed', 'Skipped'].includes(item.status);
  }

  isItemTerminal(status: string): boolean {
    return ['Completed', 'Failed', 'Skipped'].includes(status);
  }

  getWorkflowName(workflowId?: string): string {
    if (!workflowId) return 'Workflow';
    const wf = this.workflows().find(w => w.id === workflowId);
    return wf ? wf.name : workflowId;
  }
}
