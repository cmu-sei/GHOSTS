import { Component, OnInit, inject, signal } from '@angular/core';
import { ChangeDetectionStrategy } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DatePipe } from '@angular/common';
import { MachineGroup } from '../../../core/models';
import { MachineGroupService } from '../../../core/services';

@Component({
  selector: 'app-machine-groups-list',
  imports: [
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    DatePipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h1>Machine Groups</h1>
      <button mat-raised-button color="primary">
        <i class="fas fa-plus"></i>
        New Group
      </button>
    </div>

    @if (loading()) {
      <div class="loading">
        <mat-spinner></mat-spinner>
      </div>
    } @else if (error()) {
      <div class="error">
        <p>Error loading machine groups: {{ error() }}</p>
        <button mat-button (click)="loadMachineGroups()">Retry</button>
      </div>
    } @else if (machineGroups().length === 0) {
      <div class="empty-state">
        <i class="fas fa-users-slash"></i>
        <p>No machine groups found.</p>
      </div>
    } @else {
      <div class="table-container">
        <table mat-table [dataSource]="machineGroups()">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let group">{{ group.name }}</td>
          </ng-container>

          <ng-container matColumnDef="machines">
            <th mat-header-cell *matHeaderCellDef>Machines</th>
            <td mat-cell *matCellDef="let group">{{ getMachineCount(group) }}</td>
          </ng-container>

          <ng-container matColumnDef="groups">
            <th mat-header-cell *matHeaderCellDef>Nested Groups</th>
            <td mat-cell *matCellDef="let group">{{ group.groupIds?.length ?? 0 }}</td>
          </ng-container>

          <ng-container matColumnDef="created">
            <th mat-header-cell *matHeaderCellDef>Created</th>
            <td mat-cell *matCellDef="let group">
              {{ group.createdUtc | date:'medium' }}
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef>Actions</th>
            <td mat-cell *matCellDef="let group">
              <button mat-button class="icon-button">
                <i class="fas fa-ellipsis-v"></i>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      </div>
    }
  `,
  styles: [`
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }

    h1 {
      margin: 0;
      font-size: 24px;
      font-weight: 500;
    }

    .loading, .error, .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 48px;
      gap: 16px;
    }

    .error p {
      margin: 0;
      color: #f44336;
      text-align: center;
    }

    .empty-state mat-icon {
      font-size: 48px;
      color: rgba(0, 0, 0, 0.32);
    }

    .table-container {
      background: #ffffff;
      border-radius: 4px;
      box-shadow: 0 2px 4px rgba(0,0,0,0.1);
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
  `]
})
export class MachineGroupsListComponent implements OnInit {
  private readonly machineGroupService = inject(MachineGroupService);

  protected readonly machineGroups = signal<MachineGroup[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly displayedColumns = ['name', 'machines', 'groups', 'created', 'actions'];

  ngOnInit(): void {
    this.loadMachineGroups();
  }

  protected loadMachineGroups(): void {
    this.loading.set(true);
    this.error.set(null);

    this.machineGroupService.getMachineGroups().subscribe({
      next: (groups) => {
        this.machineGroups.set(groups);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load machine groups');
        this.loading.set(false);
      }
    });
  }

  protected getMachineCount(group: MachineGroup): number {
    return group.machines?.length ?? 0;
  }
}
