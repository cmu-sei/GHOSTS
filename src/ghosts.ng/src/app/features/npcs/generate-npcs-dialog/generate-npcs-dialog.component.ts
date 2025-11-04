import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { NpcService } from '../../../core/services';

export interface GenerateNpcsConfig {
  campaign: string;
  enclave: string;
  team: string;
  number: number;
}

@Component({
  selector: 'app-generate-npcs-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  template: `
    <h2 mat-dialog-title>Generate NPCs</h2>
    <mat-dialog-content>
      <p class="description">Generate a group of NPCs for your campaign.</p>

      @if (error()) {
        <div class="error-message">
          <i class="fas fa-exclamation-circle"></i>
          <span>{{ error() }}</span>
        </div>
      }

      <mat-form-field class="full-width">
        <mat-label>Campaign</mat-label>
        <input matInput [(ngModel)]="config.campaign" placeholder="e.g., Exercise 2025" required>
      </mat-form-field>

      <mat-form-field class="full-width">
        <mat-label>Enclave</mat-label>
        <input matInput [(ngModel)]="config.enclave" placeholder="e.g., Brigade Alpha" required>
      </mat-form-field>

      <mat-form-field class="full-width">
        <mat-label>Team</mat-label>
        <input matInput [(ngModel)]="config.team" placeholder="e.g., Engineering" required>
      </mat-form-field>

      <mat-form-field class="full-width">
        <mat-label>Number of NPCs</mat-label>
        <input matInput type="number" [(ngModel)]="config.number" min="1" max="100" required>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()" [disabled]="generating()">Cancel</button>
      <button mat-raised-button color="primary" (click)="generate()" [disabled]="!isValid() || generating()">
        @if (generating()) {
          <mat-spinner diameter="20"></mat-spinner>
          <span>Generating...</span>
        } @else {
          <i class="fas fa-users"></i>
          <span>Generate NPCs</span>
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content {
      min-width: 400px;
      padding: 24px 24px 0 24px;
    }

    .description {
      margin: 0 0 24px 0;
      color: rgba(0, 0, 0, 0.6);
    }

    .full-width {
      width: 100%;
      margin-bottom: 16px;
    }

    .error-message {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px;
      margin-bottom: 16px;
      background: #ffebee;
      border-radius: 4px;
      color: #c62828;

      i {
        font-size: 18px;
      }
    }

    mat-dialog-actions {
      padding: 16px 24px;

      button {
        margin-left: 8px;

        mat-spinner {
          display: inline-block;
          margin-right: 8px;
        }

        i {
          margin-right: 6px;
        }
      }
    }
  `]
})
export class GenerateNpcsDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<GenerateNpcsDialogComponent>);
  private readonly npcService = inject(NpcService);

  protected config: GenerateNpcsConfig = {
    campaign: '',
    enclave: '',
    team: '',
    number: 10
  };

  protected generating = signal(false);
  protected error = signal<string | null>(null);

  protected isValid(): boolean {
    return !!(
      this.config.campaign?.trim() &&
      this.config.enclave?.trim() &&
      this.config.team?.trim() &&
      this.config.number > 0 &&
      this.config.number <= 100
    );
  }

  protected generate(): void {
    if (!this.isValid()) {
      return;
    }

    this.generating.set(true);
    this.error.set(null);

    this.npcService.generateNpcs({
      campaign: this.config.campaign,
      enclave: this.config.enclave,
      team: this.config.team,
      number: this.config.number
    }).subscribe({
      next: (npcs) => {
        this.generating.set(false);
        this.dialogRef.close(npcs);
      },
      error: (err) => {
        this.generating.set(false);
        this.error.set(err.message || 'Failed to generate NPCs');
      }
    });
  }

  protected cancel(): void {
    this.dialogRef.close();
  }
}
