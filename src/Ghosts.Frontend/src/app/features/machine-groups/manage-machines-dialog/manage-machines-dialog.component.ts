import { ChangeDetectionStrategy, Component, Inject, inject, signal, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule, MatSelectionListChange } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Machine, MachineGroup } from '../../../core/models';
import { MachineService } from '../../../core/services';

/** Picks the group's machines; closes with the chosen machine ids, or nothing on cancel */
@Component({
  selector: 'app-manage-machines-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatListModule, MatProgressSpinnerModule],
  template: `
    <h2 mat-dialog-title>Machines in {{ group.name }}</h2>
    <mat-dialog-content>
      @if (loading()) {
        <div class="state"><mat-spinner diameter="40"></mat-spinner></div>
      } @else if (error()) {
        <div class="state">{{ error() }}</div>
      } @else if (machines().length === 0) {
        <div class="state">No machines have checked in yet.</div>
      } @else {
        <mat-selection-list (selectionChange)="onSelectionChange($event)">
          @for (machine of machines(); track machine.id) {
            <mat-list-option [value]="machine.id" [selected]="selected().has(machine.id)">
              {{ machine.name }}
            </mat-list-option>
          }
        </mat-selection-list>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <span class="count">{{ selected().size }} selected</span>
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-raised-button color="primary" [disabled]="loading() || !!error()" (click)="save()">Save</button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content { min-height: 200px; min-width: 400px; }
    .state { display: flex; justify-content: center; padding: 40px 20px; color: var(--gh-muted); }
    .count { margin-right: auto; color: var(--gh-muted); font-size: 13px; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMachinesDialogComponent implements OnInit {
  protected readonly machines = signal<Machine[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly selected = signal(new Set<string>());
  protected readonly group: MachineGroup;

  private readonly machineService = inject(MachineService);
  private readonly dialogRef = inject(MatDialogRef<ManageMachinesDialogComponent, string[]>);

  constructor(@Inject(MAT_DIALOG_DATA) data: { group: MachineGroup }) {
    this.group = data.group;
    this.selected.set(new Set((data.group.groupMachines ?? []).map(gm => gm.machineId)));
  }

  ngOnInit(): void {
    this.machineService.getMachines().subscribe({
      next: machines => {
        this.machines.set([...machines].sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '')));
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err.message || 'Failed to load machines');
        this.loading.set(false);
      }
    });
  }

  protected onSelectionChange(event: MatSelectionListChange): void {
    this.selected.set(new Set(event.source.selectedOptions.selected.map(o => o.value as string)));
  }

  protected save(): void {
    this.dialogRef.close([...this.selected()]);
  }
}
