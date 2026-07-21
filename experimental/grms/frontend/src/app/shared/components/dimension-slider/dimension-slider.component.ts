import { Component, ChangeDetectionStrategy, input, output, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatSliderModule } from '@angular/material/slider';

@Component({
  selector: 'app-dimension-slider',
  standalone: true,
  imports: [FormsModule, MatSliderModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dimension-slider">
      <div class="labels">
        <span class="low">{{ lowLabel() }}</span>
        <span class="high">{{ highLabel() }}</span>
      </div>
      <mat-slider [min]="min()" [max]="max()" [step]="step()" [disabled]="disabled()">
        <input matSliderThumb [ngModel]="value()" (ngModelChange)="onValueChange($event)" />
      </mat-slider>
      <div class="value-display">{{ value().toFixed(2) }}</div>
    </div>
  `,
  styles: [`
    @use 'variables' as *;

    .dimension-slider {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .labels {
      display: flex;
      justify-content: space-between;
      font-size: 0.7rem;
      color: $text-mute;
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }

    mat-slider {
      width: 100%;
    }

    .value-display {
      text-align: center;
      font-size: 0.75rem;
      font-family: 'JetBrains Mono', monospace;
      color: $accent;
    }
  `]
})
export class DimensionSliderComponent {
  value = model(0);
  lowLabel = input('Low');
  highLabel = input('High');
  min = input(-1);
  max = input(1);
  step = input(0.05);
  disabled = input(false);

  onValueChange(val: number): void {
    this.value.set(val);
  }
}
