import { ChangeDetectionStrategy, Component, Inject, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Machine } from '../../../core/models';

@Component({
  selector: 'app-machine-json-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatSnackBarModule],
  template: `
    <h2 mat-dialog-title>Machine JSON</h2>
    <mat-dialog-content>
      <pre>{{ json }}</pre>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="copyJson()">
        <i class="fas fa-copy"></i>
        Copy
      </button>
      <span class="spacer"></span>
      <button mat-stroked-button mat-dialog-close color="primary">Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content {
      max-height: 60vh;
      overflow: auto;
      background: #0f172a;
      color: #e2e8f0;
      border-radius: 6px;
      padding: 16px;
      font-family: 'Fira Code', 'Menlo', monospace;
      font-size: 13px;
      line-height: 1.5;
    }

    pre {
      margin: 0;
      white-space: pre-wrap;
      word-break: break-word;
    }

    mat-dialog-actions {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 16px 24px 24px;
    }

    .spacer {
      flex: 1 1 auto;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MachineJsonDialogComponent {
  protected readonly json: string;

  private readonly snackBar = inject(MatSnackBar);

  constructor(@Inject(MAT_DIALOG_DATA) machine: Machine) {
    this.json = JSON.stringify(machine, null, 2);
  }

  protected async copyJson(): Promise<void> {
    try {
      if (!navigator?.clipboard?.writeText) {
        throw new Error('Clipboard API unavailable');
      }
      await navigator.clipboard.writeText(this.json);
      this.snackBar.open('Machine JSON copied', undefined, { duration: 2500 });
    } catch {
      this.snackBar.open('Unable to copy JSON', undefined, { duration: 2500 });
    }
  }
}
