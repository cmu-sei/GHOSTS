import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { ChangeDetectionStrategy } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatMenuModule } from '@angular/material/menu';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MachineGroup, SavedTimeline, UpdateType } from '../../../core/models';
import { MachineGroupService } from '../../../core/services';
import { SearchBarComponent } from '../../../shared/components/search-bar/search-bar.component';
import { TimelineSelectorDialogComponent } from '../../machines/timeline-selector-dialog/timeline-selector-dialog.component';
import { ActivityViewerDialogComponent } from '../../machines/activity-viewer-dialog/activity-viewer-dialog.component';
import { ManageMachinesDialogComponent } from '../manage-machines-dialog/manage-machines-dialog.component';

@Component({
  selector: 'app-machine-groups-list',
  imports: [
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatMenuModule,
    MatDialogModule,
    MatSnackBarModule,
    SearchBarComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h1>Machine Groups</h1>
      <button mat-raised-button color="primary" (click)="createGroup()">
        <i class="fas fa-plus"></i>
        New Group
      </button>
    </div>

    <div class="search-container">
      <app-search-bar (searchChange)="onSearchChange($event)"></app-search-bar>
      @if (searchTerm()) {
        <div class="search-results-info">
          Showing {{ filteredMachineGroups().length }} of {{ machineGroups().length }} groups
        </div>
      }
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
        <button mat-button (click)="createGroup()">Create a group</button>
      </div>
    } @else {
      <div class="table-container">
        <table mat-table [dataSource]="filteredMachineGroups()">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let group">{{ group.name }}</td>
          </ng-container>

          <ng-container matColumnDef="machines">
            <th mat-header-cell *matHeaderCellDef>Machines</th>
            <td mat-cell *matCellDef="let group">{{ getMachineCount(group) }}</td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef>Actions</th>
            <td mat-cell *matCellDef="let group">
              <button
                mat-button
                class="icon-button"
                [matMenuTriggerFor]="actionsMenu"
                aria-label="Group actions">
                <i class="fas fa-ellipsis-v"></i>
              </button>
              <mat-menu #actionsMenu="matMenu">
                <button mat-menu-item (click)="applyTimeline(group)" [disabled]="getMachineCount(group) === 0">
                  <i class="fas fa-play"></i>
                  <span>Apply Timeline</span>
                </button>
                <button mat-menu-item (click)="manageMachines(group)">
                  <i class="fas fa-desktop"></i>
                  <span>Manage Machines</span>
                </button>
                <button mat-menu-item (click)="viewActivity(group)">
                  <i class="fas fa-chart-line"></i>
                  <span>View Activity</span>
                </button>
                <button mat-menu-item (click)="renameGroup(group)">
                  <i class="fas fa-pen"></i>
                  <span>Rename</span>
                </button>
                <button mat-menu-item (click)="deleteGroup(group)">
                  <i class="fas fa-trash" style="color: var(--gh-bad);"></i>
                  <span>Delete</span>
                </button>
              </mat-menu>
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

    .search-container {
      margin-bottom: 24px;
      display: flex;
      align-items: center;
      gap: 16px;
    }

    .search-results-info {
      color: var(--gh-muted);
      font-size: 14px;
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
      color: var(--gh-bad);
      text-align: center;
    }

    .empty-state mat-icon {
      font-size: 48px;
      color: var(--gh-faint);
    }

    .table-container {
      background: var(--gh-surface);
      border-radius: 4px;
      box-shadow: 0 2px 4px rgb(var(--gh-shadow) / 0.1);
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
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly machineGroups = signal<MachineGroup[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly displayedColumns = ['name', 'machines', 'actions'];

  protected readonly filteredMachineGroups = computed(() => {
    const search = this.searchTerm().toLowerCase().trim();
    if (!search) {
      return this.machineGroups();
    }
    return this.machineGroups().filter(group =>
      group.name?.toLowerCase().includes(search) ||
      group.id?.toString().includes(search)
    );
  });

  ngOnInit(): void {
    this.loadMachineGroups();
  }

  protected onSearchChange(searchTerm: string): void {
    this.searchTerm.set(searchTerm);
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
    return group.groupMachines?.length ?? 0;
  }

  protected createGroup(): void {
    const name = window.prompt('New group name')?.trim();
    if (!name) {
      return;
    }

    this.machineGroupService.createMachineGroup({ name, groupMachines: [] }).subscribe({
      next: () => {
        this.notify(`Group "${name}" created`);
        this.loadMachineGroups();
      },
      error: err => this.notify(`Failed to create group: ${this.errorText(err)}`)
    });
  }

  protected renameGroup(group: MachineGroup): void {
    const name = window.prompt('Group name', group.name)?.trim();
    if (!name || name === group.name) {
      return;
    }
    this.saveGroup(group, { name }, `Group renamed to "${name}"`);
  }

  protected manageMachines(group: MachineGroup): void {
    this.dialog.open(ManageMachinesDialogComponent, {
      width: '500px',
      data: { group },
      autoFocus: false,
      restoreFocus: false
    }).afterClosed().subscribe((machineIds?: string[]) => {
      if (machineIds) {
        this.saveGroup(group, { machineIds }, `Group "${group.name}" now has ${machineIds.length} machine(s)`);
      }
    });
  }

  protected applyTimeline(group: MachineGroup): void {
    this.dialog.open(TimelineSelectorDialogComponent, {
      width: '500px',
      data: { targetName: group.name },
      autoFocus: false,
      restoreFocus: false
    }).afterClosed().subscribe((timeline?: SavedTimeline) => {
      if (!timeline) {
        return;
      }
      this.machineGroupService.applyTimeline(group.id, {
        type: UpdateType.TimelinePartial,
        update: { id: timeline.id, name: timeline.name, timeLineHandlers: timeline.timeLineHandlers }
      }).subscribe({
        next: () => this.notify(
          `Timeline "${timeline.name}" queued for ${this.getMachineCount(group)} machine(s) in ${group.name}; each runs it on its next check-in`),
        error: err => this.notify(`Failed to apply timeline: ${this.errorText(err)}`)
      });
    });
  }

  protected viewActivity(group: MachineGroup): void {
    this.dialog.open(ActivityViewerDialogComponent, {
      width: '1200px',
      maxWidth: '95vw',
      data: { targetName: group.name, activity$: this.machineGroupService.getMachineGroupActivity(group.id, 0, 50) },
      autoFocus: false,
      restoreFocus: false
    });
  }

  protected deleteGroup(group: MachineGroup): void {
    if (!window.confirm(`Delete group "${group.name}"? Its machines are not deleted.`)) {
      return;
    }

    this.machineGroupService.deleteMachineGroup(group.id).subscribe({
      next: () => {
        this.notify(`Group "${group.name}" deleted`);
        this.loadMachineGroups();
      },
      error: err => this.notify(`Failed to delete group: ${this.errorText(err)}`)
    });
  }

  /** PUT replaces the machine list, so a rename must resend the current members */
  private saveGroup(group: MachineGroup, change: { name?: string; machineIds?: string[] }, success: string): void {
    const machineIds = change.machineIds ?? (group.groupMachines ?? []).map(gm => gm.machineId);
    this.machineGroupService.updateMachineGroup(group.id, {
      id: group.id,
      name: change.name ?? group.name,
      status: group.status,
      groupMachines: machineIds.map(machineId => ({ machineId }))
    }).subscribe({
      next: () => {
        this.notify(success);
        this.loadMachineGroups();
      },
      error: err => this.notify(`Failed to update group: ${this.errorText(err)}`)
    });
  }

  private notify(message: string): void {
    this.snackBar.open(message, undefined, { duration: 3500 });
  }

  private errorText(err: { error?: unknown; message?: string }): string {
    return typeof err.error === 'string' ? err.error : err.message ?? 'unknown error';
  }
}
