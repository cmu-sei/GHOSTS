import { Component, OnInit, signal, inject, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { ExecutionService } from '../../core/services/execution.service';
import {
  Execution,
  ExecutionEvent,
  ExecutionMetricSnapshot,
} from '../../core/models/execution.model';
import { interval, Subscription } from 'rxjs';
import { switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-execution-details',
  standalone: true,
  imports: [CommonModule, RouterModule, MatButtonModule, MatProgressSpinnerModule, MatTabsModule],
  templateUrl: './execution-details.component.html',
  styleUrls: ['./execution-details.component.scss'],
})
export class ExecutionDetailsComponent implements OnInit, OnDestroy {
  private readonly executionService = inject(ExecutionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  execution = signal<Execution | null>(null);
  events = signal<ExecutionEvent[]>([]);
  metrics = signal<ExecutionMetricSnapshot[]>([]);
  loading = signal(false);

  private refreshSubscription?: Subscription;

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (id) {
      this.loadExecution(id);
      this.loadEvents(id);
      this.loadMetrics(id);

      // Auto-refresh if execution is running
      this.startAutoRefresh(id);
    }
  }

  ngOnDestroy(): void {
    this.stopAutoRefresh();
  }

  private startAutoRefresh(id: number): void {
    this.refreshSubscription = interval(5000)
      .pipe(
        switchMap(() => this.executionService.getExecution(id))
      )
      .subscribe({
        next: (execution) => {
          this.execution.set(execution);

          // Stop auto-refresh if execution reaches terminal state
          if (this.isTerminal(execution.status)) {
            this.stopAutoRefresh();
            // Reload events and metrics one last time
            this.loadEvents(id);
            this.loadMetrics(id);
          }
        },
        error: (error) => {
          console.error('Error refreshing execution:', error);
        },
      });
  }

  private stopAutoRefresh(): void {
    this.refreshSubscription?.unsubscribe();
  }

  loadExecution(id: number): void {
    this.loading.set(true);
    this.executionService.getExecution(id).subscribe({
      next: (execution) => {
        this.execution.set(execution);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading execution:', error);
        this.loading.set(false);
      },
    });
  }

  loadEvents(id: number): void {
    this.executionService.getExecutionEvents(id, undefined, 100).subscribe({
      next: (events) => {
        this.events.set(events);
      },
      error: (error) => {
        console.error('Error loading events:', error);
      },
    });
  }

  loadMetrics(id: number): void {
    this.executionService.getExecutionMetrics(id, 100).subscribe({
      next: (metrics) => {
        this.metrics.set(metrics);
      },
      error: (error) => {
        console.error('Error loading metrics:', error);
      },
    });
  }

  startExecution(): void {
    const execution = this.execution();
    if (!execution) return;

    this.executionService.startExecution(execution.id).subscribe({
      next: (updated) => {
        this.execution.set(updated);
        this.startAutoRefresh(execution.id);
      },
      error: (error) => {
        console.error('Error starting execution:', error);
        alert('Failed to start execution. ' + (error.error?.error || ''));
      },
    });
  }

  pauseExecution(): void {
    const execution = this.execution();
    if (!execution) return;

    this.executionService.pauseExecution(execution.id).subscribe({
      next: (updated) => {
        this.execution.set(updated);
      },
      error: (error) => {
        console.error('Error pausing execution:', error);
        alert('Failed to pause execution. ' + (error.error?.error || ''));
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
          this.stopAutoRefresh();
        },
        error: (error) => {
          console.error('Error stopping execution:', error);
          alert('Failed to stop execution. ' + (error.error?.error || ''));
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
          this.stopAutoRefresh();
        },
        error: (error) => {
          console.error('Error cancelling execution:', error);
          alert('Failed to cancel execution. ' + (error.error?.error || ''));
        },
      });
    }
  }

  deleteExecution(): void {
    const execution = this.execution();
    if (!execution) return;

    if (
      confirm(
        `Are you sure you want to delete execution "${execution.name}"?`
      )
    ) {
      this.executionService.deleteExecution(execution.id).subscribe({
        next: () => {
          this.router.navigate(['/executions']);
        },
        error: (error) => {
          console.error('Error deleting execution:', error);
          alert('Failed to delete execution. ' + (error.error?.error || ''));
        },
      });
    }
  }

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

    if (hours > 0) {
      return `${hours}h ${minutes}m ${secs}s`;
    } else if (minutes > 0) {
      return `${minutes}m ${secs}s`;
    } else {
      return `${secs}s`;
    }
  }

  parseJson(jsonString: string): any {
    try {
      return JSON.parse(jsonString);
    } catch {
      return {};
    }
  }

  formatJson(jsonString: string): string {
    try {
      return JSON.stringify(JSON.parse(jsonString), null, 2);
    } catch {
      return jsonString;
    }
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Running':
        return 'badge-success';
      case 'Paused':
        return 'badge-warning';
      case 'Completed':
        return 'badge-info';
      case 'Failed':
        return 'badge-danger';
      case 'Cancelled':
        return 'badge-secondary';
      default:
        return 'badge-light';
    }
  }

  getSeverityClass(severity: string): string {
    switch (severity.toLowerCase()) {
      case 'error':
        return 'bg-danger text-white';
      case 'warning':
        return 'bg-warning text-dark';
      case 'info':
        return 'bg-info text-white';
      default:
        return 'bg-secondary text-white';
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
    return (
      status === 'Created' ||
      status === 'Completed' ||
      status === 'Failed' ||
      status === 'Cancelled'
    );
  }

  isTerminal(status: string): boolean {
    return (
      status === 'Completed' || status === 'Failed' || status === 'Cancelled'
    );
  }
}
