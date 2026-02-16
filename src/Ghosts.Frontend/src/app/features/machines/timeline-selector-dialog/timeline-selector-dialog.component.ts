import { ChangeDetectionStrategy, Component, Inject, inject, signal, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TimelineLocalService } from '../../../core/services';
import { LocalTimeline } from '../../../core/models';
import { Machine } from '../../../core/models';

@Component({
  selector: 'app-timeline-selector-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatListModule, MatProgressSpinnerModule],
  template: `
    <h2 mat-dialog-title>Select Timeline for {{ machine.name }}</h2>
    <mat-dialog-content>
      @if (loading()) {
        <div class="loading">
          <mat-spinner diameter="40"></mat-spinner>
          <p>Loading timelines...</p>
        </div>
      } @else if (timelines().length === 0) {
        <div class="empty">
          <p>No timelines available. Create a timeline first.</p>
        </div>
      } @else {
        <mat-selection-list [multiple]="false" (selectionChange)="onSelectionChange($event)">
          @for (timeline of timelines(); track timeline.id) {
            <mat-list-option [value]="timeline">
              <div class="timeline-item">
                <span class="timeline-name">{{ timeline.name }}</span>
                <span class="timeline-handlers">{{ timeline.timeLineHandlers.length }} handler(s)</span>
              </div>
            </mat-list-option>
          }
        </mat-selection-list>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button
        mat-raised-button
        color="primary"
        [disabled]="!selectedTimeline()"
        (click)="runTimeline()">
        <i class="fas fa-play"></i>
        Run Timeline
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content {
      min-height: 200px;
      min-width: 400px;
      padding: 20px 0;
    }

    .loading, .empty {
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

    .timeline-item {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .timeline-name {
      font-weight: 500;
      font-size: 14px;
    }

    .timeline-handlers {
      font-size: 12px;
      color: rgba(0, 0, 0, 0.54);
    }

    mat-dialog-actions {
      padding: 16px 24px 24px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TimelineSelectorDialogComponent implements OnInit {
  protected readonly timelines = signal<LocalTimeline[]>([]);
  protected readonly loading = signal(true);
  protected readonly selectedTimeline = signal<LocalTimeline | null>(null);
  protected readonly machine: Machine;

  private readonly timelineLocalService = inject(TimelineLocalService);
  private readonly dialogRef = inject(MatDialogRef<TimelineSelectorDialogComponent>);

  constructor(@Inject(MAT_DIALOG_DATA) data: { machine: Machine }) {
    this.machine = data.machine;
  }

  ngOnInit(): void {
    this.loadTimelines();
  }

  private loadTimelines(): void {
    this.loading.set(true);
    const data = this.timelineLocalService.getAll();
    this.timelines.set(data);
    this.loading.set(false);
  }

  protected onSelectionChange(event: any): void {
    const selected = event.source.selectedOptions.selected[0]?.value;
    this.selectedTimeline.set(selected || null);
  }

  protected runTimeline(): void {
    const timeline = this.selectedTimeline();
    if (timeline) {
      this.dialogRef.close(timeline);
    }
  }
}
