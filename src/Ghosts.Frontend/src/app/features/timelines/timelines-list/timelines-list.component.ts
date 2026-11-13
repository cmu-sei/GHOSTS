import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { SavedTimelineService } from '../../../core/services';
import { SavedTimeline } from '../../../core/models';
import { TimelineJsonDialogComponent } from '../timeline-json-dialog/timeline-json-dialog.component';
import { SearchBarComponent } from '../../../shared/components/search-bar/search-bar.component';

@Component({
  selector: 'app-timelines-list',
  standalone: true,
  imports: [
    RouterLink,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
    MatSnackBarModule,
    MatDialogModule,
    SearchBarComponent
  ],
  template: `
    <section class="timelines-container">
      <header class="page-header">
        <div>
          <h1>Timelines</h1>
          <p class="subtitle">Build, edit, and reuse timeline definitions for machines and groups.</p>
        </div>
        <div class="header-actions">
          <button mat-raised-button color="primary" [routerLink]="['/timelines', 'new']">
            <i class="fas fa-plus"></i>
            New timeline
          </button>
        </div>
      </header>

      <div class="search-container">
        <app-search-bar (searchChange)="onSearchChange($event)"></app-search-bar>
        @if (searchTerm()) {
          <div class="search-results-info">
            Showing {{ filteredTimelines().length }} of {{ timelines().length }} timelines
          </div>
        }
      </div>

      @if (loading()) {
        <div class="loading-state">
          <i class="fas fa-sync fa-spin"></i>
          <p>Loading timelines...</p>
        </div>
      } @else if (timelines().length === 0) {
        <div class="empty-state">
          <i class="fas fa-stream"></i>
          <h3>No timelines yet</h3>
          <p>Create a timeline to start orchestrating machine activity sequences.</p>
          <button mat-stroked-button color="primary" [routerLink]="['/timelines', 'new']">
            <i class="fas fa-plus"></i>
            Create your first timeline
          </button>
        </div>
      } @else {
        <div class="table-wrapper">
          <table mat-table [dataSource]="filteredTimelines()" class="mat-elevation-z2">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Name</th>
              <td mat-cell *matCellDef="let timeline">
                <div class="name-cell">
                  <span class="timeline-name">{{ timeline.name }}</span>
                  <span class="handler-count">{{ timeline.timeLineHandlers.length }} handler(s)</span>
                </div>
              </td>
            </ng-container>

            <ng-container matColumnDef="handlers">
              <th mat-header-cell *matHeaderCellDef>Handler flow</th>
              <td mat-cell *matCellDef="let timeline">
                <div class="handler-flow" [matTooltip]="getHandlerTooltip(timeline)">
                  {{ getHandlerSummary(timeline) }}
                </div>
              </td>
            </ng-container>

            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef>Actions</th>
              <td mat-cell *matCellDef="let timeline">
                <button
                  mat-button
                  class="icon-button"
                  [matMenuTriggerFor]="actionsMenu"
                  aria-label="Timeline actions">
                  <i class="fas fa-ellipsis-v"></i>
                </button>
                <mat-menu #actionsMenu="matMenu">
                  <button mat-menu-item (click)="navigateToEdit(timeline.id)">
                    <i class="fas fa-edit"></i>
                    <span>Edit</span>
                  </button>
                  <button mat-menu-item (click)="cloneTimeline(timeline)">
                    <i class="fas fa-copy"></i>
                    <span>Duplicate</span>
                  </button>
                  <button mat-menu-item (click)="openJson(timeline)">
                    <i class="fas fa-code"></i>
                    <span>View JSON</span>
                  </button>
                  <button mat-menu-item (click)="deleteTimeline(timeline)">
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
    </section>
  `,
  styles: [`
    .timelines-container {
      display: flex;
      flex-direction: column;
      gap: 24px;
    }

    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      flex-wrap: wrap;
      gap: 16px;
    }

    h1 {
      margin: 0;
      font-size: 28px;
      font-weight: 600;
    }

    .subtitle {
      margin: 4px 0 0;
      color: var(--gh-muted);
    }

    .header-actions {
      display: flex;
      gap: 12px;
      align-items: center;
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

    .table-wrapper {
      overflow: auto;
      border-radius: 8px;
      background: var(--gh-surface);
    }

    table {
      width: 100%;
      min-width: 720px;
    }

    th.mat-header-cell {
      font-weight: 600;
    }

    td.mat-cell, th.mat-header-cell {
      padding: 16px 20px;
    }

    .name-cell {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .timeline-name {
      font-weight: 600;
    }

    .handler-count {
      font-size: 12px;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--gh-muted);
    }

    .handler-flow {
      max-width: 480px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      color: var(--gh-body);
    }

    .empty-state, .loading-state {
      padding: 64px 24px;
      background: var(--gh-surface);
      border-radius: 8px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      text-align: center;
      color: var(--gh-muted);
    }

    .empty-state mat-icon,
    .loading-state mat-icon {
      font-size: 48px;
      color: var(--gh-faint);
      animation: pulse 2.4s ease-in-out infinite;
    }

    @keyframes pulse {
      0% { transform: scale(1); opacity: 0.6; }
      50% { transform: scale(1.1); opacity: 1; }
      100% { transform: scale(1); opacity: 0.6; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TimelinesListComponent implements OnInit {
  protected readonly displayedColumns = ['name', 'handlers', 'actions'];
  protected readonly timelines = signal<SavedTimeline[]>([]);
  protected readonly loading = signal(true);
  protected readonly searchTerm = signal('');

  protected readonly filteredTimelines = computed(() => {
    const search = this.searchTerm().toLowerCase().trim();
    if (!search) {
      return this.timelines();
    }
    return this.timelines().filter(timeline =>
      timeline.name?.toLowerCase().includes(search) ||
      timeline.timeLineHandlers.some(handler =>
        handler.handlerType?.toLowerCase().includes(search)
      )
    );
  });

  private readonly savedTimelineService = inject(SavedTimelineService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  ngOnInit(): void {
    this.refresh();
  }

  protected onSearchChange(searchTerm: string): void {
    this.searchTerm.set(searchTerm);
  }

  protected refresh(): void {
    this.loading.set(true);
    this.savedTimelineService.getAll().subscribe({
      next: data => {
        this.timelines.set(data);
        this.loading.set(false);
      },
      error: err => {
        this.snackBar.open(`Failed to load timelines: ${err.message}`, undefined, { duration: 3500 });
        this.loading.set(false);
      }
    });
  }

  protected getHandlerSummary(timeline: SavedTimeline): string {
    if (!timeline.timeLineHandlers.length) {
      return 'No handlers configured';
    }
    return timeline.timeLineHandlers
      .map(handler => handler.handlerType || 'Unknown')
      .join('  →  ');
  }

  protected getHandlerTooltip(timeline: SavedTimeline): string {
    return timeline.timeLineHandlers
      .map((handler, index) => `${index + 1}. ${handler.handlerType || 'Unknown handler'}`)
      .join('\\n');
  }

  protected navigateToEdit(id: string): void {
    this.router.navigate(['/timelines', id, 'edit']);
  }

  protected cloneTimeline(timeline: SavedTimeline): void {
    this.savedTimelineService.create({
      name: `${timeline.name} copy`,
      timeLineHandlers: timeline.timeLineHandlers
    }).subscribe({
      next: () => this.refresh(),
      error: err => this.snackBar.open(`Failed to duplicate timeline: ${err.message}`, undefined, { duration: 3500 })
    });
  }

  protected openJson(timeline: SavedTimeline): void {
    this.dialog.open(TimelineJsonDialogComponent, {
      width: '720px',
      data: timeline,
      autoFocus: false,
      restoreFocus: false
    });
  }

  protected deleteTimeline(timeline: SavedTimeline): void {
    const confirmed = window.confirm(`Delete timeline "${timeline.name}"?`);
    if (!confirmed) {
      return;
    }
    this.savedTimelineService.delete(timeline).subscribe({
      next: () => this.refresh(),
      error: err => this.snackBar.open(`Failed to delete timeline: ${err.message}`, undefined, { duration: 3500 })
    });
  }
}
