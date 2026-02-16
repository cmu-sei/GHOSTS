import { ChangeDetectionStrategy, Component, Inject, inject, signal, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DatePipe } from '@angular/common';
import { MachineService } from '../../../core/services';
import { Machine } from '../../../core/models';

@Component({
  selector: 'app-activity-viewer-dialog',
  standalone: true,
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatTableModule,
    MatProgressSpinnerModule,
    DatePipe
  ],
  template: `
    <h2 mat-dialog-title>Activity for {{ machine.name }}</h2>
    <mat-dialog-content>
      @if (loading()) {
        <div class="loading">
          <mat-spinner diameter="40"></mat-spinner>
          <p>Loading activities...</p>
        </div>
      } @else if (error()) {
        <div class="error">
          <p>{{ error() }}</p>
          <button mat-button (click)="loadActivities()">Retry</button>
        </div>
      } @else if (activities().length === 0) {
        <div class="empty">
          <p>No activities recorded yet.</p>
        </div>
      } @else {
        <div class="table-container">
          <table mat-table [dataSource]="activities()">
            <ng-container matColumnDef="timestamp">
              <th mat-header-cell *matHeaderCellDef>Timestamp</th>
              <td mat-cell *matCellDef="let activity">
                {{ activity.timestamp | date:'short' }}
              </td>
            </ng-container>

            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef>Type</th>
              <td mat-cell *matCellDef="let activity">{{ activity.type || 'N/A' }}</td>
            </ng-container>

            <ng-container matColumnDef="details">
              <th mat-header-cell *matHeaderCellDef>Details</th>
              <td mat-cell *matCellDef="let activity">
                {{ activity.details || activity.description || 'N/A' }}
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
          </table>
        </div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button mat-dialog-close color="primary">Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content {
      min-height: 300px;
      min-width: 600px;
      max-height: 70vh;
      overflow: auto;
    }

    .loading, .error, .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 40px 20px;
      color: rgba(0, 0, 0, 0.6);
    }

    .loading {
      gap: 16px;
    }

    .error {
      gap: 12px;
      color: #f44336;
    }

    .table-container {
      background: white;
      border-radius: 4px;
      overflow: auto;
    }

    table {
      width: 100%;
    }

    th.mat-header-cell {
      font-weight: 600;
    }

    td.mat-cell, th.mat-header-cell {
      padding: 12px 16px;
    }

    mat-dialog-actions {
      padding: 16px 24px 24px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ActivityViewerDialogComponent implements OnInit {
  protected readonly activities = signal<any[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly displayedColumns = ['timestamp', 'type', 'details'];
  protected readonly machine: Machine;

  private readonly machineService = inject(MachineService);

  constructor(@Inject(MAT_DIALOG_DATA) data: { machine: Machine }) {
    this.machine = data.machine;
  }

  ngOnInit(): void {
    this.loadActivities();
  }

  protected loadActivities(): void {
    this.loading.set(true);
    this.error.set(null);

    this.machineService.getMachineActivity(this.machine.id, 0, 50).subscribe({
      next: (activities) => {
        this.activities.set(activities);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load activities');
        this.loading.set(false);
      }
    });
  }
}
