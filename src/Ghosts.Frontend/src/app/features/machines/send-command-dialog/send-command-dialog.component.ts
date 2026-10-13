import { ChangeDetectionStrategy, Component, Inject, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Machine, SendCommandsRequest, CommandShell } from '../../../core/models';

@Component({
  selector: 'app-send-command-dialog',
  standalone: true,
  imports: [FormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>Send Command to {{ machine.name }}</h2>
    <mat-dialog-content>
      <mat-form-field class="full-width">
        <mat-label>Shell</mat-label>
        <mat-select [(ngModel)]="shell">
          <mat-option value="Bash">Bash</mat-option>
          <mat-option value="Command">Command (cmd.exe)</mat-option>
          <mat-option value="PowerShell">PowerShell</mat-option>
        </mat-select>
      </mat-form-field>

      <mat-form-field class="full-width" subscriptSizing="dynamic">
        <mat-label>Commands</mat-label>
        <textarea matInput [(ngModel)]="text" rows="6" class="commands" placeholder="One command per line"></textarea>
        <mat-hint>Runs once, in order. Output arrives with the machine's normal results reporting.</mat-hint>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button
        mat-raised-button
        color="primary"
        [disabled]="commands().length === 0"
        (click)="send()">
        <i class="fas fa-terminal"></i>
        Send
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content {
      min-width: 400px;
    }

    .full-width {
      width: 100%;
    }

    .commands {
      font-family: monospace;
    }

    mat-dialog-actions {
      padding: 16px 24px 24px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SendCommandDialogComponent {
  protected readonly machine: Machine;
  protected shell: CommandShell = 'Bash';
  protected text = '';

  private readonly dialogRef = inject(MatDialogRef<SendCommandDialogComponent>);

  constructor(@Inject(MAT_DIALOG_DATA) data: { machine: Machine }) {
    this.machine = data.machine;
  }

  protected commands(): string[] {
    return this.text.split('\n').filter(line => line.trim());
  }

  protected send(): void {
    const request: SendCommandsRequest = { shell: this.shell, commands: this.commands() };
    this.dialogRef.close(request);
  }
}
