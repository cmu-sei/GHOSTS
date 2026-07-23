import { Component, ChangeDetectionStrategy, input } from '@angular/core';

@Component({
  selector: 'app-glass-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="glass-card" [class.hoverable]="hoverable()" [class.compact]="compact()">
      <ng-content />
    </div>
  `,
  styles: [`
    @use 'variables' as *;
    @use 'glass' as *;

    .glass-card {
      @include glass-panel;
      padding: 20px;

      &.hoverable {
        @include glass-card;
      }

      &.compact {
        padding: 12px 16px;
      }
    }
  `]
})
export class GlassCardComponent {
  hoverable = input(false);
  compact = input(false);
}
