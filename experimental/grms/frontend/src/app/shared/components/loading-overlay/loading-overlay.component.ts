import { Component, ChangeDetectionStrategy } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-loading-overlay',
  standalone: true,
  imports: [MatProgressSpinnerModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="loading-overlay">
      <mat-spinner diameter="40" strokeWidth="3"></mat-spinner>
      <span class="loading-text">Processing...</span>
    </div>
  `,
  styles: [`
    @use 'variables' as *;

    .loading-overlay {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 16px;
      padding: 40px;
    }

    .loading-text {
      font-size: 0.8rem;
      color: $text-mute;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
  `]
})
export class LoadingOverlayComponent {}
