import { ChangeDetectionStrategy, Component, Inject, inject, signal, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { SavedTimelineService, TimelineService } from '../../../core/services';
import { Machine, TimelineHandler, UpdateType } from '../../../core/models';

/** A timeline as a client writes it (its timeline.json), PascalCase */
interface ClientTimeline {
  Id: string;
  TimeLineHandlers?: ClientTimelineHandler[];
}

interface ClientTimelineHandler {
  HandlerType: string;
  Initial?: string | null;
  UtcTimeOn?: string;
  UtcTimeOff?: string;
  HandlerArgs?: Record<string, unknown> | null;
  Loop?: boolean;
  ScheduleType?: 'Other' | 'Cron';
  Schedule?: string | null;
  TimeLineEvents?: {
    TrackableId?: string | null;
    Command?: string | null;
    CommandArgs?: unknown[] | null;
    DelayAfter?: number | null;
    DelayBefore?: number | null;
  }[];
}

interface ReportedTimeline {
  timeline: ClientTimeline;
  reportedUtc: string;
}

@Component({
  selector: 'app-client-timelines-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatTableModule, MatProgressSpinnerModule, DatePipe],
  template: `
    <h2 mat-dialog-title>Client Timelines for {{ machine.name }}</h2>
    <mat-dialog-content>
      <p class="hint">
        Timelines this machine's client has reported. Request them to have the client send its current
        timelines on its next check-in, then refresh.
      </p>
      @if (loading()) {
        <div class="loading">
          <mat-spinner diameter="40"></mat-spinner>
          <p>Loading client timelines...</p>
        </div>
      } @else if (error()) {
        <div class="error">
          <p>{{ error() }}</p>
          <button mat-button (click)="load()">Retry</button>
        </div>
      } @else if (timelines().length === 0) {
        <div class="empty">
          <p>This client hasn't reported any timelines yet.</p>
        </div>
      } @else {
        <table mat-table [dataSource]="timelines()">
          <ng-container matColumnDef="timeline">
            <th mat-header-cell *matHeaderCellDef>Timeline</th>
            <td mat-cell *matCellDef="let row">
              <div class="timeline-id">{{ row.timeline.Id }}</div>
              <div class="handlers">{{ handlerSummary(row.timeline) }}</div>
            </td>
          </ng-container>

          <ng-container matColumnDef="reported">
            <th mat-header-cell *matHeaderCellDef>Reported</th>
            <td mat-cell *matCellDef="let row">{{ row.reportedUtc | date:'short':'UTC' }} UTC</td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let row" class="actions">
              <button mat-button (click)="download(row.timeline)">
                <i class="fas fa-download"></i>
                Download JSON
              </button>
              <button mat-button color="primary" [disabled]="saving()" (click)="saveToLibrary(row.timeline)">
                <i class="fas fa-save"></i>
                Save to library
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
        </table>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [disabled]="requesting()" (click)="requestFromClient()">
        <i class="fas fa-satellite-dish"></i>
        Request from client
      </button>
      <button mat-button (click)="load()">
        <i class="fas fa-sync"></i>
        Refresh
      </button>
      <button mat-stroked-button mat-dialog-close color="primary">Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content {
      min-height: 240px;
      min-width: 640px;
      max-height: 70vh;
      overflow: auto;
    }

    .hint {
      margin: 0 0 12px;
      color: var(--gh-muted);
    }

    .loading, .error, .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 40px 20px;
      color: var(--gh-muted);
    }

    .loading {
      gap: 16px;
    }

    .error {
      gap: 12px;
      color: var(--gh-bad);
    }

    table {
      width: 100%;
    }

    .timeline-id {
      font-family: var(--gh-mono);
      font-size: 12px;
    }

    .handlers {
      font-size: 12px;
      color: var(--gh-muted);
    }

    .actions {
      white-space: nowrap;
      text-align: right;
    }

    mat-dialog-actions {
      padding: 16px 24px 24px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ClientTimelinesDialogComponent implements OnInit {
  protected readonly timelines = signal<ReportedTimeline[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly requesting = signal(false);
  protected readonly saving = signal(false);
  protected readonly displayedColumns = ['timeline', 'reported', 'actions'];
  protected readonly machine: Machine;

  private readonly timelineService = inject(TimelineService);
  private readonly savedTimelineService = inject(SavedTimelineService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly dialogRef = inject(MatDialogRef<ClientTimelinesDialogComponent>);

  constructor(@Inject(MAT_DIALOG_DATA) data: { machine: Machine }) {
    this.machine = data.machine;
  }

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.timelineService.getMachineTimelines(this.machine.id).subscribe({
      next: rows => {
        // Each report adds a row, so a timeline reported twice unchanged shows once, at its newest report.
        // Not keyed on Id: timeline files without one all report the empty Guid.
        const newest = new Map<string, ReportedTimeline>();
        for (const row of [...rows].sort((a, b) => b.id - a.id)) {
          if (!newest.has(row.timeline)) {
            newest.set(row.timeline, { timeline: JSON.parse(row.timeline) as ClientTimeline, reportedUtc: row.createdUtc });
          }
        }
        this.timelines.set([...newest.values()]);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err.message || 'Failed to load client timelines');
        this.loading.set(false);
      }
    });
  }

  protected handlerSummary(timeline: ClientTimeline): string {
    const handlers = timeline.TimeLineHandlers ?? [];
    return handlers.length ? handlers.map(h => h.HandlerType).join('  →  ') : 'No handlers';
  }

  protected requestFromClient(): void {
    this.requesting.set(true);
    this.timelineService.postTimeline({ machineId: this.machine.id, type: UpdateType.RequestForTimeline }).subscribe({
      next: () => {
        this.requesting.set(false);
        this.snackBar.open(`Asked ${this.machine.name} for its timelines; refresh after its next check-in`, undefined, { duration: 4000 });
      },
      error: err => {
        this.requesting.set(false);
        this.snackBar.open(`Failed to request timelines: ${err.message}`, undefined, { duration: 3500 });
      }
    });
  }

  protected download(timeline: ClientTimeline): void {
    const url = URL.createObjectURL(new Blob([JSON.stringify(timeline, null, 2)], { type: 'application/json' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.machine.name}-timeline-${timeline.Id}.json`;
    link.click();
    URL.revokeObjectURL(url);
  }

  protected saveToLibrary(timeline: ClientTimeline): void {
    this.saving.set(true);
    this.savedTimelineService.create({
      name: `${this.machine.name} timeline ${timeline.Id.slice(0, 8)}`,
      timeLineHandlers: (timeline.TimeLineHandlers ?? []).map(h => this.toEditorHandler(h))
    }).subscribe({
      next: saved => {
        this.dialogRef.close();
        this.router.navigate(['/timelines', saved.id, 'edit']);
      },
      error: err => {
        this.saving.set(false);
        this.snackBar.open(`Failed to save timeline: ${err.message}`, undefined, { duration: 3500 });
      }
    });
  }

  private toEditorHandler(h: ClientTimelineHandler): TimelineHandler {
    return {
      handlerType: h.HandlerType,
      initial: h.Initial ?? null,
      utcTimeOn: h.UtcTimeOn,
      utcTimeOff: h.UtcTimeOff,
      handlerArgs: h.HandlerArgs ?? {},
      loop: h.Loop,
      scheduleType: h.ScheduleType,
      schedule: h.Schedule ?? null,
      timeLineEvents: (h.TimeLineEvents ?? []).map(e => ({
        trackableId: e.TrackableId ?? null,
        command: e.Command ?? null,
        commandArgs: e.CommandArgs ?? null,
        delayAfter: e.DelayAfter ?? null,
        delayBefore: e.DelayBefore ?? null
      }))
    };
  }
}
