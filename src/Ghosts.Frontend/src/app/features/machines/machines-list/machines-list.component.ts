import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatMenuModule } from '@angular/material/menu';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ChangeDetectionStrategy } from '@angular/core';
import { Machine, MachineStatus } from '../../../core/models';
import { MachineService, TimelineService } from '../../../core/services';
import { MachineJsonDialogComponent } from '../machine-json-dialog/machine-json-dialog.component';
import { TimelineSelectorDialogComponent } from '../timeline-selector-dialog/timeline-selector-dialog.component';
import { ActivityViewerDialogComponent } from '../activity-viewer-dialog/activity-viewer-dialog.component';
import { SearchBarComponent } from '../../../shared/components/search-bar/search-bar.component';

@Component({
  selector: 'app-machines-list',
  imports: [
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatMenuModule,
    MatDialogModule,
    MatSnackBarModule,
    SearchBarComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h1>Machines</h1>
      <button mat-raised-button color="primary">
        <i class="fas fa-plus"></i>
        New Machine
      </button>
    </div>

    <div class="search-container">
      <app-search-bar (searchChange)="onSearchChange($event)"></app-search-bar>
      @if (searchTerm()) {
        <div class="search-results-info">
          Showing {{ filteredMachines().length }} of {{ machines().length }} machines
        </div>
      }
    </div>

    @if (loading()) {
      <div class="loading">
        <mat-spinner></mat-spinner>
      </div>
    } @else if (error()) {
      <div class="error">
        <p>Error loading machines: {{ error() }}</p>
        <button mat-button (click)="loadMachines()">Retry</button>
      </div>
    } @else {
      <div class="table-container">
        <table mat-table [dataSource]="filteredMachines()">
          <ng-container matColumnDef="id">
            <th mat-header-cell *matHeaderCellDef>ID</th>
            <td mat-cell *matCellDef="let machine">{{ machine.id }}</td>
          </ng-container>

          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Name</th>
            <td mat-cell *matCellDef="let machine">{{ machine.name }}</td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let machine">
              <mat-chip [class]="'status-' + getStatusClass(machine.status)">
                {{ machine.status || 'Unknown' }}
              </mat-chip>
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef>Actions</th>
            <td mat-cell *matCellDef="let machine">
              <button
                mat-button
                class="icon-button"
                [matMenuTriggerFor]="actionsMenu"
                aria-label="Machine actions">
                <i class="fas fa-ellipsis-v"></i>
              </button>
              <mat-menu #actionsMenu="matMenu">
                <button mat-menu-item (click)="runTimeline(machine)">
                  <i class="fas fa-play"></i>
                  <span>Run Timeline</span>
                </button>
                <button mat-menu-item (click)="viewActivity(machine)">
                  <i class="fas fa-chart-line"></i>
                  <span>View Activity</span>
                </button>
                <button mat-menu-item (click)="viewJson(machine)">
                  <i class="fas fa-code"></i>
                  <span>View Raw JSON</span>
                </button>
                <button mat-menu-item (click)="deleteMachine(machine)">
                  <i class="fas fa-trash" style="color: #f44336;"></i>
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
      color: rgba(0, 0, 0, 0.6);
      font-size: 14px;
    }

    .loading, .error {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 48px;
    }

    .table-container {
      background: white;
      border-radius: 4px;
      box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    }

    table {
      width: 100%;
    }

    mat-chip,
    mat-chip *,
    mat-chip::before,
    mat-chip::after {
      border: none !important;
      outline: none !important;
      box-shadow: none !important;
    }

    mat-chip {
      --mdc-chip-outline-width: 0 !important;
      --mdc-chip-outline-color: transparent !important;
      --mdc-chip-flat-outline-color: transparent !important;
      --mdc-chip-flat-outline-width: 0 !important;
      --mat-chip-outline-width: 0 !important;
      --mat-chip-outline-color: transparent !important;
      --mdc-chip-container-height: 24px !important;
      border-width: 0 !important;
      border-style: none !important;
    }

    mat-chip::part(action),
    mat-chip .mat-mdc-chip-action,
    mat-chip .mdc-evolution-chip__action,
    mat-chip .mat-mdc-chip-graphic,
    mat-chip .mdc-evolution-chip__graphic,
    mat-chip span {
      border: none !important;
      outline: none !important;
      box-shadow: none !important;
    }

    .status-up,
    .status-active {
      --mdc-chip-elevated-container-color: #4caf50 !important;
      --mdc-chip-label-text-color: white !important;
      --mat-chip-elevated-container-color: #4caf50 !important;
      --mat-chip-label-text-color: white !important;
      --mdc-chip-outline-width: 0 !important;
      --mdc-chip-outline-color: transparent !important;
      --mdc-chip-flat-outline-color: transparent !important;
      background-color: #4caf50 !important;
      color: white !important;
      border: none !important;
      border-width: 0 !important;
      box-shadow: none !important;
    }

    .status-down {
      --mdc-chip-elevated-container-color: #f44336 !important;
      --mdc-chip-label-text-color: white !important;
      --mat-chip-elevated-container-color: #f44336 !important;
      --mat-chip-label-text-color: white !important;
      --mdc-chip-outline-width: 0 !important;
      --mdc-chip-outline-color: transparent !important;
      --mdc-chip-flat-outline-color: transparent !important;
      background-color: #f44336 !important;
      color: white !important;
      border: none !important;
      border-width: 0 !important;
      box-shadow: none !important;
    }

    .status-upwitherrors {
      --mdc-chip-elevated-container-color: #ff9800 !important;
      --mdc-chip-label-text-color: white !important;
      --mat-chip-elevated-container-color: #ff9800 !important;
      --mat-chip-label-text-color: white !important;
      --mdc-chip-outline-width: 0 !important;
      --mdc-chip-outline-color: transparent !important;
      --mdc-chip-flat-outline-color: transparent !important;
      background-color: #ff9800 !important;
      color: white !important;
      border: none !important;
      border-width: 0 !important;
      box-shadow: none !important;
    }

    .status-downwitherrors {
      --mdc-chip-elevated-container-color: #ff5722 !important;
      --mdc-chip-label-text-color: white !important;
      --mat-chip-elevated-container-color: #ff5722 !important;
      --mat-chip-label-text-color: white !important;
      --mdc-chip-outline-width: 0 !important;
      --mdc-chip-outline-color: transparent !important;
      --mdc-chip-flat-outline-color: transparent !important;
      background-color: #ff5722 !important;
      color: white !important;
      border: none !important;
      border-width: 0 !important;
      box-shadow: none !important;
    }

    .status-unknown {
      --mdc-chip-elevated-container-color: #9e9e9e !important;
      --mdc-chip-label-text-color: white !important;
      --mat-chip-elevated-container-color: #9e9e9e !important;
      --mat-chip-label-text-color: white !important;
      --mdc-chip-outline-width: 0 !important;
      --mdc-chip-outline-color: transparent !important;
      --mdc-chip-flat-outline-color: transparent !important;
      background-color: #9e9e9e !important;
      color: white !important;
      border: none !important;
      border-width: 0 !important;
      box-shadow: none !important;
    }
  `]
})
export class MachinesListComponent implements OnInit {
  private readonly machineService = inject(MachineService);
  private readonly timelineService = inject(TimelineService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly machines = signal<Machine[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly displayedColumns = ['id', 'name', 'status', 'actions'];

  protected readonly filteredMachines = computed(() => {
    const search = this.searchTerm().toLowerCase().trim();
    if (!search) {
      return this.machines();
    }
    return this.machines().filter(machine =>
      machine.name?.toLowerCase().includes(search) ||
      machine.id?.toString().includes(search) ||
      machine.status?.toLowerCase().includes(search)
    );
  });

  ngOnInit(): void {
    this.loadMachines();
  }

  protected loadMachines(): void {
    this.loading.set(true);
    this.error.set(null);

    this.machineService.getMachines().subscribe({
      next: (machines) => {
        this.machines.set(machines);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load machines');
        this.loading.set(false);
      }
    });
  }

  protected onSearchChange(searchTerm: string): void {
    this.searchTerm.set(searchTerm);
  }

  protected getStatusClass(status: MachineStatus | undefined): string {
    return (status || 'unknown').toLowerCase();
  }

  protected deleteMachine(machine: Machine): void {
    const confirmed = window.confirm(`Delete machine "${machine.name}"?`);
    if (!confirmed) {
      return;
    }

    this.machineService.deleteMachine(machine.id).subscribe({
      next: () => {
        this.snackBar.open(`Machine "${machine.name}" deleted`, undefined, { duration: 2500 });
        this.loadMachines();
      },
      error: (err) => {
        this.snackBar.open(`Failed to delete machine: ${err.message}`, undefined, { duration: 3500 });
      }
    });
  }

  protected runTimeline(machine: Machine): void {
    const dialogRef = this.dialog.open(TimelineSelectorDialogComponent, {
      width: '500px',
      data: { machine },
      autoFocus: false,
      restoreFocus: false
    });

    dialogRef.afterClosed().subscribe(selectedTimeline => {
      if (selectedTimeline) {
        this.timelineService.postTimeline({
          machineId: machine.id,
          timeline: {
            name: selectedTimeline.name,
            timeLineHandlers: selectedTimeline.timeLineHandlers
          }
        }).subscribe({
          next: () => {
            this.snackBar.open(`Timeline "${selectedTimeline.name}" started on ${machine.name}`, undefined, { duration: 3000 });
          },
          error: (err) => {
            this.snackBar.open(`Failed to start timeline: ${err.message}`, undefined, { duration: 3500 });
          }
        });
      }
    });
  }

  protected viewActivity(machine: Machine): void {
    this.dialog.open(ActivityViewerDialogComponent, {
      width: '800px',
      data: { machine },
      autoFocus: false,
      restoreFocus: false
    });
  }

  protected viewJson(machine: Machine): void {
    this.dialog.open(MachineJsonDialogComponent, {
      width: '720px',
      data: machine,
      autoFocus: false,
      restoreFocus: false
    });
  }
}
