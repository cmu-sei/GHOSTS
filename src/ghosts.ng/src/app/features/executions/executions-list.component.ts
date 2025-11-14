import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatChipsModule } from '@angular/material/chips';
import { ChangeDetectionStrategy } from '@angular/core';
import { ExecutionService } from '../../core/services/execution.service';
import { ExecutionSummary } from '../../core/models/execution.model';
import { SearchBarComponent } from '../../shared/components/search-bar/search-bar.component';

@Component({
  selector: 'app-executions-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatChipsModule,
    SearchBarComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './executions-list.component.html',
  styleUrls: ['./executions-list.component.scss'],
})
export class ExecutionsListComponent implements OnInit {
  private readonly executionService = inject(ExecutionService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  executions = signal<ExecutionSummary[]>([]);
  searchTerm = signal('');
  selectedScenario = signal<number | undefined>(undefined);
  loading = signal(false);

  protected filteredExecutions = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    if (!term) {
      return this.executions();
    }
    return this.executions().filter(
      (execution) =>
        execution.name.toLowerCase().includes(term) ||
        execution.scenarioName.toLowerCase().includes(term) ||
        execution.status.toLowerCase().includes(term)
    );
  });

  protected onSearchChange(searchTerm: string): void {
    this.searchTerm.set(searchTerm);
  }

  ngOnInit(): void {
    this.loadExecutions();
  }

  protected loadExecutions(): void {
    this.loading.set(true);
    this.executionService.getExecutions(this.selectedScenario()).subscribe({
      next: (executions) => {
        this.executions.set(executions);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading executions:', error);
        this.snackBar.open('Failed to load executions', 'Close', { duration: 3000 });
        this.loading.set(false);
      },
    });
  }

  protected viewExecution(id: number): void {
    this.router.navigate(['/executions', id]);
  }

  protected deleteExecution(execution: ExecutionSummary, event: Event): void {
    event.stopPropagation();

    if (
      confirm(
        `Are you sure you want to delete execution "${execution.name}"?`
      )
    ) {
      this.executionService.deleteExecution(execution.id).subscribe({
        next: () => {
          this.executions.update((executions) =>
            executions.filter((e) => e.id !== execution.id)
          );
          this.snackBar.open('Execution deleted successfully', 'Close', { duration: 3000 });
        },
        error: (error) => {
          console.error('Error deleting execution:', error);
          this.snackBar.open('Failed to delete execution', 'Close', { duration: 3000 });
        },
      });
    }
  }

  protected startExecution(id: number, event: Event): void {
    event.stopPropagation();

    this.executionService.startExecution(id).subscribe({
      next: () => {
        this.loadExecutions();
        this.snackBar.open('Execution started', 'Close', { duration: 2000 });
      },
      error: (error) => {
        console.error('Error starting execution:', error);
        this.snackBar.open('Failed to start execution', 'Close', { duration: 3000 });
      },
    });
  }

  protected pauseExecution(id: number, event: Event): void {
    event.stopPropagation();

    this.executionService.pauseExecution(id).subscribe({
      next: () => {
        this.loadExecutions();
        this.snackBar.open('Execution paused', 'Close', { duration: 2000 });
      },
      error: (error) => {
        console.error('Error pausing execution:', error);
        this.snackBar.open('Failed to pause execution', 'Close', { duration: 3000 });
      },
    });
  }

  protected stopExecution(id: number, event: Event): void {
    event.stopPropagation();

    if (confirm('Are you sure you want to stop this execution?')) {
      this.executionService.stopExecution(id).subscribe({
        next: () => {
          this.loadExecutions();
          this.snackBar.open('Execution stopped', 'Close', { duration: 2000 });
        },
        error: (error) => {
          console.error('Error stopping execution:', error);
          this.snackBar.open('Failed to stop execution', 'Close', { duration: 3000 });
        },
      });
    }
  }

  protected cancelExecution(id: number, event: Event): void {
    event.stopPropagation();

    if (confirm('Are you sure you want to cancel this execution?')) {
      this.executionService.cancelExecution(id).subscribe({
        next: () => {
          this.loadExecutions();
          this.snackBar.open('Execution cancelled', 'Close', { duration: 2000 });
        },
        error: (error) => {
          console.error('Error cancelling execution:', error);
          this.snackBar.open('Failed to cancel execution', 'Close', { duration: 3000 });
        },
      });
    }
  }

  protected formatDate(dateString?: string): string {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  protected formatDuration(seconds?: number): string {
    if (!seconds) return 'N/A';
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;

    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    } else if (minutes > 0) {
      return `${minutes}m ${secs}s`;
    } else {
      return `${secs}s`;
    }
  }

  protected getStatusIcon(status: string): string {
    switch (status) {
      case 'Running':
        return 'fa-play-circle';
      case 'Paused':
        return 'fa-pause-circle';
      case 'Completed':
        return 'fa-check-circle';
      case 'Failed':
        return 'fa-times-circle';
      case 'Cancelled':
        return 'fa-ban';
      default:
        return 'fa-circle';
    }
  }

  protected getStatusColor(status: string): string {
    switch (status) {
      case 'Running':
        return 'status-running';
      case 'Paused':
        return 'status-paused';
      case 'Completed':
        return 'status-completed';
      case 'Failed':
        return 'status-failed';
      case 'Cancelled':
        return 'status-cancelled';
      default:
        return 'status-created';
    }
  }

  protected canStart(status: string): boolean {
    return status === 'Created' || status === 'Paused';
  }

  protected canPause(status: string): boolean {
    return status === 'Running';
  }

  protected canStop(status: string): boolean {
    return status === 'Running' || status === 'Paused';
  }

  protected canDelete(status: string): boolean {
    return (
      status === 'Created' ||
      status === 'Completed' ||
      status === 'Failed' ||
      status === 'Cancelled'
    );
  }

  protected isTerminal(status: string): boolean {
    return (
      status === 'Completed' || status === 'Failed' || status === 'Cancelled'
    );
  }
}
